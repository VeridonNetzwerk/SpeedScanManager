using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Discord-style renderer for menus and tool strips: flat menu background, rounded accent
/// highlight with inset, thin separators, chevron arrows and check glyphs. Reads colors from
/// <see cref="ThemeManager.Current"/> at paint time.
/// </summary>
internal sealed class ThemedToolStripRenderer : ToolStripRenderer
{
    private static float Dpi(ToolStrip? strip) => (strip?.DeviceDpi ?? 96) / 96f;

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        var p = ThemeManager.Current;
        var color = e.ToolStrip is ToolStripDropDown ? p.Menu : e.ToolStrip.BackColor;
        using var brush = new SolidBrush(color);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        var p = ThemeManager.Current;
        if (e.ToolStrip is ToolStripDropDown)
        {
            // Windows 11 draws a rounded border (DWM); older versions get a flat one.
            if (ThemeNative.IsWindows11) return;
            using var pen = new Pen(p.IsDark ? p.Tertiary : p.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            return;
        }
        using var line = new Pen(p.Border);
        if (e.ToolStrip is StatusStrip)
            e.Graphics.DrawLine(line, 0, 0, e.ToolStrip.Width, 0);
        else
            e.Graphics.DrawLine(line, 0, e.ToolStrip.Height - 1, e.ToolStrip.Width, e.ToolStrip.Height - 1);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        // Same color as the menu: no separate margin column.
    }

    private static bool IsDanger(ToolStripItem item) => ThemeManager.GetMenuItemRole(item) == ThemeRole.Danger;

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var p = ThemeManager.Current;
        var item = e.Item;
        var g = e.Graphics;
        float s = Dpi(item.Owner);
        bool inDropDown = item.Owner is ToolStripDropDown;

        if (inDropDown)
        {
            if (!item.Selected || !item.Enabled) return;
            var rect = new RectangleF(6 * s, 0.5f * s, item.Width - 12 * s, item.Height - 1 * s);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            using var path = ThemeGlyphs.Rounded(rect, 3 * s);
            using var brush = new SolidBrush(IsDanger(item) ? p.Danger : p.Accent);
            g.FillPath(brush, path);
            return;
        }

        if (item.Pressed || item.Selected)
        {
            var rect = new RectangleF(1, 1, item.Width - 2, item.Height - 2);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = ThemeGlyphs.Rounded(rect, 4 * s);
            using var brush = new SolidBrush(item.Pressed ? p.Selected : p.Hover);
            g.FillPath(brush, path);
        }
    }

    protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
    {
        var p = ThemeManager.Current;
        var item = e.Item;
        bool isChecked = item is ToolStripButton { Checked: true };
        if (!item.Selected && !item.Pressed && !isChecked) return;
        float s = Dpi(item.Owner);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = ThemeGlyphs.Rounded(new RectangleF(1, 1, item.Width - 2, item.Height - 2), 4 * s);
        using var brush = new SolidBrush(item.Pressed || isChecked ? p.Selected : p.Hover);
        g.FillPath(brush, path);
    }

    protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e) => OnRenderButtonBackground(e);

    protected override void OnRenderSplitButtonBackground(ToolStripItemRenderEventArgs e)
    {
        OnRenderButtonBackground(e);
        if (e.Item is ToolStripSplitButton split)
        {
            var r = split.DropDownButtonBounds;
            ThemeGlyphs.Draw(e.Graphics, Glyph.ChevronDown, new RectangleF(r.X, r.Y + (r.Height - r.Width) / 2f, r.Width, r.Width), ThemeManager.Current.TextSecondary);
        }
    }

    protected override void OnRenderOverflowButtonBackground(ToolStripItemRenderEventArgs e) => OnRenderButtonBackground(e);

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        var p = ThemeManager.Current;
        var item = e.Item;
        bool inDropDown = item.Owner is ToolStripDropDown;
        Color color;
        if (!item.Enabled)
            color = ThemePalette.Blend(p.TextMuted, inDropDown ? p.Menu : p.Secondary, 0.35f);
        else if (inDropDown && item.Selected)
            color = p.TextOnAccent;
        else if (IsDanger(item))
            color = p.Danger;
        else
            color = inDropDown ? p.Text : p.TextSecondary;
        e.TextColor = color;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var p = ThemeManager.Current;
        float s = Dpi(e.Item.Owner);
        var color = p.IsDark ? ThemePalette.Blend(p.Border, p.Menu, 0.35f) : p.Border;
        using var pen = new Pen(color);
        var r = e.Item.ContentRectangle;
        if (e.Vertical)
        {
            int x = e.Item.Width / 2;
            e.Graphics.DrawLine(pen, x, 4 * s, x, e.Item.Height - 4 * s);
        }
        else
        {
            int y = e.Item.Height / 2;
            e.Graphics.DrawLine(pen, 10 * s, y, e.Item.Width - 10 * s, y);
        }
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        var p = ThemeManager.Current;
        var item = e.Item;
        var color = item != null && item.Selected && item.Enabled && item.Owner is ToolStripDropDown ? p.TextOnAccent : p.TextSecondary;
        var r = e.ArrowRectangle;
        float size = Math.Min(r.Width, r.Height) + 4;
        var rect = new RectangleF(r.X + (r.Width - size) / 2f - 2 * Dpi(item?.Owner), r.Y + (r.Height - size) / 2f, size, size);
        var glyph = e.Direction switch
        {
            ArrowDirection.Down => Glyph.ChevronDown,
            ArrowDirection.Up => Glyph.ChevronUp,
            _ => Glyph.ChevronRight
        };
        ThemeGlyphs.Draw(e.Graphics, glyph, rect, color);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var p = ThemeManager.Current;
        var item = e.Item;
        var color = item.Selected && item.Enabled ? p.TextOnAccent : p.Accent;
        var r = e.ImageRectangle;
        float size = Math.Min(r.Width, r.Height);
        ThemeGlyphs.Draw(e.Graphics, Glyph.Check,
            new RectangleF(r.X + (r.Width - size) / 2f + 2 * Dpi(item.Owner), r.Y + (r.Height - size) / 2f, size, size), color);
    }

    protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
    {
        if (e.Image == null) return;
        if (e.Item.Enabled)
        {
            e.Graphics.DrawImage(e.Image, e.ImageRectangle);
            return;
        }
        using var ia = new System.Drawing.Imaging.ImageAttributes();
        ia.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = 0.4f });
        var r = e.ImageRectangle;
        e.Graphics.DrawImage(e.Image, r, 0, 0, e.Image.Width, e.Image.Height, GraphicsUnit.Pixel, ia);
    }

    protected override void OnRenderGrip(ToolStripGripRenderEventArgs e)
    {
        // Flat: no grip
    }

    protected override void OnRenderStatusStripSizingGrip(ToolStripRenderEventArgs e)
    {
        var p = ThemeManager.Current;
        using var brush = new SolidBrush(p.TextMuted);
        var b = e.AffectedBounds;
        for (int i = 0; i < 3; i++)
            for (int j = 0; j <= i; j++)
                e.Graphics.FillRectangle(brush, b.Right - 4 - j * 4, b.Bottom - 4 - (2 - i + j) * 4 + 0, 2, 2);
    }
}
