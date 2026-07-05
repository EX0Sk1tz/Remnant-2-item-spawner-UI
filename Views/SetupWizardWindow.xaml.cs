using System.Windows;
using Remnant2UnlockerApp.ViewModels;

namespace Remnant2UnlockerApp.Views;

public partial class SetupWizardWindow : Window
{
    public SetupWizardWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Finish_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
