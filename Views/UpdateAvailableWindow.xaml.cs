using System.Windows;
using System.Windows.Media;
using WpfColor = System.Windows.Media.Color;

namespace Remnant2UnlockerApp.Views;

public partial class UpdateAvailableWindow : Window
{
    public UpdateAvailableWindow(string currentVersion, string latestVersion, string? releaseNotes)
    {
        InitializeComponent();

        VersionText.Text = $"{currentVersion} → {latestVersion}";

        var notes = string.IsNullOrWhiteSpace(releaseNotes) ? "No release notes provided." : releaseNotes;
        var foreground = new SolidColorBrush(WpfColor.FromRgb(0xD8, 0xDC, 0xE5));

        ReleaseNotesViewer.Document = MarkdownFlowDocument.Build(notes, foreground);
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
