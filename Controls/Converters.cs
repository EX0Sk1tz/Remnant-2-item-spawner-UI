using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using Binding = System.Windows.Data.Binding;

namespace Remnant2UnlockerApp.Controls;

/// <summary>
/// Upper-cases text and spreads the letters with thin spaces, like the game's menu headers.
/// WPF has no letter-spacing property, so the gaps are real characters (U+2009 / U+200A).
/// ConverterParameter "wide" uses a wider gap.
/// </summary>
public sealed class TrackedConverter : IValueConverter
{
    private const string HairGap = "\u200A";
    private const string ThinGap = "\u2009";

    public static string Track(string? text, bool wide = false)
    {
        var upper = (text ?? "").ToUpper(CultureInfo.CurrentCulture);
        return string.Join(wide ? ThinGap + HairGap : HairGap, upper.ToCharArray());
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => Track(value?.ToString(), parameter as string == "wide");

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>{controls:Caps Some Text} → letter-spaced upper-case label.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class CapsExtension : MarkupExtension
{
    public string Text { get; set; } = "";
    public bool Wide { get; set; }

    public CapsExtension() { }
    public CapsExtension(string text) => Text = text;

    public override object ProvideValue(IServiceProvider serviceProvider)
        => TrackedConverter.Track(Text, Wide);
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is true) ^ Invert ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>true → 1, false → FalseOpacity. Used where a trigger used to dim a disabled control.</summary>
public sealed class BoolToOpacityConverter : IValueConverter
{
    public double FalseOpacity { get; set; } = 0.45;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is false ? FalseOpacity : 1.0;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
