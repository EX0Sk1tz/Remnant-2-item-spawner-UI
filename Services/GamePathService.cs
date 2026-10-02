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
    /// The tagged UE4SS releases put it at "&lt;root&gt;\Mods". The newer experimental builds (they report
    /// "v3.0.1 Beta", load via a dwmapi.dll proxy, and ship in the Allow Asset Mods package) nest
    /// everything under "&lt;root&gt;\ue4ss\Mods" instead - on Steam and Epic as well as Game Pass.
    /// A UE4SS.dll that only exists in the nested folder decides it, even when a stray "&lt;root&gt;\Mods"
    /// is left over from an older install: UE4SS never loads mods from there. Otherwise prefer
    /// whichever folder exists; fall back to "&lt;root&gt;\Mods" when neither does (before UE4SS is installed).
    /// </summary>
    public static string ResolveModsFolder(string? path)
    {
        var standard = Path.Combine(path ?? "", "Mods");

        if (string.IsNullOrWhiteSpace(path))
            return standard;

        var nested = Path.Combine(path, "ue4ss", "mods");

        if (File.Exists(Path.Combine(path, "ue4ss", "UE4SS.dll")) && !File.Exists(Path.Combine(path, "UE4SS.dll")))
            return nested;

        if (Directory.Exists(standard))
            return standard;

        return Directory.Exists(nested) ? nested : standard;
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

    public void SaveSettings()
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

    /// <summary>The first-start "Install Remnant 2 Unlocker" dialog was answered (installed or "Run without installing").</summary>
    public bool InstallPromptHandled { get; set; }

    /// <summary>Where the app installed itself, if it did.</summary>
    public string? InstallDir { get; set; }

    /// <summary>Settings → General: apply the saved settings profile at every app start.</summary>
    public bool LoadProfileOnStartup { get; set; }
}