using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace Remnant2UnlockerApp.Services;

public static class AppUpdateService
{
    public static async Task DownloadAndApplyUpdateAsync(string downloadUrl)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "Remnant2UnlockerUpdate");
        Directory.CreateDirectory(tempRoot);

        var zipPath = Path.Combine(tempRoot, "update.zip");
        var extractPath = Path.Combine(tempRoot, "extracted");

        if (File.Exists(zipPath))
            File.Delete(zipPath);

        if (Directory.Exists(extractPath))
            Directory.Delete(extractPath, true);

        using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
        using (var response = await client.GetAsync(downloadUrl))
        {
            response.EnsureSuccessStatusCode();

            await using var contentStream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);

            await contentStream.CopyToAsync(fileStream);
        }

        ZipFile.ExtractToDirectory(zipPath, extractPath, overwriteFiles: true);

        // Some archives wrap the payload in a single top-level folder -- unwrap it so the
        // copy below lands the exe and its content files directly in the install directory.
        var sourceDir = extractPath;
        var extractedEntries = Directory.GetFileSystemEntries(extractPath);

        if (extractedEntries.Length == 1 && Directory.Exists(extractedEntries[0]))
            sourceDir = extractedEntries[0];

        var installDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var exePath = Process.GetCurrentProcess().MainModule?.FileName
            ?? Path.Combine(installDir, "Remnant2UnlockerApp.exe");

        var scriptPath = Path.Combine(tempRoot, "apply_update.ps1");

        // The running exe can't overwrite itself, so a detached script waits for this
        // process to exit, copies the new files over the install directory, and relaunches.
        var script =
$@"try {{ Wait-Process -Id {Environment.ProcessId} -ErrorAction SilentlyContinue }} catch {{}}
Start-Sleep -Seconds 1
robocopy '{sourceDir}' '{installDir}' /E /IS /IT /R:3 /W:1 | Out-Null
Start-Process -FilePath '{exePath}'
Remove-Item -LiteralPath '{zipPath}' -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath '{extractPath}' -Recurse -Force -ErrorAction SilentlyContinue
";

        await File.WriteAllTextAsync(scriptPath, script);

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });

        System.Windows.Application.Current.Dispatcher.Invoke(() => System.Windows.Application.Current.Shutdown());
    }
}
