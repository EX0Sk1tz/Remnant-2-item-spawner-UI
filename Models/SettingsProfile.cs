namespace Remnant2UnlockerApp.Models;

// The file format written/read by Settings > General > Save/Load. Bundles everything shown in
// the Settings window (Cheats, Hotkeys/preferences, Weapon Mod boost values) into one portable
// snapshot. Deliberately excludes favorites -- those are user library state, not a "settings"
// concept, and keep working through FavoritesService exactly as before.
public sealed class SettingsProfile
{
    public CheatSettings? Cheats { get; set; }
    public HotkeySettings? Hotkeys { get; set; }
    public Dictionary<string, Dictionary<string, double>>? WeaponModBoosts { get; set; }
}
