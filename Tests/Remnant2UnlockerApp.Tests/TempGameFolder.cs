using System.IO;
using Remnant2UnlockerApp.Services;

namespace Remnant2UnlockerApp.Tests;

// A throwaway "<game>\Mods\Remnant2Unlocker" folder with a GamePathService pointed at it.
internal sealed class TempGameFolder : IDisposable
{
    public TempGameFolder()
    {
        Root = Path.Combine(Path.GetTempPath(), "R2UnlockerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ModRoot);

        // Settings are only changed in memory -- the user's saved game path is never written.
        PathService = new GamePathService();
        PathService.Settings.Win64Path = Root;
    }

    public string Root { get; }

    public string ModRoot => Path.Combine(Root, "Mods", "Remnant2Unlocker");

    public GamePathService PathService { get; }

    public string OwnedItemsPath => Path.Combine(ModRoot, "owned_items.json");

    public DateTime Time(int second) => new(2026, 1, 1, 0, 0, second, DateTimeKind.Utc);

    public void WriteOwnedItems(string content, DateTime writeTimeUtc)
    {
        File.WriteAllText(OwnedItemsPath, content);
        File.SetLastWriteTimeUtc(OwnedItemsPath, writeTimeUtc);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
