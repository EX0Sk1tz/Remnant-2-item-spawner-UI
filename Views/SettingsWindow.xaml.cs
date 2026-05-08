using System.Windows;
using System.Windows.Input;
using Remnant2UnlockerApp.ViewModels;

namespace Remnant2UnlockerApp.Views;

public partial class SettingsWindow : Window
{
    private readonly MainViewModel _viewModel;

    public SettingsWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private void ConsoleKeyButton_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_viewModel.IsCapturingConsoleKey)
            return;

        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            _viewModel.IsCapturingConsoleKey = false;
            return;
        }

        _viewModel.SetConsoleKey(key.ToString());
    }

    private void TeleportHotkeyButton_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_viewModel.IsCapturingTeleportHotkey)
            return;

        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            _viewModel.IsCapturingTeleportHotkey = false;
            return;
        }

        _viewModel.SetTeleportHotkey(key.ToString());
    }

    private void TeleportHotkeyButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_viewModel.IsCapturingTeleportHotkey)
            return;

        e.Handled = true;

        var keyName = e.ChangedButton switch
        {
            MouseButton.Left => "LeftMouseButton",
            MouseButton.Right => "RightMouseButton",
            MouseButton.Middle => "MiddleMouseButton",
            MouseButton.XButton1 => "ThumbMouseButton",
            MouseButton.XButton2 => "ThumbMouseButton2",
            _ => ""
        };

        if (string.IsNullOrWhiteSpace(keyName))
            return;

        _viewModel.SetTeleportHotkey(keyName);
    }
}