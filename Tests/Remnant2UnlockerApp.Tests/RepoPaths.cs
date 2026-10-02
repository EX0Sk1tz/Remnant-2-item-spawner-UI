using System.IO;

namespace Remnant2UnlockerApp.Tests;

// Locates the repo's own files by walking up from the test output folder to the app's csproj.
internal static class RepoPaths
{
    public static string Root { get; } = FindRoot();

    public static string ModSource => Path.Combine(Root, "Lua Scripts inside game directory");

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Remnant2UnlockerApp.csproj")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new DirectoryNotFoundException("Repo root (Remnant2UnlockerApp.csproj) not found.");
    }
}
