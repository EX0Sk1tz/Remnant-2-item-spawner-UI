using System.IO;
using System.Text.Json;
using Remnant2UnlockerApp.Models;

namespace Remnant2UnlockerApp.Services;

public sealed class InventoryItemsService
{
    private readonly GamePathService _gamePathService;

    public InventoryItemsService(GamePathService gamePathService)
    {
        _gamePathService = gamePathService;
    }

    public List<InventoryItemEntry> Load()
    {
        try
        {
            var path = _gamePathService.GetInventoryItemsPath();

            if (!File.Exists(path))
                return new List<InventoryItemEntry>();

            var json = File.ReadAllText(path);

            return JsonSerializer.Deserialize<List<InventoryItemEntry>>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<InventoryItemEntry>();
        }
        catch (Exception ex)
        {
            AppLogService.Error($"Failed to load inventory_items.json from {_gamePathService.GetInventoryItemsPath()}", ex);
            return new List<InventoryItemEntry>();
        }
    }
}
