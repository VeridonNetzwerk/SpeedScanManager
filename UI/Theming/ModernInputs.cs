using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Places its children side by side in equal columns (two-column card layout of the
/// settings pages). With <see cref="EqualHeight"/> all <see cref="SettingsCard"/>s of the row
/// end on the same line. Its height follows the tallest child.
/// </summary>
internal class ModernColumns : Panel, IHeightForWidth
{
    public ModernColumns()
    {
        DoubleBuffered = true;
    }

    /// <summary>Gap between the columns (96-dpi units, default 12).</summary>
    [DefaultValue(12)]
    public int Gap { get; set; } = 12;

    /// <summary>Stretch the children to the height of the tallest one (default true).</summary>
    [DefaultValue(true)]
    public bool EqualHeight { get; set; } = true;

    private int S(float v) => ModernLayout.Scale(this, v);

    private List<Control> Columns()
    {
        var list = new List<Control>();
        foreach (Control c in Controls)
            if (c.Visible || !Visible) list.Add(c);
        return list;
    }

    private int ColumnWidth(int width, int count)
        => count == 0 ? 0 : Math.Max(0, (width - (count - 1) * S(Gap)) / count);

    public int GetHeightForWidth(int width)
    {
        var cols = Columns();
        int w = ColumnWidth(width, cols.Count);
        int h = 0;
        foreach (var c in cols)
            h = Math.Max(h, ModernLayout.HeightOf(c, w));
        return h;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        int w = proposedSize.Width > 0 && proposedSize.Width < int.MaxValue / 2 ? proposedSize.Width : Width;
        return new Size(w, GetHeightForWidth(w));
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible) PerformLayout();
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        if (!Visible) return;
        var cols = Columns();
        int w = ColumnWidth(Width, cols.Count);
        var heights = cols.Select(c => ModernLayout.HeightOf(c, w)).ToList();
        int max = heights.Count == 0 ? 0 : heights.Max();
        int x = 0;
        for (int i = 0; i < cols.Count; i++)
        {
            int h = EqualHeight ? max : heights[i];
            if (cols[i] is SettingsCard card)
                card.MinimumCardHeight = EqualHeight ? max : 0;
            int cw = i == cols.Count - 1 ? Width - x : w;
            cols[i].SetBounds(x, 0, cw, h);
            x += w + S(Gap);
        }
    }
}

/// <summary>
/// Custom-drawn themed slider (replaces the native TrackBar): rounded track with accent fill,
/// round thumb with hover/drag/focus halo, tick marks and optional tick labels below.
/// Keyboard: arrow keys, Page Up/Down, Home/End; mouse: click, drag and wheel.
/// </summary>
internal class ModernSlider : Control, IThemeAware
{
    private int _minimum = 1;
    private int _maximum = 5;
    private int _value = 3;
    private bool _hover;
    private bool _thumbHover;
    private bool _dragging;
    private string[]? _tickLabels;

    public ModernSlider()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true;
        Size = new Size(240, 46);
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.Slider;
    }

    /// <summary>The value changed (by the user or by code).</summary>
    public event EventHandler? ValueChanged;

    public int Minimum
    {
        get => _minimum;
        set { _minimum = value; if (_maximum < value) _maximum = value; Value = _value; Invalidate(); }
    }

    public int Maximum
    {
        get => _maximum;
        set { _maximum = Math.Max(value, _minimum); Value = _value; Invalidate(); }
    }

    public int Value
    {
        get => _value;
        set
        {
            int v = Math.Clamp(value, _minimum, _maximum);
            if (v == _value) return;
            _value = v;
            Invalidate();
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Labels under the ticks (one per value from Minimum to Maximum); null = numbers.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string[]? TickLabels { get => _tickLabels; set { _tickLabels = value; Invalidate(); } }

    public void ApplyTheme(ThemePalette palette) => Invalidate();

    private float Sc => DeviceDpi / 96f;
    private float ThumbD => 18 * Sc;
    private float TrackLeft => ThumbD / 2f + 2 * Sc;
    private float TrackRight => Width - ThumbD / 2f - 2 * Sc;
    private float TrackY => 12 * Sc;

    /// <summary>X of the center of <paramref name="value"/> on the track (device pixels).</summary>
    public float XOf(int value)
    {
        if (_maximum == _minimum) return TrackLeft;
        return TrackLeft + (TrackRight - TrackLeft) * (value - _minimum) / (_maximum - _minimum);
    }

    private int ValueAt(int x)
    {
        if (_maximum == _minimum) return _minimum;
        float t = (x - TrackLeft) / Math.Max(1f, TrackRight - TrackLeft);
        return Math.Clamp(_minimum + (int)Math.Round(t * (_maximum - _minimum)), _minimum, _maximum);
    }

    private RectangleF ThumbRect()
    {
        float d = ThumbD;
        return new RectangleF(XOf(_value) - d / 2f, TrackY - d / 2f, d, d);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _thumbHover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !Enabled) return;
        Focus();
        _dragging = true;
        Capture = true;
        Value = ValueAt(e.X);
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging)
        {
            Value = ValueAt(e.X);
            return;
        }
        bool over = RectangleF.Inflate(ThumbRect(), 3 * Sc, 3 * Sc).Contains(e.Location);
        if (over != _thumbHover) { _thumbHover = over; Invalidate(); }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_dragging) return;
        _dragging = false;
        Capture = false;
        Invalidate();
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (_dragging && !Capture) { _dragging = false; Invalidate(); }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (!Enabled || !Focused) return;
        Value += e.Delta > 0 ? 1 : -1;
        if (e is HandledMouseEventArgs h) h.Handled = true;
    }

    protected override bool IsInputKey(Keys keyData)
        => keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown
           || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!Enabled) return;
        switch (e.KeyCode)
        {
            case Keys.Left: case Keys.Down: case Keys.PageDown: Value -= 1; e.Handled = true; break;
            case Keys.Right: case Keys.Up: case Keys.PageUp: Value += 1; e.Handled = true; break;
            case Keys.Home: Value = _minimum; e.Handled = true; break;
            case Keys.End: Value = _maximum; e.Handled = true; break;
        }
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new SliderAccessibleObject(this);

    private sealed class SliderAccessibleObject : ControlAccessibleObject
    {
        private readonly ModernSlider _slider;
        public SliderAccessibleObject(ModernSlider slider) : base(slider) => _slider = slider;
        public override string? Value
        {
            get => _slider.Value.ToString();
            set { if (int.TryParse(value, out int v)) _slider.Value = v; }
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = ThemeManager.Current;
        var g = e.Graphics;
        float s = Sc;
        var back = ModernLayout.ParentBack(this);
        g.Clear(back);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        bool enabled = Enabled;

        float left = TrackLeft, right = TrackRight, cy = TrackY;
        float trackH = 6 * s;
        float thumbX = XOf(_value);

        Color trackColor = p.IsDark ? p.ButtonSecondary : ThemePalette.Blend(p.InputBorder, back, 0.35f);
        Color accent = enabled ? (_hover || _dragging ? p.AccentHover : p.Accent) : ThemePalette.Blend(p.Accent, back, 0.6f);
        if (!enabled) trackColor = ThemePalette.Blend(trackColor, back, 0.4f);

        using (var path = ModernLayout.RoundedRect(new RectangleF(left - trackH / 2f, cy - trackH / 2f, right - left + trackH, trackH), trackH / 2f))
        using (var brush = new SolidBrush(trackColor))
            g.FillPath(brush, path);
        using (var path = ModernLayout.RoundedRect(new RectangleF(left - trackH / 2f, cy - trackH / 2f, thumbX - left + trackH, trackH), trackH / 2f))
        using (var brush = new SolidBrush(accent))
            g.FillPath(brush, path);

        // Ticks (stops) on the track: small dots, light on the filled part
        for (int v = _minimum; v <= _maximum; v++)
        {
            if (v == _value) continue;
            float x = XOf(v);
            Color dot = v < _value
                ? Color.FromArgb(enabled ? 190 : 120, p.TextOnAccent)
                : ThemePalette.Blend(p.TextMuted, back, enabled ? 0.15f : 0.55f);
            float d = 3f * s;
            using var brush = new SolidBrush(dot);
            g.FillEllipse(brush, x - d / 2f, cy - d / 2f, d, d);
        }

        // Tick labels
        var labelFont = ModernFonts.Small;
        float labelY = cy + ThumbD / 2f + 5 * s;
        for (int v = _minimum; v <= _maximum; v++)
        {
            int idx = v - _minimum;
            string text = _tickLabels != null && idx < _tickLabels.Length ? _tickLabels[idx] : v.ToString();
            if (text.Length == 0) continue;
            bool current = v == _value;
            var font = current ? ThemeFonts.Semibold : labelFont;
            Color c = current ? (enabled ? p.Text : ModernLayout.Disabled(p.Text, back)) : (enabled ? p.TextMuted : ModernLayout.Disabled(p.TextMuted, back));
            var size = TextRenderer.MeasureText(g, text, font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            int x = (int)Math.Round(XOf(v) - size.Width / 2f);
            x = Math.Clamp(x, 0, Math.Max(0, Width - size.Width));
            TextRenderer.DrawText(g, text, font, new Rectangle(x, (int)labelY, size.Width + 1, size.Height), c,
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }

        // Thumb with halo (hover / drag / focus)
        var knob = ThumbRect();
        if (enabled && (_dragging || _thumbHover || Focused))
        {
            int alpha = _dragging ? 70 : Focused && ShowFocusCues ? 60 : 40;
            using var halo = new SolidBrush(Color.FromArgb(alpha, p.Accent));
            g.FillEllipse(halo, RectangleF.Inflate(knob, 4 * s, 4 * s));
        }
        Color thumbFill = enabled ? Color.White : ThemePalette.Blend(Color.White, back, 0.45f);
        using (var shadow = new SolidBrush(Color.FromArgb(p.IsDark ? 70 : 35, 0, 0, 0)))
            g.FillEllipse(shadow, knob.X, knob.Y + 1 * s, knob.Width, knob.Height);
        using (var brush = new SolidBrush(thumbFill))
            g.FillEllipse(brush, knob);
        using (var pen = new Pen(enabled ? accent : ThemePalette.Blend(p.InputBorder, back, 0.3f), 2f * s))
            g.DrawEllipse(pen, RectangleF.Inflate(knob, -1f * s, -1f * s));
        if (enabled && Focused && ShowFocusCues)
        {
            using var ring = new Pen(Color.FromArgb(200, p.Accent), 1.5f * s);
            g.DrawEllipse(ring, RectangleF.Inflate(knob, 3.5f * s, 3.5f * s));
        }
    }
}

/// <summary>
/// Folder picker field: rounded input-style frame with a folder glyph, the path (shortened
/// in the middle when it does not fit; full path as tooltip), an "open in Explorer" icon
/// button and an integrated <see cref="BrowseButton"/>. A click on the path also browses;
/// the context menu copies the path.
/// </summary>
internal class FolderPathPicker : Panel, IThemeAware
{
    private readonly Button _browse;
    private readonly FlatIconButton _open;
    private readonly ToolTip _toolTip = new() { ShowAlways = true };
    private string _path = "";
    private bool _hover;

    public FolderPathPicker(string browseText = "Durchsuchen...")
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Size = new Size(400, 38);
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.Grouping;

        _browse = new Button { Text = browseText, Size = new Size(124, 28) };
        ThemeManager.SetGlyph(_browse, Glyph.Folder);
        _open = new FlatIconButton(Glyph.OpenExternal) { Size = new Size(28, 28), AccessibleName = "Ordner im Explorer öffnen" };
        _open.Click += (_, _) => OpenInExplorer();
        _toolTip.SetToolTip(_open, "Ordner im Explorer öffnen");
        Controls.Add(_open);
        Controls.Add(_browse);

        var menu = new ContextMenuStrip();
        var copy = new ToolStripMenuItem("Pfad kopieren", null, (_, _) => { if (_path.Length > 0) try { Clipboard.SetText(_path); } catch { } });
        menu.Items.Add(copy);
        menu.Opening += (_, e) => copy.Enabled = _path.Length > 0;
        ContextMenuStrip = menu;
    }

    /// <summary>The integrated browse button (opens the folder dialog).</summary>
    public Button BrowseButton => _browse;

    /// <summary>The folder shown in the field.</summary>
    [AllowNull]
    public string Path
    {
        get => _path;
        set
        {
            _path = value ?? "";
            AccessibleName = "Speicherordner: " + _path;
            _toolTip.SetToolTip(this, _path);
            UpdateOpenState();
            Invalidate();
        }
    }

    private void UpdateOpenState()
    {
        bool exists;
        try { exists = _path.Length > 0 && Directory.Exists(_path); }
        catch { exists = false; }
        _open.Enabled = exists;
    }

    private void OpenInExplorer()
    {
        UpdateOpenState();
        if (!_open.Enabled) return;
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + _path + "\"") { UseShellExecute = true });
        }
        catch
        {
            // Explorer could not be started: nothing to do
        }
    }

    public void ApplyTheme(ThemePalette palette)
    {
        BackColor = palette.Input;
        Invalidate();
    }

    private int S(float v) => ModernLayout.Scale(this, v);

    private Rectangle TextArea()
    {
        if (_open == null) return Rectangle.Empty;
        int left = S(36);
        int right = _open.Left - S(6);
        return new Rectangle(left, 0, Math.Max(0, right - left), Height);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (_browse == null || _open == null) return; // base constructor (Size) runs before the children exist
        int h = Height - S(8);
        int bw = _browse.Width;
        _browse.SetBounds(Width - S(4) - bw, (Height - h) / 2, bw, h);
        int ob = h;
        _open.SetBounds(_browse.Left - S(4) - ob, (Height - ob) / 2, ob, ob);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left && Enabled && _browse.Enabled)
            _browse.PerformClick();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = ThemeManager.Current;
        var g = e.Graphics;
        var back = ModernLayout.ParentBack(this);
        g.Clear(back);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        float s = DeviceDpi / 96f;

        bool focused = ContainsFocus;
        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Color border = focused ? p.Accent : _hover && Enabled ? (p.IsDark ? p.TextMuted : p.TextMuted) : p.InputBorder;
        using (var path = ModernLayout.RoundedRect(rect, 6 * s))
        {
            using (var brush = new SolidBrush(p.Input))
                g.FillPath(brush, path);
            using var pen = new Pen(border, 1f);
            g.DrawPath(pen, path);
        }

        float gs = 18 * s;
        ThemeGlyphs.Draw(g, Glyph.Folder, new RectangleF(S(11), (Height - gs) / 2f, gs, gs),
            Enabled ? p.Accent : ModernLayout.Disabled(p.TextMuted, p.Input));

        g.SmoothingMode = SmoothingMode.None;
        var area = TextArea();
        TextRenderer.DrawText(g, _path.Length > 0 ? _path : "Kein Ordner gewählt", ModernFonts.RowTitle, area,
            Enabled ? (_path.Length > 0 ? p.Text : p.TextMuted) : ModernLayout.Disabled(p.Text, p.Input),
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
            | TextFormatFlags.PathEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (e.Control != null)
        {
            e.Control.GotFocus += (_, _) => Invalidate();
            e.Control.LostFocus += (_, _) => Invalidate();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
            ContextMenuStrip?.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Small flat icon button (transparent, round hover background like the window header
/// buttons). Focusable; Space/Enter click it.
/// </summary>
internal sealed class FlatIconButton : Control, IThemeAware
{
    private readonly Glyph _glyph;
    private bool _hover;
    private bool _pressed;

    public FlatIconButton(Glyph glyph)
    {
        _glyph = glyph;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true;
        AccessibleRole = AccessibleRole.PushButton;
    }

    public void ApplyTheme(ThemePalette palette) => Invalidate();

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Space or Keys.Enter || base.IsInputKey(keyData);

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.KeyCode is Keys.Space or Keys.Enter && Enabled)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = ThemeManager.Current;
        var g = e.Graphics;
        var back = ModernLayout.ParentBack(this);
        g.Clear(back);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        float s = DeviceDpi / 96f;
        Color fore = Enabled ? p.TextMuted : ModernLayout.Disabled(p.TextMuted, back);
        if (Enabled && (_hover || _pressed))
        {
            using var brush = new SolidBrush(_pressed ? p.Selected : p.Hover);
            using var path = ModernLayout.RoundedRect(new RectangleF(0, 0, Width, Height), 4 * s);
            g.FillPath(brush, path);
            fore = p.Text;
        }
        if (Focused && ShowFocusCues && Enabled)
        {
            using var pen = new Pen(Color.FromArgb(200, p.Accent), 1.5f * s);
            using var path = ModernLayout.RoundedRect(new RectangleF(0.75f * s, 0.75f * s, Width - 1.5f * s, Height - 1.5f * s), 4 * s);
            g.DrawPath(pen, path);
        }
        float gs = 16 * s;
        ThemeGlyphs.Draw(g, _glyph, new RectangleF((Width - gs) / 2f, (Height - gs) / 2f, gs, gs), fore);
    }
}

/// <summary>Factory for the buttons of the settings pages.</summary>
internal static class ModernButtons
{
    /// <summary>
    /// Secondary button that opens a sub-dialog: text followed by a chevron, width fitted to
    /// the text (96-dpi units, at least <paramref name="minWidth"/>).
    /// </summary>
    public static Button SubDialog(string text, int minWidth = 0)
    {
        var b = new Button { Text = text };
        ThemeManager.SetRole(b, ThemeRole.Secondary);
        ThemeManager.SetTrailingGlyph(b, Glyph.ChevronRight);
        b.Size = new Size(Math.Max(minWidth, TextWidth96(text) + 16 + 6 + 12 + 8), 30);
        return b;
    }

    /// <summary>
    /// Sub-dialog button with a colored leading <paramref name="glyph"/> (e.g. a blue envelope
    /// on "E-Mail-Optionen..."), the text and a chevron.
    /// </summary>
    public static Button SubDialog(string text, Glyph glyph, Color color, int minWidth = 0)
    {
        var b = SubDialog(text, minWidth);
        ThemeManager.SetGlyph(b, glyph, color);
        b.Width = Math.Max(minWidth, TextWidth96(text) + 16 + 6 + 12 + 8 + 16 + 6);
        return b;
    }

    /// <summary>Text width in 96-dpi units (button font = Segoe UI 9).</summary>
    public static int TextWidth96(string text, Font? font = null)
    {
        using var g = Graphics.FromHwnd(IntPtr.Zero);
        var size = TextRenderer.MeasureText(g, text, font ?? ThemeFonts.Regular, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        return (int)Math.Ceiling(size.Width * 96f / g.DpiX);
    }
}

/// <summary>Row of buttons sharing its width equally (gap 8), e.g. under a stacked setting row.</summary>
internal sealed class ButtonRow : Panel
{
    public ButtonRow()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
    }

    protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(ModernLayout.ParentBack(this));

    protected override void OnLayout(LayoutEventArgs levent)
    {
        var buttons = Controls.Cast<Control>().Where(c => c.Visible).ToList();
        if (buttons.Count == 0) return;
        int gap = ModernLayout.Scale(this, 8);
        int w = (Width - gap * (buttons.Count - 1)) / buttons.Count;
        int x = 0;
        for (int i = 0; i < buttons.Count; i++)
        {
            int bw = i == buttons.Count - 1 ? Width - x : w;
            buttons[i].SetBounds(x, 0, bw, Height);
            x += w + gap;
        }
    }
}
