# Remnant 2 Unlocker — Architecture Reference

> Reference document for future work on this codebase: what each piece does, how the pieces connect, and known trouble spots. Generated from a full-project review; re-validate line numbers before relying on them, as the code will drift.

## 1. Overview

Remnant 2 Unlocker is a Windows desktop companion app (`net8.0-windows`, WPF + WinForms interop, no DI container, no third-party NuGet packages) that lets a player browse the game's item catalog and trigger spawns/unlocks in a running Remnant 2 session. The app itself never touches the game process's memory — it is a **file-based bridge client** paired with a **UE4SS Lua mod** ("Remnant2Unlocker") that runs inside the game process:

1. The UI (via `MainViewModel`) builds a command and `QueueWriter` serializes it to `command_queue.json` inside the game's `Mods/Remnant2Unlocker/` folder.
2. The in-game Lua mod (`Scripts/queue.lua`, entry point `Scripts/main.lua`) polls that file every 200ms, dedupes by a monotonically increasing `id`, executes the action via UE4SS's `ExecuteConsoleCommand`/`ExecuteInGameThread`, and writes progress back to `status.json`.
3. `BridgeStatusService` reads `status.json` and surfaces progress (e.g. in `SpawnProgressWindow`).

The Lua mod's own source is included in this repo under [`Lua Scripts inside game directory/`](#7-game-side-bridge-scripts-lua) — it is deployed to the game's `Mods/` folder, not compiled/shipped with the C# app. All paths (queue file, status file, item catalog, mod folders) are resolved through `Services/GamePathService`, which every other service takes as a constructor dependency. There is no dependency-injection container — `MainViewModel`'s constructor `new`s up every service directly.

Error visibility has two layers: on the C# side, `Services/AppLogService` is a small static rolling-file logger (`%AppData%\Remnant2UnlockerApp\logs\app.log`) that every previously-silent `catch` block now writes to, and `DiagnosticsWindow` has an "Open Log File" button to get to it; on the Lua side, `Scripts/queue.lua` (and the other scripts) `print()` to `UE4SS.log` for every real user action and its outcome (see [§7](#7-game-side-bridge-scripts-lua)).

## 2. Project structure

| Folder | Purpose |
|---|---|
| `Models/` | Plain DTOs used for JSON persistence/IPC and item entities. No behavior beyond simple computed properties. |
| `Services/` | I/O and integration layer: game-path resolution, queue writing, settings persistence, wiki image scraping, diagnostics, mod detection. |
| `ViewModels/` | `INotifyPropertyChanged` view models binding Models+Services to the WPF views. |
| `Views/` | WPF windows (`MainWindow`, `SettingsWindow`, `DiagnosticsWindow`, `SpawnProgressWindow`) and their code-behind. |
| `Lua Scripts inside game directory/` | Mirror of the UE4SS Lua mod that runs **inside the game process** — the other half of the bridge. Deployed to `<Win64Path>/Mods/Remnant2Unlocker/`. See [§7](#7-game-side-bridge-scripts-lua). |
| root (`App.xaml`, `.csproj`) | App entry point/startup, project/build configuration. |

There are no `Converters/`, `Helpers/`, or shared `Resources/` folders — styles/templates are defined inline per-window rather than in a shared `ResourceDictionary`.

## 3. Models (data / DTO layer)

### `RemnantItem`
The core item entity, implements `INotifyPropertyChanged`.
- Identity: `Name`, `Type`, `Path` (all `string`, default `""`).
- `SummonCommand => $"summon {Path}"` (computed).
- Image: `ImagePath` (setter synchronously calls `LoadImage()` and raises `PropertyChanged` for itself + `HasImage`), `Image` (`ImageSource?`, private setter), `HasImage`, `IsImageLoading` (caller-managed flag, not touched by `LoadImage`).
- `LoadImage(string?)` — static; `null` if path missing/invalid; otherwise a frozen `BitmapImage` (`CacheOption = OnLoad`).
- Trait state: `IsTraitEntry` (Type is "Trait"/"Core Trait"/"Archetype Trait"), `IsTraitPoint` (Type == "Trait Point"), `NeedsSummonableTraitsMod => IsTraitEntry && !IsTraitPoint`, `IsSummonableTraitsInstalled` (settable; on change notifies `IsTraitLocked`/`CanUseTraitCommands`/`CanUseNormalSpawn`).
- Relationships: produced by `ItemRepository`, consumed by `QueueWriter.SpawnAsync`, `ConsoleSpawnService.SpawnViaConsoleAsync`, `MainViewModel`, and `MainWindow.xaml`'s item card template. Linked to `CategoryGroup` only indirectly via matching `Type` strings.

### `CategoryGroup`
```csharp
public string Name { get; set; } = "";
public List<string> Types { get; set; } = new();
```
Sidebar filter grouping. `MainViewModel` hardcodes `_allCategoryGroups` (7 groups: Weapons, Armor, Accessories, Traits, Items, Materials, Other) and rebuilds the filtered `CategoryGroups` from it in `ApplyCategoryFilter()`.

### `QueueCommand`
Public DTO mirroring the JSON queue contract (`Id`, `Action`, `Path`, `Name`, `Paths`, `Types`, `DelayMs`, `DropQuantity`, `StackSize`, `ItemLevel`, `Command`). `QueueWriter` used to shadow this with an identically-shaped private nested class (see [Known Issues](#9-known-issues--possible-bugs) #1) — that duplicate has been removed, so this is now the actual type serialized to `command_queue.json`.

### `BridgeStatus`
JSON-deserialized bridge poll result (`[JsonPropertyName]` on every field): `Ready`, `Busy`, `LastCommandId`, `LastAction`, `LastMessage`, `LastSpawned?`, `ProcessedCount`, `TotalCount`, `Error?`. Computed `Progress` (0–100, clamped) and `ProgressText`.

### `CheatSettings` / `HotkeySettings`
- `CheatSettings`: `InfiniteHealth`, `InfiniteStamina` (bool). Persisted via `CheatSettingsService` to `cheats.json` — this is the sole/authoritative store for these two flags (`MainViewModel` loads them from here at startup).
- `HotkeySettings`: `AlwaysOnTop`, `ConsoleKey` ("F10"), `Teleport` ("F6"), `DestroyTarget` ("None"), `Wiki` ("wiki.gg"), `MovementSpeedMultiplier` (double, default 1.0), `StackSize` (int, default 1). Persisted via `HotkeySettingsService` to `hotkeys.json`. Hotkeys are raw strings, not a `Key` enum. (Previously also carried duplicate `InfiniteHealth`/`InfiniteStamina` fields that were written but never read back — removed, see Known Issues #16.)
- **Note:** `InfiniteHealth`/`InfiniteStamina` currently have no XAML control bound to them anywhere in `Views/` — there is no visible toggle for this feature yet, it exists only as backing properties on `MainViewModel` (see Known Issues #11).

### `DiagnosticIssue` / `DiagnosticReport`
`DiagnosticIssue`: `Title`, `Hint`, `Details`, `Fix`, `TechnicalDetails`, `IsError`. `DiagnosticReport`: `List<DiagnosticIssue> Issues`, computed `HasIssues`/`Hint` (first issue's hint only)/`Summary`, static `Empty` factory.

## 4. Services

Every service below takes `GamePathService` as a constructor dependency (omitted per-entry for brevity).

### `AppLogService`
Static, dependency-free rolling file logger — `Info`/`Warn`/`Error(message, exception?)` — writing timestamped lines to `%AppData%\Remnant2UnlockerApp\logs\app.log`. Rolls to `app.log.old` once the current file exceeds 1MB (single backup, not a numbered series). All writes are wrapped in a top-level try/catch that swallows failures silently, since logging must never be the thing that crashes the app. `GetLogPath()` is used by `DiagnosticsWindow`'s "Open Log File" button.

This is meant to be a complete, self-sufficient activity/error log for the app — the goal is that a user shouldn't need to open the game's `UE4SS.log` to understand what the app did or why something failed. It's called from:
- `App.OnStartup`/`Exit` — logs app start (with assembly version) and exit, so the file (and its parent folders) always gets recreated on the very next launch even if deleted, and a session boundary is always visible.
- Every previously-silent `catch` block across the services (`GamePathService`, `HotkeySettingsService`, `CheatSettingsService`, `ItemRepository`, `ConsoleSpawnService`, `SummonableTraitsService`) — see their individual entries below. (`WikiImageService` is the deliberate exception — see its entry.)
- `RelayCommand.Execute`'s catch and `App.OnDispatcherUnhandledException`.
- `MainViewModel` — logs every user-initiated bridge action at the moment it's requested (`Spawn requested: ...`, `Force console spawn requested: ...`, `SummonTrait/AddTrait requested: ...`, `Group spawn requested: ...`, `Reload requested`), then a short-delayed (`LogBridgeOutcomeAsync`, ~700ms) one-shot read of `status.json` via `BridgeStatusService` to log what the bridge actually did with it (`Info` normally, `Warn` if `status.Error` is set). This mirrors, on the C# side, the same request+outcome pairing that `Scripts/queue.lua` already prints to `UE4SS.log`.
- `SpawnProgressViewModel.Refresh()` — while a `SpawnProgressWindow` is open (group spawn or cancel in progress), every distinct `status.json` message change is logged the same way (deduped on the message itself, same principle as the `queue.lua` fix below) — for a large group spawn this means one log line per item processed, intentionally mirroring what `UE4SS.log` already shows for that flow.

### `GamePathService`
The path/settings hub. Holds `UserSettings` (`Win64Path`), persisted as JSON under `%AppData%\Remnant2UnlockerApp\settings.json`. Exposes `Win64Path`/`GameRootPath`, `IsConfigured`, `IsSteamInstall`/`IsGamePassInstall`/`PlatformName`, and path builders: `GetModRootPath()`, `GetItemsPath()` (`items.json`), `GetQueuePath()` (`command_queue.json`), `GetStatusPath()` (`status.json`), `GetScriptsPath()`. `SetWin64Path`/`SaveSettings` write synchronously. `LoadSettings`'s catch now logs via `AppLogService` instead of swallowing silently.

### `QueueWriter`
Writes the command that the external Lua mod bridge consumes. Public methods: `ReloadItemsAsync`, `SpawnAsync(RemnantItem, stackSize, itemLevel)`, `UnlockTypesAsync(IEnumerable<string> types, stackSize)`, `CancelCurrentActionAsync`, `SendConsoleCommandAsync(string)`. All construct a `Models.QueueCommand` and call `WriteCommandAsync`, which `JsonSerializer.Serialize`s (camelCase, indented) and writes it to `GetQueuePath()` **atomically** — via a `.tmp` file followed by `File.Move(..., overwrite: true)` — so the bridge's 200ms poll (`Scripts/queue.lua`) never observes a partially-written file. `Id` = `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()`.

### `ItemRepository`
`LoadItemsAsync()` deserializes `items.json` into `List<RemnantItem>`, filters out entries missing `Name`/`Path`, sorts by `Type` then `Name`. Returns an empty list if the file is missing **or** if it fails to parse (now wrapped in try/catch, logging the failure via `AppLogService` instead of throwing — `DiagnosticsService` separately flags a corrupt `items.json` as its own issue so the user gets an actionable message instead of a raw JSON exception).

### `BridgeStatusService`
`Read()` returns a `BridgeStatus` parsed from `status.json`; fully wrapped in try/catch with explanatory fallback messages on missing/invalid file.

### `SummonableTraitsService`
`IsInstalled()` walks `Win64Path/Mods` recursively for `main.lua` files and checks each (case-insensitive) for `RegisterConsoleCommandHandler("SummonTrait"`, `RegisterConsoleCommandHandler("AddTrait"`, and `Material_AwardTrait_Base`. Returns `true` on first fully-matching file. Per-file read failures are now logged via `AppLogService.Warn` (previously silently ignored) and are also surfaced as their own Diagnostics entry if the mod isn't found at all — see `DiagnosticsService` below.

### `ConsoleSpawnService`
Alternate spawn path that drives the game's own in-game console: finds the game process (checks **both** `Remnant2-Win64-Shipping` and `Remnant2-WinGDK-Shipping`, so it now works for Game Pass installs too), `SetForegroundWindow`s it, sends the configured console-open hotkey via `SendKeys`, pastes `item.SummonCommand` through the clipboard, presses Enter, then restores whatever was previously on the clipboard (best-effort, via `Clipboard.GetDataObject()`/`SetDataObject()`, now logging via `AppLogService.Warn` if either clipboard step throws). `ToSendKeys` maps `HotkeySettings.ConsoleKey` strings to SendKeys syntax.

### `CheatSettingsService` / `HotkeySettingsService`
JSON load/save for `cheats.json` / `hotkeys.json` under the mod root. `Load()` falls back to defaults on any exception and now logs the failure via `AppLogService` first (previously silently swallowed); `Save()` re-serializes an anonymous projection of the relevant fields.

### `DiagnosticsService`
Largest service. Now takes `SummonableTraitsService` as a second constructor dependency (alongside `GamePathService`). `Run()` builds a `DiagnosticReport` by checking: Win64 path configured/exists, game executable present (checks **both** Steam and Game Pass exe names), `Mods` folder, `AllowModsMod`/`dlls/main.dll`, the unlocker's own mod folder + `command_queue.json`/`status.json`/`scripts/main.lua`, **JSON validity of `items.json`/`command_queue.json`/`status.json`/`hotkeys.json`/`cheats.json`** (new — `CheckJsonFile` parses each with `JsonDocument.Parse` and reports a specific, actionable fix per file if it exists but is corrupt), three other required Lua mods, mod-enabled state via `mods.txt`/`enabled.txt`, **whether the optional "Summonable Traits" mod is installed** (new — `CheckSummonableTraits`, a non-error informational entry via `_summonableTraitsService.IsInstalled()`, since trait spawning is the only feature that depends on it), and (only if the game isn't running) `UE4SS.log` contents for expected startup lines. `IsGameRunning()` checks both Steam and Game Pass process names.

Every issue is still reported as a `DiagnosticIssue` with `Title`/`Hint`/`Details`/`Fix`/`TechnicalDetails`/`IsError`, rendered in `DiagnosticsWindow`'s card list — the `Fix` field is exactly the "possible fix" shown per issue.

### `WikiImageService`
Fetches item images from `remnant2.wiki.fextralife.com` via a static `HttpClient`. Checks a local PNG cache first; otherwise fetches the page HTML, regex-extracts an image URL, normalizes it, downloads, rejects results under 500 bytes (placeholder heuristic), and caches the result. All exceptions are swallowed and logged via `Debug.WriteLine` only (deliberately **not** written to `AppLogService`/`app.log` — missing wiki pages/images are routine and expected for a large fraction of items, not an error worth persisting). (No longer dumps a full copy of every fetched page to `Cache/Debug/<name>.html` — that unbounded debug artifact was removed, see Known Issues #15.)

## 5. ViewModels

### `MainViewModel`
Central, sealed VM; constructs every service directly (no DI). Owns:
- Item collections: `_allItems` (master), `Items` (filtered `ObservableCollection<RemnantItem>`).
- Category collections: `_allCategoryGroups` (hardcoded), `CategoryGroups` (filtered by `CategorySearchText`).
- UI state properties: `SearchText`, `SelectedGroup`, `SelectedType`, `SelectedItem`, `StatusText`, `StackSize` (clamped 1–999), `AlwaysOnTop`, wiki selection (`SelectedWiki`/`IsWikiGgSelected`/`IsFextralifeSelected`), hotkey capture state (`TeleportHotkey`/`ConsoleKey`/`DestroyTargetHotkey` + `*CaptureCommand` + `Is*Capturing*` + `*Display`), `MovementSpeedMultiplier` (clamped 1.0–5.0), `InfiniteHealth`/`InfiniteStamina`, game path status, diagnostic report state, `IsSummonableTraitsInstalled`/`SummonableTraitsStatus`.
- Commands: custom `RelayCommand`/`RelayCommand<T>` (defined in the same file) — `SelectTypeCommand`, `UnlockGroupCommand`, `ReloadCommand`, `SelectGamePathCommand`, and the three hotkey-capture-start commands. `RelayCommand.Execute` (necessarily `async void`, per `ICommand`) now wraps its body in a try/catch so a failure in any command's delegate (e.g. `UnlockGroupAsync`) logs via `AppLogService.Error` and shows the user a message box instead of crashing the app — see Known Issues #4.
- Trait classification (`IsTraitEntry`/`IsTraitPoint`) is read directly from `RemnantItem`'s own properties rather than duplicated as private statics here — see Known Issues #7.
- Methods invoked directly from view code-behind (not commands): `LoadAsync`, `SpawnItemAsync`, `ForceConsoleSpawnItemAsync`, `CopySummonCommand`, `SummonTraitAsync`, `AddTraitAsync`, `BuildWikiUrl`, `RefreshDiagnostics`.
- Event `GroupSpawnQueued` — fired after `UnlockGroupAsync` so the code-behind can open `SpawnProgressWindow`.

### `SpawnProgressViewModel`
Drives the group-spawn progress window. Holds a `DispatcherTimer` (500ms tick, UI-thread-safe) that calls `Refresh()` → `BridgeStatusService.Read()` → updates `Status` and all its dependent computed properties (`Message`, `ProgressValue`, `ProgressText`, `EstimatedTimeText`, `IsBusy`, `ErrorText`, `HasError`). `EstimatedTimeText` assumes a fixed 500ms-per-item processing rate. `CancelAsync()` logs the cancel request, calls `QueueWriter.CancelCurrentActionAsync()`, then refreshes immediately. `Refresh()` also logs every distinct status message to `AppLogService` (deduped) — see `AppLogService`'s entry above.

## 6. Views

- **`MainWindow.xaml`** — two-column layout: left sidebar (category search + "All" button + `ItemsControl` of `CategoryGroup` `Expander`s, each nested button bound to `SelectTypeCommand` via `RelativeSource AncestorType=Window`); right pane (item search + Reload button + `ListView` of item cards showing image/placeholder/spinner, trait-locked badge, type badge, and action buttons wired to **code-behind click handlers** — `SpawnItem_Click`, `ForceConsoleSpawnItem_Click`, `SummonTrait_Click`, `AddTrait_Click`, `CopyCommand_Click`, `OpenWiki_Click`); bottom status bar (status text, diagnostics hint, game-path indicator, "Spawn Group" button). `Topmost` is bound to `AlwaysOnTop`.
- **`MainWindow.xaml.cs`** — constructs `MainViewModel` directly, subscribes to `GroupSpawnQueued` to open `SpawnProgressWindow`, calls `LoadAsync()` on `Loaded`. Click handlers resolve the clicked item via `sender.DataContext` and delegate to VM async methods.
- **`SettingsWindow`** — bound to the shared `MainViewModel` instance; code-behind handles raw key/mouse capture for the three hotkey fields (console key, teleport, destroy-target) via `PreviewKeyDown`/`PreviewMouseDown`.
- **`DiagnosticsWindow`** — `DataContext` is a `DiagnosticReport` passed directly into the constructor; has a Discord link handler using `Process.Start`, and an "Open Log File" button (`OpenLogFile_Click`) that opens Explorer at `AppLogService.GetLogPath()`'s AppData folder — `/select,"..."` to highlight the file if it exists, or just the (created-on-demand) folder if it doesn't. Deliberately reveals the file in Explorer rather than launching it with whatever handler is (or isn't) registered for `.log`.
- **`SpawnProgressWindow`** — hosts `SpawnProgressViewModel`; presumably calls `Start()`/`Stop()` around the timer as the window opens/closes.

## 7. Game-side bridge scripts (Lua)

Everything here lives under `Lua Scripts inside game directory/`, mirroring what actually sits in `<Win64Path>/Mods/Remnant2Unlocker/` in-game. This is a [UE4SS](https://github.com/UE4SS-RE/RE-UE4SS) Lua mod — it runs inside the Remnant 2 process and has access to UE4SS globals like `RegisterKeyBind`, `ExecuteInGameThread`, `ExecuteWithDelay`, `LoopAsync`, `FindAllOf`, `StaticFindObject`, and UE reflection on game objects (`:IsValid()`, property access, etc.), plus the `UEHelpers` module for common lookups (world context, player controller, KismetSystemLibrary).

Folder layout:
```
Lua Scripts inside game directory/
├── items.json            (sample copy of the item catalog the mod reads)
├── command_queue.json     (sample queue file — one command "slot", not a real queue)
├── status.json            (sample status file the mod writes back)
├── hotkeys.json            (sample copy of HotkeySettings, as written by the C# app)
├── enabled.txt             (sample UE4SS mod-enable marker: "Remnant2Unlocker : 1")
└── Scripts/
    ├── main.lua            (entry point)
    ├── queue.lua           (the actual bridge: polls the queue, dispatches actions, writes status)
    ├── spawner.lua         (builds "summon" console commands)
    ├── hotkeys.lua         (registers Teleport / DestroyTarget keybinds, polls hotkeys.json for live rebinding)
    ├── movement_speed.lua  (applies the movement speed multiplier)
    └── json.lua            (vendored pure-Lua JSON encode/decode)
```
(`player.lua` and `inventory.lua` — unused/dead modules never `require`d by anything — have been deleted; see Known Issues #11/#13.)

### `Scripts/main.lua`
Entry point loaded by UE4SS. Requires and starts `queue`, `hotkeys`, and `movement_speed` (in that order), and registers a debug keybind on **F8** that just prints "Bridge is running" — a manual liveness check.

### `Scripts/queue.lua` — the actual bridge engine
- **Poll loop**: `ScheduleTick()` uses `ExecuteWithDelay(200, ...)` to re-schedule itself every 200ms; each tick reads `command_queue.json`, JSON-decodes it, and calls `ProcessCommand`.
- **Dedup**: every command has a numeric `id` (the C# side uses `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()`); `ProcessCommand` ignores any command whose `id <= lastProcessedId`, so the same file content is never re-executed, and `BaselineQueueId()` seeds `lastProcessedId` from whatever is already on disk at mod startup (so a stale command left over from a previous session isn't replayed).
- **Action dispatch** (`ProcessCommand`'s `action` field, all case-sensitive, accepts both `lowerCamel` and `PascalCase` JSON keys via `GetCommandValue`):
  | Action | Handler | Sent by current C# `QueueWriter`? |
  |---|---|---|
  | `spawn` | `SpawnSingle` → `Spawner.SpawnPath` (single console `summon`) | ✅ yes |
  | `unlock_types_safe` | `UnlockTypesSafe` → resolves all catalog items matching `types[]`, then `StartSafeSpawn` | ✅ yes |
  | `reload_items` | `LoadItems()` re-reads `items.json` | ✅ yes |
  | `console_command` | `ExecuteConsoleCommand` — runs the raw `command`/`Command` string via `ksl:ExecuteConsoleCommand` | ✅ yes (used for `SummonTrait <path>` / `AddTrait <path>`) |
  | `cancel` | Sets `cancelRequested = true`, checked before the next safe-spawn step | ✅ yes |
  | `unlock_types` | Alias of `unlock_types_safe` (identical handler) | ❌ never sent — dead alias |
  | `spawn_many` / `spawn_many_safe` | `SpawnManySafe` → reads a `paths`/`Paths` array and does the same delayed-loop spawn as group unlock | ❌ never sent — `QueueWriter` has no method that sets `Paths`, so this path is unreachable from the current UI |
  | `idle` | No-op (the default/rest state written by the C# side isn't actually sent as a real command — it's just the model's default value) | n/a |
- **Safe group spawn**: `StartSafeSpawn`/`ProcessNextSafeItem` walk a list of items **one at a time**, `ExecuteWithDelay(safeDelayMs, ...)` between each (`safeDelayMs` comes from the command's `delayMs`/`DelayMs`, clamped 100–10000, default 500), updating `status.processedCount`/`totalCount` after every item and checking `cancelRequested` each step — this is what `SpawnProgressViewModel`'s polling reflects.
- **Relic Fragment level-31 heuristic**: `IsRelicFragment` (matched by `path` containing `RelicFragment_`, `path` containing `/items/gems/`, or `name`/`type` containing "relic fragment") forces `itemLevel = 31` for any relic fragment being spawned, unless the command already specifies a non-zero `itemLevel`. The same heuristic is independently duplicated in `Spawner.SpawnItem` (unused entry point — `queue.lua` only calls `Spawner.SpawnPath` directly).
- **Status file** (`status.json`) fields mirror `Models/BridgeStatus.cs` exactly (`ready`, `busy`, `lastCommandId`, `lastAction`, `lastMessage`, `lastSpawned`, `processedCount`, `totalCount`, `error`), written via `SaveStatus()`/`SetStatus()` after essentially every state change.
- **Logging (fixed)**: `SetStatus` used to `print()` its message unconditionally on every call, including the two "file not found"/"could not be parsed" branches in `Queue.Tick()` that fire on **every 200ms poll** while `command_queue.json` is missing or invalid — that would flood `UE4SS.log` with an identical line 5x/second on a misconfigured or idle-before-first-command install. `SetStatus` now tracks the last-printed message (`lastLoggedMessage`) and only calls `print()` when it actually changes, while still writing `status.json` every time. Every other log line in this file already fires only on real per-action transitions (command received, spawn succeeded/failed, safe-spawn progress/cancel/finish, unknown action), so this was the one precision gap.

### `Scripts/spawner.lua`
`Spawner.SpawnPath(path, dropQuantity, stackSize, itemLevel)` — normalizes the path (trims, strips `|`, strips a leading `"summon "` if present), builds `"summon <path> <drop> <stack> [<level>]"`, and runs it via `ExecuteInGameThread` + `ksl:ExecuteConsoleCommand`. `Spawner.SpawnItem` is a convenience wrapper with its own relic-fragment level-31 check, but nothing in the mod calls it — `queue.lua` always calls `SpawnPath` directly with a level already resolved.

### `Scripts/hotkeys.lua`
Registers `RegisterKeyBind` for `teleport` (default `"F6"`, runs the `Teleport` console command) and `destroyTarget` (default `"None"`, runs `DestroyTarget`) if a valid key is present. `GetKeyEnum` maps stored key-name strings to UE4SS's `Key` table, with explicit special cases only for `"MouseButton4"` → `Key.XButton1`, `"MouseButton5"` → `Key.XButton2`, and `"MiddleMouseButton"`; everything else falls through to `Key[keyName]`. **Only `ConsoleKey` is not handled here** — that hotkey is used purely on the C# side (`ConsoleSpawnService` sends it via Win32 `SendKeys` to open the in-game console window), never read by Lua.

**Live-reload (added as a fix, needs in-game verification):** `Hotkeys.Start()` now applies settings once immediately and then polls `hotkeys.json` every 1000ms via `LoopAsync` (`ApplySettings`), matching `movement_speed.lua`'s cadence — previously it only read settings once at mod load. Since UE4SS's Lua API has no documented "unregister keybind" call, a rebind can't remove the previous `RegisterKeyBind` registration; instead each registered key's callback closes over its own key name and checks it against a module-level `activeTeleportKey`/`activeDestroyTargetKey` variable before firing, so an old key becomes a permanent no-op once the player picks a different one, rather than double-triggering. This design hasn't been tested against the live game — watch for stacked/duplicate triggers if a hotkey is rebound repeatedly in one session.

### `Scripts/movement_speed.lua`
Polls `hotkeys.json` for `movementSpeedMultiplier` every **1000ms** (`LoopAsync(1000, ReloadSettings)`) — unlike `hotkeys.lua`, this one *does* pick up Settings changes without a game restart. Every **250ms** (`LoopAsync(250, ApplyMovementSpeed)`) it finds the player pawn (`UEHelpers.GetPlayerController().Pawn`, falling back to `FindFirstOf("RemnantPlayerCharacter")`), locates a movement component (tries `CharacterMovement`, `MovementComponent`, `LocomotionComponent` in order), remembers the original `MaxWalkSpeed` as `baseSpeed` the first time it sees a valid value, and sets `MaxWalkSpeed = baseSpeed * multiplier`. When the multiplier drops back to `1.0`, `ResetMovementSpeedIfNeeded` restores `baseSpeed` exactly once.

### `Scripts/json.lua`
A small vendored pure-Lua JSON encoder/decoder (hand-rolled `escape_str`/`json.encode`/`json.decode`), used by `queue.lua`, `hotkeys.lua`, and `movement_speed.lua`. Not part of this project's own logic — just a dependency shim, since UE4SS's Lua environment has no built-in JSON support.

### Sample data files at the folder root
`items.json`, `command_queue.json`, `status.json`, `hotkeys.json`, and `enabled.txt` are point-in-time copies of what the mod actually reads/writes on a real install (useful for schema reference), not part of the mod's logic. `enabled.txt`'s `"Remnant2Unlocker : 1"` line is the exact format `DiagnosticsService`'s mod-enabled check greps for.

## 8. Data flow

```
MainWindow.xaml.cs ──constructs──> MainViewModel
                                        │
        ┌───────────────┬──────────────┼───────────────┬────────────────┐
        ▼                ▼              ▼               ▼                ▼
  ItemRepository   QueueWriter   BridgeStatusService  WikiImageService  ConsoleSpawnService
        │                │              │                                │
        │                ▼              │                                │
        │        command_queue.json     │                          SendKeys/Clipboard
        │                │              │                          into game window
        │                ▼              │
        │     Scripts/queue.lua (in-game UE4SS mod, polls every 200ms, dedupes by id)
        │                │              │
        │                ├──> Scripts/spawner.lua ──> console "summon ..." command
        │                ├──> Scripts/hotkeys.lua (polls hotkeys.json every 1s, live-reloads keybinds)
        │                └──> Scripts/movement_speed.lua (polls hotkeys.json every 1s)
        │                │              │
        │                ▼              │
        │          status.json ─────────┘
        ▼
   items.json

All path resolution:  *Service ──depends on──> GamePathService ──resolves──> filesystem under <Win64Path>/Mods/...
Settings persistence:  CheatSettingsService → cheats.json (unread by Lua)   HotkeySettingsService → hotkeys.json (polled by hotkeys.lua and movement_speed.lua every 1s)
Mod-install checks:    SummonableTraitsService, DiagnosticsService → scan Mods/**/*.lua and log files
```

`CategoryGroup` and `RemnantItem` are linked only by string-matching `Type`, not object references — there is no foreign-key-style relationship in these DTOs.

`SummonTrait`/`AddTrait` (used by `MainViewModel.SummonTraitAsync`/`AddTraitAsync` via the `console_command` action) are **not implemented anywhere in this mod's Lua scripts** — they are custom console commands that must be registered by a separate, externally-required "Summonable Traits" mod. `SummonableTraitsService.IsInstalled()` scans all `Mods/**/main.lua` files for exactly those `RegisterConsoleCommandHandler` registrations to detect whether that other mod is present.

## 9. Known Issues / Possible Bugs

Most of the issues found during the initial review have since been fixed. The table below keeps the original write-up for context and marks each one's resolution.

| # | Issue | Where | Why it matters | Status |
|---|---|---|---|---|
| 1 | `Models/QueueCommand.cs` was dead code | `Services/QueueWriter.cs` used to define a `private sealed class QueueCommand` identical in shape, shadowing `Models.QueueCommand` despite the `using` directive | The public model had zero live references anywhere — a stale duplicate left over from a refactor | ✅ **FIXED** — removed the private nested class; `QueueWriter` now builds and serializes `Models.QueueCommand` directly. |
| 2 | Game Pass process name gap in `ConsoleSpawnService` | Looked only for process `Remnant2-Win64-Shipping`, throwing `InvalidOperationException` if not found | `DiagnosticsService`/`GamePathService` correctly check both `Remnant2-Win64-Shipping` and `Remnant2-WinGDK-Shipping`; Game Pass players always hit the exception when using "Force Console Spawn" | ✅ **FIXED** — `SpawnViaConsoleAsync` now falls back to `Remnant2-WinGDK-Shipping` if the Steam/Epic process isn't found. |
| 3 | Non-atomic, single-slot queue writes | `QueueWriter.WriteCommandAsync` ↔ `Scripts/queue.lua`'s `Queue.Tick` | Direct `File.WriteAllTextAsync` risked the bridge observing a torn/partial JSON file mid-write | ✅ **PARTIALLY FIXED** — writes now go to a `.tmp` file followed by `File.Move(..., overwrite: true)`, so the bridge's 200ms poll never sees a partial file. The inherent single-slot design is unchanged: a second command written before the bridge's tick observes the first still silently supersedes it (`ProcessCommand` dedupes purely by `id`). Fully solving that would require a real multi-command queue file, which was out of scope for this pass. |
| 4 | Unhandled exception risk in group unlock | `RelayCommand.Execute` is `async void` with no try/catch; `App.xaml.cs` had no `DispatcherUnhandledException` handler | An I/O failure during any command (e.g. `UnlockGroupAsync`) could crash the entire app with no recovery path | ✅ **FIXED** — `RelayCommand.Execute` now wraps its body in try/catch (logs via `Debug.WriteLine`), and `App.xaml.cs` now installs a `DispatcherUnhandledException` handler that shows a message box and marks the exception handled instead of crashing. |
| 5 | Clipboard clobbered by `ConsoleSpawnService` | `SpawnViaConsoleAsync` pasted via the clipboard without saving/restoring prior contents | Silently destroys whatever the user had copied before spawning an item | ✅ **FIXED** — captures `Clipboard.GetDataObject()` before pasting and restores it afterward (best-effort, wrapped in try/catch since clipboard access can throw). The fixed `Task.Delay` timing (200/20/20/20ms) is unchanged — still worth revisiting if it proves fragile in testing. |
| 6 | Off-thread image decode risk (suspected) | `RemnantItem.ImagePath` setter calls `LoadImage()` synchronously; set from `MainViewModel`'s background wiki-image-fetch continuation | Constructing `BitmapImage` off the UI thread is fragile in WPF | ❎ **NOT A BUG** — verified: `WikiImageService` never calls `ConfigureAwait(false)`, and the call chain to `GetImageAsync` originates on the UI thread (`LoadAsync`, invoked from `MainWindow`'s `Loaded` handler or a `RelayCommand`), so every `await` continuation — including the one that sets `item.ImagePath` — correctly resumes on the UI thread's captured `SynchronizationContext`. No change made. |
| 7 | Trait-classification logic triplicated | `RemnantItem.IsTraitEntry`/`IsTraitPoint`, private statics of the same name in `MainViewModel`, and the marker-string scan in `SummonableTraitsService` | A future rule change (e.g. a new trait `Type` string) had to be updated in multiple places or would silently diverge | ✅ **PARTIALLY FIXED** — removed `MainViewModel`'s duplicate private statics; all call sites now use `item.IsTraitEntry`/`item.IsTraitPoint` directly. `SummonableTraitsService`'s marker-string scan is a different concept (detecting whether the *external* mod is installed, not classifying an item) and was left as-is. |
| 8 | Trait-mod detection runs on the UI thread with no caching | `SummonableTraitsService.IsInstalled()`, called from `MainViewModel.RefreshSummonableTraitsState()` | Recursively walks `Mods/` and full-text-reads every `main.lua` on every item load and after every spawn | ⚪ **NOT FIXED** — left as-is this pass; would need a caching/async strategy decision (e.g. cache with a TTL, or move off the UI thread) that wasn't part of this fix batch. |
| 9 | Redundant duplicate calls in `LoadAsync` | `RefreshSummonableTraitsState()` ran twice back-to-back around `ApplyFilter()` | Wasted a redundant `Mods/` scan and filter pass on every load/reload | ✅ **FIXED** — removed the second call; `RefreshSummonableTraitsState()` now runs once, before `ApplyFilter()` (which itself re-applies the now-current `IsSummonableTraitsInstalled` value to every item it adds to `Items`). |
| 10 | Teleport hotkey silently failed to bind on side mouse buttons | `SettingsWindow.xaml.cs`'s Teleport handler mapped `XButton1`/`XButton2` → `"ThumbMouseButton"`/`"ThumbMouseButton2"`, while the Destroy-Target handler mapped the *same* buttons → `"MouseButton4"`/`"MouseButton5"` — and `Scripts/hotkeys.lua`'s `GetKeyEnum` only special-cases the latter | Binding Teleport to a side mouse button silently did nothing in-game, while the identical physical binding worked for Destroy Target | ✅ **CONFIRMED AND FIXED** — Teleport's mouse handler now emits `"MouseButton4"`/`"MouseButton5"` too, matching Destroy Target and `hotkeys.lua`. |
| 11 | Infinite Health / Infinite Stamina toggles do nothing in-game | `CheatSettings`/`cheats.json`; `Scripts/player.lua` defined `Player.GetCheatManager()` but was never `require`d anywhere | No Lua script ever read `cheats.json` or called `GetCheatManager()` | ⚪ **INTENTIONALLY NOT IMPLEMENTED** (per decision) — also discovered during this pass: `InfiniteHealth`/`InfiniteStamina` have **no XAML control bound to them anywhere in `Views/`**, so there wasn't actually a user-facing toggle to disable — the feature is backend-only scaffolding with no UI entry point yet. `player.lua` (the dead scaffolding for this) was deleted along with `inventory.lua`; if this feature is built later, the CheatManager wiring will need to be written from scratch and tested in-game. |
| 12 | Teleport / Destroy-Target hotkey changes required a mod/game reload; movement speed did not | `Scripts/hotkeys.lua`'s `Hotkeys.Start()` read `hotkeys.json` once at mod load, no polling loop; `Scripts/movement_speed.lua` re-reads every 1000ms | Inconsistent UX between two settings that look equivalent in the UI | ✅ **BEST-EFFORT FIX, NEEDS IN-GAME VERIFICATION** — `hotkeys.lua` now polls `hotkeys.json` every 1000ms via `LoopAsync`, same cadence as movement speed. Since UE4SS's Lua API has no documented "unregister keybind" call, rebinding doesn't remove the old registration; instead every registered key's callback checks whether it's still the *currently active* key before firing, so a stale key becomes an inert no-op instead of double-triggering. **This has not been tested against the live game/UE4SS API** — verify in-game, especially rebinding the same hotkey several times in one session. |
| 13 | Two more dead/unused Lua modules | `Scripts/player.lua` (see #11) and `Scripts/inventory.lua` (a full `K2_AddItem`-based direct-inventory-grant path), neither ever `require`d | Suggested incomplete refactors/features rather than isolated one-offs | ✅ **FIXED** — both files deleted (per decision; git history preserves them if needed as reference later). |
| 14 | Lua supports queue actions the C# app can never trigger | `Scripts/queue.lua` handles `unlock_types` (alias of `unlock_types_safe`) and `spawn_many`/`spawn_many_safe` (reads a `paths`/`Paths` array), but `QueueWriter` never sends those action names | Dead capability on the Lua side; harmless but worth knowing if debugging "why doesn't spawn_many do anything" | ⚪ **NOT FIXED** — left as-is; not a bug, just unreachable capability, and removing it from the Lua side wasn't judged worth the risk of touching untested game-side code without a concrete need. |
| 15 | Unbounded debug artifact growth | `WikiImageService` wrote a full page-HTML dump to `Cache/Debug/<item>.html` on every non-cached fetch | No cleanup/retention logic; would keep growing disk usage in a normal install | ✅ **FIXED** — the debug dump (and the `Cache/Debug` directory creation) was removed entirely. |
| 16 | Overlapping settings models | `CheatSettings` and `HotkeySettings` both defined `InfiniteHealth`/`InfiniteStamina`, persisted to two separate JSON files, neither read by Lua | Unclear which was authoritative; redundant persistence for an inert feature | ✅ **FIXED** — removed `InfiniteHealth`/`InfiniteStamina` from `HotkeySettings`, `HotkeySettingsService.Save`, and `MainViewModel.SaveHotkeySettings`. `CheatSettings`/`cheats.json` remains the sole store (it's what's actually read back at startup). |
| 17 | Minor dead/leftover UI artifacts | `MainWindow.xaml`: unused `QuantityArrowButtonStyle`/`QuantityTextBoxStyle` resources; `AutomationProperties.HelpText="Test"`; `appsettings.json` copied to build output but never read anywhere | Cosmetic clutter that could confuse future readers | ✅ **FIXED** — removed the two unused styles and the leftover `AutomationProperties.HelpText`; deleted `appsettings.json` and its `.csproj` `Content`/`None` entries. |
| 18 | Possible historical double-prefix in trait console commands (unconfirmed) | A sample `status.json` in `Lua Scripts inside game directory/` showed `"lastMessage":"Console command sent: AddTrait AddTrait /Game/.../Trait_ArcaneStrike_C"` — the action name doubled | Current source (`MainViewModel.AddTraitAsync`) only builds `$"AddTrait {item.Path}"` (single prefix) | ⚪ **NOT REPRODUCIBLE FROM SOURCE** — left as a flag for awareness; do a quick manual in-game test (add a trait, inspect the resulting `status.json`) to confirm this isn't still happening before assuming it's fixed. |

---
*Generated by a full-codebase review on 2026-07-05; updated the same day after fixing most of the issues above (queue-write atomicity, process-name gap, unhandled-exception hardening, clipboard handling, dead-code removal, hotkey naming/live-reload, settings de-duplication). Line numbers were approximate in the original review and have been dropped here in favor of symbol names where the surrounding code has since changed.*
