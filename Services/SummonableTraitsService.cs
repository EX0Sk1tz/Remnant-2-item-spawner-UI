using System.IO;

namespace Remnant2UnlockerApp.Services;

public sealed class SummonableTraitsService
{
    private readonly GamePathService _pathService;

    public SummonableTraitsService(GamePathService pathService)
    {
        _pathService = pathService;
    }

    public bool IsInstalled()
    {
        var modsPath = Path.Combine(_pathService.Win64Path, "Mods");

        if (!Directory.Exists(modsPath))
            return false;

        foreach (var mainLua in Directory.EnumerateFiles(modsPath, "main.lua", SearchOption.AllDirectories))
        {
            try
            {
                var content = File.ReadAllText(mainLua);

                if (content.Contains("RegisterConsoleCommandHandler(\"SummonTrait\"", StringComparison.OrdinalIgnoreCase)
                    && content.Contains("RegisterConsoleCommandHandler(\"AddTrait\"", StringComparison.OrdinalIgnoreCase)
                    && content.Contains("Material_AwardTrait_Base", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                // Ignore unreadable mod files; log for visibility since a locked/corrupt main.lua
                // could otherwise cause trait spawning to look "not installed" for no obvious reason.
                AppLogService.Warn($"Could not read mod file: {mainLua}", ex);
            }
        }

        return false;
    }
}