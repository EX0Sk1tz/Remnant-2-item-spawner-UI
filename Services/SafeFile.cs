using System.IO;

namespace Remnant2UnlockerApp.Services;

/// <summary>
/// File writes for the installer: write to "&lt;path&gt;.tmp" first, then move it over the target, so
/// a crash or a full disk never leaves a half-written script in the game folder.
/// </summary>
public static class SafeFile
{
    public static void WriteAtomic(string path, byte[] content)
    {
        var dir = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var tmp = path + ".tmp";

        try
        {
            File.WriteAllBytes(tmp, content);
            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            TryDelete(tmp);
            throw;
        }
    }

    public static void CopyAtomic(string source, string destination)
    {
        var dir = Path.GetDirectoryName(destination);

        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var tmp = destination + ".tmp";

        try
        {
            File.Copy(source, tmp, overwrite: true);
            File.Move(tmp, destination, overwrite: true);
        }
        catch
        {
            TryDelete(tmp);
            throw;
        }
    }

    public static bool IsAccessDenied(Exception ex) => ex is UnauthorizedAccessException
        || (ex is IOException io && (io.HResult & 0xFFFF) == 5); // ERROR_ACCESS_DENIED

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            AppLogService.Warn($"Could not delete temp file {path}", ex);
        }
    }
}
