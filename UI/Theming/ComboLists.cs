using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Drop-down lists of the themed combo boxes: the opened list has exactly the width of the
/// box and starts at its left edge (no list that is wider than the box and sticks out of the
/// card/window), rows get a comfortable height (per-row heights for icon or two-line items),
/// and the list opens above the box when it would otherwise reach past the bottom of the window.
/// Used automatically for every themed DropDownList combo (<see cref="ThemeManager"/>).
/// </summary>
internal static class ComboLists
{
    /// <summary>Default list row height for text rows (96-dpi units).</summary>
    public const int TextRow = 26;
    /// <summary>List row height for rows with an icon (96-dpi units).</summary>
    public const int IconRow = 30;
    /// <summary>List row height for two-line rows (name + detail, 96-dpi units).</summary>
    public const int TwoLineRow = 40;

    private sealed class Info
    {
        public Func<int, int>? RowHeight;
        public bool Hooked;
        public int ListHeight;
    }

    private static readonly ConditionalWeakTable<ComboBox, Info> Infos = new();

    /// <summary>Row height (96-dpi units) per item index of the opened list.</summary>
    public static void SetRowHeight(ComboBox combo, Func<int, int> rowHeight96)
    {
        Infos.GetOrCreateValue(combo).RowHeight = rowHeight96;
    }

    /// <summary>Same row height (96-dpi units) for all items.</summary>
    public static void SetRowHeight(ComboBox combo, int rowHeight96) => SetRowHeight(combo, _ => rowHeight96);

    private static int Scale(Control c, int v) => (int)Math.Round(v * c.DeviceDpi / 96f);

    private static int RowHeight(ComboBox combo, int index)
    {
        var info = Infos.GetOrCreateValue(combo);
        return Scale(combo, index < 0 ? TextRow : info.RowHeight?.Invoke(index) ?? TextRow);
    }

    /// <summary>Hooks the combo once (called by ThemeManager when it themes the combo).</summary>
    internal static void Attach(ComboBox combo)
    {
        if (combo.DropDownStyle != ComboBoxStyle.DropDownList) return;
        var info = Infos.GetOrCreateValue(combo);
        if (info.Hooked) return;
        info.Hooked = true;

        // Variable rows: the list rows can be taller than the box (ItemHeight stays the box height).
        combo.MeasureItem += (s, e) =>
        {
            if (e.Index >= 0) e.ItemHeight = RowHeight(combo, e.Index);
        };
        if (combo.DrawMode == DrawMode.OwnerDrawFixed)
        {
            int edit = combo.ItemHeight;
            combo.DrawMode = DrawMode.OwnerDrawVariable;
            combo.ItemHeight = edit;
        }
        combo.DropDown += (s, e) => Prepare(combo);
        // Same width as the box (set outside the DropDown event: changing the drop-down size
        // while the list opens closes it again)
        combo.SizeChanged += (s, e) => { if (combo.DropDownWidth != combo.Width) combo.DropDownWidth = combo.Width; };
    }

    /// <summary>Width and height of the list before it opens.</summary>
    private static void Prepare(ComboBox combo)
    {
        var info = Infos.GetOrCreateValue(combo);
        int count = combo.Items.Count;
        int visible = Math.Min(count, Math.Max(combo.MaxDropDownItems, 12));
        int h = 0;
        for (int i = 0; i < visible; i++)
            h += RowHeight(combo, i);
        h = Math.Max(h, Scale(combo, TextRow)) + 2;
        // Row heights may depend on state that changed since the items were added
        if (combo.DrawMode == DrawMode.OwnerDrawVariable && combo.IsHandleCreated)
            for (int i = 0; i < count; i++)
                SendMessage(combo.Handle, CB_SETITEMHEIGHT, (IntPtr)i, (IntPtr)RowHeight(combo, i));
        // Applied in AdjustListPosition (setting DropDownHeight here would close the list)
        info.ListHeight = h;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPOS
    {
        public IntPtr hwnd, hwndInsertAfter;
        public int x, y, cx, cy;
        public uint flags;
    }

    private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_SHOWWINDOW = 0x0040, SWP_HIDEWINDOW = 0x0080;

    private const int CB_SETITEMHEIGHT = 0x0153;

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out ThemePainters.RECT rect);

    /// <summary>
    /// WM_WINDOWPOSCHANGING of the list window: aligns the list with the box (same left edge
    /// and width, small gap) and keeps it inside the window when possible (opens upwards
    /// when there is more room above).
    /// </summary>
    internal static void AdjustListPosition(ComboBox combo, IntPtr hwndList, IntPtr lParam)
    {
        if (lParam == IntPtr.Zero || !combo.IsHandleCreated) return;
        var wp = Marshal.PtrToStructure<WINDOWPOS>(lParam);
        if ((wp.flags & SWP_HIDEWINDOW) != 0) return;
        if ((wp.flags & SWP_SHOWWINDOW) == 0 && !IsWindowVisible(hwndList)) return;
        if ((wp.flags & (SWP_NOMOVE | SWP_NOSIZE)) == (SWP_NOMOVE | SWP_NOSIZE) && (wp.flags & SWP_SHOWWINDOW) == 0) return;

        var info = Infos.GetOrCreateValue(combo);
        int height = info.ListHeight;
        if (height <= 0)
        {
            if ((wp.flags & SWP_NOSIZE) == 0) height = wp.cy;
            else if (GetWindowRect(hwndList, out var wr)) height = wr.Bottom - wr.Top;
        }

        var box = combo.RectangleToScreen(combo.ClientRectangle);
        var screen = Screen.FromControl(combo).WorkingArea;
        var form = combo.FindForm();
        var bounds = form != null && form.Visible ? Rectangle.Intersect(form.Bounds, screen) : screen;
        if (bounds.IsEmpty) bounds = screen;

        int gap = Scale(combo, 3);
        int margin = Scale(combo, 8);
        int minRows = Math.Min(height, RowHeight(combo, 0) * 4 + 2);

        int below = bounds.Bottom - margin - (box.Bottom + gap);
        int above = box.Top - gap - (bounds.Top + margin);
        if (Math.Max(below, above) < minRows)
        {
            // The window is too small: use the screen
            below = screen.Bottom - (box.Bottom + gap);
            above = box.Top - gap - screen.Top;
        }

        int y;
        if (height <= below)
            y = box.Bottom + gap;
        else if (height <= above)
            y = box.Top - gap - height;
        else if (below >= above)
        {
            height = Math.Max(minRows, below);
            y = box.Bottom + gap;
        }
        else
        {
            height = Math.Max(minRows, above);
            y = box.Top - gap - height;
        }

        wp.x = box.Left;
        wp.y = y;
        wp.cx = box.Width;
        wp.cy = height;
        wp.flags &= ~(SWP_NOMOVE | SWP_NOSIZE);
        Marshal.StructureToPtr(wp, lParam, false);
    }
}

/// <summary>
/// Owner-drawn list boxes with a colorful icon, the name and a muted right-aligned detail per
/// row (profile list, custom sizes, custom applications). Same look as the combo lists.
/// </summary>
internal static class ModernLists
{
    /// <summary>
    /// Sets up <paramref name="list"/>: rows of <paramref name="rowHeight96"/> (96-dpi units),
    /// drawn with <paramref name="row"/> (icon, name, detail) per index.
    /// </summary>
    public static void Setup(ListBox list, Func<int, (ColorIcon? Icon, string Name, string? Detail)> row, int rowHeight96 = 34)
    {
        list.DrawMode = DrawMode.OwnerDrawFixed;
        list.ItemHeight = (int)Math.Round(rowHeight96 * list.DeviceDpi / 96f);
        list.DrawItem += (s, e) =>
        {
            Color fg = ThemeManager.DrawItemBackground(list, e);
            if (e.Index < 0 || e.Index >= list.Items.Count) return;
            var (icon, name, detail) = row(e.Index);
            int S(float v) => (int)Math.Round(v * list.DeviceDpi / 96f);
            int x = e.Bounds.X + S(10);
            if (icon != null)
            {
                int size = S(22);
                icon.Draw(e.Graphics, new RectangleF(x, e.Bounds.Y + (e.Bounds.Height - size) / 2f, size, size), list.Enabled);
                x += size + S(10);
            }
            var flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
                        | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;
            int right = e.Bounds.Right - S(10);
            int detailW = 0;
            if (!string.IsNullOrEmpty(detail))
            {
                detailW = Math.Min(TextRenderer.MeasureText(e.Graphics, detail, ModernFonts.Small, Size.Empty,
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width + 1, Math.Max(0, (right - x) / 2));
                TextRenderer.DrawText(e.Graphics, detail, ModernFonts.Small, new Rectangle(right - detailW, e.Bounds.Y, detailW, e.Bounds.Height),
                    list.Enabled ? ThemeManager.Current.TextMuted : fg, flags);
                detailW += S(12);
            }
            TextRenderer.DrawText(e.Graphics, name, list.Font, new Rectangle(x, e.Bounds.Y, Math.Max(0, right - detailW - x), e.Bounds.Height), fg, flags);
        };
    }
}

/// <summary>Small control that shows a <see cref="ColorIcon"/> (e.g. next to a slider).</summary>
internal sealed class ColorIconBox : Control, IThemeAware
{
    private ColorIcon? _icon;

    public ColorIconBox(ColorIcon? icon = null, int size = 20)
    {
        _icon = icon;
        Size = new Size(size, size);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
    }

    public ColorIcon? Icon { get => _icon; set { _icon = value; Invalidate(); } }

    /// <summary>Draw the icon faded (follows the enabled state of another control).</summary>
    public bool Faded { get; set; }

    public void ApplyTheme(ThemePalette palette) => Invalidate();

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(ModernLayout.ParentBack(this));
        int s = Math.Min(Width, Height);
        _icon?.Draw(e.Graphics, new RectangleF((Width - s) / 2f, (Height - s) / 2f, s, s), Enabled && !Faded);
    }
}
