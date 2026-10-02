using System.Windows.Threading;
using Remnant2UnlockerApp.Services;

namespace Remnant2UnlockerApp;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        // Before base.OnStartup so every window, including the self-install dialog, gets grain and dark title bar.
        ThemeManager.Initialize();
        StartupOptions.Parse(e.Args);

        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        AppLogService.Info($"Remnant 2 Unlocker started (v{version})");

        // Also before base.OnStartup: after installing, this copy exits before any main window exists.
        if (OfferSelfInstall(e.Args))
        {
            AppLogService.Info("Exiting after self-install; the installed copy takes over");
            Environment.Exit(0);
        }

        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        Exit += (_, _) => AppLogService.Info("Remnant 2 Unlocker exited");
    }

    /// <summary>
    /// First start of the released exe: ask where to install it. Returns true when it was installed
    /// and the installed copy has been started.
    /// </summary>
    private bool OfferSelfInstall(string[] args)
    {
        try
        {
            var pathService = new GamePathService();
            var settings = pathService.Settings;

            if (!AppSelfInstaller.ShouldOfferInstall(
                    AppSelfInstaller.IsSingleFileBuild, Environment.ProcessPath, settings.InstallDir, settings.InstallPromptHandled))
            {
                return false;
            }

            var language = pathService.IsConfigured ? new HotkeySettingsService(pathService).Load().Language : null;
            var loc = new LocalizationService(string.IsNullOrWhiteSpace(language) ? "en" : language);

            // Closing the only open window would otherwise end the app before the main window exists.
            ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;

            var dialog = new Views.InstallAppWindow(loc, settings.InstallDir ?? AppSelfInstaller.DefaultInstallDir);
            var installed = dialog.ShowDialog() == true && dialog.InstalledExe != null;

            ShutdownMode = System.Windows.ShutdownMode.OnLastWindowClose;

            settings.InstallPromptHandled = true;

            if (installed)
                settings.InstallDir = System.IO.Path.GetDirectoryName(dialog.InstalledExe);

            pathService.SaveSettings();

            if (!installed)
            {
                AppLogService.Info("Self-install declined; running portable");
                return false;
            }

            AppSelfInstaller.StartInstalled(dialog.InstalledExe!, args);
            return true;
        }
        catch (Exception ex)
        {
            ShutdownMode = System.Windows.ShutdownMode.OnLastWindowClose;
            AppLogService.Error("Self-install prompt failed; continuing portable", ex);
            return false;
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLogService.Error("Unhandled UI exception", e.Exception);

        System.Windows.Forms.MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nDetails were written to the app log.",
            "Remnant 2 Unlocker",
            System.Windows.Forms.MessageBoxButtons.OK,
            System.Windows.Forms.MessageBoxIcon.Error);

        e.Handled = true;
    }
}