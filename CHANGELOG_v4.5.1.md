# Changelog — v4.5.1

## Fixes

- **Random game crashes with Enemy Outlines on**: the outline feature kept hold of enemies between checks. Once an enemy died or despawned and the game freed it, the next check could read freed memory and crash the game. Enemies are now looked up fresh on every check, so ones that are gone are simply skipped.
- **Infinite Health setup no longer runs off the game thread**: hooking the damage function ran from a background loop and could clash with the game cleaning up objects. It now runs on the game thread like everything else.

The in-game mod files update automatically the next time you start the app.
