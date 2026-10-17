# SalsaNeural

**Neural rendering, HDR, and ReShade management for GeForce NOW / SalsaNOW — no administrator privileges, ever.**

SalsaNeural is a GPL-3.0 fork of [RankFTW's RHI](https://github.com/RankFTW/RHI) with every elevation path disabled. It is built for sessions where admin is impossible: GeForce NOW with [SalsaNOW](https://github.com/dpadGuy/SalsaNOW) desktop mode, kiosk accounts, and locked-down PCs.

## Download

Get the latest build from [Releases](https://github.com/DevDad-Main/SalsaNeural/releases):

| File | Use it when |
| --- | --- |
| `SalsaNeural-Portable.zip` | **GeForce NOW / SalsaNOW.** Extract to `I:\Apps\SalsaNOW\SalsaNeural\` and run `SalsaNeural.exe`. No install, no prerequisites. |
| `SalsaNeural-Setup.exe` | Normal PC, or prefer a Start-menu install. Per-user Inno installer — **no UAC prompt**. |

Windows SmartScreen may warn about unsigned apps. Choose **More info → Run anyway**.

## GeForce NOW / SalsaNOW quick start

1. Subscribe to GFN persistent storage (the `I:` drive).
2. Extract `SalsaNeural-Portable.zip` to `I:\Apps\SalsaNOW\SalsaNeural\`.
3. Install your games **to the `I:` drive** (other drives are ~60 GB and ephemeral).
4. Run `SalsaNeural.exe`. Add games (drag & drop the game `.exe` or let auto-detection find Steam libraries on `I:`).
5. Optional — auto-start every session: add this line to `I:\Apps\SalsaNOW\StartupBatch.bat`:
   ```bat
   start "" "I:\Apps\SalsaNOW\SalsaNeural\SalsaNeural.exe"
   ```
6. Install the **DLSS5 Feeder** (or DLSS5 Tool) method for your game from the Neural Rendering section, then launch.

App settings and logs live in `%LOCALAPPDATA%\SalsaNeural` — AppData persists on GeForce NOW, so your configuration survives sessions.

## What works without admin

- **DLSS5 Feeder / DLSS5 Tool / ShortFuse** — neural rendering for games without native DLSS (DX11, DX12, 32-bit, OpenGL)
- **ReShade** installs into game folders (`dxgi.dll` / `d3d9.dll` / `opengl32.dll`)
- **RenoDX HDR mods**, Luma Framework, UE-extended HDR, DXVK + Lilium HDR
- **OptiScaler** (FSR4/FSR3/XeSS redirection)
- **DLSS / Streamline version manager** — swap DLLs per game, presets, render scale
- **Shader pack manager**, ReLimiter, Display Commander, addon manager
- **HDR auto-toggle** (DisplayConfig APIs)
- Game library detection across Steam / Epic / GOG / Xbox / EA / Ubisoft / Battle.net / Rockstar / itch.io

## What is unavailable (needs admin upstream)

| Feature | Why |
| --- | --- |
| Vulkan ReShade | Writes `C:\ProgramData\ReShade` + HKLM |
| ReBAR, Smooth Motion, Low Latency Ultra | Driver settings require elevation |
| Driver profile export/import | Protected registry keys |
| DSR / DLDSR | Protected registry keys |
| Admin Mode (upstream feature) | Removed entirely |

These show a friendly "unavailable" message instead of a UAC prompt.

## Build

Requirements: Windows, .NET 8 SDK.

```powershell
# Portable (self-contained)
powershell -ExecutionPolicy Bypass -File build.ps1

# Installer (needs Inno Setup 6)
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" SalsaNeural.iss
```

CI builds both artifacts on every `v*` tag — same pattern as [SalsaStats](https://github.com/DevDad-Main/SalsaStats).

## License

**GPL-3.0** (inherited from RHI). See [LICENSE](LICENSE) and [NOTICE](NOTICE).

SalsaNeural is an independent fork. Not affiliated with NVIDIA, GeForce NOW, SalsaNOW, RankFTW, RenoDX, Crosire, or pmnoxx. Use GeForce NOW methods at your own risk.

## Credits

- [RHI](https://github.com/RankFTW/RHI) by RankFTW — the app this fork is based on
- [DLSS5 Feeder](https://github.com/jlrouzies-fr/DLSS5-Feeder) by jlrouzies-fr
- [ReShade](https://reshade.me) by crosire, [RenoDX](https://github.com/clshortfuse/renodx) by clshortfuse, [OptiScaler](https://github.com/optiscaler/OptiScaler), and all upstream component authors
