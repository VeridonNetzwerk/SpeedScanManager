using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Brings windows of the tray app to the front. Post-scan dialogs are often opened while
/// another application has the focus (scan started with the scanner button), and Windows'
/// foreground lock would otherwise leave them behind other windows.
/// </summary>
internal static class WindowActivation
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);

    private const int ASFW_ANY = -1;

    /// <summary>Activates <paramref name="form"/> and tries to make it the foreground window.</summary>
    public static void BringToFront(Form form)
    {
        try
        {
            if (form.WindowState == FormWindowState.Minimized)
                form.WindowState = FormWindowState.Normal;
            form.BringToFront();
            form.Activate();
            SetForegroundWindow(form.Handle);
        }
        catch
        {
            // Activation is best effort.
        }
    }

    /// <summary>
    /// Lets any process (the running instance) take the foreground. Called by a secondary
    /// instance before it forwards a command, while it may still hold foreground rights.
    /// </summary>
    public static void AllowAnyProcessToActivate()
    {
        try { AllowSetForegroundWindow(ASFW_ANY); } catch { }
    }
}


/// <summary>
/// Message boxes for the tray app. The owner is usually the invisible tray window, so the
/// box must be top-most and take the foreground to never be hidden behind other windows.
///
/// On a thread with a WinForms message loop (the UI thread) the box is a WinForms modal
/// form: its message loop runs the application's message filters, through which the TWAIN
/// session receives its events while a scan is running. A native MessageBox has its own
/// loop without those filters and would stall the scan while it is open. Other threads
/// (e.g. crash reports) use the native MessageBox with MB_TOPMOST | MB_SETFOREGROUND.
/// The WinForms box follows the app theme (light/dark).
/// </summary>
internal static class TrayMessageBox
{
    public const string Caption = "SpeedScan Manager";

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    private const uint MB_TOPMOST = 0x00040000;
    private const uint MB_SETFOREGROUND = 0x00010000;
    private const uint MB_DEFBUTTON2 = 0x00000100;

    public static DialogResult Show(IWin32Window? owner, string text,
        MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxIcon icon = MessageBoxIcon.Information,
        bool defaultSecondButton = false)
        => Show(owner, text, Caption, buttons, icon, defaultSecondButton);

    /// <summary>Same as <see cref="Show(IWin32Window?, string, MessageBoxButtons, MessageBoxIcon, bool)"/> with an own window caption.</summary>
    public static DialogResult Show(IWin32Window? owner, string text, string caption,
        MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxIcon icon = MessageBoxIcon.Information,
        bool defaultSecondButton = false)
    {
        if (string.IsNullOrEmpty(caption)) caption = Caption;
        if (Application.MessageLoop && buttons is MessageBoxButtons.OK or MessageBoxButtons.YesNo or MessageBoxButtons.YesNoCancel)
        {
            try
            {
                using var form = new TrayPromptForm(text, caption, buttons, icon, defaultSecondButton);
                return owner != null ? form.ShowDialog(owner) : form.ShowDialog();
            }
            catch (Exception ex)
            {
                DiagLog.Write($"TrayMessageBox: WinForms prompt failed, using native box: {ex.Message}");
            }
        }

        uint type = (uint)buttons | (uint)icon | MB_TOPMOST | MB_SETFOREGROUND;
        if (defaultSecondButton) type |= MB_DEFBUTTON2;
        IntPtr hwnd = IntPtr.Zero;
        try { hwnd = owner?.Handle ?? IntPtr.Zero; } catch { }
        // IDOK=1, IDCANCEL=2, IDABORT=3, IDRETRY=4, IDIGNORE=5, IDYES=6, IDNO=7 = DialogResult values
        return (DialogResult)MessageBoxW(hwnd, text, caption, type);
    }

    public static void Error(IWin32Window? owner, string text) => Show(owner, text, MessageBoxButtons.OK, MessageBoxIcon.Error);
    public static void Warning(IWin32Window? owner, string text) => Show(owner, text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    public static void Info(IWin32Window? owner, string text) => Show(owner, text, MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static bool AskYesNo(IWin32Window? owner, string text, bool defaultNo = false)
        => Show(owner, text, MessageBoxButtons.YesNo, MessageBoxIcon.Question, defaultNo) == DialogResult.Yes;
}

/// <summary>
/// WinForms replacement for the native message box (see <see cref="TrayMessageBox"/>): a
/// <see cref="ModernWindow"/> (header with logo and caption, close button) with a colored
/// symbol badge, the text and a footer bar with OK, Ja/Nein or Ja/Nein/Abbrechen; top-most,
/// centered on the screen, brought to the foreground when shown, sized to the text.
/// </summary>
internal sealed class TrayPromptForm : ModernWindow
{
    public TrayPromptForm(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, bool defaultSecondButton)
    {
        SuspendLayout();
        Text = caption;
        Title = caption;
        HeaderHeight = 52;
        HeaderButtons = ModernHeaderButtons.Close;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        AutoSizeToContent = true;
        MinimumContentWidth = 340;

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(20, 20, 24, 8),
            Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var kind = icon switch
        {
            MessageBoxIcon.Error => PromptBadge.Kind.Error,
            MessageBoxIcon.Warning => PromptBadge.Kind.Warning,
            MessageBoxIcon.Question => PromptBadge.Kind.Question,
            MessageBoxIcon.Information => PromptBadge.Kind.Info,
            _ => PromptBadge.Kind.None
        };
        if (kind != PromptBadge.Kind.None)
        {
            layout.Controls.Add(new PromptBadge(kind)
            {
                Size = new Size(36, 36),
                Margin = new Padding(0, 0, 14, 12)
            }, 0, 0);
        }

        // A short first paragraph ("Scan verwerfen?\n\nJa: ...") becomes a semibold heading.
        string normalized = text.Replace("\r\n", "\n");
        int split = normalized.IndexOf("\n\n", StringComparison.Ordinal);
        Control content;
        if (split > 0 && split <= 80 && normalized.IndexOf('\n') == split)
        {
            var heading = new Label
            {
                Text = normalized[..split],
                AutoSize = true,
                MaximumSize = new Size(400, 0),
                Font = ThemeFonts.Heading,
                Margin = new Padding(0, 0, 0, 6),
                UseMnemonic = false
            };
            var body = new Label
            {
                Text = normalized[(split + 2)..],
                AutoSize = true,
                MaximumSize = new Size(400, 0),
                Margin = Padding.Empty,
                UseMnemonic = false
            };
            ThemeManager.SetRole(body, ThemeRole.Muted);
            var stack = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 2, 0, 16),
                Padding = Padding.Empty
            };
            stack.Controls.Add(heading);
            stack.Controls.Add(body);
            content = stack;
        }
        else
        {
            content = new Label
            {
                Text = text,
                AutoSize = true,
                MaximumSize = new Size(400, 0),
                MinimumSize = new Size(0, 36),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 0, 16),
                UseMnemonic = false
            };
        }
        layout.Controls.Add(content, kind != PromptBadge.Kind.None ? 1 : 0, 0);
        Body.Controls.Add(layout);

        // Footer buttons (left to right; the last one is the rightmost)
        Button first, second;
        if (buttons == MessageBoxButtons.YesNoCancel)
        {
            first = AddFooterButton("&Ja", ThemeRole.Secondary, Glyph.None, DialogResult.Yes);
            second = AddFooterButton("&Nein", ThemeRole.Secondary, Glyph.None, DialogResult.No);
            var third = AddFooterButton("Abbrechen", ThemeRole.Secondary, Glyph.None, DialogResult.Cancel);
            CancelButton = third;
        }
        else if (buttons == MessageBoxButtons.YesNo)
        {
            first = AddFooterButton("&Ja", ThemeRole.Secondary, Glyph.None, DialogResult.Yes);
            second = AddFooterButton("&Nein", ThemeRole.Secondary, Glyph.None, DialogResult.No);
            CancelButton = second;
        }
        else
        {
            first = AddFooterButton("OK", ThemeRole.Secondary, Glyph.None, DialogResult.OK);
            second = first;
            CancelButton = first;
        }
        foreach (Control c in Footer.Controls)
            if (c is Button b) b.Size = new Size(Math.Max(90, b.Width - 34), 32);
        var defaultButton = defaultSecondButton ? second : first;
        AcceptButton = defaultButton;
        ThemeManager.SetRole(first, ReferenceEquals(first, defaultButton) ? ThemeRole.Primary : ThemeRole.Secondary);
        if (!ReferenceEquals(first, second))
            ThemeManager.SetRole(second, ReferenceEquals(second, defaultButton) ? ThemeRole.Primary : ThemeRole.Secondary);

        ThemeManager.Apply(this);
        ResumeLayout(true);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        (AcceptButton as Control)?.Focus();
    }

    /// <summary>Round colored badge with the message symbol (i, !, ?, x).</summary>
    private sealed class PromptBadge : Control, IThemeAware
    {
        public enum Kind { None, Info, Warning, Error, Question }

        private readonly Kind _kind;

        public PromptBadge(Kind kind)
        {
            _kind = kind;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            AccessibleRole = AccessibleRole.Graphic;
            AccessibleName = kind switch
            {
                Kind.Error => "Fehler",
                Kind.Warning => "Warnung",
                Kind.Question => "Frage",
                _ => "Information"
            };
        }

        public void ApplyTheme(ThemePalette palette) => Invalidate();

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = ThemeManager.Current;
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? p.Background);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

            var color = _kind switch
            {
                Kind.Error => p.Danger,
                Kind.Warning => p.Warning,
                _ => p.Accent
            };
            float d = Math.Min(Width, Height) - 1f;
            var circle = new RectangleF((Width - d) / 2f, (Height - d) / 2f, d, d);
            // Soft halo + solid disc
            using (var halo = new SolidBrush(Color.FromArgb(p.IsDark ? 60 : 40, color)))
                g.FillEllipse(halo, circle);
            var inner = RectangleF.Inflate(circle, -d * 0.14f, -d * 0.14f);
            using (var brush = new SolidBrush(color))
                g.FillEllipse(brush, inner);

            float s = inner.Width / 16f;
            float cx = inner.X + inner.Width / 2f, cy = inner.Y + inner.Height / 2f;
            var fore = _kind == Kind.Warning && !p.IsDark ? Color.White : p.TextOnAccent;
            using var pen = new Pen(fore, 2f * s) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round, LineJoin = System.Drawing.Drawing2D.LineJoin.Round };
            using var dot = new SolidBrush(fore);
            switch (_kind)
            {
                case Kind.Info:
                    g.FillEllipse(dot, cx - 1.25f * s, cy - 5.5f * s, 2.5f * s, 2.5f * s);
                    g.DrawLine(pen, cx, cy - 0.5f * s, cx, cy + 4.5f * s);
                    break;
                case Kind.Warning:
                    g.DrawLine(pen, cx, cy - 5f * s, cx, cy + 1f * s);
                    g.FillEllipse(dot, cx - 1.25f * s, cy + 3.25f * s, 2.5f * s, 2.5f * s);
                    break;
                case Kind.Error:
                    g.DrawLine(pen, cx - 3.5f * s, cy - 3.5f * s, cx + 3.5f * s, cy + 3.5f * s);
                    g.DrawLine(pen, cx + 3.5f * s, cy - 3.5f * s, cx - 3.5f * s, cy + 3.5f * s);
                    break;
                case Kind.Question:
                    g.DrawArc(pen, cx - 3f * s, cy - 5.5f * s, 6f * s, 6f * s, 180, 270);
                    g.DrawLine(pen, cx, cy + 0.5f * s, cx, cy + 1.5f * s);
                    g.FillEllipse(dot, cx - 1.25f * s, cy + 3.5f * s, 2.5f * s, 2.5f * s);
                    break;
            }
        }
    }
}
