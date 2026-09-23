using System.IO;

namespace Remnant2UnlockerApp.Services;

public static class AppLogService
{
    private const long MaxLogSizeBytes = 1 * 1024 * 1024;

    private static readonly object Lock = new();

    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Remnant2UnlockerApp",
        "logs");

    private static readonly string LogPath = Path.Combine(LogDir, "app.log");

    private static readonly string DiagnosticsLogPath = Path.Combine(LogDir, "diagnostics.txt");

    public static string GetLogPath() => LogPath;

    public static string GetDiagnosticsLogPath() => DiagnosticsLogPath;

    public static void WriteDiagnosticsReport(string content)
    {
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(LogDir);
                File.WriteAllText(DiagnosticsLogPath, content);
            }
        }
        catch
        {
            // Logging must never crash the app it's trying to help debug.
        }
    }

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warn(string message, Exception? exception = null) => Write("WARN", message, exception);

    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(LogDir);
                RollIfNeeded();

                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";

                if (exception != null)
                    line += Environment.NewLine + "    " + exception;

                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never crash the app it's trying to help debug.
        }
    }

    private static void RollIfNeeded()
    {
        if (!File.Exists(LogPath))
            return;

        if (new FileInfo(LogPath).Length < MaxLogSizeBytes)
            return;

        var backupPath = LogPath + ".old";

        File.Move(LogPath, backupPath, overwrite: true);
    }
}
