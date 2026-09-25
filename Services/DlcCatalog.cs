using System.Text.RegularExpressions;
using Remnant2UnlockerApp.Models;

namespace Remnant2UnlockerApp.Services;

public sealed record DlcInfo(string Key, string Name, string ShortName);

// Which DLC an item belongs to, read from its asset path: the game keeps each DLC's content in its
// own top-level folder ("/Game/World_DLC2/..."). Everything else -- World_Base, the per-world
// folders, Events -- is base game. The per-world folders are not used for a world filter: 44% of
// items live in the shared World_Base folder whatever world they're found in.
public static class DlcCatalog
{
    // Filter keys besides the DLC keys below.
    public const string AllContent = "All";
    public const string BaseGame = "Base";

    public static readonly IReadOnlyList<DlcInfo> Known = new[]
    {
        new DlcInfo("DLC1", "The Awakened King", "Awakened King"),
        new DlcInfo("DLC2", "The Forgotten Kingdom", "Forgotten Kingdom"),
        new DlcInfo("DLC3", "The Dark Horizon", "Dark Horizon"),
    };

    private static readonly Regex DlcFolder = new(
        @"^/Game/World_DLC(\d+)/",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // A future DLC folder we don't have a name for yet still counts as DLC (never as base game).
    public static DlcInfo? FromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var match = DlcFolder.Match(path.Trim());

        if (!match.Success)
            return null;

        var key = "DLC" + int.Parse(match.Groups[1].Value);

        return Known.FirstOrDefault(x => x.Key == key)
            ?? new DlcInfo(key, $"DLC {key[3..]}", $"DLC {key[3..]}");
    }

    public static bool Matches(RemnantItem item, string? filterKey)
    {
        if (string.IsNullOrEmpty(filterKey) || filterKey == AllContent)
            return true;

        var dlc = item.Dlc;

        if (filterKey == BaseGame)
            return dlc == null;

        return dlc != null && string.Equals(dlc.Key, filterKey, StringComparison.OrdinalIgnoreCase);
    }

    public static string DisplayName(string? filterKey, string allContentLabel, string baseGameLabel) =>
        filterKey switch
        {
            null or "" or AllContent => allContentLabel,
            BaseGame => baseGameLabel,
            _ => Known.FirstOrDefault(x => x.Key == filterKey)?.Name ?? filterKey
        };
}
