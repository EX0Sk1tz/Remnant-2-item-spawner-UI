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

    private void RunChecks_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.RefreshDiagnostics();

        Dispatcher.BeginInvoke(new Action(AnimateResultsIn), DispatcherPriority.Loaded);
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