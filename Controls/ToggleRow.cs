using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using Cursors = System.Windows.Input.Cursors;
using Panel = System.Windows.Controls.Panel;

namespace Remnant2UnlockerApp.Controls;

/// <summary>
/// controls:ToggleRow.IsEnabled="True" on a settings row (Border or Panel) makes a click anywhere
/// in the row flip the ToggleButton inside it, not just a click on the switch itself.
/// </summary>
public static class ToggleRow
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(ToggleRow), new PropertyMetadata(false, OnIsEnabledChanged));

    // Only a press that started in the same row counts, so dragging in from elsewhere doesn't toggle.
    private static readonly DependencyProperty IsPressedProperty = DependencyProperty.RegisterAttached(
        "IsPressed", typeof(bool), typeof(ToggleRow), new PropertyMetadata(false));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement row)
            return;

        if ((bool)e.NewValue)
        {
            // A null background isn't hit-testable, so empty space in the row wouldn't get clicks.
            if (row is Panel { Background: null } panel)
                panel.Background = Brushes.Transparent;
            else if (row is Border { Background: null } border)
                border.Background = Brushes.Transparent;

            row.Cursor = Cursors.Hand;
            row.MouseLeftButtonDown += OnMouseLeftButtonDown;
            row.MouseLeftButtonUp += OnMouseLeftButtonUp;
        }
        else
        {
            row.ClearValue(FrameworkElement.CursorProperty);
            row.MouseLeftButtonDown -= OnMouseLeftButtonDown;
            row.MouseLeftButtonUp -= OnMouseLeftButtonUp;
        }
    }

    private static void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ((DependencyObject)sender).SetValue(IsPressedProperty, !IsInsideButton(e.OriginalSource as DependencyObject, (DependencyObject)sender));
    }

    private static void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var row = (DependencyObject)sender;
        var pressed = (bool)row.GetValue(IsPressedProperty);
        row.SetValue(IsPressedProperty, false);

        // The switch itself (or any other button in the row) already handles its own click.
        if (!pressed || IsInsideButton(e.OriginalSource as DependencyObject, row))
            return;

        if (FindToggle(row) is { IsEnabled: true } toggle)
        {
            toggle.IsChecked = toggle.IsChecked != true;
            e.Handled = true;
        }
    }

    private static bool IsInsideButton(DependencyObject? source, DependencyObject row)
    {
        for (var current = source; current != null && current != row; current = GetParent(current))
        {
            if (current is ButtonBase)
                return true;
        }

        return false;
    }

    // Runs inside a TextBlock are ContentElements, which have no visual parent.
    private static DependencyObject? GetParent(DependencyObject element) =>
        element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);

    private static ToggleButton? FindToggle(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is ToggleButton toggle)
                return toggle;

            if (FindToggle(child) is { } nested)
                return nested;
        }

        return null;
    }
}
