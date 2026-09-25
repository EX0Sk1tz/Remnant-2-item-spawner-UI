using System.IO;
using System.Text.Json;
using Remnant2UnlockerApp.Models;

namespace Remnant2UnlockerApp.Services;

// Reads owned_items.json, which inventory_cheats.lua rewrites every 5s while a character is loaded.
// The file is written in place (no atomic rename on the Lua side), so a read can land mid-write:
// anything that doesn't parse is treated as "no new data" and the last good snapshot is kept.
public sealed class OwnedItemsService
{
    private readonly GamePathService _gamePathService;
    private DateTime _lastWriteTimeUtc = DateTime.MinValue;
    private string _lastPath = "";

    public OwnedItemsService(GamePathService gamePathService)
    {
        _gamePathService = gamePathService;
    }

    public OwnedItemsSnapshot? Current { get; private set; }

    // Re-reads the file only when its write time changed. Returns true when Current was replaced.
    public bool Refresh()
    {
        var path = _gamePathService.GetOwnedItemsPath();

        if (!string.Equals(path, _lastPath, StringComparison.OrdinalIgnoreCase))
        {
            // Different install (game path changed): the old snapshot belongs to another game folder.
            _lastPath = path;
            _lastWriteTimeUtc = DateTime.MinValue;

            if (Current != null)
            {
                Current = null;
                return true;
            }
        }

        try
        {
            if (!File.Exists(path))
                return false;

            var writeTime = File.GetLastWriteTimeUtc(path);

            if (writeTime == _lastWriteTimeUtc)
                return false;

            var snapshot = Parse(File.ReadAllText(path), new DateTimeOffset(writeTime, TimeSpan.Zero));

            if (snapshot == null)
                return false; // Probably caught mid-write; the next poll retries since the time wasn't recorded.

            _lastWriteTimeUtc = writeTime;
            Current = snapshot;
            return true;
        }
        catch (IOException)
        {
            // The mod has the file open for writing right now. Try again on the next poll.
            return false;
        }
        catch (Exception ex)
        {
            AppLogService.Warn($"Failed to read owned_items.json from {path}", ex);
            return false;
        }
    }

    // Expected shape: { "scannedAt": 1727000000, "items": ["Weapon_AlphaOmega_C", ...] }.
    // Returns null for anything else. fallbackScannedAt is used when scannedAt is missing.
    public static OwnedItemsSnapshot? Parse(string? json, DateTimeOffset fallbackScannedAt)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return null;

            if (!TryGetProperty(root, "items", out var itemsElement))
                return null;

            var names = new List<string?>();

            // The mod's json.lua can't tell an empty array from an empty object, so accept {} as "no items".
            if (itemsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in itemsElement.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.String)
                        names.Add(element.GetString());
                }
            }
            else if (itemsElement.ValueKind != JsonValueKind.Object || itemsElement.EnumerateObject().Any())
            {
                return null;
            }

            var scannedAt = fallbackScannedAt;

            // Lua numbers may carry a fraction (some os.time() implementations return one).
            if (TryGetProperty(root, "scannedAt", out var scannedAtElement)
                && scannedAtElement.ValueKind == JsonValueKind.Number
                && scannedAtElement.TryGetDouble(out var unixSeconds)
                && unixSeconds > 0
                && unixSeconds < DateTimeOffset.MaxValue.ToUnixTimeSeconds())
            {
                scannedAt = DateTimeOffset.FromUnixTimeSeconds((long)Math.Floor(unixSeconds));
            }

            return new OwnedItemsSnapshot(CollectionTracker.ToClassKeys(names), scannedAt);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
