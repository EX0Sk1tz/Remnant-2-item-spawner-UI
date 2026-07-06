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
- Hotkey Customization (Console key, Teleport, Destroy Target)
- Teleport
- Movement speed multiplier and configurable default stack size
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
      ├─ Scripts
      │  ├─ hotkeys.lua
      │  ├─ json.lua
      │  ├─ main.lua
      │  ├─ movement_speed.lua
      │  ├─ queue.lua
      │  └─ spawner.lua
      │
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

Opened via the gear icon in the top left.

- **Always on top** — keeps the app window above the game.
- **Wiki** — choose wiki.gg or Fextralife as the source the Wiki button opens.
- **Speed multiplier** — movement speed multiplier, 1x–5x.
- **Default stack size** — quantity used for Spawn/Force/Spawn Group.
- **Hotkeys** — Console key, Teleport, Destroy Target (deletes whatever you're looking at; use carefully).
- **Language** — English or Deutsch.

---

# Diagnostics

Click the diagnostics icon to check your setup: game path, required UE4SS mods present/enabled, `Remnant2Unlocker` files valid, and (if the game isn't running) whether UE4SS's log confirms everything loaded correctly. Each check shows what failed and how to fix it.

---

# Troubleshooting

## Spawn does nothing

Check:
- UE4SS is installed correctly
- all required mods are enabled
- the correct Win64 folder is selected

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
