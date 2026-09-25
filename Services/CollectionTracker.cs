using Remnant2UnlockerApp.Models;

namespace Remnant2UnlockerApp.Services;

// Pure matching logic behind the "what am I still missing" view: no I/O, no WPF, so it can be
// unit-tested directly. Items are matched on their blueprint class name ("Weapon_AlphaOmega_C"),
// which is what inventory_cheats.lua writes and what every items.json path ends with.
public static class CollectionTracker
{
    // Only kinds of items that stay in the inventory once found. Consumables, materials and trait
    // points get used up, and traits/archetypes aren't stored in the inventory at all, so counting
    // them would fill the "missing" list with things the player can never keep.
    public static readonly IReadOnlySet<string> TrackedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Bow", "Handgun", "Long Gun", "Melee",
        "Body", "Gloves", "Head", "Legs",
        "Amulet", "Ring",
        "Mutator", "Relic"
    };

    public static bool IsTrackedType(string? type) =>
        !string.IsNullOrWhiteSpace(type) && TrackedTypes.Contains(type.Trim());

    // Accepts every shape a class reference shows up in and reduces it to the lowercase class name:
    //   "/Game/.../Weapon_AlphaOmega.Weapon_AlphaOmega_C"          (items.json path)
    //   "/Game/.../RelicFragment_X.RelicFragment_X_C 1 1 31"        (items.json path with spawn args)
    //   "BlueprintGeneratedClass /Game/.../Weapon_X.Weapon_X_C"    (UE GetFullName)
    //   "Weapon_AlphaOmega_C"                                       (UE GetFName, what the mod writes)
    public static string ToClassKey(string? pathOrName)
    {
        if (string.IsNullOrWhiteSpace(pathOrName))
            return "";

        var tokens = pathOrName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var token = tokens.FirstOrDefault(t => t.Contains('/') || t.Contains('.')) ?? tokens[0];

        token = token.Trim('"', '\'', '|');

        var cut = Math.Max(token.LastIndexOf('.'), token.LastIndexOf('/'));

        if (cut >= 0)
            token = token[(cut + 1)..];

        return token.ToLowerInvariant();
    }

    public static HashSet<string> ToClassKeys(IEnumerable<string?> pathsOrNames)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var value in pathsOrNames)
        {
            var key = ToClassKey(value);

            if (key.Length > 0)
                keys.Add(key);
        }

        return keys;
    }

    // Counts per item Type. Only tracked types appear in the result.
    public static Dictionary<string, CollectionProgress> ComputeProgress(
        IEnumerable<RemnantItem> items,
        IReadOnlySet<string> ownedKeys)
    {
        var result = new Dictionary<string, CollectionProgress>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            if (!item.IsCollectible)
                continue;

            result.TryGetValue(item.Type, out var progress);

            result[item.Type] = new CollectionProgress(
                progress.Owned + (ownedKeys.Contains(item.ClassKey) ? 1 : 0),
                progress.Total + 1);
        }

        return result;
    }

    public static CollectionProgress Sum(IEnumerable<CollectionProgress> parts)
    {
        var owned = 0;
        var total = 0;

        foreach (var part in parts)
        {
            owned += part.Owned;
            total += part.Total;
        }

        return new CollectionProgress(owned, total);
    }

    public static bool IsMissing(RemnantItem item, IReadOnlySet<string> ownedKeys) =>
        item.IsCollectible
        && !string.IsNullOrWhiteSpace(item.Path)
        && !ownedKeys.Contains(item.ClassKey);
}

public readonly record struct CollectionProgress(int Owned, int Total)
{
    public int Missing => Total - Owned;

    public bool IsComplete => Total > 0 && Owned >= Total;

    public override string ToString() => $"{Owned} / {Total}";
}
