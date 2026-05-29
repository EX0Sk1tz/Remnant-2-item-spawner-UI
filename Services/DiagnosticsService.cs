using System.Diagnostics;
using System.IO;
using Remnant2UnlockerApp.Models;

namespace Remnant2UnlockerApp.Services;

public sealed class DiagnosticsService
{
    private readonly GamePathService _pathService;

    public DiagnosticsService(GamePathService pathService)
    {
        _pathService = pathService;
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
                true);

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
                true);

            return report;
        }

        CheckFile(
            report,
            Path.Combine(path, "Remnant2-Win64-Shipping.exe"),
            "Game executable missing",
            "Game exe missing",
            "The selected folder does not contain Remnant2-Win64-Shipping.exe.",
            "Select the exact Win64 folder, not the Steam library root.",
            true);

        var modsPath = Path.Combine(path, "Mods");

        CheckDirectory(
            report,
            modsPath,
            "Mods folder missing",
            "Mods folder missing",
            "The selected Win64 folder does not contain a Mods folder.",
            "Install UE4SS into the Win64 folder again.",
            true);

        CheckAllowModsMod(report, modsPath);

        var unlockerPath = Path.Combine(modsPath, "Remnant2Unlocker");

        CheckDirectory(
            report,
            unlockerPath,
            "Remnant2Unlocker folder missing",
            "Unlocker missing",
            "The Remnant2Unlocker mod folder is missing.",
            "Copy the Remnant2Unlocker folder into Win64\\Mods.",
            true);

        CheckGameExecutable(report, path);

        CheckFile(
            report,
            Path.Combine(unlockerPath, "command_queue.json"),
            "command_queue.json missing",
            "Queue file missing",
            "The command queue file is missing.",
            "Create command_queue.json or reinstall the release package.",
            false);

        CheckFile(
            report,
            Path.Combine(unlockerPath, "status.json"),
            "status.json missing",
            "Status file missing",
            "The status file is missing.",
            "Create status.json or start the game once with the mod installed.",
            false);

        CheckFile(
            report,
            Path.Combine(unlockerPath, "scripts", "main.lua"),
            "Remnant2Unlocker main.lua missing",
            "Unlocker script missing",
            "The Lua entry script for Remnant2Unlocker is missing.",
            "Reinstall the scripts folder from the release package.",
            true);

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

        if (IsGameRunning())
        {
            AddIssue(
                report,
                "Runtime log checks skipped",
                "Game is running",
                "The game is currently running, so the app will not read UE4SS.log to avoid file access conflicts.",
                "Close Remnant 2 and reopen diagnostics to run the full UE4SS log checks.",
                "Skipped checks: UE4SS.log startup checks, AllowModsMod runtime patch check, CheatManager runtime check.",
                false);

            return report;
        }

        CheckLog(report, path);

        return report;
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

        if (File.Exists(steamExe) || File.Exists(gamePassExe))
            return;

        AddIssue(
            report,
            "Game executable missing",
            "Game exe missing",
            "The selected folder does not contain a supported Remnant 2 executable.",
            "Select the exact executable folder. Steam/Epic uses Binaries\\Win64. Game Pass uses Binaries\\WinGDK.",
            $"Checked for:{Environment.NewLine}{steamExe}{Environment.NewLine}{gamePassExe}",
            true);
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
                true);

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
                true);
        }
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
            AddIssue(report, title, hint, details, fix, modPath, true);
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
                true);
        }
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
                true);

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
                true);
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
                false);

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
                false);

            return;
        }

        CheckLogContains(
            report,
            log,
            "Starting C++ mod 'AllowModsMod'",
            "AllowModsMod did not start",
            "AllowModsMod not loaded",
            "UE4SS did not start AllowModsMod.",
            "Check that AllowModsMod : 1 is set and that Win64\\Mods\\AllowModsMod\\dlls\\main.dll exists.",
            logPath);

        CheckLogContains(
            report,
            log,
            "[AllowModsMod]: Init.",
            "AllowModsMod did not initialize",
            "AllowModsMod init missing",
            "AllowModsMod started, but the init log line was not found.",
            "Replace AllowModsMod from a clean UE4SS package.",
            logPath);

        CheckLogContains(
            report,
            log,
            "[AllowModsMod]: Delegate found and patched.",
            "AllowModsMod patch was not applied",
            "AllowModsMod patch missing",
            "AllowModsMod did not report that the game delegate was patched.",
            "Use the bundled UE4SS version and replace AllowModsMod from the release package.",
            logPath);

        CheckLogContains(
            report,
            log,
            "Starting Lua mod 'Remnant2Unlocker'",
            "Remnant2Unlocker did not start",
            "Unlocker not loaded",
            "UE4SS did not start the Remnant2Unlocker Lua mod.",
            "Check mods.txt or enabled.txt and make sure Remnant2Unlocker : 1 is set.",
            logPath);

        CheckLogContains(
            report,
            log,
            "Starting Lua mod 'ConsoleCommandsMod'",
            "ConsoleCommandsMod did not start",
            "ConsoleCommandsMod not loaded",
            "UE4SS did not start ConsoleCommandsMod.",
            "Enable ConsoleCommandsMod and restart the game.",
            logPath);

        CheckLogContains(
            report,
            log,
            "Starting Lua mod 'CheatManagerEnablerMod'",
            "CheatManagerEnablerMod did not start",
            "CheatManager not loaded",
            "UE4SS did not start CheatManagerEnablerMod.",
            "Enable CheatManagerEnablerMod and verify that scripts\\main.lua exists.",
            logPath);

        if (!log.Contains("[CheatManager Creator] Enabled CheatManager", StringComparison.OrdinalIgnoreCase))
        {
            AddIssue(
                report,
                "CheatManager was not enabled",
                "CheatManager inactive",
                "The log does not show that the CheatManager was created. Summon commands may load assets but spawn nothing.",
                "Load into a world, then check if CheatManagerEnablerMod logs 'Enabled CheatManager'. If not, replace CheatManagerEnablerMod with the fixed fallback version.",
                logPath,
                true);
        }
    }

    private static void CheckLogContains(
        DiagnosticReport report,
        string log,
        string expected,
        string title,
        string hint,
        string details,
        string fix,
        string logPath)
    {
        if (log.Contains(expected, StringComparison.OrdinalIgnoreCase))
            return;

        AddIssue(report, title, hint, details, fix, logPath, true);
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
        if (File.Exists(path))
            return;

        AddIssue(report, title, hint, details, fix, path, isError);
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
        if (Directory.Exists(path))
            return;

        AddIssue(report, title, hint, details, fix, path, isError);
    }

    private static void AddIssue(
        DiagnosticReport report,
        string title,
        string hint,
        string details,
        string fix,
        string technicalDetails,
        bool isError)
    {
        report.Issues.Add(new DiagnosticIssue
        {
            Title = title,
            Hint = hint,
            Details = details,
            Fix = fix,
            TechnicalDetails = technicalDetails,
            IsError = isError
        });
    }
}