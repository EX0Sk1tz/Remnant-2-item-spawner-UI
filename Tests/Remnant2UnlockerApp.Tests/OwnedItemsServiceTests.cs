using System.IO;
using Remnant2UnlockerApp.Models;
using Remnant2UnlockerApp.Services;
using Xunit;

namespace Remnant2UnlockerApp.Tests;

public class OwnedItemsServiceTests
{
    private static readonly DateTimeOffset Fallback = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private const string ValidJson = """{"scannedAt":1758800000,"items":["Weapon_AlphaOmega_C","Ring_A_C","Ring_A_C"]}""";

    [Fact]
    public void Parse_ValidSnapshot_NormalizesAndDeduplicatesNames()
    {
        var snapshot = OwnedItemsService.Parse(ValidJson, Fallback);

        Assert.NotNull(snapshot);
        Assert.Equal(new[] { "ring_a_c", "weapon_alphaomega_c" }, snapshot!.ClassKeys.OrderBy(x => x));
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1758800000), snapshot.ScannedAt);
    }

    [Fact]
    public void Parse_KeyOrderAndCaseDoNotMatter()
    {
        // json.lua writes object keys in pairs() order, which isn't stable.
        var snapshot = OwnedItemsService.Parse("""{"Items":["Ring_A_C"],"ScannedAt":1758800000}""", Fallback);

        Assert.NotNull(snapshot);
        Assert.Contains("ring_a_c", snapshot!.ClassKeys);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1758800000), snapshot.ScannedAt);
    }

    [Theory]
    [InlineData("""{"scannedAt":1758800000,"items":[]}""")]
    [InlineData("""{"scannedAt":1758800000,"items":{}}""")]
    public void Parse_EmptyInventory_IsAValidEmptySnapshot(string json)
    {
        // An empty inventory is real data (everything missing), unlike an unreadable file.
        var snapshot = OwnedItemsService.Parse(json, Fallback);

        Assert.NotNull(snapshot);
        Assert.Empty(snapshot!.ClassKeys);
    }

    [Fact]
    public void Parse_FractionalScanTime_IsFloored()
    {
        var snapshot = OwnedItemsService.Parse("""{"scannedAt":1758800000.75,"items":[]}""", Fallback);

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1758800000), snapshot!.ScannedAt);
    }

    [Theory]
    [InlineData("""{"items":["Ring_A_C"]}""")]
    [InlineData("""{"scannedAt":1e300,"items":["Ring_A_C"]}""")]
    [InlineData("""{"scannedAt":0,"items":["Ring_A_C"]}""")]
    [InlineData("""{"scannedAt":-5,"items":["Ring_A_C"]}""")]
    [InlineData("""{"scannedAt":"soon","items":["Ring_A_C"]}""")]
    public void Parse_MissingOrInvalidScanTime_FallsBackToFileTime(string json)
    {
        var snapshot = OwnedItemsService.Parse(json, Fallback);

        Assert.NotNull(snapshot);
        Assert.Equal(Fallback, snapshot!.ScannedAt);
    }

    [Fact]
    public void Parse_SkipsNonStringEntries()
    {
        var snapshot = OwnedItemsService.Parse("""{"items":["Ring_A_C",42,null,{"x":1},""]}""", Fallback);

        Assert.NotNull(snapshot);
        Assert.Equal(new[] { "ring_a_c" }, snapshot!.ClassKeys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""["Ring_A_C"]""")]
    [InlineData("""{"scannedAt":1758800000}""")]
    [InlineData("""{"items":"Ring_A_C"}""")]
    [InlineData("""{"items":{"a":"Ring_A_C"}}""")]
    public void Parse_AnythingElse_ReturnsNull(string? json)
    {
        Assert.Null(OwnedItemsService.Parse(json, Fallback));
    }

    // The mod rewrites the file in place every 5s, so the app can read it half-written. No prefix
    // of a real document may parse as a (smaller) inventory, or items would flicker to "missing".
    [Fact]
    public void Parse_EveryTruncatedWriteIsRejected()
    {
        for (var length = 0; length < ValidJson.Length; length++)
            Assert.Null(OwnedItemsService.Parse(ValidJson[..length], Fallback));
    }

    [Fact]
    public void Snapshot_StalenessAndItemComparison()
    {
        var keys = CollectionTracker.ToClassKeys(new[] { "Ring_A_C" });
        var snapshot = new OwnedItemsSnapshot(keys, Fallback);

        Assert.False(snapshot.IsStale(Fallback.AddSeconds(29), TimeSpan.FromSeconds(30)));
        Assert.True(snapshot.IsStale(Fallback.AddSeconds(31), TimeSpan.FromSeconds(30)));

        Assert.True(snapshot.HasSameItemsAs(new OwnedItemsSnapshot(CollectionTracker.ToClassKeys(new[] { "RING_A_C" }), Fallback.AddHours(1))));
        Assert.False(snapshot.HasSameItemsAs(new OwnedItemsSnapshot(new HashSet<string>(), Fallback)));
        Assert.False(snapshot.HasSameItemsAs(null));
    }

    [Fact]
    public void Refresh_TracksTheFileAndKeepsTheLastGoodSnapshot()
    {
        using var game = new TempGameFolder();
        var service = new OwnedItemsService(game.PathService);

        // No file yet.
        Assert.False(service.Refresh());
        Assert.Null(service.Current);

        game.WriteOwnedItems(ValidJson, game.Time(1));
        Assert.True(service.Refresh());
        Assert.Contains("weapon_alphaomega_c", service.Current!.ClassKeys);

        // Unchanged write time: not re-read.
        Assert.False(service.Refresh());

        // Half-written file: ignored, previous snapshot stays.
        var good = service.Current;
        game.WriteOwnedItems("""{"scannedAt":1758800001,"items":["Ring_""", game.Time(2));
        Assert.False(service.Refresh());
        Assert.Same(good, service.Current);

        // Finished write: picked up.
        game.WriteOwnedItems("""{"scannedAt":1758800002,"items":["Ring_B_C"]}""", game.Time(3));
        Assert.True(service.Refresh());
        Assert.Equal(new[] { "ring_b_c" }, service.Current!.ClassKeys);

        // File deleted (e.g. mod reinstalled): keep what we had.
        File.Delete(game.OwnedItemsPath);
        Assert.False(service.Refresh());
        Assert.NotNull(service.Current);
    }

    [Fact]
    public void Refresh_WhileTheModHoldsTheFileOpen_DoesNotThrow()
    {
        using var game = new TempGameFolder();
        var service = new OwnedItemsService(game.PathService);

        game.WriteOwnedItems(ValidJson, game.Time(1));

        using (new FileStream(game.OwnedItemsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.False(service.Refresh());
            Assert.Null(service.Current);
        }

        // Retried once the lock is gone, even though the write time didn't change.
        Assert.True(service.Refresh());
        Assert.NotNull(service.Current);
    }

    [Fact]
    public void Refresh_AfterSwitchingGameFolder_DropsTheOldSnapshot()
    {
        using var first = new TempGameFolder();
        using var second = new TempGameFolder();

        var service = new OwnedItemsService(first.PathService);

        first.WriteOwnedItems(ValidJson, first.Time(1));
        Assert.True(service.Refresh());

        first.PathService.Settings.Win64Path = second.Root;

        Assert.True(service.Refresh());
        Assert.Null(service.Current);
    }
}
