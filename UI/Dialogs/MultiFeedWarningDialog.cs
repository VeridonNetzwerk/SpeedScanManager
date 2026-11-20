using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Dialog shown when multi-feed (double feed) is detected during scanning.
/// Shows preview images of the current page and the previous page (if available),
/// and offers three options: rescan, keep as-is, or disable multi-feed detection.
/// Modern window (<see cref="ModernWindow"/>): warning tile in the header, two preview cards,
/// a hint and the three actions in the footer ("Wiederholen" is the default button).
/// </summary>
internal class MultiFeedWarningDialog : ModernWindow
{
    /// <summary>
    /// The user's choice from the dialog.
    /// </summary>
    public enum MultiFeedAction
    {
        Rescan,
        KeepAsIs,
        DisableDetection
    }

    public MultiFeedAction Action { get; private set; } = MultiFeedAction.Rescan;

    private readonly Bitmap _warningIcon;

    public MultiFeedWarningDialog(Bitmap? currentPage, Bitmap? previousPage)
    {
        SuspendLayout();
        Text = "SpeedScan Manager – Mehrfacheinzug erkannt";
        Title = "Mehrfacheinzug erkannt!";
        Subtitle = "Es wurden möglicherweise zwei Seiten gleichzeitig eingezogen.";
        _warningIcon = CreateWarningIcon();
        LogoImage = _warningIcon;
        HeaderButtons = ModernHeaderButtons.Help | ModernHeaderButtons.Close;
        HelpTopic = "multifeed-dialog";
        StartPosition = FormStartPosition.CenterScreen;
        // Owned by the invisible tray window: keep it on top so it isn't hidden behind
        // other applications while the scanner waits for the decision.
        TopMost = true;
        ShowInTaskbar = true;
        CloseOnEscape = false; // as before: Esc does not dismiss the decision
        ClientSize = new Size(720, 520);

        // === Previews: previous page | current page (multi-feed) ===
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(10, 12, 10, 4)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        grid.Controls.Add(CreatePreviewCard("Vorherige Seite", previousPage, highlight: false), 0, 0);
        grid.Controls.Add(CreatePreviewCard("Aktuelle Seite (Mehrfacheinzug)", currentPage, highlight: true), 1, 0);

        var hint = new HintBar("Tipp: \"Erkennung ausschalten\" deaktiviert die Mehrfacheinzugserkennung "
                               + "für den restlichen Scan und übernimmt die aktuelle Seite.")
        {
            Dock = DockStyle.Bottom,
            Height = 64
        };

        Body.Controls.Add(grid);
        Body.Controls.Add(hint);

        // === Footer: Erkennung ausschalten (left) | So übernehmen, Wiederholen (right) ===
        var btnDisable = AddFooterButton("Erkennung ausschalten", ThemeRole.Secondary, alignLeft: true);
        btnDisable.Click += (_, _) =>
        {
            Action = MultiFeedAction.DisableDetection;
            DialogResult = DialogResult.OK;
            Close();
        };

        var btnKeep = AddFooterButton("So übernehmen", ThemeRole.Secondary, Glyph.Check);
        btnKeep.Click += (_, _) =>
        {
            Action = MultiFeedAction.KeepAsIs;
            DialogResult = DialogResult.OK;
            Close();
        };

        var btnRescan = AddFooterButton("Wiederholen", ThemeRole.Primary, Glyph.Scanner);
        btnRescan.Click += (_, _) =>
        {
            Action = MultiFeedAction.Rescan;
            DialogResult = DialogResult.OK;
            Close();
        };

        AcceptButton = btnRescan;
        ThemeManager.Apply(this);
        ResumeLayout(true);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _warningIcon.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>Warning triangle for the header tile.</summary>
    private static Bitmap CreateWarningIcon()
    {
        const int size = 48;
        var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);
        var danger = Color.FromArgb(0xDA, 0x37, 0x3C);
        using (var path = new GraphicsPath())
        {
            path.AddLines(new[] { new PointF(24f, 5f), new PointF(45f, 42f), new PointF(3f, 42f) });
            path.CloseFigure();
            using var pen = new Pen(danger, 5f) { LineJoin = LineJoin.Round };
            using var brush = new SolidBrush(danger);
            g.FillPath(brush, path);
            g.DrawPath(pen, path);
        }
        using var white = new Pen(Color.White, 4.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(white, 24f, 17f, 24f, 29f);
        using var dot = new SolidBrush(Color.White);
        g.FillEllipse(dot, 21.4f, 33.2f, 5.2f, 5.2f);
        return bmp;
    }

    private static Control CreatePreviewCard(string title, Bitmap? image, bool highlight)
    {
        var card = new PreviewCard(title, highlight) { Dock = DockStyle.Fill, Margin = new Padding(6) };
        card.Preview.Image = image;
        return card;
    }

    /// <summary>Card with a title and the page preview (current page: danger badge).</summary>
    private sealed class PreviewCard : Panel, IThemeAware
    {
        private readonly string _title;
        private readonly bool _highlight;

        public PostScanSaveDialog.PagePreview Preview { get; }

        public PreviewCard(string title, bool highlight)
        {
            _title = title;
            _highlight = highlight;
            AccessibleName = title;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Padding = new Padding(12, 42, 12, 12);
            Preview = new PostScanSaveDialog.PagePreview { Dock = DockStyle.Fill, AccessibleName = title };
            Controls.Add(Preview);
        }

        public void ApplyTheme(ThemePalette palette)
        {
            BackColor = palette.Card;
            Invalidate();
        }

        private int S(float v) => ModernLayout.Scale(this, v);

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
                using var pen = new Pen(_highlight ? ThemePalette.Blend(p.CardBorder, p.Danger, 0.55f) : p.CardBorder, _highlight ? 1.5f : 1f);
                g.DrawPath(pen, path);
            }

            int x = S(14);
            if (_highlight)
            {
                float d = S(8);
                using var dot = new SolidBrush(p.Danger);
                g.FillEllipse(dot, x, S(21) - d / 2f, d, d);
                x += S(14);
            }
            TextRenderer.DrawText(g, _title, ModernFonts.CardTitle, new Rectangle(x, S(8), Math.Max(0, Width - x - S(12)), S(26)),
                p.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
                        | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    /// <summary>Muted hint in a rounded info bar (accent stripe).</summary>
    private sealed class HintBar : Control, IThemeAware
    {
        public HintBar(string text)
        {
            Text = text;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            AccessibleRole = AccessibleRole.StaticText;
            AccessibleName = text;
        }

        public void ApplyTheme(ThemePalette palette) => Invalidate();

        private int S(float v) => ModernLayout.Scale(this, v);

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = ThemeManager.Current;
            var g = e.Graphics;
            var back = ModernLayout.ParentBack(this);
            g.Clear(back);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var rect = new RectangleF(S(16) + 0.5f, S(4) + 0.5f, Width - S(32) - 1f, Height - S(20) - 1f);
            Color fill = ThemePalette.Blend(p.Card, p.Accent, p.IsDark ? 0.08f : 0.05f);
            using (var path = ModernLayout.RoundedRect(rect, S(8)))
            {
                using (var brush = new SolidBrush(fill))
                    g.FillPath(brush, path);
                using var pen = new Pen(ThemePalette.Blend(p.CardBorder, p.Accent, 0.25f));
                g.DrawPath(pen, path);
            }
            using (var stripe = new SolidBrush(p.Accent))
            using (var sp = ModernLayout.RoundedRect(new RectangleF(rect.X + S(8), rect.Y + S(10), S(3), rect.Height - S(20)), S(1.5f)))
                g.FillPath(stripe, sp);

            var textRect = Rectangle.Round(new RectangleF(rect.X + S(22), rect.Y, rect.Width - S(34), rect.Height));
            TextRenderer.DrawText(g, Text, ThemeFonts.Regular, textRect, p.TextSecondary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }
    }
}
