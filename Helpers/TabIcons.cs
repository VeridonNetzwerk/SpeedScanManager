using System.Drawing;
using System.Drawing.Drawing2D;

namespace SpeedScanManager;

/// <summary>
/// Generates 16x16 pixel classic Windows-style icons for tab pages and buttons at runtime.
/// All icons use a consistent color palette and transparent backgrounds.
/// </summary>
internal static class TabIcons
{
    private static readonly Color DarkBlue = Color.FromArgb(49, 90, 134);
    private static readonly Color MidBlue = Color.FromArgb(95, 134, 173);
    private static readonly Color LightBlue = Color.FromArgb(184, 211, 234);
    private static readonly Color DarkGray = Color.FromArgb(85, 85, 85);
    private static readonly Color LightGray = Color.FromArgb(232, 232, 232);
    private static readonly Color Green = Color.FromArgb(62, 154, 84);
    private static readonly Color Red = Color.FromArgb(200, 66, 66);
    private static readonly Color Yellow = Color.FromArgb(215, 165, 44);
    private static readonly Color FolderYellow = Color.FromArgb(218, 195, 110);
    private static readonly Color FolderDark = Color.FromArgb(170, 140, 60);

    public static Bitmap CreateAppIcon() => DrawIcon(g =>
    {
        g.FillRectangle(new SolidBrush(LightBlue), 2, 3, 12, 9);
        g.DrawRectangle(new Pen(DarkBlue), 2, 3, 12, 9);
        g.FillRectangle(new SolidBrush(DarkBlue), 2, 3, 12, 2);
        g.DrawLine(new Pen(Color.White), 4, 7, 12, 7);
        g.DrawLine(new Pen(Color.White), 4, 9, 10, 9);
        g.FillRectangle(new SolidBrush(DarkGray), 6, 12, 4, 2);
        g.FillRectangle(new SolidBrush(DarkGray), 4, 14, 8, 1);
    });

    public static Bitmap CreateSaveIcon() => DrawIcon(g =>
    {
        g.FillPolygon(new SolidBrush(FolderDark), new Point[]
        {
            new(2, 5), new(6, 5), new(8, 3), new(14, 3), new(14, 13), new(2, 13)
        });
        g.FillPolygon(new SolidBrush(FolderYellow), new Point[]
        {
            new(2, 6), new(13, 6), new(14, 13), new(2, 13)
        });
        g.DrawPolygon(new Pen(FolderDark), new Point[]
        {
            new(2, 5), new(6, 5), new(8, 3), new(14, 3), new(14, 13), new(2, 13)
        });
        g.DrawLine(new Pen(FolderDark), 2, 6, 13, 6);
        g.FillRectangle(new SolidBrush(Color.White), 5, 3, 5, 4);
        g.DrawRectangle(new Pen(DarkGray), 5, 3, 5, 4);
        g.DrawLine(new Pen(LightGray), 6, 4, 9, 4);
        g.DrawLine(new Pen(LightGray), 6, 5, 8, 5);
    });

    public static Bitmap CreateScanIcon() => DrawIcon(g =>
    {
        g.FillRectangle(new SolidBrush(DarkGray), 2, 4, 12, 8);
        g.DrawRectangle(new Pen(Color.Black), 2, 4, 12, 8);
        g.FillRectangle(new SolidBrush(Color.White), 3, 6, 10, 4);
        g.DrawRectangle(new Pen(DarkGray), 3, 6, 10, 4);
        g.DrawLine(new Pen(Red, 1.5f), 4, 8, 12, 8);
        g.FillRectangle(new SolidBrush(LightGray), 4, 12, 8, 1);
        g.FillRectangle(new SolidBrush(Green), 12, 5, 1, 1);
    });

    public static Bitmap CreateFileIcon() => DrawIcon(g =>
    {
        var docPts = new Point[] { new(3, 2), new(10, 2), new(13, 5), new(13, 14), new(3, 14) };
        g.FillPolygon(new SolidBrush(Color.White), docPts);
        g.DrawPolygon(new Pen(DarkGray), docPts);
        g.FillPolygon(new SolidBrush(LightGray), new Point[] { new(10, 2), new(13, 5), new(10, 5) });
        g.DrawLine(new Pen(DarkGray), 10, 2, 10, 5);
        g.DrawLine(new Pen(DarkGray), 10, 5, 13, 5);
        g.DrawLine(new Pen(MidBlue), 5, 8, 11, 8);
        g.DrawLine(new Pen(MidBlue), 5, 10, 11, 10);
        g.DrawLine(new Pen(MidBlue), 5, 12, 9, 12);
        g.FillRectangle(new SolidBrush(Red), 3, 2, 10, 1);
    });

    public static Bitmap CreatePaperIcon() => DrawIcon(g =>
    {
        var paperPts = new Point[] { new(3, 2), new(10, 2), new(13, 5), new(13, 14), new(3, 14) };
        g.FillPolygon(new SolidBrush(Color.White), paperPts);
        g.DrawPolygon(new Pen(MidBlue), paperPts);
        g.FillPolygon(new SolidBrush(LightBlue), new Point[] { new(10, 2), new(13, 5), new(10, 5) });
        g.DrawLine(new Pen(MidBlue), 10, 2, 10, 5);
        g.DrawLine(new Pen(MidBlue), 10, 5, 13, 5);
        g.DrawLine(new Pen(MidBlue), 5, 7, 11, 7);
        g.DrawLine(new Pen(MidBlue), 5, 9, 11, 9);
        g.DrawLine(new Pen(MidBlue), 5, 11, 11, 11);
        g.DrawLine(new Pen(MidBlue), 5, 13, 9, 13);
    });

    public static Bitmap CreateSizeIcon() => DrawIcon(g =>
    {
        g.FillRectangle(new SolidBrush(MidBlue), 2, 3, 12, 11);
        g.DrawRectangle(new Pen(DarkBlue), 2, 3, 12, 11);
        g.FillRectangle(new SolidBrush(LightBlue), 3, 4, 10, 3);
        g.DrawLine(new Pen(DarkBlue), 3, 7, 13, 7);
        g.FillRectangle(new SolidBrush(Color.White), 3, 9, 10, 4);
        g.DrawRectangle(new Pen(DarkBlue), 3, 9, 10, 4);
        g.FillRectangle(new SolidBrush(Green), 4, 10, 2, 2);
        g.FillRectangle(new SolidBrush(Yellow), 7, 10, 2, 2);
        g.FillRectangle(new SolidBrush(Red), 10, 10, 2, 2);
    });

    public static Bitmap CreateBrowseIcon() => DrawIcon(g =>
    {
        g.FillPolygon(new SolidBrush(FolderYellow), new Point[]
        {
            new(1, 5), new(5, 5), new(7, 3), new(12, 3), new(12, 12), new(1, 12)
        });
        g.DrawPolygon(new Pen(FolderDark), new Point[]
        {
            new(1, 5), new(5, 5), new(7, 3), new(12, 3), new(12, 12), new(1, 12)
        });
        g.DrawLine(new Pen(FolderDark), 1, 6, 12, 6);
        g.DrawEllipse(new Pen(DarkBlue, 1.5f), 7, 7, 6, 6);
        g.DrawLine(new Pen(DarkBlue, 1.5f), 12, 12, 15, 15);
    });

    public static Bitmap CreateFormatIcon() => DrawIcon(g =>
    {
        var docPts = new Point[] { new(2, 2), new(10, 2), new(13, 5), new(13, 14), new(2, 14) };
        g.FillPolygon(new SolidBrush(Color.White), docPts);
        g.DrawPolygon(new Pen(DarkGray), docPts);
        g.FillPolygon(new SolidBrush(LightGray), new Point[] { new(10, 2), new(13, 5), new(10, 5) });
        g.DrawLine(new Pen(DarkGray), 10, 2, 10, 5);
        g.DrawLine(new Pen(DarkGray), 10, 5, 13, 5);
        g.DrawLine(new Pen(DarkBlue), 4, 7, 11, 7);
        g.DrawLine(new Pen(MidBlue), 4, 9, 9, 9);
        g.DrawLine(new Pen(DarkBlue), 4, 11, 11, 11);
        g.DrawLine(new Pen(MidBlue), 4, 13, 8, 13);
    });

    public static Bitmap CreateGearIcon() => DrawIcon(g =>
    {
        g.FillEllipse(new SolidBrush(DarkGray), 4, 4, 8, 8);
        g.DrawEllipse(new Pen(Color.Black), 4, 4, 8, 8);
        for (int i = 0; i < 8; i++)
        {
            double angle = i * Math.PI / 4;
            int x1 = (int)(8 + Math.Cos(angle) * 4);
            int y1 = (int)(8 + Math.Sin(angle) * 4);
            int x2 = (int)(8 + Math.Cos(angle) * 6);
            int y2 = (int)(8 + Math.Sin(angle) * 6);
            g.FillRectangle(new SolidBrush(DarkGray),
                Math.Min(x1, x2), Math.Min(y1, y2),
                Math.Abs(x2 - x1) + 1, Math.Abs(y2 - y1) + 1);
        }
        g.FillEllipse(new SolidBrush(Color.White), 6, 6, 4, 4);
        g.DrawEllipse(new Pen(Color.Black), 6, 6, 4, 4);
    });

    public static Bitmap CreateDownArrowIcon() => DrawIcon(g =>
    {
        g.FillPolygon(new SolidBrush(DarkBlue),
            new Point[] { new(3, 5), new(13, 5), new(8, 12) });
        g.DrawPolygon(new Pen(Color.Black),
            new Point[] { new(3, 5), new(13, 5), new(8, 12) });
    });

    public static Bitmap CreateUpArrowIcon() => DrawIcon(g =>
    {
        g.FillPolygon(new SolidBrush(DarkBlue),
            new Point[] { new(3, 11), new(13, 11), new(8, 4) });
        g.DrawPolygon(new Pen(Color.Black),
            new Point[] { new(3, 11), new(13, 11), new(8, 4) });
    });

    public static Bitmap CreateCheckIcon() => DrawIcon(g =>
    {
        g.DrawLine(new Pen(Green, 2f), 3, 8, 7, 12);
        g.DrawLine(new Pen(Green, 2f), 6, 12, 13, 4);
    });

    public static Bitmap CreateCrossIcon() => DrawIcon(g =>
    {
        g.DrawLine(new Pen(Red, 2f), 4, 4, 12, 12);
        g.DrawLine(new Pen(Red, 2f), 12, 4, 4, 12);
    });

    public static Bitmap CreateApplyIcon() => DrawIcon(g =>
    {
        g.FillEllipse(new SolidBrush(DarkGray), 3, 3, 7, 7);
        g.FillEllipse(new SolidBrush(Color.White), 5, 5, 3, 3);
        g.DrawLine(new Pen(Green, 1.5f), 9, 10, 12, 13);
        g.DrawLine(new Pen(Green, 1.5f), 11, 13, 15, 8);
    });

    public static Bitmap CreateCarrierIcon() => DrawIcon(g =>
    {
        g.FillRectangle(new SolidBrush(LightBlue), 2, 3, 12, 10);
        g.DrawRectangle(new Pen(DarkBlue), 2, 3, 12, 10);
        g.FillRectangle(new SolidBrush(Color.White), 4, 5, 8, 6);
        g.DrawRectangle(new Pen(DarkGray), 4, 5, 8, 6);
        g.DrawLine(new Pen(LightGray), 5, 7, 11, 7);
        g.DrawLine(new Pen(LightGray), 5, 9, 11, 9);
    });

    public static Bitmap CreateRulerIcon() => DrawIcon(g =>
    {
        g.FillRectangle(new SolidBrush(Yellow), 2, 6, 12, 5);
        g.DrawRectangle(new Pen(DarkGray), 2, 6, 12, 5);
        for (int i = 0; i < 5; i++)
        {
            int x = 3 + i * 2;
            int h = (i == 0 || i == 4) ? 3 : 2;
            g.DrawLine(new Pen(DarkGray), x, 6, x, 6 + h);
        }
    });

    public static Bitmap CreateManageIcon() => DrawIcon(g =>
    {
        g.FillRectangle(new SolidBrush(Green), 2, 7, 5, 2);
        g.FillRectangle(new SolidBrush(Green), 4, 5, 1, 6);
        g.FillRectangle(new SolidBrush(Red), 9, 7, 5, 2);
    });

    // === Scan mode combo item icons ===

    public static Bitmap CreateQualityAutoIcon() => DrawIcon(g =>
    {
        g.FillRectangle(new SolidBrush(DarkGray), 2, 4, 12, 8);
        g.DrawRectangle(new Pen(Color.Black), 2, 4, 12, 8);
        g.FillRectangle(new SolidBrush(Color.White), 3, 6, 10, 4);
        g.DrawLine(new Pen(Red, 1.5f), 4, 8, 12, 8);
        g.FillRectangle(new SolidBrush(Green), 12, 5, 1, 1);
        // Small "A" indicator
        g.FillRectangle(new SolidBrush(LightBlue), 2, 12, 12, 3);
        g.DrawRectangle(new Pen(DarkBlue), 2, 12, 12, 3);
    });

    public static Bitmap CreateQualityNormalIcon() => DrawIcon(g =>
    {
        g.FillRectangle(new SolidBrush(DarkGray), 2, 4, 12, 8);
        g.DrawRectangle(new Pen(Color.Black), 2, 4, 12, 8);
        g.FillRectangle(new SolidBrush(Color.White), 3, 6, 10, 4);
        // Single scan line (coarse)
        g.DrawLine(new Pen(Red, 2f), 4, 8, 12, 8);
    });

    public static Bitmap CreateQualityFineIcon() => DrawIcon(g =>
    {
        g.FillRectangle(new SolidBrush(DarkGray), 2, 4, 12, 8);
        g.DrawRectangle(new Pen(Color.Black), 2, 4, 12, 8);
        g.FillRectangle(new SolidBrush(Color.White), 3, 6, 10, 4);
        // Two scan lines (finer)
        g.DrawLine(new Pen(Red, 1f), 4, 7, 12, 7);
        g.DrawLine(new Pen(Red, 1f), 4, 9, 12, 9);
    });

    public static Bitmap CreateQualityBestIcon() => DrawIcon(g =>
    {
        g.FillRectangle(new SolidBrush(DarkGray), 2, 4, 12, 8);
        g.DrawRectangle(new Pen(Color.Black), 2, 4, 12, 8);
        g.FillRectangle(new SolidBrush(Color.White), 3, 6, 10, 4);
        // Three scan lines (dense)
        g.DrawLine(new Pen(Red, 0.8f), 4, 7, 12, 7);
        g.DrawLine(new Pen(Red, 0.8f), 4, 8, 12, 8);
        g.DrawLine(new Pen(Red, 0.8f), 4, 9, 12, 9);
    });

    public static Bitmap CreateQualityExcellentIcon() => DrawIcon(g =>
    {
        g.FillRectangle(new SolidBrush(DarkGray), 2, 4, 12, 8);
        g.DrawRectangle(new Pen(Color.Black), 2, 4, 12, 8);
        g.FillRectangle(new SolidBrush(Color.White), 3, 6, 10, 4);
        // Five scan lines (very dense)
        for (int y = 6; y <= 10; y++)
            g.DrawLine(new Pen(Red, 0.5f), 4, y, 12, y);
    });

    public static Bitmap CreateColorAutoIcon() => DrawIcon(g =>
    {
        // Multi-color swatch (RGB detection)
        g.FillRectangle(new SolidBrush(Red), 2, 3, 5, 5);
        g.FillRectangle(new SolidBrush(Green), 8, 3, 5, 5);
        g.FillRectangle(new SolidBrush(Color.FromArgb(50, 100, 220)), 2, 8, 5, 5);
        g.FillRectangle(new SolidBrush(Yellow), 8, 8, 5, 5);
        g.DrawRectangle(new Pen(DarkGray), 2, 3, 11, 10);
        g.DrawLine(new Pen(DarkGray), 7, 3, 7, 13);
        g.DrawLine(new Pen(DarkGray), 2, 8, 13, 8);
    });

    public static Bitmap CreateColorColorIcon() => DrawIcon(g =>
    {
        // Colorful swatch
        g.FillRectangle(new SolidBrush(Red), 2, 3, 4, 10);
        g.FillRectangle(new SolidBrush(Green), 6, 3, 4, 10);
        g.FillRectangle(new SolidBrush(Color.FromArgb(50, 100, 220)), 10, 3, 4, 10);
        g.DrawRectangle(new Pen(DarkGray), 2, 3, 12, 10);
    });

    public static Bitmap CreateColorGrayIcon() => DrawIcon(g =>
    {
        // Grayscale gradient (inverted look: dark to light)
        g.FillRectangle(new SolidBrush(Color.White), 2, 3, 12, 10);
        g.DrawRectangle(new Pen(DarkGray), 2, 3, 12, 10);
        for (int i = 0; i < 12; i++)
        {
            int v = 240 - i * 18;
            g.FillRectangle(new SolidBrush(Color.FromArgb(v, v, v)), 2 + i, 3, 1, 10);
        }
        g.DrawRectangle(new Pen(DarkGray), 2, 3, 12, 10);
    });

    public static Bitmap CreateColorBWIcon() => DrawIcon(g =>
    {
        // Black and white split
        g.FillRectangle(new SolidBrush(Color.Black), 2, 3, 6, 10);
        g.FillRectangle(new SolidBrush(Color.White), 8, 3, 6, 10);
        g.DrawRectangle(new Pen(DarkGray), 2, 3, 12, 10);
        g.DrawLine(new Pen(DarkGray), 8, 3, 8, 13);
    });

    public static Bitmap CreateScanSideDuplexIcon() => DrawIcon(g =>
    {
        // Two sheets (front + back)
        g.FillRectangle(new SolidBrush(Color.White), 2, 3, 6, 10);
        g.DrawRectangle(new Pen(DarkBlue), 2, 3, 6, 10);
        g.FillRectangle(new SolidBrush(LightBlue), 7, 5, 6, 10);
        g.DrawRectangle(new Pen(DarkBlue), 7, 5, 6, 10);
        // Arrows
        g.DrawLine(new Pen(Red, 1.5f), 3, 6, 7, 6);
        g.DrawLine(new Pen(Red, 1.5f), 8, 12, 12, 12);
    });

    public static Bitmap CreateScanSideSimplexIcon() => DrawIcon(g =>
    {
        // Single sheet with scan arrow
        g.FillRectangle(new SolidBrush(Color.White), 4, 2, 8, 12);
        g.DrawRectangle(new Pen(DarkBlue), 4, 2, 8, 12);
        g.DrawLine(new Pen(MidBlue), 6, 5, 10, 5);
        g.DrawLine(new Pen(MidBlue), 6, 7, 10, 7);
        g.DrawLine(new Pen(MidBlue), 6, 9, 10, 9);
        // Scan arrow
        g.FillPolygon(new SolidBrush(Red), new Point[] { new(2, 14), new(6, 11), new(6, 13), new(12, 13), new(12, 15), new(6, 15), new(6, 17) });
    });

    public static Bitmap CreateScanSideFlatbedIcon() => DrawIcon(g =>
    {
        // Flatbed scanner: lid + glass surface
        g.FillRectangle(new SolidBrush(DarkGray), 2, 3, 12, 3);
        g.DrawRectangle(new Pen(Color.Black), 2, 3, 12, 3);
        g.FillRectangle(new SolidBrush(LightBlue), 2, 7, 12, 6);
        g.DrawRectangle(new Pen(DarkBlue), 2, 7, 12, 6);
        // Paper on glass
        g.FillRectangle(new SolidBrush(Color.White), 5, 9, 6, 3);
        g.DrawRectangle(new Pen(DarkGray), 5, 9, 6, 3);
    });

    public static Bitmap CreateScanSideAutoIcon() => DrawIcon(g =>
    {
        // Circular arrows (automatic detection)
        g.DrawArc(new Pen(DarkBlue, 2f), 3, 3, 10, 10, 0, 270);
        // Arrow head
        g.FillPolygon(new SolidBrush(DarkBlue), new Point[] { new(12, 4), new(14, 8), new(10, 8) });
    });

    private static Bitmap DrawIcon(Action<Graphics> draw)
    {
        var bmp = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);
        draw(g);
        return bmp;
    }
}
