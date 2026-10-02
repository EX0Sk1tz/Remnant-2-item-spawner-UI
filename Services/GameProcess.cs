using System.Diagnostics;

namespace Remnant2UnlockerApp.Services;

public static class GameProcess
{
    /// <summary>Steam/Epic and Game Pass process names.</summary>
    public static readonly IReadOnlyList<string> ProcessNames = new[] { "Remnant2-Win64-Shipping", "Remnant2-WinGDK-Shipping" };

    public static bool IsRunning() => ProcessNames.Any(name =>
    {
        var processes = Process.GetProcessesByName(name);

        foreach (var process in processes)
            process.Dispose();

        return processes.Length > 0;
    });
}
