using System.IO;
using Remnant2UnlockerApp.ViewModels;
using Xunit;

namespace Remnant2UnlockerApp.Tests;

public class SetupWizardTests
{
    [Fact]
    public void CheckBridge_NothingWrittenSinceLaunch_IsNotConnected()
    {
        using var game = new FakeGameFolder();
        game.Write("UE4SS.log", "[Remnant2Unlocker] main.lua loaded\r\n");
        game.Write(@"Mods\Remnant2Unlocker\status.json", "{\"ready\":true}");

        var result = SetupWizardViewModel.CheckBridge(game.Root, DateTime.Now.AddMinutes(5));

        Assert.False(result.ModLoaded);
        Assert.False(result.Connected);
    }

    [Fact]
    public void CheckBridge_LogAndReadyStatus_IsConnected()
    {
        using var game = new FakeGameFolder();
        var since = DateTime.Now.AddSeconds(-5);
        game.Write("UE4SS.log", "...\r\n[Remnant2Unlocker] main.lua loaded\r\n");
        game.Write(@"Mods\Remnant2Unlocker\status.json", "{\"ready\":true,\"lastMessage\":\"Loaded 10 items\"}");

        Assert.Equal((true, true), SetupWizardViewModel.CheckBridge(game.Root, since));
    }

    [Fact]
    public void CheckBridge_ModLoadedButNotReady()
    {
        using var game = new FakeGameFolder();
        var since = DateTime.Now.AddSeconds(-5);
        game.Write("UE4SS.log", "[Remnant2Unlocker] main.lua loaded\r\n");
        game.Write(@"Mods\Remnant2Unlocker\status.json", "{\"ready\":false}");

        Assert.Equal((true, false), SetupWizardViewModel.CheckBridge(game.Root, since));
    }

    [Fact]
    public void CheckBridge_LockedLogIsStillRead()
    {
        using var game = new FakeGameFolder();
        var since = DateTime.Now.AddSeconds(-5);
        game.Write("UE4SS.log", "[Remnant2Unlocker] main.lua loaded\r\n");

        // UE4SS keeps its log open for writing while the game runs.
        using var writer = new FileStream(game.PathOf("UE4SS.log"), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

        Assert.True(SetupWizardViewModel.CheckBridge(game.Root, since).ModLoaded);
    }
}
