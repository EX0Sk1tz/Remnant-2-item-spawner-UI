namespace Remnant2UnlockerApp.Models;

public sealed class QueueCommand
{
    public long Id { get; set; }

    public string Action { get; set; } = "idle";

    public string? Path { get; set; }

    public string? Name { get; set; }

    public List<string>? Paths { get; set; }

    public List<string>? Types { get; set; }

    public int DelayMs { get; set; } = 500;

    public int DropQuantity { get; set; } = 1;

    public int StackSize { get; set; } = 1;

    public int ItemLevel { get; set; }

    public string? Command { get; set; }
}