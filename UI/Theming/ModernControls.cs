using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>Fonts of the modern (settings) layout.</summary>
internal static class ModernFonts
{
    /// <summary>Window header title.</summary>
    public static readonly Font WindowTitle = new("Segoe UI Semibold", 12f);
    /// <summary>Page title (settings pages).</summary>
    public static readonly Font PageTitle = new("Segoe UI Semibold", 14f);
    /// <summary>Card title.</summary>
    public static readonly Font CardTitle = new("Segoe UI Semibold", 10f);
    /// <summary>Setting row title.</summary>
    public static readonly Font RowTitle = new("Segoe UI", 9.5f);
    /// <summary>Descriptions and hints.</summary>
    public static readonly Font Small = new("Segoe UI", 8.5f);
    /// <summary>Upper-case section caption (sidebar groups).</summary>
    public static readonly Font Caption = new("Segoe UI Semibold", 7.5f);
}

/// <summary>
/// Controls that compute their height from a given width (used by <see cref="ModernStack"/>,
/// <see cref="SettingsCard"/> and <see cref="TileGroup"/>). Values are device pixels.
/// </summary>
internal interface IHeightForWidth
{
    int GetHeightForWidth(int width);
}

/// <summary>Layout helpers of the modern controls.</summary>
internal static class ModernLayout
{
    public static int Scale(Control c, float value) => (int)Math.Round(value * c.DeviceDpi / 96f);

    /// <summary>Height a child needs at <paramref name="width"/>.</summary>
    public static int HeightOf(Control c, int width)
    {
        if (c is IHeightForWidth h) return h.GetHeightForWidth(width);
        if (c.AutoSize) return c.GetPreferredSize(new Size(width, 0)).Height;
        return c.Height;
    }

    /// <summary>Surface the control sits on (first opaque parent color).</summary>
    public static Color ParentBack(Control c)
    {
        var parent = c.Parent;
        while (parent != null && parent.BackColor.A < 255)
            parent = parent.Parent;
        return parent?.BackColor ?? ThemeManager.Current.Background;
    }

    public static Color Disabled(Color fore, Color back) => ThemePalette.Blend(fore, back, 0.55f);

    public static GraphicsPath RoundedRect(RectangleF r, float radius) => ThemeGlyphs.Rounded(r, radius);
}

/// <summary>
/// Vertical stack: lays out its children top-down at full width with <see cref="Spacing"/>
/// between them (children's heights from <see cref="IHeightForWidth"/>, the preferred size of
/// AutoSize controls, or their Height). Its own height follows the content (<see cref="AutoHeight"/>).
/// Hidden children are skipped. Children are laid out in the order they were added.
/// </summary>
internal class ModernStack : Panel, IHeightForWidth
{
    private int _spacing = 12;

    public ModernStack()
    {
        DoubleBuffered = true;
    }

    /// <summary>Gap between children (96-dpi units).</summary>
    [DefaultValue(12)]
    public int Spacing
    {
        get => _spacing;
        set { _spacing = value; PerformLayout(); }
    }

    /// <summary>Height follows the content (default true).</summary>
    [DefaultValue(true)]
    public bool AutoHeight { get; set; } = true;

    public int GetHeightForWidth(int width)
    {
        int inner = Math.Max(0, width - Padding.Horizontal);
        int h = Padding.Vertical;
        bool first = true;
        foreach (Control c in Controls)
        {
            if (!c.Visible) continue;
            if (!first) h += ModernLayout.Scale(this, _spacing);
            h += ModernLayout.HeightOf(c, inner);
            first = false;
        }
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
        // Children report Visible = false while this stack is hidden: lay out when shown.
        if (!Visible) return;
        int inner = Math.Max(0, ClientSize.Width - Padding.Horizontal);
        int y = Padding.Top;
        bool first = true;
        foreach (Control c in Controls)
        {
            if (!c.Visible) continue;
            if (!first) y += ModernLayout.Scale(this, _spacing);
            int h = ModernLayout.HeightOf(c, inner);
            c.SetBounds(Padding.Left, y, inner, h);
            y += h;
            first = false;
        }
        y += Padding.Bottom;
        if (AutoHeight && Dock is DockStyle.None or DockStyle.Top or DockStyle.Bottom && Height != y)
            Height = y;
    }
}

/// <summary>
/// Rounded settings card (Discord style): optional <see cref="Title"/> and
/// <see cref="Description"/> at the top, then its children (usually <see cref="SettingRow"/>s)
/// stacked at full width with divider lines between them. Height follows the content.
/// </summary>
internal class SettingsCard : Panel, IThemeAware, IHeightForWidth
{
    private string _title;
    private string _description;
    private bool _showDividers = true;

    public SettingsCard(string? title = null, string? description = null)
    {
        _title = title ?? "";
        _description = description ?? "";
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        ApplyTheme(ThemeManager.Current);
    }

    [AllowNull]
    public string Title { get => _title; set { _title = value ?? ""; PerformLayout(); Invalidate(); } }

    [AllowNull]
    public string Description { get => _description; set { _description = value ?? ""; PerformLayout(); Invalidate(); } }

    /// <summary>Divider lines between the rows (default true).</summary>
    [DefaultValue(true)]
    public bool ShowDividers { get => _showDividers; set { _showDividers = value; PerformLayout(); Invalidate(); } }

    /// <summary>Horizontal inner padding (96-dpi units, default 16).</summary>
    [DefaultValue(16)]
    public int InnerPadding { get; set; } = 16;

    /// <summary>Adds a <see cref="SettingRow"/> and returns it.</summary>
    public SettingRow AddRow(string title, string? description = null, Control? control = null, int controlWidth = 0)
    {
        var row = new SettingRow(title, description, control) { ControlWidth = controlWidth };
        Controls.Add(row);
        return row;
    }

    /// <summary>Adds any control as full-width row content.</summary>
    public T Add<T>(T control) where T : Control
    {
        Controls.Add(control);
        return control;
    }

    public void ApplyTheme(ThemePalette palette)
    {
        BackColor = palette.Card;
        Invalidate();
    }

    private int S(float v) => ModernLayout.Scale(this, v);

    private bool HasHeader => _title.Length > 0 || _description.Length > 0;

    /// <summary>Gap between rows that holds the divider line (1 px, only with dividers).</summary>
    private int DividerGap => _showDividers ? 1 : 0;

    private int HeaderHeight(int width)
    {
        if (!HasHeader) return S(4);
        int textW = Math.Max(1, width - 2 * S(InnerPadding));
        int h = S(14);
        var flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        if (_title.Length > 0)
            h += TextRenderer.MeasureText(_title, ModernFonts.CardTitle, new Size(textW, 0), flags).Height;
        if (_description.Length > 0)
            h += S(2) + TextRenderer.MeasureText(_description, ModernFonts.Small, new Size(textW, 0), flags).Height;
        return h + S(6);
    }

    public int GetHeightForWidth(int width)
    {
        int inner = Math.Max(0, width - 2 * S(InnerPadding));
        int h = HeaderHeight(width);
        bool first = true;
        foreach (Control c in Controls)
        {
            if (!c.Visible) continue;
            if (!first || HasHeader) h += DividerGap;
            h += ModernLayout.HeightOf(c, inner);
            first = false;
        }
        return h + S(4);
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
        int pad = S(InnerPadding);
        int inner = Math.Max(0, Width - 2 * pad);
        int y = HeaderHeight(Width);
        bool first = true;
        foreach (Control c in Controls)
        {
            if (!c.Visible) continue;
            if (!first || HasHeader) y += DividerGap;
            int h = ModernLayout.HeightOf(c, inner);
            c.SetBounds(pad, y, inner, h);
            y += h;
            first = false;
        }
        y += S(4);
        if (Height != y && Dock is DockStyle.None or DockStyle.Top)
            Height = y;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = ThemeManager.Current;
        var g = e.Graphics;
        g.Clear(ModernLayout.ParentBack(this));
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using (var path = ModernLayout.RoundedRect(rect, S(8)))
        {
            using (var brush = new SolidBrush(BackColor))
                g.FillPath(brush, path);
            using var pen = new Pen(p.CardBorder, 1f);
            g.DrawPath(pen, path);
        }
        g.SmoothingMode = SmoothingMode.None;
        g.PixelOffsetMode = PixelOffsetMode.Default;

        int pad = S(InnerPadding);
        int textW = Math.Max(1, Width - 2 * pad);
        var flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        bool enabled = Enabled;
        int y = S(14);
        if (_title.Length > 0)
        {
            var size = TextRenderer.MeasureText(g, _title, ModernFonts.CardTitle, new Size(textW, 0), flags);
            TextRenderer.DrawText(g, _title, ModernFonts.CardTitle, new Rectangle(pad, y, textW, size.Height),
                enabled ? p.Text : ModernLayout.Disabled(p.Text, BackColor), flags);
            y += size.Height;
        }
        if (_description.Length > 0)
        {
            y += S(2);
            var size = TextRenderer.MeasureText(g, _description, ModernFonts.Small, new Size(textW, 0), flags);
            TextRenderer.DrawText(g, _description, ModernFonts.Small, new Rectangle(pad, y, textW, size.Height),
                enabled ? p.TextMuted : ModernLayout.Disabled(p.TextMuted, BackColor), flags);
        }

        if (!_showDividers) return;
        using var line = new Pen(p.CardDivider);
        bool first = true;
        foreach (Control c in Controls)
        {
            if (!c.Visible) continue;
            if (!first || HasHeader)
                g.DrawLine(line, pad, c.Top - 1, Width - pad - 1, c.Top - 1);
            first = false;
        }
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }
}

/// <summary>
/// One setting (Discord style): title and muted description on the left, the control on the
/// right (vertically centered) — or below the text when <see cref="Stacked"/> is set. A click
/// on the text toggles a hosted CheckBox/<see cref="ToggleSwitch"/>. The text is drawn
/// disabled when the row or its control is disabled.
/// </summary>
internal class SettingRow : Panel, IThemeAware, IHeightForWidth
{
    private string _title;
    private string _description;
    private ThemeRole _descriptionRole = ThemeRole.Muted;
    private Control? _control;
    private int _controlWidth;
    private bool _stacked;

    public SettingRow(string title, string? description = null, Control? control = null)
    {
        _title = title;
        _description = description ?? "";
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        if (control != null)
            Control = control;
    }

    [AllowNull]
    public string Title
    {
        get => _title;
        set { _title = value ?? ""; RelayoutParent(); }
    }

    [AllowNull]
    public string Description
    {
        get => _description;
        set
        {
            value ??= "";
            if (value == _description) return;
            _description = value;
            RelayoutParent();
        }
    }

    /// <summary>Color of the description: Muted (default), Danger, Warning, Success or Accent.</summary>
    public ThemeRole DescriptionRole
    {
        get => _descriptionRole;
        set { _descriptionRole = value; Invalidate(); }
    }

    /// <summary>The control on the right (or below when stacked).</summary>
    public Control? Control
    {
        get => _control;
        set
        {
            if (_control != null)
            {
                _control.EnabledChanged -= OnControlEnabledChanged;
                Controls.Remove(_control);
            }
            _control = value;
            if (_control != null)
            {
                if (string.IsNullOrEmpty(_control.AccessibleName))
                    _control.AccessibleName = _title.TrimEnd(':');
                // Remember the width the control was created with (96-dpi units, the row
                // scales it); AutoSize controls use their preferred width.
                if (_controlWidth == 0 && !_control.AutoSize)
                    _controlWidth = _control.Width;
                _control.EnabledChanged += OnControlEnabledChanged;
                Controls.Add(_control);
            }
            RelayoutParent();
        }
    }

    /// <summary>
    /// Width of the control in 96-dpi units. 0 = the width the control had when it was
    /// assigned (or the preferred width of an AutoSize control).
    /// </summary>
    public int ControlWidth
    {
        get => _controlWidth;
        set
        {
            if (value == 0 && _control != null && !_control.AutoSize) return;
            _controlWidth = value;
            RelayoutParent();
        }
    }

    /// <summary>Control below the text at full width (wide controls such as a path box).</summary>
    public bool Stacked
    {
        get => _stacked;
        set { _stacked = value; RelayoutParent(); }
    }

    public void ApplyTheme(ThemePalette palette) => Invalidate();

    private int S(float v) => ModernLayout.Scale(this, v);

    private void OnControlEnabledChanged(object? sender, EventArgs e) => Invalidate();

    private void RelayoutParent()
    {
        Parent?.PerformLayout();
        PerformLayout();
        Invalidate();
    }

    private int ControlPixelWidth(int width)
    {
        if (_control == null) return 0;
        if (_stacked) return width;
        int w = _controlWidth > 0 ? S(_controlWidth)
            : _control.AutoSize ? _control.GetPreferredSize(Size.Empty).Width
            : _control.Width;
        return Math.Min(w, Math.Max(S(40), width / 2 + S(60)));
    }

    private int TextWidth(int width)
    {
        if (_control == null || _stacked) return Math.Max(1, width);
        return Math.Max(1, width - ControlPixelWidth(width) - S(20));
    }

    private const TextFormatFlags TextFlags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

    private (int titleH, int descH) MeasureText(int textW)
    {
        int th = _title.Length > 0 ? TextRenderer.MeasureText(_title, ModernFonts.RowTitle, new Size(textW, 0), TextFlags).Height : 0;
        int dh = _description.Length > 0 ? TextRenderer.MeasureText(_description, ModernFonts.Small, new Size(textW, 0), TextFlags).Height : 0;
        return (th, dh);
    }

    private int TextBlockHeight(int width)
    {
        var (th, dh) = MeasureText(TextWidth(width));
        return th + (dh > 0 ? S(2) + dh : 0);
    }

    private int ControlHeight() => _control == null ? 0 : ModernLayout.HeightOf(_control, _control.Width);

    public int GetHeightForWidth(int width)
    {
        int text = TextBlockHeight(width);
        int pad = S(12);
        if (_control == null) return text + 2 * pad;
        int ch = _stacked ? ModernLayout.HeightOf(_control, width) : ControlHeight();
        if (_stacked) return pad + text + (text > 0 ? S(8) : 0) + ch + pad;
        return Math.Max(text, ch) + 2 * pad;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        int w = proposedSize.Width > 0 && proposedSize.Width < int.MaxValue / 2 ? proposedSize.Width : Width;
        return new Size(w, GetHeightForWidth(w));
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (_control == null) return;
        int pad = S(12);
        if (_stacked)
        {
            int text = TextBlockHeight(Width);
            int y = pad + text + (text > 0 ? S(8) : 0);
            _control.SetBounds(0, y, Width, ModernLayout.HeightOf(_control, Width));
        }
        else
        {
            int w = ControlPixelWidth(Width);
            int h = ControlHeight();
            _control.SetBounds(Width - w, (Height - h) / 2, w, h);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = ThemeManager.Current;
        var g = e.Graphics;
        var back = ModernLayout.ParentBack(this);
        if (BackColor.A == 255) back = BackColor;
        g.Clear(back);

        bool enabled = Enabled && (_control?.Enabled ?? true);
        int textW = TextWidth(Width);
        var (th, dh) = MeasureText(textW);
        int block = th + (dh > 0 ? S(2) + dh : 0);
        int y = _stacked || _control == null ? S(12) : Math.Max(S(12), (Height - block) / 2);

        if (th > 0)
        {
            TextRenderer.DrawText(g, _title, ModernFonts.RowTitle, new Rectangle(0, y, textW, th),
                enabled ? p.Text : ModernLayout.Disabled(p.Text, back), TextFlags);
            y += th + S(2);
        }
        if (dh > 0)
        {
            var color = _descriptionRole switch
            {
                ThemeRole.Danger => p.Danger,
                ThemeRole.Warning => p.Warning,
                ThemeRole.Success => p.Success,
                ThemeRole.Accent => p.Accent,
                _ => p.TextMuted
            };
            TextRenderer.DrawText(g, _description, ModernFonts.Small, new Rectangle(0, y, textW, dh),
                enabled ? color : ModernLayout.Disabled(color, back), TextFlags);
        }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left && _control is CheckBox check && check.Enabled && Enabled && !_stacked)
        {
            check.Focus();
            check.Checked = !check.Checked;
        }
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }
}

/// <summary>On/off switch (a CheckBox drawn as a Discord-style toggle, 40 x 24).</summary>
internal class ToggleSwitch : CheckBox, IThemeAware
{
    private bool _hover;

    public ToggleSwitch()
    {
        AutoSize = false;
        Size = new Size(40, 24);
        Text = "";
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        AccessibleRole = AccessibleRole.CheckButton;
    }

    public void ApplyTheme(ThemePalette palette) => Invalidate();

    protected override void OnMouseEnter(EventArgs eventargs) { _hover = true; Invalidate(); base.OnMouseEnter(eventargs); }
    protected override void OnMouseLeave(EventArgs eventargs) { _hover = false; Invalidate(); base.OnMouseLeave(eventargs); }
    protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var p = ThemeManager.Current;
        var g = pevent.Graphics;
        var back = ModernLayout.ParentBack(this);
        g.Clear(back);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        float s = DeviceDpi / 96f;

        float trackH = 24 * s, trackW = 40 * s;
        var track = new RectangleF((Width - trackW) / 2f, (Height - trackH) / 2f, trackW, trackH);
        Color off = p.IsDark ? Color.FromArgb(0x80, 0x84, 0x8E) : Color.FromArgb(0xB5, 0xBA, 0xC1);
        Color on = _hover && Enabled ? p.AccentHover : p.Accent;
        Color fill = Checked ? on : (_hover && Enabled ? ThemePalette.Blend(off, p.Text, 0.15f) : off);
        Color knob = Color.White;
        if (!Enabled)
        {
            fill = ThemePalette.Blend(fill, back, 0.55f);
            knob = ThemePalette.Blend(knob, back, 0.35f);
        }

        using (var path = ModernLayout.RoundedRect(track, trackH / 2f))
        using (var brush = new SolidBrush(fill))
            g.FillPath(brush, path);

        float d = trackH - 6 * s;
        float kx = Checked ? track.Right - 3 * s - d : track.X + 3 * s;
        var knobRect = new RectangleF(kx, track.Y + 3 * s, d, d);
        using (var brush = new SolidBrush(knob))
            g.FillEllipse(brush, knobRect);

        // Check / cross in the knob
        using var pen = new Pen(fill, 1.6f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        float cx = knobRect.X + d / 2f, cy = knobRect.Y + d / 2f;
        if (Checked)
            g.DrawLines(pen, new[] { new PointF(cx - 3.5f * s, cy + 0.2f * s), new PointF(cx - 1f * s, cy + 2.6f * s), new PointF(cx + 3.6f * s, cy - 2.6f * s) });
        else
        {
            g.DrawLine(pen, cx - 2.6f * s, cy - 2.6f * s, cx + 2.6f * s, cy + 2.6f * s);
            g.DrawLine(pen, cx + 2.6f * s, cy - 2.6f * s, cx - 2.6f * s, cy + 2.6f * s);
        }

        if (Focused && ShowFocusCues && Enabled)
        {
            using var ring = new Pen(Color.FromArgb(160, p.Accent), 1.5f * s);
            using var rp = ModernLayout.RoundedRect(RectangleF.Inflate(track, 1.5f * s, 1.5f * s), trackH / 2f + 1.5f * s);
            g.DrawPath(ring, rp);
        }
    }
}

/// <summary>Arrangement of a <see cref="TileButton"/>'s content.</summary>
internal enum TileLayout
{
    /// <summary>Icon centered at the top, title (and description) below — Quick-Menü tiles.</summary>
    Vertical,
    /// <summary>Icon left, title and description right.</summary>
    Horizontal
}

/// <summary>
/// Selectable tile (generalised Quick-Menü tile): rounded surface with an icon
/// (<see cref="Icon"/> image or tinted <see cref="Glyph"/>), title and optional description;
/// hover, pressed and accent selection with a check badge. Raises Click / DoubleClick.
/// Not focusable by default (the owner handles arrow keys); set <see cref="Focusable"/> for
/// Tab/Space/Enter support.
/// </summary>
internal class TileButton : Control, IThemeAware
{
    private Image? _icon;
    private Glyph _glyph;
    private string _description = "";
    private bool _selected;
    private bool _hover;
    private bool _pressed;
    private TileLayout _layout = SpeedScanManager.TileLayout.Vertical;
    private int _iconSize;
    private bool _focusable;

    public TileButton()
    {
        Font = ThemeFonts.Regular;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.StandardDoubleClick, true);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        AccessibleRole = AccessibleRole.PushButton;
    }

    public TileButton(string title, Image? icon = null, Glyph glyph = Glyph.None, string? description = null) : this()
    {
        Text = title;
        AccessibleName = title;
        _icon = icon;
        _glyph = glyph;
        _description = description ?? "";
    }

    /// <summary>Picture icon (drawn as is).</summary>
    public Image? Icon { get => _icon; set { _icon = value; Invalidate(); } }

    /// <summary>Line glyph (drawn in a tinted chip; used when <see cref="Icon"/> is null).</summary>
    public Glyph Glyph { get => _glyph; set { _glyph = value; Invalidate(); } }

    [AllowNull]
    public string Description { get => _description; set { _description = value ?? ""; Invalidate(); } }

    public TileLayout TileLayout { get => _layout; set { _layout = value; Invalidate(); } }

    /// <summary>Icon size in 96-dpi units (0 = default: 44 for images, 36 chip for glyphs).</summary>
    public int IconSize { get => _iconSize; set { _iconSize = value; Invalidate(); } }

    /// <summary>Show the check badge on the selected tile (default true).</summary>
    public bool ShowCheckBadge { get; set; } = true;

    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
            Invalidate();
        }
    }

    /// <summary>Tile can get the keyboard focus (Tab); Space/Enter click it.</summary>
    public bool Focusable
    {
        get => _focusable;
        set
        {
            _focusable = value;
            SetStyle(ControlStyles.Selectable, value);
            TabStop = value;
        }
    }

    public void ApplyTheme(ThemePalette palette) => Invalidate();

    private float S => DeviceDpi / 96f;

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); if (_focusable) Focus(); }
        base.OnMouseDown(e);
    }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override bool IsInputKey(Keys keyData) => _focusable && keyData is Keys.Space or Keys.Enter || base.IsInputKey(keyData);

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (_focusable && e.KeyCode is Keys.Space or Keys.Enter && Enabled)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
        }
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new TileAccessibleObject(this);

    private sealed class TileAccessibleObject : ControlAccessibleObject
    {
        private readonly TileButton _tile;
        public TileAccessibleObject(TileButton tile) : base(tile) => _tile = tile;
        public override AccessibleStates State => base.State | (_tile.Selected ? AccessibleStates.Checked : 0);
        public override string? Description => _tile.Description.Length > 0 ? _tile.Description : base.Description;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = ThemeManager.Current;
        var g = e.Graphics;
        float s = S;
        var parentBg = ModernLayout.ParentBack(this);
        g.Clear(parentBg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        // On the main background the tile is a secondary surface; on a card it stands out from the card.
        Color baseFill = parentBg.ToArgb() == p.Background.ToArgb() ? p.Secondary : p.TileOnCard;
        bool active = Enabled;
        Color fill = _selected
            ? ThemePalette.Blend(baseFill, p.Accent, p.IsDark ? 0.22f : 0.10f)
            : !active ? baseFill : _pressed ? p.Selected : _hover ? p.Hover : baseFill;
        Color border = _selected ? p.Accent
            : _hover && active ? (p.IsDark ? p.ButtonSecondary : p.TextMuted)
            : p.IsDark ? p.Border : p.InputBorder;
        if (!active)
        {
            fill = ThemePalette.Blend(fill, parentBg, 0.4f);
            border = ThemePalette.Blend(border, parentBg, 0.5f);
        }

        var rect = new RectangleF(1f, 1f, Width - 2f, Height - 2f);
        using (var path = ModernLayout.RoundedRect(rect, 8 * s))
        {
            using (var brush = new SolidBrush(fill))
                g.FillPath(brush, path);
            using var pen = new Pen(border, _selected ? 2f * s : 1f);
            g.DrawPath(pen, path);
        }

        Color titleColor = _selected ? p.Text : p.TextSecondary;
        Color descColor = p.TextMuted;
        if (!active)
        {
            titleColor = ModernLayout.Disabled(titleColor, fill);
            descColor = ModernLayout.Disabled(descColor, fill);
        }
        var titleFont = _selected ? ThemeFonts.Semibold : ThemeFonts.Regular;
        var flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;

        if (_layout == SpeedScanManager.TileLayout.Vertical)
        {
            int iconSize = (int)Math.Round((_iconSize > 0 ? _iconSize : _icon != null ? 44 : 36) * s);
            int descH = _description.Length > 0
                ? TextRenderer.MeasureText(g, _description, ModernFonts.Small, new Size(Width - (int)(12 * s), 0), flags | TextFormatFlags.NoPadding).Height
                : 0;
            int titleH = TextRenderer.MeasureText(g, Text, titleFont, new Size(Width - (int)(8 * s), 0), flags).Height;
            int contentH = iconSize + (int)(8 * s) + titleH + (descH > 0 ? descH + (int)(2 * s) : 0);
            // With a description all tiles of a row align at the top (texts may wrap differently).
            int iconY = _description.Length > 0
                ? (int)(16 * s)
                : Math.Max((int)(10 * s), (Height - iconSize - (int)(40 * s)) / 2);
            var iconRect = new Rectangle((Width - iconSize) / 2, iconY, iconSize, iconSize);
            if (_pressed) iconRect.Offset(0, 1);
            DrawIcon(g, p, iconRect, fill, active);

            var textRect = new Rectangle((int)(4 * s), iconRect.Bottom + (int)(8 * s), Width - (int)(8 * s),
                _description.Length > 0 ? titleH : Height - iconRect.Bottom - (int)(12 * s));
            TextRenderer.DrawText(g, Text, titleFont, textRect, titleColor, flags | TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
            if (descH > 0)
            {
                var descRect = new Rectangle((int)(6 * s), textRect.Bottom + (int)(2 * s), Width - (int)(12 * s), Math.Max(0, Height - textRect.Bottom - (int)(6 * s)));
                TextRenderer.DrawText(g, _description, ModernFonts.Small, descRect, descColor,
                    flags | TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPadding);
            }
        }
        else
        {
            int iconSize = (int)Math.Round((_iconSize > 0 ? _iconSize : 32) * s);
            int x = (int)(14 * s);
            var iconRect = new Rectangle(x, (Height - iconSize) / 2, iconSize, iconSize);
            DrawIcon(g, p, iconRect, fill, active);
            int tx = iconRect.Right + (int)(12 * s);
            int tw = Math.Max(1, Width - tx - (int)(34 * s));
            int titleH = TextRenderer.MeasureText(g, Text, titleFont, new Size(tw, 0), flags | TextFormatFlags.NoPadding).Height;
            int descH = _description.Length > 0
                ? TextRenderer.MeasureText(g, _description, ModernFonts.Small, new Size(tw, 0), flags | TextFormatFlags.NoPadding).Height
                : 0;
            int y = (Height - titleH - (descH > 0 ? descH + (int)(2 * s) : 0)) / 2;
            TextRenderer.DrawText(g, Text, titleFont, new Rectangle(tx, y, tw, titleH), titleColor, flags | TextFormatFlags.NoPadding);
            if (descH > 0)
                TextRenderer.DrawText(g, _description, ModernFonts.Small, new Rectangle(tx, y + titleH + (int)(2 * s), tw, descH), descColor, flags | TextFormatFlags.NoPadding);
        }

        if (_selected && ShowCheckBadge)
        {
            var badge = new RectangleF(Width - 25f * s, 7f * s, 18f * s, 18f * s);
            using (var brush = new SolidBrush(active ? p.Accent : ThemePalette.Blend(p.Accent, parentBg, 0.5f)))
                g.FillEllipse(brush, badge);
            using var pen = new Pen(p.TextOnAccent, 1.8f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            g.DrawLines(pen, new[]
            {
                new PointF(badge.X + 5f * s, badge.Y + 9.5f * s),
                new PointF(badge.X + 8f * s, badge.Y + 12.5f * s),
                new PointF(badge.X + 13f * s, badge.Y + 6.5f * s)
            });
        }

        if (_focusable && Focused && ShowFocusCues)
        {
            using var pen = new Pen(Color.FromArgb(170, p.Accent), 1.5f * s) { DashStyle = DashStyle.Solid };
            using var path = ModernLayout.RoundedRect(RectangleF.Inflate(rect, -3f * s, -3f * s), 6 * s);
            g.DrawPath(pen, path);
        }
    }

    private void DrawIcon(Graphics g, ThemePalette p, Rectangle rect, Color fill, bool active)
    {
        if (_icon != null)
        {
            if (active)
                g.DrawImage(_icon, rect);
            else
            {
                using var ia = new System.Drawing.Imaging.ImageAttributes();
                ia.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = 0.45f });
                g.DrawImage(_icon, rect, 0, 0, _icon.Width, _icon.Height, GraphicsUnit.Pixel, ia);
            }
            return;
        }
        if (_glyph == Glyph.None) return;
        Color chip = _selected ? p.Accent : ThemePalette.Blend(fill, p.Accent, p.IsDark ? 0.18f : 0.12f);
        Color glyphColor = _selected ? p.TextOnAccent : p.Accent;
        if (!active)
        {
            chip = ThemePalette.Blend(chip, fill, 0.5f);
            glyphColor = ThemePalette.Blend(glyphColor, fill, 0.5f);
        }
        using (var path = ModernLayout.RoundedRect(rect, rect.Width / 2f))
        using (var brush = new SolidBrush(chip))
            g.FillPath(brush, path);
        float gs = rect.Width * 0.55f;
        ThemeGlyphs.Draw(g, _glyph, new RectangleF(rect.X + (rect.Width - gs) / 2f, rect.Y + (rect.Height - gs) / 2f, gs, gs), glyphColor);
    }
}

/// <summary>Arranges its children (tiles) in <see cref="Columns"/> equal columns.</summary>
internal class TileGroup : Panel, IHeightForWidth
{
    public TileGroup()
    {
        DoubleBuffered = true;
    }

    /// <summary>Columns (default 4).</summary>
    public int Columns { get; set; } = 4;

    /// <summary>Tile height (96-dpi units, default 104).</summary>
    public int TileHeight { get; set; } = 104;

    /// <summary>Gap between tiles (96-dpi units, default 10).</summary>
    public int Gap { get; set; } = 10;

    /// <summary>Space above/below the tiles (96-dpi units, default 12).</summary>
    public int VerticalPadding { get; set; } = 12;

    private int S(float v) => ModernLayout.Scale(this, v);

    private List<Control> VisibleTiles()
    {
        var list = new List<Control>();
        foreach (Control c in Controls)
            if (c.Visible) list.Add(c);
        return list;
    }

    public int GetHeightForWidth(int width)
    {
        int n = Visible ? VisibleTiles().Count : Controls.Count;
        int rows = Math.Max(1, (n + Columns - 1) / Columns);
        return 2 * S(VerticalPadding) + rows * S(TileHeight) + (rows - 1) * S(Gap);
    }

    public override Size GetPreferredSize(Size proposedSize) => new(Width, GetHeightForWidth(Width));

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible) PerformLayout();
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        if (!Visible) return;
        var tiles = VisibleTiles();
        int gap = S(Gap);
        int cols = Math.Max(1, Columns);
        int colW = (Width - (cols - 1) * gap) / cols;
        int h = S(TileHeight);
        for (int i = 0; i < tiles.Count; i++)
        {
            int col = i % cols, row = i / cols;
            int x = col * (colW + gap);
            int w = col == cols - 1 ? Width - x : colW;
            tiles[i].SetBounds(x, S(VerticalPadding) + row * (h + gap), w, h);
        }
    }
}

/// <summary>Page title with a muted description (settings pages).</summary>
internal class PageHeader : Control, IThemeAware, IHeightForWidth
{
    private string _description;

    public PageHeader(string title, string? description = null)
    {
        Text = title;
        _description = description ?? "";
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
        AccessibleRole = AccessibleRole.StaticText;
        AccessibleName = title;
    }

    [AllowNull]
    public string Description { get => _description; set { _description = value ?? ""; Parent?.PerformLayout(); Invalidate(); } }

    public void ApplyTheme(ThemePalette palette) => Invalidate();

    private const TextFormatFlags Flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

    public int GetHeightForWidth(int width)
    {
        int w = Math.Max(1, width);
        int h = TextRenderer.MeasureText(Text, ModernFonts.PageTitle, new Size(w, 0), Flags).Height;
        if (_description.Length > 0)
            h += ModernLayout.Scale(this, 4) + TextRenderer.MeasureText(_description, ModernFonts.RowTitle, new Size(w, 0), Flags).Height;
        return h + ModernLayout.Scale(this, 4);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = ThemeManager.Current;
        var g = e.Graphics;
        g.Clear(ModernLayout.ParentBack(this));
        int w = Math.Max(1, Width);
        var th = TextRenderer.MeasureText(g, Text, ModernFonts.PageTitle, new Size(w, 0), Flags).Height;
        TextRenderer.DrawText(g, Text, ModernFonts.PageTitle, new Rectangle(0, 0, w, th), p.Text, Flags);
        if (_description.Length > 0)
        {
            int y = th + ModernLayout.Scale(this, 4);
            TextRenderer.DrawText(g, _description, ModernFonts.RowTitle, new Rectangle(0, y, w, Height - y), p.TextMuted, Flags);
        }
    }
}

/// <summary>Upper-case muted section caption (e.g. "SCANEINSTELLUNGEN").</summary>
internal class SectionHeading : Control, IThemeAware
{
    public SectionHeading(string text)
    {
        Text = text;
        Height = 28;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
        AccessibleRole = AccessibleRole.StaticText;
    }

    public void ApplyTheme(ThemePalette palette) => Invalidate();

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = ThemeManager.Current;
        e.Graphics.Clear(ModernLayout.ParentBack(this));
        TextRenderer.DrawText(e.Graphics, Text.ToUpperInvariant(), ModernFonts.Caption, ClientRectangle, p.TextMuted,
            TextFormatFlags.Left | TextFormatFlags.Bottom | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
    }
}

/// <summary>
/// Sidebar navigation (Discord settings style): optional upper-case group headings and items
/// with a glyph; the selected item is highlighted. Keyboard: Up/Down/Home/End. Disabled items
/// are drawn grayed and cannot be selected.
/// </summary>
internal class NavSidebar : Control, IThemeAware
{
    private sealed class NavEntry
    {
        public string Text = "";
        public Glyph Glyph;
        public bool Enabled = true;
        public bool IsHeading;
        public Rectangle Bounds;
    }

    private readonly List<NavEntry> _entries = new();
    private readonly List<int> _itemIndexes = new(); // item index -> entry index
    private int _selected = -1;
    private int _hover = -1;

    public NavSidebar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true;
        AccessibleRole = AccessibleRole.PageTabList;
        AccessibleName = "Navigation";
    }

    /// <summary>The selected item changed (by the user or by code).</summary>
    public event EventHandler? SelectedIndexChanged;

    public int ItemCount => _itemIndexes.Count;

    /// <summary>Adds an upper-case group heading.</summary>
    public void AddHeading(string text)
    {
        _entries.Add(new NavEntry { Text = text, IsHeading = true });
        Relayout();
    }

    /// <summary>Adds an item; returns its index.</summary>
    public int AddItem(string text, Glyph glyph)
    {
        _entries.Add(new NavEntry { Text = text, Glyph = glyph });
        _itemIndexes.Add(_entries.Count - 1);
        Relayout();
        return _itemIndexes.Count - 1;
    }

    public string GetItemText(int index) => _entries[_itemIndexes[index]].Text;

    public bool IsItemEnabled(int index) => _entries[_itemIndexes[index]].Enabled;

    public void SetItemEnabled(int index, bool enabled)
    {
        _entries[_itemIndexes[index]].Enabled = enabled;
        Invalidate();
    }

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            if (value < -1 || value >= _itemIndexes.Count || value == _selected) return;
            _selected = value;
            Invalidate();
            AccessibilityNotifyClients(AccessibleEvents.Selection, value);
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ApplyTheme(ThemePalette palette)
    {
        BackColor = palette.Secondary;
        Invalidate();
    }

    private int S(float v) => ModernLayout.Scale(this, v);

    private void Relayout()
    {
        int y = S(16);
        int x = S(10);
        int w = Width - 2 * x;
        bool first = true;
        foreach (var entry in _entries)
        {
            if (entry.IsHeading)
            {
                if (!first) y += S(10);
                entry.Bounds = new Rectangle(x + S(10), y, w - S(10), S(24));
                y += S(26);
            }
            else
            {
                entry.Bounds = new Rectangle(x, y, w, S(36));
                y += S(38);
            }
            first = false;
        }
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Relayout();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        Relayout();
    }

    private int HitTest(Point pt)
    {
        for (int i = 0; i < _itemIndexes.Count; i++)
            if (_entries[_itemIndexes[i]].Bounds.Contains(pt)) return i;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int hit = HitTest(e.Location);
        if (hit >= 0 && !IsItemEnabled(hit)) hit = -1;
        Cursor = hit >= 0 ? Cursors.Hand : Cursors.Default;
        if (hit != _hover) { _hover = hit; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hover != -1) { _hover = -1; Invalidate(); }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        int hit = HitTest(e.Location);
        if (hit >= 0 && IsItemEnabled(hit))
        {
            Focus();
            SelectedIndex = hit;
        }
    }

    protected override bool IsInputKey(Keys keyData)
        => keyData is Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        int next = e.KeyCode switch
        {
            Keys.Up => FindEnabled(_selected - 1, -1),
            Keys.Down => FindEnabled(_selected + 1, +1),
            Keys.Home => FindEnabled(0, +1),
            Keys.End => FindEnabled(_itemIndexes.Count - 1, -1),
            _ => -2
        };
        if (next >= 0)
        {
            SelectedIndex = next;
            e.Handled = true;
        }
    }

    /// <summary>First enabled item from <paramref name="start"/> in <paramref name="step"/> direction, or -1.</summary>
    public int FindEnabled(int start, int step)
    {
        for (int i = start; i >= 0 && i < _itemIndexes.Count; i += step)
            if (IsItemEnabled(i)) return i;
        return -1;
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = ThemeManager.Current;
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        for (int ei = 0; ei < _entries.Count; ei++)
        {
            var entry = _entries[ei];
            if (entry.IsHeading)
            {
                TextRenderer.DrawText(g, entry.Text.ToUpperInvariant(), ModernFonts.Caption, entry.Bounds, p.TextMuted,
                    TextFormatFlags.Left | TextFormatFlags.Bottom | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                continue;
            }
            int index = _itemIndexes.IndexOf(ei);
            bool selected = index == _selected;
            bool hover = index == _hover && entry.Enabled;
            var r = entry.Bounds;

            if (selected || hover)
            {
                using var path = ModernLayout.RoundedRect(new RectangleF(r.X, r.Y, r.Width, r.Height), S(4));
                using var brush = new SolidBrush(selected ? p.Selected : p.Hover);
                g.FillPath(brush, path);
            }
            if (selected && Focused && ShowFocusCues)
            {
                using var path = ModernLayout.RoundedRect(new RectangleF(r.X + 0.75f, r.Y + 0.75f, r.Width - 1.5f, r.Height - 1.5f), S(4));
                using var pen = new Pen(Color.FromArgb(180, p.Accent), 1.5f);
                g.DrawPath(pen, path);
            }

            Color fore = !entry.Enabled ? ModernLayout.Disabled(p.TextMuted, BackColor)
                : selected ? p.Text
                : hover ? p.Text
                : p.TextSecondary;
            Color glyph = !entry.Enabled ? fore : selected ? p.Accent : hover ? p.TextSecondary : p.TextMuted;
            int icon = S(18);
            ThemeGlyphs.Draw(g, entry.Glyph, new RectangleF(r.X + S(10), r.Y + (r.Height - icon) / 2f, icon, icon), glyph);
            var textRect = new Rectangle(r.X + S(38), r.Y, Math.Max(0, r.Width - S(42)), r.Height);
            TextRenderer.DrawText(g, entry.Text, selected ? ThemeFonts.Semibold : ModernFonts.RowTitle, textRect, fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }
    }
}
