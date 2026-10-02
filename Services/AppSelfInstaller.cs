using System.Diagnostics;
using System.IO;

namespace Remnant2UnlockerApp.Services;

/// <summary>
/// Lets the released single-file exe install itself: copy to a fixed folder, optional desktop
/// shortcut, then restart from there. Dev builds (bin\Debug, not single-file) never offer it.
/// </summary>
public static class AppSelfInstaller
{
    public const string ExeName = "Remnant2UnlockerApp.exe";
    public const string ShortcutName = "Remnant 2 Unlocker.lnk";

    public static string DefaultInstallDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Remnant2Unlocker");

    /// <summary>A single-file publish has no assembly location on disk.</summary>
#pragma warning disable IL3000 // Location being empty in a single-file app is exactly what this checks.
    public static bool IsSingleFileBuild => string.IsNullOrEmpty(typeof(App).Assembly.Location);
#pragma warning restore IL3000

    /// <summary>
    /// Offer the install dialog only for single-file builds, only once, and not when this exe
    /// already runs from the install folder (the saved one or the default).
    /// </summary>
    public static bool ShouldOfferInstall(bool isSingleFile, string? processPath, string? installDir, bool promptHandled)
    {
        if (!isSingleFile || promptHandled || string.IsNullOrWhiteSpace(processPath))
            return false;

        var runningDir = Path.GetDirectoryName(Path.GetFullPath(processPath)) ?? "";

        return !SameDir(runningDir, installDir) && !SameDir(runningDir, DefaultInstallDir);
    }

    public static string GetTargetExe(string installDir) => Path.Combine(installDir, ExeName);

    /// <summary>
    /// Copies <paramref name="sourceExe"/> into <paramref name="installDir"/> (overwriting an older
    /// install = update) and optionally creates <paramref name="shortcutPath"/> pointing at it.
    /// Returns the installed exe.
    /// </summary>
    public static string Install(string sourceExe, string installDir, string? shortcutPath)
    {
        var target = GetTargetExe(installDir);

        if (!string.Equals(Path.GetFullPath(sourceExe), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
        {
            SafeFile.CopyAtomic(sourceExe, target);

            // A build published without IncludeNativeLibrariesForSelfExtract needs WPF's native
            // DLLs next to the exe; without them the installed copy dies at start.
            foreach (var nativeDll in Directory.GetFiles(Path.GetDirectoryName(Path.GetFullPath(sourceExe))!, "*_cor3.dll"))
                SafeFile.CopyAtomic(nativeDll, Path.Combine(installDir, Path.GetFileName(nativeDll)));
        }

        if (shortcutPath != null)
        {
            try
            {
                CreateShortcut(shortcutPath, target);
            }
            catch (Exception ex)
            {
                // The app is installed; a missing shortcut is not worth failing over.
                AppLogService.Warn($"Could not create shortcut {shortcutPath}", ex);
                shortcutPath = null;
            }
        }

        AppLogService.Info($"App installed to {target}" + (shortcutPath != null ? $", shortcut {shortcutPath}" : ""));
        return target;
    }

    public static string DesktopShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ShortcutName);

    public static void CreateShortcut(string shortcutPath, string targetExe)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell is not available.");

        dynamic shell = Activator.CreateInstance(shellType)!;

        try
        {
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetExe;
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetExe);
            shortcut.IconLocation = targetExe + ",0";
            shortcut.Description = "Remnant 2 Unlocker";
            shortcut.Save();
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }
    }

    public static void StartInstalled(string exe, IEnumerable<string> args)
    {
        var startInfo = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe) };

        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);

        Process.Start(startInfo);
    }

    private static bool SameDir(string a, string? b) =>
        !string.IsNullOrWhiteSpace(b) && string.Equals(
            Path.GetFullPath(a).TrimEnd('\\', '/'),
            Path.GetFullPath(b).TrimEnd('\\', '/'),
            StringComparison.OrdinalIgnoreCase);
}
