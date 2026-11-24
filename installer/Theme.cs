using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;

namespace SpeedScanInstaller;

/// <summary>
/// Light/dark colors of the setup, the same palette as the app (Discord-inspired, see
/// UI/Theming/ThemePalette.cs). The brushes live in the application resources and are
/// used with DynamicResource, so switching updates all open windows at once.
/// </summary>
internal static class Theme
{
    public static bool IsDark { get; private set; }

    /// <summary>Raised after the colors changed (e.g. to re-render the README).</summary>
    public static event Action? Changed;

    private static readonly Dictionary<string, (uint Light, uint Dark)> Colors = new()
    {
        ["WindowBrush"] = (0xFFFFFF, 0x313338),
        ["HeaderBrush"] = (0xFFFFFF, 0x1E1F22),
        ["SidebarBrush"] = (0xF2F3F5, 0x2B2D31),
        ["ContentBrush"] = (0xFFFFFF, 0x313338),
        ["FooterBrush"] = (0xF2F3F5, 0x2B2D31),
        ["BorderBrush"] = (0xE3E5E8, 0x3F4147),
        ["CardBrush"] = (0xF7F8F9, 0x2B2D31),
        ["CardBorderBrush"] = (0xE3E5E8, 0x3A3C42),
        ["HoverBrush"] = (0xE8E9EC, 0x3A3C42),
        ["SelectedBrush"] = (0xDCDEE2, 0x404249),
        ["InputBrush"] = (0xEBEDEF, 0x1E1F22),
        ["InputBorderBrush"] = (0xD4D7DC, 0x1E1F22),
        ["TextBrush"] = (0x313338, 0xDBDEE1),
        ["TextSecondaryBrush"] = (0x4E5058, 0xB5BAC1),
        ["TextMutedBrush"] = (0x6D6F78, 0x949BA4),
        ["AccentBrush"] = (0x5865F2, 0x5865F2),
        ["AccentHoverBrush"] = (0x4752C4, 0x4752C4),
        ["AccentPressedBrush"] = (0x3C45A5, 0x3C45A5),
        ["LinkBrush"] = (0x4752C4, 0x8F98FF),
        ["DangerBrush"] = (0xDA373C, 0xDA373C),
        ["DangerHoverBrush"] = (0xA12828, 0xA12828),
        ["ButtonSecondaryBrush"] = (0xE3E5E8, 0x4E5058),
        ["ButtonSecondaryHoverBrush"] = (0xD4D7DC, 0x6D6F78),
        ["ButtonSecondaryPressedBrush"] = (0xC4C9CE, 0x80848E),
        ["ButtonSecondaryTextBrush"] = (0x313338, 0xFFFFFF),
        ["TrackBrush"] = (0xE3E5E8, 0x1E1F22),
    };

    /// <summary>Follows the Windows app theme ("Apps" light/dark).</summary>
    public static void ApplySystem() => Apply(IsSystemDark());

    public static void Toggle() => Apply(!IsDark);

    public static void Apply(bool dark)
    {
        IsDark = dark;
        var res = Application.Current.Resources;
        foreach (var (key, value) in Colors)
        {
            var brush = new SolidColorBrush(ToColor(dark ? value.Dark : value.Light));
            brush.Freeze();
            res[key] = brush;
        }
        var overlay = new SolidColorBrush(dark ? Color.FromArgb(0xB0, 0x11, 0x12, 0x14) : Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF));
        overlay.Freeze();
        res["OverlayBrush"] = overlay;
        res["ThemeToggleTip"] = dark ? I18n.T("Helles Design") : I18n.T("Dunkles Design");
        Changed?.Invoke();
    }

    /// <summary>Color as "#rrggbb" for the README HTML.</summary>
    public static string Css(string key)
    {
        var (light, dark) = Colors[key];
        return "#" + (IsDark ? dark : light).ToString("x6");
    }

    /// <summary>Rounded corners for the borderless windows (Windows 11; no effect on Windows 10).</summary>
    public static void RoundCorners(Window window)
    {
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            int round = 2; // DWMWCP_ROUND
            DwmSetWindowAttribute(hwnd, 33 /* DWMWA_WINDOW_CORNER_PREFERENCE */, ref round, sizeof(int));
        }
        catch
        {
            // Older Windows: square corners
        }
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private static Color ToColor(uint rgb) =>
        Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    private static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch
        {
            return false;
        }
    }
}
