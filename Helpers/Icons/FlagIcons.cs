using System.Drawing;
using System.Drawing.Drawing2D;

namespace SpeedScanManager;

/// <summary>
/// Generates 16x16 simplified flag icons for the OCR language dropdown.
/// </summary>
internal static class FlagIcons
{
    public static Bitmap Globe => DrawIcon(g =>
    {
        g.Clear(Color.FromArgb(220, 235, 252));
        using var pen = new Pen(Color.FromArgb(60, 100, 160), 1f);
        g.DrawEllipse(pen, 2, 2, 12, 12);
        // Equator
        g.DrawLine(pen, 2, 8, 14, 8);
        // Meridian
        g.DrawLine(pen, 8, 2, 8, 14);
        // Simplified continents
        g.FillEllipse(new SolidBrush(Color.FromArgb(100, 160, 100)), 4, 4, 3, 2);
        g.FillEllipse(new SolidBrush(Color.FromArgb(100, 160, 100)), 9, 5, 3, 3);
        g.FillEllipse(new SolidBrush(Color.FromArgb(100, 160, 100)), 5, 10, 3, 2);
    });

    public static Bitmap Germany => DrawIcon(g =>
    {
        g.FillRectangle(new SolidBrush(Color.Black), 1, 2, 14, 4);
        g.FillRectangle(new SolidBrush(Color.FromArgb(220, 0, 0)), 1, 6, 14, 4);
        g.FillRectangle(new SolidBrush(Color.FromArgb(255, 204, 0)), 1, 10, 14, 4);
        DrawBorder(g);
    });

    public static Bitmap Japan => DrawIcon(g =>
    {
        g.Clear(Color.White);
        g.FillEllipse(new SolidBrush(Color.FromArgb(188, 0, 45)), 5, 5, 6, 6);
        DrawBorder(g);
    });

    public static Bitmap UK => DrawIcon(g =>
    {
        g.Clear(Color.FromArgb(1, 33, 102));
        // White cross
        g.FillRectangle(Brushes.White, 1, 7, 14, 2);
        g.FillRectangle(Brushes.White, 7, 2, 2, 12);
        // Red cross
        g.FillRectangle(new SolidBrush(Color.FromArgb(200, 16, 46)), 1, 7, 14, 1);
        g.FillRectangle(new SolidBrush(Color.FromArgb(200, 16, 46)), 7, 2, 1, 12);
        // Diagonal white
        g.DrawLine(new Pen(Color.White, 2f), 1, 2, 15, 14);
        g.DrawLine(new Pen(Color.White, 2f), 15, 2, 1, 14);
        // Diagonal red
        g.DrawLine(new Pen(Color.FromArgb(200, 16, 46), 1f), 1, 2, 15, 14);
        g.DrawLine(new Pen(Color.FromArgb(200, 16, 46), 1f), 15, 2, 1, 14);
        DrawBorder(g);
    });

    public static Bitmap France => DrawIcon(g =>
    {
        g.FillRectangle(new SolidBrush(Color.FromArgb(0, 35, 149)), 1, 2, 5, 12);
        g.FillRectangle(Brushes.White, 6, 2, 5, 12);
        g.FillRectangle(new SolidBrush(Color.FromArgb(237, 41, 57)), 11, 2, 4, 12);
        DrawBorder(g);
    });

    public static Bitmap Italy => DrawIcon(g =>
    {
        g.FillRectangle(new SolidBrush(Color.FromArgb(0, 140, 69)), 1, 2, 5, 12);
        g.FillRectangle(Brushes.White, 6, 2, 5, 12);
        g.FillRectangle(new SolidBrush(Color.FromArgb(205, 33, 42)), 11, 2, 4, 12);
        DrawBorder(g);
    });

    public static Bitmap Spain => DrawIcon(g =>
    {
        g.FillRectangle(new SolidBrush(Color.FromArgb(170, 21, 28)), 1, 2, 14, 3);
        g.FillRectangle(new SolidBrush(Color.FromArgb(241, 191, 0)), 1, 5, 14, 6);
        g.FillRectangle(new SolidBrush(Color.FromArgb(170, 21, 28)), 1, 11, 14, 3);
        DrawBorder(g);
    });

    public static Bitmap ChinaSimplified => DrawIcon(g =>
    {
        g.FillRectangle(new SolidBrush(Color.FromArgb(238, 28, 37)), 1, 2, 14, 12);
        // One large star
        DrawStar(g, 4, 5, 2, Color.Yellow);
        // Four small stars
        DrawStar(g, 8, 4, 1, Color.Yellow);
        DrawStar(g, 10, 6, 1, Color.Yellow);
        DrawStar(g, 10, 9, 1, Color.Yellow);
        DrawStar(g, 8, 11, 1, Color.Yellow);
        DrawBorder(g);
    });

    public static Bitmap ChinaTraditional => DrawIcon(g =>
    {
        // Taiwan flag: red field, blue canton with white sun
        g.FillRectangle(new SolidBrush(Color.FromArgb(206, 17, 38)), 1, 2, 14, 12);
        // Blue canton (top-left quarter)
        g.FillRectangle(new SolidBrush(Color.FromArgb(0, 44, 124)), 1, 2, 7, 6);
        // White sun (simplified circle)
        g.FillEllipse(Brushes.White, 3, 3, 3, 3);
        DrawBorder(g);
    });

    public static Bitmap Korea => DrawIcon(g =>
    {
        g.Clear(Color.White);
        // Taegeuk (simplified: red top, blue bottom)
        g.FillPie(new SolidBrush(Color.FromArgb(205, 46, 58)), 5, 5, 6, 6, 0, 180);
        g.FillPie(new SolidBrush(Color.FromArgb(0, 71, 160)), 5, 5, 6, 6, 180, 180);
        // Trigram lines (simplified)
        using var pen = new Pen(Color.Black, 1f);
        g.DrawLine(pen, 2, 4, 4, 4);
        g.DrawLine(pen, 2, 12, 4, 12);
        g.DrawLine(pen, 12, 4, 14, 4);
        g.DrawLine(pen, 12, 12, 14, 12);
        DrawBorder(g);
    });

    public static Bitmap Russia => DrawIcon(g =>
    {
        g.FillRectangle(Brushes.White, 1, 2, 14, 4);
        g.FillRectangle(new SolidBrush(Color.FromArgb(0, 57, 166)), 1, 6, 14, 4);
        g.FillRectangle(new SolidBrush(Color.FromArgb(213, 43, 30)), 1, 10, 14, 4);
        DrawBorder(g);
    });

    public static Bitmap Portugal => DrawIcon(g =>
    {
        g.FillRectangle(new SolidBrush(Color.FromArgb(0, 101, 49)), 1, 2, 6, 12);
        g.FillRectangle(new SolidBrush(Color.FromArgb(213, 43, 30)), 7, 2, 8, 12);
        // Simplified coat of arms: yellow circle
        g.FillEllipse(new SolidBrush(Color.FromArgb(255, 215, 0)), 3, 6, 4, 4);
        g.DrawEllipse(new Pen(Color.Red, 0.5f), 3, 6, 4, 4);
        DrawBorder(g);
    });

    public static Bitmap Arabic => DrawIcon(g =>
    {
        // Generic Arabic: green with white crescent and star
        g.FillRectangle(new SolidBrush(Color.FromArgb(0, 122, 61)), 1, 2, 14, 12);
        // White crescent
        g.FillEllipse(Brushes.White, 5, 5, 5, 5);
        g.FillEllipse(new SolidBrush(Color.FromArgb(0, 122, 61)), 6, 5, 4, 5);
        // Small star
        DrawStar(g, 10, 6, 1, Color.White);
        DrawBorder(g);
    });

    private static void DrawBorder(Graphics g)
    {
        g.DrawRectangle(new Pen(Color.FromArgb(160, 160, 160)), 1, 2, 14, 12);
    }

    private static void DrawStar(Graphics g, int cx, int cy, int radius, Color color)
    {
        var points = new PointF[10];
        for (int i = 0; i < 10; i++)
        {
            double angle = -Math.PI / 2 + i * Math.PI / 5;
            double r = i % 2 == 0 ? radius : radius * 0.4;
            points[i] = new PointF(
                cx + (float)(r * Math.Cos(angle)),
                cy + (float)(r * Math.Sin(angle)));
        }
        g.FillPolygon(new SolidBrush(color), points);
    }

    private static Bitmap DrawIcon(Action<Graphics> draw)
    {
        var bmp = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        draw(g);
        return bmp;
    }
}
