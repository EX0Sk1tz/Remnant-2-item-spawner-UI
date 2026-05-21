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

    private void DestroyTargetHotkeyButton_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        if (!viewModel.IsCapturingDestroyTargetHotkey)
            return;

        e.Handled = true;

        if (e.Key == System.Windows.Input.Key.Escape)
        {
            viewModel.SetDestroyTargetHotkey("None");
            return;
        }

        var key = e.Key == System.Windows.Input.Key.System
            ? e.SystemKey
            : e.Key;

        viewModel.SetDestroyTargetHotkey(key.ToString());
    }

    private void DestroyTargetHotkeyButton_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        if (!viewModel.IsCapturingDestroyTargetHotkey)
            return;

        e.Handled = true;

        var key = e.ChangedButton switch
        {
            System.Windows.Input.MouseButton.XButton1 => "MouseButton4",
            System.Windows.Input.MouseButton.XButton2 => "MouseButton5",
            System.Windows.Input.MouseButton.Middle => "MiddleMouseButton",
            _ => ""
        };

        if (!string.IsNullOrWhiteSpace(key))
            viewModel.SetDestroyTargetHotkey(key);
    }
}