using System.IO;
using Remnant2UnlockerApp.Services;
using Xunit;

namespace Remnant2UnlockerApp.Tests;

public class GameLocatorTests
{
    [Fact]
    public void ParseSteamLibraryFolders_CurrentFormat()
    {
        const string vdf = """
            "libraryfolders"
            {
            	"0"
            	{
            		"path"		"C:\\Program Files (x86)\\Steam"
            		"label"		""
            		"apps"
            		{
            			"228980"		"2140564713"
            		}
            	}
            	"1"
            	{
            		"path"		"G:\\Steam"
            		"label"		""
            		"apps"
            		{
            			"1282100"		"54783827452"
            		}
            	}
            }
            """;

        Assert.Equal(new[] { @"C:\Program Files (x86)\Steam", @"G:\Steam" }, GameLocator.ParseSteamLibraryFolders(vdf));
    }

    [Fact]
    public void ParseSteamLibraryFolders_LegacyFormat()
    {
        const string vdf = """
            "LibraryFolders"
            {
            	"TimeNextStatsReport"		"1600000000"
            	"ContentStatsID"		"-123"
            	"1"		"D:\\SteamLibrary"
            	"2"		"E:\\Games\\Steam"
            }
            """;

        Assert.Equal(new[] { @"D:\SteamLibrary", @"E:\Games\Steam" }, GameLocator.ParseSteamLibraryFolders(vdf));
    }

    [Fact]
    public void ParseEpicManifest_Remnant_ReturnsInstallLocation()
    {
        const string json = """
            { "FormatVersion": 0, "DisplayName": "Remnant II", "AppName": "e4b3e7d1b3a24c2c9e0f", "InstallLocation": "D:\\Epic Games\\Remnant2" }
            """;

        Assert.Equal(@"D:\Epic Games\Remnant2", GameLocator.ParseEpicManifest(json));
    }

    [Fact]
    public void ParseEpicManifest_OtherGame_ReturnsNull()
    {
        const string json = """
            { "DisplayName": "Fortnite", "AppName": "Fortnite", "InstallLocation": "G:\\Epic Games\\Fortnite" }
            """;

        Assert.Null(GameLocator.ParseEpicManifest(json));
    }

    [Fact]
    public void FromSteamLibraries_FindsTheWin64FolderWithTheExe()
    {
        using var library = new FakeGameFolder(withUe4ss: false);
        var win64 = Path.Combine(library.Root, @"steamapps\common\Remnant2\Remnant2\Binaries\Win64");
        Directory.CreateDirectory(win64);
        File.WriteAllText(Path.Combine(win64, "Remnant2-Win64-Shipping.exe"), "fake");

        var candidates = GameLocator.FromSteamLibraries(new[] { library.Root, Path.Combine(library.Root, "nothing") }).ToList();

        Assert.Equal(2, candidates.Count);
        Assert.True(candidates[0].IsValid);
        Assert.Equal(win64, candidates[0].Win64Path);
        Assert.Equal(GamePlatform.Steam, candidates[0].Platform);
        Assert.False(candidates[1].IsValid);
    }

    [Fact]
    public void FromEpicManifests_ResolvesTheNestedBinariesFolder()
    {
        using var temp = new FakeGameFolder(withUe4ss: false);
        var install = Path.Combine(temp.Root, "Epic", "Remnant2");
        var win64 = Path.Combine(install, @"Remnant2\Binaries\Win64");
        Directory.CreateDirectory(win64);
        File.WriteAllText(Path.Combine(win64, "Remnant2-Win64-Shipping.exe"), "fake");

        var manifests = Path.Combine(temp.Root, "Manifests");
        Directory.CreateDirectory(manifests);
        File.WriteAllText(Path.Combine(manifests, "a.item"),
            $$"""{ "DisplayName": "Remnant 2", "InstallLocation": "{{install.Replace(@"\", @"\\")}}" }""");
        File.WriteAllText(Path.Combine(manifests, "b.item"), "not json");

        var candidate = Assert.Single(GameLocator.FromEpicManifests(manifests));

        Assert.Equal(win64, candidate.Win64Path);
        Assert.True(candidate.IsValid);
        Assert.Equal(GamePlatform.Epic, candidate.Platform);
    }

    [Fact]
    public void NormalizeSelection_MapsTheGameRootToTheExeFolder()
    {
        using var temp = new FakeGameFolder(withUe4ss: false);
        var gameRoot = Path.Combine(temp.Root, @"steamapps\common\Remnant2");
        var win64 = Path.Combine(gameRoot, @"Remnant2\Binaries\Win64");
        Directory.CreateDirectory(win64);
        File.WriteAllText(Path.Combine(win64, "Remnant2-Win64-Shipping.exe"), "fake");

        Assert.Equal(win64, GameLocator.NormalizeSelection(gameRoot));
        Assert.Equal(win64, GameLocator.NormalizeSelection(win64));
        Assert.Equal(Path.Combine(temp.Root, "steamapps"), GameLocator.NormalizeSelection(Path.Combine(temp.Root, "steamapps")));
        Assert.True(GameLocator.IsSteamLibraryPath(win64));
    }

    [Fact]
    public void FromDriveRoots_UsesTheXboxGamesLayout()
    {
        var candidate = Assert.Single(GameLocator.FromDriveRoots(new[] { @"X:\" }));

        Assert.Equal(@"X:\XboxGames\Remnant 2\Content\Remnant2\Binaries\WinGDK", candidate.Win64Path);
        Assert.Equal("Game Pass", candidate.PlatformName);
    }
}
