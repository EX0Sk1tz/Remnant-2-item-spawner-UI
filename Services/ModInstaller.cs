using System.IO;

namespace Remnant2UnlockerApp.Services;

public enum ModFileState
{
    UpToDate,
    Outdated,
    Missing
}

public sealed record ModFileStatus(ModPayloadFile File, string TargetPath, ModFileState State);

public sealed class ModInstallStatus
{
    public required string ModsFolder { get; init; }

    public required IReadOnlyList<ModFileStatus> Files { get; init; }

    /// <summary>Mods\Remnant2Unlocker\Scripts\main.lua exists.</summary>
    public required bool IsModInstalled { get; init; }

    /// <summary>UE4SS's ConsoleCommandsMod exists, so the summon patch can be applied.</summary>
    public required bool IsConsoleCommandsModPresent { get; init; }

    /// <summary>The installed summon_unloaded_assets.lua is ours (stack size and item level work).</summary>
    public required bool IsSummonPatchApplied { get; init; }

    public required string ConfigPath { get; init; }

    /// <summary>Installed mods the bridge needs that aren't "Name : 1" in the mod config.</summary>
    public required IReadOnlyList<string> NotEnabledMods { get; init; }

    public int OutOfDateCount => Files.Count(f => f.State != ModFileState.UpToDate);

    public bool AreFilesUpToDate => OutOfDateCount == 0;

    public bool IsUpToDate => AreFilesUpToDate && NotEnabledMods.Count == 0;
}

public enum ModFileOutcome
{
    Unchanged,
    Installed,
    Updated,
    Failed
}

public sealed record ModFileResult(string TargetPath, ModFileOutcome Outcome, string? Error = null);

public sealed class ModInstallResult
{
    public List<ModFileResult> Files { get; } = new();

    public ModsConfigEdit? ConfigEdit { get; set; }

    public string? ConfigError { get; set; }

    public string? BackupFolder { get; set; }

    /// <summary>Files in our Scripts folder that the app doesn't ship (reported, never deleted).</summary>
    public List<string> ExtraFiles { get; } = new();

    public bool AccessDenied { get; set; }

    public int ChangedCount => Files.Count(f => f.Outcome is ModFileOutcome.Installed or ModFileOutcome.Updated);

    public int FailedCount => Files.Count(f => f.Outcome == ModFileOutcome.Failed);

    public bool Success => FailedCount == 0 && ConfigError == null;
}

/// <summary>
/// Installs the embedded mod payload (<see cref="ModPayload"/>) into a game folder and keeps it in
/// sync with this app version. Everything it overwrites is backed up first (<see cref="InstallBackup"/>),
/// and it never deletes files.
/// </summary>
public static class ModInstaller
{
    public static ModInstallStatus GetStatus(string win64Path)
    {
        var modsFolder = GamePathService.ResolveModsFolder(win64Path);
        var consoleCommandsPresent = Directory.Exists(Path.Combine(modsFolder, ModPayload.ConsoleCommandsComponent));
        var files = new List<ModFileStatus>();

        foreach (var file in ModPayload.Files)
        {
            // Only patch UE4SS's ConsoleCommandsMod when UE4SS put it there; never create it.
            if (file.Component == ModPayload.ConsoleCommandsComponent && !consoleCommandsPresent)
                continue;

            var target = file.GetTargetPath(modsFolder);
            var hash = ModPayload.HashFile(target);
            var state = hash == null ? ModFileState.Missing
                : hash == file.Sha256 ? ModFileState.UpToDate
                : ModFileState.Outdated;

            files.Add(new ModFileStatus(file, target, state));
        }

        var summonPatch = ModPayload.SummonPatch;
        var summonApplied = consoleCommandsPresent && summonPatch != null
            && ModPayload.HashFile(summonPatch.GetTargetPath(modsFolder)) == summonPatch.Sha256;

        var configPath = ModsConfig.ResolveConfigPath(modsFolder);
        var (content, _) = ModsConfig.ReadFile(configPath);

        return new ModInstallStatus
        {
            ModsFolder = modsFolder,
            Files = files,
            // Not just the folder: the app itself creates it for hotkeys.json/cheats.json.
            IsModInstalled = File.Exists(Path.Combine(modsFolder, ModPayload.UnlockerComponent, "Scripts", "main.lua")),
            IsConsoleCommandsModPresent = consoleCommandsPresent,
            IsSummonPatchApplied = summonApplied,
            ConfigPath = configPath,
            NotEnabledMods = ModsConfig.GetNotEnabled(content, GetModsToEnable(modsFolder))
        };
    }

    /// <summary>
    /// Writes every missing/outdated payload file, then makes sure the needed mods are enabled.
    /// Running it again right after changes nothing.
    /// </summary>
    public static ModInstallResult InstallOrUpdate(string win64Path)
    {
        var status = GetStatus(win64Path);
        var result = new ModInstallResult();
        var backup = new InstallBackup(win64Path);

        foreach (var file in status.Files)
        {
            if (file.State == ModFileState.UpToDate)
            {
                result.Files.Add(new ModFileResult(file.TargetPath, ModFileOutcome.Unchanged));
                continue;
            }

            try
            {
                backup.Save(file.TargetPath);
                SafeFile.WriteAtomic(file.TargetPath, file.File.Content);

                if (ModPayload.HashFile(file.TargetPath) != file.File.Sha256)
                    throw new IOException("The file on disk doesn't match after writing it.");

                result.Files.Add(new ModFileResult(
                    file.TargetPath,
                    file.State == ModFileState.Missing ? ModFileOutcome.Installed : ModFileOutcome.Updated));
            }
            catch (Exception ex)
            {
                result.AccessDenied |= SafeFile.IsAccessDenied(ex);
                result.Files.Add(new ModFileResult(file.TargetPath, ModFileOutcome.Failed, ex.Message));
                AppLogService.Error($"Mod install: could not write {file.TargetPath}", ex);
            }
        }

        UpdateModsConfig(win64Path, status.ModsFolder, backup, result);
        CollectExtraFiles(status.ModsFolder, result);

        result.BackupFolder = backup.Folder;

        if (backup.Folder != null)
            InstallBackup.Prune(win64Path);

        AppLogService.Info(
            $"Mod install/update in {status.ModsFolder}: {result.ChangedCount} file(s) written, {result.FailedCount} failed"
            + (result.ConfigEdit?.Changed == true ? $", enabled {string.Join(", ", result.ConfigEdit.Flipped.Concat(result.ConfigEdit.Added))}" : "")
            + (backup.Folder != null ? $", backup: {backup.Folder}" : ""));

        return result;
    }

    public static IReadOnlyList<string> RestoreLatestBackup(string win64Path) => InstallBackup.RestoreLatest(win64Path);

    /// <summary>
    /// Only (re)applies the mod config part of an install: enables the needed mods that are installed.
    /// Used after a package install, which adds new mod folders.
    /// </summary>
    public static ModInstallResult EnableMods(string win64Path, InstallBackup backup)
    {
        var result = new ModInstallResult();
        UpdateModsConfig(win64Path, GamePathService.ResolveModsFolder(win64Path), backup, result);
        return result;
    }

    /// <summary>
    /// The mods that must be "Name : 1": the required ones that are installed, our own mod, and
    /// Summonable Traits if present.
    /// </summary>
    public static IReadOnlyList<string> GetModsToEnable(string modsFolder)
    {
        var names = ModsConfig.RequiredMods
            .Where(name => name == ModPayload.UnlockerComponent || Directory.Exists(Path.Combine(modsFolder, name)))
            .ToList();

        var traitsFolder = SummonableTraitsService.FindModFolder(modsFolder);

        if (traitsFolder != null && !PathsEqual(traitsFolder, modsFolder))
            names.Add(Path.GetFileName(traitsFolder));

        return names;
    }

    private static void UpdateModsConfig(string win64Path, string modsFolder, InstallBackup backup, ModInstallResult result)
    {
        var configPath = ModsConfig.ResolveConfigPath(modsFolder);

        try
        {
            var (content, hasBom) = ModsConfig.ReadFile(configPath);
            var edit = ModsConfig.EnsureEnabled(content, GetModsToEnable(modsFolder));

            result.ConfigEdit = edit;

            if (!edit.Changed)
                return;

            backup.Save(configPath);
            SafeFile.WriteAtomic(configPath, ModsConfig.Encode(edit.Content, hasBom));
        }
        catch (Exception ex)
        {
            result.AccessDenied |= SafeFile.IsAccessDenied(ex);
            result.ConfigError = ex.Message;
            AppLogService.Error($"Mod install: could not update {configPath}", ex);
        }
    }

    private static void CollectExtraFiles(string modsFolder, ModInstallResult result)
    {
        var scripts = Path.Combine(modsFolder, ModPayload.UnlockerComponent, "Scripts");

        if (!Directory.Exists(scripts))
            return;

        var shipped = ModPayload.Files
            .Where(f => f.Component == ModPayload.UnlockerComponent)
            .Select(f => f.GetTargetPath(modsFolder))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        result.ExtraFiles.AddRange(Directory
            .GetFiles(scripts, "*", SearchOption.AllDirectories)
            .Where(file => !shipped.Contains(file)));
    }

    private static bool PathsEqual(string a, string b) => string.Equals(
        Path.GetFullPath(a).TrimEnd('\\', '/'),
        Path.GetFullPath(b).TrimEnd('\\', '/'),
        StringComparison.OrdinalIgnoreCase);
}
