using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using Remnant2UnlockerApp.Services;
using Xunit;

namespace Remnant2UnlockerApp.Tests;

public class PackageInstallerTests
{
    private const string TraitsMainLua =
        "RegisterConsoleCommandHandler(\"SummonTrait\", function() end)\r\n" +
        "RegisterConsoleCommandHandler(\"AddTrait\", function() end)\r\n" +
        "-- Material_AwardTrait_Base\r\n";

    // Lays out an "Allow Asset Mods"-like package (UE4SS + standard mods + AllowModsMod) under
    // <dir>\<wrapper>, like AMM.zip.
    private static void WriteAmmPackage(FakeGameFolder temp, string dir, string wrapper = "")
    {
        var root = Path.Combine(dir, wrapper);
        temp.Write(Path.Combine(root, "UE4SS.dll"), "new ue4ss");
        temp.Write(Path.Combine(root, "UE4SS-settings.ini"), "[General]\r\n");
        temp.Write(Path.Combine(root, "dwmapi.dll"), "new proxy");
        temp.Write(Path.Combine(root, @"Mods\mods.txt"), "ConsoleCommandsMod : 1\r\nAllowModsMod : 1\r\nKeybinds : 1\r\n");
        temp.Write(Path.Combine(root, @"Mods\AllowModsMod\dlls\main.dll"), "new amm");
        temp.Write(Path.Combine(root, @"Mods\ConsoleCommandsMod\Scripts\main.lua"), "require(\"summon_unloaded_assets\")\r\n");
        temp.Write(Path.Combine(root, @"Mods\ConsoleCommandsMod\Scripts\summon_unloaded_assets.lua"), FakeGameFolder.StockSummonLua);
        temp.Write(Path.Combine(root, @"Mods\ConsoleEnablerMod\Scripts\main.lua"), "-- enabler\r\n");
        temp.Write(Path.Combine(root, @"Mods\CheatManagerEnablerMod\Scripts\main.lua"), "-- cheat manager\r\n");
    }

    private static string Zip(FakeGameFolder temp, string relativeDir)
    {
        var zip = temp.PathOf(relativeDir.TrimEnd('\\') + ".zip");
        ZipFile.CreateFromDirectory(temp.PathOf(relativeDir), zip);
        return zip;
    }

    [Fact]
    public void AllowAssetMods_ZipIntoFreshGame_InstallsEverythingAndPatchesSummon()
    {
        using var game = new FakeGameFolder(withUe4ss: false);
        using var temp = new FakeGameFolder(withUe4ss: false);
        WriteAmmPackage(temp, "pkg");

        var result = PackageInstaller.InstallAllowAssetMods(game.Root, Zip(temp, "pkg"));

        Assert.True(result.Success, $"{result.Error}: {result.ErrorDetail}");
        Assert.True(PackageInstaller.IsUe4ssInstalled(game.Root));
        Assert.True(PackageInstaller.IsAllowModsModInstalled(game.Root));
        Assert.Equal("new amm", game.Read(@"Mods\AllowModsMod\dlls\main.dll"));

        // The package's stock summon was replaced by ours, our mod was installed and enabled.
        Assert.True(result.ModSync!.Success);
        var status = ModInstaller.GetStatus(game.Root);
        Assert.True(status.IsSummonPatchApplied);
        Assert.True(status.IsUpToDate);
        Assert.Contains("Remnant2Unlocker : 1\r\nKeybinds : 1", game.Read(@"Mods\mods.txt"));
    }

    [Fact]
    public void AllowAssetMods_WrapperFolder_IsUnwrapped()
    {
        using var game = new FakeGameFolder(withUe4ss: false);
        using var temp = new FakeGameFolder(withUe4ss: false);
        WriteAmmPackage(temp, "pkg", wrapper: "AMM");

        var result = PackageInstaller.InstallAllowAssetMods(game.Root, Zip(temp, "pkg"));

        Assert.True(result.Success, $"{result.Error}: {result.ErrorDetail}");
        Assert.True(File.Exists(game.PathOf("UE4SS.dll")));
        Assert.False(Directory.Exists(game.PathOf("AMM")));
    }

    [Fact]
    public void AllowAssetMods_Folder_OverExistingInstall_BacksUpAndKeepsModsTxt()
    {
        using var game = new FakeGameFolder();
        game.Write(@"Mods\mods.txt", FakeGameFolder.StockModsTxt.Replace("AllowModsMod : 1", "AllowModsMod : 1\r\nMyOtherMod : 1"));
        using var temp = new FakeGameFolder(withUe4ss: false);
        WriteAmmPackage(temp, "pkg");

        var result = PackageInstaller.InstallAllowAssetMods(game.Root, temp.PathOf("pkg"));

        Assert.True(result.Success, $"{result.Error}: {result.ErrorDetail}");
        Assert.Equal("new ue4ss", game.Read("UE4SS.dll"));
        Assert.Equal("fake ue4ss", File.ReadAllText(Path.Combine(result.BackupFolder!, "UE4SS.dll")));

        var modsTxt = game.Read(@"Mods\mods.txt");
        Assert.Contains("MyOtherMod : 1", modsTxt);
        Assert.Contains("Remnant2Unlocker : 1", modsTxt);

        // The dropped folder is used in place, never deleted.
        Assert.True(File.Exists(temp.PathOf(@"pkg\UE4SS.dll")));
    }

    [Fact]
    public void AllowAssetMods_AllowModsModOnly_WithUe4ssPresent_InstallsTheFolder()
    {
        using var game = new FakeGameFolder();
        using var temp = new FakeGameFolder(withUe4ss: false);
        temp.Write(@"pkg\AllowModsMod\dlls\main.dll", "new amm");

        var result = PackageInstaller.InstallAllowAssetMods(game.Root, Zip(temp, "pkg"));

        Assert.True(result.Success, $"{result.Error}: {result.ErrorDetail}");
        Assert.Equal(1, result.FilesWritten);
        Assert.Equal("new amm", game.Read(@"Mods\AllowModsMod\dlls\main.dll"));
        Assert.Equal("fake ue4ss", game.Read("UE4SS.dll"));
    }

    [Fact]
    public void AllowAssetMods_AllowModsModOnly_WithoutUe4ss_Fails()
    {
        using var game = new FakeGameFolder(withUe4ss: false);
        using var temp = new FakeGameFolder(withUe4ss: false);
        temp.Write(@"pkg\AllowModsMod\dlls\main.dll", "new amm");

        var result = PackageInstaller.InstallAllowAssetMods(game.Root, Zip(temp, "pkg"));

        Assert.Equal(PackageError.AllowModsModWithoutUe4ss, result.Error);
        Assert.False(Directory.Exists(game.Mods));
    }

    [Fact]
    public void AllowAssetMods_UnrelatedZip_IsRejected()
    {
        using var game = new FakeGameFolder(withUe4ss: false);
        using var temp = new FakeGameFolder(withUe4ss: false);
        temp.Write(@"pkg\readme.txt", "something else");

        var result = PackageInstaller.InstallAllowAssetMods(game.Root, Zip(temp, "pkg"));

        Assert.Equal(PackageError.NotAllowAssetMods, result.Error);
    }

    [Fact]
    public void AllowAssetMods_GivenSummonableTraits_SaysSo()
    {
        using var game = new FakeGameFolder();
        using var temp = new FakeGameFolder(withUe4ss: false);
        temp.Write(@"pkg\SummonableTraits\Scripts\main.lua", TraitsMainLua);

        var result = PackageInstaller.InstallAllowAssetMods(game.Root, Zip(temp, "pkg"));

        Assert.Equal(PackageError.LooksLikeSummonableTraits, result.Error);
    }

    [Fact]
    public void SummonableTraits_Zip_InstallsAndEnables()
    {
        using var game = new FakeGameFolder();
        using var temp = new FakeGameFolder(withUe4ss: false);
        temp.Write(@"pkg\SummonableTraits\Scripts\main.lua", TraitsMainLua);
        temp.Write(@"pkg\SummonableTraits\enabled.txt", "");

        var result = PackageInstaller.InstallSummonableTraits(game.Root, Zip(temp, "pkg"));

        Assert.True(result.Success, $"{result.Error}: {result.ErrorDetail}");
        Assert.Equal("SummonableTraits", result.ModFolderName);
        Assert.Equal(TraitsMainLua, game.Read(@"Mods\SummonableTraits\Scripts\main.lua"));
        Assert.Contains("SummonableTraits : 1", game.Read(@"Mods\mods.txt"));
    }

    [Fact]
    public void SummonableTraits_ScriptsOnlyArchive_GetsAModFolder()
    {
        using var game = new FakeGameFolder();
        using var temp = new FakeGameFolder(withUe4ss: false);
        temp.Write(@"pkg\main.lua", TraitsMainLua);

        var result = PackageInstaller.InstallSummonableTraits(game.Root, Zip(temp, "pkg"));

        Assert.True(result.Success, $"{result.Error}: {result.ErrorDetail}");
        Assert.Equal(TraitsMainLua, game.Read(@"Mods\SummonableTraits\Scripts\main.lua"));
    }

    [Fact]
    public void SummonableTraits_Reinstall_UsesTheExistingFolder()
    {
        using var game = new FakeGameFolder();
        game.Write(@"Mods\MyTraits\Scripts\main.lua", TraitsMainLua);
        using var temp = new FakeGameFolder(withUe4ss: false);
        temp.Write(@"pkg\SummonableTraits\Scripts\main.lua", TraitsMainLua + "-- v2\r\n");

        var result = PackageInstaller.InstallSummonableTraits(game.Root, temp.PathOf("pkg"));

        Assert.True(result.Success, $"{result.Error}: {result.ErrorDetail}");
        Assert.Equal("MyTraits", result.ModFolderName);
        Assert.EndsWith("-- v2\r\n", game.Read(@"Mods\MyTraits\Scripts\main.lua"));
        Assert.False(Directory.Exists(game.PathOf(@"Mods\SummonableTraits")));
    }

    [Fact]
    public void SummonableTraits_GivenAllowAssetMods_SaysSo()
    {
        using var game = new FakeGameFolder();
        using var temp = new FakeGameFolder(withUe4ss: false);
        WriteAmmPackage(temp, "pkg");

        var result = PackageInstaller.InstallSummonableTraits(game.Root, Zip(temp, "pkg"));

        Assert.Equal(PackageError.LooksLikeAllowAssetMods, result.Error);
    }

    [Fact]
    public void UnsupportedExtension_And_MissingFile_AreReported()
    {
        using var game = new FakeGameFolder();
        game.Write("mod.exe", "x");

        Assert.Equal(PackageError.UnsupportedFormat, PackageInstaller.InstallAllowAssetMods(game.Root, game.PathOf("mod.exe")).Error);
        Assert.Equal(PackageError.NotFound, PackageInstaller.InstallAllowAssetMods(game.Root, game.PathOf("missing.zip")).Error);
    }

    [Fact]
    public void AccessDenied_IsReported()
    {
        using var game = new FakeGameFolder();
        using var temp = new FakeGameFolder(withUe4ss: false);
        WriteAmmPackage(temp, "pkg");
        var locked = game.PathOf("UE4SS.dll");
        File.SetAttributes(locked, FileAttributes.ReadOnly);

        try
        {
            var result = PackageInstaller.InstallAllowAssetMods(game.Root, temp.PathOf("pkg"));

            Assert.Equal(PackageError.AccessDenied, result.Error);
            Assert.True(result.AccessDenied);
        }
        finally
        {
            File.SetAttributes(locked, FileAttributes.Normal);
        }
    }

    [Fact]
    public void ExtractedTempFolder_IsCleanedUp()
    {
        using var game = new FakeGameFolder(withUe4ss: false);
        using var temp = new FakeGameFolder(withUe4ss: false);
        WriteAmmPackage(temp, "pkg");
        var tempRoot = Path.Combine(Path.GetTempPath(), "Remnant2Unlocker");
        var before = Directory.Exists(tempRoot) ? Directory.GetDirectories(tempRoot, "pkg-*").Length : 0;

        PackageInstaller.InstallAllowAssetMods(game.Root, Zip(temp, "pkg"));

        Assert.Equal(before, Directory.Exists(tempRoot) ? Directory.GetDirectories(tempRoot, "pkg-*").Length : 0);
    }

    // .rar/.7z go through Windows' tar.exe. tar can't write RAR, but it writes 7z, which takes the same path.
    [Fact]
    public void SummonableTraits_7z_ExtractsWithTar()
    {
        if (!File.Exists(PackageInstaller.TarPath))
            return;

        using var game = new FakeGameFolder();
        using var temp = new FakeGameFolder(withUe4ss: false);
        temp.Write(@"pkg\SummonableTraits\Scripts\main.lua", TraitsMainLua);
        var archive = temp.PathOf("traits.7z");

        var create = new ProcessStartInfo(PackageInstaller.TarPath) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in new[] { "--format", "7zip", "-cf", archive, "-C", temp.PathOf("pkg"), "SummonableTraits" })
            create.ArgumentList.Add(arg);

        using (var process = Process.Start(create)!)
        {
            process.WaitForExit();

            if (process.ExitCode != 0)
                return; // this tar build can't write 7z; nothing to test here
        }

        var result = PackageInstaller.InstallSummonableTraits(game.Root, archive);

        Assert.True(result.Success, $"{result.Error}: {result.ErrorDetail}");
        Assert.Equal(TraitsMainLua, game.Read(@"Mods\SummonableTraits\Scripts\main.lua"));
    }
}
