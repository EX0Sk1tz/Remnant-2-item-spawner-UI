using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brush = System.Windows.Media.Brush;

namespace Remnant2UnlockerApp.Services;

/// <summary>
/// Runtime parts of the Remnant theme (Themes/Remnant.xaml, merged in App.xaml) that XAML can't
/// express: the film-grain texture ("Grain") and the dark title bar on every window.
/// </summary>
public static class ThemeManager
{
    /// <summary>Call once before the first window is created.</summary>
    public static void Initialize()
    {
        System.Windows.Application.Current.Resources["Grain"] = CreateGrain();

        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => ApplyTitleBar((Window)sender)));
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

    // ── Dark title bar (standard window frame is kept) ──

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

        var dark = 1;

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
