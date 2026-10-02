using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Remnant2UnlockerApp.Services;
using Xunit;

namespace Remnant2UnlockerApp.Tests;

public class LocalizationTests
{
    private static Dictionary<string, string> Load(string code) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(RepoPaths.Root, "Localization", $"{code}.json")))!;

    [Fact]
    public void EnglishAndGerman_HaveTheSameKeys()
    {
        var en = Load("en").Keys.ToHashSet();
        var de = Load("de").Keys.ToHashSet();

        Assert.Empty(en.Except(de));
        Assert.Empty(de.Except(en));
    }

    // Loc["Key"] in C# and Loc[Key] in XAML bindings.
    [Fact]
    public void EveryKeyUsedInCode_Exists()
    {
        var en = Load("en");
        var pattern = new Regex(@"Loc\[""?(?<key>[A-Za-z]+\.[A-Za-z.]+)""?\]");
        var missing = new List<string>();

        foreach (var file in Directory.EnumerateFiles(RepoPaths.Root, "*.*", SearchOption.AllDirectories)
                     .Where(f => f.EndsWith(".cs") || f.EndsWith(".xaml"))
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                                 && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                                 && !f.Contains($"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}")))
        {
            foreach (Match match in pattern.Matches(File.ReadAllText(file)))
            {
                if (!en.ContainsKey(match.Groups["key"].Value))
                    missing.Add($"{Path.GetFileName(file)}: {match.Groups["key"].Value}");
            }
        }

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryPackageError_HasAText()
    {
        var en = Load("en");

        Assert.All(Enum.GetNames<PackageError>(), name => Assert.True(en.ContainsKey($"Wizard.PackageError.{name}"), name));
    }
}
