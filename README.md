# Remnant 2 Item Spawner UI

<img width="1921" height="769" alt="image" src="https://github.com/user-attachments/assets/c1dbc76f-fa37-41f9-94c3-99f479cdfcb0" />

Hi there, and welcome! ♥

This is a little desktop app that sits next to Remnant 2 and lets you spawn pretty much any item in the game with one click. It also has a bunch of cheats, hotkeys and quality-of-life helpers that I kept adding because I wanted them myself. Want to try out a build without farming for hours, finish your collection, or just mess around? That's exactly what it's for.

**New in v4.5.0:** the app now sets everything up for you! A setup wizard finds your game, installs the mod and turns everything on, so there's no more copying folders around or editing text files.

Download it from [Nexus Mods](https://www.nexusmods.com/remnant2/mods/215) or from the [Releases](https://github.com/EX0Sk1tz/Remnant-2-item-spawner-UI/releases) page here.

---

# Features

### Spawning
- Searchable item database with category and subcategory filters
- **Spawn** directly through UE4SS, or **Force** through the in-game console
- **Spawn Group**: spawn a whole subcategory at once
- Stack size and item level support out of the box
- Trait support (Spawn, or Add straight to your inventory) via the optional Summonable Traits mod
- **Prisms**: add any of the seven unique prisms (Greed, Hatred, Jealousy, Lethargy, Passion, Pride, Voracity) straight to your inventory. No boss kills or crafting needed, and prisms you already have are skipped
- Copy the summon command to your clipboard
- Favorites
- DLC and hidden item support

### Collection tracking (new!)
- The app reads your in-game inventory and marks everything you already own
- Collection counts per category and a **Missing only** filter
- **Spawn Missing**: spawns one of everything you don't have yet
- **DLC filter and badges**: show the base game or a single DLC, and see which DLC each item comes from

### Cheats
- Infinite Health (God Mode), Infinite Stamina, Infinite Ammo
- No Fall Damage (experimental)
- Magic Bullets (aim assist with adjustable FOV)
- 100% Loot Drop Chance
- No Recoil, No Spread
- **Enemy Outlines (new!)**: every living enemy glows red like the Hunter's Mark, at any range and through walls. Only you can see it

### Cheat Commands
- Level Up by count, Set All Weapon Level
- Set/inspect inventory item quantity by name
- Log inventory items

### Weapon Mod boosts
- Per-mod tuning for HotShot, Sandstorm, Concussive Shot, Helix, Statis Beam, Voltaic Rondure, Scrapshot and Rotted Arrow, plus a "Boost All" shortcut

### Hotkeys
- Console key, Teleport, Destroy Target / Last Spawned / Nearby Spawned, Replenish Cooldowns & Mod Power, Fast Player Actions, Toggle Enemy Outlines (all customizable)
- Movement speed multiplier

### Comfy stuff
- **Setup wizard (new!)** that installs and repairs everything for you
- Mod files in the game stay in sync with the app automatically after updates, with backups and "Undo last change"
- Optional install of the app itself, with a desktop shortcut
- Brand-new dark look in the style of Remnant II
- Settings profile (Cheats, Hotkeys, Weapon Mod boosts) saved in your user profile, so it survives reinstalling the game, and can load automatically on start
- Built-in Diagnostics to check your setup
- In-app update checker with one-click update install
- Integrated wiki button (wiki.gg or Fextralife)
- English and German UI
- Single standalone `.exe`, no .NET installation needed

---

# Requirements

- Remnant 2 (Steam, Epic or Game Pass)
- [Allow Asset Mods](https://www.nexusmods.com/remnant2/mods/2) from Nexus Mods. It bundles UE4SS and everything else the game needs to load mods. The setup wizard installs it for you, you just download the file
- (Optional) [Summonable Traits](https://www.nexusmods.com/remnant2/mods/122), only needed if you want to spawn or add Trait / Core Trait / Archetype Trait items

The Remnant2Unlocker mod itself (the in-game half of this app) is built into the app and gets installed and updated automatically. The separate "Cheat Mod" download for stack size and item level isn't needed anymore either.

---

# Installation

### 1. Start the app

Unpack the zip anywhere and run `Remnant2UnlockerApp.exe`.

On the first start it asks if you want to install it (to `%LocalAppData%\Programs\Remnant2Unlocker`, with an optional desktop shortcut). If you'd rather keep it where you unpacked it, just pick **Run without installing**.

### 2. Follow the setup wizard

The wizard opens by itself and walks you through everything:

- **Game**: finds your Steam, Epic or Game Pass install, or lets you pick the folder (the game's main folder works too).
- **Close the game**: only shows up if Remnant 2 is running.
- **Components**: everything the mod needs, with live status.
  - *Allow Asset Mods*: this is the only thing you download yourself. Click **Open Nexus page**, download the main file and drag & drop the `.zip` / `.rar` / `.7z` onto the wizard (or use **Choose file…** / **Choose folder…**).
  - *Remnant2Unlocker mod*, *Stack size & item level support* and *mods enabled in mods.txt*: all handled by **Install / Repair**.
  - *Summonable Traits (optional)*: same drop zone, or just **Skip**.
- **Antivirus check**: makes sure Windows Defender didn't eat UE4SS right after it was copied.
- **Test launch**: starts the game (or asks you to) and waits until the mod says hello.

And that's it!

A few nice things to know:

- You can reopen the wizard anytime under **Settings → General → Repair / update installation**.
- After an app update, the mod files in your game folder are updated automatically on the next start. If the game is running, the app asks first (restart the game afterwards).
- Everything the app replaces in the game folder is backed up first (`Mods\Remnant2Unlocker\Backups`, the last 5 are kept). **Undo last change** in the wizard puts the latest backup back.
- If your game lives under `C:\Program Files (x86)`, Windows needs admin rights to write there. The wizard offers **Restart as administrator** when that happens.

**Updating from an older version?** On the first start of v4.5.0 the app updates the mod files in your game folder once (with a backup) and shows "Mod updated". Restart the game if it was running and you're good to go.

Prefer doing it by hand? The manual steps are in [Instructions.txt](Instructions.txt).

## Nested `ue4ss` folder / Game Pass notes

Newer UE4SS builds (the UE4SS console shows "v3.0.1 Beta") keep everything in a `ue4ss` subfolder. That happens on Steam and Epic (`Win64\ue4ss\Mods`) as well as on Game Pass. A leftover `Win64\Mods` folder from an older UE4SS is then ignored by UE4SS, but don't worry, the app notices this and uses `ue4ss\Mods`.

The Xbox / Microsoft Store (WinGDK) build loads UE4SS through a `dwmapi.dll` proxy. **[UE4SS v3.0.1](https://github.com/UE4SS-RE/RE-UE4SS/releases/tag/v3.0.1)** is known to work. The layout looks like this:

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

The app and the wizard detect both layouts, so always just point them at the `Win64` / `WinGDK` folder itself.

To be honest with you: Game Pass setups are more fragile than Steam/Epic, and `AllowModsMod` has been reported to crash the Store version with mismatched UE4SS builds. If the game crashes right after launch, double-check you're on [UE4SS v3.0.1](https://github.com/UE4SS-RE/RE-UE4SS/releases/tag/v3.0.1).

---

# Buttons

### Spawn
Uses the UE4SS bridge to spawn the selected item. Fast and safe for most items.

### Force
Opens the in-game console and pastes the summon command for you. Handy for DLC items, unloaded assets, or anything that's being stubborn with Spawn.

### Add (Traits and Prisms)
Puts the item straight into your inventory instead of dropping it in the world. Traits need the optional [Summonable Traits](https://www.nexusmods.com/remnant2/mods/122) mod, prisms work without anything extra.

### Spawn Group
Spawns every item in the selected subcategory, using your default stack size. Groups bigger than 50 items ask first and spawn with a short delay in between so the game doesn't get overwhelmed.

### Spawn Missing
Spawns one of every item you don't own yet in the selected subcategory, in All, or in your Favorites. It respects the DLC filter too.

### Scan Inventory
Rescans your inventory right away (it also happens automatically every few seconds).

### Copy
Copies the full summon command, for example:

```text
summon /Game/World_Base/Items/Weapons/Longguns/Special/CrescentMoon/Weapon_CrescentMoon.Weapon_CrescentMoon_C
```

You can paste and tweak it in the in-game console yourself.

### Wiki
Opens the item's wiki page on wiki.gg or Fextralife (your choice in Settings).

---

# Settings

Open them with the gear icon in the top left.

### General
- **Always on top**: keeps the app above the game
- **Wiki**: wiki.gg or Fextralife
- **Speed multiplier**: movement speed, 1x to 5x
- **Default stack size**: used for Spawn, Force and Spawn Group
- **Settings Profile**: Save / Load your Cheats, Hotkeys and Weapon Mod boosts, with an option to load them automatically on startup. Favorites aren't included, they keep working like always
- **Repair / update installation**: reopens the setup wizard
- **Language**: English or Deutsch

### Hotkeys
- **Console key**, **Teleport**
- **Destroy Target**: deletes whatever you're looking at. This can remove world objects, invisible barriers or important level parts, so please be careful with it!
- **Destroy Last Spawned** / **Destroy Nearby Spawned**
- **Replenish Cooldowns & Mod Power**
- **Fast Player Actions**: speeds up skill, attack and evade animations. Press it again after changing areas
- **Toggle Enemy Outlines** (F7 by default)

### Cheats
- **Infinite Health** (God Mode), **Infinite Stamina**, **Infinite Ammo**
- **No Fall Damage**: works most of the time, but isn't fully tested yet
- **Aim → Magic Bullets** with an adjustable FOV cone
- **Loot → 100% Loot Drop Chance**
- **Weapon Handling → No Recoil**, **No Spread**
- **Vision → Enemy Outlines**

### Cheat Commands
- **Level Up (count)**, **Set All Weapon Level** (232 is the highest level known to do anything)
- **Set / inspect Inventory Item Quantity** by name
- **Log Inventory Items**: writes to `UE4SS.log`, with an "All items" toggle

### Weapon Mods
Per-mod boost values for HotShot, Sandstorm, Concussive Shot, Helix, Statis Beam, Voltaic Rondure, Scrapshot and Rotted Arrow, plus "Boost All". Each field is a multiplier on the mod's base value (`10` = 10x). "Frequency" and "Delay" fields are time intervals, so they work the other way around: use a value below 1 if you want them to trigger more often.

---

# Diagnostics

Click the diagnostics icon to check your setup: game path, UE4SS files (`UE4SS.dll`, and `dwmapi.dll` on Game Pass), required mods present and enabled, `Remnant2Unlocker` files, stack size & item level support, and (when the game isn't running) whether UE4SS's log says everything loaded. Every failed check tells you what's wrong and how to fix it, and most fixes point you straight to the wizard.

If you need help, hit **Copy Report** and paste it into your issue or comment. It makes helping you so much easier!

---

# Troubleshooting

**Run Diagnostics first**, it usually names the exact missing piece. And the wizard's **Install / Repair** fixes most problems on its own.

### Spawn does nothing
Run **Install / Repair** in the wizard (**Settings → General → Repair / update installation**) and check Diagnostics.

### No UE4SS console window appears when the game starts
UE4SS never loaded. This is almost always your antivirus / Windows Defender quarantining `UE4SS.dll` (or `dwmapi.dll` on Game Pass), because it hooks into the game. Restore it from quarantine and add an exclusion for the Binaries folder so it doesn't happen again. The wizard checks for this too.

### The app says "Game path not configured"
Open the setup wizard (**Settings → General → Repair / update installation**) and pick your install there. It finds Steam, Epic and Game Pass installs, and accepts the game's main folder as well as `Binaries\Win64` / `Binaries\WinGDK`.

### Stack size / item level is ignored
The patched `summon` command is missing (Diagnostics: *Stack size & item level support*). Run **Install / Repair**.

### Some items crash or fail to spawn
Use **Force** or **Copy** instead. Some assets just don't like being spawned directly.

### Traits won't spawn or Add is blocked
Summonable Traits is missing or not enabled. Just drop its download onto the setup wizard, then run Diagnostics to make sure the app sees it.

---

# Good to know about Teleport

- Teleporting down a lethal height will kill you, just like falling would. Going up is fine, and it works best aimed at a surface.
- It won't work while you're holding Shift (sprinting).
- Anything in the way shortens the teleport. Think of it like a super fast fly mod: you need a clear line of sight.

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

# Disclaimer

This project is meant for offline and personal use only. Use it at your own risk.
It's an unofficial fan project and not affiliated with the rights holders of Remnant 2. Licensed under the [MIT License](LICENSE.txt).

Thank you so much for stopping by, and have fun out there! If you run into anything weird or have an idea for a new feature, feel free to open an issue, I read all of them. ♥
