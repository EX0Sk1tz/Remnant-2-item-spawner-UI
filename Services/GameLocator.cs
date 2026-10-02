using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Remnant2UnlockerApp.Services;

public enum GamePlatform
{
    Steam,
    Epic,
    GamePass
}

public sealed record GameInstallCandidate(string Win64Path, GamePlatform Platform)
{
    public bool IsValid => GamePathService.HasSupportedGameExe(Win64Path);

    public string PlatformName => Platform switch
    {
        GamePlatform.GamePass => "Game Pass",
        _ => Platform.ToString()
    };
}

/// <summary>Finds Remnant 2 installs on this PC (Steam libraries, Epic manifests, Game Pass drives).</summary>
public static partial class GameLocator
{
    /// <summary>Remnant II on Steam (appmanifest_1282100.acf, "name" "Remnant II").</summary>
    public const string SteamAppId = "1282100";

    private static readonly string SteamGameRelative = Path.Combine("steamapps", "common", "Remnant2", "Remnant2", "Binaries", "Win64");
    private static readonly string GamePassRelative = Path.Combine("XboxGames", "Remnant 2", "Content", "Remnant2", "Binaries", "WinGDK");

    [GeneratedRegex(@"""path""\s+""(?<path>(?:[^""\\]|\\.)*)""", RegexOptions.IgnoreCase)]
    private static partial Regex VdfPathRegex();

    // Old (pre-2021) format: "1"  "D:\\SteamLibrary" directly under "LibraryFolders".
    [GeneratedRegex(@"^\s*""\d+""\s+""(?<path>(?:[^""\\]|\\.)*)""\s*$", RegexOptions.Multiline)]
    private static partial Regex VdfLegacyPathRegex();

    /// <summary>All installs found, valid ones only, without duplicates.</summary>
    public static IReadOnlyList<GameInstallCandidate> FindInstalls()
    {
        var candidates = new List<GameInstallCandidate>();

        void Add(Func<IEnumerable<GameInstallCandidate>> source, string name)
        {
            try
            {
                candidates.AddRange(source());
            }
            catch (Exception ex)
            {
                AppLogService.Warn($"Game search ({name}) failed", ex);
            }
        }

        Add(() => FromSteamLibraries(GetSteamLibraries()), "Steam");
        Add(() => FromEpicManifests(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests")), "Epic");
        Add(() => FromDriveRoots(DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
            .Select(d => d.RootDirectory.FullName)), "Game Pass");

        return Distinct(candidates.Where(c => c.IsValid));
    }

    /// <summary>
    /// Maps a folder the user browsed to onto the folder with the game exe, e.g. the game's root
    /// ("...\common\Remnant2") → "...\Remnant2\Binaries\Win64". Returns the input if nothing fits.
    /// </summary>
    public static string NormalizeSelection(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || GamePathService.HasSupportedGameExe(path))
            return path;

        var candidates = new[]
        {
            Path.Combine(path, "Remnant2", "Binaries", "Win64"),
            Path.Combine(path, "Binaries", "Win64"),
            Path.Combine(path, "Content", "Remnant2", "Binaries", "WinGDK"),
            Path.Combine(path, "Remnant2", "Binaries", "WinGDK"),
            Path.Combine(path, "Binaries", "WinGDK")
        };

        return candidates.FirstOrDefault(GamePathService.HasSupportedGameExe) ?? path;
    }

    /// <summary>Steam installs live under a "steamapps" folder; Epic uses the same exe name.</summary>
    public static bool IsSteamLibraryPath(string win64Path) =>
        win64Path.Contains($"{Path.DirectorySeparatorChar}steamapps{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    public static IEnumerable<GameInstallCandidate> FromSteamLibraries(IEnumerable<string> libraries) =>
        libraries.Select(lib => new GameInstallCandidate(Path.Combine(lib, SteamGameRelative), GamePlatform.Steam));

    public static IEnumerable<GameInstallCandidate> FromDriveRoots(IEnumerable<string> roots) =>
        roots.Select(root => new GameInstallCandidate(Path.Combine(root, GamePassRelative), GamePlatform.GamePass));

    public static IEnumerable<GameInstallCandidate> FromEpicManifests(string manifestsFolder)
    {
        if (!Directory.Exists(manifestsFolder))
            yield break;

        foreach (var file in Directory.EnumerateFiles(manifestsFolder, "*.item"))
        {
            string? installLocation;

            try
            {
                installLocation = ParseEpicManifest(File.ReadAllText(file));
            }
            catch (Exception ex)
            {
                AppLogService.Warn($"Could not read Epic manifest {file}", ex);
                continue;
            }

            if (installLocation == null)
                continue;

            // The launcher's InstallLocation is the game root; the exe is one level deeper.
            var nested = Path.Combine(installLocation, "Remnant2", "Binaries", "Win64");
            var flat = Path.Combine(installLocation, "Binaries", "Win64");

            yield return new GameInstallCandidate(
                GamePathService.HasSupportedGameExe(flat) && !GamePathService.HasSupportedGameExe(nested) ? flat : nested,
                GamePlatform.Epic);
        }
    }

    /// <summary>Steam library roots: the Steam folder itself plus every "path" in libraryfolders.vdf.</summary>
    public static IReadOnlyList<string> GetSteamLibraries()
    {
        var steamPaths = new[]
        {
            Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string,
            Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string
        };

        var libraries = new List<string>();

        foreach (var steamPath in steamPaths.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => Path.GetFullPath(p!)))
        {
            var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");

            // The vdf first: it has the folder's real casing (the registry's SteamPath is often "c:/program files (x86)/steam").
            if (File.Exists(vdf))
                libraries.AddRange(ParseSteamLibraryFolders(File.ReadAllText(vdf)));

            libraries.Add(steamPath);
        }

        return libraries.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static IReadOnlyList<string> ParseSteamLibraryFolders(string vdf)
    {
        var matches = VdfPathRegex().Matches(vdf);

        return (matches.Count > 0 ? matches : VdfLegacyPathRegex().Matches(vdf))
            .Select(m => Unescape(m.Groups["path"].Value))
            .Where(p => Path.IsPathFullyQualified(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>The InstallLocation of a Remnant 2 Epic manifest, or null for any other game.</summary>
    public static string? ParseEpicManifest(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string Get(string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

        var isRemnant = new[] { Get("DisplayName"), Get("AppName"), Get("MainGameAppName"), Get("LaunchExecutable") }
            .Any(v => v.Contains("Remnant", StringComparison.OrdinalIgnoreCase));

        var location = Get("InstallLocation");

        return isRemnant && location.Length > 0 ? location : null;
    }

    private static IReadOnlyList<GameInstallCandidate> Distinct(IEnumerable<GameInstallCandidate> candidates) => candidates
        .GroupBy(c => Path.GetFullPath(c.Win64Path).TrimEnd('\\'), StringComparer.OrdinalIgnoreCase)
        .Select(g => g.First())
        .ToList();

    private static string Unescape(string value) => Regex.Replace(value, @"\\(.)", "$1");
}
