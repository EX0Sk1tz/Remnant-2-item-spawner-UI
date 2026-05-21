using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using Remnant2UnlockerApp.Models;

namespace Remnant2UnlockerApp.Views;

public partial class DiagnosticsWindow : Window
{
    public DiagnosticsWindow(DiagnosticReport report)
    {
        InitializeComponent();
        DataContext = report;
    }

    private void DiscordLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = e.Uri.AbsoluteUri,
            UseShellExecute = true
        });

        e.Handled = true;
    }
}