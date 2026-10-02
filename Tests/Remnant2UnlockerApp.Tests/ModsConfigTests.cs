using System.IO;
using Remnant2UnlockerApp.Services;
using Xunit;

namespace Remnant2UnlockerApp.Tests;

public class ModsConfigTests
{
    [Fact]
    public void EnsureEnabled_InsertsNewModsBeforeTheKeybindsBlock()
    {
        var edit = ModsConfig.EnsureEnabled(FakeGameFolder.StockModsTxt, new[] { "AllowModsMod", "Remnant2Unlocker" });

        Assert.Equal(new[] { "Remnant2Unlocker" }, edit.Added);
        Assert.Empty(edit.Flipped);
        Assert.EndsWith(
            "AllowModsMod : 1\r\n\r\n\r\n\r\nRemnant2Unlocker : 1\r\n; Built-in keybinds, do not move up!\r\nKeybinds : 1\r\n",
            edit.Content);
    }

    [Fact]
    public void EnsureEnabled_KeepsEverythingElseAsIs()
    {
        var edit = ModsConfig.EnsureEnabled(FakeGameFolder.StockModsTxt, new[] { "Remnant2Unlocker" });

        Assert.Equal(FakeGameFolder.StockModsTxt, edit.Content.Replace("Remnant2Unlocker : 1\r\n", ""));
    }

    [Fact]
    public void EnsureEnabled_FlipsDisabledModsAndKeepsTheirFormatting()
    {
        var content = "ConsoleEnablerMod:0\r\nOtherMod : 0\r\n  CheatManagerEnablerMod   :   0\r\nKeybinds : 1\r\n";

        var edit = ModsConfig.EnsureEnabled(content, new[] { "ConsoleEnablerMod", "CheatManagerEnablerMod" });

        Assert.Equal(new[] { "ConsoleEnablerMod", "CheatManagerEnablerMod" }, edit.Flipped);
        Assert.Empty(edit.Added);
        Assert.Equal("ConsoleEnablerMod:1\r\nOtherMod : 0\r\n  CheatManagerEnablerMod   :   1\r\nKeybinds : 1\r\n", edit.Content);
    }

    [Fact]
    public void EnsureEnabled_NeverTouchesOtherModsOrComments()
    {
        var content = "; SomeMod : 0 (comment)\nActorDumperMod : 0\nKeybinds : 1\n";

        var edit = ModsConfig.EnsureEnabled(content, new[] { "SomeMod" });

        Assert.Equal("; SomeMod : 0 (comment)\nActorDumperMod : 0\nSomeMod : 1\nKeybinds : 1\n", edit.Content);
    }

    [Fact]
    public void EnsureEnabled_AlreadyEnabled_ChangesNothing()
    {
        var edit = ModsConfig.EnsureEnabled(FakeGameFolder.StockModsTxt, new[] { "ConsoleCommandsMod", "AllowModsMod" });

        Assert.False(edit.Changed);
        Assert.Equal(FakeGameFolder.StockModsTxt, edit.Content);
    }

    [Fact]
    public void EnsureEnabled_PreservesLfLineEndings()
    {
        var edit = ModsConfig.EnsureEnabled("A : 1\nKeybinds : 1\n", new[] { "B" });

        Assert.Equal("A : 1\nB : 1\nKeybinds : 1\n", edit.Content);
    }

    [Fact]
    public void EnsureEnabled_WithoutKeybinds_AppendsAfterTheLastEntry()
    {
        var edit = ModsConfig.EnsureEnabled("Remnant2Unlocker : 1\r\n\r\n", new[] { "SummonableTraits" });

        Assert.Equal("Remnant2Unlocker : 1\r\nSummonableTraits : 1\r\n\r\n", edit.Content);
    }

    [Fact]
    public void EnsureEnabled_WithoutTrailingNewline_KeepsIt()
    {
        var edit = ModsConfig.EnsureEnabled("Remnant2Unlocker : 1", new[] { "AllowModsMod" });

        Assert.Equal("Remnant2Unlocker : 1\r\nAllowModsMod : 1", edit.Content);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EnsureEnabled_NoFile_CreatesContent(string? content)
    {
        var edit = ModsConfig.EnsureEnabled(content, new[] { "AllowModsMod", "Remnant2Unlocker" });

        Assert.Equal("AllowModsMod : 1\r\nRemnant2Unlocker : 1\r\n", edit.Content);
    }

    [Fact]
    public void GetNotEnabled_ListsMissingAndDisabledMods()
    {
        var missing = ModsConfig.GetNotEnabled(
            "ConsoleCommandsMod : 1\r\nConsoleEnablerMod : 0\r\n",
            new[] { "ConsoleCommandsMod", "ConsoleEnablerMod", "Remnant2Unlocker" });

        Assert.Equal(new[] { "ConsoleEnablerMod", "Remnant2Unlocker" }, missing);
    }

    [Fact]
    public void ResolveConfigPath_PrefersModsTxtThenEnabledTxt()
    {
        using var game = new FakeGameFolder(withUe4ss: false);
        Directory.CreateDirectory(game.Mods);

        Assert.Equal(game.ModsTxt, ModsConfig.ResolveConfigPath(game.Mods));

        game.Write(@"Mods\enabled.txt", "Remnant2Unlocker : 1");
        Assert.Equal(Path.Combine(game.Mods, "enabled.txt"), ModsConfig.ResolveConfigPath(game.Mods));

        game.Write(@"Mods\mods.txt", "Remnant2Unlocker : 1");
        Assert.Equal(game.ModsTxt, ModsConfig.ResolveConfigPath(game.Mods));
    }

    [Fact]
    public void ReadAndEncode_RoundTripTheBom()
    {
        using var game = new FakeGameFolder(withUe4ss: false);
        var path = game.PathOf("bom.txt");
        File.WriteAllBytes(path, ModsConfig.Encode("A : 1\r\n", withBom: true));

        var (content, hasBom) = ModsConfig.ReadFile(path);

        Assert.True(hasBom);
        Assert.Equal("A : 1\r\n", content);
        Assert.Equal(File.ReadAllBytes(path), ModsConfig.Encode(content, hasBom));
    }
}
