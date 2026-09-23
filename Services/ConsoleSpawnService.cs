using System.Diagnostics;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;
using Remnant2UnlockerApp.Models;

namespace Remnant2UnlockerApp.Services;

public sealed class ConsoleSpawnService
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    public async Task SpawnViaConsoleAsync(RemnantItem item, string consoleKey)
    {
        var process = Process
            .GetProcessesByName("Remnant2-Win64-Shipping")
            .FirstOrDefault()
            ?? Process
                .GetProcessesByName("Remnant2-WinGDK-Shipping")
                .FirstOrDefault();

        if (process == null)
            throw new InvalidOperationException("Remnant 2 is not running.");

        var command = item.SummonCommand;
        var sendKey = ToSendKeys(consoleKey);

        Forms.IDataObject? previousClipboard = null;

        try
        {
            previousClipboard = Forms.Clipboard.GetDataObject();
        }
        catch (Exception ex)
        {
            // Clipboard can throw if another process holds it open; not worth failing the spawn over.
            AppLogService.Warn("Could not read clipboard before force console spawn", ex);
        }

        SetForegroundWindow(process.MainWindowHandle);

        await Task.Delay(200);

        Forms.SendKeys.SendWait(sendKey);
        await Task.Delay(20);

        Forms.Clipboard.SetText(command);
        await Task.Delay(20);

        Forms.SendKeys.SendWait("^v");
        await Task.Delay(20);

        Forms.SendKeys.SendWait("{ENTER}");

        try
        {
            if (previousClipboard != null)
                Forms.Clipboard.SetDataObject(previousClipboard, true);
            else
                Forms.Clipboard.Clear();
        }
        catch (Exception ex)
        {
            // Best-effort restore; not worth failing the spawn over.
            AppLogService.Warn("Could not restore clipboard after force console spawn", ex);
        }
    }

    private static string ToSendKeys(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return "{F10}";

        var value = key.Trim();

        if (value.StartsWith("F", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(value[1..], out var fNumber)
            && fNumber >= 1
            && fNumber <= 24)
        {
            return "{" + value.ToUpperInvariant() + "}";
        }

        return value switch
        {
            "Oem3" => "{`}",
            "Oem5" => "{\\}",
            "Escape" => "{ESC}",
            "Enter" => "{ENTER}",
            "Space" => " ",
            "Tab" => "{TAB}",
            _ => "{" + value.ToUpperInvariant() + "}"
        };
    }
}