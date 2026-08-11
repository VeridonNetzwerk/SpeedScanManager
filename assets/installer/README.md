# SpeedScan Manager — Installer Assets

This directory holds bitmap assets used by the NSIS installer (`build/installer.nsi`).

## Files

| File | Purpose |
|------|---------|
| `SpeedScanManager_Logo.bmp` | Logo in the sidebar (top-left, dark area) |
| `SpeedScanManager_Text.bmp` | Text logo (reserved for future use) |
| `github_logo.bmp` | GitHub icon (bottom-right of installer pages) |
| `discord_logo.bmp` | Discord icon (bottom-right of installer pages) |

## Notes

- NSIS `nsDialogs` requires **BMP format** (not PNG/SVG).
- Files are loaded at runtime from `$EXEDIR\assets\installer\`.
- If a file is missing, the installer simply skips the image — no crash.
