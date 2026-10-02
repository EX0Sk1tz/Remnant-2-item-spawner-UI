using System.IO;

namespace Remnant2UnlockerApp.Services;

public sealed class SummonableTraitsService
{
    private readonly GamePathService _pathService;

    public SummonableTraitsService(GamePathService pathService)
    {
        _pathService = pathService;
    }

    public bool IsInstalled() => FindModFolder(_pathService.GetModsFolderPath()) != null;

    /// <summary>True if a main.lua's content is the Summonable Traits mod's entry script.</summary>
    public static bool IsSummonableTraitsScript(string content) =>
        content.Contains("RegisterConsoleCommandHandler(\"SummonTrait\"", StringComparison.OrdinalIgnoreCase)
        && content.Contains("RegisterConsoleCommandHandler(\"AddTrait\"", StringComparison.OrdinalIgnoreCase)
        && content.Contains("Material_AwardTrait_Base", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The top-level folder under <paramref name="searchRoot"/> (a Mods folder, or an extracted
    /// download) whose main.lua is the Summonable Traits script, or null.
    /// </summary>
    public static string? FindModFolder(string searchRoot)
    {
        var mainLua = FindMainLua(searchRoot);

        if (mainLua == null)
            return null;

        var relative = Path.GetRelativePath(searchRoot, mainLua);
        var first = relative.Split(Path.DirectorySeparatorChar)[0];

        // main.lua directly in searchRoot or searchRoot\Scripts: the root itself is the mod folder.
        return string.Equals(Path.GetFileName(mainLua), relative, StringComparison.OrdinalIgnoreCase)
            || string.Equals(first, "Scripts", StringComparison.OrdinalIgnoreCase)
            ? searchRoot
            : Path.Combine(searchRoot, first);
    }

    /// <summary>The Summonable Traits main.lua under <paramref name="searchRoot"/>, or null.</summary>
    public static string? FindMainLua(string searchRoot)
    {
        if (!Directory.Exists(searchRoot))
            return null;

        foreach (var mainLua in Directory.EnumerateFiles(searchRoot, "main.lua", SearchOption.AllDirectories))
        {
            try
            {
                if (IsSummonableTraitsScript(File.ReadAllText(mainLua)))
                    return mainLua;
            }
            catch (Exception ex)
            {
                // Ignore unreadable mod files; log for visibility since a locked/corrupt main.lua
                // could otherwise cause trait spawning to look "not installed" for no obvious reason.
                AppLogService.Warn($"Could not read mod file: {mainLua}", ex);
            }
        }

        return null;
    }
}
