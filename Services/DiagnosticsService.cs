using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Remnant2UnlockerApp.Models;

namespace Remnant2UnlockerApp.Services;

public sealed class DiagnosticsService
{
    private readonly GamePathService _pathService;
    private readonly SummonableTraitsService _summonableTraitsService;

    public DiagnosticsService(GamePathService pathService, SummonableTraitsService summonableTraitsService)
    {
        _pathService = pathService;
        _summonableTraitsService = summonableTraitsService;
    }

    public DiagnosticReport Run()
    {
        var report = new DiagnosticReport();
        var path = _pathService.Win64Path;

        if (string.IsNullOrWhiteSpace(path))
        {
            AddIssue(
                report,
                "Game path is not configured",
                "Game path missing",
                "The app does not know where your Remnant 2 Win64 folder is.",
                "Click Browse and select the folder that contains Remnant2-Win64-Shipping.exe.",
                "Steam/Epic expected folder: Remnant2\\Remnant2\\Binaries\\Win64\nGame Pass expected folder: XboxGames\\Remnant 2\\Content\\Remnant2\\Binaries\\WinGDK",
                DiagnosticStatus.Error);

            LogSummary(report);
            return report;
        }

        if (!Directory.Exists(path))
        {
            AddIssue(
                report,
                "Selected folder does not exist",
                "Invalid folder",
                "The selected Win64 path does not exist on this system.",
                "Click Browse and select the real Win64 folder again.",
                path,
                DiagnosticStatus.Error);

            LogSummary(report);
            return report;
        }

        AddPass(report, "Game path is configured", "The Win64 folder is set and exists.", path);

        var modsPath = Path.Combine(path, "Mods");

        CheckDirectory(
            report,
            modsPath,
            "Mods folder",
            "Mods folder missing",
            "The selected Win64 folder does not contain a Mods folder.",
            "Install UE4SS into the Win64 folder again.",
            true);

        CheckAllowModsMod(report, modsPath);

        var unlockerPath = Path.Combine(modsPath, "Remnant2Unlocker");

        CheckDirectory(
            report,
            unlockerPath,
            "Remnant2Unlocker folder",
            "Unlocker missing",
            "The Remnant2Unlocker mod folder is missing.",
            "Copy the Remnant2Unlocker folder into Win64\\Mods.",
            true);

        CheckGameExecutable(report, path);

        CheckFile(
            report,
            Path.Combine(unlockerPath, "command_queue.json"),
            "command_queue.json",
            "Queue file missing",
            "The command queue file is missing.",
            "Create command_queue.json or reinstall the release package.",
            false);

        CheckFile(
            report,
            Path.Combine(unlockerPath, "status.json"),
            "status.json",
            "Status file missing",
            "The status file is missing.",
            "Create status.json or start the game once with the mod installed.",
            false);

        CheckFile(
            report,
            Path.Combine(unlockerPath, "scripts", "main.lua"),
            "Remnant2Unlocker main.lua",
            "Unlocker script missing",
            "The Lua entry script for Remnant2Unlocker is missing.",
            "Reinstall the scripts folder from the release package.",
            true);

        CheckJsonFile(
            report,
            Path.Combine(unlockerPath, "items.json"),
            "items.json",
            "Item catalog corrupted",
            "items.json exists but could not be parsed as valid JSON, so the item list will fail to load.",
            "Reinstall the Remnant2Unlocker mod package to restore a valid items.json.");

        CheckJsonFile(
            report,
            Path.Combine(unlockerPath, "command_queue.json"),
            "command_queue.json",
            "Queue file corrupted",
            "command_queue.json exists but could not be parsed as valid JSON.",
            "Delete the file, or spawn/unlock an item once to let the app regenerate it automatically.");

        CheckJsonFile(
            report,
            Path.Combine(unlockerPath, "status.json"),
            "status.json",
            "Status file corrupted",
            "status.json exists but could not be parsed as valid JSON, so spawn progress cannot be read.",
            "Delete the file; it will be regenerated automatically the next time the bridge mod runs.");

        CheckJsonFile(
            report,
            Path.Combine(unlockerPath, "hotkeys.json"),
            "hotkeys.json",
            "Hotkey settings corrupted",
            "hotkeys.json exists but could not be parsed as valid JSON, so saved hotkeys and settings may be ignored.",
            "Delete the file, or change any hotkey/setting in the app to let it regenerate automatically.");

        CheckJsonFile(
            report,
            Path.Combine(unlockerPath, "cheats.json"),
            "cheats.json",
            "Cheat settings corrupted",
            "cheats.json exists but could not be parsed as valid JSON.",
            "Delete the file, or toggle a cheat setting in the app to let it regenerate automatically.");

        CheckRequiredLuaMod(
            report,
            modsPath,
            "ConsoleCommandsMod",
            Path.Combine("scripts", "summon_unloaded_assets.lua"),
            "ConsoleCommandsMod missing",
            "Console command handler missing",
            "The custom summon handler is required for unloaded assets.",
            "Install ConsoleCommandsMod and make sure summon_unloaded_assets.lua exists.");

        CheckRequiredLuaMod(
            report,
            modsPath,
            "ConsoleEnablerMod",
            Path.Combine("scripts", "main.lua"),
            "ConsoleEnablerMod missing",
            "Console enabler missing",
            "The console enabler is required for force spawning and manual command testing.",
            "Install ConsoleEnablerMod and make sure scripts\\main.lua exists.");

        CheckRequiredLuaMod(
            report,
            modsPath,
            "CheatManagerEnablerMod",
            Path.Combine("scripts", "main.lua"),
            "CheatManagerEnablerMod missing",
            "CheatManager missing",
            "The CheatManager must be enabled or summon commands may load assets without spawning items.",
            "Install CheatManagerEnablerMod and make sure scripts\\main.lua exists.");

        CheckModEnabled(report, path, "AllowModsMod");
        CheckModEnabled(report, path, "ConsoleCommandsMod");
        CheckModEnabled(report, path, "ConsoleEnablerMod");
        CheckModEnabled(report, path, "CheatManagerEnablerMod");
        CheckModEnabled(report, path, "Remnant2Unlocker");

        CheckSummonableTraits(report);

        if (IsGameRunning())
        {
            AddIssue(
                report,
                "Game is running — a few extra checks are paused",
                "This is expected, not a problem",
                "Remnant 2 is currently open, so the app skips reading UE4SS.log while the game has it open to avoid file conflicts. Everything above this has already been checked normally.",
                "No action needed. If you also want the log-based checks (mod startup confirmation, CheatManager status), close Remnant 2 and click Run Checks again.",
                "Skipped while the game is running: UE4SS.log startup-line checks, AllowModsMod runtime patch check, CheatManager runtime check.",
                DiagnosticStatus.Info);

            LogSummary(report);
            return report;
        }

        CheckLog(report, path);

        LogSummary(report);
        return report;
    }

    private static void LogSummary(DiagnosticReport report)
    {
        report.Results = report.Results
            .OrderBy(r => r.Status switch
            {
                DiagnosticStatus.Error => 0,
                DiagnosticStatus.Info => 1,
                _ => 2
            })
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("Remnant 2 Unlocker — Setup Diagnostics");
        sb.AppendLine($"Run at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(report.Summary);
        sb.AppendLine();

        foreach (var result in report.Results)
        {
            var level = result.Status switch
            {
                DiagnosticStatus.Error => "ERROR",
                DiagnosticStatus.Info => "INFO",
                _ => "PASS"
            };

            sb.AppendLine($"[{level}] {result.Title}");
            sb.AppendLine($"    {result.Details}");

            if (!string.IsNullOrWhiteSpace(result.Fix))
                sb.AppendLine($"    Fix: {result.Fix}");

            sb.AppendLine($"    Technical: {result.TechnicalDetails}");
            sb.AppendLine();
        }

        AppLogService.WriteDiagnosticsReport(sb.ToString());
    }

    private static bool IsGameRunning()
    {
        return Process.GetProcessesByName("Remnant2-Win64-Shipping").Length > 0
            || Process.GetProcessesByName("Remnant2-WinGDK-Shipping").Length > 0;
    }

    private static void CheckGameExecutable(DiagnosticReport report, string path)
    {
        var steamExe = Path.Combine(path, "Remnant2-Win64-Shipping.exe");
        var gamePassExe = Path.Combine(path, "Remnant2-WinGDK-Shipping.exe");

        if (File.Exists(steamExe))
        {
            AddPass(report, "Game executable", "Found the Steam/Epic executable.", steamExe);
            return;
        }

        if (File.Exists(gamePassExe))
        {
            AddPass(report, "Game executable", "Found the Game Pass executable.", gamePassExe);
            return;
        }

        AddIssue(
            report,
            "Game executable missing",
            "Game exe missing",
            "The selected folder does not contain a supported Remnant 2 executable.",
            "Select the exact executable folder. Steam/Epic uses Binaries\\Win64. Game Pass uses Binaries\\WinGDK.",
            $"Checked for:{Environment.NewLine}{steamExe}{Environment.NewLine}{gamePassExe}",
            DiagnosticStatus.Error);
    }

    private static void CheckAllowModsMod(DiagnosticReport report, string modsPath)
    {
        var modPath = Path.Combine(modsPath, "AllowModsMod");
        var dllPath = Path.Combine(modPath, "dlls", "main.dll");

        if (!Directory.Exists(modPath))
        {
            AddIssue(
                report,
                "AllowModsMod folder missing",
                "AllowModsMod missing",
                "AllowModsMod is required because it patches the game so UE4SS mods can run correctly.",
                "Reinstall UE4SS or copy the AllowModsMod folder into Win64\\Mods.",
                modPath,
                DiagnosticStatus.Error);

            return;
        }

        if (!File.Exists(dllPath))
        {
            AddIssue(
                report,
                "AllowModsMod binary missing",
                "AllowModsMod incomplete",
                "AllowModsMod exists, but dlls\\main.dll is missing.",
                "Replace the full AllowModsMod folder from a clean UE4SS package.",
                dllPath,
                DiagnosticStatus.Error);

            return;
        }

        AddPass(report, "AllowModsMod", "Mod folder and binary are present.", dllPath);
    }

    private static void CheckRequiredLuaMod(
        DiagnosticReport report,
        string modsPath,
        string modName,
        string requiredRelativeFile,
        string title,
        string hint,
        string details,
        string fix)
    {
        var modPath = Path.Combine(modsPath, modName);
        var requiredFile = Path.Combine(modPath, requiredRelativeFile);

        if (!Directory.Exists(modPath))
        {
            AddIssue(report, title, hint, details, fix, modPath, DiagnosticStatus.Error);
            return;
        }

        if (!File.Exists(requiredFile))
        {
            AddIssue(
                report,
                $"{modName} script missing",
                $"{modName} incomplete",
                $"{modName} exists, but the required script is missing.",
                $"Replace the full {modName} folder from a clean release package.",
                requiredFile,
                DiagnosticStatus.Error);

            return;
        }

        AddPass(report, modName, "Mod folder and required script are present.", requiredFile);
    }

    private static void CheckModEnabled(DiagnosticReport report, string win64Path, string modName)
    {
        var modsTxt = Path.Combine(win64Path, "Mods", "mods.txt");
        var enabledTxt = Path.Combine(win64Path, "Mods", "enabled.txt");

        var content = "";

        if (File.Exists(modsTxt))
            content += File.ReadAllText(modsTxt);

        if (File.Exists(enabledTxt))
            content += Environment.NewLine + File.ReadAllText(enabledTxt);

        if (string.IsNullOrWhiteSpace(content))
        {
            AddIssue(
                report,
                "No mods.txt or enabled.txt found",
                "Mod config missing",
                "UE4SS needs a mod config file to know which mods are enabled.",
                "Make sure Win64\\Mods\\mods.txt or Win64\\Mods\\enabled.txt exists.",
                $"Checked: {modsTxt} and {enabledTxt}",
                DiagnosticStatus.Error);

            return;
        }

        if (!content.Contains($"{modName} : 1", StringComparison.OrdinalIgnoreCase))
        {
            AddIssue(
                report,
                $"{modName} is not enabled",
                $"{modName} disabled",
                $"{modName} is missing or disabled in the UE4SS mod config.",
                $"Add or change this line: {modName} : 1",
                $"Checked mods.txt and enabled.txt for {modName} : 1",
                DiagnosticStatus.Error);

            return;
        }

        AddPass(report, $"{modName} enabled", $"{modName} : 1 found in the UE4SS mod config.", $"{modsTxt} / {enabledTxt}");
    }

    private void CheckSummonableTraits(DiagnosticReport report)
    {
        if (_summonableTraitsService.IsInstalled())
        {
            AddPass(
                report,
                "Summonable Traits mod",
                "Detected a main.lua registering SummonTrait/AddTrait console commands.",
                "Checked all main.lua files under Mods\\.");

            return;
        }

        AddIssue(
            report,
            "Summonable Traits mod not detected",
            "Optional: trait spawning",
            "Trait, Core Trait, and Archetype Trait items require a separate, optional \"Summonable Traits\" mod that registers the SummonTrait/AddTrait console commands. It was not found under Mods\\.",
            "Install the Summonable Traits mod if you want to spawn Trait/Archetype Trait items. Everything else works without it.",
            "Checked all main.lua files under Mods\\ for RegisterConsoleCommandHandler(\"SummonTrait\"/\"AddTrait\") and Material_AwardTrait_Base.",
            DiagnosticStatus.Info);
    }

    private static void CheckJsonFile(
        DiagnosticReport report,
        string path,
        string title,
        string hint,
        string details,
        string fix)
    {
        if (!File.Exists(path))
            return;

        try
        {
            using var _ = JsonDocument.Parse(File.ReadAllText(path));
            AddPass(report, $"{title} is valid JSON", "Parsed successfully.", path);
        }
        catch (Exception ex)
        {
            AddIssue(report, $"{title} is corrupted", hint, details, fix, ex.Message, DiagnosticStatus.Error);
        }
    }

    private static void CheckLog(DiagnosticReport report, string win64Path)
    {
        var logPath = FindLatestLog(win64Path);

        if (logPath == null)
        {
            AddIssue(
                report,
                "UE4SS log not found",
                "No UE4SS log",
                "The app could not find a UE4SS log file.",
                "Start the game once with UE4SS installed, then reopen the app.",
                win64Path,
                DiagnosticStatus.Info);

            return;
        }

        string log;

        try
        {
            log = File.ReadAllText(logPath);
        }
        catch (IOException ex)
        {
            AddIssue(
                report,
                "UE4SS log could not be read",
                "Log locked",
                "The app could not read UE4SS.log. The file may still be locked by the running game.",
                "Close Remnant 2 and run diagnostics again.",
                ex.Message,
                DiagnosticStatus.Info);

            return;
        }

        CheckLogContains(
            report,
            log,
            "Starting C++ mod 'AllowModsMod'",
            "AllowModsMod started",
            "AllowModsMod did not start",
            "AllowModsMod not loaded",
            "UE4SS did not start AllowModsMod.",
            "Check that AllowModsMod : 1 is set and that Win64\\Mods\\AllowModsMod\\dlls\\main.dll exists.",
            logPath);

        CheckLogContains(
            report,
            log,
            "[AllowModsMod]: Init.",
            "AllowModsMod initialized",
            "AllowModsMod did not initialize",
            "AllowModsMod init missing",
            "AllowModsMod started, but the init log line was not found.",
            "Replace AllowModsMod from a clean UE4SS package.",
            logPath);

        CheckLogContains(
            report,
            log,
            "[AllowModsMod]: Delegate found and patched.",
            "AllowModsMod patch applied",
            "AllowModsMod patch was not applied",
            "AllowModsMod patch missing",
            "AllowModsMod did not report that the game delegate was patched.",
            "Use the bundled UE4SS version and replace AllowModsMod from the release package.",
            logPath);

        CheckLogContains(
            report,
            log,
            "Starting Lua mod 'Remnant2Unlocker'",
            "Remnant2Unlocker started",
            "Remnant2Unlocker did not start",
            "Unlocker not loaded",
            "UE4SS did not start the Remnant2Unlocker Lua mod.",
            "Check mods.txt or enabled.txt and make sure Remnant2Unlocker : 1 is set.",
            logPath);

        CheckLogContains(
            report,
            log,
            "Starting Lua mod 'ConsoleCommandsMod'",
            "ConsoleCommandsMod started",
            "ConsoleCommandsMod did not start",
            "ConsoleCommandsMod not loaded",
            "UE4SS did not start ConsoleCommandsMod.",
            "Enable ConsoleCommandsMod and restart the game.",
            logPath);

        CheckLogContains(
            report,
            log,
            "Starting Lua mod 'CheatManagerEnablerMod'",
            "CheatManagerEnablerMod started",
            "CheatManagerEnablerMod did not start",
            "CheatManager not loaded",
            "UE4SS did not start CheatManagerEnablerMod.",
            "Enable CheatManagerEnablerMod and verify that scripts\\main.lua exists.",
            logPath);

        if (log.Contains("[CheatManager Creator] Enabled CheatManager", StringComparison.OrdinalIgnoreCase))
        {
            AddPass(report, "CheatManager enabled", "Found 'Enabled CheatManager' in the UE4SS log.", logPath);
        }
        else
        {
            AddIssue(
                report,
                "CheatManager was not enabled",
                "CheatManager inactive",
                "The log does not show that the CheatManager was created. Summon commands may load assets but spawn nothing.",
                "Load into a world, then check if CheatManagerEnablerMod logs 'Enabled CheatManager'. If not, replace CheatManagerEnablerMod with the fixed fallback version.",
                logPath,
                DiagnosticStatus.Error);
        }
    }

    private static void CheckLogContains(
        DiagnosticReport report,
        string log,
        string expected,
        string passTitle,
        string title,
        string hint,
        string details,
        string fix,
        string logPath)
    {
        if (log.Contains(expected, StringComparison.OrdinalIgnoreCase))
        {
            AddPass(report, passTitle, $"Found \"{expected}\" in the UE4SS log.", logPath);
            return;
        }

        AddIssue(report, title, hint, details, fix, logPath, DiagnosticStatus.Error);
    }

    private static string? FindLatestLog(string win64Path)
    {
        var direct = Path.Combine(win64Path, "UE4SS.log");

        if (File.Exists(direct))
            return direct;

        return Directory
            .GetFiles(win64Path, "*.log", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static void CheckFile(
        DiagnosticReport report,
        string path,
        string title,
        string hint,
        string details,
        string fix,
        bool isError)
    {
        if (!File.Exists(path))
        {
            AddIssue(report, $"{title} missing", hint, details, fix, path, isError ? DiagnosticStatus.Error : DiagnosticStatus.Info);
            return;
        }

        AddPass(report, title, "File is present.", path);
    }

    private static void CheckDirectory(
        DiagnosticReport report,
        string path,
        string title,
        string hint,
        string details,
        string fix,
        bool isError)
    {
        if (!Directory.Exists(path))
        {
            AddIssue(report, $"{title} missing", hint, details, fix, path, isError ? DiagnosticStatus.Error : DiagnosticStatus.Info);
            return;
        }

        AddPass(report, title, "Folder is present.", path);
    }

    private static void AddIssue(
        DiagnosticReport report,
        string title,
        string hint,
        string details,
        string fix,
        string technicalDetails,
        DiagnosticStatus status)
    {
        report.Results.Add(new DiagnosticCheckResult
        {
            Title = title,
            Hint = hint,
            Details = details,
            Fix = fix,
            TechnicalDetails = technicalDetails,
            Status = status
        });
    }

    private static void AddPass(DiagnosticReport report, string title, string details, string technicalDetails)
    {
        report.Results.Add(new DiagnosticCheckResult
        {
            Title = title,
            Hint = "Passed",
            Details = details,
            Fix = "",
            TechnicalDetails = technicalDetails,
            Status = DiagnosticStatus.Passed
        });
    }
}
