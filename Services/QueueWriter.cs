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

    public const string SpawnListFileName = "spawn_list.txt";

    // Same one-item-at-a-time safe spawn as UnlockTypesAsync, but for an explicit list of paths
    // (queue.lua's "spawn_many_safe"). Plain "summon" only -- don't pass trait paths here.
    // queue.lua clamps delayMs to 100-10000.
    //
    // The paths go into their own file, one per line, instead of into command_queue.json: the mod
    // re-reads the command file every 200ms on UE4SS's async thread, and a ~40 KB command there
    // (all ~400 missing items) coincided with game crashes inside UE4SS's ProcessEvent hook.
    public async Task SpawnManyAsync(IEnumerable<string> paths, int stackSize, int delayMs = 500)
    {
        var listPath = Path.Combine(_pathService.GetModRootPath(), SpawnListFileName);

        // Written (atomically) before the command that points at it.
        await WriteFileAtomicallyAsync(listPath, string.Join("\n", paths) + "\n");

        await WriteCommandAsync(new QueueCommand
        {
            Id = CreateId(),
            Action = "spawn_many_safe",
            PathsFile = SpawnListFileName,
            DropQuantity = 1,
            StackSize = Math.Clamp(stackSize, 1, 999),
            DelayMs = Math.Clamp(delayMs, 100, 10000)
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
        var json = JsonSerializer.Serialize(
            command,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

        await WriteFileAtomicallyAsync(_pathService.GetQueuePath(), json);
    }

    // Write to a temp file and rename over the target so the bridge's 200ms poll never observes a
    // partially-written file.
    private static async Task WriteFileAtomicallyAsync(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var tempPath = path + ".tmp";

        await File.WriteAllTextAsync(tempPath, content);
        File.Move(tempPath, path, overwrite: true);
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