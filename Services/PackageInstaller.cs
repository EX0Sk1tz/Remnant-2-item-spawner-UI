using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace Remnant2UnlockerApp.Services;

public enum PackageKind
{
    Unknown,
    AllowAssetMods,
    SummonableTraits
}

public enum PackageError
{
    None,
    NotFound,
    UnsupportedFormat,
    ExtractFailed,
    NotAllowAssetMods,
    AllowModsModWithoutUe4ss,
    NotSummonableTraits,
    LooksLikeSummonableTraits,
    LooksLikeAllowAssetMods,
    AccessDenied,
    CopyFailed,
    VerifyFailed
}

public sealed class PackageInstallResult
{
    public PackageKind Kind { get; set; }

    public PackageError Error { get; set; }

    /// <summary>Technical detail for the log/UI (exception message, tar output, ...).</summary>
    public string? ErrorDetail { get; set; }

    public int FilesWritten { get; set; }

    public string? BackupFolder { get; set; }

    /// <summary>The mod folder name that was installed (Summonable Traits) or "AllowModsMod".</summary>
    public string? ModFolderName { get; set; }

    /// <summary>The follow-up sync of our own mod files and mods.txt (after Allow Asset Mods).</summary>
    public ModInstallResult? ModSync { get; set; }

    /// <summary>A leftover proxy DLL from an older UE4SS that the Nexus page says to delete.</summary>
    public bool HasLegacyXinputProxy { get; set; }

    public bool Success => Error == PackageError.None;

    public bool AccessDenied => Error == PackageError.AccessDenied || ModSync?.AccessDenied == true;
}

/// <summary>
/// Installs a Nexus download the user dropped onto the wizard: a .zip, .rar or .7z archive, or an
/// already-extracted folder. Only the two packages the app knows are accepted.
/// </summary>
public static class PackageInstaller
{
    public const string AllowAssetModsUrl = "https://www.nexusmods.com/remnant2/mods/2";
    public const string SummonableTraitsUrl = "https://www.nexusmods.com/remnant2/mods/122";
    public const string SummonableTraitsDefaultFolder = "SummonableTraits";

    private const string AllowModsModName = "AllowModsMod";

    public static readonly IReadOnlyList<string> SupportedExtensions = new[] { ".zip", ".rar", ".7z" };

    /// <summary>Windows 10+'s bsdtar, which reads RAR5 and 7z archives.</summary>
    public static string TarPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "tar.exe");

    public static bool IsUe4ssInstalled(string win64Path) =>
        File.Exists(Path.Combine(win64Path, "UE4SS.dll"))
        || File.Exists(Path.Combine(win64Path, "ue4ss", "UE4SS.dll"));

    public static bool IsAllowModsModInstalled(string win64Path) =>
        File.Exists(Path.Combine(GamePathService.ResolveModsFolder(win64Path), AllowModsModName, "dlls", "main.dll"));

    public static PackageInstallResult InstallAllowAssetMods(string win64Path, string source)
    {
        var result = new PackageInstallResult { Kind = PackageKind.AllowAssetMods, ModFolderName = AllowModsModName };

        using var package = OpenPackage(source, result);

        if (package == null)
            return result;

        var kind = DetectKind(package.Root);

        if (kind == PackageKind.SummonableTraits)
            return Fail(result, PackageError.LooksLikeSummonableTraits);

        var ammDll = FindFile(package.Root, Path.Combine(AllowModsModName, "dlls", "main.dll"));
        var ue4ssDll = FindFile(package.Root, "UE4SS.dll");

        if (ammDll == null)
            return Fail(result, PackageError.NotAllowAssetMods);

        var backup = new InstallBackup(win64Path);

        if (ue4ssDll != null)
        {
            // The full package: UE4SS + its standard mods + AllowModsMod, meant to be extracted into Win64.
            // An existing mods.txt/enabled.txt is kept: the package's stock one would drop the lines
            // of every other mod. ModInstaller enables what's needed afterwards.
            var root = FindUe4ssPackageRoot(package.Root) ?? package.Root;

            if (!CopyTree(root, win64Path, backup, result, keepExisting: IsModsConfigFile))
                return result;
        }
        else
        {
            // Just the AllowModsMod folder (some Nexus files are only that): needs UE4SS already there.
            if (!IsUe4ssInstalled(win64Path))
                return Fail(result, PackageError.AllowModsModWithoutUe4ss);

            var modDir = Path.GetDirectoryName(Path.GetDirectoryName(ammDll)!)!;
            var target = Path.Combine(GamePathService.ResolveModsFolder(win64Path), AllowModsModName);

            if (!CopyTree(modDir, target, backup, result))
                return result;
        }

        result.BackupFolder = backup.Folder;
        result.HasLegacyXinputProxy = File.Exists(Path.Combine(win64Path, "xinput1_3.dll"));

        if (!IsUe4ssInstalled(win64Path) || !IsAllowModsModInstalled(win64Path))
            return Fail(result, PackageError.VerifyFailed, "UE4SS.dll or Mods\\AllowModsMod\\dlls\\main.dll is missing after copying.");

        // The package's stock ConsoleCommandsMod just replaced our summon patch, and it may have
        // brought a mods.txt of its own: re-apply both (and install our mod if it's missing).
        result.ModSync = ModInstaller.InstallOrUpdate(win64Path);

        AppLogService.Info($"Allow Asset Mods installed from {source}: {result.FilesWritten} file(s)"
            + (backup.Folder != null ? $", backup: {backup.Folder}" : ""));

        return result;
    }

    public static PackageInstallResult InstallSummonableTraits(string win64Path, string source)
    {
        var result = new PackageInstallResult { Kind = PackageKind.SummonableTraits };

        using var package = OpenPackage(source, result);

        if (package == null)
            return result;

        var mainLua = SummonableTraitsService.FindMainLua(package.Root);

        if (mainLua == null)
        {
            return Fail(result, DetectKind(package.Root) == PackageKind.AllowAssetMods
                ? PackageError.LooksLikeAllowAssetMods
                : PackageError.NotSummonableTraits);
        }

        var modsFolder = GamePathService.ResolveModsFolder(win64Path);

        // Reinstalling goes into the folder it already lives in, so it isn't loaded twice.
        var existing = SummonableTraitsService.FindModFolder(modsFolder);
        var scriptsDir = Path.GetDirectoryName(mainLua)!;
        var isInScripts = Path.GetFileName(scriptsDir).Equals("Scripts", StringComparison.OrdinalIgnoreCase);
        var modDir = isInScripts ? Path.GetDirectoryName(scriptsDir)! : scriptsDir;

        var name = existing != null && !SamePath(existing, modsFolder)
            ? Path.GetFileName(existing)
            : SamePath(modDir, package.Root) ? SummonableTraitsDefaultFolder : Path.GetFileName(modDir);

        var target = Path.Combine(modsFolder, name);
        var backup = new InstallBackup(win64Path);

        // UE4SS wants <Mods>\<Name>\Scripts\main.lua; accept archives that only have the Scripts content.
        if (!CopyTree(isInScripts ? modDir : scriptsDir, isInScripts ? target : Path.Combine(target, "Scripts"), backup, result))
            return result;

        result.ModFolderName = name;
        result.ModSync = ModInstaller.EnableMods(win64Path, backup);
        result.BackupFolder = backup.Folder;

        if (result.ModSync.ConfigError != null)
            return Fail(result, result.ModSync.AccessDenied ? PackageError.AccessDenied : PackageError.CopyFailed, result.ModSync.ConfigError);

        AppLogService.Info($"Summonable Traits installed from {source} into {target}: {result.FilesWritten} file(s)");
        return result;
    }

    public static PackageKind DetectKind(string root)
    {
        if (SummonableTraitsService.FindMainLua(root) != null)
            return PackageKind.SummonableTraits;

        if (FindFile(root, Path.Combine(AllowModsModName, "dlls", "main.dll")) != null || FindFile(root, "UE4SS.dll") != null)
            return PackageKind.AllowAssetMods;

        return PackageKind.Unknown;
    }

    /// <summary>
    /// The folder meant to be extracted into Win64: the shallowest one that holds Mods\, ue4ss\,
    /// UE4SS.dll or dwmapi.dll (so a wrapper folder like "AMM\" is skipped).
    /// </summary>
    public static string? FindUe4ssPackageRoot(string root)
    {
        var queue = new Queue<string>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var dir = queue.Dequeue();

            if (Directory.Exists(Path.Combine(dir, "Mods"))
                || Directory.Exists(Path.Combine(dir, "ue4ss"))
                || File.Exists(Path.Combine(dir, "UE4SS.dll"))
                || File.Exists(Path.Combine(dir, "dwmapi.dll")))
            {
                return dir;
            }

            foreach (var sub in Directory.GetDirectories(dir).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                queue.Enqueue(sub);
        }

        return null;
    }

    private static ExtractedPackage? OpenPackage(string source, PackageInstallResult result)
    {
        if (Directory.Exists(source))
            return new ExtractedPackage(source, temp: null);

        if (!File.Exists(source))
        {
            Fail(result, PackageError.NotFound, source);
            return null;
        }

        var extension = Path.GetExtension(source).ToLowerInvariant();

        if (!SupportedExtensions.Contains(extension))
        {
            Fail(result, PackageError.UnsupportedFormat, extension);
            return null;
        }

        var temp = Path.Combine(Path.GetTempPath(), "Remnant2Unlocker", "pkg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var package = new ExtractedPackage(temp, temp);

        try
        {
            if (extension == ".zip")
                ZipFile.ExtractToDirectory(source, temp);
            else
                ExtractWithTar(source, temp);

            return package;
        }
        catch (Exception ex)
        {
            package.Dispose();
            AppLogService.Error($"Could not extract {source}", ex);
            Fail(result, ex is FileNotFoundException && !File.Exists(TarPath) ? PackageError.UnsupportedFormat : PackageError.ExtractFailed, ex.Message);
            return null;
        }
    }

    private static void ExtractWithTar(string archive, string destination)
    {
        if (!File.Exists(TarPath))
            throw new FileNotFoundException("tar.exe was not found, so .rar/.7z archives can't be extracted. Extract it yourself and choose the folder.", TarPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = TarPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        startInfo.ArgumentList.Add("-xf");
        startInfo.ArgumentList.Add(archive);
        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(destination);

        using var process = Process.Start(startInfo) ?? throw new IOException("tar.exe could not be started.");

        var stderr = process.StandardError.ReadToEndAsync();
        _ = process.StandardOutput.ReadToEndAsync();

        if (!process.WaitForExit(120_000))
        {
            process.Kill(entireProcessTree: true);
            throw new IOException("Extracting the archive took too long.");
        }

        if (process.ExitCode != 0)
            throw new IOException($"tar.exe failed ({process.ExitCode}): {stderr.Result.Trim()}");
    }

    private static bool IsModsConfigFile(string relativePath)
    {
        var name = Path.GetFileName(relativePath);
        var parent = Path.GetFileName(Path.GetDirectoryName(relativePath) ?? "");

        return parent.Equals("Mods", StringComparison.OrdinalIgnoreCase)
            && (name.Equals(ModsConfig.ModsTxtName, StringComparison.OrdinalIgnoreCase)
                || name.Equals(ModsConfig.EnabledTxtName, StringComparison.OrdinalIgnoreCase));
    }

    // Merge-copies every file, backing up what it overwrites. Stops at the first failure.
    // Files matching keepExisting are only copied if the target doesn't exist yet.
    private static bool CopyTree(
        string sourceDir,
        string targetDir,
        InstallBackup backup,
        PackageInstallResult result,
        Func<string, bool>? keepExisting = null)
    {
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var target = Path.Combine(targetDir, relative);

            if (keepExisting?.Invoke(relative) == true && File.Exists(target))
                continue;

            try
            {
                backup.Save(target);
                SafeFile.CopyAtomic(file, target);
                result.FilesWritten++;
            }
            catch (Exception ex)
            {
                AppLogService.Error($"Package install: could not write {target}", ex);
                result.BackupFolder = backup.Folder;
                Fail(result, SafeFile.IsAccessDenied(ex) ? PackageError.AccessDenied : PackageError.CopyFailed, $"{target}: {ex.Message}");
                return false;
            }
        }

        return true;
    }

    private static string? FindFile(string root, string relativeSuffix)
    {
        var name = Path.GetFileName(relativeSuffix);
        var suffix = Path.DirectorySeparatorChar + relativeSuffix;

        return Directory
            .EnumerateFiles(root, name, SearchOption.AllDirectories)
            .Where(f => f.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.Length)
            .FirstOrDefault();
    }

    private static PackageInstallResult Fail(PackageInstallResult result, PackageError error, string? detail = null)
    {
        result.Error = error;
        result.ErrorDetail = detail;
        return result;
    }

    private static bool SamePath(string a, string b) => string.Equals(
        Path.GetFullPath(a).TrimEnd('\\', '/'),
        Path.GetFullPath(b).TrimEnd('\\', '/'),
        StringComparison.OrdinalIgnoreCase);

    private sealed class ExtractedPackage : IDisposable
    {
        private readonly string? _temp;

        public ExtractedPackage(string root, string? temp)
        {
            Root = root;
            _temp = temp;
        }

        public string Root { get; }

        public void Dispose()
        {
            if (_temp == null)
                return;

            try
            {
                Directory.Delete(_temp, recursive: true);
            }
            catch (Exception ex)
            {
                AppLogService.Warn($"Could not delete temp folder {_temp}", ex);
            }
        }
    }
}
