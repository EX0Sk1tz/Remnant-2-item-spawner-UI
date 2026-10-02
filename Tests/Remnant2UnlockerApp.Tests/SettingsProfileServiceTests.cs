using System.IO;
using Remnant2UnlockerApp.Models;
using Remnant2UnlockerApp.Services;
using Xunit;

namespace Remnant2UnlockerApp.Tests;

public class SettingsProfileServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "R2U-ProfileTests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Load_WithoutSavedProfile_ReturnsNull()
    {
        var service = new SettingsProfileService(_dir);

        Assert.False(service.Exists);
        Assert.Null(service.LastSaved);
        Assert.Null(service.Load());
    }

    [Fact]
    public void Save_CreatesTheFolderAndRoundTrips()
    {
        var service = new SettingsProfileService(_dir);

        service.Save(new SettingsProfile
        {
            Cheats = new CheatSettings { InfiniteHealth = true, AimFov = 12.5 },
            Hotkeys = new HotkeySettings { Teleport = "F7", StackSize = 20 },
            WeaponModBoosts = new() { ["Mod"] = new() { ["Damage"] = 10 } }
        });

        var loaded = service.Load();

        Assert.True(service.Exists);
        Assert.NotNull(service.LastSaved);
        Assert.True(loaded!.Cheats!.InfiniteHealth);
        Assert.Equal(12.5, loaded.Cheats.AimFov);
        Assert.Equal("F7", loaded.Hotkeys!.Teleport);
        Assert.Equal(20, loaded.Hotkeys.StackSize);
        Assert.Equal(10, loaded.WeaponModBoosts!["Mod"]["Damage"]);
    }

    [Fact]
    public void Save_ReplacesThePreviousProfile()
    {
        var service = new SettingsProfileService(_dir);

        service.Save(new SettingsProfile { Hotkeys = new HotkeySettings { Teleport = "F7" } });
        service.Save(new SettingsProfile { Hotkeys = new HotkeySettings { Teleport = "F8" } });

        Assert.Equal("F8", service.Load()!.Hotkeys!.Teleport);
        Assert.Single(Directory.GetFiles(_dir));
    }

    [Fact]
    public void Load_ReadsProfilesWrittenByTheOldSaveDialog()
    {
        // Old exports were plain JsonSerializer output; property names match case-insensitively.
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "settings-profile.json"), "{\"cheats\":{\"noRecoil\":true}}");

        Assert.True(new SettingsProfileService(_dir).Load()!.Cheats!.NoRecoil);
    }

    [Fact]
    public void Load_BrokenFile_Throws()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "settings-profile.json"), "not json");

        Assert.ThrowsAny<Exception>(() => new SettingsProfileService(_dir).Load());
    }
}
