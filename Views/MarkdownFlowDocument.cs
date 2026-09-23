using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfFontFamily = System.Windows.Media.FontFamily;

namespace Remnant2UnlockerApp.Views;

/// <summary>
/// Turns the small subset of Markdown used in this project's CHANGELOG_*.md files (headings,
/// **bold**, `code`, and "- " bullet lists) into a WPF FlowDocument, so release notes pulled from
/// a GitHub release body render instead of showing raw Markdown syntax. Not a general-purpose
/// Markdown parser -- only handles what our own changelogs actually contain.
/// </summary>
internal static class MarkdownFlowDocument
{
    private static readonly Regex InlineTokenPattern = new(@"(\*\*.+?\*\*|`[^`]+`)", RegexOptions.Compiled);

    private static readonly WpfBrush CodeForeground = new SolidColorBrush(WpfColor.FromRgb(0x93, 0xC5, 0xFD));
    private static readonly WpfFontFamily CodeFontFamily = new("Consolas");

    public static FlowDocument Build(string markdown, WpfBrush foreground)
    {
        var document = new FlowDocument
        {
            // FlowDocument defaults to Times New Roman regardless of the app's font -- it doesn't
            // inherit FontFamily from its container the way a normal TextBlock would.
            FontFamily = new WpfFontFamily("Segoe UI"),
            Foreground = foreground,
            FontSize = 13,
            PagePadding = new Thickness(0),
            Background = WpfBrushes.Transparent
        };

        List? currentList = null;
        var isFirstBlock = true;

        foreach (var rawLine in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.TrimEnd();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line.StartsWith("### "))
            {
                currentList = null;
                document.Blocks.Add(CreateHeading(line[4..], 14, isFirstBlock));
            }
            else if (line.StartsWith("## "))
            {
                currentList = null;
                document.Blocks.Add(CreateHeading(line[3..], 16, isFirstBlock));
            }
            else if (line.StartsWith("# "))
            {
                currentList = null;
                document.Blocks.Add(CreateHeading(line[2..], 19, isFirstBlock));
            }
            else if (line.TrimStart() is var trimmed && (trimmed.StartsWith("- ") || trimmed.StartsWith("* ")))
            {
                if (currentList == null)
                {
                    currentList = new List
                    {
                        MarkerStyle = TextMarkerStyle.Disc,
                        Margin = new Thickness(0, isFirstBlock ? 0 : 2, 0, 10)
                    };

                    document.Blocks.Add(currentList);
                }

                var itemParagraph = new Paragraph { Margin = new Thickness(0) };
                itemParagraph.Inlines.AddRange(ParseInline(trimmed[2..]));
                currentList.ListItems.Add(new ListItem(itemParagraph));
            }
            else
            {
                currentList = null;

                var paragraph = new Paragraph { Margin = new Thickness(0, isFirstBlock ? 0 : 4, 0, 10) };
                paragraph.Inlines.AddRange(ParseInline(line));
                document.Blocks.Add(paragraph);
            }

            isFirstBlock = false;
        }

        if (document.Blocks.Count == 0)
            document.Blocks.Add(new Paragraph(new Run(markdown)));

        return document;
    }

    private static Paragraph CreateHeading(string text, double fontSize, bool isFirstBlock)
    {
        var paragraph = new Paragraph
        {
            FontSize = fontSize,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, isFirstBlock ? 0 : 12, 0, 6)
        };

        paragraph.Inlines.AddRange(ParseInline(text));
        return paragraph;
    }

    private static IEnumerable<Inline> ParseInline(string text)
    {
        foreach (var token in InlineTokenPattern.Split(text))
        {
            if (token.Length == 0)
                continue;

            if (token.StartsWith("**") && token.EndsWith("**") && token.Length >= 4)
            {
                yield return new Run(token[2..^2]) { FontWeight = FontWeights.Bold };
                continue;
            }

            if (token.StartsWith('`') && token.EndsWith('`') && token.Length >= 2)
            {
                yield return new Run(token[1..^1])
                {
                    FontFamily = CodeFontFamily,
                    Foreground = CodeForeground
                };
                continue;
            }

            yield return new Run(token);
        }
    }
}
