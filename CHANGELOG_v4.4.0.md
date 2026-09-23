# Changelog — v4.4.0

## Fixes

- **Game Pass installs now detected correctly**: some Game Pass/Windows Store UE4SS builds nest mods under `WinGDK\ue4ss\mods` instead of `WinGDK\Mods`. The app now checks both layouts automatically when validating the game path, resolving the mod folder, reading `mods.txt`/`enabled.txt`, and locating `UE4SS.log` - Game Pass users who previously got stuck on "Game path not configured" despite pointing at the right folder should now go straight through.
- Fixed the movement speed slider writing to disk on every pixel of a drag instead of only on an actual value change.
- Fixed Summonable Traits detection scanning the Mods folder twice back-to-back on every group spawn and trait spawn.

## Improvements

- **Diagnostics now checks UE4SS itself**: `UE4SS.dll`, `UE4SS-settings.ini`, and (on Game Pass) `dwmapi.dll` are verified directly, instead of diagnostics jumping straight to "Mods folder missing" when UE4SS was never installed at all.
- **Copy Report button** in the Diagnostics window - copies the full report to your clipboard so it's easy to paste into Discord when asking for help.
- Diagnostics and the Summonable Traits check no longer run on the UI thread, so opening Diagnostics, spawning a group, or spawning a trait no longer causes a brief freeze - especially noticeable with a large `UE4SS.log`.
- Wiki item images now load up to 6 at a time instead of strictly one after another, so a fresh item catalog populates its images significantly faster.

## Documentation

- Instructions and README rewritten for Game Pass: the `ue4ss\mods` folder layout, the `dwmapi.dll` proxy DLL, and a pinned recommendation of UE4SS v3.0.1 (the rolling "experimental" build on GitHub hasn't been updated since December 2024).
- Added an antivirus/Windows Defender troubleshooting note - `UE4SS.dll` and `AllowModsMod`'s DLL get quarantined often since they hook the game process, and this is a common invisible cause of "nothing works."
- Added a quick way to sanity-check that UE4SS loaded at all (a separate UE4SS console window should appear when the game starts) before chasing mod-specific issues.
- Fixed a stale example of the `Scripts\` folder contents in the setup instructions that no longer matched what actually ships in the release zip.

## Notes

- No user-facing behavior changed for Steam/Epic installs - all of the above either only affects Game Pass path resolution or is a pure performance/internal fix.
