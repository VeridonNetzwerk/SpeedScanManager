using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Non-selectable header of the tray menu: logo tile, "SpeedScan Manager" and a status line
/// with a colored dot (same look as the <see cref="ModernWindow"/> header).
/// </summary>
internal sealed class TrayMenuHeaderItem : ToolStripItem
{
    public enum State { Ready, Busy, Offline }

    private string _status = "";
    private State _state = State.Busy;

    public TrayMenuHeaderItem()
    {
        Text = "SpeedScan Manager";
        AutoSize = true;
        AccessibleRole = AccessibleRole.StaticText;
    }

    public override bool CanSelect => false;

    public void SetStatus(string status, State state)
    {
        if (status == _status && state == _state) return;
        _status = status;
        _state = state;
        AccessibleDescription = status;
        Invalidate();
    }

    private float S => (Owner?.DeviceDpi ?? 96) / 96f;

    public override Size GetPreferredSize(Size constrainingSize)
    {
        float s = S;
        var font = ThemeFonts.Semibold;
        int textW = Math.Max(TextRenderer.MeasureText(Text, font).Width,
                             TextRenderer.MeasureText(_status, ModernFonts.Small).Width + (int)(14 * s));
        return new Size((int)(12 * s + 32 * s + 10 * s) + textW + (int)(16 * s), (int)(52 * s));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = ThemeManager.Current;
        var g = e.Graphics;
        float s = S;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        float tile = 32 * s;
        var tileRect = new RectangleF(12 * s, (Height - tile) / 2f, tile, tile);
        using (var path = ThemeGlyphs.Rounded(tileRect, 7 * s))
        using (var brush = new SolidBrush(p.IsDark ? p.Background : p.Secondary))
            g.FillPath(brush, path);
        if (!p.IsDark)
        {
            using var path = ThemeGlyphs.Rounded(RectangleF.Inflate(tileRect, -0.5f, -0.5f), 7 * s);
            using var pen = new Pen(ThemePalette.Blend(p.Border, p.Secondary, 0.35f));
            g.DrawPath(pen, path);
        }
        float logo = 22 * s;
        g.DrawImage(AppResources.Logo, new RectangleF(tileRect.X + (tile - logo) / 2f, tileRect.Y + (tile - logo) / 2f, logo, logo));

        int x = (int)(tileRect.Right + 10 * s);
        var flags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
        int titleH = TextRenderer.MeasureText(g, "Ag", ThemeFonts.Semibold, Size.Empty, flags).Height;
        int subH = TextRenderer.MeasureText(g, "Ag", ModernFonts.Small, Size.Empty, flags).Height;
        int top = (Height - titleH - subH - (int)(2 * s)) / 2;
        int w = Math.Max(0, Width - x - (int)(10 * s));
        TextRenderer.DrawText(g, Text, ThemeFonts.Semibold, new Rectangle(x, top, w, titleH), p.Text, flags);

        int y = top + titleH + (int)(2 * s);
        Color dot = _state switch
        {
            State.Ready => p.Success,
            State.Offline => p.Danger,
            _ => p.Warning
        };
        float d = 7 * s;
        using (var brush = new SolidBrush(dot))
            g.FillEllipse(brush, x + 0.5f * s, y + (subH - d) / 2f, d, d);
        TextRenderer.DrawText(g, _status, ModernFonts.Small, new Rectangle(x + (int)(12 * s), y, Math.Max(0, w - (int)(12 * s)), subH), p.TextMuted, flags);
    }
}
