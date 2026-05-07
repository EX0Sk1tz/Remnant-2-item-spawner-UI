# Remnant 2 Item Spawner UI

A lightweight WPF companion app and UE4SS Lua bridge for spawning Remnant 2 items through a searchable interface.

---

# Features

- Searchable item database
- Category and subcategory filtering
- Direct spawn through UE4SS
- Force spawn through in game console
- Copy summon command to clipboard
- Integrated wiki button
- DLC and hidden item support
- Clean standalone executable
- No external .NET installation required

---

# Requirements

- Scripts I uploaded [here](https://github.com/EX0Sk1tz/Remnant-2-item-spawner-scripts). (They are already included in the latest release)
- UE4SS installed
- Mods enabled in UE4SS

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
      ├─ scripts
      │  ├─ main.lua
      │  ├─ queue.lua
      │  ├─ spawner.lua
      │  ├─ player.lua
      │  └─ json.lua
      │
      ├─ items.json
      ├─ enabled.txt
      ├─ command_queue.json
      └─ status.json
```

---

## 3. Start the Game

Launch Remnant 2 normally through Steam.

---

## 4. Start the Unlocker App

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

## Copy

Copies the complete summon command to your clipboard.

Example:

```text
summon /Game/World_Base/Items/Weapons/Longguns/Special/CrescentMoon/Weapon_CrescentMoon.Weapon_CrescentMoon_C
```

You can manually paste and modify the command in the in game console.

---

## Wiki

Opens the corresponding Remnant 2 wiki page.

---

# Notes

Some DLC assets are not fully loaded by the game until they have been summoned once through the console.

If a normal spawn does not work:
1. Use Force once
2. Afterwards normal Spawn usually works

---

# Troubleshooting

## Spawn does nothing

Check:
- UE4SS is installed correctly
- all required mods are enabled
- the game is running
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

# Build From Source

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Output:

```text
bin\Release\net8.0-windows\win-x64\publish
```

---

# Disclaimer

This project is intended for offline and personal use only.
Use at your own risk.
