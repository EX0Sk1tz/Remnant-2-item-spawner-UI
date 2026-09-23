using System.Windows;

namespace Remnant2UnlockerApp.Views;

public partial class UpdateAvailableWindow : Window
{
    public UpdateAvailableWindow(string currentVersion, string latestVersion, string? releaseNotes)
    {
        InitializeComponent();

        VersionText.Text = $"{currentVersion} → {latestVersion}";
        ReleaseNotesText.Text = string.IsNullOrWhiteSpace(releaseNotes)
            ? "No release notes provided."
            : releaseNotes;
    }

    private void Later_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void UpdateNow_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
