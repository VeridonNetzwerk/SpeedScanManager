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
    /// Normal "connected" icon: the app logo with a green status dot.
    /// </summary>
    public static Icon CreateConnectedIcon()
    {
        using var bmp = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // Draw the logo scaled to fit 32x32
        DrawLogoCentered(g, 28);

        // Status LED (green dot) in bottom-right corner
        using var ledBrush = new SolidBrush(Color.FromArgb(80, 200, 80));
        g.FillEllipse(ledBrush, 23, 23, 7, 7);
        using var ledPen = new Pen(Color.White, 1.5f);
        g.DrawEllipse(ledPen, 23, 23, 7, 7);

        return IconFromBitmap(bmp);
    }

    /// <summary>
    /// "Disconnected" icon: the app logo dimmed with a red slash overlay.
    /// </summary>
    public static Icon CreateDisconnectedIcon()
    {
        using var bmp = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // Draw the logo dimmed
        DrawLogoCentered(g, 28);
        // Apply dimming overlay
        using (var dimBrush = new SolidBrush(Color.FromArgb(100, 200, 200, 200)))
        {
            g.FillRectangle(dimBrush, 0, 0, 32, 32);
        }

        // Red diagonal slash (the "no signal" overlay)
        using var slashPen = new Pen(Color.FromArgb(220, 40, 40), 3f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        g.DrawLine(slashPen, 4, 28, 28, 4);

        return IconFromBitmap(bmp);
    }

    /// <summary>
    /// Draws the app logo centered in the bitmap at the given max size.
    /// </summary>
    private static void DrawLogoCentered(Graphics g, int maxSize)
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
            g.DrawImage(logo, new Rectangle(x, y, w, h));
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

    private static void DrawRoundedRect(Graphics g, Pen pen, Rectangle rect, int radius)
    {
        var path = GetRoundedRectPath(rect, radius);
        g.DrawPath(pen, path);
    }

    /// <summary>
    /// Creates an Icon from the app logo PNG for use as Form.Icon (title bar).
    /// </summary>
    public static Icon GetAppIcon()
    {
        try
        {
            var logo = AppResources.Logo;
            using var bmp = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
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
