using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Point = System.Windows.Point;
using Remnant2UnlockerApp.Models;
using Remnant2UnlockerApp.Services;
using Remnant2UnlockerApp.ViewModels;

namespace Remnant2UnlockerApp.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        _viewModel.GroupSpawnQueued += (_, title) => OpenSpawnProgressWindow(title);
        _viewModel.UpdatePreviewRequested += (_, _) => OpenUpdatePreviewWindow();

        Loaded += async (_, _) => await OnLoadedAsync();
    }

    private async Task OnLoadedAsync()
    {
        if (!_viewModel.IsGamePathValid)
        {
            var wizard = new SetupWizardWindow(_viewModel)
            {
                Owner = this
            };

            wizard.ShowDialog();
        }

        await _viewModel.LoadAsync();

        _ = CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        var result = await UpdateCheckService.CheckForUpdateAsync();

        if (!result.IsUpdateAvailable)
            return;

        if (!string.IsNullOrWhiteSpace(result.LatestVersion))
            _viewModel.SetUpdateAvailable(result.LatestVersion!, result.DownloadUrl, result.ReleaseNotes);

        if (string.IsNullOrWhiteSpace(result.ReleaseUrl))
            return;

        var releaseUrl = result.ReleaseUrl;

        ToastService.Show(
            "Update available",
            $"Version {result.LatestVersion} is out (you have {result.CurrentVersion}).",
            ToastType.Info,
            durationMs: 12000,
            actionText: "View Release",
            onAction: () => Process.Start(new ProcessStartInfo
            {
                FileName = releaseUrl,
                UseShellExecute = true
            }));
    }

    private void ToastClose_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ToastMessage toast)
            ToastService.Dismiss(toast);
    }

    private void ToastAction_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not ToastMessage toast)
            return;

        toast.OnAction?.Invoke();
        ToastService.Dismiss(toast);
    }

    private RemnantItem? GetItemFromSender(object sender)
    {
        if (sender is not FrameworkElement element)
            return null;

        return element.DataContext as RemnantItem;
    }

    // Keeps the list feeling continuous during rapid clicking: if the row a user just acted on was
    // the last one fully visible, nudge the view down just far enough to reveal the next row instead
    // of making them scroll manually every time they reach the bottom of the viewport.
    private void ScrollNextItemIntoViewIfNeeded(RemnantItem item)
    {
        var items = _viewModel.Items;
        var index = items.IndexOf(item);

        if (index < 0 || index + 1 >= items.Count)
            return;

        var scrollViewer = FindScrollViewer(ItemsListView);
        if (scrollViewer == null)
            return;

        var nextItem = items[index + 1];
        var nextContainer = ItemsListView.ItemContainerGenerator.ContainerFromItem(nextItem) as FrameworkElement;

        if (nextContainer == null || !IsFullyVisible(scrollViewer, nextContainer))
            ItemsListView.ScrollIntoView(nextItem);
    }

    private static bool IsFullyVisible(ScrollViewer scrollViewer, FrameworkElement container)
    {
        var transform = container.TransformToAncestor(scrollViewer);
        var top = transform.Transform(new Point(0, 0)).Y;
        var bottom = transform.Transform(new Point(0, container.ActualHeight)).Y;

        return top >= 0 && bottom <= scrollViewer.ViewportHeight;
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer scrollViewer)
            return scrollViewer;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var found = FindScrollViewer(VisualTreeHelper.GetChild(root, i));
            if (found != null)
                return found;
        }

        return null;
    }

    private async void SpawnItem_Click(object sender, RoutedEventArgs e)
    {
        var item = GetItemFromSender(sender);

        if (item == null)
        {
            _viewModel.StatusText = "Spawn failed: item context missing";
            return;
        }

        await _viewModel.SpawnItemAsync(item);

        ScrollNextItemIntoViewIfNeeded(item);
    }

    private async void ForceConsoleSpawnItem_Click(object sender, RoutedEventArgs e)
    {
        var item = GetItemFromSender(sender);

        if (item == null)
        {
            _viewModel.StatusText = "Force spawn failed: item context missing";
            return;
        }

        await _viewModel.ForceConsoleSpawnItemAsync(item);

        ScrollNextItemIntoViewIfNeeded(item);
    }

    private void CopyCommand_Click(object sender, RoutedEventArgs e)
    {
        var item = GetItemFromSender(sender);

        if (item == null)
        {
            _viewModel.StatusText = "Copy failed: item context missing";
            return;
        }

        _viewModel.CopySummonCommand(item);
    }

    private void OpenWiki_Click(object sender, RoutedEventArgs e)
    {
        var item = GetItemFromSender(sender);

        if (item == null)
        {
            _viewModel.StatusText = "Wiki failed: item context missing";
            return;
        }

        var url = _viewModel.BuildWikiUrl(item);

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow(_viewModel)
        {
            Owner = this
        };

        window.ShowDialog();
    }

    private void OpenDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.RefreshDiagnostics();

        var window = new DiagnosticsWindow(_viewModel)
        {
            Owner = this
        };

        window.ShowDialog();
    }

    private void OpenUpdatePreviewWindow()
    {
        var window = new UpdateAvailableWindow(
            _viewModel.VersionText,
            _viewModel.LatestVersionText ?? "",
            _viewModel.ReleaseNotes)
        {
            Owner = this
        };

        if (window.ShowDialog() == true)
            _viewModel.UpdateNowCommand.Execute(null);
    }

    private void OpenGroupProgress_Click(object sender, RoutedEventArgs e)
    {
        OpenSpawnProgressWindow("Safe Group Spawn");
    }

    private void OpenSpawnProgressWindow(string title)
    {
        var viewModel = new SpawnProgressViewModel(
            _viewModel.BridgeStatusService,
            _viewModel.QueueWriter,
            title);

        var window = new SpawnProgressWindow(viewModel)
        {
            Owner = this
        };

        window.Show();
    }

    private async void SummonTrait_Click(object sender, RoutedEventArgs e)
    {
        var item = GetItemFromSender(sender);

        if (item == null)
        {
            _viewModel.StatusText = "SummonTrait failed: item context missing";
            return;
        }

        await _viewModel.SummonTraitAsync(item);

        ScrollNextItemIntoViewIfNeeded(item);
    }

    private async void AddTrait_Click(object sender, RoutedEventArgs e)
    {
        var item = GetItemFromSender(sender);

        if (item == null)
        {
            _viewModel.StatusText = "AddTrait failed: item context missing";
            return;
        }

        await _viewModel.AddTraitAsync(item);

        ScrollNextItemIntoViewIfNeeded(item);
    }

}