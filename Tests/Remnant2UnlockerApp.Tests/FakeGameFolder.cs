using System.IO;
using System.Text;

namespace Remnant2UnlockerApp.Tests;

// A throwaway Win64 folder that looks like a Steam install: fake game exe and, optionally, UE4SS
// with its standard mods. Never points at a real game folder.
internal sealed class FakeGameFolder : IDisposable
{
    // UE4SS 3.x's default mods.txt, as found on the test machine (with AllowModsMod added).
    public const string StockModsTxt =
        "CheatManagerEnablerMod : 1\r\n" +
        "ActorDumperMod : 0\r\n" +
        "ConsoleCommandsMod : 1\r\n" +
        "ConsoleEnablerMod : 1\r\n" +
        "SplitScreenMod : 0\r\n" +
        "LineTraceMod : 0\r\n" +
        "BPModLoaderMod : 1\r\n" +
        "BPML_GenericFunctions : 1\r\n" +
        "jsbLuaProfilerMod : 0\r\n" +
        "AllowModsMod : 1\r\n" +
        "\r\n" +
        "\r\n" +
        "\r\n" +
        "; Built-in keybinds, do not move up!\r\n" +
        "Keybinds : 1\r\n";

    // UE4SS's stock summon handler: plain "summon <path>", no quantity/level.
    public const string StockSummonLua =
        "RegisterConsoleCommandHandler(\"summon\", function(FullCommand, Parameters)\r\n" +
        "    if #Parameters < 1 then return false end\r\n" +
        "    LoadAsset(Parameters[1])\r\n" +
        "    return false\r\n" +
        "end)\r\n";

    public FakeGameFolder(bool withUe4ss = true, bool gamePass = false)
    {
        Root = Path.Combine(Path.GetTempPath(), "R2UnlockerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);

        File.WriteAllText(Path.Combine(Root, gamePass ? "Remnant2-WinGDK-Shipping.exe" : "Remnant2-Win64-Shipping.exe"), "fake");

        if (!withUe4ss)
            return;

        File.WriteAllText(Path.Combine(Root, "UE4SS.dll"), "fake ue4ss");
        File.WriteAllText(Path.Combine(Root, "dwmapi.dll"), "fake proxy");
        Write(@"Mods\mods.txt", StockModsTxt);
        Write(@"Mods\AllowModsMod\dlls\main.dll", "fake amm");
        Write(@"Mods\ConsoleCommandsMod\Scripts\main.lua", "require(\"summon_unloaded_assets\")\r\n");
        Write(@"Mods\ConsoleCommandsMod\Scripts\summon_unloaded_assets.lua", StockSummonLua);
        Write(@"Mods\ConsoleEnablerMod\Scripts\main.lua", "-- console enabler\r\n");
        Write(@"Mods\CheatManagerEnablerMod\Scripts\main.lua", "-- cheat manager\r\n");
        Write(@"Mods\Keybinds\Scripts\main.lua", "-- keybinds\r\n");
    }

    public string Root { get; }

    public string Mods => Path.Combine(Root, "Mods");

    public string ModRoot => Path.Combine(Mods, "Remnant2Unlocker");

    public string ModsTxt => Path.Combine(Mods, "mods.txt");

    public string PathOf(string relative) => Path.Combine(Root, relative);

    public void Write(string relative, string content)
    {
        var path = PathOf(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(content));
    }

    public string Read(string relative) => File.ReadAllText(PathOf(relative));

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
