using System.IO;
using System.Text.Json;
using Remnant2UnlockerApp.Models;

namespace Remnant2UnlockerApp.Services;

public sealed class CheatSettingsService
{
    private readonly GamePathService _gamePathService;

    public CheatSettingsService(GamePathService gamePathService)
    {
        _gamePathService = gamePathService;
    }

    private string GetCheatsPath()
    {
        return Path.Combine(_gamePathService.GetModRootPath(), "cheats.json");
    }

    public CheatSettings Load()
    {
        try
        {
            var path = GetCheatsPath();

            if (!File.Exists(path))
                return new CheatSettings();

            var json = File.ReadAllText(path);

            return JsonSerializer.Deserialize<CheatSettings>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new CheatSettings();
        }
        catch (Exception ex)
        {
            AppLogService.Error($"Failed to load cheats.json from {GetCheatsPath()}", ex);
            return new CheatSettings();
        }
    }

    public void Save(CheatSettings settings)
    {
        var path = GetCheatsPath();
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(
            new
            {
                infiniteHealth = settings.InfiniteHealth,
                infiniteStamina = settings.InfiniteStamina
            },
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        File.WriteAllText(path, json);
    }
}