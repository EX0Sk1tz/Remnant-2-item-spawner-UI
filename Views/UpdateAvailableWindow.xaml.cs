using System.Windows;

namespace Remnant2UnlockerApp.Views;

public partial class UpdateAvailableWindow : Window
{
    public UpdateAvailableWindow(string currentVersion, string latestVersion, string? releaseNotes)
    {
        InitializeComponent();

        VersionText.Text = $"{currentVersion} → {latestVersion}";

        var notes = string.IsNullOrWhiteSpace(releaseNotes) ? "No release notes provided." : releaseNotes;

        ReleaseNotesViewer.Document = MarkdownFlowDocument.Build(notes);
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
