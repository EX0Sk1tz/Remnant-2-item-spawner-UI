namespace Remnant2UnlockerApp.Services;

/// <summary>
/// Command-line switches: "--setup" opens the setup wizard at start (used by the "Restart as
/// administrator" button), "--game-path &lt;Win64 folder&gt;" preselects the game folder in it.
/// </summary>
public static class StartupOptions
{
    public static bool OpenSetup { get; private set; }

    public static string? GamePath { get; private set; }

    public static void Parse(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i].Equals("--setup", StringComparison.OrdinalIgnoreCase))
                OpenSetup = true;
            else if (args[i].Equals("--game-path", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count)
                GamePath = args[++i];
        }
    }
}
