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

    // enemyOutline is the one value the game also writes (its in-game hotkey, enemy_outline.lua).
    // Returns null when the file is missing, has no such value, or was caught mid-write, so a
    // half-written file never reads as "off" the way Load()'s defaults would.
    public bool? TryReadEnemyOutline()
    {
        try
        {
            var path = GetCheatsPath();

            if (!File.Exists(path))
                return null;

            using var document = JsonDocument.Parse(File.ReadAllText(path));

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!property.Name.Equals("enemyOutline", StringComparison.OrdinalIgnoreCase))
                    continue;

                return property.Value.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => null
                };
            }

            return null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
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
                infiniteStamina = settings.InfiniteStamina,
                infiniteAmmo = settings.InfiniteAmmo,
                noFallDamage = settings.NoFallDamage,
                aimMagicBulletsEnabled = settings.AimMagicBulletsEnabled,
                aimFov = settings.AimFov,
                lootFullDropChance = settings.LootFullDropChance,
                noRecoil = settings.NoRecoil,
                noSpread = settings.NoSpread,
                enemyOutline = settings.EnemyOutline
            },
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        File.WriteAllText(path, json);
    }
}