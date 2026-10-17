using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RenoDXCommander.Services;

/// <summary>
/// Checks GitHub Releases for a newer version of SalsaNeural and applies it (installer or portable zip) if requested.
/// </summary>
public class UpdateService : IUpdateService
{
    private readonly HttpClient _http;
    private readonly GitHubETagCache _etagCache;

    public UpdateService(HttpClient http, GitHubETagCache etagCache)
    {
        _http = http;
        _etagCache = etagCache;
    }
    // GitHub API endpoint for the latest SalsaNeural release (tags like "v0.2.0").
    // The updater ONLY ever consults this fork's own releases — never upstream RHI,
    // so an update can never replace SalsaNeural with vanilla RHI.
    private const string LatestReleaseApiUrl =
        "https://api.github.com/repos/DevDad-Main/SalsaNeural/releases/latest";

    // The asset filenames to look for when updating. Preference order is adjusted at
    // runtime by install type (portable zip vs per-user installer) in FetchReleaseAsync.
    private static readonly string[] InstallerFileNames = ["SalsaNeural-Setup.exe", "SalsaNeural-Portable.zip"];
    private static readonly string[] PortableFileNames  = ["SalsaNeural-Portable.zip", "SalsaNeural-Setup.exe"];

    /// <summary>
    /// True when the running EXE is NOT under %LOCALAPPDATA%\Programs — i.e. the user
    /// launched from the portable zip. Portable installs prefer the zip asset so the
    /// update replaces files in place; installed copies prefer the installer.
    /// </summary>
    internal static bool IsPortableInstall
    {
        get
        {
            try
            {
                var exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe)) return true;
                var programs = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs");
                return !exe.StartsWith(programs, StringComparison.OrdinalIgnoreCase);
            }
            catch { return true; }
        }
    }

    private static string[] PreferredAssets =>
        IsPortableInstall ? PortableFileNames : InstallerFileNames;

    /// <summary>
    /// Returns the current app version from the assembly metadata (set via .csproj AssemblyVersion).
    /// </summary>
    public Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);

    /// <summary>
    /// Silently checks GitHub for a newer release. When <paramref name="betaOptIn"/> is true,
    /// queries both the stable and beta endpoints and uses <see cref="VersionResolver"/> to
    /// pick the winning version. Returns the parsed remote version and the download URL for
    /// the installer asset, or null if no update is available or the check fails.
    /// </summary>
    public async Task<UpdateInfo?> CheckForUpdateAsync(bool betaOptIn = false)
    {
        // Beta channel is intentionally unused in SalsaNeural — stable releases only.
        _ = betaOptIn;

        try
        {
            // Only ever check this fork's own releases (see LatestReleaseApiUrl).
            var stable = await FetchReleaseAsync(LatestReleaseApiUrl).ConfigureAwait(false);

            var current = CurrentVersion;
            var currentRdxc = new RdxcVersion(current.Major, current.Minor, current.Build,
                current.Revision > 0 ? (int?)current.Revision : null);

            RdxcVersion? winner = VersionResolver.Resolve(currentRdxc, stable?.version, null);

            if (winner == null)
            {
                CrashReporter.Log($"[UpdateService.CheckForUpdateAsync] Up to date (local={currentRdxc.ToDisplayString()})");
                return null;
            }

            string? winnerDownloadUrl = stable.HasValue && winner.Value == stable.Value.version
                ? stable.Value.downloadUrl
                : null;

            if (string.IsNullOrEmpty(winnerDownloadUrl))
            {
                CrashReporter.Log($"[UpdateService.CheckForUpdateAsync] Winner {winner.Value.ToDisplayString()} has no download URL");
                return null;
            }

            CrashReporter.Log($"[UpdateService.CheckForUpdateAsync] Update available {currentRdxc.ToDisplayString()} → {winner.Value.ToDisplayString()}");
            return new UpdateInfo
            {
                CurrentVersion = new Version(current.Major, current.Minor, current.Build),
                RemoteVersion  = new Version(winner.Value.Major, winner.Value.Minor, winner.Value.Build),
                DownloadUrl    = winnerDownloadUrl,
                DisplayVersion = winner.Value.ToDisplayString(),
            };
        }
        catch (Exception ex)
        {
            // Update check should never crash the app — swallow and log
            CrashReporter.Log($"[UpdateService.CheckForUpdateAsync] Check failed — {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Fetches a single GitHub release endpoint, parses the version with <see cref="RdxcVersion.TryParse"/>,
    /// and extracts the installer download URL. Returns null if the endpoint fails or the version cannot be parsed.
    /// </summary>
    private async Task<(RdxcVersion version, string downloadUrl)?> FetchReleaseAsync(string apiUrl)
    {
        var json = await _etagCache.GetWithETagAsync(_http, apiUrl, $"SalsaNeural/{CurrentVersion}").ConfigureAwait(false);
        if (json == null)
        {
            CrashReporter.Log($"[UpdateService.FetchReleaseAsync] GitHub API returned error for {apiUrl}");
            return null;
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Parse version from release name or tag_name using RdxcVersion
        var releaseName = root.TryGetProperty("name", out var name) ? name.GetString() : null;
        var tagName = root.TryGetProperty("tag_name", out var tag) ? tag.GetString() : null;

        if (!RdxcVersion.TryParse(releaseName, out var version) &&
            !RdxcVersion.TryParse(tagName, out version))
        {
            CrashReporter.Log($"[UpdateService.FetchReleaseAsync] Could not parse version from tag='{tagName}' name='{releaseName}'");
            return null;
        }

        // Find the download asset (portable installs prefer the zip, installed copies
        // prefer the setup exe; the other asset remains a fallback).
        string? downloadUrl = null;
        var preferred = PreferredAssets;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var assetName in preferred)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var n = asset.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                    if (string.Equals(n, assetName, StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.TryGetProperty("browser_download_url", out var url)
                            ? url.GetString() : null;
                        break;
                    }
                }
                if (!string.IsNullOrEmpty(downloadUrl)) break;
            }
        }

        if (string.IsNullOrEmpty(downloadUrl))
        {
            CrashReporter.Log($"[UpdateService.FetchReleaseAsync] Version {version.ToDisplayString()} found but no update asset (tried {string.Join(", ", preferred)})");
            return null;
        }

        return (version, downloadUrl);
    }

    /// <summary>
    /// Downloads the installer to a temp file and returns the file path.
    /// Reports progress via the optional callback.
    /// </summary>
    public async Task<string?> DownloadInstallerAsync(
        string downloadUrl,
        IProgress<(string msg, double pct)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string? tempPath = null;
        try
        {
            // HttpClient.Timeout ends at the headers with ResponseHeadersRead.
            // Apply a deadline to the body reads/writes as well as shutdown cancellation.
            using var transfer = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            transfer.CancelAfter(TimeSpan.FromMinutes(10));
            var token = transfer.Token;
            progress?.Report(("Downloading update...", 0));

            using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("SalsaNeural", CurrentVersion.ToString()));

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            // Derive the installer filename from the download URL, falling back to RHI-Setup.exe
            var fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
            if (string.IsNullOrEmpty(fileName)) fileName = PreferredAssets[0];
            tempPath = Path.Combine(Path.GetTempPath(), fileName);

            await using var contentStream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 1024 * 1024, useAsync: true);

            var buffer = new byte[1024 * 1024]; // 1 MB
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), token).ConfigureAwait(false);
                totalRead += bytesRead;

                if (totalBytes > 0)
                {
                    var pct = (double)totalRead / totalBytes * 100;
                    progress?.Report(($"Downloading update... {pct:F0}%", pct));
                }
            }

            if (totalBytes >= 0 && totalRead != totalBytes)
                throw new IOException($"Incomplete installer download: expected {totalBytes} bytes, received {totalRead}.");
            progress?.Report(("Download complete.", 100));
            CrashReporter.Log($"[UpdateService.DownloadInstallerAsync] Downloaded installer to {tempPath} ({totalRead:N0} bytes)");
            return tempPath;
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[UpdateService.DownloadInstallerAsync] Download failed — {ex.Message}");
            if (tempPath != null)
            {
                try { File.Delete(tempPath); }
                catch (Exception cleanupError) { CrashReporter.Log($"[UpdateService] Partial download cleanup failed — {cleanupError.Message}"); }
            }
            progress?.Report(($"Download failed: {ex.Message}", 0));
            return null;
        }
    }

    /// <summary>
    /// Applies a downloaded update and requests the app to close.
    /// .exe assets are launched as the per-user installer; .zip assets (portable
    /// installs) are replaced in place by a background script that waits for this
    /// process to exit, extracts over the install directory, and relaunches.
    /// </summary>
    public void LaunchInstallerAndExit(string downloadedFilePath, Action closeApp)
    {
        try
        {
            if (downloadedFilePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                LaunchPortableUpdateScript(downloadedFilePath, closeApp);
                return;
            }

            CrashReporter.Log($"[UpdateService.LaunchInstaller] Launching installer {downloadedFilePath}");
            Process.Start(new ProcessStartInfo
            {
                FileName        = downloadedFilePath,
                // Inno per-user installer: silent with a progress bar, no restart prompt.
                Arguments       = "/SILENT /SUPPRESSMSGBOXES /NORESTART",
                UseShellExecute = true,
            });

            // Give the installer a moment to start, then close the app
            closeApp();
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[UpdateService.LaunchInstaller] Failed to launch update — {ex.Message}");
        }
    }

    /// <summary>
    /// Spawns a hidden PowerShell script that waits for this process to exit, extracts
    /// the portable zip over the current install directory, and relaunches the app.
    /// </summary>
    private void LaunchPortableUpdateScript(string zipPath, Action closeApp)
    {
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath)
                     ?? AppContext.BaseDirectory;
        var exeName = Path.GetFileName(Environment.ProcessPath ?? "SalsaNeural.exe");
        var pid = Environment.ProcessId;

        var scriptPath = Path.Combine(Path.GetTempPath(), $"salsaneural_update_{pid}.ps1");

        // The zip contains a top-level SalsaNeural/ folder (or the exe may sit at its
        // root) — locate the extracted directory that actually contains the EXE so a
        // renamed local folder doesn't break the replace.
        var script = $$"""
            $ErrorActionPreference = 'Stop'
            $zip      = '{{zipPath.Replace("'", "''")}}'
            $target   = '{{exeDir.Replace("'", "''")}}'
            $exeName  = '{{exeName.Replace("'", "''")}}'
            $pid      = {{pid}}

            # Wait for the running app to exit (files unlocked)
            while (Get-Process -Id $pid -ErrorAction SilentlyContinue) { Start-Sleep -Milliseconds 750 }

            $stage = Join-Path $env:TEMP ("salsaneural_stage_" + $pid)
            try {
                Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
                Expand-Archive -LiteralPath $zip -DestinationPath $stage -Force

                $exe = Get-ChildItem -Path $stage -Recurse -Filter $exeName -File | Select-Object -First 1
                if (-not $exe) { throw "SalsaNeural.exe not found in update archive" }

                Copy-Item -Path (Join-Path $exe.DirectoryName '*') -Destination $target -Recurse -Force
            }
            finally {
                Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
                Remove-Item -LiteralPath $zip -Force -ErrorAction SilentlyContinue
            }

            Start-Process -FilePath (Join-Path $target $exeName) -WorkingDirectory $target
            """;

        File.WriteAllText(scriptPath, script);
        CrashReporter.Log($"[UpdateService.LaunchPortableUpdateScript] Spawning replace script {scriptPath} for {zipPath} → {exeDir}");

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
            UseShellExecute = true,
        });

        closeApp();
    }
}

/// <summary>
/// Contains information about an available update.
/// </summary>
public class UpdateInfo
{
    public required Version CurrentVersion { get; init; }
    public required Version RemoteVersion  { get; init; }
    public required string  DownloadUrl    { get; init; }
    public string? DisplayVersion { get; init; }
}

/// <summary>
/// Represents a RHI version with optional beta suffix.
/// Examples: "1.4.8", "1.4.8-beta1", "1.4.8 beta 1"
/// </summary>
public readonly record struct RdxcVersion(int Major, int Minor, int Build, int? BetaNumber = null)
    : IComparable<RdxcVersion>
{
    /// <summary>True when this version has a beta suffix.</summary>
    public bool IsBeta => BetaNumber.HasValue;

    /// <summary>Base version tuple without beta suffix, used for priority comparison.</summary>
    public (int, int, int) BaseVersion => (Major, Minor, Build);

    /// <summary>
    /// Compares by (Major, Minor, Build) first, then by beta status:
    /// stable (no beta) is greater than any beta at the same base version,
    /// and a higher beta number is greater than a lower one.
    /// </summary>
    public int CompareTo(RdxcVersion other)
    {
        int cmp = Major.CompareTo(other.Major);
        if (cmp != 0) return cmp;

        cmp = Minor.CompareTo(other.Minor);
        if (cmp != 0) return cmp;

        cmp = Build.CompareTo(other.Build);
        if (cmp != 0) return cmp;

        // Same base version — stable > beta, higher beta > lower beta
        if (!IsBeta && !other.IsBeta) return 0;
        if (!IsBeta && other.IsBeta) return 1;   // stable > beta
        if (IsBeta && !other.IsBeta) return -1;  // beta < stable

        // Both are beta — compare beta numbers
        return BetaNumber!.Value.CompareTo(other.BetaNumber!.Value);
    }

    // Regex: optional leading text (e.g. "HDRX-", "v"), then Major.Minor.Build, optional beta suffix
    private static readonly Regex VersionPattern = new(
        @"(\d+)\.(\d+)\.(\d+)(?:[\s-]*beta[\s-]*(\d+))?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Parses version strings such as "1.4.8", "1.4.8-beta1", "1.4.8 beta 1",
    /// "v1.4.8-beta2", "RHI-1.4.8-beta1". Returns false and logs when input is invalid.
    /// </summary>
    public static bool TryParse(string? input, out RdxcVersion version)
    {
        version = default;

        if (string.IsNullOrWhiteSpace(input))
        {
            CrashReporter.Log($"[RdxcVersion.TryParse] Cannot parse null or empty version string");
            return false;
        }

        var match = VersionPattern.Match(input);
        if (!match.Success)
        {
            CrashReporter.Log($"[RdxcVersion.TryParse] Cannot parse version from '{input}'");
            return false;
        }

        int major = int.Parse(match.Groups[1].Value);
        int minor = int.Parse(match.Groups[2].Value);
        int build = int.Parse(match.Groups[3].Value);
        int? betaNumber = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : null;

        version = new RdxcVersion(major, minor, build, betaNumber);
        return true;
    }

    /// <summary>
    /// Formats to display string: "1.4.8" for stable, "1.4.8 beta 1" for beta.
    /// </summary>
    public string ToDisplayString() =>
        IsBeta ? $"{Major}.{Minor}.{Build} beta {BetaNumber}" : $"{Major}.{Minor}.{Build}";
}

/// <summary>
/// Determines which update (if any) to offer based on current version,
/// latest stable, and latest beta.
/// </summary>
internal static class VersionResolver
{
    /// <summary>
    /// Returns the version to offer, or null if up to date.
    /// Rules:
    ///   - Stable always wins over beta at same or higher base version
    ///   - Beta only offered when its base version exceeds both latest stable's
    ///     and current version's base version
    ///   - Only offer if candidate's base version > current base version
    /// </summary>
    public static RdxcVersion? Resolve(
        RdxcVersion current,
        RdxcVersion? latestStable,
        RdxcVersion? latestBeta)
    {
        var currentBase = current.BaseVersion;

        // Determine if stable is a valid candidate (base version > current base version)
        bool stableValid = latestStable.HasValue
            && latestStable.Value.BaseVersion.CompareTo(currentBase) > 0;

        // Determine if beta is a valid candidate:
        //   - base version > current base version, OR
        //   - same base version but current is a beta with a lower beta number
        //   - base version > latest stable base version (beta only wins when ahead of stable)
        bool betaValid = latestBeta.HasValue
            && (latestBeta.Value.BaseVersion.CompareTo(currentBase) > 0
                || (latestBeta.Value.BaseVersion == currentBase
                    && current.IsBeta
                    && latestBeta.Value.CompareTo(current) > 0))
            && (!latestStable.HasValue
                || latestBeta.Value.BaseVersion.CompareTo(latestStable.Value.BaseVersion) > 0);

        // Beta takes priority when it's ahead of both stable and current
        if (betaValid)
            return latestBeta;

        if (stableValid)
            return latestStable;

        return null;
    }
}
