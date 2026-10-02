using System.IO;
using System.Text;
using System.Text.Json;
using Remnant2UnlockerApp.Models;

namespace Remnant2UnlockerApp.Services;

/// <summary>
/// Settings > General > Save/Load: one fixed profile file next to the app's other data
/// (%AppData%\Remnant2UnlockerApp\settings-profile.json), so no file dialog is needed. It lives
/// outside the game folder on purpose and survives reinstalling the game or the mod.
/// </summary>
public sealed class SettingsProfileService
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    public SettingsProfileService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Remnant2UnlockerApp"))
    {
    }

    public SettingsProfileService(string directory)
    {
        ProfilePath = Path.Combine(directory, "settings-profile.json");
    }

    public string ProfilePath { get; }

    public bool Exists => File.Exists(ProfilePath);

    /// <summary>When the profile was last saved, or null if there is none yet.</summary>
    public DateTime? LastSaved => Exists ? File.GetLastWriteTime(ProfilePath) : null;

    public void Save(SettingsProfile profile)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath)!);
        SafeFile.WriteAtomic(ProfilePath, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(profile, WriteOptions)));
    }

    /// <summary>The saved profile, or null if none has been saved yet. Throws if the file is unreadable.</summary>
    public SettingsProfile? Load()
    {
        if (!Exists)
            return null;

        return JsonSerializer.Deserialize<SettingsProfile>(File.ReadAllText(ProfilePath), ReadOptions)
            ?? throw new InvalidDataException("The settings profile is empty.");
    }
}
