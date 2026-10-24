using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Rounded, themed button (same rendering as a standard Button after ThemeManager.Apply,
/// but styled from construction on, also outside a themed form).
/// </summary>
internal class RoundedButton : Button
{
    public RoundedButton() : this(ThemeRole.Auto) { }

    public RoundedButton(ThemeRole role, Glyph glyph = Glyph.None)
    {
        ThemeManager.SetRole(this, role);
        if (glyph != Glyph.None)
            ThemeManager.SetGlyph(this, glyph);
        ThemeManager.Apply(this);
    }
}

/// <summary>Themed DropDownList ComboBox (owner-drawn items, flat rounded frame).</summary>
internal class ThemedComboBox : ComboBox
{
    public ThemedComboBox()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        ThemeManager.Apply(this);
    }
}

/// <summary>Rounded card: secondary surface with a subtle border. Children inherit its color.</summary>
internal class CardPanel : Panel
{
    public CardPanel()
    {
        DoubleBuffered = true;
        ThemeManager.SetRole(this, ThemeRole.Card);
        ThemeManager.Apply(this);
    }
}

/// <summary>
/// Flat info banner (replaces the former blue gradient bar): secondary surface, accent bar
/// on the left, semibold secondary text.
/// </summary>
internal class ThemedBanner : Panel, IThemeAware
{
    private string _text;

    public ThemedBanner(string text)
    {
        _text = text;
        DoubleBuffered = true;
        Dock = DockStyle.Top;
        Height = 20;
        ApplyTheme(ThemeManager.Current);
    }

    public string BannerText
    {
        get => _text;
        set { _text = value; Invalidate(); }
    }

    public void ApplyTheme(ThemePalette palette)
    {
        BackColor = palette.Secondary;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = ThemeManager.Current;
        float s = DeviceDpi / 96f;
        e.Graphics.Clear(p.Secondary);
        using (var accent = new SolidBrush(p.Accent))
            e.Graphics.FillRectangle(accent, 0, 0, Math.Max(2, (int)Math.Round(3 * s)), Height);
        var textRect = new Rectangle((int)Math.Round(12 * s), 0, Width - (int)Math.Round(16 * s), Height);
        TextRenderer.DrawText(e.Graphics, _text, ThemeFonts.Semibold, textRect, p.TextSecondary,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix
            | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }
}
