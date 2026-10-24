using System.Drawing;
using System.Drawing.Drawing2D;

namespace SpeedScanManager;

/// <summary>
/// Theme support for the colorful generated icons (combo box items, flags, Quick-Menü):
/// outline colors that stay visible on dark surfaces, and in-place re-rendering of every
/// icon bitmap when the theme changes (callers keep their cached Bitmap instances).
/// </summary>
internal static class IconInk
{
    private static readonly List<(WeakReference<Bitmap> Bitmap, Action<Graphics> Draw)> Live = new();
    private static bool _hooked;

    /// <summary>Black outlines in light mode, a light gray in dark mode.</summary>
    public static Color Outline => ThemeManager.IsDark ? Color.FromArgb(160, 166, 176) : Color.Black;

    /// <summary>
    /// <paramref name="light"/> in light mode; in dark mode a lighter variant (same hue) so
    /// that dark outline colors keep enough contrast to dark backgrounds.
    /// </summary>
    public static Color Adapt(Color light, float minLuminance = 0.55f)
    {
        if (!ThemeManager.IsDark) return light;
        double lum = (0.299 * light.R + 0.587 * light.G + 0.114 * light.B) / 255.0;
        if (lum >= minLuminance) return light;
        // Blend towards white until the luminance target is reached
        float amount = (float)((minLuminance - lum) / Math.Max(0.01, 1 - lum));
        return Color.FromArgb(light.A, ThemePalette.Blend(light, Color.White, amount));
    }

    /// <summary>Registers <paramref name="bitmap"/> to be redrawn with <paramref name="draw"/> after theme changes.</summary>
    public static Bitmap Track(Bitmap bitmap, Action<Graphics> draw)
    {
        lock (Live)
        {
            if (!_hooked)
            {
                _hooked = true;
                ThemeManager.ThemeChanged += (_, _) => RedrawAll();
            }
            if (Live.Count >= 256)
                Live.RemoveAll(x => !x.Bitmap.TryGetTarget(out _));
            Live.Add((new WeakReference<Bitmap>(bitmap), draw));
        }
        return bitmap;
    }

    /// <summary>Creates a transparent icon bitmap, draws it and keeps it theme-aware.</summary>
    public static Bitmap Create(int width, int height, Action<Graphics> draw)
    {
        var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        Render(bmp, draw);
        return Track(bmp, draw);
    }

    private static void Render(Bitmap bmp, Action<Graphics> draw)
    {
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);
        draw(g);
    }

    private static void RedrawAll()
    {
        List<(WeakReference<Bitmap> Bitmap, Action<Graphics> Draw)> items;
        lock (Live)
        {
            Live.RemoveAll(x => !x.Bitmap.TryGetTarget(out _));
            items = Live.ToList();
        }
        foreach (var (weak, draw) in items)
        {
            if (!weak.TryGetTarget(out var bmp)) continue;
            try
            {
                Render(bmp, draw);
            }
            catch (ArgumentException)
            {
                // Bitmap was disposed by its owner
                lock (Live) Live.RemoveAll(x => ReferenceEquals(x.Bitmap, weak));
            }
            catch (InvalidOperationException)
            {
                // Bitmap is in use elsewhere right now; it keeps the previous rendering
            }
        }
    }
}
