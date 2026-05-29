using System.IO;
using System.Text.Json;

namespace Remnant2UnlockerApp.Services;

public sealed class GamePathService
{
    private readonly string _settingsDir;
    private readonly string _settingsPath;

    private const string SteamExeName = "Remnant2-Win64-Shipping.exe";
    private const string GamePassExeName = "Remnant2-WinGDK-Shipping.exe";

    public GamePathService()
    {
        _settingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Remnant2UnlockerApp");

        _settingsPath = Path.Combine(_settingsDir, "settings.json");

        Directory.CreateDirectory(_settingsDir);

        Settings = LoadSettings();
    }

    public UserSettings Settings { get; private set; }

    public string GameRootPath => Settings.Win64Path ?? "";

    public string Win64Path => GameRootPath;

    public bool IsConfigured => IsValidGamePath(GameRootPath);

    public bool IsSteamInstall => IsSteamPath(GameRootPath);

    public bool IsGamePassInstall => IsGamePassPath(GameRootPath);

    public string PlatformName
    {
        get
        {
            if (IsSteamInstall)
                return "Steam/Epic";

            if (IsGamePassInstall)
                return "Game Pass";

            return "Unknown";
        }
    }

    public string ExpectedFolderHint =>
        "Steam/Epic: Remnant2\\Remnant2\\Binaries\\Win64\n" +
        "Game Pass: Remnant 2\\Content\\Remnant2\\Binaries\\WinGDK";

    public string GetGameExePath()
    {
        var steamExe = Path.Combine(GameRootPath, SteamExeName);
        if (File.Exists(steamExe))
            return steamExe;

        var gamePassExe = Path.Combine(GameRootPath, GamePassExeName);
        if (File.Exists(gamePassExe))
            return gamePassExe;

        return "";
    }

    public string GetModRootPath()
    {
        return Path.Combine(GameRootPath, "Mods", "Remnant2Unlocker");
    }

    public string GetItemsPath()
    {
        return Path.Combine(GetModRootPath(), "items.json");
    }

    public string GetQueuePath()
    {
        return Path.Combine(GetModRootPath(), "command_queue.json");
    }

    public string GetStatusPath()
    {
        return Path.Combine(GetModRootPath(), "status.json");
    }

    public string GetScriptsPath()
    {
        return Path.Combine(GetModRootPath(), "scripts");
    }

    public void SetWin64Path(string path)
    {
        Settings.Win64Path = path;
        SaveSettings();
    }

    public static bool IsValidWin64Path(string? path)
    {
        return IsValidGamePath(path);
    }

    public static bool IsValidGamePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var hasExe = HasSupportedGameExe(path);
        var mods = Path.Combine(path, "Mods");
        var modRoot = Path.Combine(mods, "Remnant2Unlocker");
        var items = Path.Combine(modRoot, "items.json");
        var scripts = Path.Combine(modRoot, "scripts");

        return hasExe
            && Directory.Exists(mods)
            && Directory.Exists(modRoot)
            && File.Exists(items)
            && Directory.Exists(scripts);
    }

    public static bool HasSupportedGameExe(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return File.Exists(Path.Combine(path, SteamExeName))
            || File.Exists(Path.Combine(path, GamePassExeName));
    }

    public static bool IsSteamPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return File.Exists(Path.Combine(path, SteamExeName));
    }

    public static bool IsGamePassPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return File.Exists(Path.Combine(path, GamePassExeName));
    }

    private UserSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return new UserSettings();

            var json = File.ReadAllText(_settingsPath);

            return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
        }
        catch
        {
            return new UserSettings();
        }
    }

    private void SaveSettings()
    {
        var json = JsonSerializer.Serialize(
            Settings,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        File.WriteAllText(_settingsPath, json);
    }
}

public sealed class UserSettings
{
    public string? Win64Path { get; set; }
}