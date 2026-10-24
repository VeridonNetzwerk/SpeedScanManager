using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Generates simple tray icons at runtime (no external .ico files needed).
/// </summary>
internal static class TrayIcons
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>
    /// Creates an Icon from a Bitmap, cloning it so the HICON can be safely destroyed.
    /// </summary>
    private static Icon IconFromBitmap(Bitmap bmp)
    {
        IntPtr hIcon = bmp.GetHicon();
        try
        {
            var tmp = Icon.FromHandle(hIcon);
            return (Icon)tmp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    /// <summary>
    /// Size the tray shows its icons at (16 px at 100 %, 20/24/32 px at higher scaling).
    /// The icons are rendered at exactly this size so the shell doesn't rescale them (crisp).
    /// </summary>
    private static int TraySize => Math.Max(16, SystemInformation.SmallIconSize.Width);

    /// <summary>
    /// Draws on a transparent bitmap of <see cref="TraySize"/> through a 32x32 design grid.
    /// </summary>
    private static Icon CreateTrayIcon(Action<Graphics> draw)
    {
        int size = TraySize;
        using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);
        g.ScaleTransform(size / 32f, size / 32f);
        draw(g);
        return IconFromBitmap(bmp);
    }

    /// <summary>
    /// Normal "connected" icon: the app logo with a green status dot.
    /// </summary>
    public static Icon CreateConnectedIcon() => CreateTrayIcon(g =>
    {
        DrawLogoCentered(g, 30);

        // Status LED (green dot) in bottom-right corner
        using var ledBrush = new SolidBrush(Color.FromArgb(35, 165, 90));
        g.FillEllipse(ledBrush, 20.5f, 20.5f, 10f, 10f);
        using var ledPen = new Pen(Color.White, 2f);
        g.DrawEllipse(ledPen, 20.5f, 20.5f, 10f, 10f);
    });

    /// <summary>
    /// "Scanning" icon: the app logo with an orange status dot (scan / post-scan flow running).
    /// </summary>
    public static Icon CreateScanningIcon() => CreateTrayIcon(g =>
    {
        DrawLogoCentered(g, 30);

        using var ledBrush = new SolidBrush(Color.FromArgb(245, 150, 30));
        g.FillEllipse(ledBrush, 19.5f, 19.5f, 11.5f, 11.5f);
        using var ledPen = new Pen(Color.White, 2f);
        g.DrawEllipse(ledPen, 19.5f, 19.5f, 11.5f, 11.5f);
    });

    /// <summary>
    /// "Disconnected" icon: the app logo dimmed with a red slash overlay.
    /// </summary>
    public static Icon CreateDisconnectedIcon() => CreateTrayIcon(g =>
    {
        // Draw the logo dimmed (gray, slightly transparent: visible on light and dark taskbars)
        DrawLogoCentered(g, 30, dimmed: true);

        // Red diagonal slash (the "no signal" overlay)
        using var slashPen = new Pen(Color.FromArgb(218, 55, 60), 3.5f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        g.DrawLine(slashPen, 4, 28, 28, 4);
    });

    /// <summary>
    /// Draws the app logo centered in the bitmap at the given max size.
    /// </summary>
    private static void DrawLogoCentered(Graphics g, int maxSize, bool dimmed = false)
    {
        try
        {
            var logo = AppResources.Logo;
            int w = logo.Width;
            int h = logo.Height;
            if (w > h)
            {
                h = (int)(h * (maxSize / (double)w));
                w = maxSize;
            }
            else
            {
                w = (int)(w * (maxSize / (double)h));
                h = maxSize;
            }
            int x = (32 - w) / 2;
            int y = (32 - h) / 2;
            if (!dimmed)
            {
                g.DrawImage(logo, new Rectangle(x, y, w, h));
            }
            else
            {
                using var ia = new System.Drawing.Imaging.ImageAttributes();
                ia.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix(new[]
                {
                    new[] { 0.3f, 0.3f, 0.3f, 0f, 0f },
                    new[] { 0.59f, 0.59f, 0.59f, 0f, 0f },
                    new[] { 0.11f, 0.11f, 0.11f, 0f, 0f },
                    new[] { 0f, 0f, 0f, 0.85f, 0f },
                    new[] { 0.25f, 0.25f, 0.25f, 0f, 1f }
                }));
                g.DrawImage(logo, new Rectangle(x, y, w, h), 0, 0, logo.Width, logo.Height, GraphicsUnit.Pixel, ia);
            }
        }
        catch
        {
            // Fallback: draw simple scanner silhouette if logo unavailable
            var bodyRect = new Rectangle(6, 10, 20, 14);
            using var brush = new SolidBrush(Color.FromArgb(45, 90, 170));
            FillRoundedRect(g, brush, bodyRect, 3);
        }
    }

    private static void FillRoundedRect(Graphics g, Brush brush, Rectangle rect, int radius)
    {
        var path = GetRoundedRectPath(rect, radius);
        g.FillPath(brush, path);
    }

    private static readonly object AppIconSync = new();
    private static Icon? _appIcon;

    /// <summary>
    /// The app logo as Form.Icon (title bar). Created once and shared by all forms —
    /// do not dispose it.
    /// </summary>
    public static Icon GetAppIcon()
    {
        lock (AppIconSync)
            return _appIcon ??= CreateAppIcon();
    }

    private static Icon CreateAppIcon()
    {
        try
        {
            var logo = AppResources.Logo;
            using var bmp = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            int w = logo.Width, h = logo.Height;
            if (w > h) { h = (int)(h * (28.0 / w)); w = 28; }
            else { w = (int)(w * (28.0 / h)); h = 28; }
            g.DrawImage(logo, new Rectangle((32 - w) / 2, (32 - h) / 2, w, h));
            return IconFromBitmap(bmp);
        }
        catch
        {
            return SystemIcons.Application;
        }
    }

    private static GraphicsPath GetRoundedRectPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
