namespace Remnant2UnlockerApp.Models;

public sealed class HotkeySettings
{
    public bool AlwaysOnTop { get; set; }

    public string ConsoleKey { get; set; } = "F10";

    public string Teleport { get; set; } = "F6";

    public string DestroyTarget { get; set; } = "None";

    public string Wiki { get; set; } = "wiki.gg";

    public double MovementSpeedMultiplier { get; set; } = 1.0;

    public int StackSize { get; set; } = 1;
}