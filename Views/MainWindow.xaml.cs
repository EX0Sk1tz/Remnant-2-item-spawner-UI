using System.Diagnostics;
using System.Windows;
using Remnant2UnlockerApp.Models;
using Remnant2UnlockerApp.ViewModels;

namespace Remnant2UnlockerApp.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }

    private RemnantItem? GetItemFromSender(object sender)
    {
        if (sender is not FrameworkElement element)
            return null;

        return element.DataContext as RemnantItem;
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
        if (sender is not System.Windows.Controls.Button button)
            return;

        if (button.DataContext is not RemnantItem item)
            return;

        if (DataContext is not MainViewModel vm)
            return;

        string url;

        if (vm.SelectedWiki == "Fextralife")
        {
            var pageName = Uri.EscapeDataString(item.Name.Replace(" ", "+"));
            url = $"https://remnant2.wiki.fextralife.com/{pageName}";
        }
        else
        {
            var pageName = item.Name
                .Replace(" ", "_")
                .Replace("'", "%27");

            url = $"https://remnant2.wiki.gg/wiki/{pageName}";
        }

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
}