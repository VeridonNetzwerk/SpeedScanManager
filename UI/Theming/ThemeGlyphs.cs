using System.Drawing;
using System.Drawing.Drawing2D;

namespace SpeedScanManager;

/// <summary>Monochrome line glyphs (Discord-like icons) drawn as vectors in any color and size.</summary>
internal enum Glyph
{
    None,
    Apps,
    Folder,
    Scanner,
    FileType,
    Paper,
    Compress,
    Check,
    Cross,
    Apply,
    ChevronDown,
    ChevronUp,
    ChevronRight,
    Gear,
    PlusMinus,
    Carrier,
    Ruler,
    Mail,
    Printer,
    Palette,
    Sliders,
    Star,
    Sparkle,
    User,
    Question,
    Minus,
    Plus,
    Power,
    Image,
    /// <summary>Magic wand (automatic detection).</summary>
    Wand,
    /// <summary>Circle in half-tone (grayscale).</summary>
    Grayscale,
    /// <summary>Circle half black / half white (black and white).</summary>
    Contrast,
    /// <summary>Single page with text lines (simplex).</summary>
    PageSingle,
    /// <summary>Front page with a second page behind it (duplex).</summary>
    PageDuplex,
    /// <summary>Shield with a check mark (recommended).</summary>
    ShieldCheck,
    /// <summary>Document with an arrow pointing down (small file).</summary>
    FileSmall,
    /// <summary>Picture with a sparkle (high image quality).</summary>
    ImageHq,
    /// <summary>Box with an arrow pointing out (open externally).</summary>
    OpenExternal,
    /// <summary>Level meter, 1 of 4 bars filled (image quality Normal).</summary>
    Level1,
    /// <summary>Level meter, 2 of 4 bars filled (image quality Fein).</summary>
    Level2,
    /// <summary>Level meter, 3 of 4 bars filled (image quality Beste).</summary>
    Level3,
    /// <summary>Level meter, 4 of 4 bars filled (image quality Hervorragend).</summary>
    Level4,
    /// <summary>Globe with meridians (languages).</summary>
    Globe,
    /// <summary>Award rosette with ribbons (recommended).</summary>
    Award,
    /// <summary>Document squeezed by two arrows (compressed, small file).</summary>
    FileZip,
    /// <summary>Circular arrow (rotation).</summary>
    Rotate,
    /// <summary>Tilted page next to a straight guide (skew correction).</summary>
    Skew,
    /// <summary>Empty page with a diagonal stroke (blank page removal).</summary>
    PageBlank,
    /// <summary>Padlock (password).</summary>
    Lock,
    /// <summary>Sun (brightness).</summary>
    Sun,
    /// <summary>Clock (date and time).</summary>
    Clock,
    /// <summary>Number sign (counter).</summary>
    Hash,
    /// <summary>Two pages side by side with a cut line (split).</summary>
    Split,
    /// <summary>Text cursor in a field (name / text input).</summary>
    TextField,
    /// <summary>Crescent moon (dark design).</summary>
    Moon,
    /// <summary>Monitor (follow the system).</summary>
    Monitor,
    /// <summary>Arrow down into a tray (update / download).</summary>
    Download
}

internal static class ThemeGlyphs
{
    /// <summary>Draws <paramref name="glyph"/> into <paramref name="bounds"/> (designed on a 16x16 grid).</summary>
    public static void Draw(Graphics g, Glyph glyph, RectangleF bounds, Color color) => Draw(g, glyph, bounds, color, 1f);

    /// <summary>
    /// Draws <paramref name="glyph"/> with the stroke width multiplied by <paramref name="weight"/>
    /// (e.g. 1.3 for white glyphs on small colored tiles).
    /// </summary>
    public static void Draw(Graphics g, Glyph glyph, RectangleF bounds, Color color, float weight)
    {
        if (glyph == Glyph.None || bounds.Width <= 0 || bounds.Height <= 0) return;

        var state = g.Save();
        try
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            float s = Math.Min(bounds.Width, bounds.Height) / 16f;
            g.TranslateTransform(bounds.X + (bounds.Width - 16 * s) / 2f, bounds.Y + (bounds.Height - 16 * s) / 2f);
            g.ScaleTransform(s, s);

            using var pen = new Pen(color, 1.5f * weight) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            using var thick = new Pen(color, 2f * weight) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            using var brush = new SolidBrush(color);
            DrawCore(g, glyph, pen, thick, brush);
        }
        finally
        {
            g.Restore(state);
        }
    }

    /// <summary>Renders the glyph into a new 32-bit bitmap (for APIs that need an Image).</summary>
    public static Bitmap CreateBitmap(Glyph glyph, int size, Color color)
    {
        var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        Draw(g, glyph, new RectangleF(0, 0, size, size), color);
        return bmp;
    }

    private static void DrawCore(Graphics g, Glyph glyph, Pen pen, Pen thick, Brush brush)
    {
        switch (glyph)
        {
            case Glyph.Apps:
                using (var p = Rounded(new RectangleF(2f, 2f, 5f, 5f), 1.5f)) g.DrawPath(pen, p);
                using (var p = Rounded(new RectangleF(9f, 2f, 5f, 5f), 1.5f)) g.DrawPath(pen, p);
                using (var p = Rounded(new RectangleF(2f, 9f, 5f, 5f), 1.5f)) g.DrawPath(pen, p);
                using (var p = Rounded(new RectangleF(9f, 9f, 5f, 5f), 1.5f)) g.DrawPath(pen, p);
                break;

            case Glyph.Folder:
                g.DrawPolygon(pen, new PointF[]
                {
                    new(1.75f, 3.5f), new(6f, 3.5f), new(7.5f, 5.25f), new(14.25f, 5.25f),
                    new(14.25f, 12.75f), new(1.75f, 12.75f)
                });
                g.DrawLine(pen, 1.75f, 7.5f, 14.25f, 7.5f);
                break;

            case Glyph.Scanner:
                using (var p = Rounded(new RectangleF(1.75f, 7.75f, 12.5f, 5.5f), 1.5f)) g.DrawPath(pen, p);
                g.DrawLine(pen, 2.5f, 5.75f, 13.25f, 2.75f);
                g.FillEllipse(brush, 10.6f, 9.6f, 1.8f, 1.8f);
                g.DrawLine(pen, 4f, 10.5f, 8f, 10.5f);
                break;

            case Glyph.FileType:
                g.DrawPolygon(pen, new PointF[]
                {
                    new(3.25f, 1.75f), new(9.25f, 1.75f), new(12.75f, 5.25f), new(12.75f, 14.25f), new(3.25f, 14.25f)
                });
                g.DrawLines(pen, new PointF[] { new(9.25f, 1.75f), new(9.25f, 5.25f), new(12.75f, 5.25f) });
                g.DrawLine(pen, 5.75f, 8.5f, 10.25f, 8.5f);
                g.DrawLine(pen, 5.75f, 11.25f, 9f, 11.25f);
                break;

            case Glyph.Paper:
                using (var p = Rounded(new RectangleF(5f, 1.75f, 9.25f, 10.5f), 1.25f)) g.DrawPath(pen, p);
                g.DrawLines(pen, new PointF[] { new(2f, 4.75f), new(2f, 14.25f), new(10.75f, 14.25f) });
                break;

            case Glyph.Compress:
                g.DrawLine(pen, 8f, 1.75f, 8f, 6f);
                g.DrawLines(pen, new PointF[] { new(5.5f, 3.75f), new(8f, 6.25f), new(10.5f, 3.75f) });
                g.DrawLine(pen, 8f, 14.25f, 8f, 10f);
                g.DrawLines(pen, new PointF[] { new(5.5f, 12.25f), new(8f, 9.75f), new(10.5f, 12.25f) });
                g.DrawLine(pen, 2f, 8f, 14f, 8f);
                break;

            case Glyph.Check:
                g.DrawLines(thick, new PointF[] { new(3f, 8.5f), new(6.5f, 12f), new(13f, 4.5f) });
                break;

            case Glyph.Cross:
                g.DrawLine(thick, 4f, 4f, 12f, 12f);
                g.DrawLine(thick, 12f, 4f, 4f, 12f);
                break;

            case Glyph.Apply:
                g.DrawEllipse(pen, 1.75f, 1.75f, 12.5f, 12.5f);
                g.DrawLines(pen, new PointF[] { new(5f, 8.25f), new(7.25f, 10.5f), new(11f, 6f) });
                break;

            case Glyph.ChevronDown:
                g.DrawLines(thick, new PointF[] { new(4f, 6.25f), new(8f, 10.25f), new(12f, 6.25f) });
                break;

            case Glyph.ChevronUp:
                g.DrawLines(thick, new PointF[] { new(4f, 9.75f), new(8f, 5.75f), new(12f, 9.75f) });
                break;

            case Glyph.ChevronRight:
                g.DrawLines(thick, new PointF[] { new(6.25f, 4f), new(10.25f, 8f), new(6.25f, 12f) });
                break;

            case Glyph.Gear:
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4;
                    g.DrawLine(thick,
                        (float)(8 + Math.Cos(a) * 4.75), (float)(8 + Math.Sin(a) * 4.75),
                        (float)(8 + Math.Cos(a) * 6.5), (float)(8 + Math.Sin(a) * 6.5));
                }
                g.DrawEllipse(pen, 3.5f, 3.5f, 9f, 9f);
                g.DrawEllipse(pen, 6.25f, 6.25f, 3.5f, 3.5f);
                break;

            case Glyph.PlusMinus:
                g.DrawLine(pen, 1.75f, 8f, 7.25f, 8f);
                g.DrawLine(pen, 4.5f, 5.25f, 4.5f, 10.75f);
                g.DrawLine(pen, 9.25f, 8f, 14.25f, 8f);
                break;

            case Glyph.Carrier:
                using (var p = Rounded(new RectangleF(1.75f, 2.25f, 12.5f, 11.5f), 2f)) g.DrawPath(pen, p);
                using (var p = Rounded(new RectangleF(4.75f, 5.25f, 6.5f, 5.5f), 1f)) g.DrawPath(pen, p);
                break;

            case Glyph.Ruler:
                using (var p = Rounded(new RectangleF(1.75f, 5f, 12.5f, 6f), 1.25f)) g.DrawPath(pen, p);
                foreach (var x in new[] { 4.5f, 7f, 9.5f, 12f })
                    g.DrawLine(pen, x, 5f, x, x == 7f || x == 12f ? 8.5f : 7.25f);
                break;

            case Glyph.Mail:
                using (var p = Rounded(new RectangleF(1.75f, 3.25f, 12.5f, 9.5f), 1.5f)) g.DrawPath(pen, p);
                g.DrawLines(pen, new PointF[] { new(2.5f, 4.5f), new(8f, 8.75f), new(13.5f, 4.5f) });
                break;

            case Glyph.Printer:
                // Paper in the top slot, printer body with a status light, printed sheet below
                g.DrawLines(pen, new PointF[] { new(4.5f, 5.25f), new(4.5f, 1.75f), new(11.5f, 1.75f), new(11.5f, 5.25f) });
                using (var p = Rounded(new RectangleF(1.5f, 5.25f, 13f, 6.25f), 1.75f)) g.DrawPath(pen, p);
                g.FillEllipse(brush, 11.1f, 6.9f, 1.6f, 1.6f);
                using (var p = new GraphicsPath())
                {
                    p.AddLines(new PointF[] { new(4.5f, 9.25f), new(11.5f, 9.25f), new(11.5f, 14.5f), new(4.5f, 14.5f) });
                    p.CloseFigure();
                    var bc = ((SolidBrush)brush).Color;
                    using (var back = new SolidBrush(Color.FromArgb(Math.Max(1, bc.A / 4), bc)))
                        g.FillPath(back, p);
                    g.DrawPath(pen, p);
                }
                g.DrawLine(pen, 6.25f, 11.6f, 9.75f, 11.6f);
                break;

            case Glyph.Palette:
                g.DrawEllipse(pen, 1.75f, 1.75f, 12.5f, 12.5f);
                g.FillEllipse(brush, 4.5f, 5.5f, 2f, 2f);
                g.FillEllipse(brush, 7.25f, 3.75f, 2f, 2f);
                g.FillEllipse(brush, 10f, 5.75f, 2f, 2f);
                g.FillEllipse(brush, 9f, 9.25f, 2.5f, 2.5f);
                break;

            case Glyph.Sliders:
                g.DrawLine(pen, 2f, 4f, 14f, 4f);
                g.DrawLine(pen, 2f, 8f, 14f, 8f);
                g.DrawLine(pen, 2f, 12f, 14f, 12f);
                g.FillEllipse(brush, 8.75f, 2.25f, 3.5f, 3.5f);
                g.FillEllipse(brush, 3.75f, 6.25f, 3.5f, 3.5f);
                g.FillEllipse(brush, 7.25f, 10.25f, 3.5f, 3.5f);
                break;

            case Glyph.Star:
            {
                var pts = new PointF[10];
                for (int i = 0; i < 10; i++)
                {
                    double a = -Math.PI / 2 + i * Math.PI / 5;
                    float r = i % 2 == 0 ? 6.5f : 2.9f;
                    pts[i] = new PointF(8f + (float)Math.Cos(a) * r, 8.5f + (float)Math.Sin(a) * r);
                }
                g.DrawPolygon(pen, pts);
                break;
            }

            case Glyph.Sparkle:
                using (var p = new GraphicsPath())
                {
                    p.AddBezier(7f, 1.5f, 7.6f, 5.4f, 8.6f, 6.4f, 12.5f, 7f);
                    p.AddBezier(12.5f, 7f, 8.6f, 7.6f, 7.6f, 8.6f, 7f, 12.5f);
                    p.AddBezier(7f, 12.5f, 6.4f, 8.6f, 5.4f, 7.6f, 1.5f, 7f);
                    p.AddBezier(1.5f, 7f, 5.4f, 6.4f, 6.4f, 5.4f, 7f, 1.5f);
                    p.CloseFigure();
                    g.DrawPath(pen, p);
                }
                g.DrawLine(pen, 12.5f, 11f, 12.5f, 14.5f);
                g.DrawLine(pen, 10.75f, 12.75f, 14.25f, 12.75f);
                break;

            case Glyph.User:
                g.DrawEllipse(pen, 5f, 1.75f, 6f, 6f);
                g.DrawArc(pen, 2.25f, 9.5f, 11.5f, 9.5f, 180, 180);
                break;

            case Glyph.Question:
                g.DrawEllipse(pen, 1.75f, 1.75f, 12.5f, 12.5f);
                g.DrawArc(pen, 5.75f, 4f, 4.5f, 4.5f, 180, 250);
                g.DrawLine(pen, 8f, 8.75f, 8f, 9.5f);
                g.FillEllipse(brush, 7.1f, 10.9f, 1.8f, 1.8f);
                break;

            case Glyph.Minus:
                g.DrawLine(thick, 3.5f, 8f, 12.5f, 8f);
                break;

            case Glyph.Plus:
                g.DrawLine(thick, 3.5f, 8f, 12.5f, 8f);
                g.DrawLine(thick, 8f, 3.5f, 8f, 12.5f);
                break;

            case Glyph.Power:
                g.DrawArc(pen, 2.25f, 2.75f, 11.5f, 11.5f, -55, 290);
                g.DrawLine(pen, 8f, 1.5f, 8f, 7.5f);
                break;

            case Glyph.Image:
                using (var p = Rounded(new RectangleF(1.75f, 2.75f, 12.5f, 10.5f), 1.75f)) g.DrawPath(pen, p);
                g.DrawLines(pen, new PointF[] { new(2.5f, 12f), new(6.25f, 8f), new(9f, 10.5f), new(10.75f, 9f), new(13.5f, 11.75f) });
                g.FillEllipse(brush, 9.5f, 4.75f, 2.25f, 2.25f);
                break;

            case Glyph.Wand:
                g.DrawLine(thick, 2.75f, 13.25f, 9.5f, 6.5f);
                // sparkles
                g.DrawLine(pen, 12f, 1.75f, 12f, 4.75f);
                g.DrawLine(pen, 10.5f, 3.25f, 13.5f, 3.25f);
                g.DrawLine(pen, 13.75f, 7.25f, 13.75f, 9.25f);
                g.DrawLine(pen, 12.75f, 8.25f, 14.75f, 8.25f);
                g.DrawLine(pen, 6.5f, 1.75f, 6.5f, 3.75f);
                g.DrawLine(pen, 5.5f, 2.75f, 7.5f, 2.75f);
                break;

            case Glyph.Grayscale:
                using (var half = new SolidBrush(Color.FromArgb(Math.Max(1, ((SolidBrush)brush).Color.A * 45 / 100), ((SolidBrush)brush).Color)))
                    g.FillEllipse(half, 1.75f, 1.75f, 12.5f, 12.5f);
                g.DrawEllipse(pen, 1.75f, 1.75f, 12.5f, 12.5f);
                break;

            case Glyph.Contrast:
                g.DrawEllipse(pen, 1.75f, 1.75f, 12.5f, 12.5f);
                using (var p = new GraphicsPath())
                {
                    p.AddArc(1.75f, 1.75f, 12.5f, 12.5f, -90, 180);
                    p.CloseFigure();
                    g.FillPath(brush, p);
                }
                break;

            case Glyph.PageSingle:
                using (var p = Rounded(new RectangleF(3.25f, 1.75f, 9.5f, 12.5f), 1.5f)) g.DrawPath(pen, p);
                g.DrawLine(pen, 5.75f, 5.5f, 10.25f, 5.5f);
                g.DrawLine(pen, 5.75f, 8f, 10.25f, 8f);
                g.DrawLine(pen, 5.75f, 10.5f, 8.75f, 10.5f);
                break;

            case Glyph.PageDuplex:
                using (var p = Rounded(new RectangleF(1.75f, 3.75f, 8.5f, 10.5f), 1.5f)) g.DrawPath(pen, p);
                g.DrawLines(pen, new PointF[] { new(4.75f, 3.75f), new(4.75f, 3.25f), new(5.25f, 1.75f), new(12.75f, 1.75f), new(14.25f, 3.25f), new(14.25f, 10.75f), new(12.75f, 12.25f), new(10.25f, 12.25f) });
                g.DrawLine(pen, 4f, 7.25f, 8f, 7.25f);
                g.DrawLine(pen, 4f, 9.75f, 7f, 9.75f);
                break;

            case Glyph.ShieldCheck:
                using (var p = new GraphicsPath())
                {
                    p.AddLine(8f, 1.5f, 13.75f, 3.5f);
                    p.AddBezier(13.75f, 3.5f, 13.75f, 9.5f, 11.5f, 12.5f, 8f, 14.5f);
                    p.AddBezier(8f, 14.5f, 4.5f, 12.5f, 2.25f, 9.5f, 2.25f, 3.5f);
                    p.CloseFigure();
                    g.DrawPath(pen, p);
                }
                g.DrawLines(pen, new PointF[] { new(5.25f, 8f), new(7.25f, 10f), new(10.75f, 6.25f) });
                break;

            case Glyph.FileSmall:
                g.DrawPolygon(pen, new PointF[]
                {
                    new(3.25f, 1.75f), new(9.25f, 1.75f), new(12.75f, 5.25f), new(12.75f, 14.25f), new(3.25f, 14.25f)
                });
                g.DrawLines(pen, new PointF[] { new(9.25f, 1.75f), new(9.25f, 5.25f), new(12.75f, 5.25f) });
                g.DrawLine(pen, 8f, 6.75f, 8f, 11.75f);
                g.DrawLines(pen, new PointF[] { new(5.75f, 9.5f), new(8f, 11.75f), new(10.25f, 9.5f) });
                break;

            case Glyph.ImageHq:
                using (var p = Rounded(new RectangleF(1.75f, 3.75f, 10.5f, 10.5f), 1.75f)) g.DrawPath(pen, p);
                g.DrawLines(pen, new PointF[] { new(2.5f, 13f), new(5.75f, 9.25f), new(8.25f, 11.5f), new(9.5f, 10.5f), new(11.5f, 12.5f) });
                using (var p = new GraphicsPath())
                {
                    p.AddBezier(12.5f, 0.75f, 12.8f, 2.4f, 13.1f, 2.7f, 14.75f, 3f);
                    p.AddBezier(14.75f, 3f, 13.1f, 3.3f, 12.8f, 3.6f, 12.5f, 5.25f);
                    p.AddBezier(12.5f, 5.25f, 12.2f, 3.6f, 11.9f, 3.3f, 10.25f, 3f);
                    p.AddBezier(10.25f, 3f, 11.9f, 2.7f, 12.2f, 2.4f, 12.5f, 0.75f);
                    p.CloseFigure();
                    g.FillPath(brush, p);
                }
                break;

            case Glyph.Level1:
            case Glyph.Level2:
            case Glyph.Level3:
            case Glyph.Level4:
            {
                int level = glyph - Glyph.Level1 + 1;
                using var thin = new Pen(((Pen)pen).Color, 1.25f) { LineJoin = LineJoin.Round };
                for (int i = 0; i < 4; i++)
                {
                    float h = 4.5f + i * 3f;
                    var bar = new RectangleF(1.75f + i * 3.4f, 14.25f - h, 2.4f, h);
                    using var path = Rounded(bar, 0.6f);
                    if (i < level) g.FillPath(brush, path);
                    else g.DrawPath(thin, path);
                }
                break;
            }

            case Glyph.Globe:
                g.DrawEllipse(pen, 1.75f, 1.75f, 12.5f, 12.5f);
                g.DrawEllipse(pen, 5f, 1.75f, 6f, 12.5f);
                g.DrawLine(pen, 2f, 6f, 14f, 6f);
                g.DrawLine(pen, 2f, 10f, 14f, 10f);
                break;

            case Glyph.Award:
            {
                // Ribbons first, the medal on top
                g.DrawLines(pen, new PointF[] { new(5.5f, 9.5f), new(4f, 14.75f), new(6.25f, 13.75f), new(7.5f, 15f), new(8f, 11.25f) });
                g.DrawLines(pen, new PointF[] { new(10.5f, 9.5f), new(12f, 14.75f), new(9.75f, 13.75f), new(8.5f, 15f), new(8f, 11.25f) });
                g.DrawEllipse(pen, 3f, 0.75f, 10f, 10f);
                var star = new PointF[10];
                for (int i = 0; i < 10; i++)
                {
                    double a = -Math.PI / 2 + i * Math.PI / 5;
                    float r = i % 2 == 0 ? 3.1f : 1.35f;
                    star[i] = new PointF(8f + (float)Math.Cos(a) * r, 6f + (float)Math.Sin(a) * r);
                }
                g.FillPolygon(brush, star);
                break;
            }

            case Glyph.FileZip:
                // Narrow document, arrows pressing from both sides
                g.DrawPolygon(pen, new PointF[] { new(5.5f, 1.75f), new(9f, 1.75f), new(10.5f, 3.25f), new(10.5f, 14.25f), new(5.5f, 14.25f) });
                g.DrawLine(pen, 7f, 5.5f, 9f, 5.5f);
                g.DrawLine(pen, 7f, 7.75f, 9f, 7.75f);
                g.DrawLine(pen, 7f, 10f, 9f, 10f);
                g.DrawLine(pen, 0.75f, 8f, 3.5f, 8f);
                g.DrawLines(pen, new PointF[] { new(2.1f, 6.4f), new(3.7f, 8f), new(2.1f, 9.6f) });
                g.DrawLine(pen, 15.25f, 8f, 12.5f, 8f);
                g.DrawLines(pen, new PointF[] { new(13.9f, 6.4f), new(12.3f, 8f), new(13.9f, 9.6f) });
                break;

            case Glyph.Rotate:
                g.DrawArc(pen, 2.25f, 2.25f, 11.5f, 11.5f, -50, 290);
                g.DrawLines(pen, new PointF[] { new(13.9f, 2f), new(12.9f, 5.4f), new(9.5f, 4.5f) });
                break;

            case Glyph.Skew:
                g.DrawPolygon(pen, new PointF[] { new(6.5f, 1.5f), new(14.25f, 3.25f), new(12f, 14.5f), new(4.25f, 12.75f) });
                g.DrawLine(pen, 1.5f, 1.5f, 1.5f, 14.5f);
                break;

            case Glyph.PageBlank:
                using (var p = Rounded(new RectangleF(3.25f, 1.75f, 9.5f, 12.5f), 1.5f)) g.DrawPath(pen, p);
                g.DrawLine(pen, 1.5f, 14.5f, 14.5f, 1.5f);
                break;

            case Glyph.Lock:
                using (var p = Rounded(new RectangleF(2.5f, 7f, 11f, 7.75f), 1.75f)) g.DrawPath(pen, p);
                g.DrawArc(pen, 4.75f, 1.75f, 6.5f, 8f, 180, 180);
                g.DrawLine(pen, 4.75f, 5.75f, 4.75f, 7f);
                g.DrawLine(pen, 11.25f, 5.75f, 11.25f, 7f);
                g.FillEllipse(brush, 7f, 9.25f, 2f, 2f);
                g.DrawLine(pen, 8f, 10.75f, 8f, 12.5f);
                break;

            case Glyph.Sun:
                g.DrawEllipse(pen, 5f, 5f, 6f, 6f);
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4;
                    g.DrawLine(pen,
                        (float)(8 + Math.Cos(a) * 5.1), (float)(8 + Math.Sin(a) * 5.1),
                        (float)(8 + Math.Cos(a) * 6.75), (float)(8 + Math.Sin(a) * 6.75));
                }
                break;

            case Glyph.Clock:
                g.DrawEllipse(pen, 1.75f, 1.75f, 12.5f, 12.5f);
                g.DrawLines(pen, new PointF[] { new(8f, 4.5f), new(8f, 8f), new(10.5f, 9.75f) });
                break;

            case Glyph.Hash:
                g.DrawLine(pen, 6.25f, 2f, 5f, 14f);
                g.DrawLine(pen, 11f, 2f, 9.75f, 14f);
                g.DrawLine(pen, 2.5f, 5.75f, 14f, 5.75f);
                g.DrawLine(pen, 2f, 10.25f, 13.5f, 10.25f);
                break;

            case Glyph.Split:
                using (var p = Rounded(new RectangleF(1.5f, 2.5f, 5.25f, 11f), 1.25f)) g.DrawPath(pen, p);
                using (var p = Rounded(new RectangleF(9.25f, 2.5f, 5.25f, 11f), 1.25f)) g.DrawPath(pen, p);
                using (var dash = new Pen(pen.Color, pen.Width * 0.8f) { DashPattern = new[] { 1.2f, 1.4f } })
                    g.DrawLine(dash, 8f, 0.75f, 8f, 15.25f);
                break;

            case Glyph.Moon:
                using (var p = new GraphicsPath())
                {
                    p.AddArc(2f, 2f, 12f, 12f, -70, 290);
                    p.AddArc(5.25f, 0.5f, 9.5f, 9.5f, 140, -210);
                    p.CloseFigure();
                    g.DrawPath(pen, p);
                }
                break;

            case Glyph.Monitor:
                using (var p = Rounded(new RectangleF(1.5f, 2.25f, 13f, 9f), 1.5f)) g.DrawPath(pen, p);
                g.DrawLine(pen, 8f, 11.25f, 8f, 13.75f);
                g.DrawLine(pen, 5f, 13.75f, 11f, 13.75f);
                break;

            case Glyph.TextField:
                using (var p = Rounded(new RectangleF(1.5f, 4f, 13f, 8f), 1.75f)) g.DrawPath(pen, p);
                g.DrawLine(pen, 4.5f, 6.25f, 4.5f, 9.75f);
                g.DrawLine(pen, 6.75f, 8f, 11.5f, 8f);
                break;

            case Glyph.OpenExternal:
                g.DrawLines(pen, new PointF[] { new(7f, 2.75f), new(3.75f, 2.75f), new(2.75f, 3.75f), new(2.75f, 12.25f), new(3.75f, 13.25f), new(12.25f, 13.25f), new(13.25f, 12.25f), new(13.25f, 9f) });
                g.DrawLine(pen, 7.5f, 8.5f, 13.25f, 2.75f);
                g.DrawLines(pen, new PointF[] { new(9.5f, 2.75f), new(13.25f, 2.75f), new(13.25f, 6.5f) });
                break;

            case Glyph.Download:
                g.DrawLine(pen, 8f, 2f, 8f, 10f);
                g.DrawLines(pen, new PointF[] { new(4.75f, 7f), new(8f, 10.25f), new(11.25f, 7f) });
                g.DrawLines(pen, new PointF[] { new(2.5f, 10.5f), new(2.5f, 12.75f), new(3.25f, 13.5f), new(12.75f, 13.5f), new(13.5f, 12.75f), new(13.5f, 10.5f) });
                break;
        }
    }

    /// <summary>Path of a rectangle with rounded corners.</summary>
    public static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0.5f)
        {
            path.AddRectangle(r);
            return path;
        }
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
