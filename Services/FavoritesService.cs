using System.IO;
using System.Text.Json;

namespace Remnant2UnlockerApp.Services;

public sealed class FavoritesService
{
    private readonly string _path;
    private readonly HashSet<string> _favorites;

    public FavoritesService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Remnant2UnlockerApp");

        Directory.CreateDirectory(dir);

        _path = Path.Combine(dir, "favorites.json");
        _favorites = Load();
    }

    public bool IsFavorite(string itemPath) => _favorites.Contains(itemPath);

    public void SetFavorite(string itemPath, bool isFavorite)
    {
        var changed = isFavorite ? _favorites.Add(itemPath) : _favorites.Remove(itemPath);

        if (changed)
            Save();
    }

    private HashSet<string> Load()
    {
        try
        {
            if (!File.Exists(_path))
                return new HashSet<string>();

            var json = File.ReadAllText(_path);

            return JsonSerializer.Deserialize<HashSet<string>>(json) ?? new HashSet<string>();
        }
        catch (Exception ex)
        {
            AppLogService.Error($"Failed to load favorites.json from {_path}", ex);
            return new HashSet<string>();
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_favorites, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_path, json);
    }
}
