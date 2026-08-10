using System.Drawing;
using System.Drawing.Drawing2D;

namespace SpeedScanManager;

/// <summary>
/// Renders detailed 3D-style icons for the PostScanMediaDialog grid items,
/// matching the Fujitsu ScanSnap original visual style.
/// </summary>
internal static class PostScanIcons
{
    public static Bitmap CreateFolderIcon() => DrawToBitmap(DrawFolder);
    public static Bitmap CreateEmailIcon() => DrawToBitmap(DrawEmail);
    public static Bitmap CreatePrintIcon() => DrawToBitmap(DrawPrinter);
    public static Bitmap CreateWordIcon() => DrawToBitmap(DrawWord);
    public static Bitmap CreateExcelIcon() => DrawToBitmap(DrawExcel);
    public static Bitmap CreatePowerPointIcon() => DrawToBitmap(DrawPowerPoint);
    public static Bitmap CreatePictureFolderIcon() => DrawToBitmap(DrawPictureFolder);
    public static Bitmap CreatePdfEditIcon() => DrawToBitmap(DrawPdfEdit);

    private static Bitmap DrawToBitmap(Action<Graphics, Rectangle> drawAction)
    {
        var bmp = new Bitmap(48, 48);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        drawAction(g, new Rectangle(0, 0, 48, 48));
        return bmp;
    }

    private static void DrawFolder(Graphics g, Rectangle rect)
    {
        int x = rect.X + 4, y = rect.Y + 6, w = rect.Width - 8, h = rect.Height - 10;
        // Folder tab
        var tabPath = new GraphicsPath();
        tabPath.AddRectangle(new Rectangle(x, y, w / 3, 6));
        using var tabBrush = new LinearGradientBrush(new Rectangle(x, y, w / 3, 6),
            Color.FromArgb(255, 220, 100), Color.FromArgb(220, 175, 30), LinearGradientMode.Vertical);
        g.FillPath(tabBrush, tabPath);
        // Folder body with gradient
        var bodyRect = new Rectangle(x, y + 5, w, h - 5);
        var bodyPath = RoundRect(bodyRect, 3);
        using var bodyBrush = new LinearGradientBrush(bodyRect,
            Color.FromArgb(255, 225, 110), Color.FromArgb(210, 165, 25), LinearGradientMode.Vertical);
        g.FillPath(bodyBrush, bodyPath);
        using var edgePen = new Pen(Color.FromArgb(160, 120, 15), 1.2f);
        g.DrawPath(edgePen, bodyPath);
        g.DrawRectangle(edgePen, new Rectangle(x, y, w / 3, 6));
        // Inner highlight line
        using var hlPen = new Pen(Color.FromArgb(255, 240, 160), 1f);
        g.DrawLine(hlPen, x + 2, y + 8, x + w - 2, y + 8);
        // Paper sheets sticking out
        var paperRect = new Rectangle(x + 6, y + 2, w - 12, 10);
        g.FillRectangle(Brushes.White, paperRect);
        g.DrawRectangle(new Pen(Color.FromArgb(200, 200, 200), 0.8f), paperRect);
    }

    private static void DrawEmail(Graphics g, Rectangle rect)
    {
        int x = rect.X + 4, y = rect.Y + 8, w = rect.Width - 8, h = rect.Height - 16;
        var envelopeRect = new Rectangle(x, y, w, h);
        // Envelope body
        using var bodyBrush = new LinearGradientBrush(envelopeRect,
            Color.FromArgb(210, 170, 110), Color.FromArgb(170, 130, 70), LinearGradientMode.Vertical);
        var path = RoundRect(envelopeRect, 2);
        g.FillPath(bodyBrush, path);
        using var edgePen = new Pen(Color.FromArgb(120, 90, 50), 1.2f);
        g.DrawPath(edgePen, path);
        // Flap (V shape)
        using var flapPen = new Pen(Color.FromArgb(140, 100, 60), 1.5f);
        var midX = x + w / 2;
        var midY = y + h / 2;
        g.DrawLine(flapPen, x, y, midX, midY);
        g.DrawLine(flapPen, x + w, y, midX, midY);
        // Inner flap shadow
        using var shadowPen = new Pen(Color.FromArgb(190, 150, 90), 1f);
        g.DrawLine(shadowPen, x + 2, y + 2, midX, midY - 1);
        g.DrawLine(shadowPen, x + w - 2, y + 2, midX, midY - 1);
        // Stamp
        var stampRect = new Rectangle(x + w - 12, y + 3, 8, 6);
        g.FillRectangle(new SolidBrush(Color.FromArgb(200, 80, 80)), stampRect);
        g.DrawRectangle(new Pen(Color.FromArgb(140, 40, 40), 0.8f), stampRect);
    }

    private static void DrawPrinter(Graphics g, Rectangle rect)
    {
        int x = rect.X + 4, y = rect.Y + 4, w = rect.Width - 8;
        // Paper input (top sheet)
        var topRect = new Rectangle(x + 8, y, w - 16, 10);
        g.FillRectangle(Brushes.White, topRect);
        g.DrawRectangle(new Pen(Color.FromArgb(180, 180, 180), 0.8f), topRect);
        // Printer body
        var bodyRect = new Rectangle(x, y + 9, w, 18);
        using var bodyBrush = new LinearGradientBrush(bodyRect,
            Color.FromArgb(120, 120, 125), Color.FromArgb(80, 80, 85), LinearGradientMode.Vertical);
        var bodyPath = RoundRect(bodyRect, 2);
        g.FillPath(bodyBrush, bodyPath);
        using var edgePen = new Pen(Color.FromArgb(50, 50, 55), 1.2f);
        g.DrawPath(edgePen, bodyPath);
        // LED indicator
        g.FillEllipse(new SolidBrush(Color.FromArgb(80, 200, 80)), x + 4, y + 14, 4, 4);
        // Paper output (bottom)
        var botRect = new Rectangle(x + 6, y + 27, w - 12, rect.Height - y - 27);
        g.FillRectangle(Brushes.White, botRect);
        g.DrawRectangle(new Pen(Color.FromArgb(180, 180, 180), 0.8f), botRect);
        // Text lines on output paper
        using var linePen = new Pen(Color.FromArgb(150, 150, 160), 0.8f);
        for (int i = 0; i < 3; i++)
            g.DrawLine(linePen, botRect.X + 2, botRect.Y + 3 + i * 3, botRect.Right - 2, botRect.Y + 3 + i * 3);
    }

    private static void DrawWord(Graphics g, Rectangle rect)
    {
        int x = rect.X + 6, y = rect.Y + 6, s = rect.Width - 12;
        // Document background
        var docRect = new Rectangle(x, y, s, s);
        using var docBrush = new LinearGradientBrush(docRect,
            Color.White, Color.FromArgb(240, 245, 255), LinearGradientMode.Vertical);
        var docPath = RoundRect(docRect, 3);
        g.FillPath(docBrush, docPath);
        using var edgePen = new Pen(Color.FromArgb(180, 200, 230), 1f);
        g.DrawPath(edgePen, docPath);
        // Blue banner
        var bannerRect = new Rectangle(x, y + s - 12, s, 12);
        using var bannerBrush = new LinearGradientBrush(bannerRect,
            Color.FromArgb(40, 90, 200), Color.FromArgb(20, 60, 160), LinearGradientMode.Vertical);
        var bannerPath = RoundRect(bannerRect, 2);
        g.FillPath(bannerBrush, bannerPath);
        // W text
        using var font = new Font("Arial", 11f, FontStyle.Bold);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("W", font, Brushes.White, bannerRect, sf);
        // Text lines on document
        using var linePen = new Pen(Color.FromArgb(180, 200, 220), 0.8f);
        for (int i = 0; i < 3; i++)
            g.DrawLine(linePen, x + 4, y + 4 + i * 4, x + s - 4, y + 4 + i * 4);
    }

    private static void DrawExcel(Graphics g, Rectangle rect)
    {
        int x = rect.X + 6, y = rect.Y + 6, s = rect.Width - 12;
        var docRect = new Rectangle(x, y, s, s);
        using var docBrush = new LinearGradientBrush(docRect,
            Color.White, Color.FromArgb(235, 250, 240), LinearGradientMode.Vertical);
        var docPath = RoundRect(docRect, 3);
        g.FillPath(docBrush, docPath);
        using var edgePen = new Pen(Color.FromArgb(180, 230, 200), 1f);
        g.DrawPath(edgePen, docPath);
        // Green banner
        var bannerRect = new Rectangle(x, y + s - 12, s, 12);
        using var bannerBrush = new LinearGradientBrush(bannerRect,
            Color.FromArgb(40, 150, 70), Color.FromArgb(20, 110, 45), LinearGradientMode.Vertical);
        var bannerPath = RoundRect(bannerRect, 2);
        g.FillPath(bannerBrush, bannerPath);
        using var font = new Font("Arial", 11f, FontStyle.Bold);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("X", font, Brushes.White, bannerRect, sf);
        // Grid lines
        using var linePen = new Pen(Color.FromArgb(180, 220, 195), 0.8f);
        for (int i = 0; i < 3; i++)
        {
            g.DrawLine(linePen, x + 4, y + 4 + i * 4, x + s - 4, y + 4 + i * 4);
            g.DrawLine(linePen, x + 4 + i * (s - 8) / 3, y + 4, x + 4 + i * (s - 8) / 3, y + s - 12);
        }
    }

    private static void DrawPowerPoint(Graphics g, Rectangle rect)
    {
        int x = rect.X + 6, y = rect.Y + 6, s = rect.Width - 12;
        var docRect = new Rectangle(x, y, s, s);
        using var docBrush = new LinearGradientBrush(docRect,
            Color.White, Color.FromArgb(255, 245, 240), LinearGradientMode.Vertical);
        var docPath = RoundRect(docRect, 3);
        g.FillPath(docBrush, docPath);
        using var edgePen = new Pen(Color.FromArgb(230, 190, 180), 1f);
        g.DrawPath(edgePen, docPath);
        // Orange/red banner
        var bannerRect = new Rectangle(x, y + s - 12, s, 12);
        using var bannerBrush = new LinearGradientBrush(bannerRect,
            Color.FromArgb(210, 80, 40), Color.FromArgb(170, 55, 20), LinearGradientMode.Vertical);
        var bannerPath = RoundRect(bannerRect, 2);
        g.FillPath(bannerBrush, bannerPath);
        using var font = new Font("Arial", 10f, FontStyle.Bold);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("P", font, Brushes.White, bannerRect, sf);
        // Slide placeholder lines
        using var linePen = new Pen(Color.FromArgb(220, 180, 170), 0.8f);
        g.DrawRectangle(linePen, x + 4, y + 3, s - 8, s - 18);
    }

    private static void DrawPictureFolder(Graphics g, Rectangle rect)
    {
        int x = rect.X + 4, y = rect.Y + 6, w = rect.Width - 8, h = rect.Height - 10;
        // Folder tab
        using var tabBrush = new SolidBrush(Color.FromArgb(100, 180, 220));
        g.FillRectangle(tabBrush, new Rectangle(x, y, w / 3, 6));
        // Folder body
        var bodyRect = new Rectangle(x, y + 5, w, h - 5);
        var bodyPath = RoundRect(bodyRect, 3);
        using var bodyBrush = new LinearGradientBrush(bodyRect,
            Color.FromArgb(110, 190, 230), Color.FromArgb(50, 120, 180), LinearGradientMode.Vertical);
        g.FillPath(bodyBrush, bodyPath);
        using var edgePen = new Pen(Color.FromArgb(30, 90, 150), 1.2f);
        g.DrawPath(edgePen, bodyPath);
        // Picture frame on folder
        var imgRect = new Rectangle(x + 6, y + 10, w - 12, h - 16);
        g.FillRectangle(Brushes.White, imgRect);
        g.DrawRectangle(new Pen(Color.FromArgb(200, 200, 200), 0.8f), imgRect);
        // Sky
        var skyRect = new Rectangle(imgRect.X + 1, imgRect.Y + 1, imgRect.Width - 2, imgRect.Height / 2);
        using var skyBrush = new LinearGradientBrush(skyRect,
            Color.FromArgb(180, 220, 255), Color.FromArgb(200, 230, 255), LinearGradientMode.Vertical);
        g.FillRectangle(skyBrush, skyRect);
        // Mountains
        using var mtnBrush = new SolidBrush(Color.FromArgb(80, 130, 70));
        var p1 = new PointF(imgRect.X + 2, imgRect.Bottom - 2);
        var p2 = new PointF(imgRect.X + imgRect.Width / 3, imgRect.Y + imgRect.Height / 2);
        var p3 = new PointF(imgRect.X + imgRect.Width * 2 / 3, imgRect.Bottom - 2);
        var p4 = new PointF(imgRect.Right - 2, imgRect.Y + imgRect.Height / 3);
        var p5 = new PointF(imgRect.Right - 2, imgRect.Bottom - 2);
        g.FillPolygon(mtnBrush, new[] { p1, p2, p3, p4, p5 });
        // Sun
        g.FillEllipse(Brushes.Gold, imgRect.X + imgRect.Width - 10, imgRect.Y + 3, 6, 6);
    }

    private static void DrawPdfEdit(Graphics g, Rectangle rect)
    {
        int x = rect.X + 6, y = rect.Y + 4, w = rect.Width - 12, h = rect.Height - 8;
        // PDF document
        var docRect = new Rectangle(x, y, w, h);
        using var docBrush = new LinearGradientBrush(docRect,
            Color.FromArgb(230, 70, 60), Color.FromArgb(180, 40, 30), LinearGradientMode.Vertical);
        var docPath = RoundRect(docRect, 3);
        g.FillPath(docBrush, docPath);
        using var edgePen = new Pen(Color.FromArgb(120, 20, 15), 1.2f);
        g.DrawPath(edgePen, docPath);
        // PDF text
        using var font = new Font("Arial", 9f, FontStyle.Bold);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("PDF", font, Brushes.White, new Rectangle(x, y + 6, w, h - 10), sf);
        // Folded corner
        var cornerPath = new GraphicsPath();
        cornerPath.AddLine(new PointF(x + w - 8, y), new PointF(x + w, y));
        cornerPath.AddLine(new PointF(x + w, y), new PointF(x + w, y + 8));
        cornerPath.CloseFigure();
        g.FillPath(new SolidBrush(Color.FromArgb(160, 30, 20)), cornerPath);
        // Magnifying glass overlay
        int gx = x + w - 6, gy = y + h - 4;
        var glassRect = new Rectangle(gx - 10, gy - 10, 12, 12);
        g.FillEllipse(new SolidBrush(Color.FromArgb(100, 255, 255, 200)), glassRect);
        g.DrawEllipse(new Pen(Color.FromArgb(80, 80, 80), 1.5f), glassRect);
        g.DrawLine(new Pen(Color.FromArgb(80, 80, 80), 2f), gx - 1, gy - 1, gx + 4, gy + 4);
    }

    private static GraphicsPath RoundRect(Rectangle rect, int radius)
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
