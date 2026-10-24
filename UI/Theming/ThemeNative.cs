using System.Drawing;
using System.Runtime.InteropServices;

namespace SpeedScanManager;

/// <summary>
/// Native helpers for the theme: dark title bars and rounded corners (DWM), dark common
/// controls/scroll bars (uxtheme). Every call fails silently on Windows versions that don't
/// support it.
/// </summary>
internal static class ThemeNative
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;
    private const uint DWMWA_COLOR_DEFAULT = 0xFFFFFFFF;

    public const int DWMWCP_DEFAULT = 0;
    public const int DWMWCP_DONOTROUND = 1;
    public const int DWMWCP_ROUND = 2;
    public const int DWMWCP_ROUNDSMALL = 3;

    private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004,
        SWP_NOACTIVATE = 0x0010, SWP_FRAMECHANGED = 0x0020;

    internal const int RDW_INVALIDATE = 0x0001, RDW_FRAME = 0x0400, RDW_UPDATENOW = 0x0100;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref uint value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? appName, string? idList);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    internal static extern bool RedrawWindow(IntPtr hWnd, IntPtr rect, IntPtr region, int flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryW(string name);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetProcAddress(IntPtr module, IntPtr ordinal);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int SetPreferredAppModeFn(int mode);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void FlushMenuThemesFn();

    /// <summary>Windows 11 (build 22000+): DWM rounded corners and caption colors.</summary>
    public static bool IsWindows11 { get; } = Environment.OSVersion.Version.Build >= 22000;

    private static readonly bool SupportsDarkMode = Environment.OSVersion.Version.Build >= 17763;

    private static uint ToColorRef(Color c) => (uint)(c.R | (c.G << 8) | (c.B << 16));

    /// <summary>Dark or light title bar (and caption color on Windows 11).</summary>
    public static void ApplyTitleBar(IntPtr hwnd, ThemePalette palette)
    {
        if (hwnd == IntPtr.Zero || !SupportsDarkMode) return;
        try
        {
            int dark = palette.IsDark ? 1 : 0;
            if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref dark, sizeof(int));

            if (IsWindows11)
            {
                int corner = DWMWCP_ROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
                uint caption = palette.IsDark ? ToColorRef(palette.Header) : DWMWA_COLOR_DEFAULT;
                uint text = palette.IsDark ? ToColorRef(palette.Text) : DWMWA_COLOR_DEFAULT;
                uint border = palette.IsDark ? ToColorRef(palette.Tertiary) : DWMWA_COLOR_DEFAULT;
                DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref caption, sizeof(uint));
                DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref text, sizeof(uint));
                DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref border, sizeof(uint));
            }

            // Make the non-client area pick up the change while the window is visible.
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }
        catch
        {
            // DWM not available
        }
    }

    /// <summary>Rounded corners for popup windows (context menus, dropdowns) on Windows 11.</summary>
    public static void ApplyPopupCorners(IntPtr hwnd, ThemePalette palette)
    {
        if (hwnd == IntPtr.Zero || !IsWindows11) return;
        try
        {
            int corner = DWMWCP_ROUNDSMALL;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
            uint border = ToColorRef(palette.IsDark ? palette.Tertiary : palette.Border);
            DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref border, sizeof(uint));
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>Sets the visual style class of a window ("DarkMode_Explorer", "DarkMode_CFD", "Explorer", "" = classic, null = default).</summary>
    public static void SetTheme(IntPtr hwnd, string? appName)
    {
        if (hwnd == IntPtr.Zero) return;
        try
        {
            SetWindowTheme(hwnd, appName, appName == "" ? "" : null);
        }
        catch
        {
            // uxtheme not available
        }
    }

    /// <summary>Dark scroll bars etc. for scrollable controls in dark mode, the default theme otherwise.</summary>
    public static void ApplyExplorerTheme(IntPtr hwnd, bool dark)
    {
        if (!SupportsDarkMode && dark) return;
        SetTheme(hwnd, dark ? "DarkMode_Explorer" : "Explorer");
    }

    private static bool _appModeInitialized;
    private static SetPreferredAppModeFn? _setPreferredAppMode;
    private static FlushMenuThemesFn? _flushMenuThemes;

    /// <summary>
    /// Allows dark mode for native menus/scroll bars of this process (uxtheme ordinals 135/136,
    /// Windows 10 1903+). Only affects controls that get a DarkMode_* theme.
    /// </summary>
    public static void SetAppDarkMode(bool dark)
    {
        if (Environment.OSVersion.Version.Build < 18362) return;
        try
        {
            if (!_appModeInitialized)
            {
                _appModeInitialized = true;
                var uxtheme = LoadLibraryW("uxtheme.dll");
                if (uxtheme != IntPtr.Zero)
                {
                    var p135 = GetProcAddress(uxtheme, new IntPtr(135));
                    var p136 = GetProcAddress(uxtheme, new IntPtr(136));
                    if (p135 != IntPtr.Zero)
                        _setPreferredAppMode = Marshal.GetDelegateForFunctionPointer<SetPreferredAppModeFn>(p135);
                    if (p136 != IntPtr.Zero)
                        _flushMenuThemes = Marshal.GetDelegateForFunctionPointer<FlushMenuThemesFn>(p136);
                }
            }
            // PreferredAppMode: 2 = ForceDark, 3 = ForceLight
            _setPreferredAppMode?.Invoke(dark ? 2 : 3);
            _flushMenuThemes?.Invoke();
        }
        catch
        {
            // undocumented API not available
        }
    }
}
