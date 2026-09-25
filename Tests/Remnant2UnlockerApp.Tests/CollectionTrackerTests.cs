using System.IO;
using System.Text.Json;
using Remnant2UnlockerApp.Models;
using Remnant2UnlockerApp.Services;
using Xunit;

namespace Remnant2UnlockerApp.Tests;

public class CollectionTrackerTests
{
    private const string AlphaOmegaPath = "/Game/World_Base/Items/Weapons/Longguns/Special/AlphaOmega/Weapon_AlphaOmega.Weapon_AlphaOmega_C";

    [Theory]
    [InlineData(AlphaOmegaPath, "weapon_alphaomega_c")]
    [InlineData("Weapon_AlphaOmega_C", "weapon_alphaomega_c")]
    [InlineData("  Weapon_AlphaOmega_C  ", "weapon_alphaomega_c")]
    [InlineData("BlueprintGeneratedClass " + AlphaOmegaPath, "weapon_alphaomega_c")]
    [InlineData("/Game/World_Base/Items/Gems/_Core/Shared/RelicFragment_CriticalDamage.RelicFragment_CriticalDamage_C 1 1 31", "relicfragment_criticaldamage_c")]
    [InlineData("\"" + AlphaOmegaPath + "\"", "weapon_alphaomega_c")]
    [InlineData("/Game/Items/NoDotHere/Weapon_Foo", "weapon_foo")]
    public void ToClassKey_ReducesEveryShapeToTheLowercaseClassName(string input, string expected)
    {
        Assert.Equal(expected, CollectionTracker.ToClassKey(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToClassKey_EmptyInput_ReturnsEmpty(string? input)
    {
        Assert.Equal("", CollectionTracker.ToClassKey(input));
    }

    [Fact]
    public void ToClassKeys_DropsBlanksAndDuplicates()
    {
        var keys = CollectionTracker.ToClassKeys(new[] { "Ring_A_C", "RING_A_C", "", null, "Amulet_B_C" });

        Assert.Equal(new[] { "amulet_b_c", "ring_a_c" }, keys.OrderBy(x => x));
    }

    [Theory]
    [InlineData("Long Gun", true)]
    [InlineData("long gun", true)]
    [InlineData("Ring", true)]
    [InlineData("Mutator", true)]
    [InlineData("Relic", true)]
    [InlineData("Consumable", false)]
    [InlineData("Crafting Material", false)]
    [InlineData("Trait", false)]
    [InlineData("Trait Point", false)]
    [InlineData("Prism Fragment", false)]
    [InlineData("All", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsTrackedType_OnlyKeepsItemsThatStayInTheInventory(string? type, bool expected)
    {
        Assert.Equal(expected, CollectionTracker.IsTrackedType(type));
    }

    [Fact]
    public void ComputeProgress_CountsOwnedAndTotalPerTrackedType()
    {
        var items = new[]
        {
            Item("Alpha", "Long Gun", AlphaOmegaPath),
            Item("Beta", "Long Gun", "/Game/X/Weapon_Beta.Weapon_Beta_C"),
            Item("Ring A", "Ring", "/Game/X/Ring_A.Ring_A_C"),
            Item("Potion", "Consumable", "/Game/X/Consumable_P.Consumable_P_C"),
        };

        var owned = CollectionTracker.ToClassKeys(new[] { "Weapon_AlphaOmega_C", "Consumable_P_C" });

        var progress = CollectionTracker.ComputeProgress(items, owned);

        Assert.Equal(new CollectionProgress(1, 2), progress["Long Gun"]);
        Assert.Equal(new CollectionProgress(0, 1), progress["ring"]);
        Assert.False(progress.ContainsKey("Consumable"));
        Assert.Equal(new CollectionProgress(1, 3), CollectionTracker.Sum(progress.Values));
    }

    [Fact]
    public void IsMissing_OnlyForTrackedItemsNotInTheScan()
    {
        var owned = CollectionTracker.ToClassKeys(new[] { "Weapon_AlphaOmega_C" });

        Assert.False(CollectionTracker.IsMissing(Item("Alpha", "Long Gun", AlphaOmegaPath), owned));
        Assert.True(CollectionTracker.IsMissing(Item("Beta", "Long Gun", "/Game/X/Weapon_Beta.Weapon_Beta_C"), owned));
        Assert.False(CollectionTracker.IsMissing(Item("Potion", "Consumable", "/Game/X/Consumable_P.Consumable_P_C"), owned));
        Assert.False(CollectionTracker.IsMissing(Item("No path", "Long Gun", ""), owned));
    }

    [Fact]
    public void CollectionProgress_ReportsMissingAndCompletion()
    {
        Assert.Equal(3, new CollectionProgress(2, 5).Missing);
        Assert.True(new CollectionProgress(5, 5).IsComplete);
        Assert.False(new CollectionProgress(0, 0).IsComplete);
        Assert.Equal("2 / 5", new CollectionProgress(2, 5).ToString());
    }

    // Guards future catalog updates: if a tracked items.json entry stops matching the class name the
    // mod reports (GetFName = the part after the last '.'), that item would show as missing forever.
    [Fact]
    public void ShippedCatalog_EveryTrackedItemHasAUniqueClassKeyMatchingItsFName()
    {
        var catalog = LoadCatalog().Where(x => x.IsCollectible).ToList();

        Assert.NotEmpty(catalog);

        foreach (var item in catalog)
        {
            var firstToken = item.Path.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            var fName = firstToken[(firstToken.LastIndexOf('.') + 1)..];

            Assert.EndsWith("_c", item.ClassKey);
            Assert.Equal(CollectionTracker.ToClassKey(fName), item.ClassKey);
        }

        var duplicates = catalog
            .GroupBy(x => x.ClassKey)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(x => x.Name))}")
            .ToList();

        Assert.Empty(duplicates);
    }

    private static RemnantItem Item(string name, string type, string path) => new()
    {
        Name = name,
        Type = type,
        Path = path
    };

    private static List<RemnantItem> LoadCatalog()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "items.json");

        return JsonSerializer.Deserialize<List<RemnantItem>>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<RemnantItem>();
    }
}
