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
            Types = types.ToList(),
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

        // Write to a temp file and rename over the target so the bridge's 200ms poll
        // never observes a partially-written command_queue.json.
        var tempPath = queuePath + ".tmp";

        await File.WriteAllTextAsync(tempPath, json);
        File.Move(tempPath, queuePath, overwrite: true);
    }

    public async Task SendConsoleCommandAsync(string command)
    {
        await WriteCommandAsync(new QueueCommand
        {
            Id = CreateId(),
            Action = "console_command",
            Command = command
        });
    }

    private static long CreateId()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}