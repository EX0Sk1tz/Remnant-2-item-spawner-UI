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

- Scripts I uploaded [here](https://github.com/EX0Sk1tz/Remnant-2-item-spawner-scripts). (They are already included in the latest release)
- UE4SS installed
- Mods enabled in UE4SS
- (Optional) [Summonable Traits](#3-optional-install-summonable-traits) mod, only needed if you want to spawn or add Trait / Core Trait / Archetype Trait items

---

# Installation

## 1. Install UE4SS

Install UE4SS into:

```text
...\Steam\steamapps\common\Remnant2\Remnant2\Binaries\Win64
```

After installation the folder should contain:

```text
Remnant2-Win64-Shipping.exe
Mods
ue4ss
```

> **Game Pass / Windows Store install?** See [Game Pass notes](#game-pass--windows-store-notes) below — the folder layout is different and needs an experimental UE4SS build.

---

## 2. Install the Unlocker Mod

Copy the included:

```text
Remnant2Unlocker
```

folder into:

```text
...\Remnant2\Remnant2\Binaries\Win64\Mods
```

Final structure:

```text
Win64
└─ Mods
   └─ Remnant2Unlocker
      ├─ Scripts        (all .lua files from the zip — leave the whole folder as-is)
      ├─ command_queue.json
      ├─ enabled.txt
      ├─ hotkeys.json
      ├─ items.json
      └─ status.json
```

---

## 3. (Optional) Install Summonable Traits

Only needed if you want to spawn or directly add Trait / Core Trait / Archetype Trait items. Everything else works without it.

Download from Nexusmods:
[Summonable Traits](https://www.nexusmods.com/remnant2/mods/122)

The app's Diagnostics window will report whether it detects this mod as installed.
---

## Game Pass / Windows Store notes

The Xbox/Microsoft Store (WinGDK) build of Remnant 2 can't load UE4SS the normal way, so it needs to be hooked through a `dwmapi.dll` proxy instead. Use **[UE4SS v3.0.1](https://github.com/UE4SS-RE/RE-UE4SS/releases/tag/v3.0.1)** (the repo's rolling "experimental" build hasn't been updated since December 2024, so 3.0.1 is the one to grab). This changes where everything lives:

```text
...\XboxGames\Remnant 2\Content\Remnant2\Binaries\WinGDK
```

```text
WinGDK
├─ Remnant2-WinGDK-Shipping.exe
├─ dwmapi.dll                 <- proxy dll, loads UE4SS on game start
└─ ue4ss
   ├─ UE4SS.dll
   └─ mods                    <- note: nested here, not WinGDK\Mods
      ├─ Remnant2Unlocker
      ├─ AllowModsMod
      ├─ mods.txt / enabled.txt
      └─ ...
```

So for Game Pass, copy the `Remnant2Unlocker` folder into:

```text
...\WinGDK\ue4ss\mods
```

instead of `WinGDK\Mods`. When browsing to the game folder in the app (step 5 below), still point it at the `WinGDK` folder itself — the app checks both `Mods\` and `ue4ss\mods\` automatically and will find the mod either way.

Known issues on Game Pass:
- This setup is janky and depends on which UE4SS build you're using — expect more friction than Steam/Epic.
- `AllowModsMod` has occasionally been reported to crash the game on the Windows Store version. If you hit crashes right after launch, make sure you're on [UE4SS v3.0.1](https://github.com/UE4SS-RE/RE-UE4SS/releases/tag/v3.0.1) and not an older or mismatched build.

---

## 4. Start the Game

Launch Remnant 2 normally through Steam.

---

## 5. Start the Unlocker App

Run:

```text
Remnant2UnlockerApp.exe
```

Click:

```text
Browse
```

and select:

```text
...\Remnant2\Remnant2\Binaries\Win64
```

The path is saved automatically.

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

Adds a Trait / Core Trait / Archetype Trait directly to your inventory instead of spawning a world item. Requires the optional [Summonable Traits](#3-optional-install-summonable-traits) mod.

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

Click the diagnostics icon to check your setup: game path, UE4SS core files (`UE4SS.dll`, and `dwmapi.dll` on Game Pass), required UE4SS mods present/enabled, `Remnant2Unlocker` files valid, and (if the game isn't running) whether UE4SS's log confirms everything loaded correctly. Each check shows what failed and how to fix it. Use **Copy Report** to copy the full report to your clipboard when asking for help.

---

# Troubleshooting

## Spawn does nothing

Check:
- UE4SS is installed correctly
- all required mods are enabled
- the correct Win64 folder is selected

Run Diagnostics first — it will usually name the exact missing piece.

---

## No UE4SS console window appears when the game starts

UE4SS itself never loaded. This is almost always antivirus/Windows Defender quarantining `UE4SS.dll` (or `dwmapi.dll` on Game Pass) right after extraction, since it's a DLL that hooks the game process. Check your antivirus quarantine/history, restore the file, and add the UE4SS folder to its exclusions so it doesn't happen again on the next update.

---

## The app says "Game path not configured"

Select:

```text
...\Remnant2\Remnant2\Binaries\Win64
```

Not:
- Steam folder
- Remnant2 root folder
- Mods folder

---

## Some items crash or fail

Use:
- Force
- Copy

Some assets are unstable through direct spawning.

---

## Traits won't spawn or Add is blocked

Install the [Summonable Traits](#3-optional-install-summonable-traits) mod. Run Diagnostics to confirm the app detects it.

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
