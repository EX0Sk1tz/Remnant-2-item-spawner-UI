using System.Windows.Threading;
using Remnant2UnlockerApp.Services;

namespace Remnant2UnlockerApp;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        AppLogService.Info($"Remnant 2 Unlocker started (v{version})");

        Exit += (_, _) => AppLogService.Info("Remnant 2 Unlocker exited");
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