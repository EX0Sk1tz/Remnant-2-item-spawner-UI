using System.IO;
using System.Text.Json;
using Remnant2UnlockerApp.Models;
using Remnant2UnlockerApp.Services;
using Xunit;

namespace Remnant2UnlockerApp.Tests;

public class DlcCatalogTests
{
    [Theory]
    [InlineData("/Game/World_DLC1/Items/Weapons/Monarch/Weapon_Monarch.Weapon_Monarch_C", "DLC1", "The Awakened King")]
    [InlineData("/Game/World_DLC2/Items/Weapons/Polygun/Weapon_Polygun.Weapon_Polygun_C", "DLC2", "The Forgotten Kingdom")]
    [InlineData("/Game/World_DLC3/Items/Weapons/Redeemer/Weapon_Redeemer.Weapon_Redeemer_C", "DLC3", "The Dark Horizon")]
    [InlineData("/game/world_dlc2/items/x.x_c", "DLC2", "The Forgotten Kingdom")]
    [InlineData("  /Game/World_DLC1/Items/X.X_C 1 1 31", "DLC1", "The Awakened King")]
    public void FromPath_RecognisesEachDlcFolder(string path, string key, string name)
    {
        var dlc = DlcCatalog.FromPath(path);

        Assert.NotNull(dlc);
        Assert.Equal(key, dlc!.Key);
        Assert.Equal(name, dlc.Name);
    }

    [Theory]
    [InlineData("/Game/World_Base/Items/Weapons/AlphaOmega/Weapon_AlphaOmega.Weapon_AlphaOmega_C")]
    [InlineData("/Game/World_Fae/Items/Trinkets/Amulets/NimuesRibbon/Amulet_NimuesRibbon.Amulet_NimuesRibbon_C")]
    [InlineData("/Game/Events/Items/X.X_C")]
    [InlineData("/Game/World_Base/Items/World_DLC1/X.X_C")]
    [InlineData("")]
    [InlineData(null)]
    public void FromPath_EverythingElseIsBaseGame(string? path)
    {
        Assert.Null(DlcCatalog.FromPath(path));
    }

    [Fact]
    public void FromPath_UnknownFutureDlc_IsStillDlc()
    {
        var dlc = DlcCatalog.FromPath("/Game/World_DLC4/Items/X.X_C");

        Assert.NotNull(dlc);
        Assert.Equal("DLC4", dlc!.Key);
        Assert.Equal("DLC 4", dlc.Name);
    }

    [Theory]
    [InlineData(DlcCatalog.AllContent, true, true)]
    [InlineData(null, true, true)]
    [InlineData(DlcCatalog.BaseGame, true, false)]
    [InlineData("DLC2", false, true)]
    [InlineData("DLC1", false, false)]
    public void Matches_FiltersBaseGameAndEachDlc(string? filter, bool baseMatches, bool dlc2Matches)
    {
        var baseItem = new RemnantItem { Path = "/Game/World_Base/Items/X.X_C" };
        var dlc2Item = new RemnantItem { Path = "/Game/World_DLC2/Items/Y.Y_C" };

        Assert.Equal(baseMatches, DlcCatalog.Matches(baseItem, filter));
        Assert.Equal(dlc2Matches, DlcCatalog.Matches(dlc2Item, filter));
    }

    [Fact]
    public void Item_ExposesBadgeTextAndTooltip()
    {
        var dlcItem = new RemnantItem { Path = "/Game/World_DLC3/Items/X.X_C" };
        var baseItem = new RemnantItem { Path = "/Game/World_Base/Items/X.X_C" };

        Assert.True(dlcItem.IsDlc);
        Assert.Equal("Dark Horizon", dlcItem.DlcBadgeText);
        Assert.Equal("DLC: The Dark Horizon", dlcItem.DlcTooltip);

        Assert.False(baseItem.IsDlc);
        Assert.Equal("", baseItem.DlcBadgeText);
    }

    [Theory]
    [InlineData(DlcCatalog.AllContent, "All content")]
    [InlineData(DlcCatalog.BaseGame, "Base game")]
    [InlineData("DLC1", "The Awakened King")]
    public void DisplayName_UsesTheGivenLabelsAndDlcNames(string key, string expected)
    {
        Assert.Equal(expected, DlcCatalog.DisplayName(key, "All content", "Base game"));
    }

    // Guards catalog updates: a new DLC folder shows up here until it gets a proper name.
    [Fact]
    public void ShippedCatalog_EveryDlcItemBelongsToAKnownDlc_AndEachDlcHasItems()
    {
        var catalog = JsonSerializer.Deserialize<List<RemnantItem>>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "items.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var dlcItems = catalog.Where(x => x.IsDlc).ToList();

        Assert.All(dlcItems, x => Assert.Contains(DlcCatalog.Known, known => known.Key == x.Dlc!.Key));

        foreach (var known in DlcCatalog.Known)
            Assert.Contains(dlcItems, x => x.Dlc!.Key == known.Key);

        // Base game + every DLC together is the whole catalog.
        var perFilter = new[] { DlcCatalog.BaseGame }
            .Concat(DlcCatalog.Known.Select(x => x.Key))
            .Sum(filter => catalog.Count(x => DlcCatalog.Matches(x, filter)));

        Assert.Equal(catalog.Count, perFilter);
    }
}
