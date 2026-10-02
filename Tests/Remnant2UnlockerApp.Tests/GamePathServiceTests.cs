using System.IO;
using Remnant2UnlockerApp.Services;
using Xunit;

namespace Remnant2UnlockerApp.Tests;

public class GamePathServiceTests
{
    [Fact]
    public void ResolveModsFolder_FlatUe4ss_UsesModsNextToTheExe()
    {
        using var game = new FakeGameFolder();

        Assert.Equal(game.Mods, GamePathService.ResolveModsFolder(game.Root));
    }

    [Fact]
    public void ResolveModsFolder_NestedUe4ss_UsesUe4ssMods()
    {
        using var game = new FakeGameFolder(withUe4ss: false);
        game.Write(@"ue4ss\UE4SS.dll", "fake ue4ss");
        game.Write(@"ue4ss\Mods\mods.txt", FakeGameFolder.StockModsTxt);

        Assert.Equal(game.PathOf(@"ue4ss\mods"), GamePathService.ResolveModsFolder(game.Root), ignoreCase: true);
    }

    [Fact]
    public void ResolveModsFolder_NestedUe4ssWithStrayModsFolder_StillUsesUe4ssMods()
    {
        // UE4SS never loads Win64\Mods when it runs from Win64\ue4ss; a leftover one must not win.
        using var game = new FakeGameFolder(withUe4ss: false);
        game.Write(@"ue4ss\UE4SS.dll", "fake ue4ss");
        game.Write(@"ue4ss\Mods\mods.txt", FakeGameFolder.StockModsTxt);
        game.Write(@"Mods\Remnant2Unlocker\items.json", "[]");

        Assert.Equal(game.PathOf(@"ue4ss\mods"), GamePathService.ResolveModsFolder(game.Root), ignoreCase: true);
    }

    [Fact]
    public void ResolveModsFolder_UE4SSInBothPlaces_KeepsPreferringModsNextToTheExe()
    {
        using var game = new FakeGameFolder();
        game.Write(@"ue4ss\UE4SS.dll", "fake ue4ss");
        game.Write(@"ue4ss\Mods\mods.txt", FakeGameFolder.StockModsTxt);

        Assert.Equal(game.Mods, GamePathService.ResolveModsFolder(game.Root));
    }

    [Fact]
    public void ResolveModsFolder_NoUe4ssYet_FallsBackToModsNextToTheExe()
    {
        using var game = new FakeGameFolder(withUe4ss: false);

        Assert.Equal(game.Mods, GamePathService.ResolveModsFolder(game.Root));
    }
}
