using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace Remnant2UnlockerApp.Services;

public enum ToastType
{
    Info,
    Success,
    Warning,
    Error
}

public sealed class ToastMessage
{
    public required string Title { get; init; }
    public required string Message { get; init; }
    public ToastType Type { get; init; } = ToastType.Info;
    public string? ActionText { get; init; }
    public Action? OnAction { get; init; }
}

public static class ToastService
{
    private const int MaxVisible = 4;

    public static ObservableCollection<ToastMessage> Toasts { get; } = new();

    public static void Show(
        string title,
        string message,
        ToastType type = ToastType.Info,
        int durationMs = 3000,
        string? actionText = null,
        Action? onAction = null)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;

        if (dispatcher == null)
            return;

        dispatcher.Invoke(() =>
        {
            var toast = new ToastMessage
            {
                Title = title,
                Message = message,
                Type = type,
                ActionText = actionText,
                OnAction = onAction
            };

            Toasts.Add(toast);

            while (Toasts.Count > MaxVisible)
                Toasts.RemoveAt(0);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };

            timer.Tick += (_, _) =>
            {
                timer.Stop();
                Toasts.Remove(toast);
            };

            timer.Start();
        });
    }

    public static void Dismiss(ToastMessage toast)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;

        dispatcher?.Invoke(() => Toasts.Remove(toast));
    }
}
