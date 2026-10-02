# Changelog — v4.5.0

## New: the app sets everything up itself

- **Install wizard**: finds your game (Steam, Epic, Game Pass), installs the Remnant2Unlocker mod, turns the needed mods on in `mods.txt`, checks that your antivirus didn't remove UE4SS, and confirms with a test launch that the app can talk to the game. No more copying folders or editing text files.
- **Allow Asset Mods by drag & drop**: download it from Nexus Mods and drop the `.zip`, `.rar` or `.7z` onto the wizard (or choose the file or an extracted folder). Summonable Traits (optional) works the same way.
- **Stack size & item level work out of the box**: the app ships the `summon` command that understands stack size and item level and installs it into UE4SS's ConsoleCommandsMod. The separate "Cheat Mod" download is no longer needed.
- **Mod files stay in sync with the app**: after an app update, the mod files in the game are updated automatically on the next start. If the game is running, the app asks first.
- **Repair / update installation** in Settings → General reopens the wizard at any time.
- **Backups**: every file the app replaces in the game folder is backed up first (`Mods\Remnant2Unlocker\Backups`, last 5 kept). "Undo last change" in the wizard restores the latest backup.
- **Install the app itself**: on first start the app offers to install into `%LocalAppData%\Programs\Remnant2Unlocker` with an optional desktop shortcut, or keep running from where you unpacked it.
- Games under `C:\Program Files (x86)` that need admin rights: the wizard offers "Restart as administrator".

## Also new since v4.4.0

- **Collection tracking**: the app reads your in-game inventory and shows what you already own. "Missing only" filter, collection counts per category, and **Spawn Missing** to spawn everything you don't have yet.
- **DLC filter and badges**: show the base game or a single DLC; items show which DLC they belong to.
- **Prisms**: new subcategory Other → Prism with all seven unique prisms (Greed, Hatred, Jealousy, Lethargy, Passion, Pride, Voracity). **Add** puts the prism straight into your inventory, no boss kills or crafting needed; prisms you already have are skipped. Prisms can't be spawned as items, so their cards only have Add and Copy (Copy gives the `AddPrism` console command). Spawn Group on the subcategory adds every prism you're missing. No extra mod needed.
- **Enemy Outlines**: outlines every living enemy in red, like the Hunter's Mark, at any range and through walls. Only you see it. Switch it on in Settings → Cheats → Vision or with its hotkey (F7 by default).
- **New look**: the whole app now uses a dark design in the style of Remnant II.
- **Settings profile without file dialogs**: Save/Load in Settings → General now keep one profile in `%AppData%\Remnant2UnlockerApp`, so it survives reinstalling the game or the mod. Optionally load it automatically on every start.
- Release notes in the update dialog are shown as formatted text.

## Fixes

- **Nothing spawns with newer UE4SS builds ("items.json not found" in UE4SS.log)**: UE4SS builds that keep everything in a `ue4ss` subfolder (`Win64\ue4ss\Mods`, reported as "v3.0.1 Beta") are used on Steam and Epic too, not just Game Pass. The in-game mod only looked in `Win64\Mods` and never saw the app's commands, so spawning (including traits) did nothing. It now finds its files wherever UE4SS loaded it from, even when an old `Win64\Mods\Remnant2Unlocker` folder is still lying around.
- The app no longer picks a leftover `Win64\Mods` folder when UE4SS actually runs from `Win64\ue4ss`. The wizard, the mod sync, Diagnostics and the spawn queue all use the folder UE4SS really loads mods from.
- **Random game crashes while playing**: the background inventory scan (every 5 seconds, used for collection tracking) read a copy of the inventory that UE4SS had already freed, which crashed the game at random. It now reads the inventory in place.
- Item images no longer hang on items with duplicate names, pick images that actually belong to the item, and load colour variants.
- Diagnostics: new check "Stack size & item level support"; fix suggestions now point to the setup wizard.
- Text typed into the item search and category search boxes is fully visible again (it was clipped to a few pixels).

## Notes

- Existing installs: on the first start of this version the app updates the mod files in your game folder once (backup included) and shows "Mod updated". Restart the game if it was running.
- The project is now under the MIT License.
