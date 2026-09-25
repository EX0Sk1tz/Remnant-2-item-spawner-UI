# Install Wizard + Self-Install — Implementation Plan

Handoff for a fresh session. To start: *"Read `INSTALL_WIZARD_PLAN.md` and implement it, phase by phase."*
Also read `ARCHITECTURE.md` (especially the Lua bridge section and the threading rule) before touching anything.

---

## 1. Goal

The app installs itself and everything the mod needs, so a new user never extracts folders or edits text files:

1. **Self-install:** on first start, a dialog asks where to install the app (default `%LocalAppData%\Programs\Remnant2Unlocker`) with a checkbox "Create desktop shortcut". Option to keep running portable instead.
2. **Install wizard:** finds the game, installs/updates the Remnant2Unlocker mod from files built into the exe, patches the summon command, enables the mods in `mods.txt`, installs the one required Nexus download from a zip the user drops in, checks for antivirus removal, and confirms the bridge works with a test launch.
3. **Repair/update:** the same wizard is reachable from Settings, and the app keeps the mod files in the game in sync with its own version automatically (replaces the blind copy in `AppUpdateService`).

## 2. Decisions already made by the user (don't re-ask)

- Nexus downloads: **user downloads manually, then drops the zip onto the wizard**; the wizard does the rest. No bundling of third-party mods, no Nexus API.
- `items.json` and **all** other files in the mod folder are **app-owned**. Users are not expected to edit anything. Overwrite them on install/update (with backup).
- The app must show **where it will be installed** and offer **"Create desktop shortcut"**.
- **Only "Allow Asset Mods" is required** from Nexus. The "Cheat Mod" is no longer a dependency.
- The quantity/level-aware `summon` (see §4.3) is the user's own modification → **Remnant2Unlocker ships it** and installs it into UE4SS's `ConsoleCommandsMod`. Chosen over re-implementing summon inside our mod because this exact file is what runs in-game today and is proven stable.
- Summonable Traits stays **optional** (only for trait items).

## 3. Repo state when this plan was written (2026-09-26)

- All earlier work (collection tracking / "Missing only" / Spawn Missing, DLC filter + badges, card layout, tests project, Lua bridge fixes) is **committed**, together with this plan.
- The app-owned files in `Lua Scripts inside game directory/` are **tracked** now (`Scripts/**`, `items.json`, `enabled.txt`, `ConsoleCommandsMod/**`); local sample/runtime files there (`cheats.json`, `hotkeys.json`) stay ignored via `.gitignore`. The test project links files from this folder.
- The repo copy of the mod folder is the source of truth for mod files. It currently contains: `Scripts/**` (incl. `Scripts/WeaponMods/**`), `items.json`, `enabled.txt`, sample `cheats.json`/`hotkeys.json`, and (new) `ConsoleCommandsMod/Scripts/summon_unloaded_assets.lua` (the user's modified summon, copied byte-exact from the game, SHA256 `6D72FC6EF7E1AC4D88206E44FD57DF0E548DCFE587ED5E48A0E5A5D24D9A5597`).
- Tests: `Tests/Remnant2UnlockerApp.Tests` (xUnit + MoonSharp), 97 passing. Main csproj excludes `Tests\**`.
- User preferences (from memory): never add Claude/Anthropic co-author lines to commits/PRs; it's OK to kill a running `Remnant2UnlockerApp.exe` that locks the build output (say so in the reply).

## 4. Facts established by research (verified this session)

### 4.1 What the mod needs in the game folder
- **UE4SS** + **AllowModsMod** (`Mods\AllowModsMod\dlls\main.dll`). Both come in the Nexus package **"Allow Asset Mods"**: <https://www.nexusmods.com/remnant2/mods/2>. Its page says: extract `AMM.zip` into `...\Binaries\Win64` (or `WinGDK`); it bundles an unreleased UE4SS build plus UE4SS's standard mods ("console enabler and a blueprint mod loader"). If a prior version had `xinput1_3.dll`, delete it.
- `ConsoleCommandsMod`, `ConsoleEnablerMod`, `CheatManagerEnablerMod`, `BPModLoaderMod`, `Keybinds`, `shared`, … are **UE4SS standard mods** (verified in RE-UE4SS repo `assets/Mods`). Stock `ConsoleCommandsMod/Scripts/` = `main.lua`, `set.lua`, `dump_object.lua`, `summon_unloaded_assets.lua`; `main.lua` does `require("summon_unloaded_assets")`.
- **Stock** `summon_unloaded_assets.lua` only does `LoadAsset(Parameters[1])` and returns false (plain `summon <path>`, no quantity/level). `spawner.lua` already falls back to plain summon if argument summons spawn nothing — so the stock file *works*, just without stack size / item level.
- **Summonable Traits** (optional): <https://www.nexusmods.com/remnant2/mods/122>. Detected today by `SummonableTraitsService` (a `main.lua` with `RegisterConsoleCommandHandler("SummonTrait"`, `...("AddTrait"`, and `Material_AwardTrait_Base`). On the user's PC it lives in `Mods\SummonableTraits\` (with its own `enabled.txt`).
- The "Cheat Mod" Nexus page (mods/218) is a **different** mod (single `CheatMod` folder); it is **not** needed. Remove it from `Instructions.txt` and Diagnostics fix texts.
- UE4SS v3.0.1 (Game Pass): `https://github.com/UE4SS-RE/RE-UE4SS/releases/download/v3.0.1/UE4SS_v3.0.1.zip` (5,523,402 bytes; other assets are `zDEV-…`, `zCustomGameConfigs.zip`, `zMapGenBP.zip`). MIT licensed. See §9 — optional/untested.

### 4.2 Layouts on disk
- User's Steam install (test machine): `G:\Steam\steamapps\common\Remnant2\Remnant2\Binaries\Win64`. It has **`UE4SS.dll`, `UE4SS-settings.ini`, `dwmapi.dll` directly in Win64** and mods in `Win64\Mods\`. `DiagnosticsService.CheckUE4SSCore` already handles both "UE4SS.dll in root" and "`ue4ss\` subfolder" layouts — reuse it.
- Game Pass: `...\XboxGames\Remnant 2\Content\Remnant2\Binaries\WinGDK`, UE4SS via `dwmapi.dll`, mods often nested in `WinGDK\ue4ss\mods`. `GamePathService.ResolveModsFolder` already picks `Mods\` or `ue4ss\mods\` — **always** go through it.
- User's `Mods\mods.txt` (UE4SS 2.x/3.x format, one `Name : 0|1` per line). It ends with:
  ```
  ; Built-in keybinds, do not move up!
  Keybinds : 1
  ```
  → When adding lines, insert them **before** that comment/`Keybinds` line, never after. Some installs use `enabled.txt` instead of `mods.txt` (Diagnostics checks both).
- Required enabled lines: `AllowModsMod : 1`, `ConsoleCommandsMod : 1`, `ConsoleEnablerMod : 1`, `CheatManagerEnablerMod : 1`, `Remnant2Unlocker : 1` (+ Summonable Traits' folder name if installed). Don't touch other mods' lines except flipping required ones from 0 to 1.

### 4.3 Nexus archive formats
- Nexus files may be `.zip`, `.rar` or `.7z`. The user's `AllowModsMod.rar` is **RAR5** (`52 61 72 21 1A 07 01 00`).
- **Windows 10's built-in `%SystemRoot%\System32\tar.exe` (bsdtar 3.5.2) lists and extracts that RAR5 correctly** (verified byte-identical `main.dll`). So: `.zip` → `System.IO.Compression.ZipFile`; `.rar`/`.7z` → `tar.exe -xf <archive> -C <temp>`; plus **accept an already-extracted folder** as a universal fallback. No third-party NuGet (project policy: none in the app).

### 4.4 Existing code that this feature replaces/uses
- `Views/SetupWizardWindow.xaml(.cs)`: today only an informational window with Skip/Finish; `MainWindow.OnLoadedAsync` opens it when `!IsGamePathValid`. Replace with the real wizard.
- `GamePathService.IsValidGamePath` (→ `IsConfigured`) requires the **mod to already be installed** (`Mods\Remnant2Unlocker\items.json` + `scripts`). The wizard must select/validate the game path with `HasSupportedGameExe` instead, and only then install the mod. `SetWin64Path` persists immediately to `%AppData%\Remnant2UnlockerApp\settings.json` (`UserSettings` has only `Win64Path` today).
- `AppUpdateService.DownloadAndApplyUpdateAsync` → `CopyModScriptsIfPresent` blindly overwrites `Mods\Remnant2Unlocker\Scripts` from the release zip (no backup, no verification) and robocopies the app into `AppContext.BaseDirectory`. Replace the script copy with the new payload sync (Phase 2); keep the app self-update.
- `LocalizationService` loads embedded resources `"{AssemblyName}.Localization.{code}.json"` — same pattern for the mod payload.
- Release zip layout (user's screenshot): `Remnant2Unlocker\` (mod folder), `Changelog.txt`, `Instructions.txt`, `LICENSE.txt`, `Remnant2UnlockerApp.exe` (~158 MB → self-contained **single-file** publish). Keep shipping the folder for manual installs.

## 5. Hard constraints (learned the hard way — read before coding)

- **UE4SS threading rule:** never call `ExecuteInGameThread` from inside an `ExecuteInGameThread` callback, and don't restructure the Lua bridge's threading. Four crash dumps this week were all inside UE4SS's game-thread dispatch; see `ARCHITECTURE.md`. This feature should **not change any Lua bridge behaviour** — it only installs files.
- Batch spawn pacing stays at **500 ms** (`MainViewModel.SpawnMissingDelayMs`).
- Mod scripts are only loaded when the game starts → after installing/updating while the game runs, tell the user to restart the game. Never write into the mod folder while the game is running without warning (files are read at start; `status.json`/`command_queue.json` are live).
- Never delete user/game files that aren't ours; back up everything we overwrite.
- Steam's default location `C:\Program Files (x86)\Steam\...` needs **admin rights** to write. Detect `UnauthorizedAccessException` and offer "Restart as administrator" (`ProcessStartInfo { Verb = "runas", UseShellExecute = true, Arguments = "--setup" }`) rather than running the whole app elevated by default.
- Files written by our code: UTF-8 **without BOM**, keep CRLF for Lua files (copy bytes, don't re-encode).

## 6. Design

### 6.1 Embedded mod payload
- csproj: after the existing `<EmbeddedResource Remove="Lua Scripts inside game directory\**" />`, add explicit includes with stable logical names, e.g.
  ```xml
  <EmbeddedResource Include="Lua Scripts inside game directory\Scripts\**\*.lua;Lua Scripts inside game directory\items.json;Lua Scripts inside game directory\enabled.txt"
                    LogicalName="ModPayload/Remnant2Unlocker/%(RecursiveDir)%(Filename)%(Extension)" />
  <EmbeddedResource Include="Lua Scripts inside game directory\ConsoleCommandsMod\Scripts\summon_unloaded_assets.lua"
                    LogicalName="ModPayload/ConsoleCommandsMod/Scripts/summon_unloaded_assets.lua" />
  ```
  (Check `%(RecursiveDir)` produces `Scripts\...`; normalise `\` → `/` in code. Include `items.json`, `enabled.txt`; do **not** embed sample runtime files `cheats.json`, `hotkeys.json`, `status.json`, `command_queue.json` — the app/mod create those.)
- `Services/ModPayload.cs`: enumerates `GetManifestResourceNames()` with prefix `ModPayload/`, exposes entries `(RelativeTargetPath, Func<Stream> Open, Sha256)`. Target root per top-level folder: `Remnant2Unlocker` → `<Mods>\Remnant2Unlocker\`, `ConsoleCommandsMod` → `<Mods>\ConsoleCommandsMod\` (only if that folder exists = UE4SS installed).
- Unit test: every file under the repo's `Scripts\` is present in the payload (guards csproj globs).

### 6.2 Services (new, pure where possible, unit-tested)
- `GameLocator` — returns candidates `{ Win64Path, Platform (Steam/Epic/GamePass), IsValid }`:
  - Steam: `HKCU\Software\Valve\Steam\SteamPath`, then parse `steamapps\libraryfolders.vdf` (`"path"` entries) → `<lib>\steamapps\common\Remnant2\Remnant2\Binaries\Win64` with `Remnant2-Win64-Shipping.exe`. Put VDF parsing in a pure function (test it).
  - Epic: `%ProgramData%\Epic\EpicGamesLauncher\Data\Manifests\*.item` (JSON; match `DisplayName`/`AppName` containing "Remnant"; use `InstallLocation` + `\Remnant2\Binaries\Win64`).
  - Game Pass: each fixed drive `X:\XboxGames\Remnant 2\Content\Remnant2\Binaries\WinGDK` with `Remnant2-WinGDK-Shipping.exe`.
- `ModsConfig` (pure) — read/modify `mods.txt`/`enabled.txt` text: ensure `Name : 1` for a list of names; flip `: 0` → `: 1`; insert new lines before the `Keybinds` block; preserve everything else, line endings and order; create `mods.txt` if neither exists. Tests with the real file content from §4.2.
- `ModInstaller`:
  - `GetStatus(win64)` → per payload file: UpToDate / Outdated / Missing (SHA256 compare), plus "summon patch applied" (hash of installed `ConsoleCommandsMod\Scripts\summon_unloaded_assets.lua` == payload hash).
  - `InstallOrUpdate(win64)` → back up every file it will replace into `<Mods>\Remnant2Unlocker\Backups\<yyyy-MM-dd_HH-mm-ss>\<relative path>` (keep last ~5 backups), write files atomically (`.tmp` + `File.Move(overwrite)`), verify hashes after writing, update `mods.txt`. Never deletes files (report extras only). Returns a result with per-file outcomes for the UI/log.
  - `RestoreLatestBackup(win64)`.
- `PackageInstaller` — installs a dropped archive or folder:
  - Extract to `%TEMP%\Remnant2Unlocker\pkg-<guid>` (zip via `ZipFile`, rar/7z via `tar.exe`; folders used directly). Always clean up temp.
  - **Allow Asset Mods:** find the package root = shallowest directory containing `Mods\` or `ue4ss\` or `UE4SS.dll` or `dwmapi.dll` (unwrap a single top-level folder). Validate: contains `UE4SS.dll` somewhere **and** `AllowModsMod\dlls\main.dll` somewhere, else error "This doesn't look like Allow Asset Mods (expected UE4SS.dll and AllowModsMod)". Merge-copy root → Win64 (overwrite, back up overwritten files). Afterwards verify with `DiagnosticsService` checks (UE4SS core + AllowModsMod), then re-apply our summon patch (AMM's stock `ConsoleCommandsMod` just overwrote it!) and `mods.txt`.
  - **Summonable Traits:** find a folder whose `Scripts\main.lua` matches the Summonable Traits markers (extract the marker check from `SummonableTraitsService` into a static helper). Copy that folder into `<Mods>\<FolderName>` and enable `<FolderName> : 1`. Error if not found.
  - Also accept an archive of the **wrong** kind with a clear message ("This looks like Summonable Traits — drop it on the Summonable Traits step").
- `AppSelfInstaller`:
  - Is single-file publish? `string.IsNullOrEmpty(typeof(App).Assembly.Location)`. **Only offer self-install for single-file builds** (dev builds from `bin\Debug` must not prompt).
  - Install = copy the running exe (`Environment.ProcessPath`) to `<dir>\Remnant2UnlockerApp.exe`, optionally create desktop shortcut, start the installed copy, shut down this one.
  - Desktop shortcut via COM `WScript.Shell` (`Type.GetTypeFromProgID("WScript.Shell")`, `CreateShortcut(<Desktop>\Remnant 2 Unlocker.lnk)`, set `TargetPath`, `WorkingDirectory`, `IconLocation`, `Save()`); use reflection/`dynamic` (Microsoft.CSharp is in the shared framework).
  - Persist in `UserSettings`: `InstallPromptHandled` (bool) so "Keep portable" isn't asked again; skip prompt when already running from the install dir.
  - After self-install, `AppUpdateService` naturally updates the installed location (it uses `AppContext.BaseDirectory`).

### 6.3 UI
- **First-start install dialog** (before the wizard, single-file builds only): "Install Remnant 2 Unlocker" — install folder (TextBox + Browse, default `%LocalAppData%\Programs\Remnant2Unlocker`), `[x] Create desktop shortcut`, buttons **Install** / **Run without installing**. Handle an existing install in that folder (overwrite = update).
- **Setup wizard** (replaces `SetupWizardWindow`), pages:
  1. **Game** — auto-detected installs as a list ("Steam — G:\…\Win64 ✓"), Browse fallback, platform label. Next disabled until a folder with a supported game exe is chosen.
  2. **Game must be closed** — shown only if `Remnant2-*-Shipping` is running; waits/polls.
  3. **Components** checklist (live status), each row with its action:
     - `UE4SS + AllowModsMod (Allow Asset Mods)` — ✓ or [Open Nexus page] + drop zone / [Choose file…] (accepts .zip/.rar/.7z/folder).
     - `Remnant2Unlocker mod` — ✓ up to date / "N files out of date" → installed automatically.
     - `Stack size & item level support` — patched summon ✓ / applied automatically (needs UE4SS present).
     - `Summonable Traits (optional)` — ✓ / [Open Nexus page] + drop / [Skip].
     - `Mods enabled in mods.txt` — fixed automatically.
     - **[Install / Repair]** runs all automatic parts; show per-step results; on access denied → "Restart as administrator".
  4. **Antivirus check** — ~3 s after installing UE4SS, re-check `UE4SS.dll` (+ `dwmapi.dll` on Game Pass). If gone: explain Windows Defender quarantine, how to restore and add an exclusion (don't try to change Defender settings automatically).
  5. **Test launch** — Steam: `steam://rungameid/1282100` (**verify Remnant II's app id**); others: "Start the game now". Then wait (timeout ~3 min) until `UE4SS.log` shows `[Remnant2Unlocker] main.lua loaded` after the launch time and `status.json` is written/`ready` → "Bridge connected ✓". Allow Skip.
  6. **Done.**
- Entry points: first start when not configured (replace current behaviour in `MainWindow.OnLoadedAsync`); **Settings → "Repair / update installation"**; command-line `--setup` (used by the admin restart).
- **Startup sync:** on every start, if the mod is installed and `ModInstaller.GetStatus` says outdated: game not running → update silently + toast "Mod files updated to match this app version"; game running → toast with action "Update" and "restart the game afterwards". Remove `AppUpdateService.CopyModScriptsIfPresent` (one code path).
- Localize all new texts in `Localization/en.json` + `de.json`; styles for both themes (`Themes/Classic.xaml`, `Themes/Remnant.xaml` must define the same keys).
- Diagnostics: add a check "Stack size & item level support" (patched summon present) as an info-level entry; update fix texts that mention the Cheat Mod to point at Allow Asset Mods / the wizard.

## 7. Implementation phases (do in order; build + test after each)

0. ~~Commit existing work; track the app-owned mod files in git~~ — done (see §3).
1. `ModPayload` + csproj embedding + test that the payload matches the repo files.
2. `ModsConfig` + `ModInstaller` (status, install/update with backups, restore) + tests (temp game folders; reuse `Tests/.../TempGameFolder.cs`). Replace `CopyModScriptsIfPresent`; add startup sync + toast.
3. `GameLocator` + tests (VDF parsing, manifest parsing).
4. `PackageInstaller` (zip/rar/7z/folder, AMM + Summonable Traits detection) + tests with zips built in the test (and a folder); rar path can only be tested where tar.exe exists (skip otherwise).
5. Wizard UI (pages above) + Settings entry + `--setup` + admin restart. Update `GamePathService`/`MainWindow` flow so an unconfigured app opens the wizard.
6. `AppSelfInstaller` + first-start install dialog + desktop shortcut + settings flag.
7. Docs: `Instructions.txt` (short: "run the app, follow the wizard"; keep manual steps as fallback, remove Cheat Mod), `ARCHITECTURE.md` section for the installer, `README.md`.

## 8. Test plan

- Unit tests (xUnit, `Tests/Remnant2UnlockerApp.Tests`): payload completeness; mods.txt editing (real file content, Keybinds ordering, 0→1 flips, missing file, enabled.txt variant, CRLF preserved); installer status/update/backup/restore/idempotency (second run = no changes); package detection (AMM-like zip, nested wrapper folder, wrong package, Summonable Traits zip, folder input); VDF/manifest parsing; single-file detection logic isolated behind a seam.
- UI verification in the real app: launch the built exe, drive it with UI Automation from PowerShell (`System.Windows.Automation`: find by Name, `InvokePattern` walking up to the nearest invokable parent, `TogglePattern`, `SelectionItemPattern` for ComboBox items) and screenshot with `PrintWindow`. Look at every screenshot.
- **Never install into the user's real game folder from tests.** For end-to-end wizard runs, use a fake game folder (fake `Remnant2-Win64-Shipping.exe` file + `Mods\`) and make the wizard's path overridable.
- Manual in-game checklist for the user at the end: fresh-ish install via wizard (e.g. rename `Mods\Remnant2Unlocker` first), drop AMM zip, test launch shows "Bridge connected", spawn with stack size > 1 works (proves summon patch), Spawn Missing still works at 500 ms, repair after deleting a script, update prompt while game running.

## 9. Out of scope / later

- Game Pass: automatic UE4SS v3.0.1 download (URL in §4.1) — optional, can't be tested on the user's Steam machine; if implemented, clearly mark as experimental. Game Pass otherwise works through the same wizard (mods folder resolution already handles it).
- Start-menu shortcut, uninstall wizard (nice to have: remove mod folder, set `Remnant2Unlocker : 0`, restore stock summon from backup).
- Re-implementing summon quantity/level inside our own mod (rejected for now, see §2).

## Appendix A — the modified `summon_unloaded_assets.lua` (ship exactly this)

Saved byte-exact at `Lua Scripts inside game directory/ConsoleCommandsMod/Scripts/summon_unloaded_assets.lua` (1268 bytes, CRLF, no BOM, SHA256 `6D72FC6EF7E1AC4D88206E44FD57DF0E548DCFE587ED5E48A0E5A5D24D9A5597`). Original location: `G:\Steam\steamapps\common\Remnant2\Remnant2\Binaries\Win64\Mods\ConsoleCommandsMod\Scripts\`.

```lua
local UEHelpers = require("UEHelpers")
local ItemToSpawn=nil
local Quantity=1
local Level=0
NotifyOnNewObject("/Script/GunfireRuntime.ItemInstanceData", function(item)
    if not ItemToSpawn then return end
    item.Quantity=Quantity
    item.Level=Level
end)
-- EXAMPLE: `summon Material_Scraps_C 10 10`
-- summons 10 instances of 10 scrap
local function SpawnItem(Parameters)
    if #Parameters < 1 then return false end
    LoadAsset(Parameters[1])
    
    if #Parameters < 2 then return false end
    ItemToSpawn=Parameters[1]

    local KSL = UEHelpers.GetKismetSystemLibrary(true)
    local Player = UEHelpers.GetPlayerController()
    local Context = UEHelpers.GetWorldContextObject()

    if #Parameters > 2 then Quantity=Parameters[3] end
    if #Parameters > 3 then Level=Parameters[4] end
    
    for i=1,Parameters[2],1
    do
       KSL:ExecuteConsoleCommand(Context,string.format("summon %s",Parameters[1]),Player)
    end
    ItemToSpawn=nil
    Quantity=1
    Level=0
    return true
end
RegisterConsoleCommandHandler("summon", function(FullCommand, Parameters)
    return SpawnItem(Parameters)
end)
RegisterConsoleCommandHandler("Summon", function(FullCommand, Parameters)
    return SpawnItem(Parameters)
end)
```

## Appendix B — key file references

| What | Where |
|---|---|
| Current setup window | `Views/SetupWizardWindow.xaml(.cs)`, opened in `Views/MainWindow.xaml.cs` → `OnLoadedAsync` |
| Game path + mods folder resolution | `Services/GamePathService.cs` (`ResolveModsFolder`, `IsValidGamePath`, `HasSupportedGameExe`, `UserSettings`) |
| Component checks to reuse | `Services/DiagnosticsService.cs` (`CheckUE4SSCore`, `CheckAllowModsMod`, `CheckRequiredLuaMod`, `CheckModEnabled`) |
| Summonable Traits markers | `Services/SummonableTraitsService.cs` |
| Blind script copy to replace | `Services/AppUpdateService.cs` → `CopyModScriptsIfPresent` |
| Embedded-resource pattern | `Services/LocalizationService.cs` |
| Toasts | `Services/ToastService.cs` (`ToastService.Show(title, message, ToastType, durationMs, actionText, onAction)`) |
| App log | `Services/AppLogService.cs` |
| Manual install guide | `Instructions.txt` |
| Temp game folder for tests | `Tests/Remnant2UnlockerApp.Tests/TempGameFolder.cs` |
