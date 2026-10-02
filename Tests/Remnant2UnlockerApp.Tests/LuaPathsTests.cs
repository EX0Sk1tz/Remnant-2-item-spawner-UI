using MoonSharp.Interpreter;
using Xunit;

namespace Remnant2UnlockerApp.Tests;

// Runs the real paths.lua against a fake file system (relative to the game exe's folder, like
// io.open in the game) and a debug.getinfo whose source can be set or left useless, as under UE4SS.
public class LuaPathsTests
{
    private const string FakeEnvironment = """
        __files = {}
        __source = nil

        io = {
            open = function(path, mode)
                if __files[path] == nil then return nil end
                return { close = function(self) end }
            end
        }

        debug = {
            getinfo = function() return { source = __source } end
        }
        """;

    private static string ResolveModDir(string? scriptSource, params string[] files)
    {
        var lua = LuaModuleLoader.CreateScript(new Dictionary<string, string>(), new List<string>());
        lua.DoString(FakeEnvironment);

        var fileTable = lua.Globals.Get("__files").Table;

        foreach (var file in files)
            fileTable[file] = true;

        if (scriptSource != null)
            lua.Globals["__source"] = scriptSource;

        return lua.DoString(LuaModuleLoader.ReadScript("paths")).Table.Get("ModDir").String;
    }

    [Fact]
    public void FlatUe4ss_UsesModsNextToTheExe()
    {
        var dir = ResolveModDir(null,
            "UE4SS.dll",
            "Mods/Remnant2Unlocker/Scripts/paths.lua",
            "Mods/Remnant2Unlocker/items.json");

        Assert.Equal("Mods/Remnant2Unlocker/", dir);
    }

    [Fact]
    public void NestedUe4ss_UsesUe4ssMods()
    {
        var dir = ResolveModDir(null,
            "ue4ss/UE4SS.dll",
            "ue4ss/Mods/Remnant2Unlocker/Scripts/paths.lua",
            "ue4ss/Mods/Remnant2Unlocker/items.json");

        Assert.Equal("ue4ss/Mods/Remnant2Unlocker/", dir);
    }

    [Fact]
    public void NestedUe4ssWithStrayItemsJsonInMods_StillUsesUe4ssMods()
    {
        // A user's UE4SS.log: the stray Win64\Mods\Remnant2Unlocker\items.json won, so the mod
        // loaded a stale items.json and never found the app's command_queue.json.
        var dir = ResolveModDir(null,
            "ue4ss/UE4SS.dll",
            "Mods/Remnant2Unlocker/items.json",
            "ue4ss/Mods/Remnant2Unlocker/Scripts/paths.lua",
            "ue4ss/Mods/Remnant2Unlocker/items.json");

        Assert.Equal("ue4ss/Mods/Remnant2Unlocker/", dir);
    }

    [Fact]
    public void StrayFolderWithoutScripts_IsSkippedEvenWhenUe4ssIsNotDetected()
    {
        var dir = ResolveModDir(null,
            "Mods/Remnant2Unlocker/items.json",
            "ue4ss/Mods/Remnant2Unlocker/Scripts/paths.lua",
            "ue4ss/Mods/Remnant2Unlocker/items.json");

        Assert.Equal("ue4ss/Mods/Remnant2Unlocker/", dir);
    }

    [Fact]
    public void ScriptLocation_WinsWhenUe4ssReportsAFullPath()
    {
        const string modDir = @"F:\Game\Win64\ue4ss\Mods\Remnant2Unlocker\";

        var dir = ResolveModDir("@" + modDir + @"Scripts\paths.lua",
            modDir + "items.json",
            "Mods/Remnant2Unlocker/Scripts/paths.lua",
            "Mods/Remnant2Unlocker/items.json");

        Assert.Equal(modDir, dir);
    }

    [Fact]
    public void OnlyItemsJsonAnywhere_FallsBackToThatFolder()
    {
        var dir = ResolveModDir(null, "ue4ss/Mods/Remnant2Unlocker/items.json");

        Assert.Equal("ue4ss/Mods/Remnant2Unlocker/", dir);
    }

    [Fact]
    public void NothingInstalled_KeepsTheHistoricalDefault()
    {
        Assert.Equal("Mods/Remnant2Unlocker/", ResolveModDir(null));
    }
}
