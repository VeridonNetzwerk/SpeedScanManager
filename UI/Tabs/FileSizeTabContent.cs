using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Content of the "Dateigröße" settings page: compression rate (1-5) on a custom-drawn
/// themed slider with tick labels, the wedge indicators of the ScanSnap Manager layout
/// (compression low → high, file size large → small) and the value badge, plus the hint why
/// the rate is not used (black and white / PNG).
/// </summary>
internal class FileSizeTabContent : ModernStack
{
    private readonly CompressionScale _scale;
    private readonly ScanSettings _settings;
    private readonly SettingRow _lblDisabledHint;
    private bool _loading;

    public void ApplyPreset(int compression)
    {
        _settings.CompressionRate = Math.Clamp(compression, 1, 5);
        LoadFromSettings();
    }

    /// <summary>
    /// Refreshes all controls from the settings without raising change notifications.
    /// </summary>
    public void LoadFromSettings()
    {
        _loading = true;
        try
        {
            _scale.Slider.Value = Math.Clamp(_settings.CompressionRate, 1, 5);
        }
        finally
        {
            _loading = false;
        }
        UpdateEnabledState();
    }

    /// <summary>
    /// The compression rate only affects color/gray JPEG data: disabled for
    /// black and white scans and for PNG (lossless) output.
    /// </summary>
    public void UpdateEnabledState()
    {
        string? reason = _settings.ColorMode == ColorMode.BlackWhite
            ? "Für Schwarzweiß-Scans wird die Komprimierungsrate für Schwarzweißbilder übernommen."
            : _settings.FileFormat == FileFormat.Png
                ? "PNG-Dateien werden verlustfrei gespeichert; die Komprimierungsrate wird nicht verwendet."
                : null;
        _scale.Enabled = reason == null;
        _lblDisabledHint.Description = reason ?? "";
        _lblDisabledHint.Visible = reason != null;
    }

    public FileSizeTabContent(ScanSettings settings)
    {
        _settings = settings;

        _scale = new CompressionScale();
        _scale.Slider.AccessibleName = "Komprimierungsrate";
        _scale.Slider.ValueChanged += (s, e) =>
        {
            if (!_loading)
                _settings.CompressionRate = _scale.Slider.Value;
        };

        _lblDisabledHint = new SettingRow("", "") { DescriptionRole = ThemeRole.Danger, Visible = false, VerticalPadding = 4 };

        var card = new SettingsCard("Komprimierungsrate",
            "Eine höhere Komprimierung ergibt kleinere Dateien, aber eine geringere Bildqualität.") { Dense = true, Icon = ColorIcon.Tile(Glyph.Compress, IconColors.Green) };
        card.Add(_scale);
        card.Add(_lblDisabledHint);
        Controls.Add(card);

        LoadFromSettings();
    }
}

/// <summary>
/// Compression scale of the "Dateigröße" page: captions and the two wedges aligned exactly to
/// the slider's first and last stop, the <see cref="ModernSlider"/> (1-5) and a value badge.
/// </summary>
internal sealed class CompressionScale : Panel, IThemeAware, IHeightForWidth
{
    private const int LabelColumn = 112;
    private const int BadgeSize = 44;

    public CompressionScale()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Slider = new ModernSlider { Minimum = 1, Maximum = 5, Value = 3 };
        Slider.ValueChanged += (_, _) => Invalidate();
        Controls.Add(Slider);
        Height = 150;
    }

    public ModernSlider Slider { get; }

    public void ApplyTheme(ThemePalette palette) => Invalidate();

    private int S(float v) => ModernLayout.Scale(this, v);

    public int GetHeightForWidth(int width) => S(146);

    // Vertical positions (96-dpi units)
    private const int CaptionTopY = 12;
    private const int WedgeTopY = 30;
    private const int SliderY = 48;
    private const int WedgeBottomY = 100;
    private const int CaptionBottomY = 116;
    private const int WedgeHeight = 12;

    private Rectangle SliderBounds()
    {
        int left = S(LabelColumn);
        int right = Width - S(BadgeSize) - S(28);
        return new Rectangle(left, S(SliderY), Math.Max(S(120), right - left), S(46));
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        Slider?.SetBounds(SliderBounds().X, SliderBounds().Y, SliderBounds().Width, SliderBounds().Height);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = ThemeManager.Current;
        var g = e.Graphics;
        var back = ModernLayout.ParentBack(this);
        g.Clear(back);
        bool enabled = Enabled;

        var sb = Slider.Bounds;
        float x0 = sb.Left + Slider.XOf(Slider.Minimum);
        float x1 = sb.Left + Slider.XOf(Slider.Maximum);

        Color muted = enabled ? p.TextMuted : ModernLayout.Disabled(p.TextMuted, back);
        Color label = enabled ? p.TextSecondary : ModernLayout.Disabled(p.TextSecondary, back);
        var small = ModernFonts.Small;
        var flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;

        // Row labels (left column), centered on their wedge
        DrawRowLabel(g, "Komprimierung", S(WedgeTopY) + S(WedgeHeight) / 2, label);
        DrawRowLabel(g, "Dateigröße", S(WedgeBottomY) + S(WedgeHeight) / 2, label);

        // Captions at the ends of the wedges
        DrawCaption(g, "Niedrig", x0, S(CaptionTopY), muted, small, flags, align: -1);
        DrawCaption(g, "Hoch", x1, S(CaptionTopY), muted, small, flags, align: 1);
        DrawCaption(g, "Groß", x0, S(CaptionBottomY), muted, small, flags, align: -1);
        DrawCaption(g, "Klein", x1, S(CaptionBottomY), muted, small, flags, align: 1);
        DrawCaption(g, "Normal", sb.Left + Slider.XOf(3), S(CaptionBottomY), muted, small, flags, align: 0);

        // Wedges: compression grows to the right (red), file size shrinks to the right (accent)
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        float wh = S(WedgeHeight);
        float ty = S(WedgeTopY), by = S(WedgeBottomY);
        Color red = enabled ? p.Danger : ThemePalette.Blend(p.Danger, back, 0.6f);
        Color blue = enabled ? p.Accent : ThemePalette.Blend(p.Accent, back, 0.6f);
        var topRect = new RectangleF(x0, ty, Math.Max(1, x1 - x0), wh);
        using (var path = new GraphicsPath())
        {
            path.AddPolygon(new[] { new PointF(x0, ty + wh), new PointF(x1, ty), new PointF(x1, ty + wh) });
            using var brush = new LinearGradientBrush(RectangleF.Inflate(topRect, 1, 0), Color.FromArgb(40, red), red, LinearGradientMode.Horizontal);
            g.FillPath(brush, path);
        }
        var bottomRect = new RectangleF(x0, by, Math.Max(1, x1 - x0), wh);
        using (var path = new GraphicsPath())
        {
            path.AddPolygon(new[] { new PointF(x0, by), new PointF(x0, by + wh), new PointF(x1, by) });
            using var brush = new LinearGradientBrush(RectangleF.Inflate(bottomRect, 1, 0), blue, Color.FromArgb(40, blue), LinearGradientMode.Horizontal);
            g.FillPath(brush, path);
        }

        // Value badge, vertically centered on the slider track
        float s = DeviceDpi / 96f;
        float trackY = sb.Top + 12 * s;
        var badge = new RectangleF(Width - S(BadgeSize) - 1, trackY - S(BadgeSize) / 2f, S(BadgeSize), S(BadgeSize));
        using (var path = ModernLayout.RoundedRect(badge, 8 * s))
        {
            using (var brush = new SolidBrush(enabled ? ThemePalette.Blend(p.Input, p.Accent, p.IsDark ? 0.18f : 0.08f) : p.Input))
                g.FillPath(brush, path);
            using var pen = new Pen(enabled ? ThemePalette.Blend(p.InputBorder, p.Accent, 0.45f) : p.InputBorder, 1f);
            g.DrawPath(pen, path);
        }
        g.SmoothingMode = SmoothingMode.None;
        TextRenderer.DrawText(g, Slider.Value.ToString(), ModernFonts.PageTitle, Rectangle.Round(badge),
            enabled ? p.Text : ModernLayout.Disabled(p.Text, back),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
    }

    private void DrawRowLabel(Graphics g, string text, int centerY, Color color)
    {
        var size = TextRenderer.MeasureText(g, text, ModernFonts.RowTitle, Size.Empty, TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, text, ModernFonts.RowTitle, new Rectangle(0, centerY - size.Height / 2, S(LabelColumn) - S(8), size.Height),
            color, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }

    private static void DrawCaption(Graphics g, string text, float x, int y, Color color, Font font, TextFormatFlags flags, int align)
    {
        var size = TextRenderer.MeasureText(g, text, font, Size.Empty, flags);
        int left = align switch
        {
            < 0 => (int)Math.Round(x),
            > 0 => (int)Math.Round(x - size.Width),
            _ => (int)Math.Round(x - size.Width / 2f)
        };
        TextRenderer.DrawText(g, text, font, new Rectangle(left, y, size.Width + 1, size.Height), color, flags);
    }
}
