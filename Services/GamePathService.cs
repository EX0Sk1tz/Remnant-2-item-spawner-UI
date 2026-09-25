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

    public string GetModsFolderPath()
    {
        return ResolveModsFolder(GameRootPath);
    }

    public string GetModRootPath()
    {
        return Path.Combine(GetModsFolderPath(), "Remnant2Unlocker");
    }

    /// <summary>
    /// Finds the folder that directly contains the UE4SS mods (Remnant2Unlocker, AllowModsMod, etc).
    /// Steam/Epic UE4SS installs put it at "&lt;root&gt;\Mods". Some Game Pass / Windows Store setups
    /// use an experimental UE4SS build (loaded via a dwmapi.dll proxy) that nests everything under
    /// "&lt;root&gt;\ue4ss\mods" instead. Prefer whichever actually exists on disk; fall back to the
    /// Steam/Epic layout when neither is present yet (e.g. before UE4SS is installed).
    /// </summary>
    public static string ResolveModsFolder(string? path)
    {
        var standard = Path.Combine(path ?? "", "Mods");

        if (string.IsNullOrWhiteSpace(path) || Directory.Exists(standard))
            return standard;

        var gamePassNested = Path.Combine(path, "ue4ss", "mods");

        return Directory.Exists(gamePassNested) ? gamePassNested : standard;
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

    public string GetInventoryItemsPath()
    {
        return Path.Combine(GetModRootPath(), "inventory_items.json");
    }

    public string GetOwnedItemsPath()
    {
        return Path.Combine(GetModRootPath(), "owned_items.json");
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

    public static bool IsValidGamePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var hasExe = HasSupportedGameExe(path);
        var mods = ResolveModsFolder(path);
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
        catch (Exception ex)
        {
            AppLogService.Error($"Failed to load settings.json from {_settingsPath}", ex);
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