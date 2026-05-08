using System.IO;
using System.Text.Json;
using Remnant2UnlockerApp.Models;

namespace Remnant2UnlockerApp.Services;

public sealed class HotkeySettingsService
{
    private readonly GamePathService _gamePathService;

    public HotkeySettingsService(GamePathService gamePathService)
    {
        _gamePathService = gamePathService;
    }

    public string GetHotkeysPath()
    {
        return Path.Combine(_gamePathService.GetModRootPath(), "hotkeys.json");
    }

    public HotkeySettings Load()
    {
        try
        {
            var path = GetHotkeysPath();

            if (!File.Exists(path))
                return new HotkeySettings();

            var json = File.ReadAllText(path);

            return JsonSerializer.Deserialize<HotkeySettings>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new HotkeySettings();
        }
        catch
        {
            return new HotkeySettings();
        }
    }

    public void Save(HotkeySettings settings)
    {
        var path = GetHotkeysPath();
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(
            new
            {
                alwaysOnTop = settings.AlwaysOnTop,
                consoleKey = settings.ConsoleKey,
                teleport = settings.Teleport
            },
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        File.WriteAllText(path, json);
    }
}