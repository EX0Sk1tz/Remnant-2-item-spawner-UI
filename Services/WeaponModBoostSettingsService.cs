using System.IO;
using System.Text.Json;

namespace Remnant2UnlockerApp.Services;

public sealed class WeaponModBoostSettingsService
{
    private readonly GamePathService _gamePathService;

    public WeaponModBoostSettingsService(GamePathService gamePathService)
    {
        _gamePathService = gamePathService;
    }

    private string GetPath()
    {
        return Path.Combine(_gamePathService.GetModRootPath(), "weapon_mod_boosts.json");
    }

    public Dictionary<string, Dictionary<string, double>> Load()
    {
        try
        {
            var path = GetPath();

            if (!File.Exists(path))
                return new Dictionary<string, Dictionary<string, double>>();

            var json = File.ReadAllText(path);

            return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, double>>>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new Dictionary<string, Dictionary<string, double>>();
        }
        catch (Exception ex)
        {
            AppLogService.Error($"Failed to load weapon_mod_boosts.json from {GetPath()}", ex);
            return new Dictionary<string, Dictionary<string, double>>();
        }
    }

    public void Save(Dictionary<string, Dictionary<string, double>> settings)
    {
        var path = GetPath();
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(path, json);
    }
}
