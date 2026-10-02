# Remnant 2 Item Spawner UI

<img width="1921" height="769" alt="image" src="https://github.com/user-attachments/assets/c1dbc76f-fa37-41f9-94c3-99f479cdfcb0" />

---

# Features

- Searchable item database
- Category and subcategory filtering
- Direct spawn through UE4SS
- Force spawn through in game console
- Group spawn an entire category/subcategory at once
- Trait support (Spawn or Add directly to inventory) via the optional Summonable Traits mod
- Prisms (Other → Prism): Add any of the seven unique prisms straight to your inventory; prisms you already have are skipped
- Copy summon command to clipboard
- Integrated wiki button (wiki.gg or Fextralife, selectable in Settings)
- Hotkey Customization (Console key, Teleport, Destroy Target/Last Spawned/Nearby Spawned, Replenish Cooldowns & Mod Power, Fast Player Actions)
- Teleport
- Movement speed multiplier and configurable default stack size
- **Cheats**: Infinite Health/Stamina/Ammo, No Fall Damage, Magic Bullets (aim assist with adjustable FOV), 100% Loot Drop Chance, No Recoil, No Spread
- **Cheat Commands**: Level Up by count, Set All Weapon Level, Set/inspect Inventory Item Quantity by name, Log Inventory Items
- **Weapon Mod boosts**: per-mod tuning for HotShot, Sandstorm, Concussive Shot, Helix, Statis Beam, Voltaic Rondure, Scrapshot, and Rotted Arrow, plus a "Boost All" shortcut
- Save/load a full settings profile (Cheats, Hotkeys, Weapon Mod boosts) to a file
- Built-in Diagnostics to check your UE4SS/mod setup
- In-app update checker with one-click update install
- English and German UI language
- DLC and hidden item support
- Clean standalone executable
- No external .NET installation required

---

# Requirements

- Remnant 2 (Steam, Epic or Game Pass)
- [Allow Asset Mods](https://www.nexusmods.com/remnant2/mods/2) from Nexus Mods (UE4SS + AllowModsMod + UE4SS's console mods, all in one download). The setup wizard installs it for you from the file you download.
- (Optional) [Summonable Traits](https://www.nexusmods.com/remnant2/mods/122), only needed if you want to spawn or add Trait / Core Trait / Archetype Trait items

The Remnant2Unlocker mod itself (the in-game half of this app) is built into the app and installed/updated automatically.

---

# Installation

1. Download the latest release and run `Remnant2UnlockerApp.exe`.
2. On the first start the app offers to **install itself** (default `%LocalAppData%\Programs\Remnant2Unlocker`, optional desktop shortcut). "Run without installing" keeps it portable.
3. The **setup wizard** opens:
   - **Game** – finds Steam, Epic and Game Pass installs; or choose the folder (the game's main folder works too).
   - **Close the game** – only shown if Remnant 2 is running.
   - **Components** – everything the mod needs, with live status:
     - *UE4SS + AllowModsMod (Allow Asset Mods)*: click **Open Nexus page**, download the main file, and drop the `.zip` / `.rar` / `.7z` onto the wizard (or **Choose file…** / **Choose folder…**).
     - *Remnant2Unlocker mod*, *Stack size & item level support* (a `summon` command that understands stack size and level) and *mods enabled in mods.txt*: installed by **Install / Repair**.
     - *Summonable Traits (optional)*: same drop zone, or **Skip**.
   - **Antivirus check** – makes sure Windows Defender didn't quarantine UE4SS right after it was copied.
   - **Test launch** – starts the game (Steam) or asks you to, and waits until the mod reports in.

Afterwards:

- Reopen the wizard any time: **Settings → General → Repair / update installation**.
- After an app update, the mod files in the game are brought up to date automatically on the next start (while the game runs, the app asks first).
- Every file the app replaces in the game folder is backed up to `Mods\Remnant2Unlocker\Backups` (last 5 kept); **Undo last change** in the wizard restores the latest one.
- Steam under `C:\Program Files (x86)` needs admin rights to write; the wizard offers **Restart as administrator**.

Manual installation steps (without the wizard) are in [Instructions.txt](Instructions.txt).

## Nested `ue4ss` folder / Game Pass notes

Newer UE4SS builds (the UE4SS console shows "v3.0.1 Beta") keep everything in a `ue4ss` subfolder, on Steam and Epic (`Win64\ue4ss\Mods`) as well as Game Pass. A leftover `Win64\Mods` from an older UE4SS is then ignored by UE4SS; the app detects this and uses `ue4ss\Mods`.

The Xbox/Microsoft Store (WinGDK) build loads UE4SS through a `dwmapi.dll` proxy; **[UE4SS v3.0.1](https://github.com/UE4SS-RE/RE-UE4SS/releases/tag/v3.0.1)** is known to work. With it, mods live in `WinGDK\ue4ss\mods` instead of `WinGDK\Mods`:

```text
WinGDK
├─ Remnant2-WinGDK-Shipping.exe
├─ dwmapi.dll                 <- proxy dll, loads UE4SS on game start
└─ ue4ss
   ├─ UE4SS.dll
   └─ mods                    <- nested here, not WinGDK\Mods
      ├─ Remnant2Unlocker
      ├─ AllowModsMod
      ├─ mods.txt / enabled.txt
      └─ ...
```

The app and the wizard detect both layouts; always select the `Win64` / `WinGDK` folder itself.

Known issues on Game Pass:
- This setup is janky and depends on which UE4SS build you're using — expect more friction than Steam/Epic.
- `AllowModsMod` has occasionally been reported to crash the game on the Windows Store version. If you hit crashes right after launch, make sure you're on [UE4SS v3.0.1](https://github.com/UE4SS-RE/RE-UE4SS/releases/tag/v3.0.1) and not an older or mismatched build.

---

# Buttons

## Spawn

Uses the UE4SS bridge and CheatManager to spawn the selected item.

Fast and safe for most items.

---

## Force

Uses the in game console directly.

Useful for:
- DLC items
- unloaded assets
- problematic items
- testing summon commands

---

## Add (Traits only)

Adds a Trait / Core Trait / Archetype Trait directly to your inventory instead of spawning a world item. Requires the optional [Summonable Traits](https://www.nexusmods.com/remnant2/mods/122) mod (install it in the setup wizard).

---

## Spawn Group

Spawns every item in the selected subcategory at once, using your configured default stack size. Groups larger than 50 items ask for confirmation first and spawn with a short delay between items to avoid overloading the game.

---

## Copy

Copies the complete summon command to your clipboard.

Example:

```text
summon /Game/World_Base/Items/Weapons/Longguns/Special/CrescentMoon/Weapon_CrescentMoon.Weapon_CrescentMoon_C
```

You can manually paste and modify the command in the in game console.

---

## Wiki

Opens the corresponding Remnant 2 wiki page. Choose wiki.gg or Fextralife as the source in Settings.

---

# Settings

Opened via the gear icon in the top left. Split into five tabs:

## General

- **Always on top** — keeps the app window above the game.
- **Wiki** — choose wiki.gg or Fextralife as the source the Wiki button opens.
- **Speed multiplier** — movement speed multiplier, 1x–5x.
- **Default stack size** — quantity used for Spawn/Force/Spawn Group.
- **Settings Profile** — Save Settings / Load Settings buttons. Saves the current Cheats, Hotkeys, and Weapon Mod boost values to a file, or loads a previously saved one. Favorites aren't included — those keep working as they always have.
- **Language** — English or Deutsch.

## Hotkeys

- **Console key**, **Teleport**
- **Destroy Target** — deletes whatever you're currently looking at. Can remove world objects, invisible barriers, or important level parts — use carefully.
- **Destroy Last Spawned** / **Destroy Nearby Spawned**
- **Replenish Cooldowns & Mod Power**
- **Fast Player Actions** — speeds up skill/attack/evade animations; re-press after changing areas.

## Cheats

- **Infinite Health** (God Mode — zeroes incoming damage), **Infinite Stamina**, **Infinite Ammo**
- **No Fall Damage** — experimental, not fully verified yet
- **Aim → Magic Bullets** — aim assist, with an adjustable FOV cone
- **Loot → 100% Loot Drop Chance**
- **Weapon Handling → No Recoil**, **No Spread**

## Cheat Commands

- **Level Up (count)**, **Set All Weapon Level**
- **Set/inspect Inventory Item Quantity** by name
- **Log Inventory Items** — dumps to `UE4SS.log`, with an "all items" toggle

## Weapon Mods

Per-mod boost values for HotShot, Sandstorm, Concussive Shot, Helix, Statis Beam, Voltaic Rondure, Scrapshot, and Rotted Arrow, plus a "Boost All" shortcut. Each field is a multiplier on the mod's base value (e.g. `10` = 10x); duration/frequency fields work in reverse — use a value below 1 for more frequent triggers.

---

# Diagnostics

Click the diagnostics icon to check your setup: game path, UE4SS core files (`UE4SS.dll`, and `dwmapi.dll` on Game Pass), required UE4SS mods present/enabled, `Remnant2Unlocker` files valid, stack size & item level support installed, and (if the game isn't running) whether UE4SS's log confirms everything loaded correctly. Each check shows what failed and how to fix it. Use **Copy Report** to copy the full report to your clipboard when asking for help.

---

# Troubleshooting

## Spawn does nothing

Run Diagnostics first — it will usually name the exact missing piece. Then open **Settings → Repair / update installation** and click **Install / Repair**.

---

## No UE4SS console window appears when the game starts

UE4SS itself never loaded. This is almost always antivirus/Windows Defender quarantining `UE4SS.dll` (or `dwmapi.dll` on Game Pass) right after extraction, since it's a DLL that hooks the game process. Check your antivirus quarantine/history, restore the file, and add the UE4SS folder to its exclusions so it doesn't happen again on the next update.

---

## The app says "Game path not configured"

Open the setup wizard (**Settings → Repair / update installation**) and pick your install there. It finds Steam, Epic and Game Pass installs, and accepts the game's main folder as well as `Binaries\Win64` / `Binaries\WinGDK`.

---

## Stack size / item level is ignored

The patched `summon` command is missing (Diagnostics: *Stack size & item level support*). Run **Install / Repair** in the setup wizard.

---

## Some items crash or fail

Use:
- Force
- Copy

Some assets are unstable through direct spawning.

---

## Traits won't spawn or Add is blocked

Install the [Summonable Traits](https://www.nexusmods.com/remnant2/mods/122) mod: drop its download onto the setup wizard. Run Diagnostics to confirm the app detects it.

---

# Build From Source

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Output:

```text
bin\Release\net8.0-windows\win-x64\publish
```

The app checks GitHub Releases on startup and can download/install updates itself, so release assets must be a `.zip` containing the contents of that publish folder (not `.rar`).

The exe must be a true single file (the csproj turns on `IncludeNativeLibrariesForSelfExtract` for single-file publishes): the app installs itself by copying just the exe. The in-game mod files are embedded in the exe (from `Lua Scripts inside game directory/`), so the `Remnant2Unlocker` folder in the release zip is only for manual installs.

---

# Notice:
If experiencing crashes with the Spawn Button, just use the Force Button.
This will open the console ingame and paste the summon command, the hotkey for the console can be configured in the app.

Teleport will kill you if the vertical distance downwards would be lethal through falling.
Upwards no restriction. Best to use against a surface.
Pressing Shift aka. Sprinting while trying to Teleport will not work.
Any environment blocking your character from its path to the desired location will result in shorter teleport.
Imagine it being a very fast fly mod, the line of sight must be clear.

# Disclaimer

This project is intended for offline and personal use only.
Use at your own risk.
