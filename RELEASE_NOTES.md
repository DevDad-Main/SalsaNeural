## SalsaNeural

First release — GPL-3.0 fork of [RHI](https://github.com/RankFTW/RHI) with all administrator-elevation paths disabled for GeForce NOW / SalsaNOW desktop mode.

### Highlights

- **No admin, ever** — Admin Mode, scheduled tasks, `runas`, and HKLM writes removed. No UAC prompts.
- **Portable zip** for `I:\Apps\SalsaNOW\SalsaNeural\` — self-contained, zero prerequisites.
- **Per-user installer** (`PrivilegesRequired=lowest`) for normal PCs; suggests the SalsaNOW folder when the `I:` drive is present.
- **Everything that works in game folders stays:** DLSS5 Feeder, DLSS5 Tool, ReShade, RenoDX HDR, OptiScaler, shader packs, DLSS version manager, HDR auto-toggle.
- **Unavailable with a friendly message:** Vulkan ReShade, ReBAR, Smooth Motion, Low Latency Ultra, driver export/import, DSR/DLDSR.

### Notes

- Install games to the `I:` persistent-storage drive; other drives are ~60 GB and ephemeral.
- App data: `%LOCALAPPDATA%\SalsaNeural` (persists on GFN).
- Auto-start: add to `I:\Apps\SalsaNOW\StartupBatch.bat`:
  `start "" "I:\Apps\SalsaNOW\SalsaNeural\SalsaNeural.exe"`
