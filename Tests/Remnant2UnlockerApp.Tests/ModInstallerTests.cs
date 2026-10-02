using System.IO;
using Remnant2UnlockerApp.Services;
using Xunit;

namespace Remnant2UnlockerApp.Tests;

public class ModInstallerTests
{
    private const string TraitsMainLua =
        "RegisterConsoleCommandHandler(\"SummonTrait\", function() end)\r\n" +
        "RegisterConsoleCommandHandler(\"AddTrait\", function() end)\r\n" +
        "-- Material_AwardTrait_Base\r\n";

    [Fact]
    public void GetStatus_FreshGame_EverythingMissingAndStockSummonNotPatched()
    {
        using var game = new FakeGameFolder();

        var status = ModInstaller.GetStatus(game.Root);

        Assert.False(status.IsModInstalled);
        Assert.True(status.IsConsoleCommandsModPresent);
        Assert.False(status.IsSummonPatchApplied);
        Assert.Equal(ModPayload.Files.Count, status.Files.Count);
        Assert.Equal(ModFileState.Outdated, status.Files.Single(f => f.File == ModPayload.SummonPatch).State);
        Assert.All(status.Files.Where(f => f.File.Component == ModPayload.UnlockerComponent),
            f => Assert.Equal(ModFileState.Missing, f.State));
        Assert.Equal(new[] { "Remnant2Unlocker" }, status.NotEnabledMods);
    }

    // The app writes hotkeys.json/cheats.json at start, which creates the folder before the mod is installed.
    [Fact]
    public void GetStatus_FolderWithOnlySettingsFiles_IsNotInstalled()
    {
        using var game = new FakeGameFolder();
        game.Write(@"Mods\Remnant2Unlocker\hotkeys.json", "{}");
        game.Write(@"Mods\Remnant2Unlocker\cheats.json", "{}");

        Assert.False(ModInstaller.GetStatus(game.Root).IsModInstalled);
    }

    [Fact]
    public void InstallOrUpdate_FreshGame_InstallsPatchesAndEnables()
    {
        using var game = new FakeGameFolder();

        var result = ModInstaller.InstallOrUpdate(game.Root);

        Assert.True(result.Success);
        Assert.Equal(ModPayload.Files.Count, result.ChangedCount);

        foreach (var file in ModPayload.Files)
            Assert.Equal(file.Content, File.ReadAllBytes(file.GetTargetPath(game.Mods)));

        Assert.Contains("Remnant2Unlocker : 1\r\n; Built-in keybinds", game.Read(@"Mods\mods.txt"));

        var status = ModInstaller.GetStatus(game.Root);
        Assert.True(status.IsUpToDate);
        Assert.True(status.IsSummonPatchApplied);

        // Only the stock summon and mods.txt existed before, so only they were backed up.
        Assert.NotNull(result.BackupFolder);
        Assert.Equal(FakeGameFolder.StockSummonLua,
            File.ReadAllText(Path.Combine(result.BackupFolder!, @"Mods\ConsoleCommandsMod\Scripts\summon_unloaded_assets.lua")));
        Assert.Equal(FakeGameFolder.StockModsTxt, File.ReadAllText(Path.Combine(result.BackupFolder!, @"Mods\mods.txt")));
        Assert.Equal(2, Directory.GetFiles(result.BackupFolder!, "*", SearchOption.AllDirectories).Length);
    }

    [Fact]
    public void InstallOrUpdate_SecondRun_ChangesNothing()
    {
        using var game = new FakeGameFolder();
        ModInstaller.InstallOrUpdate(game.Root);
        var modsTxtBefore = File.ReadAllBytes(game.ModsTxt);

        var second = ModInstaller.InstallOrUpdate(game.Root);

        Assert.True(second.Success);
        Assert.Equal(0, second.ChangedCount);
        Assert.False(second.ConfigEdit!.Changed);
        Assert.Null(second.BackupFolder);
        Assert.Equal(modsTxtBefore, File.ReadAllBytes(game.ModsTxt));
    }

    [Fact]
    public void InstallOrUpdate_ChangedScript_IsRestoredAndBackedUp()
    {
        using var game = new FakeGameFolder();
        ModInstaller.InstallOrUpdate(game.Root);
        game.Write(@"Mods\Remnant2Unlocker\Scripts\queue.lua", "-- edited");
        File.Delete(game.PathOf(@"Mods\Remnant2Unlocker\Scripts\spawner.lua"));

        Assert.Equal(2, ModInstaller.GetStatus(game.Root).OutOfDateCount);

        var result = ModInstaller.InstallOrUpdate(game.Root);

        Assert.Equal(2, result.ChangedCount);
        Assert.Contains(result.Files, f => f.TargetPath.EndsWith("queue.lua") && f.Outcome == ModFileOutcome.Updated);
        Assert.Contains(result.Files, f => f.TargetPath.EndsWith("spawner.lua") && f.Outcome == ModFileOutcome.Installed);
        Assert.Equal("-- edited", File.ReadAllText(Path.Combine(result.BackupFolder!, @"Mods\Remnant2Unlocker\Scripts\queue.lua")));
        Assert.True(ModInstaller.GetStatus(game.Root).AreFilesUpToDate);
    }

    [Fact]
    public void InstallOrUpdate_LeavesRuntimeAndExtraFilesAlone()
    {
        using var game = new FakeGameFolder();
        game.Write(@"Mods\Remnant2Unlocker\hotkeys.json", "{\"teleport\":\"F7\"}");
        game.Write(@"Mods\Remnant2Unlocker\status.json", "{}");
        game.Write(@"Mods\Remnant2Unlocker\Scripts\my_own.lua", "-- user file");

        var result = ModInstaller.InstallOrUpdate(game.Root);

        Assert.Equal("{\"teleport\":\"F7\"}", game.Read(@"Mods\Remnant2Unlocker\hotkeys.json"));
        Assert.Equal("{}", game.Read(@"Mods\Remnant2Unlocker\status.json"));
        Assert.Equal("-- user file", game.Read(@"Mods\Remnant2Unlocker\Scripts\my_own.lua"));
        Assert.Contains(result.ExtraFiles, f => f.EndsWith("my_own.lua"));
    }

    [Fact]
    public void InstallOrUpdate_WithoutUe4ss_InstallsOurModButDoesNotCreateConsoleCommandsMod()
    {
        using var game = new FakeGameFolder(withUe4ss: false);

        var result = ModInstaller.InstallOrUpdate(game.Root);

        Assert.True(result.Success);
        Assert.False(Directory.Exists(Path.Combine(game.Mods, "ConsoleCommandsMod")));
        Assert.True(File.Exists(Path.Combine(game.ModRoot, "Scripts", "main.lua")));
        Assert.Equal("Remnant2Unlocker : 1\r\n", game.Read(@"Mods\mods.txt"));
    }

    [Fact]
    public void InstallOrUpdate_UsesEnabledTxtWhenThereIsNoModsTxt()
    {
        using var game = new FakeGameFolder();
        File.Delete(game.ModsTxt);
        game.Write(@"Mods\enabled.txt", "ConsoleCommandsMod : 0\r\n");

        ModInstaller.InstallOrUpdate(game.Root);

        Assert.False(File.Exists(game.ModsTxt));
        var enabled = game.Read(@"Mods\enabled.txt");
        Assert.StartsWith("ConsoleCommandsMod : 1\r\n", enabled);
        Assert.Contains("Remnant2Unlocker : 1", enabled);
        Assert.Contains("AllowModsMod : 1", enabled);
    }

    [Fact]
    public void InstallOrUpdate_EnablesSummonableTraitsFolder()
    {
        using var game = new FakeGameFolder();
        game.Write(@"Mods\SummonableTraits\Scripts\main.lua", TraitsMainLua);

        ModInstaller.InstallOrUpdate(game.Root);

        Assert.Contains("SummonableTraits : 1", game.Read(@"Mods\mods.txt"));
    }

    [Fact]
    public void InstallOrUpdate_GamePassNestedLayout_InstallsIntoUe4ssMods()
    {
        using var game = new FakeGameFolder(withUe4ss: false, gamePass: true);
        game.Write(@"ue4ss\mods\mods.txt", FakeGameFolder.StockModsTxt);
        game.Write(@"ue4ss\mods\ConsoleCommandsMod\Scripts\summon_unloaded_assets.lua", FakeGameFolder.StockSummonLua);

        var result = ModInstaller.InstallOrUpdate(game.Root);

        Assert.True(result.Success);
        Assert.True(File.Exists(game.PathOf(@"ue4ss\mods\Remnant2Unlocker\Scripts\main.lua")));
        Assert.Equal(ModPayload.SummonPatch!.Content,
            File.ReadAllBytes(game.PathOf(@"ue4ss\mods\ConsoleCommandsMod\Scripts\summon_unloaded_assets.lua")));
        Assert.False(Directory.Exists(game.Mods));
    }

    [Fact]
    public void InstallOrUpdate_SteamNestedUe4ssWithStrayModsFolder_InstallsWhereUe4ssLoadsMods()
    {
        // A user's Steam log: UE4SS "v3.0.1 Beta" running from Win64\ue4ss, plus a leftover Win64\Mods.
        using var game = new FakeGameFolder(withUe4ss: false);
        game.Write(@"ue4ss\UE4SS.dll", "fake ue4ss");
        game.Write(@"ue4ss\Mods\mods.txt", FakeGameFolder.StockModsTxt);
        game.Write(@"Mods\Remnant2Unlocker\items.json", "[]");

        var result = ModInstaller.InstallOrUpdate(game.Root);

        Assert.True(result.Success);
        Assert.True(File.Exists(game.PathOf(@"ue4ss\Mods\Remnant2Unlocker\Scripts\main.lua")));
        Assert.False(File.Exists(game.PathOf(@"Mods\Remnant2Unlocker\Scripts\main.lua")));
    }

    [Fact]
    public void RestoreLatestBackup_PutsTheOldFilesBack()
    {
        using var game = new FakeGameFolder();
        ModInstaller.InstallOrUpdate(game.Root);

        var restored = ModInstaller.RestoreLatestBackup(game.Root);

        Assert.Equal(2, restored.Count);
        Assert.Equal(FakeGameFolder.StockSummonLua, game.Read(@"Mods\ConsoleCommandsMod\Scripts\summon_unloaded_assets.lua"));
        Assert.Equal(FakeGameFolder.StockModsTxt, game.Read(@"Mods\mods.txt"));
    }

    [Fact]
    public void Backups_KeepOnlyTheNewestFive()
    {
        using var game = new FakeGameFolder();
        var root = InstallBackup.GetBackupsRoot(game.Root);

        for (var i = 1; i <= 7; i++)
            Directory.CreateDirectory(Path.Combine(root, $"2026-01-0{i}_12-00-00"));

        InstallBackup.Prune(game.Root);

        Assert.Equal(
            new[] { "2026-01-07_12-00-00", "2026-01-06_12-00-00", "2026-01-05_12-00-00", "2026-01-04_12-00-00", "2026-01-03_12-00-00" },
            InstallBackup.ListBackups(game.Root).Select(Path.GetFileName));
    }

    [Fact]
    public void Backup_RefusesFilesOutsideTheGameFolder()
    {
        using var game = new FakeGameFolder();
        var outside = Path.GetTempFileName();

        try
        {
            Assert.Throws<InvalidOperationException>(() => new InstallBackup(game.Root).Save(outside));
        }
        finally
        {
            File.Delete(outside);
        }
    }
}
