using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Threading;
using Remnant2UnlockerApp.Services;
using Remnant2UnlockerApp.ViewModels;

namespace Remnant2UnlockerApp.Views;

public partial class DiagnosticsWindow : Window
{
    private readonly MainViewModel _viewModel;

    public DiagnosticsWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private async void RunChecks_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.RefreshDiagnosticsAsync();

        _ = Dispatcher.BeginInvoke(new Action(AnimateResultsIn), DispatcherPriority.Loaded);
    }

    private void AnimateResultsIn()
    {
        ResultsItemsControl.UpdateLayout();

        var generator = ResultsItemsControl.ItemContainerGenerator;

        for (var i = 0; i < ResultsItemsControl.Items.Count; i++)
        {
            if (generator.ContainerFromIndex(i) is not ContentPresenter presenter)
                continue;

            var transform = new TranslateTransform(0, 24);
            presenter.RenderTransform = transform;
            presenter.Opacity = 0;

            var delay = TimeSpan.FromMilliseconds(i * 70);
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
            {
                BeginTime = delay,
                EasingFunction = easing
            };

            var slideUp = new DoubleAnimation(24, 0, TimeSpan.FromMilliseconds(300))
            {
                BeginTime = delay,
                EasingFunction = easing
            };

            presenter.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            transform.BeginAnimation(TranslateTransform.YProperty, slideUp);
        }
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

    private async void CopyReport_Click(object sender, RoutedEventArgs e)
    {
        var reportPath = AppLogService.GetDiagnosticsLogPath();

        if (!File.Exists(reportPath) || sender is not System.Windows.Controls.Button button)
            return;

        try
        {
            System.Windows.Clipboard.SetText(File.ReadAllText(reportPath));

            // DiagnosticsWindow is a modal dialog on top of MainWindow, where the toast overlay
            // lives -- a ToastService toast would be rendered behind this window and never seen,
            // so give feedback directly on the button instead.
            var original = button.Content;
            button.Content = "Copied!";
            button.IsEnabled = false;

            await Task.Delay(1200);

            button.Content = original;
            button.IsEnabled = true;
        }
        catch (Exception ex)
        {
            AppLogService.Error("Failed to copy diagnostics report to clipboard", ex);
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var logDir = Path.GetDirectoryName(AppLogService.GetLogPath())!;
        Directory.CreateDirectory(logDir);

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{logDir}\"",
            UseShellExecute = true
        });
    }
}