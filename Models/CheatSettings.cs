namespace Remnant2UnlockerApp.Models;

public sealed class CheatSettings
{
    // Backed by a God Mode damage hook on the Lua side, not a health-property write.
    public bool InfiniteHealth { get; set; }
    public bool InfiniteStamina { get; set; }
    public bool InfiniteAmmo { get; set; }

    // Works most of the time but not verified 100% reliable yet; see Settings.NoFallDamageTooltip.
    public bool NoFallDamage { get; set; }
    public bool EnemyEsp { get; set; }
}