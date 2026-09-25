namespace Remnant2UnlockerApp.Models;

// One read of owned_items.json: the class keys (see CollectionTracker.ToClassKey) of everything in
// the player's inventory, and when the in-game mod took that scan.
public sealed class OwnedItemsSnapshot
{
    public OwnedItemsSnapshot(IReadOnlySet<string> classKeys, DateTimeOffset scannedAt)
    {
        ClassKeys = classKeys;
        ScannedAt = scannedAt;
    }

    public IReadOnlySet<string> ClassKeys { get; }

    public DateTimeOffset ScannedAt { get; }

    public bool IsStale(DateTimeOffset now, TimeSpan maxAge) => now - ScannedAt > maxAge;

    public bool HasSameItemsAs(OwnedItemsSnapshot? other) =>
        other != null && ClassKeys.SetEquals(other.ClassKeys);
}
