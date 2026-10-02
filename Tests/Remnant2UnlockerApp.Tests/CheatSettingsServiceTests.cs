using System.IO;
using Remnant2UnlockerApp.Models;
using Remnant2UnlockerApp.Services;
using Xunit;

namespace Remnant2UnlockerApp.Tests;

public class CheatSettingsServiceTests : IDisposable
{
    private readonly TempGameFolder _game = new();
    private readonly CheatSettingsService _service;

    public CheatSettingsServiceTests()
    {
        _service = new CheatSettingsService(_game.PathService);
    }

    public void Dispose() => _game.Dispose();

    private void WriteCheats(string content) => File.WriteAllText(Path.Combine(_game.ModRoot, "cheats.json"), content);

    [Fact]
    public void TryReadEnemyOutline_ReadsWhatTheAppSaved()
    {
        _service.Save(new CheatSettings { EnemyOutline = true, NoRecoil = true });

        Assert.True(_service.TryReadEnemyOutline());
    }

    // The game's hotkey rewrites the file with json.lua when the value was missing (key order changes).
    [Fact]
    public void TryReadEnemyOutline_ReadsWhatTheGameWrote()
    {
        WriteCheats("{\"noRecoil\":true,\"enemyOutline\":false}");

        Assert.False(_service.TryReadEnemyOutline());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{\"noRecoil\": true}")]
    [InlineData("{\"noRecoil\": true, \"enemyOut")]
    [InlineData("")]
    public void TryReadEnemyOutline_IsNullWhenThereIsNoUsableValue(string? content)
    {
        if (content != null)
            WriteCheats(content);

        Assert.Null(_service.TryReadEnemyOutline());
    }
}
