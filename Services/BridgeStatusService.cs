using System.IO;
using System.Text.Json;
using Remnant2UnlockerApp.Models;

namespace Remnant2UnlockerApp.Services;

public sealed class BridgeStatusService
{
    private readonly GamePathService _pathService;

    public BridgeStatusService(GamePathService pathService)
    {
        _pathService = pathService;
    }

    public BridgeStatus Read()
    {
        try
        {
            var path = _pathService.GetStatusPath();

            if (!File.Exists(path))
            {
                return new BridgeStatus
                {
                    LastMessage = "status.json not found"
                };
            }

            var json = File.ReadAllText(path);

            return JsonSerializer.Deserialize<BridgeStatus>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new BridgeStatus
                {
                    LastMessage = "Could not read status"
                };
        }
        catch (Exception ex)
        {
            return new BridgeStatus
            {
                LastMessage = "Could not read status",
                Error = ex.Message
            };
        }
    }
}