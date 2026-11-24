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
        _list ??= new DropDownBorderWindow((ComboBox)Owner);
        if (_list.Handle != IntPtr.Zero) _list.ReleaseHandle();
        _list.AssignHandle(hwndList);
    }

    private sealed class DropDownBorderWindow : NativeWindow
    {
        private const int WM_WINDOWPOSCHANGING = 0x0046;
        private readonly ComboBox _combo;

        public DropDownBorderWindow(ComboBox combo) => _combo = combo;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_WINDOWPOSCHANGING)
                ComboLists.AdjustListPosition(_combo, Handle, m.LParam);
            base.WndProc(ref m);
            if (m.Msg == WM_NCPAINT && GetWindowRect(Handle, out var wr))
            {
                IntPtr hdc = GetWindowDC(Handle);
                if (hdc == IntPtr.Zero) return;
                try
                {
                    using var g = Graphics.FromHdc(hdc);
                    var pal = ThemeManager.Current;
                    using var pen = new Pen(pal.IsDark ? pal.Tertiary : pal.Border);
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

/// <summary>
/// Single-line TextBox drawn like the combo boxes: rounded field in the input color, inner
/// padding (the text sits centered), border muted on hover and accent while focused. The
/// non-client area is painted completely here (the native themed frame isn't drawn at all, it
/// flashed white when the mouse moved over the field); the padding counts as client for the
/// mouse, so it shows the I-beam and clicking it focuses the field.
/// </summary>
internal sealed class TextBoxSubclass : ControlSubclass
{
    private const int WM_NCCALCSIZE = 0x0083;
    private const int WM_NCHITTEST = 0x0084;
    private const int HTCLIENT = 1;
    private const uint SWP_FRAMECHANGED = 0x0020, SWP_NOMOVE = 0x0002, SWP_NOSIZE = 0x0001, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    private bool _hover;
    private Rectangle _client; // client area in window coordinates

    public TextBoxSubclass(TextBox box) : base(box)
    {
        box.HandleCreated += (_, _) => Reframe();
        box.FontChanged += (_, _) => Reframe();
        if (box.IsHandleCreated) Reframe();
    }

    private void Reframe()
    {
        if (!Owner.IsHandleCreated) return;
        ThemeNative.SetTheme(Owner.Handle, ""); // no visual-style frame (hot tracking flashed it)
        SetWindowPos(Owner.Handle, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
    }

    private (int H, int V) Padding(int height)
    {
        int line = Owner.Font.Height;
        int v = Math.Max(1, (height - line) / 2);
        int h = Math.Max(2, (int)Math.Round(8 * Scale));
        return (h, v);
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_NCCALCSIZE:
            {
                // lParam points to a RECT in both cases (first member of NCCALCSIZE_PARAMS)
                var r = Marshal.PtrToStructure<ThemePainters.RECT>(m.LParam);
                int w = r.Right - r.Left, h = r.Bottom - r.Top;
                var (ph, pv) = Padding(h);
                _client = new Rectangle(ph, pv, Math.Max(0, w - 2 * ph), Math.Max(0, h - 2 * pv));
                r.Left += ph; r.Right -= ph; r.Top += pv; r.Bottom -= pv;
                if (r.Right < r.Left) r.Right = r.Left;
                if (r.Bottom < r.Top) r.Bottom = r.Top;
                Marshal.StructureToPtr(r, m.LParam, false);
                m.Result = IntPtr.Zero;
                return;
            }
            case WM_NCPAINT:
                PaintFrame();
                m.Result = IntPtr.Zero;
                return;
            case WM_NCHITTEST:
                base.WndProc(ref m);
                if ((int)m.Result != HTCLIENT && (int)m.Result > 0)
                    m.Result = (IntPtr)HTCLIENT;
                return;
            case WM_MOUSEMOVE:
                if (!_hover)
                {
                    _hover = true;
                    TrackMouseLeave();
                    PaintFrame();
                }
                break;
            case WM_MOUSELEAVE:
                _hover = false;
                base.WndProc(ref m);
                PaintFrame();
                return;
            case WM_SETFOCUS:
            case WM_KILLFOCUS:
            case WM_ENABLE:
                base.WndProc(ref m);
                PaintFrame();
                return;
        }
        base.WndProc(ref m);
    }

    private void PaintFrame()
    {
        if (Handle == IntPtr.Zero || !GetWindowRect(Handle, out var wr)) return;
        int w = wr.Right - wr.Left, h = wr.Bottom - wr.Top;
        if (w <= 0 || h <= 0) return;
        IntPtr hdc = GetWindowDC(Handle);
        if (hdc == IntPtr.Zero) return;
        try
        {
            var p = ThemeManager.Current;
            var box = (TextBox)Owner;
            var parentBg = ParentBack(box);
            var fill = box.BackColor;
            bool lowContrast = Math.Abs(parentBg.GetBrightness() - fill.GetBrightness()) < 0.03f;
            var border = box.Focused && box.Enabled ? p.Accent
                : _hover && box.Enabled && !box.ReadOnly ? (p.IsDark ? p.ButtonSecondary : p.TextMuted)
                : lowContrast ? p.Border
                : p.InputBorder;

            using var bmp = new Bitmap(w, h);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(parentBg);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = ThemeGlyphs.Rounded(new RectangleF(0.5f, 0.5f, w - 1f, h - 1f), 4 * Scale);
                using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
                using var pen = new Pen(border, 1f);
                g.DrawPath(pen, path);
            }
            using var target = Graphics.FromHdc(hdc);
            if (_client.Width > 0 && _client.Height > 0)
                target.ExcludeClip(_client); // the edit paints its own text area
            target.DrawImageUnscaled(bmp, 0, 0);
        }
        finally
        {
            ReleaseDC(Handle, hdc);
        }
    }
}

/// <summary>Themed 1-2 px border for controls with a native border (TextBox, ListBox, TreeView).</summary>
internal class BorderSubclass : ControlSubclass
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
            var outer = _focusAccent && Owner.ContainsFocus ? p.Accent : p.InputBorder;
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

/// <summary>
/// Themed frame of NumericUpDown/DomainUpDown. UpDownBase paints its visual-style border after
/// the Paint event, so the frame is drawn after WM_PAINT; the spin buttons get their own subclass.
/// </summary>
internal sealed class UpDownSubclass : ControlSubclass
{
    private readonly UpDownButtonsSubclass? _buttons;

    public UpDownSubclass(UpDownBase upDown) : base(upDown)
    {
        foreach (Control child in upDown.Controls)
        {
            if (child is not TextBoxBase)
                _buttons ??= new UpDownButtonsSubclass(child);
        }
        upDown.Enter += (_, _) => upDown.Invalidate();
        upDown.Leave += (_, _) => upDown.Invalidate();
    }

    /// <summary>Repaints the spin buttons (after a theme change).</summary>
    public void InvalidateButtons() => _buttons?.Invalidate();

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_PAINT:
                base.WndProc(ref m);
                PaintFrame(IntPtr.Zero);
                return;
            case WM_PRINTCLIENT:
                base.WndProc(ref m);
                PaintFrame(m.WParam);
                return;
        }
        base.WndProc(ref m);
    }

    private void PaintFrame(IntPtr hdcIn)
    {
        var upDown = (UpDownBase)Owner;
        if (upDown.BorderStyle == BorderStyle.None) return;
        IntPtr hdc = hdcIn != IntPtr.Zero ? hdcIn : GetDC(Handle);
        if (hdc == IntPtr.Zero) return;
        try
        {
            using var g = Graphics.FromHdc(hdc);
            var p = ThemeManager.Current;
            var client = upDown.ClientRectangle;
            // Cover the native 2 px border and the gaps around the edit/buttons children
            // (the children themselves are clipped: WS_CLIPCHILDREN).
            using (var fill = new SolidBrush(upDown.BackColor))
                g.FillRectangle(fill, client);
            var outer = upDown.ContainsFocus && upDown.Enabled ? p.Accent : p.InputBorder;
            using var pen = new Pen(outer);
            g.DrawRectangle(pen, 0, 0, client.Width - 1, client.Height - 1);
        }
        finally
        {
            if (hdcIn == IntPtr.Zero)
                ReleaseDC(Handle, hdc);
        }
    }
}

/// <summary>Fully themed spin buttons of an UpDownBase (flat, chevrons, hover/pressed halves).</summary>
internal sealed class UpDownButtonsSubclass : ControlSubclass
{
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_CAPTURECHANGED = 0x0215;

    private Point? _mouse;

    public UpDownButtonsSubclass(Control buttons) : base(buttons)
    {
        buttons.EnabledChanged += (_, _) => buttons.Invalidate();
    }

    public void Invalidate() => Owner.Invalidate();

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_ERASEBKGND:
                m.Result = (IntPtr)1;
                return;
            case WM_PAINT:
            {
                IntPtr hdc = BeginPaint(Handle, out var ps);
                try { PaintBuffered(hdc, Draw); }
                finally { EndPaint(Handle, ref ps); }
                return;
            }
            case WM_PRINTCLIENT:
                PaintBuffered(m.WParam, Draw);
                return;
            case WM_MOUSEMOVE:
                base.WndProc(ref m);
                if (_mouse == null) TrackMouseLeave();
                _mouse = new Point((short)((long)m.LParam & 0xFFFF), (short)(((long)m.LParam >> 16) & 0xFFFF));
                Owner.Invalidate();
                return;
            case WM_MOUSELEAVE:
                base.WndProc(ref m);
                _mouse = null;
                Owner.Invalidate();
                return;
            case WM_LBUTTONDOWN:
            case WM_LBUTTONUP:
            case WM_CAPTURECHANGED:
                base.WndProc(ref m);
                Owner.Invalidate();
                return;
        }
        base.WndProc(ref m);
    }

    private void Draw(Graphics g)
    {
        var p = ThemeManager.Current;
        var upDown = Owner.Parent;
        bool enabled = Owner.Enabled && (upDown?.Enabled ?? true);
        var bg = upDown?.BackColor ?? p.Input;
        g.Clear(bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        int w = Owner.Width, h = Owner.Height, half = h / 2;
        var top = new Rectangle(0, 0, w, half);
        var bottom = new Rectangle(0, half, w, h - half);
        bool pressed = (Control.MouseButtons & MouseButtons.Left) != 0;
        float s = Scale;

        void DrawHalf(Rectangle r, Glyph glyph)
        {
            bool hot = enabled && _mouse.HasValue && r.Contains(_mouse.Value);
            if (hot)
            {
                var fill = pressed
                    ? (p.IsDark ? p.Selected : p.ButtonSecondaryPressed)
                    : (p.IsDark ? p.Hover : p.Selected);
                using var path = ThemeGlyphs.Rounded(RectangleF.Inflate(r, -1f, -1f), 2f * s);
                using var brush = new SolidBrush(fill);
                g.FillPath(brush, path);
            }
            var color = !enabled ? ThemePalette.Blend(p.TextMuted, bg, 0.45f) : hot ? p.Text : p.TextSecondary;
            float size = Math.Min(r.Width, r.Height);
            var glyphRect = new RectangleF(r.X + (r.Width - size) / 2f, r.Y + (r.Height - size) / 2f, size, size);
            ThemeGlyphs.Draw(g, glyph, glyphRect, color);
        }

        DrawHalf(top, Glyph.ChevronUp);
        DrawHalf(bottom, Glyph.ChevronDown);
    }
}

/// <summary>
/// ListView: themed native border plus readable header text in dark mode (the dark
/// "ItemsView" header theme keeps black text; NM_CUSTOMDRAW sets the text color).
/// </summary>
internal sealed class ListViewSubclass : BorderSubclass
{
    private const int WM_NOTIFY = 0x004E;
    private const int NM_CUSTOMDRAW = -12;
    private const int CDDS_PREPAINT = 0x00000001;
    private const int CDDS_ITEMPREPAINT = 0x00010001;
    private const int CDRF_DODEFAULT = 0x00000000;
    private const int CDRF_NOTIFYITEMDRAW = 0x00000020;
    private const int LVM_GETHEADER = 0x101F;

    [StructLayout(LayoutKind.Sequential)]
    private struct NMHDR
    {
        public IntPtr hwndFrom;
        public IntPtr idFrom;
        public int code;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NMCUSTOMDRAW
    {
        public NMHDR hdr;
        public int dwDrawStage;
        public IntPtr hdc;
        public ThemePainters.RECT rc;
        public IntPtr dwItemSpec;
        public int uItemState;
        public IntPtr lItemlParam;
    }

    [DllImport("gdi32.dll")]
    private static extern int SetTextColor(IntPtr hdc, int color);

    public ListViewSubclass(ListView listView) : base(listView, focusAccent: false) { }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NOTIFY && m.LParam != IntPtr.Zero && ThemeManager.IsDark
            && Owner is ListView { OwnerDraw: false, View: View.Details })
        {
            var hdr = Marshal.PtrToStructure<NMHDR>(m.LParam);
            if (hdr.code == NM_CUSTOMDRAW
                && hdr.hwndFrom == ThemePainters.SendMessage(Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero))
            {
                var cd = Marshal.PtrToStructure<NMCUSTOMDRAW>(m.LParam);
                if (cd.dwDrawStage == CDDS_PREPAINT)
                {
                    m.Result = (IntPtr)CDRF_NOTIFYITEMDRAW;
                    return;
                }
                if (cd.dwDrawStage == CDDS_ITEMPREPAINT)
                {
                    var c = ThemeManager.Current.TextSecondary;
                    SetTextColor(cd.hdc, c.R | (c.G << 8) | (c.B << 16));
                    m.Result = (IntPtr)CDRF_DODEFAULT;
                    return;
                }
            }
        }
        base.WndProc(ref m);
    }
}
