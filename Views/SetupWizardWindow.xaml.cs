using System.IO;
using System.Windows;
using Remnant2UnlockerApp.Services;
using Remnant2UnlockerApp.ViewModels;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using Forms = System.Windows.Forms;

namespace Remnant2UnlockerApp.Views;

public partial class SetupWizardWindow : Window
{
    private readonly SetupWizardViewModel _viewModel;

    public SetupWizardWindow(SetupWizardViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        _viewModel.CloseRequested += (_, completed) =>
        {
            DialogResult = completed;
        };

        Closed += (_, _) => _viewModel.Dispose();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private static ComponentRow? RowOf(object sender) => (sender as FrameworkElement)?.DataContext as ComponentRow;

    private void OpenNexus_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender)?.NexusUrl is { } url)
            SetupWizardViewModel.OpenUrl(url);
    }

    private async void ChooseFile_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row)
            return;

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = row.Title,
            Filter = $"{_viewModel.Loc["Wizard.ArchiveFilter"]} (*.zip;*.rar;*.7z)|*.zip;*.rar;*.7z",
            InitialDirectory = DownloadsFolder()
        };

        if (dialog.ShowDialog(this) == true)
            await _viewModel.InstallPackageAsync(row.Kind, dialog.FileName);
    }

    private async void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row)
            return;

        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = _viewModel.Loc["Wizard.ChooseFolderDescription"],
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
            InitialDirectory = DownloadsFolder()
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
            await _viewModel.InstallPackageAsync(row.Kind, dialog.SelectedPath);
    }

    private void SkipTraits_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SkipTraits();
    }

    private void PackageDrop_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = GetDroppedPath(e) != null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void PackageDrop_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;

        if (RowOf(sender) is { } row && GetDroppedPath(e) is { } path)
            await _viewModel.InstallPackageAsync(row.Kind, path);
    }

    // One archive (.zip/.rar/.7z) or one folder.
    private static string? GetDroppedPath(DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: 1 } paths)
            return null;

        var path = paths[0];

        return Directory.Exists(path)
            || PackageInstaller.SupportedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant())
            ? path
            : null;
    }

    private static string DownloadsFolder()
    {
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        return Directory.Exists(downloads) ? downloads : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
