using System.Diagnostics;

namespace RenoDXCommander.Services;

/// <summary>
/// SalsaNeural fork configuration. The app never elevates: no UAC prompts, no scheduled
/// tasks, no runas, no HKLM writes. Targets GeForce NOW / SalsaNOW desktop sessions where
/// the user is always unprivileged (kiosk account, no admin).
/// </summary>
public static class NoAdminMode
{
    /// <summary>Forced on. Every elevation path in the app is disabled.</summary>
    public static readonly bool Enabled = true;

    /// <summary>Elevation can never be attempted in this build.</summary>
    public static bool CanElevate => false;

    /// <summary>
    /// Never starts an elevated process. Callers must treat a false return as
    /// "feature unavailable without admin" and surface a friendly message.
    /// </summary>
    public static bool TryRunElevated(ProcessStartInfo psi)
    {
        _ = psi;
        CrashReporter.Log("[NoAdminMode] Elevation blocked — SalsaNeural never runs elevated");
        return false;
    }

    /// <summary>
    /// Throws IOException when elevation would have been required. Used by elevated-copy
    /// fallbacks so the original UnauthorizedAccessException flow degrades cleanly.
    /// </summary>
    public static void ThrowElevationBlocked(string feature)
    {
        CrashReporter.Log($"[NoAdminMode] {feature} requires administrator privileges — unavailable");
        throw new IOException(
            $"{feature} requires administrator privileges and is unavailable in SalsaNeural.");
    }

    // ── Game-folder write preflight ──────────────────────────────────────────────

    /// <summary>
    /// Returns true when the directory is writable by the current user.
    /// On GeForce NOW / SalsaNOW, game folders must live on the persistent-storage
    /// drive (usually I:) — other drives and Program Files are read-only.
    /// </summary>
    public static bool IsDirectoryWritable(string directory)
    {
        try
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return false;

            var probe = Path.Combine(directory, $".salsaneural_write_probe_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "probe");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Friendly message shown when a game folder rejects writes.</summary>
    public const string ReadOnlyGameFolderMessage =
        "This game folder is read-only. On GeForce NOW / SalsaNOW, reinstall the game to " +
        "your persistent storage drive (usually I:), then try again.";

    /// <summary>Friendly message for features that need admin (Vulkan layer, ReBAR, etc.).</summary>
    public const string AdminFeatureUnavailableMessage =
        "This feature requires administrator privileges and is unavailable in SalsaNeural " +
        "(GeForce NOW sessions never have admin).";
}
