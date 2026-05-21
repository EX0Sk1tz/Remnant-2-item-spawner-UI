using System.Windows;
using Remnant2UnlockerApp.ViewModels;

namespace Remnant2UnlockerApp.Views;

public partial class SpawnProgressWindow : Window
{
    private readonly SpawnProgressViewModel _viewModel;

    public SpawnProgressWindow(SpawnProgressViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        Loaded += (_, _) => _viewModel.Start();
        Closed += (_, _) => _viewModel.Stop();
    }

    private async void Cancel_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.CancelAsync();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}