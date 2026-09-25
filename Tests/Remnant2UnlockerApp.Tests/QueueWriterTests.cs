using System.IO;
using System.Text.Json;
using Remnant2UnlockerApp.Services;
using Xunit;

namespace Remnant2UnlockerApp.Tests;

public class QueueWriterTests
{
    [Fact]
    public async Task SpawnMany_PutsThePathsInTheListFileAndKeepsTheCommandSmall()
    {
        using var game = new TempGameFolder();
        var writer = new QueueWriter(game.PathService);

        var paths = Enumerable.Range(1, 400).Select(i => $"/Game/Items/Ring_{i}.Ring_{i}_C").ToList();

        await writer.SpawnManyAsync(paths, stackSize: 1, delayMs: 100);

        var list = await File.ReadAllLinesAsync(Path.Combine(game.ModRoot, QueueWriter.SpawnListFileName));
        Assert.Equal(paths, list);

        var commandJson = await File.ReadAllTextAsync(game.PathService.GetQueuePath());
        Assert.True(commandJson.Length < 1024, $"command_queue.json is {commandJson.Length} bytes");

        var command = JsonDocument.Parse(commandJson).RootElement;
        Assert.Equal("spawn_many_safe", command.GetProperty("action").GetString());
        Assert.Equal(QueueWriter.SpawnListFileName, command.GetProperty("pathsFile").GetString());
        Assert.Equal(100, command.GetProperty("delayMs").GetInt32());
        Assert.False(command.TryGetProperty("paths", out _));

        Assert.Empty(Directory.GetFiles(game.ModRoot, "*.tmp"));
    }

    [Theory]
    [InlineData(10, 100)]
    [InlineData(50_000, 10_000)]
    public async Task SpawnMany_ClampsTheDelayToWhatTheModAccepts(int requested, int expected)
    {
        using var game = new TempGameFolder();

        await new QueueWriter(game.PathService).SpawnManyAsync(new[] { "/Game/A.A_C" }, 1, requested);

        var command = JsonDocument.Parse(await File.ReadAllTextAsync(game.PathService.GetQueuePath())).RootElement;
        Assert.Equal(expected, command.GetProperty("delayMs").GetInt32());
    }
}
