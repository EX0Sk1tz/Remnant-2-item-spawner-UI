using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using Remnant2UnlockerApp.Models;
using Remnant2UnlockerApp.Services;

namespace Remnant2UnlockerApp.ViewModels;

public sealed class SpawnProgressViewModel : INotifyPropertyChanged
{
    private readonly BridgeStatusService _statusService;
    private readonly QueueWriter _queueWriter;
    private readonly DispatcherTimer _timer;

    private BridgeStatus _status = new();
    private string _title = "Safe Group Spawn";
    private string? _lastLoggedMessage;

    public SpawnProgressViewModel(
        BridgeStatusService statusService,
        QueueWriter queueWriter,
        string title)
    {
        _statusService = statusService;
        _queueWriter = queueWriter;
        _title = title;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };

        _timer.Tick += (_, _) => Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            OnPropertyChanged();
        }
    }

    public BridgeStatus Status
    {
        get => _status;
        private set
        {
            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Message));
            OnPropertyChanged(nameof(ProgressValue));
            OnPropertyChanged(nameof(ProgressText));
            OnPropertyChanged(nameof(EstimatedTimeText));
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(ErrorText));
            OnPropertyChanged(nameof(HasError));
        }
    }

    public string Message => string.IsNullOrWhiteSpace(Status.LastMessage)
        ? "Waiting for bridge status..."
        : Status.LastMessage;

    public double ProgressValue => Status.Progress;

    public string ProgressText => Status.ProgressText;

    public string EstimatedTimeText
    {
        get
        {
            if (Status.TotalCount <= 0)
                return "Estimated time: waiting...";

            if (Status.ProcessedCount >= Status.TotalCount)
                return "Estimated time: done";

            var remainingItems = Status.TotalCount - Status.ProcessedCount;

            // Current group spawn setup uses one item every 500 ms.
            var estimatedSeconds = Math.Ceiling(remainingItems * 0.5);

            return $"Estimated time left: {FormatDuration(TimeSpan.FromSeconds(estimatedSeconds))}";
        }
    }

    public bool IsBusy => Status.Busy;

    public bool HasError => !string.IsNullOrWhiteSpace(Status.Error);

    public string ErrorText => Status.Error ?? "";

    public void Start()
    {
        Refresh();
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
    }

    public async Task CancelAsync()
    {
        AppLogService.Info($"{Title}: cancel requested");

        await _queueWriter.CancelCurrentActionAsync();
        Refresh();
    }

    private void Refresh()
    {
        Status = _statusService.Read();

        if (Status.LastMessage == _lastLoggedMessage)
            return;

        _lastLoggedMessage = Status.LastMessage;

        if (HasError)
            AppLogService.Warn($"{Title}: {Status.LastMessage} (bridge error: {Status.Error})");
        else
            AppLogService.Info($"{Title}: {Status.LastMessage}");
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalSeconds < 1)
            return "less than 1 sec";

        if (duration.TotalMinutes < 1)
            return $"{duration.Seconds} sec";

        if (duration.TotalHours < 1)
            return $"{duration.Minutes} min {duration.Seconds} sec";

        return $"{(int)duration.TotalHours} h {duration.Minutes} min";
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}