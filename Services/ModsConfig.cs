using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Remnant2UnlockerApp.Services;

/// <summary>
/// Reads and edits UE4SS's mod list ("Mods\mods.txt", one "Name : 0|1" per line; some installs use
/// "Mods\enabled.txt" in the same format). Pure text in, text out: everything we don't need to change
/// (other mods, comments, order, line endings) is kept as it was.
/// </summary>
public static partial class ModsConfig
{
    public const string ModsTxtName = "mods.txt";
    public const string EnabledTxtName = "enabled.txt";

    /// <summary>The mods the bridge needs. Only the ones that are actually installed get enabled.</summary>
    public static readonly IReadOnlyList<string> RequiredMods = new[]
    {
        "AllowModsMod",
        "ConsoleCommandsMod",
        "ConsoleEnablerMod",
        "CheatManagerEnablerMod",
        ModPayload.UnlockerComponent
    };

    [GeneratedRegex(@"^(?<name>\s*[^;:\s][^:]*?)(?<sep>\s*:\s*)(?<value>\d+)(?<rest>\s*)$")]
    private static partial Regex EntryRegex();

    /// <summary>mods.txt if it exists, else enabled.txt if that exists, else mods.txt (to be created).</summary>
    public static string ResolveConfigPath(string modsFolder)
    {
        var modsTxt = Path.Combine(modsFolder, ModsTxtName);

        if (File.Exists(modsTxt))
            return modsTxt;

        var enabledTxt = Path.Combine(modsFolder, EnabledTxtName);

        return File.Exists(enabledTxt) ? enabledTxt : modsTxt;
    }

    public static bool IsEnabled(string? content, string modName) => GetNotEnabled(content, new[] { modName }).Count == 0;

    public static IReadOnlyList<string> GetNotEnabled(string? content, IEnumerable<string> modNames)
    {
        var enabled = SplitLines(content ?? "").Lines
            .Select(line => EntryRegex().Match(line))
            .Where(m => m.Success && m.Groups["value"].Value == "1")
            .Select(m => m.Groups["name"].Value.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return modNames.Where(name => !enabled.Contains(name)).ToList();
    }

    /// <summary>
    /// Makes sure every name in <paramref name="modNames"/> has a "Name : 1" line: existing lines set
    /// to 0 are flipped, missing ones are inserted before UE4SS's built-in Keybinds block (which must
    /// stay last). <paramref name="content"/> null or empty creates a new file's content.
    /// </summary>
    public static ModsConfigEdit EnsureEnabled(string? content, IEnumerable<string> modNames)
    {
        var names = modNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var (lines, newline) = SplitLines(content ?? "");
        var flipped = new List<string>();
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < lines.Count; i++)
        {
            var match = EntryRegex().Match(lines[i]);

            if (!match.Success)
                continue;

            var name = match.Groups["name"].Value.Trim();

            if (!names.Contains(name, StringComparer.OrdinalIgnoreCase))
                continue;

            present.Add(name);

            if (match.Groups["value"].Value == "1")
                continue;

            lines[i] = match.Groups["name"].Value + match.Groups["sep"].Value + "1" + match.Groups["rest"].Value;
            flipped.Add(name);
        }

        var added = names.Where(n => !present.Contains(n)).ToList();

        if (added.Count > 0)
        {
            if (lines.Count == 1 && lines[0].Length == 0)
                lines.Clear();

            var insertAt = FindInsertIndex(lines);
            lines.InsertRange(insertAt, added.Select(n => $"{n} : 1"));

            // A brand-new file ends with a newline, like UE4SS's own.
            if (lines.Count == added.Count)
                lines.Add("");
        }

        return new ModsConfigEdit(string.Join(newline, lines), flipped, added);
    }

    /// <summary>Reads the config file, keeping track of a UTF-8 BOM so a rewrite keeps the file's encoding.</summary>
    public static (string Content, bool HasBom) ReadFile(string path)
    {
        if (!File.Exists(path))
            return ("", false);

        var bytes = File.ReadAllBytes(path);
        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;

        return (Encoding.UTF8.GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0)), hasBom);
    }

    public static byte[] Encode(string content, bool withBom)
    {
        var body = new UTF8Encoding(false).GetBytes(content);

        return withBom ? new byte[] { 0xEF, 0xBB, 0xBF }.Concat(body).ToArray() : body;
    }

    // Before the "Keybinds" entry, and before the comment lines directly above it
    // ("; Built-in keybinds, do not move up!"). Without a Keybinds entry: after the last non-empty line.
    private static int FindInsertIndex(List<string> lines)
    {
        var keybinds = lines.FindIndex(line =>
        {
            var match = EntryRegex().Match(line);
            return match.Success && match.Groups["name"].Value.Trim().Equals("Keybinds", StringComparison.OrdinalIgnoreCase);
        });

        if (keybinds >= 0)
        {
            var index = keybinds;

            while (index > 0 && lines[index - 1].TrimStart().StartsWith(';'))
                index--;

            return index;
        }

        var last = lines.FindLastIndex(line => line.Trim().Length > 0);
        return last + 1;
    }

    private static (List<string> Lines, string Newline) SplitLines(string content)
    {
        var newline = content.Contains("\r\n") ? "\r\n" : content.Contains('\n') ? "\n" : "\r\n";

        return (content.Split(newline).ToList(), newline);
    }
}

public sealed record ModsConfigEdit(string Content, IReadOnlyList<string> Flipped, IReadOnlyList<string> Added)
{
    public bool Changed => Flipped.Count > 0 || Added.Count > 0;
}
