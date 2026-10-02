using System.Globalization;
using System.IO;

namespace Remnant2UnlockerApp.Services;

/// <summary>
/// Backs up every game-folder file the installer is about to overwrite into
/// "&lt;Mods&gt;\Remnant2Unlocker\Backups\&lt;yyyy-MM-dd_HH-mm-ss&gt;\&lt;path relative to Win64&gt;".
/// One instance = one backup folder, created only once something is actually saved.
/// </summary>
public sealed class InstallBackup
{
    public const int KeepCount = 5;
    private const string TimestampFormat = "yyyy-MM-dd_HH-mm-ss";

    private readonly string _win64Path;
    private readonly HashSet<string> _saved = new(StringComparer.OrdinalIgnoreCase);

    public InstallBackup(string win64Path)
    {
        _win64Path = Path.GetFullPath(win64Path);
    }

    /// <summary>The backup folder, or null while nothing has been backed up.</summary>
    public string? Folder { get; private set; }

    public int Count => _saved.Count;

    public static string GetBackupsRoot(string win64Path) =>
        Path.Combine(GamePathService.ResolveModsFolder(win64Path), ModPayload.UnlockerComponent, "Backups");

    /// <summary>Copies <paramref name="path"/> into the backup (once per file); no-op if it doesn't exist.</summary>
    public void Save(string path)
    {
        var fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath) || _saved.Contains(fullPath))
            return;

        var relative = Path.GetRelativePath(_win64Path, fullPath);

        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new InvalidOperationException($"Refusing to back up a file outside the game folder: {fullPath}");

        Folder ??= CreateFolder();

        var destination = Path.Combine(Folder, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(fullPath, destination, overwrite: true);

        // File.Copy keeps a read-only attribute, which would make Prune fail on this backup later.
        File.SetAttributes(destination, File.GetAttributes(destination) & ~FileAttributes.ReadOnly);

        _saved.Add(fullPath);
    }

    /// <summary>Deletes all but the newest <see cref="KeepCount"/> backups.</summary>
    public static void Prune(string win64Path, int keep = KeepCount)
    {
        foreach (var dir in ListBackups(win64Path).Skip(keep))
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (Exception ex)
            {
                AppLogService.Warn($"Could not delete old backup {dir}", ex);
            }
        }
    }

    /// <summary>Backup folders, newest first.</summary>
    public static IReadOnlyList<string> ListBackups(string win64Path)
    {
        var root = GetBackupsRoot(win64Path);

        if (!Directory.Exists(root))
            return Array.Empty<string>();

        return Directory.GetDirectories(root)
            .Where(dir => DateTime.TryParseExact(
                Path.GetFileName(dir)[..Math.Min(TimestampFormat.Length, Path.GetFileName(dir).Length)],
                TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            .OrderByDescending(dir => Path.GetFileName(dir), StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Copies every file of the newest backup back to where it came from.</summary>
    public static IReadOnlyList<string> RestoreLatest(string win64Path)
    {
        var latest = ListBackups(win64Path).FirstOrDefault();

        if (latest == null)
            return Array.Empty<string>();

        var restored = new List<string>();

        foreach (var file in Directory.GetFiles(latest, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(win64Path, Path.GetRelativePath(latest, file));
            SafeFile.CopyAtomic(file, target);
            restored.Add(target);
        }

        AppLogService.Info($"Restored {restored.Count} file(s) from backup {latest}");
        return restored;
    }

    private string CreateFolder()
    {
        var root = GetBackupsRoot(_win64Path);
        var name = DateTime.Now.ToString(TimestampFormat, CultureInfo.InvariantCulture);
        var folder = Path.Combine(root, name);

        for (var i = 2; Directory.Exists(folder); i++)
            folder = Path.Combine(root, $"{name}_{i}");

        Directory.CreateDirectory(folder);
        return folder;
    }
}
