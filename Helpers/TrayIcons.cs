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
    /// Normal "connected" icon: a simple scanner silhouette in dark blue.
    /// </summary>
    public static Icon CreateConnectedIcon()
    {
        using var bmp = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        g.Clear(Color.Transparent);

        // Scanner body (rounded rectangle)
        var bodyRect = new Rectangle(6, 10, 20, 14);
        using (var brush = new SolidBrush(Color.FromArgb(45, 90, 170)))
        using (var pen = new Pen(Color.FromArgb(25, 55, 120), 1.5f))
        {
            FillRoundedRect(g, brush, bodyRect, 3);
            DrawRoundedRect(g, pen, bodyRect, 3);
        }

        // Paper feed slot (lighter line)
        using var slotPen = new Pen(Color.FromArgb(180, 200, 230), 1.5f);
        g.DrawLine(slotPen, 9, 14, 23, 14);

        // Status LED (green dot)
        using var ledBrush = new SolidBrush(Color.FromArgb(80, 200, 80));
        g.FillEllipse(ledBrush, 22, 19, 4, 4);

        return IconFromBitmap(bmp);
    }

    /// <summary>
    /// "Disconnected" icon: same scanner silhouette with a red diagonal slash overlay.
    /// </summary>
    public static Icon CreateDisconnectedIcon()
    {
        using var bmp = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        g.Clear(Color.Transparent);

        // Scanner body (dimmed)
        var bodyRect = new Rectangle(6, 10, 20, 14);
        using (var brush = new SolidBrush(Color.FromArgb(90, 110, 150)))
        using (var pen = new Pen(Color.FromArgb(60, 80, 120), 1.5f))
        {
            FillRoundedRect(g, brush, bodyRect, 3);
            DrawRoundedRect(g, pen, bodyRect, 3);
        }

        // Paper feed slot
        using var slotPen = new Pen(Color.FromArgb(160, 170, 190), 1.5f);
        g.DrawLine(slotPen, 9, 14, 23, 14);

        // Red diagonal slash (the "no signal" overlay)
        using var slashPen = new Pen(Color.FromArgb(220, 40, 40), 3f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        g.DrawLine(slashPen, 4, 28, 28, 4);

        return IconFromBitmap(bmp);
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
