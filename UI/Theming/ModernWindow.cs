using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>Round icon buttons shown on the right of a <see cref="ModernWindow"/> header.</summary>
[Flags]
internal enum ModernHeaderButtons
{
    None = 0,
    Help = 1,
    Minimize = 2,
    Close = 4
}

/// <summary>
/// Borderless window in the style of the Quick-Menü ("Wohin möchtest du scannen?"):
/// rounded corners with shadow (Windows 11 DWM; clip region + painted outline on older
/// versions), a header zone (logo tile, <see cref="Title"/>, muted <see cref="Subtitle"/>,
/// round help/minimize/close buttons, drag to move), a <see cref="Body"/> panel and an
/// optional footer bar (hint text left, buttons right, see <see cref="AddFooterButton"/>).
///
/// Usage: derive from it (or create it directly), set Title/Subtitle, put the content into
/// <see cref="Body"/>, add footer buttons, set AcceptButton/CancelButton as usual and call
/// <see cref="ThemeManager.Apply(Control)"/> at the end of the constructor. Sizes are given in
/// 96-dpi units (AutoScaleMode.Dpi). Either set <see cref="Form.ClientSize"/> (fixed size) or
/// <see cref="AutoSizeToContent"/> = true (size from the preferred size of the body content,
/// computed when the window loads).
///
/// Keyboard: Enter/Esc via AcceptButton/CancelButton; Esc closes the window when no
/// CancelButton is set (<see cref="CloseOnEscape"/>); Alt+F4 and the taskbar's close/minimize
/// work like for a normal window; the first control of the body (or the AcceptButton) gets the
/// focus when the window is shown.
/// </summary>
internal class ModernWindow : Form
{
    private const int CornerRadius = 8;
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int HTCAPTION = 2;
    private const int WS_SYSMENU = 0x00080000;
    private const int WS_MINIMIZEBOX = 0x00020000;
    private const int CS_DROPSHADOW = 0x00020000;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private readonly bool _useRegion = !ThemeNative.IsWindows11;
    private readonly ModernWindowHeader _header;
    private readonly Panel _body;
    private readonly ModernWindowFooter _footer;

    public ModernWindow()
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        Font = ThemeFonts.Regular;
        Icon = TrayIcons.GetAppIcon();
        KeyPreview = true;
        if (_useRegion)
            Padding = new Padding(1); // room for the painted outline

        _header = new ModernWindowHeader(this) { Dock = DockStyle.Top };
        _footer = new ModernWindowFooter { Dock = DockStyle.Bottom, Visible = false };
        _body = new Panel { Dock = DockStyle.Fill };
        ThemeManager.SetRole(_body, ThemeRole.Surface);

        // Fill first so the docked bars reserve their space.
        Controls.Add(_body);
        Controls.Add(_footer);
        Controls.Add(_header);
        ResumeLayout(false);
    }

    // ───────────────────────── Public API ─────────────────────────

    /// <summary>Header title (semibold). Also used as window caption when <see cref="Form.Text"/> is empty.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Title
    {
        get => _header.Title;
        set
        {
            _header.Title = value ?? "";
            if (string.IsNullOrEmpty(Text)) Text = value;
        }
    }

    /// <summary>Muted second line under the title (empty = title vertically centered).</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Subtitle
    {
        get => _header.Subtitle;
        set => _header.Subtitle = value ?? "";
    }

    /// <summary>Image in the rounded logo tile (default: app logo; null = no tile).</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Image? LogoImage
    {
        get => _header.Logo;
        set => _header.Logo = value;
    }

    /// <summary>
    /// Colored icon tile in the header instead of the app logo (the topic of a dialog, e.g. a
    /// red PDF document on the PDF options). Null = logo.
    /// </summary>
    public ColorIcon? HeaderIcon
    {
        get => _header.Icon;
        set => _header.Icon = value;
    }

    /// <summary>Which round buttons the header shows (default: Close).</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ModernHeaderButtons HeaderButtons
    {
        get => _header.Buttons;
        set => _header.Buttons = value;
    }

    /// <summary>Header height in 96-dpi units (default 60).</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int HeaderHeight
    {
        get => _header.LogicalHeight;
        set => _header.LogicalHeight = value;
    }

    /// <summary>
    /// Help topic opened by the header's help button (<see cref="HelpForm"/>) when no
    /// <see cref="HelpClicked"/> handler is attached.
    /// </summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? HelpTopic { get; set; }

    /// <summary>Help button clicked (replaces opening <see cref="HelpTopic"/>).</summary>
    public event EventHandler? HelpClicked;

    /// <summary>
    /// Header close button clicked. Default: <see cref="Form.Close"/> (a modal window returns
    /// DialogResult.Cancel). Set <see cref="CancelEventArgs.Cancel"/> to handle it yourself.
    /// </summary>
    public event CancelEventHandler? CloseButtonClicked;

    /// <summary>Content area between header and footer (theme role Surface).</summary>
    public Panel Body => _body;

    /// <summary>The header zone (custom painted). Add own controls only if really needed.</summary>
    public Control Header => _header;

    /// <summary>The "?" button in the title bar (for the guided tour).</summary>
    internal Control HeaderHelpButton => _header.HelpButton;

    /// <summary>The footer bar (shown automatically by <see cref="AddFooterButton"/> / <see cref="FooterHint"/>).</summary>
    public Control Footer => _footer;

    /// <summary>Whether the footer bar is shown.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowFooter
    {
        get => _footer.Shown;
        set { _footer.Shown = value; _footer.Visible = value; }
    }

    /// <summary>Muted hint text on the left of the footer (empty = none).</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string FooterHint
    {
        get => _footer.Hint;
        set
        {
            _footer.Hint = value ?? "";
            if (!string.IsNullOrEmpty(value)) ShowFooter = true;
        }
    }

    /// <summary>Esc closes the window when no CancelButton is set (default true).</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool CloseOnEscape { get; set; } = true;

    /// <summary>
    /// Size the window from the preferred size of the body content when it loads
    /// (<see cref="Form.ClientSize"/> is then ignored, <see cref="MinimumContentWidth"/> applies).
    /// </summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool AutoSizeToContent { get; set; }

    /// <summary>Minimum body width (96-dpi units) for <see cref="AutoSizeToContent"/>.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int MinimumContentWidth { get; set; } = 320;

    /// <summary>
    /// Adds a footer button. Buttons are arranged from the right: the last added button is the
    /// rightmost (add "Abbrechen" before "OK"). <paramref name="alignLeft"/> puts it on the left
    /// (instead of the hint). Size: 124 x 32 or wider for long texts.
    /// </summary>
    public Button AddFooterButton(string text, ThemeRole role = ThemeRole.Secondary, Glyph glyph = Glyph.None,
        DialogResult dialogResult = DialogResult.None, bool alignLeft = false)
    {
        var button = new Button
        {
            Text = text,
            Size = new Size(124, 32),
            DialogResult = dialogResult,
            UseMnemonic = text.Contains('&')
        };
        ThemeManager.SetRole(button, role);
        if (glyph != Glyph.None)
            ThemeManager.SetGlyph(button, glyph);
        // Measured in device pixels; before the window was scaled sizes are 96-dpi units.
        float f = DeviceDpi / 96f;
        int needed = (int)Math.Ceiling(TextRenderer.MeasureText(text.Replace("&", ""), ThemeFonts.Regular).Width / f)
                     + (glyph != Glyph.None ? 22 : 0) + 32;
        if (IsLayoutScaled) needed = (int)Math.Round(needed * f);
        if (needed > button.Width)
            button.Width = needed;
        AddFooterControl(button, alignLeft);
        return button;
    }

    /// <summary>Adds any control to the footer (vertically centered; see <see cref="AddFooterButton"/> for the order).</summary>
    public void AddFooterControl(Control control, bool alignLeft = false)
    {
        _footer.AddItem(control, alignLeft);
        ShowFooter = true;
    }

    /// <summary>Computes the window size from the body content now (see <see cref="AutoSizeToContent"/>).</summary>
    public void FitToContent()
    {
        Control? content = null;
        foreach (Control c in _body.Controls)
        {
            if (content == null || c.Dock == DockStyle.Fill) content = c;
        }
        if (content == null) return;

        float s = DeviceDpi / 96f;
        int minW = (int)Math.Round(MinimumContentWidth * s);
        var pref = content.GetPreferredSize(new Size(Math.Max(minW, content.Width), 0));
        int width = Math.Max(minW, pref.Width + _body.Padding.Horizontal);
        width = Math.Max(width, _footer.Shown ? _footer.GetMinimumWidth() : 0);
        int height = pref.Height + _body.Padding.Vertical;
        int frame = _useRegion ? 2 : 0;
        ClientSize = new Size(width + frame, _header.Height + height + (_footer.Shown ? _footer.Height : 0) + frame);
    }

    // ───────────────────────── Window behaviour ─────────────────────────

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= CS_DROPSHADOW;
            // System menu + minimize box: Alt+F4, Alt+Space and the taskbar button
            // (minimize/restore, close) keep working without a caption.
            cp.Style |= WS_SYSMENU | WS_MINIMIZEBOX;
            return cp;
        }
    }

    /// <summary>
    /// True after the window was loaded (DPI scaling done): sizes set from now on are device
    /// pixels, before they are 96-dpi units scaled automatically.
    /// </summary>
    internal bool IsLayoutScaled { get; private set; }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        IsLayoutScaled = true;
        if (AutoSizeToContent)
        {
            FitToContent();
            if (StartPosition == FormStartPosition.CenterParent && Owner != null)
                CenterToParent();
            else if (StartPosition is FormStartPosition.CenterScreen or FormStartPosition.CenterParent)
                CenterToScreen();
        }
        UpdateRegion();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        WindowActivation.BringToFront(this);
        if (AcceptButton is Control accept && accept.CanFocus && !ContainsFocusInBody())
            accept.Focus();
        else if (!ContainsFocusInBody())
            _body.SelectNextControl(null, true, true, true, false);
    }

    private bool ContainsFocusInBody() => _body.ContainsFocus;

    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (keyData == Keys.Escape && CancelButton == null && CloseOnEscape)
        {
            Close();
            return true;
        }
        return base.ProcessDialogKey(keyData);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateRegion();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!_useRegion) return;
        var p = ThemeManager.Current;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(p.IsDark ? p.Tertiary : p.Border, 1f);
        using var path = ThemeGlyphs.Rounded(new RectangleF(0, 0, ClientSize.Width - 1, ClientSize.Height - 1), Px(CornerRadius));
        e.Graphics.DrawPath(pen, path);
    }

    private void UpdateRegion()
    {
        if (!_useRegion || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        using var path = ThemeGlyphs.Rounded(new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), Px(CornerRadius));
        var old = Region;
        Region = new Region(path);
        old?.Dispose();
    }

    private float Px(float value) => value * DeviceDpi / 96f;

    /// <summary>Starts moving the window (called by the header on a left mouse down).</summary>
    internal void BeginDrag()
    {
        if (WindowState != FormWindowState.Normal) return;
        ReleaseCapture();
        SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
    }

    internal void OnHeaderHelp()
    {
        if (HelpClicked != null)
        {
            HelpClicked(this, EventArgs.Empty);
            return;
        }
        if (string.IsNullOrEmpty(HelpTopic)) return;
        using var help = new HelpForm(HelpTopic);
        help.ShowDialog(this);
    }

    internal void OnHeaderClose()
    {
        var args = new CancelEventArgs();
        CloseButtonClicked?.Invoke(this, args);
        if (!args.Cancel)
            Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Region?.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Header zone of a <see cref="ModernWindow"/> (painted; hosts the round buttons).</summary>
internal sealed class ModernWindowHeader : Panel, IThemeAware
{
    private static readonly Font TitleFont = new("Segoe UI Semibold", 12f);

    private readonly ModernWindow _window;
    private readonly HeaderIconButton _btnHelp;
    private readonly HeaderIconButton _btnMinimize;
    private readonly HeaderIconButton _btnClose;
    private string _title = "";
    private string _subtitle = "";
    private Image? _logo = AppResources.Logo;
    private ModernHeaderButtons _buttons = ModernHeaderButtons.Close;
    private int _logicalHeight = 60;

    internal Control HelpButton => _btnHelp;

    public ModernWindowHeader(ModernWindow window)
    {
        _window = window;
        DoubleBuffered = true;
        ResizeRedraw = true;
        Height = _logicalHeight;

        _btnHelp = new HeaderIconButton(HeaderIconButton.Kind.Help) { Size = new Size(30, 30), AccessibleName = "Hilfe" };
        _btnMinimize = new HeaderIconButton(HeaderIconButton.Kind.Minimize) { Size = new Size(30, 30), AccessibleName = "Minimieren" };
        _btnClose = new HeaderIconButton(HeaderIconButton.Kind.Close) { Size = new Size(30, 30), AccessibleName = "Schließen" };
        _btnHelp.Click += (_, _) => _window.OnHeaderHelp();
        _btnMinimize.Click += (_, _) => _window.WindowState = FormWindowState.Minimized;
        _btnClose.Click += (_, _) => _window.OnHeaderClose();
        Controls.AddRange(new Control[] { _btnHelp, _btnMinimize, _btnClose });
        UpdateButtons();
    }

    public string Title { get => _title; set { _title = value; AccessibleName = value; Invalidate(); } }
    public string Subtitle { get => _subtitle; set { _subtitle = value; Invalidate(); } }
    public Image? Logo { get => _logo; set { _logo = value; Invalidate(); } }

    private ColorIcon? _icon;

    /// <summary>Colored icon tile shown instead of the logo (topic of the dialog).</summary>
    public ColorIcon? Icon { get => _icon; set { _icon = value; Invalidate(); } }

    public ModernHeaderButtons Buttons
    {
        get => _buttons;
        set { _buttons = value; UpdateButtons(); }
    }

    public int LogicalHeight
    {
        get => _logicalHeight;
        set
        {
            _logicalHeight = Math.Max(36, value);
            Height = _window.IsLayoutScaled ? S(_logicalHeight) : _logicalHeight;
            PerformLayout();
            Invalidate();
        }
    }

    public void ApplyTheme(ThemePalette palette)
    {
        BackColor = palette.Header;
        Invalidate();
    }

    private int S(float v) => (int)Math.Round(v * DeviceDpi / 96f);

    private void UpdateButtons()
    {
        _btnHelp.Visible = (_buttons & ModernHeaderButtons.Help) != 0;
        _btnMinimize.Visible = (_buttons & ModernHeaderButtons.Minimize) != 0;
        _btnClose.Visible = (_buttons & ModernHeaderButtons.Close) != 0;
        PerformLayout();
        Invalidate();
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        int size = S(30);
        int x = Width - S(12);
        int y = (Height - size) / 2;
        foreach (var b in new[] { _btnClose, _btnMinimize, _btnHelp })
        {
            if ((b == _btnClose && (_buttons & ModernHeaderButtons.Close) == 0)
                || (b == _btnMinimize && (_buttons & ModernHeaderButtons.Minimize) == 0)
                || (b == _btnHelp && (_buttons & ModernHeaderButtons.Help) == 0))
                continue;
            x -= size;
            b.Bounds = new Rectangle(x, y, size, size);
            x -= S(4);
        }
    }

    private int ButtonsLeft()
    {
        int left = Width;
        foreach (Control c in Controls)
            if (c is HeaderIconButton && ((c == _btnClose && (_buttons & ModernHeaderButtons.Close) != 0)
                || (c == _btnMinimize && (_buttons & ModernHeaderButtons.Minimize) != 0)
                || (c == _btnHelp && (_buttons & ModernHeaderButtons.Help) != 0)))
                left = Math.Min(left, c.Left);
        return left;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
            _window.BeginDrag();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = ThemeManager.Current;
        var g = e.Graphics;
        g.Clear(p.Header);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        int x = S(16);
        if (_icon != null)
        {
            float tileSize = S(36);
            var tile = new RectangleF(x, (Height - tileSize) / 2f, tileSize, tileSize);
            _icon.Draw(g, tile);
            x = (int)tile.Right + S(12);
        }
        else if (_logo != null)
        {
            float tileSize = S(36);
            var tile = new RectangleF(x, (Height - tileSize) / 2f, tileSize, tileSize);
            using (var path = ThemeGlyphs.Rounded(tile, S(8)))
            using (var brush = new SolidBrush(p.IsDark ? p.Background : p.Secondary))
                g.FillPath(brush, path);
            if (!p.IsDark)
            {
                using var path = ThemeGlyphs.Rounded(RectangleF.Inflate(tile, -0.5f, -0.5f), S(8));
                using var pen = new Pen(ThemePalette.Blend(p.Border, p.Secondary, 0.35f));
                g.DrawPath(pen, path);
            }
            float logo = S(24);
            g.DrawImage(_logo, new RectangleF(tile.X + (tileSize - logo) / 2f, tile.Y + (tileSize - logo) / 2f, logo, logo));
            x = (int)tile.Right + S(10);
        }

        int right = ButtonsLeft() - S(8);
        var flags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
        int titleH = TextRenderer.MeasureText(g, "Ag", TitleFont, Size.Empty, flags).Height;
        if (string.IsNullOrEmpty(_subtitle))
        {
            TextRenderer.DrawText(g, _title, TitleFont, new Rectangle(x, (Height - titleH) / 2, Math.Max(0, right - x), titleH), p.Text, flags);
        }
        else
        {
            int subH = TextRenderer.MeasureText(g, "Ag", ThemeFonts.Regular, Size.Empty, flags).Height;
            int gap = S(2);
            int top = (Height - titleH - gap - subH) / 2;
            TextRenderer.DrawText(g, _title, TitleFont, new Rectangle(x, top, Math.Max(0, right - x), titleH), p.Text, flags);
            TextRenderer.DrawText(g, _subtitle, ThemeFonts.Regular, new Rectangle(x + 1, top + titleH + gap, Math.Max(0, right - x), subH), p.TextMuted, flags);
        }

        if (!p.IsDark)
        {
            using var pen = new Pen(p.Border);
            g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }
    }
}

/// <summary>Footer bar of a <see cref="ModernWindow"/>: hint text left, buttons right.</summary>
internal sealed class ModernWindowFooter : Panel, IThemeAware
{
    private readonly List<Control> _right = new();
    private readonly List<Control> _left = new();
    private string _hint = "";

    public ModernWindowFooter()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        Height = 56;
    }

    /// <summary>Intended visibility (Visible is false while the window is hidden).</summary>
    public bool Shown { get; set; }

    public string Hint { get => _hint; set { _hint = value; Invalidate(); } }

    public void ApplyTheme(ThemePalette palette)
    {
        BackColor = palette.Secondary;
        Invalidate();
    }

    private int S(float v) => (int)Math.Round(v * DeviceDpi / 96f);

    public void AddItem(Control control, bool left)
    {
        (left ? _left : _right).Add(control);
        Controls.Add(control);
        PerformLayout();
    }

    /// <summary>Width needed for all items (window auto size).</summary>
    public int GetMinimumWidth()
    {
        int w = S(18) + S(16);
        foreach (var c in _right.Concat(_left))
            w += c.Width + S(8);
        if (_hint.Length > 0)
            w += Math.Min(S(260), TextRenderer.MeasureText(_hint, ThemeFonts.Regular).Width + S(16));
        return w;
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        int x = Width - S(16);
        for (int i = _right.Count - 1; i >= 0; i--)
        {
            var c = _right[i];
            if (!c.Visible && IsHandleCreated && Visible) continue;
            x -= c.Width;
            c.Location = new Point(x, (Height - c.Height) / 2);
            x -= S(8);
        }
        int lx = S(16);
        foreach (var c in _left)
        {
            if (!c.Visible && IsHandleCreated && Visible) continue;
            c.Location = new Point(lx, (Height - c.Height) / 2);
            lx += c.Width + S(8);
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = ThemeManager.Current;
        var g = e.Graphics;
        g.Clear(p.Secondary);
        if (!p.IsDark)
        {
            using var pen = new Pen(p.Border);
            g.DrawLine(pen, 0, 0, Width, 0);
        }
        if (_hint.Length == 0) return;

        int left = S(18);
        foreach (var c in _left)
            if (c.Visible) left = Math.Max(left, c.Right + S(12));
        int right = Width - S(16);
        foreach (var c in _right)
            if (c.Visible) right = Math.Min(right, c.Left - S(12));
        var rect = new Rectangle(left, 0, Math.Max(0, right - left), Height);
        TextRenderer.DrawText(g, _hint, ThemeFonts.Regular, rect, p.TextMuted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
            | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
}

/// <summary>Round icon button (help "?", minimize "–", close "x" turning red on hover).</summary>
internal sealed class HeaderIconButton : Control, IThemeAware
{
    public enum Kind { Help, Minimize, Close }

    private readonly Kind _kind;
    private bool _hover;
    private bool _pressed;

    public HeaderIconButton(Kind kind)
    {
        _kind = kind;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
        AccessibleRole = AccessibleRole.PushButton;
    }

    public void ApplyTheme(ThemePalette palette) => Invalidate();

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = ThemeManager.Current;
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? p.Header);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        float s = DeviceDpi / 96f;

        Color fore = p.TextMuted;
        if (_hover || _pressed)
        {
            Color bg = _kind == Kind.Close
                ? (_pressed ? p.DangerHover : p.Danger)
                : (_pressed ? p.Selected : p.IsDark ? p.ButtonSecondary : p.Hover);
            using var brush = new SolidBrush(bg);
            g.FillEllipse(brush, 0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            fore = _kind == Kind.Close ? p.TextOnAccent : p.Text;
        }

        float size = 16 * s;
        var r = new RectangleF((Width - size) / 2f, (Height - size) / 2f, size, size);
        switch (_kind)
        {
            case Kind.Close:
            {
                using var pen = new Pen(fore, 1.8f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(pen, r.X + 4f * s, r.Y + 4f * s, r.Right - 4f * s, r.Bottom - 4f * s);
                g.DrawLine(pen, r.Right - 4f * s, r.Y + 4f * s, r.X + 4f * s, r.Bottom - 4f * s);
                break;
            }
            case Kind.Minimize:
            {
                using var pen = new Pen(fore, 1.8f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                float y = r.Y + r.Height / 2f;
                g.DrawLine(pen, r.X + 3.5f * s, y, r.Right - 3.5f * s, y);
                break;
            }
            default:
            {
                // Bare vector "?" with the same stroke weight and optical size as the
                // close/minimize glyphs (the round hover background is the button shape).
                using var pen = new Pen(fore, 1.8f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                float cx = r.X + r.Width / 2f;
                using (var path = new GraphicsPath())
                {
                    path.AddArc(cx - 3f * s, r.Y + 2.5f * s, 6f * s, 6f * s, 180, 225);
                    var end = path.GetLastPoint();
                    path.AddBezier(end, new PointF(end.X - 1.2f * s, end.Y + 0.9f * s),
                        new PointF(cx, r.Y + 9f * s), new PointF(cx, r.Y + 10.25f * s));
                    g.DrawPath(pen, path);
                }
                float dot = 2.3f * s;
                using (var brush = new SolidBrush(fore))
                    g.FillEllipse(brush, cx - dot / 2f, r.Y + 12.6f * s - dot / 2f, dot, dot);
                break;
            }
        }
    }
}
