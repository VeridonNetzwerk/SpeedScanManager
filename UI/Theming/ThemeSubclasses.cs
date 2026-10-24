using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Window subclass that follows the control's handle (re-attached when the handle is
/// recreated; released automatically on WM_NCDESTROY).
/// </summary>
internal abstract class ControlSubclass : NativeWindow
{
    protected const int WM_PAINT = 0x000F;
    protected const int WM_ERASEBKGND = 0x0014;
    protected const int WM_NCPAINT = 0x0085;
    protected const int WM_SETFOCUS = 0x0007;
    protected const int WM_KILLFOCUS = 0x0008;
    protected const int WM_MOUSEMOVE = 0x0200;
    protected const int WM_MOUSELEAVE = 0x02A3;
    protected const int WM_PRINTCLIENT = 0x0318;
    protected const int WM_ENABLE = 0x000A;

    [StructLayout(LayoutKind.Sequential)]
    protected struct PAINTSTRUCT
    {
        public IntPtr hdc;
        public bool fErase;
        public ThemePainters.RECT rcPaint;
        public bool fRestore;
        public bool fIncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] rgbReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TRACKMOUSEEVENT
    {
        public int cbSize;
        public int dwFlags;
        public IntPtr hwndTrack;
        public int dwHoverTime;
    }

    [DllImport("user32.dll")]
    protected static extern IntPtr BeginPaint(IntPtr hwnd, out PAINTSTRUCT ps);

    [DllImport("user32.dll")]
    protected static extern bool EndPaint(IntPtr hwnd, ref PAINTSTRUCT ps);

    [DllImport("user32.dll")]
    protected static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    protected static extern IntPtr GetWindowDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    protected static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("user32.dll")]
    protected static extern bool GetWindowRect(IntPtr hwnd, out ThemePainters.RECT rect);

    [DllImport("user32.dll")]
    private static extern bool TrackMouseEvent(ref TRACKMOUSEEVENT tme);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    protected static extern int GetWindowLong(IntPtr hwnd, int index);

    protected readonly Control Owner;

    protected ControlSubclass(Control owner)
    {
        Owner = owner;
        owner.HandleCreated += (_, _) => Attach();
        if (owner.IsHandleCreated)
            Attach();
    }

    private void Attach()
    {
        if (Handle != IntPtr.Zero)
            ReleaseHandle();
        AssignHandle(Owner.Handle);
    }

    protected float Scale => Owner.DeviceDpi / 96f;

    protected void TrackMouseLeave()
    {
        var tme = new TRACKMOUSEEVENT
        {
            cbSize = Marshal.SizeOf<TRACKMOUSEEVENT>(),
            dwFlags = 0x2, // TME_LEAVE
            hwndTrack = Handle
        };
        TrackMouseEvent(ref tme);
    }

    protected static Color ParentBack(Control c)
    {
        var parent = c.Parent;
        while (parent != null && parent.BackColor.A < 255)
            parent = parent.Parent;
        return parent?.BackColor ?? ThemeManager.Current.Background;
    }

    /// <summary>Paints <paramref name="paint"/> double-buffered into <paramref name="hdc"/>.</summary>
    protected void PaintBuffered(IntPtr hdc, Action<Graphics> paint)
    {
        var rect = Owner.ClientRectangle;
        if (rect.Width <= 0 || rect.Height <= 0) return;
        using var g = Graphics.FromHdc(hdc);
        using var buffer = BufferedGraphicsManager.Current.Allocate(g, rect);
        paint(buffer.Graphics);
        buffer.Render(g);
    }
}

/// <summary>Themed frame and drop-down button for ComboBox (the item area is owner-drawn).</summary>
internal sealed class ComboSubclass : ControlSubclass
{
    private bool _hover;
    private DropDownBorderWindow? _list;

    public ComboSubclass(ComboBox combo) : base(combo) { }

    /// <summary>Themed 1 px border of the drop-down list window (ComboLBox).</summary>
    public void AttachList(IntPtr hwndList)
    {
        if (hwndList == IntPtr.Zero || _list?.Handle == hwndList) return;
        _list ??= new DropDownBorderWindow();
        if (_list.Handle != IntPtr.Zero) _list.ReleaseHandle();
        _list.AssignHandle(hwndList);
    }

    private sealed class DropDownBorderWindow : NativeWindow
    {
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_NCPAINT && GetWindowRect(Handle, out var wr))
            {
                IntPtr hdc = GetWindowDC(Handle);
                if (hdc == IntPtr.Zero) return;
                try
                {
                    using var g = Graphics.FromHdc(hdc);
                    using var pen = new Pen(ThemeManager.Current.IsDark ? ThemeManager.Current.Border : ThemeManager.Current.InputBorder);
                    g.DrawRectangle(pen, 0, 0, wr.Right - wr.Left - 1, wr.Bottom - wr.Top - 1);
                }
                finally
                {
                    ReleaseDC(Handle, hdc);
                }
            }
        }
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_PAINT:
                base.WndProc(ref m);
                PaintOverlay(IntPtr.Zero);
                return;
            case WM_PRINTCLIENT:
                base.WndProc(ref m);
                PaintOverlay(m.WParam);
                return;
            case WM_MOUSEMOVE:
                if (!_hover)
                {
                    _hover = true;
                    TrackMouseLeave();
                    Owner.Invalidate();
                }
                break;
            case WM_MOUSELEAVE:
                _hover = false;
                Owner.Invalidate();
                break;
            case WM_SETFOCUS:
            case WM_KILLFOCUS:
            case WM_ENABLE:
                base.WndProc(ref m);
                Owner.Invalidate();
                return;
        }
        base.WndProc(ref m);
    }

    private void PaintOverlay(IntPtr hdcIn)
    {
        var combo = (ComboBox)Owner;
        if (!ThemePainters.TryGetComboInfo(combo, out var info)) return;
        IntPtr hdc = hdcIn != IntPtr.Zero ? hdcIn : GetDC(Handle);
        if (hdc == IntPtr.Zero) return;
        try
        {
            using var g = Graphics.FromHdc(hdc);
            var p = ThemeManager.Current;
            float s = Scale;
            var client = combo.ClientRectangle;
            var item = info.rcItem.ToRectangle();
            var button = info.rcButton.ToRectangle();

            using var clip = new Region(client);
            clip.Exclude(item);
            g.Clip = clip;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            using (var bg = new SolidBrush(ParentBack(combo)))
                g.FillRectangle(bg, client);

            var fill = combo.Enabled ? p.Input : ThemePalette.Blend(p.Input, ParentBack(combo), 0.4f);
            var rect = new RectangleF(0, 0, client.Width, client.Height);
            using (var path = ThemeGlyphs.Rounded(RectangleF.Inflate(rect, -0.5f, -0.5f), 4 * s))
            {
                using (var brush = new SolidBrush(fill))
                    g.FillPath(brush, path);
                // Without a visible contrast to the surface (dark input on a dark header) the
                // field gets a border, otherwise the input color alone marks it.
                var parentBg = ParentBack(combo);
                bool lowContrast = Math.Abs(parentBg.GetBrightness() - fill.GetBrightness()) < 0.03f;
                var borderColor = combo.Focused || combo.DroppedDown ? p.Accent
                    : _hover && combo.Enabled ? (p.IsDark ? p.ButtonSecondary : p.TextMuted)
                    : lowContrast ? p.Border
                    : p.InputBorder;
                using var pen = new Pen(borderColor, 1f);
                g.DrawPath(pen, path);
            }

            if (combo.DropDownStyle != ComboBoxStyle.Simple)
            {
                float size = 14 * s;
                var glyphRect = new RectangleF(
                    button.X + (button.Width - size) / 2f,
                    (client.Height - size) / 2f, size, size);
                var color = combo.Enabled ? p.TextSecondary : p.TextMuted;
                ThemeGlyphs.Draw(g, Glyph.ChevronDown, glyphRect, color);
            }
        }
        finally
        {
            if (hdcIn == IntPtr.Zero)
                ReleaseDC(Handle, hdc);
        }
    }
}

/// <summary>Themed 1-2 px border for controls with a native border (TextBox, ListBox, TreeView).</summary>
internal sealed class BorderSubclass : ControlSubclass
{
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const int WS_BORDER = 0x00800000;
    private const int WS_EX_CLIENTEDGE = 0x00000200;

    private readonly bool _focusAccent;

    public BorderSubclass(Control owner, bool focusAccent) : base(owner)
    {
        _focusAccent = focusAccent;
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_NCPAINT:
                base.WndProc(ref m);
                PaintBorder();
                return;
            case WM_SETFOCUS:
            case WM_KILLFOCUS:
                base.WndProc(ref m);
                ThemePainters.RedrawFrame(Owner);
                return;
        }
        base.WndProc(ref m);
    }

    private void PaintBorder()
    {
        int style = GetWindowLong(Handle, GWL_STYLE);
        int exStyle = GetWindowLong(Handle, GWL_EXSTYLE);
        int thickness = (exStyle & WS_EX_CLIENTEDGE) != 0 ? 2 : (style & WS_BORDER) != 0 ? 1 : 0;
        if (thickness == 0) return;
        if (!GetWindowRect(Handle, out var wr)) return;
        int w = wr.Right - wr.Left, h = wr.Bottom - wr.Top;

        IntPtr hdc = GetWindowDC(Handle);
        if (hdc == IntPtr.Zero) return;
        try
        {
            using var g = Graphics.FromHdc(hdc);
            var p = ThemeManager.Current;
            var outer = _focusAccent && Owner.Focused ? p.Accent : p.InputBorder;
            using (var pen = new Pen(outer))
                g.DrawRectangle(pen, 0, 0, w - 1, h - 1);
            if (thickness == 2)
            {
                using var pen = new Pen(Owner.BackColor);
                g.DrawRectangle(pen, 1, 1, w - 3, h - 3);
            }
        }
        finally
        {
            ReleaseDC(Handle, hdc);
        }
    }
}

/// <summary>Fully owner-painted horizontal TrackBar (rounded channel, accent fill, round thumb).</summary>
internal sealed class TrackBarSubclass : ControlSubclass
{
    private const int TBM_GETTHUMBRECT = 0x0419;
    private const int TBM_GETCHANNELRECT = 0x041A;
    private bool _hover;

    public TrackBarSubclass(TrackBar track) : base(track) { }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_PAINT:
            {
                var hdc = BeginPaint(Handle, out var ps);
                try { PaintBuffered(hdc, Paint); }
                finally { EndPaint(Handle, ref ps); }
                return;
            }
            case WM_PRINTCLIENT:
                PaintBuffered(m.WParam, Paint);
                return;
            case WM_ERASEBKGND:
                m.Result = (IntPtr)1;
                return;
            case WM_MOUSEMOVE:
                if (!_hover)
                {
                    _hover = true;
                    TrackMouseLeave();
                    Owner.Invalidate();
                }
                break;
            case WM_MOUSELEAVE:
                _hover = false;
                Owner.Invalidate();
                break;
            case WM_SETFOCUS:
            case WM_KILLFOCUS:
                base.WndProc(ref m);
                Owner.Invalidate();
                return;
        }
        base.WndProc(ref m);
    }

    private void Paint(Graphics g)
    {
        var track = (TrackBar)Owner;
        var p = ThemeManager.Current;
        float s = Scale;
        g.Clear(track.BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var channel = new ThemePainters.RECT();
        var thumb = new ThemePainters.RECT();
        ThemePainters.SendMessage(Handle, TBM_GETCHANNELRECT, IntPtr.Zero, ref channel);
        ThemePainters.SendMessage(Handle, TBM_GETTHUMBRECT, IntPtr.Zero, ref thumb);
        var ch = channel.ToRectangle();
        var th = thumb.ToRectangle();

        float cy = ch.Top + ch.Height / 2f;
        float left = ch.Left + th.Width / 2f;
        float right = ch.Right - th.Width / 2f;
        float thumbX = th.Left + th.Width / 2f;
        float trackH = 4 * s;

        var trackColor = p.IsDark ? p.ButtonSecondary : p.Tertiary;
        var active = track.Enabled ? p.Accent : ThemePalette.Blend(p.Accent, track.BackColor, 0.55f);
        using (var path = ThemeGlyphs.Rounded(new RectangleF(left, cy - trackH / 2, right - left, trackH), trackH / 2))
        using (var brush = new SolidBrush(trackColor))
            g.FillPath(brush, path);
        using (var path = ThemeGlyphs.Rounded(new RectangleF(left, cy - trackH / 2, Math.Max(trackH, thumbX - left), trackH), trackH / 2))
        using (var brush = new SolidBrush(active))
            g.FillPath(brush, path);

        // Tick marks
        if (track.TickStyle != TickStyle.None && track.Maximum > track.Minimum && track.TickFrequency > 0)
        {
            using var pen = new Pen(p.TextMuted, Math.Max(1f, s));
            float tickTop = cy + 9 * s, tickBottom = tickTop + 4 * s;
            float tickTopAbove = cy - 9 * s, tickBottomAbove = tickTopAbove - 4 * s;
            for (int v = track.Minimum; v <= track.Maximum; v += track.TickFrequency)
            {
                float x = left + (right - left) * (v - track.Minimum) / (track.Maximum - track.Minimum);
                if (track.TickStyle is TickStyle.BottomRight or TickStyle.Both)
                    g.DrawLine(pen, x, tickTop, x, tickBottom);
                if (track.TickStyle is TickStyle.TopLeft or TickStyle.Both)
                    g.DrawLine(pen, x, tickTopAbove, x, tickBottomAbove);
            }
        }

        // Thumb
        float d = 16 * s;
        var knob = new RectangleF(thumbX - d / 2, cy - d / 2, d, d);
        if (track.Focused || _hover)
        {
            using var halo = new SolidBrush(Color.FromArgb(track.Focused ? 90 : 50, p.Accent));
            g.FillEllipse(halo, RectangleF.Inflate(knob, 3 * s, 3 * s));
        }
        using (var brush = new SolidBrush(track.Enabled ? p.SliderThumb : ThemePalette.Blend(p.SliderThumb, track.BackColor, 0.4f)))
            g.FillEllipse(brush, knob);
        using (var pen = new Pen(p.IsDark ? Color.FromArgb(60, 0, 0, 0) : p.Border, 1f))
            g.DrawEllipse(pen, knob);
    }
}

/// <summary>
/// Flat themed TabControl: paints the tab strip itself. For DrawMode.OwnerDrawFixed the
/// control's DrawItem handlers draw each tab (state HotLight = hover); otherwise tabs are
/// drawn with an accent underline.
/// </summary>
internal sealed class TabControlSubclass : ControlSubclass
{
    private static readonly MethodInfo? OnDrawItemMethod =
        typeof(TabControl).GetMethod("OnDrawItem", BindingFlags.Instance | BindingFlags.NonPublic);

    private int _hot = -1;

    public TabControlSubclass(TabControl tabs) : base(tabs) { }

    /// <summary>Index of the tab under the mouse, or -1.</summary>
    public int HotIndex => _hot;

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_PAINT:
            {
                var hdc = BeginPaint(Handle, out var ps);
                try { PaintBuffered(hdc, Paint); }
                finally { EndPaint(Handle, ref ps); }
                return;
            }
            case WM_PRINTCLIENT:
                PaintBuffered(m.WParam, Paint);
                return;
            case WM_ERASEBKGND:
                m.Result = (IntPtr)1;
                return;
            case WM_MOUSEMOVE:
            {
                var tabs = (TabControl)Owner;
                int x = (short)((long)m.LParam & 0xFFFF), y = (short)(((long)m.LParam >> 16) & 0xFFFF);
                int hot = -1;
                for (int i = 0; i < tabs.TabCount; i++)
                    if (tabs.GetTabRect(i).Contains(x, y)) { hot = i; break; }
                if (hot != _hot)
                {
                    if (_hot < 0) TrackMouseLeave();
                    _hot = hot;
                    Owner.Invalidate();
                }
                break;
            }
            case WM_MOUSELEAVE:
                if (_hot >= 0)
                {
                    _hot = -1;
                    Owner.Invalidate();
                }
                break;
        }
        base.WndProc(ref m);
    }

    private void Paint(Graphics g)
    {
        var tabs = (TabControl)Owner;
        var p = ThemeManager.Current;
        float s = Scale;
        var stripBg = ThemeManager.SurfaceColorFor(tabs) ?? ParentBack(tabs);
        g.Clear(stripBg);

        int stripBottom = 0;
        for (int i = 0; i < tabs.TabCount; i++)
            stripBottom = Math.Max(stripBottom, tabs.GetTabRect(i).Bottom);
        using (var pen = new Pen(p.Border))
            g.DrawLine(pen, 0, stripBottom, tabs.Width, stripBottom);

        for (int i = 0; i < tabs.TabCount; i++)
        {
            var rect = tabs.GetTabRect(i);
            bool selected = tabs.SelectedIndex == i;
            var state = selected ? DrawItemState.Selected : i == _hot ? DrawItemState.HotLight : DrawItemState.None;
            if (tabs.DrawMode == TabDrawMode.OwnerDrawFixed && OnDrawItemMethod != null)
            {
                OnDrawItemMethod.Invoke(tabs, new object[] { new DrawItemEventArgs(g, tabs.Font, rect, i, state) });
                continue;
            }

            if (state == DrawItemState.HotLight)
            {
                using var path = ThemeGlyphs.Rounded(RectangleF.Inflate(rect, -2 * s, -3 * s), 4 * s);
                using var brush = new SolidBrush(p.Hover);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillPath(brush, path);
            }
            var color = selected ? p.Text : i == _hot ? p.TextSecondary : p.TextMuted;
            TextRenderer.DrawText(g, tabs.TabPages[i].Text, tabs.Font, rect, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            if (selected)
            {
                using var brush = new SolidBrush(p.Accent);
                g.FillRectangle(brush, rect.X + 4 * s, stripBottom - 2 * s, rect.Width - 8 * s, 2 * s);
            }
        }
    }
}
