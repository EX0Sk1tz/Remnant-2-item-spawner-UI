# Changelog — v4.3.0

## New Features

- **New cheats**: Infinite Ammo, No Fall Damage (experimental — not fully verified), and Enemy ESP (see enemies through walls via the same custom-depth technique as Hunter's Mark).
- **New hotkeys**: Destroy Last Spawned, Destroy Nearby Spawned, Replenish Cooldowns & Mod Power, and Fast Player Actions (speeds up skill/attack/evade animations).
- **Field of View slider**: adjustable from 60°–120°, saved automatically.
- **Cheat Commands panel**: Level Up (by count), Set All Weapon Level, Set/inspect Inventory Item Quantity by name, and Log Inventory Items (dumps to UE4SS.log, with an "all items" toggle).
- **Weapon Mod boosts**: per-mod tuning for HotShot, Sandstorm, Concussive Shot, Helix, Statis Beam, Voltaic Rondure, Scrapshot, and Rotted Arrow, plus a "Boost All" shortcut. Each field is a multiplier on the mod's base value (e.g. `10` = 10x); duration/frequency fields work in reverse (lower = more frequent).
- **Inventory item picker**: reads live inventory snapshots so you can pick items by name instead of typing them blind.

## Improvements

- Settings window reorganized into tabs (new "General" tab) to make room for the added cheat and hotkey options.
- Item list now auto-scrolls to keep the next row in view after spawning/summoning, so rapid-fire clicking near the bottom of the list no longer requires manual scrolling.
- Teleport hotkey now defaults to unbound ("None") instead of F6.

## Notes

- No Fall Damage and Enemy ESP are marked experimental — both work "most of the time" but haven't been fully verified across all scenarios.
