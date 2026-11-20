using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>Paint routines for standard controls styled by <see cref="ThemeManager"/>.</summary>
internal static class ThemePainters
{
    private static float Dpi(Control c) => c.DeviceDpi / 96f;

    private static Color EffectiveBack(Control c)
    {
        var color = c.BackColor;
        var parent = c.Parent;
        while (color.A < 255 && parent != null)
        {
            color = parent.BackColor;
            parent = parent.Parent;
        }
        return color.A < 255 ? ThemeManager.Current.Background : color;
    }

    // ───────────────────────── Buttons ─────────────────────────

    private static ThemeRole ButtonRole(ButtonBase b, ThemeState st)
    {
        if (st.Role is ThemeRole.Primary or ThemeRole.Secondary or ThemeRole.Danger or ThemeRole.Success)
            return st.Role;
        if (b is CheckBox { Checked: true } || b is RadioButton { Checked: true })
            return ThemeRole.Primary;
        if (b is IButtonControl && b.FindForm()?.AcceptButton == b)
            return ThemeRole.Primary;
        return ThemeRole.Secondary;
    }

    public static void PaintButton(ButtonBase b, Graphics g, ThemeState st)
    {
        var p = ThemeManager.Current;
        float s = Dpi(b);
        var parentBg = b.Parent != null ? EffectiveBack(b.Parent) : p.Background;

        Color fill, hover, pressed, fore;
        switch (ButtonRole(b, st))
        {
            case ThemeRole.Primary:
                (fill, hover, pressed, fore) = (p.Accent, p.AccentHover, p.AccentPressed, p.TextOnAccent);
                break;
            case ThemeRole.Danger:
                (fill, hover, pressed, fore) = (p.Danger, p.DangerHover, p.DangerHover, p.TextOnAccent);
                break;
            case ThemeRole.Success:
                (fill, hover, pressed, fore) = (p.Success, p.SuccessHover, p.SuccessHover, p.TextOnAccent);
                break;
            default:
                (fill, hover, pressed, fore) = (p.ButtonSecondary, p.ButtonSecondaryHover, p.ButtonSecondaryPressed, p.ButtonSecondaryText);
                break;
        }

        Color bg = st.Pressed ? pressed : st.Hover ? hover : fill;
        if (!b.Enabled)
        {
            bg = ThemePalette.Blend(fill, parentBg, 0.5f);
            fore = ThemePalette.Blend(fore, bg, 0.5f);
        }

        g.Clear(parentBg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var rect = new RectangleF(0, 0, b.Width, b.Height);
        using (var path = ThemeGlyphs.Rounded(rect, 4 * s))
        using (var brush = new SolidBrush(bg))
            g.FillPath(brush, path);

        if (b.Focused && b.Enabled && ShowFocusCues(b))
        {
            var ring = ButtonRole(b, st) == ThemeRole.Secondary ? p.Accent : Color.FromArgb(200, Color.White);
            using var pen = new Pen(ring, 1.5f * s);
            var r = RectangleF.Inflate(rect, -1.25f * s, -1.25f * s);
            using var path = ThemeGlyphs.Rounded(r, 3 * s);
            g.DrawPath(pen, path);
        }

        // Content: icon (glyph or image) + text, centered as a group
        int iconSize = 0;
        Image? image = null;
        if (st.Glyph != Glyph.None)
            iconSize = (int)Math.Round(16 * s);
        else if (b.Image != null)
        {
            image = b.Image;
            iconSize = image.Width;
        }

        string text = b.Text;
        var flags = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Left
                    | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
        if (!b.UseMnemonic) flags |= TextFormatFlags.NoPrefix;
        else if (!ShowKeyboardCues(b)) flags |= TextFormatFlags.HidePrefix;

        int pad = (int)Math.Round(8 * s);
        int gap = iconSize > 0 && text.Length > 0 ? (int)Math.Round(6 * s) : 0;
        int trailSize = st.TrailingGlyph != Glyph.None ? (int)Math.Round(12 * s) : 0;
        int trailGap = trailSize > 0 ? (int)Math.Round(6 * s) : 0;
        int textW = text.Length > 0
            ? TextRenderer.MeasureText(g, text, b.Font, new Size(int.MaxValue, b.Height), flags & ~TextFormatFlags.EndEllipsis).Width
            : 0;
        int avail = Math.Max(0, b.Width - 2 * pad - iconSize - gap - trailSize - trailGap);
        textW = Math.Min(textW, avail);
        int total = iconSize + gap + textW + trailGap + trailSize;

        bool alignLeft = iconSize == 0 && (b.TextAlign & (ContentAlignment.TopLeft | ContentAlignment.MiddleLeft | ContentAlignment.BottomLeft)) != 0;
        int x = alignLeft ? pad : Math.Max(pad / 2, (b.Width - total) / 2);

        if (iconSize > 0)
        {
            int iconH = image?.Height ?? iconSize;
            var iconRect = new Rectangle(x, (b.Height - iconH) / 2, iconSize, iconH);
            if (st.Glyph != Glyph.None)
            {
                var glyphColor = st.GlyphColor is { } gc && b.Enabled && ButtonRole(b, st) == ThemeRole.Secondary
                    ? (p.IsDark ? ColorIcons.Lighten(gc, 0.25f) : gc)
                    : fore;
                ThemeGlyphs.Draw(g, st.Glyph, iconRect, glyphColor, st.GlyphColor.HasValue ? 1.1f : 1f);
            }
            else if (image != null)
                DrawButtonImage(g, b, st, image, iconRect, fore);
            x += iconSize + gap;
        }

        if (textW > 0)
        {
            var textRect = new Rectangle(x, 0, textW + 1, b.Height);
            TextRenderer.DrawText(g, text, b.Font, textRect, fore, flags);
            x += textW;
        }

        if (trailSize > 0)
        {
            x += trailGap;
            var trailRect = new Rectangle(x, (b.Height - trailSize) / 2 + 1, trailSize, trailSize);
            ThemeGlyphs.Draw(g, st.TrailingGlyph, trailRect, ThemePalette.Blend(fore, bg, 0.3f));
        }
    }

    private static void DrawButtonImage(Graphics g, ButtonBase b, ThemeState st, Image image, Rectangle rect, Color fore)
    {
        bool tint = st.TintImage ?? Math.Max(image.Width, image.Height) <= Math.Round(24 * Dpi(b));
        if (tint)
        {
            if (st.Tinted == null || !ReferenceEquals(st.TintSource, image) || st.TintColor != fore)
            {
                st.Tinted?.Dispose();
                st.Tinted = Tint(image, fore);
                st.TintSource = image;
                st.TintColor = fore;
            }
            g.DrawImage(st.Tinted, rect);
        }
        else if (!b.Enabled)
        {
            using var ia = new ImageAttributes();
            ia.SetColorMatrix(new ColorMatrix { Matrix33 = 0.45f });
            g.DrawImage(image, rect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, ia);
        }
        else
            g.DrawImage(image, rect);
    }

    /// <summary>Monochrome version of an icon: dark/saturated pixels become <paramref name="color"/>, light pixels transparent.</summary>
    public static Bitmap Tint(Image image, Color color)
    {
        var src = new Bitmap(image);
        var result = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, src.Width, src.Height);
        var sd = src.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var rd = result.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            int count = sd.Stride / 4 * src.Height;
            var pixels = new int[count];
            Marshal.Copy(sd.Scan0, pixels, 0, count);
            for (int i = 0; i < count; i++)
            {
                int px = pixels[i];
                int a = (px >> 24) & 0xFF;
                if (a == 0) { pixels[i] = 0; continue; }
                int r = (px >> 16) & 0xFF, gr = (px >> 8) & 0xFF, bl = px & 0xFF;
                double lum = (0.299 * r + 0.587 * gr + 0.114 * bl) / 255.0;
                int alpha = (int)Math.Round(a * Math.Clamp((1 - lum) * 1.35, 0, 1));
                pixels[i] = (alpha << 24) | (color.R << 16) | (color.G << 8) | color.B;
            }
            Marshal.Copy(pixels, 0, rd.Scan0, count);
        }
        finally
        {
            src.UnlockBits(sd);
            result.UnlockBits(rd);
            src.Dispose();
        }
        return result;
    }

    private static readonly System.Reflection.PropertyInfo? ShowFocusCuesProperty =
        typeof(Control).GetProperty("ShowFocusCues", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
    private static readonly System.Reflection.PropertyInfo? ShowKeyboardCuesProperty =
        typeof(Control).GetProperty("ShowKeyboardCues", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

    private static bool ShowFocusCues(Control c)
    {
        try { return ShowFocusCuesProperty?.GetValue(c) as bool? ?? true; }
        catch { return true; }
    }

    private static bool ShowKeyboardCues(Control c)
    {
        try { return ShowKeyboardCuesProperty?.GetValue(c) as bool? ?? true; }
        catch { return true; }
    }

    // ───────────────────────── CheckBox / RadioButton ─────────────────────────

    public static void PaintCheck(ButtonBase b, Graphics g, ThemeState st)
    {
        var p = ThemeManager.Current;
        float s = Dpi(b);
        var bg = EffectiveBack(b);
        g.Clear(bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        bool isRadio = b is RadioButton;
        bool isChecked = b is CheckBox cb ? cb.CheckState != CheckState.Unchecked : ((RadioButton)b).Checked;
        bool indeterminate = b is CheckBox { CheckState: CheckState.Indeterminate };
        var checkAlign = b is CheckBox c1 ? c1.CheckAlign : ((RadioButton)b).CheckAlign;
        bool top = (checkAlign & (ContentAlignment.TopLeft | ContentAlignment.TopCenter | ContentAlignment.TopRight)) != 0;

        float size = (float)Math.Round(16 * s);
        float y = top ? b.Padding.Top + Math.Max(0, (b.Font.Height - size) / 2f) : (b.Height - size) / 2f;
        var box = new RectangleF(0.5f * s, y, size, size);

        Color accent = st.Hover && b.Enabled ? p.AccentHover : p.Accent;
        Color outline = st.Hover && b.Enabled ? p.TextSecondary : p.TextMuted;
        if (!b.Enabled)
        {
            accent = ThemePalette.Blend(accent, bg, 0.5f);
            outline = ThemePalette.Blend(outline, bg, 0.5f);
        }

        if (isRadio)
        {
            if (isChecked)
            {
                using var pen = new Pen(accent, 2f * s);
                var ring = RectangleF.Inflate(box, -1f * s, -1f * s);
                g.DrawEllipse(pen, ring);
                using var brush = new SolidBrush(accent);
                var dot = RectangleF.Inflate(box, -4.5f * s, -4.5f * s);
                g.FillEllipse(brush, dot);
            }
            else
            {
                using var pen = new Pen(outline, 1.5f * s);
                g.DrawEllipse(pen, RectangleF.Inflate(box, -1f * s, -1f * s));
            }
        }
        else
        {
            var inner = RectangleF.Inflate(box, -0.75f * s, -0.75f * s);
            if (isChecked)
            {
                using (var path = ThemeGlyphs.Rounded(inner, 4 * s))
                using (var brush = new SolidBrush(accent))
                    g.FillPath(brush, path);
                var markColor = b.Enabled ? p.TextOnAccent : ThemePalette.Blend(p.TextOnAccent, accent, 0.4f);
                using var pen = new Pen(markColor, 2f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                if (indeterminate)
                    g.DrawLine(pen, inner.X + 4 * s, inner.Y + inner.Height / 2, inner.Right - 4 * s, inner.Y + inner.Height / 2);
                else
                    g.DrawLines(pen, new[]
                    {
                        new PointF(inner.X + 3.5f * s, inner.Y + 7.5f * s),
                        new PointF(inner.X + 6.25f * s, inner.Y + 10.25f * s),
                        new PointF(inner.X + 11f * s, inner.Y + 4.5f * s)
                    });
            }
            else
            {
                using var path = ThemeGlyphs.Rounded(RectangleF.Inflate(inner, -0.5f * s, -0.5f * s), 3.5f * s);
                using var pen = new Pen(outline, 1.5f * s);
                g.DrawPath(pen, path);
            }
        }

        if (b.Focused && b.Enabled && ShowFocusCues(b))
        {
            using var pen = new Pen(Color.FromArgb(140, p.Accent), 1.5f * s);
            var r = RectangleF.Inflate(box, 1.5f * s, 1.5f * s);
            if (isRadio) g.DrawEllipse(pen, r);
            else
            {
                using var path = ThemeGlyphs.Rounded(r, 5 * s);
                g.DrawPath(pen, path);
            }
        }

        if (b.Text.Length > 0)
        {
            var fore = b.Enabled ? b.ForeColor : ThemePalette.Blend(b.ForeColor, bg, 0.5f);
            int x = (int)Math.Ceiling(size + 6 * s);
            var flags = TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;
            if (!b.UseMnemonic) flags |= TextFormatFlags.NoPrefix;
            else if (!ShowKeyboardCues(b)) flags |= TextFormatFlags.HidePrefix;
            Rectangle textRect;
            if (b.AutoSize || b.Height < b.Font.Height * 2)
            {
                flags |= TextFormatFlags.SingleLine | (top ? TextFormatFlags.Top : TextFormatFlags.VerticalCenter);
                textRect = new Rectangle(x, top ? b.Padding.Top : 0, Math.Max(0, b.Width - x), b.Height);
            }
            else
            {
                flags |= TextFormatFlags.WordBreak | (top ? TextFormatFlags.Top : TextFormatFlags.VerticalCenter);
                textRect = new Rectangle(x, top ? b.Padding.Top : 0, Math.Max(0, b.Width - x), b.Height);
            }
            TextRenderer.DrawText(g, b.Text, b.Font, textRect, fore, flags);
        }
    }

    // ───────────────────────── Labels ─────────────────────────

    /// <summary>Repaints a disabled label's text in a muted theme color (opaque labels only).</summary>
    public static void PaintDisabledLabel(Label label, Graphics g)
    {
        if (label.Enabled || label is LinkLabel || label.BackColor.A < 255 || label.Image != null)
            return;
        var bg = label.BackColor;
        g.Clear(bg);
        var flags = TextFormatFlags.Default;
        var align = label.TextAlign;
        if ((align & (ContentAlignment.TopCenter | ContentAlignment.MiddleCenter | ContentAlignment.BottomCenter)) != 0)
            flags |= TextFormatFlags.HorizontalCenter;
        else if ((align & (ContentAlignment.TopRight | ContentAlignment.MiddleRight | ContentAlignment.BottomRight)) != 0)
            flags |= TextFormatFlags.Right;
        if ((align & (ContentAlignment.MiddleLeft | ContentAlignment.MiddleCenter | ContentAlignment.MiddleRight)) != 0)
            flags |= TextFormatFlags.VerticalCenter;
        else if ((align & (ContentAlignment.BottomLeft | ContentAlignment.BottomCenter | ContentAlignment.BottomRight)) != 0)
            flags |= TextFormatFlags.Bottom;
        if (!label.AutoSize) flags |= TextFormatFlags.WordBreak;
        if (label.AutoEllipsis) flags |= TextFormatFlags.EndEllipsis;
        if (!label.UseMnemonic) flags |= TextFormatFlags.NoPrefix;
        var rect = new Rectangle(
            label.Padding.Left, label.Padding.Top,
            Math.Max(0, label.Width - label.Padding.Horizontal),
            Math.Max(0, label.Height - label.Padding.Vertical));
        TextRenderer.DrawText(g, label.Text, label.Font, rect, ThemePalette.Blend(label.ForeColor, bg, 0.55f), flags);
    }

    // ───────────────────────── Containers ─────────────────────────

    public static void PaintGroupBox(GroupBox box, Graphics g)
    {
        var p = ThemeManager.Current;
        float s = Dpi(box);
        var bg = EffectiveBack(box);
        g.Clear(bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var font = ThemeFonts.SemiboldOf(box.Font);
        var flags = TextFormatFlags.SingleLine | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
        var textSize = box.Text.Length > 0 ? TextRenderer.MeasureText(g, box.Text, font, Size.Empty, flags) : Size.Empty;
        float top = textSize.Height > 0 ? textSize.Height / 2f : 1;

        var rect = new RectangleF(0.5f, top, box.Width - 1.5f, box.Height - top - 1.5f);
        using (var path = ThemeGlyphs.Rounded(rect, 6 * s))
        using (var pen = new Pen(p.Border, 1f))
            g.DrawPath(pen, path);

        if (textSize.Width > 0)
        {
            int tx = (int)Math.Round(10 * s);
            using (var brush = new SolidBrush(bg))
                g.FillRectangle(brush, tx - 4 * s, 0, textSize.Width + 8 * s, textSize.Height);
            var color = box.Enabled ? p.TextSecondary : ThemePalette.Blend(p.TextSecondary, bg, 0.5f);
            TextRenderer.DrawText(g, box.Text, font, new Point(tx, 0), color, flags);
        }
    }

    public static void PaintCard(Control panel, Graphics g)
    {
        var p = ThemeManager.Current;
        float s = Dpi(panel);
        var parentBg = panel.Parent != null ? EffectiveBack(panel.Parent) : p.Background;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var rect = new RectangleF(0, 0, panel.Width, panel.Height);
        using var path = ThemeGlyphs.Rounded(RectangleF.Inflate(rect, -0.5f, -0.5f), 6 * s);
        using (var region = new Region(rect))
        {
            region.Exclude(path);
            using var bgBrush = new SolidBrush(parentBg);
            g.FillRegion(bgBrush, region);
        }
        using (var brush = new SolidBrush(panel.BackColor))
            g.FillPath(brush, path);
        using (var pen = new Pen(p.IsDark ? p.Border : p.InputBorder, 1f))
            g.DrawPath(pen, path);
    }

    public static void PaintUpDownBorder(UpDownBase upDown, Graphics g)
    {
        var p = ThemeManager.Current;
        var r = new Rectangle(0, 0, upDown.Width - 1, upDown.Height - 1);
        using (var inner = new Pen(p.Input))
            g.DrawRectangle(inner, Rectangle.Inflate(r, -1, -1));
        using var pen = new Pen(upDown.ContainsFocus ? p.Accent : p.InputBorder);
        g.DrawRectangle(pen, r);
    }

    // ───────────────────────── Owner-drawn items ─────────────────────────

    public static void DrawComboItemDefault(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox combo) return;
        var fore = ThemeManager.DrawItemBackground(combo, e);
        if (e.Index < 0 || e.Index >= combo.Items.Count) return;
        string text = combo.GetItemText(combo.Items[e.Index]) ?? "";
        DrawItemText(combo, e, text, fore);
    }

    public static void DrawListItemDefault(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ListBox list) return;
        var fore = ThemeManager.DrawItemBackground(list, e);
        if (e.Index < 0 || e.Index >= list.Items.Count) return;
        DrawItemText(list, e, list.GetItemText(list.Items[e.Index]) ?? "", fore);
    }

    private static void DrawItemText(Control owner, DrawItemEventArgs e, string text, Color fore)
    {
        int pad = ThemeManager.Scale(owner, 6);
        if (text == "-")
        {
            // Separator entry
            int y = e.Bounds.Y + e.Bounds.Height / 2;
            using var pen = new Pen(ThemeManager.Current.Border);
            e.Graphics.DrawLine(pen, e.Bounds.X + pad, y, e.Bounds.Right - pad, y);
            return;
        }
        var rect = new Rectangle(e.Bounds.X + pad, e.Bounds.Y, Math.Max(0, e.Bounds.Width - pad - 2), e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, text, e.Font ?? owner.Font, rect, fore,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
            | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
    }

    // ───────────────────────── Native helpers ─────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left, Top, Right, Bottom;
        public Rectangle ToRectangle() => Rectangle.FromLTRB(Left, Top, Right, Bottom);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct COMBOBOXINFO
    {
        public int cbSize;
        public RECT rcItem;
        public RECT rcButton;
        public int stateButton;
        public IntPtr hwndCombo;
        public IntPtr hwndItem;
        public IntPtr hwndList;
    }

    [DllImport("user32.dll")]
    internal static extern bool GetComboBoxInfo(IntPtr hwnd, ref COMBOBOXINFO info);

    [DllImport("user32.dll")]
    internal static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    internal static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, ref RECT lParam);

    internal static bool TryGetComboInfo(ComboBox combo, out COMBOBOXINFO info)
    {
        info = new COMBOBOXINFO { cbSize = Marshal.SizeOf<COMBOBOXINFO>() };
        return combo.IsHandleCreated && GetComboBoxInfo(combo.Handle, ref info);
    }

    public static void ApplyComboNative(ComboBox combo, bool dark)
    {
        // Classic rendering (no theme animations that would paint over the themed frame).
        ThemeNative.SetTheme(combo.Handle, "");
        if (TryGetComboInfo(combo, out var info) && info.hwndList != IntPtr.Zero)
        {
            ThemeNative.ApplyExplorerTheme(info.hwndList, dark);
            ThemeNative.ApplyPopupCorners(info.hwndList, ThemeManager.Current);
            (ThemeManager.GetState(combo).Subclass as ComboSubclass)?.AttachList(info.hwndList);
        }
    }

    public static void ApplyListViewHeaderTheme(ListView listView, bool dark)
    {
        const int LVM_GETHEADER = 0x101F;
        var header = SendMessage(listView.Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
        if (header != IntPtr.Zero)
        {
            ThemeNative.SetTheme(header, dark ? "DarkMode_ItemsView" : "ItemsView");
            ThemeNative.RedrawWindow(header, IntPtr.Zero, IntPtr.Zero, ThemeNative.RDW_INVALIDATE | ThemeNative.RDW_FRAME);
        }
    }

    /// <summary>Repaints the non-client border (after a theme or focus change).</summary>
    public static void RedrawFrame(Control c)
    {
        if (c.IsHandleCreated)
            ThemeNative.RedrawWindow(c.Handle, IntPtr.Zero, IntPtr.Zero, ThemeNative.RDW_INVALIDATE | ThemeNative.RDW_FRAME);
    }
}
