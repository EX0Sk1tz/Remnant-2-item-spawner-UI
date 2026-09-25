using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace Remnant2UnlockerApp.Services;

/// <summary>
/// Swaps the theme dictionary (Themes/&lt;Name&gt;.xaml) in Application.Resources at runtime.
/// Every theme reference in the views is a DynamicResource, so open windows update live.
/// The choice is kept in %LocalAppData%\Remnant2UnlockerApp\settings.json.
/// </summary>
public static class ThemeManager
{
    public const string Classic = "Classic";
    public const string Remnant = "Remnant";

    public static IReadOnlyList<string> Available { get; } = new[] { Classic, Remnant };

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Remnant2UnlockerApp",
        "settings.json");

    private static Brush? _grain;

    public static string Current { get; private set; } = Classic;

    public static event EventHandler? ThemeChanged;

    static ThemeManager()
    {
        // Windows opened later pick up the title-bar colour of the active theme.
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => ApplyTitleBar((Window)sender)));
    }

    /// <summary>Applies the saved theme; a missing or broken settings file falls back to Classic.</summary>
    public static void RestoreSaved() => Apply(LoadSaved(), persist: false);

    public static void Apply(string name, bool persist = true)
    {
        var theme = Available.FirstOrDefault(t => string.Equals(t, name, StringComparison.OrdinalIgnoreCase)) ?? Classic;
        var resources = System.Windows.Application.Current.Resources;
        var dictionaries = resources.MergedDictionaries;

        var replacement = new ResourceDictionary
        {
            Source = new Uri($"/Remnant2UnlockerApp;component/Themes/{theme}.xaml", UriKind.Relative)
        };

        var index = -1;
        for (var i = 0; i < dictionaries.Count; i++)
        {
            if (dictionaries[i].Source?.OriginalString.Contains("Themes/", StringComparison.OrdinalIgnoreCase) == true)
            {
                index = i;
                break;
            }
        }

        if (index >= 0)
            dictionaries[index] = replacement;
        else
            dictionaries.Add(replacement);

        resources["Grain"] = theme == Remnant ? (_grain ??= CreateGrain()) : Brushes.Transparent;

        Current = theme;

        foreach (Window window in System.Windows.Application.Current.Windows)
            ApplyTitleBar(window);

        if (persist)
            Save(theme);

        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    private static string LoadSaved()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return Classic;

            using var document = JsonDocument.Parse(File.ReadAllText(SettingsPath));

            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("theme", out var value) &&
                value.ValueKind == JsonValueKind.String)
                return value.GetString() ?? Classic;
        }
        catch (Exception ex)
        {
            AppLogService.Error($"Failed to read theme from {SettingsPath}", ex);
        }

        return Classic;
    }

    private static void Save(string theme)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new { theme }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            AppLogService.Error($"Failed to save theme to {SettingsPath}", ex);
        }
    }

    /// <summary>Tiled film-grain texture: faint warm speckles over the dark background.</summary>
    private static Brush CreateGrain()
    {
        const int size = 160;
        var pixels = new byte[size * size * 4];
        var random = new Random(1337);

        for (var i = 0; i < size * size; i++)
        {
            var v = random.Next(256);
            pixels[i * 4 + 0] = 0xB8; // B
            pixels[i * 4 + 1] = 0xC8; // G
            pixels[i * 4 + 2] = 0xD8; // R
            pixels[i * 4 + 3] = (byte)(v > 128 ? (v - 128) / 11 : 0);
        }

        var bitmap = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4);
        bitmap.Freeze();

        var brush = new ImageBrush(bitmap)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, size, size),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None
        };
        brush.Freeze();
        return brush;
    }

    // ── Dark title bar for the Remnant theme (standard window frame is kept) ──

    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmUseImmersiveDarkModeBefore20H1 = 19;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    private static void ApplyTitleBar(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        var dark = Current == Remnant ? 1 : 0;

        try
        {
            if (DwmSetWindowAttribute(hwnd, DwmUseImmersiveDarkMode, ref dark, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, DwmUseImmersiveDarkModeBefore20H1, ref dark, sizeof(int));

            // SWP_NOSIZE | SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED: repaint the caption.
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Pre-Windows 10 DWM: keep the default caption.
        }
    }
}
