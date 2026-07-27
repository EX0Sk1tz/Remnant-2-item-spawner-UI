namespace Remnant2UnlockerApp.Models;

public sealed class InventoryItemEntry
{
    private static readonly string[] CategoryPrefixes = { "Material_", "Consumable_", "Item_" };

    public string Name { get; set; } = "";

    public int Quantity { get; set; }

    public string DisplayName
    {
        get
        {
            var trimmed = Name;

            foreach (var prefix in CategoryPrefixes)
            {
                if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    trimmed = trimmed[prefix.Length..];
                    break;
                }
            }

            if (trimmed.EndsWith("_C", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed[..^2];

            return $"{trimmed.Replace('_', ' ')} (x{Quantity})";
        }
    }
}
