using System.Text.Json.Serialization;

namespace Remnant2UnlockerApp.Models;

public sealed class BridgeStatus
{
    [JsonPropertyName("ready")]
    public bool Ready { get; set; }

    [JsonPropertyName("busy")]
    public bool Busy { get; set; }

    [JsonPropertyName("lastCommandId")]
    public long LastCommandId { get; set; }

    [JsonPropertyName("lastAction")]
    public string LastAction { get; set; } = "";

    [JsonPropertyName("lastMessage")]
    public string LastMessage { get; set; } = "";

    [JsonPropertyName("lastSpawned")]
    public string? LastSpawned { get; set; }

    [JsonPropertyName("processedCount")]
    public int ProcessedCount { get; set; }

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    public double Progress
    {
        get
        {
            if (TotalCount <= 0)
                return 0;

            return Math.Clamp((double)ProcessedCount / TotalCount * 100.0, 0, 100);
        }
    }

    public string ProgressText
    {
        get
        {
            if (TotalCount <= 0)
                return "Waiting...";

            return $"{ProcessedCount} / {TotalCount}";
        }
    }
}