using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Shown when "Beenden" waits for a scan that is still being written: app-styled window with a
/// spinner and no buttons. It stays open until the app exits (it closes with the message loop).
/// </summary>
internal sealed class ExitWaitDialog : ModernWindow
{
    public ExitWaitDialog()
    {
        SuspendLayout();
        Text = TrayMessageBox.Caption;
        Title = TrayMessageBox.Caption;
        HeaderHeight = 52;
        HeaderButtons = ModernHeaderButtons.None;
        CloseOnEscape = false;
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
            Padding = new Padding(20, 20, 24, 20),
            Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.Controls.Add(new Spinner { Size = new Size(36, 36), Margin = new Padding(0, 0, 14, 0) }, 0, 0);

        var heading = new Label
        {
            Text = L.T("Der Scan wird noch gespeichert …"),
            AutoSize = true,
            MaximumSize = new Size(400, 0),
            Font = ThemeFonts.Heading,
            Margin = new Padding(0, 0, 0, 6),
            UseMnemonic = false
        };
        var body = new Label
        {
            Text = L.T("SpeedScan Manager wird danach beendet."),
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
            Margin = new Padding(0, 0, 0, 0),
            Padding = Padding.Empty
        };
        stack.Controls.Add(heading);
        stack.Controls.Add(body);
        layout.Controls.Add(stack, 1, 0);
        Body.Controls.Add(layout);

        ThemeManager.Apply(this);
        ResumeLayout(true);
    }

    // The user can't close it: the app is about to exit anyway (Alt+F4 included)
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing) e.Cancel = true;
        base.OnFormClosing(e);
    }

    /// <summary>Rotating accent arc on a faint ring.</summary>
    private sealed class Spinner : Control, IThemeAware
    {
        private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
        private float _angle;

        public Spinner()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            AccessibleRole = AccessibleRole.Animation;
            AccessibleName = L.T("Wird gespeichert");
            _timer.Tick += (_, _) => { _angle = (_angle + 6f) % 360f; Invalidate(); };
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); _timer.Start(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer.Dispose();
            base.Dispose(disposing);
        }

        public void ApplyTheme(ThemePalette palette) => Invalidate();

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = ThemeManager.Current;
            var g = e.Graphics;
            g.Clear(ModernLayout.ParentBack(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float stroke = Math.Max(3f, Width / 9f);
            var r = new RectangleF(stroke / 2f + 1, stroke / 2f + 1, Width - stroke - 2, Height - stroke - 2);
            using (var ring = new Pen(Color.FromArgb(p.IsDark ? 60 : 40, p.Accent), stroke))
                g.DrawEllipse(ring, r);
            using var arc = new Pen(p.Accent, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawArc(arc, r, _angle - 90f, 100f);
        }
    }
}
