namespace Remnant2UnlockerApp.Models;

public sealed class HotkeySettings
{
    public bool AlwaysOnTop { get; set; }

    public string ConsoleKey { get; set; } = "F10";

    public string Teleport { get; set; } = "None";

    public string DestroyTarget { get; set; } = "None";

    public string DestroyLastSpawned { get; set; } = "None";

    public string DestroyNearbySpawned { get; set; } = "None";

    public string ReplenishCooldowns { get; set; } = "None";

    public string FastPlayerActions { get; set; } = "None";

    public string Wiki { get; set; } = "wiki.gg";

    public double MovementSpeedMultiplier { get; set; } = 1.0;

    public double FovValue { get; set; } = 90.0;

    public int StackSize { get; set; } = 1;

    public string Language { get; set; } = "en";
}