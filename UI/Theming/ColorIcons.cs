using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace SpeedScanManager;

/// <summary>
/// Color palette of the colorful icons (Win11 / Discord settings style: small rounded tiles
/// in a saturated color with a white line glyph). The same colors work on light and dark.
/// </summary>
internal static class IconColors
{
    public static readonly Color Blue = Color.FromArgb(0x3B, 0x82, 0xF6);
    public static readonly Color Sky = Color.FromArgb(0x0E, 0xA5, 0xE9);
    public static readonly Color Indigo = Color.FromArgb(0x63, 0x66, 0xF1);
    public static readonly Color Purple = Color.FromArgb(0x8B, 0x5C, 0xF6);
    public static readonly Color Pink = Color.FromArgb(0xEC, 0x48, 0x99);
    public static readonly Color Red = Color.FromArgb(0xE5, 0x48, 0x4D);
    public static readonly Color Orange = Color.FromArgb(0xF9, 0x73, 0x16);
    public static readonly Color Amber = Color.FromArgb(0xF5, 0x9E, 0x0B);
    public static readonly Color Green = Color.FromArgb(0x16, 0xA3, 0x4A);
    public static readonly Color Emerald = Color.FromArgb(0x10, 0xB9, 0x81);
    public static readonly Color Teal = Color.FromArgb(0x14, 0xB8, 0xA6);
    public static readonly Color Cyan = Color.FromArgb(0x06, 0xB6, 0xD4);
    public static readonly Color Slate = Color.FromArgb(0x64, 0x74, 0x8B);
    public static readonly Color Graphite = Color.FromArgb(0x3F, 0x46, 0x53);

    // Office-like brand-neutral colors for the document targets
    public static readonly Color WordBlue = Color.FromArgb(0x2B, 0x6C, 0xD8);
    public static readonly Color ExcelGreen = Color.FromArgb(0x1F, 0x8F, 0x55);
    public static readonly Color SlidesOrange = Color.FromArgb(0xE0, 0x5A, 0x2B);
}

/// <summary>
/// A colorful icon that can be drawn at any size (combo items, sidebar, setting rows, window
/// headers, tiles). Disabled icons are drawn faded and desaturated.
/// </summary>
internal sealed class ColorIcon
{
    private readonly Action<Graphics, RectangleF> _draw;

    private ColorIcon(Action<Graphics, RectangleF> draw) => _draw = draw;

    /// <summary>Rounded tile in <paramref name="color"/> with a white <paramref name="glyph"/>.</summary>
    public static ColorIcon Tile(Glyph glyph, Color color) => new((g, r) => ColorIcons.DrawTile(g, r, color, glyph));

    /// <summary>Tile with a diagonal gradient from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static ColorIcon Gradient(Glyph glyph, Color from, Color to, bool sparkle = false)
        => new((g, r) => ColorIcons.DrawTile(g, r, from, glyph, to, sparkle));

    /// <summary>"Automatic" icon used everywhere: violet/indigo gradient tile with a magic wand.</summary>
    public static ColorIcon Auto { get; } = Gradient(Glyph.Wand, IconColors.Purple, IconColors.Indigo);

    /// <summary>Rounded tile with a short white text (e.g. "W" for Word).</summary>
    public static ColorIcon Letter(string text, Color color) => new((g, r) => ColorIcons.DrawLetterTile(g, r, color, text));

    /// <summary>Any custom drawing (flags, file-type documents).</summary>
    public static ColorIcon Custom(Action<Graphics, RectangleF> draw) => new(draw);

    /// <summary>Draws the icon into <paramref name="bounds"/> (square recommended).</summary>
    public void Draw(Graphics g, RectangleF bounds, bool enabled = true)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        if (enabled)
        {
            var state = g.Save();
            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                _draw(g, bounds);
            }
            finally
            {
                g.Restore(state);
            }
            return;
        }

        // Disabled: render off-screen, then draw desaturated and faded
        int w = (int)Math.Ceiling(bounds.Width) + 2, h = (int)Math.Ceiling(bounds.Height) + 2;
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var bg = Graphics.FromImage(bmp))
        {
            bg.SmoothingMode = SmoothingMode.AntiAlias;
            bg.PixelOffsetMode = PixelOffsetMode.HighQuality;
            bg.Clear(Color.Transparent);
            _draw(bg, new RectangleF(1, 1, bounds.Width, bounds.Height));
        }
        const float r = 0.299f * 0.7f, gr = 0.587f * 0.7f, b = 0.114f * 0.7f;
        var matrix = new ColorMatrix(new[]
        {
            new[] { r + 0.3f, r, r, 0f, 0f },
            new[] { gr, gr + 0.3f, gr, 0f, 0f },
            new[] { b, b, b + 0.3f, 0f, 0f },
            new[] { 0f, 0f, 0f, 0.42f, 0f },
            new[] { 0f, 0f, 0f, 0f, 1f }
        });
        using var ia = new ImageAttributes();
        ia.SetColorMatrix(matrix);
        var dest = new Rectangle((int)Math.Round(bounds.X) - 1, (int)Math.Round(bounds.Y) - 1, w, h);
        g.DrawImage(bmp, dest, 0, 0, w, h, GraphicsUnit.Pixel, ia);
    }

    /// <summary>Renders the icon into a new bitmap (for APIs that need an Image).</summary>
    public Bitmap ToBitmap(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        Draw(g, new RectangleF(0, 0, size, size));
        return bmp;
    }
}

/// <summary>Drawing routines of the colorful icons.</summary>
internal static class ColorIcons
{
    public static Color Lighten(Color c, float amount) => ThemePalette.Blend(c, Color.White, amount);
    public static Color Darken(Color c, float amount) => ThemePalette.Blend(c, Color.Black, amount);

    /// <summary>Corner radius of a tile of size <paramref name="size"/>.</summary>
    private static float Radius(float size) => Math.Max(2f, size * 0.27f);

    /// <summary>
    /// Rounded tile with a soft vertical gradient (or a diagonal one from <paramref name="color"/>
    /// to <paramref name="to"/>) and a white glyph; optional yellow sparkle in the top right.
    /// </summary>
    public static void DrawTile(Graphics g, RectangleF r, Color color, Glyph glyph, Color? to = null, bool sparkle = false)
    {
        float size = Math.Min(r.Width, r.Height);
        var tile = new RectangleF(r.X + (r.Width - size) / 2f, r.Y + (r.Height - size) / 2f, size, size);
        FillTile(g, tile, color, to);

        float gs = size * (size <= 26 ? 0.7f : 0.6f);
        float weight = size <= 18 ? 1.3f : size <= 26 ? 1.25f : size <= 40 ? 1.15f : 1.05f;
        var gr = new RectangleF(tile.X + (size - gs) / 2f, tile.Y + (size - gs) / 2f, gs, gs);
        ThemeGlyphs.Draw(g, glyph, gr, Color.White, weight);

        if (sparkle)
            DrawSparkle(g, new PointF(tile.Right - size * 0.2f, tile.Y + size * 0.2f), size * 0.2f, Color.FromArgb(0xFF, 0xD6, 0x4A));
    }

    /// <summary>The tile background only.</summary>
    public static void FillTile(Graphics g, RectangleF tile, Color color, Color? to = null)
    {
        float size = Math.Min(tile.Width, tile.Height);
        using var path = ThemeGlyphs.Rounded(tile, Radius(size));
        var gradRect = RectangleF.Inflate(tile, 1, 1);
        using (var brush = to.HasValue
                   ? new LinearGradientBrush(gradRect, Lighten(color, 0.08f), to.Value, LinearGradientMode.ForwardDiagonal)
                   : new LinearGradientBrush(gradRect, Lighten(color, 0.16f), Darken(color, 0.06f), LinearGradientMode.Vertical))
            g.FillPath(brush, path);
        // Subtle top highlight and edge for depth
        using (var pen = new Pen(Color.FromArgb(size >= 28 ? 40 : 28, Color.White), 1f))
        using (var hl = ThemeGlyphs.Rounded(RectangleF.Inflate(tile, -0.5f, -0.5f), Radius(size) - 0.5f))
            g.DrawPath(pen, hl);
    }

    /// <summary>Four-point star (sparkle) centered at <paramref name="c"/>.</summary>
    public static void DrawSparkle(Graphics g, PointF c, float radius, Color color)
    {
        using var path = new GraphicsPath();
        float k = radius * 0.28f;
        path.AddBezier(c.X, c.Y - radius, c.X + k * 0.3f, c.Y - k, c.X + k, c.Y - k * 0.3f, c.X + radius, c.Y);
        path.AddBezier(c.X + radius, c.Y, c.X + k, c.Y + k * 0.3f, c.X + k * 0.3f, c.Y + k, c.X, c.Y + radius);
        path.AddBezier(c.X, c.Y + radius, c.X - k * 0.3f, c.Y + k, c.X - k, c.Y + k * 0.3f, c.X - radius, c.Y);
        path.AddBezier(c.X - radius, c.Y, c.X - k, c.Y - k * 0.3f, c.X - k * 0.3f, c.Y - k, c.X, c.Y - radius);
        path.CloseFigure();
        using (var shadow = new Pen(Color.FromArgb(70, 0, 0, 0), Math.Max(1f, radius * 0.25f)))
            g.DrawPath(shadow, path);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    /// <summary>Tile with a short bold white text (document targets: W, X, P).</summary>
    public static void DrawLetterTile(Graphics g, RectangleF r, Color color, string text)
    {
        float size = Math.Min(r.Width, r.Height);
        var tile = new RectangleF(r.X + (r.Width - size) / 2f, r.Y + (r.Height - size) / 2f, size, size);
        FillTile(g, tile, color);
        float px = size * (text.Length <= 1 ? 0.56f : 0.36f);
        DrawCenteredText(g, text, px, tile, Color.White);
    }

    private static void DrawCenteredText(Graphics g, string text, float pixelSize, RectangleF rect, Color color)
    {
        using var font = new Font("Segoe UI", Math.Max(4f, pixelSize), FontStyle.Bold, GraphicsUnit.Pixel);
        using var sf = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip
        };
        var hint = g.TextRenderingHint;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var brush = new SolidBrush(color);
        // Typographic format: optically center (cap height sits a bit high)
        g.DrawString(text, font, brush, new RectangleF(rect.X, rect.Y + pixelSize * 0.04f, rect.Width, rect.Height), sf);
        g.TextRenderingHint = hint;
    }

    // ───────────────────────── Scan targets ─────────────────────────

    private static readonly Dictionary<ApplicationType, ColorIcon> TargetIcons = new()
    {
        [ApplicationType.ScanToFolder] = ColorIcon.Tile(Glyph.Folder, IconColors.Amber),
        [ApplicationType.ScanToEmail] = ColorIcon.Tile(Glyph.Mail, IconColors.Blue),
        [ApplicationType.ScanToPrint] = ColorIcon.Tile(Glyph.Printer, IconColors.Slate),
        [ApplicationType.ScanToWord] = ColorIcon.Letter("W", IconColors.WordBlue),
        [ApplicationType.ScanToExcel] = ColorIcon.Letter("X", IconColors.ExcelGreen),
        [ApplicationType.ScanToPowerPoint] = ColorIcon.Letter("P", IconColors.SlidesOrange),
        [ApplicationType.ScanPictureFolder] = ColorIcon.Tile(Glyph.Image, IconColors.Teal),
        [ApplicationType.EditWithPdf] = ColorIcon.Letter("PDF", IconColors.Red),
        [ApplicationType.Custom] = ColorIcon.Tile(Glyph.Apps, IconColors.Purple)
    };

    /// <summary>Icon of a scan target (Anwendung list).</summary>
    public static ColorIcon Target(ApplicationType type)
        => TargetIcons.TryGetValue(type, out var icon) ? icon : TargetIcons[ApplicationType.Custom];

    // ───────────────────────── File types ─────────────────────────

    /// <summary>
    /// Document with a folded corner and a colored label band (PDF red, JPEG green, PNG blue);
    /// image types show a small landscape, PDF text lines.
    /// </summary>
    public static void DrawFileDocument(Graphics g, RectangleF r, string label, Color color, bool picture)
    {
        float h = Math.Min(r.Height, r.Width / 0.86f);
        float w = h * 0.78f;
        var doc = new RectangleF(r.X + (r.Width - w) / 2f, r.Y + (r.Height - h) / 2f, w, h);
        float fold = w * 0.34f;
        float rad = Math.Max(1.2f, h * 0.08f);

        using var body = new GraphicsPath();
        body.AddArc(doc.X, doc.Y, rad * 2, rad * 2, 180, 90);
        body.AddLine(doc.X + rad, doc.Y, doc.Right - fold, doc.Y);
        body.AddLine(doc.Right - fold, doc.Y, doc.Right, doc.Y + fold);
        body.AddArc(doc.Right - rad * 2, doc.Bottom - rad * 2, rad * 2, rad * 2, 0, 90);
        body.AddArc(doc.X, doc.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
        body.CloseFigure();

        using (var brush = new SolidBrush(Color.FromArgb(0xFB, 0xFB, 0xFC)))
            g.FillPath(brush, body);
        using (var pen = new Pen(Darken(color, 0.1f), Math.Max(1f, h * 0.045f)))
            g.DrawPath(pen, body);

        // Folded corner
        using (var corner = new GraphicsPath())
        {
            corner.AddLine(doc.Right - fold, doc.Y, doc.Right - fold, doc.Y + fold - rad * 0.5f);
            corner.AddLine(doc.Right - fold, doc.Y + fold, doc.Right, doc.Y + fold);
            corner.CloseFigure();
            using var brush = new SolidBrush(Lighten(color, 0.45f));
            g.FillPath(brush, corner);
            using var pen = new Pen(Darken(color, 0.1f), Math.Max(1f, h * 0.045f)) { LineJoin = LineJoin.Round };
            g.DrawPath(pen, corner);
        }

        // Content above the band
        float cx = doc.X + w * 0.18f, cw = w * 0.5f;
        float top = doc.Y + h * 0.16f;
        if (picture)
        {
            using var mountain = new SolidBrush(color);
            g.FillPolygon(mountain, new[]
            {
                new PointF(cx, top + h * 0.32f), new PointF(cx + cw * 0.38f, top + h * 0.08f),
                new PointF(cx + cw * 0.62f, top + h * 0.22f), new PointF(cx + cw * 0.8f, top + h * 0.12f),
                new PointF(cx + cw * 1.15f, top + h * 0.32f)
            });
            using var sun = new SolidBrush(Color.FromArgb(0xF5, 0xB0, 0x0B));
            float sd = h * 0.1f;
            g.FillEllipse(sun, cx + cw * 0.02f, top - sd * 0.35f, sd, sd);
        }
        else
        {
            using var line = new Pen(Lighten(Color.FromArgb(0x6B, 0x72, 0x80), 0.2f), Math.Max(1f, h * 0.05f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            for (int i = 0; i < 3; i++)
            {
                float y = top + h * 0.06f + i * h * 0.1f;
                g.DrawLine(line, cx, y, cx + (i == 2 ? cw * 0.6f : cw), y);
            }
        }

        // Label band (slightly wider than the page on the left, like classic file icons)
        float bandH = h * 0.36f;
        var band = new RectangleF(doc.X - w * 0.1f, doc.Bottom - bandH - h * 0.1f, w * 0.92f, bandH);
        using (var path = ThemeGlyphs.Rounded(band, Math.Max(1f, bandH * 0.22f)))
        using (var brush = new LinearGradientBrush(RectangleF.Inflate(band, 0, 1), Lighten(color, 0.1f), Darken(color, 0.06f), LinearGradientMode.Vertical))
            g.FillPath(brush, path);
        DrawCenteredText(g, label, bandH * 0.78f, band, Color.White);
    }

    public static ColorIcon Pdf { get; } = ColorIcon.Custom((g, r) => DrawFileDocument(g, r, "PDF", IconColors.Red, false));
    public static ColorIcon Jpeg { get; } = ColorIcon.Custom((g, r) => DrawFileDocument(g, r, "JPG", IconColors.Green, true));
    public static ColorIcon Png { get; } = ColorIcon.Custom((g, r) => DrawFileDocument(g, r, "PNG", IconColors.Blue, true));

    // ───────────────────────── Flags ─────────────────────────

    /// <summary>
    /// Crisp rounded flag (3:2) for an OCR language with a thin frame and a soft shadow.
    /// "Automatisch" draws a globe tile with a sparkle.
    /// </summary>
    public static void DrawFlag(Graphics g, RectangleF r, OcrLanguage language)
    {
        if (language == OcrLanguage.Automatisch)
        {
            DrawTile(g, r, IconColors.Sky, Glyph.Globe, IconColors.Purple, sparkle: true);
            return;
        }
        DrawFlagFrame(g, r, f => DrawFlagContent(g, f, language));
    }

    /// <summary>Flag of an app language (<see cref="L.Languages"/> code), e.g. "pt-BR" → Brazil.</summary>
    public static void DrawFlag(Graphics g, RectangleF r, string languageCode) =>
        DrawFlagFrame(g, r, f => DrawLanguageFlagContent(g, f, languageCode));

    public static ColorIcon Flag(string languageCode) => ColorIcon.Custom((g, r) => DrawFlag(g, r, languageCode));

    /// <summary>Rounded 3:2 frame with shadow, gloss and border around <paramref name="content"/>.</summary>
    private static void DrawFlagFrame(Graphics g, RectangleF r, Action<RectangleF> content)
    {
        float w = r.Width, h = Math.Min(r.Height, (float)Math.Round(w * 0.7f));
        var f = new RectangleF(r.X, r.Y + (r.Height - h) / 2f, w, h);
        float rad = Math.Max(1.5f, h * 0.18f);
        using var path = ThemeGlyphs.Rounded(f, rad);

        // Shadow
        using (var shadow = ThemeGlyphs.Rounded(new RectangleF(f.X, f.Y + 1f, f.Width, f.Height), rad))
        using (var brush = new SolidBrush(Color.FromArgb(ThemeManager.IsDark ? 90 : 38, 0, 0, 0)))
            g.FillPath(brush, shadow);

        var state = g.Save();
        g.SetClip(path, CombineMode.Intersect);
        content(f);
        g.Restore(state);

        // Gloss and frame
        using (var gloss = new LinearGradientBrush(RectangleF.Inflate(f, 0, 1), Color.FromArgb(46, Color.White), Color.FromArgb(0, Color.White), LinearGradientMode.Vertical))
        using (var top = ThemeGlyphs.Rounded(new RectangleF(f.X, f.Y, f.Width, f.Height * 0.5f), rad))
            g.FillPath(gloss, top);
        using var pen = new Pen(ThemeManager.IsDark ? Color.FromArgb(70, 255, 255, 255) : Color.FromArgb(60, 0, 0, 0), 1f);
        using var frame = ThemeGlyphs.Rounded(RectangleF.Inflate(f, -0.5f, -0.5f), rad - 0.5f);
        g.DrawPath(pen, frame);
    }

    public static ColorIcon Flag(OcrLanguage language) => ColorIcon.Custom((g, r) => DrawFlag(g, r, language));

    private static void Stripes(Graphics g, RectangleF f, bool vertical, params Color[] colors)
    {
        for (int i = 0; i < colors.Length; i++)
        {
            using var brush = new SolidBrush(colors[i]);
            if (vertical)
            {
                float sw = f.Width / colors.Length;
                g.FillRectangle(brush, f.X + i * sw - 0.5f, f.Y, sw + 1f, f.Height);
            }
            else
            {
                float sh = f.Height / colors.Length;
                g.FillRectangle(brush, f.X, f.Y + i * sh - 0.5f, f.Width, sh + 1f);
            }
        }
    }

    private static void Star(Graphics g, float cx, float cy, float radius, Color color, double rotation = 0)
    {
        var pts = new PointF[10];
        for (int i = 0; i < 10; i++)
        {
            double a = -Math.PI / 2 + rotation + i * Math.PI / 5;
            float rr = i % 2 == 0 ? radius : radius * 0.42f;
            pts[i] = new PointF(cx + (float)Math.Cos(a) * rr, cy + (float)Math.Sin(a) * rr);
        }
        using var brush = new SolidBrush(color);
        g.FillPolygon(brush, pts);
    }

    private static void DrawLanguageFlagContent(Graphics g, RectangleF f, string code)
    {
        float w = f.Width, h = f.Height;
        switch (code)
        {
            case "en-US":
            {
                var red = Color.FromArgb(0xB2, 0x22, 0x34);
                var stripes = new Color[7];
                for (int i = 0; i < 7; i++) stripes[i] = i % 2 == 0 ? red : Color.White;
                Stripes(g, f, false, stripes);
                using (var b = new SolidBrush(Color.FromArgb(0x3C, 0x3B, 0x6E))) g.FillRectangle(b, f.X, f.Y, w * 0.44f, h * 4f / 7f);
                break;
            }
            case "pt-BR":
            {
                using (var b = new SolidBrush(Color.FromArgb(0x00, 0x9C, 0x3B))) g.FillRectangle(b, f);
                float mx = w * 0.1f, my = h * 0.14f;
                using (var b = new SolidBrush(Color.FromArgb(0xFF, 0xDF, 0x00)))
                    g.FillPolygon(b, new[] { new PointF(f.X + w / 2f, f.Y + my), new PointF(f.Right - mx, f.Y + h / 2f),
                                             new PointF(f.X + w / 2f, f.Bottom - my), new PointF(f.X + mx, f.Y + h / 2f) });
                float d = h * 0.44f;
                using (var b = new SolidBrush(Color.FromArgb(0x00, 0x27, 0x76))) g.FillEllipse(b, f.X + (w - d) / 2f, f.Y + (h - d) / 2f, d, d);
                break;
            }
            case "pl-PL":
                Stripes(g, f, false, Color.White, Color.FromArgb(0xDC, 0x14, 0x3C));
                break;
            case "nl-NL":
                Stripes(g, f, false, Color.FromArgb(0xAE, 0x1C, 0x28), Color.White, Color.FromArgb(0x21, 0x46, 0x8B));
                break;
            case "tr-TR":
            {
                var red = Color.FromArgb(0xE3, 0x0A, 0x17);
                using (var b = new SolidBrush(red)) g.FillRectangle(b, f);
                float d = h * 0.5f, cx = f.X + w * 0.38f, cy = f.Y + h / 2f;
                using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, cx - d / 2f, cy - d / 2f, d, d);
                float d2 = d * 0.8f;
                using (var b = new SolidBrush(red)) g.FillEllipse(b, cx - d2 / 2f + d * 0.12f, cy - d2 / 2f, d2, d2);
                Star(g, cx + d * 0.5f, cy, h * 0.13f, Color.White, -Math.PI / 2);
                break;
            }
            default:
                DrawFlagContent(g, f, code switch
                {
                    "de-DE" => OcrLanguage.Deutsch,
                    "ja-JP" => OcrLanguage.Japanisch,
                    "fr-FR" => OcrLanguage.Franzoesisch,
                    "es-ES" => OcrLanguage.Spanisch,
                    "zh-CN" => OcrLanguage.ChinesischVereinfacht,
                    "zh-TW" => OcrLanguage.ChinesischTraditionell,
                    "ru-RU" => OcrLanguage.Russisch,
                    "ko-KR" => OcrLanguage.Koreanisch,
                    "it-IT" => OcrLanguage.Italienisch,
                    _ => OcrLanguage.English,
                });
                break;
        }
    }

    private static void DrawFlagContent(Graphics g, RectangleF f, OcrLanguage language)
    {
        float w = f.Width, h = f.Height;
        switch (language)
        {
            case OcrLanguage.Deutsch:
                Stripes(g, f, false, Color.FromArgb(0x1A, 0x1A, 0x1A), Color.FromArgb(0xDD, 0x00, 0x00), Color.FromArgb(0xFF, 0xCE, 0x00));
                break;
            case OcrLanguage.Japanisch:
                using (var b = new SolidBrush(Color.White)) g.FillRectangle(b, f);
                using (var b = new SolidBrush(Color.FromArgb(0xBC, 0x00, 0x2D)))
                {
                    float d = h * 0.6f;
                    g.FillEllipse(b, f.X + (w - d) / 2f, f.Y + (h - d) / 2f, d, d);
                }
                break;
            case OcrLanguage.English:
            {
                using (var b = new SolidBrush(Color.FromArgb(0x01, 0x21, 0x69))) g.FillRectangle(b, f);
                using (var white = new Pen(Color.White, h * 0.22f))
                {
                    g.DrawLine(white, f.X, f.Y, f.Right, f.Bottom);
                    g.DrawLine(white, f.Right, f.Y, f.X, f.Bottom);
                }
                using (var red = new Pen(Color.FromArgb(0xC8, 0x10, 0x2E), h * 0.08f))
                {
                    g.DrawLine(red, f.X, f.Y, f.Right, f.Bottom);
                    g.DrawLine(red, f.Right, f.Y, f.X, f.Bottom);
                }
                using (var b = new SolidBrush(Color.White))
                {
                    g.FillRectangle(b, f.X + w / 2f - h * 0.18f, f.Y, h * 0.36f, h);
                    g.FillRectangle(b, f.X, f.Y + h / 2f - h * 0.18f, w, h * 0.36f);
                }
                using (var b = new SolidBrush(Color.FromArgb(0xC8, 0x10, 0x2E)))
                {
                    g.FillRectangle(b, f.X + w / 2f - h * 0.1f, f.Y, h * 0.2f, h);
                    g.FillRectangle(b, f.X, f.Y + h / 2f - h * 0.1f, w, h * 0.2f);
                }
                break;
            }
            case OcrLanguage.Franzoesisch:
                Stripes(g, f, true, Color.FromArgb(0x00, 0x55, 0xA4), Color.White, Color.FromArgb(0xEF, 0x41, 0x35));
                break;
            case OcrLanguage.Italienisch:
                Stripes(g, f, true, Color.FromArgb(0x00, 0x92, 0x46), Color.White, Color.FromArgb(0xCE, 0x2B, 0x37));
                break;
            case OcrLanguage.Spanisch:
            {
                var red = Color.FromArgb(0xC6, 0x0B, 0x1E);
                Stripes(g, f, false, red, Color.FromArgb(0xFF, 0xC4, 0x00), Color.FromArgb(0xFF, 0xC4, 0x00), red);
                break;
            }
            case OcrLanguage.ChinesischVereinfacht:
            {
                using (var b = new SolidBrush(Color.FromArgb(0xDE, 0x29, 0x10))) g.FillRectangle(b, f);
                var yellow = Color.FromArgb(0xFF, 0xDE, 0x00);
                Star(g, f.X + w * 0.2f, f.Y + h * 0.32f, h * 0.2f, yellow);
                Star(g, f.X + w * 0.36f, f.Y + h * 0.12f, h * 0.07f, yellow, 0.4);
                Star(g, f.X + w * 0.43f, f.Y + h * 0.25f, h * 0.07f, yellow, 0.8);
                Star(g, f.X + w * 0.43f, f.Y + h * 0.42f, h * 0.07f, yellow);
                Star(g, f.X + w * 0.36f, f.Y + h * 0.54f, h * 0.07f, yellow, 0.4);
                break;
            }
            case OcrLanguage.ChinesischTraditionell:
            {
                using (var b = new SolidBrush(Color.FromArgb(0xFE, 0x00, 0x00))) g.FillRectangle(b, f);
                using (var b = new SolidBrush(Color.FromArgb(0x00, 0x00, 0x95))) g.FillRectangle(b, f.X, f.Y, w / 2f, h / 2f);
                using (var b = new SolidBrush(Color.White))
                {
                    float d = h * 0.26f;
                    g.FillEllipse(b, f.X + w / 4f - d / 2f, f.Y + h / 4f - d / 2f, d, d);
                }
                break;
            }
            case OcrLanguage.Koreanisch:
            {
                using (var b = new SolidBrush(Color.White)) g.FillRectangle(b, f);
                float d = h * 0.5f;
                var circle = new RectangleF(f.X + (w - d) / 2f, f.Y + (h - d) / 2f, d, d);
                using (var b = new SolidBrush(Color.FromArgb(0xCD, 0x2E, 0x3A))) g.FillPie(b, circle.X, circle.Y, d, d, 180, 180);
                using (var b = new SolidBrush(Color.FromArgb(0x00, 0x47, 0xA0))) g.FillPie(b, circle.X, circle.Y, d, d, 0, 180);
                using (var b = new SolidBrush(Color.FromArgb(0xCD, 0x2E, 0x3A))) g.FillEllipse(b, circle.X, circle.Y + d / 4f, d / 2f, d / 2f);
                using (var b = new SolidBrush(Color.FromArgb(0x00, 0x47, 0xA0))) g.FillEllipse(b, circle.X + d / 2f, circle.Y + d / 4f, d / 2f, d / 2f);
                using (var pen = new Pen(Color.FromArgb(0x1A, 0x1A, 0x1A), Math.Max(1f, h * 0.07f)))
                {
                    float l = h * 0.16f;
                    void Bars(float x, float y, double angle)
                    {
                        var st = g.Save();
                        g.TranslateTransform(x, y);
                        g.RotateTransform((float)angle);
                        for (int i = -1; i <= 1; i++)
                            g.DrawLine(pen, -l / 2f, i * h * 0.08f, l / 2f, i * h * 0.08f);
                        g.Restore(st);
                    }
                    Bars(f.X + w * 0.17f, f.Y + h * 0.22f, -56);
                    Bars(f.Right - w * 0.17f, f.Y + h * 0.22f, 56);
                    Bars(f.X + w * 0.17f, f.Bottom - h * 0.22f, 56);
                    Bars(f.Right - w * 0.17f, f.Bottom - h * 0.22f, -56);
                }
                break;
            }
            case OcrLanguage.Russisch:
                Stripes(g, f, false, Color.White, Color.FromArgb(0x00, 0x39, 0xA6), Color.FromArgb(0xD5, 0x2B, 0x1E));
                break;
            case OcrLanguage.Portugiesisch:
            {
                using (var b = new SolidBrush(Color.FromArgb(0xDA, 0x29, 0x1C))) g.FillRectangle(b, f);
                using (var b = new SolidBrush(Color.FromArgb(0x04, 0x6A, 0x38))) g.FillRectangle(b, f.X, f.Y, w * 0.4f, h);
                float d = h * 0.44f;
                using (var b = new SolidBrush(Color.FromArgb(0xFF, 0xD7, 0x00))) g.FillEllipse(b, f.X + w * 0.4f - d / 2f, f.Y + (h - d) / 2f, d, d);
                float d2 = d * 0.55f;
                using (var b = new SolidBrush(Color.FromArgb(0xDA, 0x29, 0x1C))) g.FillEllipse(b, f.X + w * 0.4f - d2 / 2f, f.Y + (h - d2) / 2f, d2, d2);
                break;
            }
            case OcrLanguage.Arabisch:
            {
                var green = Color.FromArgb(0x00, 0x7A, 0x3D);
                using (var b = new SolidBrush(green)) g.FillRectangle(b, f);
                float d = h * 0.56f;
                float cx = f.X + w * 0.44f, cy = f.Y + h / 2f;
                using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, cx - d / 2f, cy - d / 2f, d, d);
                using (var b = new SolidBrush(green)) g.FillEllipse(b, cx - d / 2f + d * 0.22f, cy - d / 2f - d * 0.04f, d * 0.9f, d * 0.9f);
                Star(g, cx + d * 0.55f, cy, h * 0.12f, Color.White, Math.PI / 10);
                break;
            }
            default:
                using (var b = new SolidBrush(IconColors.Slate)) g.FillRectangle(b, f);
                break;
        }
    }
}
