using System.IO;
using System.Text.Json;
using Remnant2UnlockerApp.Models;

namespace Remnant2UnlockerApp.Services;

public sealed class QueueWriter
{
    private readonly GamePathService _pathService;

    public QueueWriter(GamePathService pathService)
    {
        _pathService = pathService;
    }

    public async Task ReloadItemsAsync()
    {
        await WriteCommandAsync(new QueueCommand
        {
            Id = CreateId(),
            Action = "reload_items",
            DelayMs = 500
        });
    }

    public async Task SpawnAsync(RemnantItem item, int stackSize, int itemLevel)
    {
        await WriteCommandAsync(new QueueCommand
        {
            Id = CreateId(),
            Action = "spawn",
            Path = item.Path,
            Name = item.Name,
            DropQuantity = 1,
            StackSize = Math.Clamp(stackSize, 1, 999),
            ItemLevel = itemLevel,
            DelayMs = 500
        });
    }

    public async Task UnlockTypesAsync(IEnumerable<string> types, int stackSize)
    {
        await WriteCommandAsync(new QueueCommand
        {
            Id = CreateId(),
            Action = "unlock_types_safe",
            Types = types.ToArray(),
            DropQuantity = 1,
            StackSize = Math.Clamp(stackSize, 1, 999),
            DelayMs = 500
        });
    }

    public async Task CancelCurrentActionAsync()
    {
        await WriteCommandAsync(new QueueCommand
        {
            Id = CreateId(),
            Action = "cancel",
            DelayMs = 500
        });
    }

    private async Task WriteCommandAsync(QueueCommand command)
    {
        var queuePath = _pathService.GetQueuePath();
        var directory = Path.GetDirectoryName(queuePath);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(
            command,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

        await File.WriteAllTextAsync(queuePath, json);
    }

    private static long CreateId()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private sealed class QueueCommand
    {
        public long Id { get; set; }

        public string Action { get; set; } = "idle";

        public string? Path { get; set; }

        public string? Name { get; set; }

        public string[]? Paths { get; set; }

        public string[]? Types { get; set; }

        public int DropQuantity { get; set; } = 1;

        public int StackSize { get; set; } = 1;

        public int ItemLevel { get; set; }

        public int DelayMs { get; set; } = 500;
    }
}