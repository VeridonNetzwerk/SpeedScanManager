using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Post-scan "In Ordner speichern" dialog shown when destination is Scan to Folder.
/// Lets the user preview scanned images, choose a title and destination folder before saving.
/// Modern window (<see cref="ModernWindow"/>): page preview on a rounded canvas on the left,
/// cards for title, folder and the scan settings summary on the right, footer bar with the
/// page navigation (left) and Abbrechen / Speichern (right).
/// </summary>
internal class PostScanSaveDialog : ModernWindow
{
    private readonly IReadOnlyList<Bitmap> _scanImages;
    private readonly string _defaultTitle;
    private readonly ScanSettings _settings;
    private int _currentPageIndex;

    private static readonly Font UiFont = ThemeFonts.Regular;

    private readonly PagePreview _imgPreview;
    private readonly Label _lblPageCounter;
    private readonly TextBox _txtTitle;
    private readonly TextBox _txtFolderPath;
    private readonly Button _btnBrowse;
    private readonly Button _btnSave;
    private readonly Button _btnCancel;
    private readonly Button _btnPrev;
    private readonly Button _btnNext;

    /// <summary>
    /// The typed title as a single valid file name (invalid characters such as "/" are
    /// replaced, so "03/2026" never creates subfolders). Empty input = the generated name.
    /// </summary>
    public string SelectedTitle => ScanOutputProcessor.SanitizeFileName(
        string.IsNullOrWhiteSpace(_txtTitle.Text) ? _defaultTitle : _txtTitle.Text.Trim());
    public string SelectedFolderPath => _txtFolderPath.Text;

    /// <summary>
    /// Ask "Scan verwerfen?" when the dialog is cancelled or closed. "Nein" keeps the dialog open.
    /// </summary>
    public bool ConfirmDiscardOnCancel { get; set; } = true;

    /// <summary>
    /// True only when the user explicitly answered "Ja" to "Scan verwerfen?". Every other
    /// way the dialog closes without OK (Windows logoff/shutdown, application exit, no
    /// confirmation) must keep the already saved files.
    /// </summary>
    public bool DiscardConfirmed { get; private set; }

    public PostScanSaveDialog(IReadOnlyList<Bitmap> scanImages, ScanSettings settings, string defaultTitle, string defaultFolder)
    {
        _scanImages = scanImages;
        _defaultTitle = string.IsNullOrWhiteSpace(defaultTitle) ? "Scan" : defaultTitle;
        _settings = settings;
        _currentPageIndex = 0;

        SuspendLayout();
        Text = L.T("In Ordner speichern");
        Title = L.T("In Ordner speichern");
        Subtitle = _scanImages.Count == 1 ? L.T("1 Seite gescannt") : L.F("{0} Seiten gescannt", _scanImages.Count);
        HeaderButtons = ModernHeaderButtons.Help | ModernHeaderButtons.Close;
        HelpTopic = "postscan-save";
        // Owner is the invisible tray window: center on screen and stay on top, otherwise the
        // dialog may open behind other windows (scan started with the hardware button).
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        ShowInTaskbar = true;
        ClientSize = new Size(760, 560);

        // === Left: page preview on a canvas ===
        var previewHost = new Panel
        {
            Dock = DockStyle.Left,
            Width = 300,
            Padding = new Padding(16, 16, 0, 16)
        };
        _imgPreview = new PagePreview { Dock = DockStyle.Fill, AccessibleName = L.T("Vorschau") };
        previewHost.Controls.Add(_imgPreview);

        // === Right: title, folder, scan settings ===
        _txtTitle = new TextBox
        {
            Font = UiFont,
            AutoSize = false,
            Size = new Size(300, 28),
            Text = _defaultTitle,
            AccessibleName = L.T("Titel festlegen")
        };

        _txtFolderPath = new TextBox
        {
            ReadOnly = true,
            Font = UiFont,
            AutoSize = false,
            Size = new Size(300, 28),
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(0, 2, 8, 2),
            Text = defaultFolder,
            AccessibleName = L.T("Zielordner festlegen")
        };

        _btnBrowse = new Button
        {
            Text = L.T("Durchsuchen..."),
            Size = new Size(140, 32),
            Font = UiFont,
            Anchor = AnchorStyles.Right,
            Margin = Padding.Empty
        };
        ThemeManager.SetGlyph(_btnBrowse, Glyph.Folder);
        _btnBrowse.Click += (_, _) => BrowseFolder();

        var folderBox = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            Size = new Size(400, 32),
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        folderBox.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        folderBox.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        folderBox.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        folderBox.Controls.Add(_txtFolderPath, 0, 0);
        folderBox.Controls.Add(_btnBrowse, 1, 0);

        var cardTarget = new SettingsCard();
        cardTarget.AddRow(L.T("Titel festlegen"), L.T("Name der Datei; ungültige Zeichen werden ersetzt."), _txtTitle).Stacked = true;
        cardTarget.AddRow(L.T("Zielordner festlegen"), null, folderBox).Stacked = true;

        // Scan settings summary
        var dpiValue = _settings.ImageQuality switch
        {
            ImageQuality.Normal => 150,
            ImageQuality.Fine => 200,
            ImageQuality.Best => 300,
            ImageQuality.Excellent => 600,
            _ => 200
        };

        var presetText = _settings.ImageQuality switch
        {
            ImageQuality.Automatic => "200 dpi",
            ImageQuality.Normal => L.T("Kleine Datei"),
            ImageQuality.Fine => L.T("Fein"),
            ImageQuality.Best => L.T("Beste Qualität"),
            ImageQuality.Excellent => L.T("Exzellent"),
            _ => L.T("Empfohlen")
        };

        var fileFormatText = _settings.FileFormat switch
        {
            FileFormat.Pdf => "PDF",
            FileFormat.Jpeg => L.T("JPEG"),
            FileFormat.Png => "PNG",
            _ => "PDF"
        };

        var summary = new SummaryList(presetText, new[]
        {
            (L.T("Auflösung"), $"{dpiValue} dpi"),
            (L.T("Farbmodus"), GetColorModeText(_settings.ColorMode)),
            (L.T("Scanseite"), GetScanSideText(_settings.ScanSide)),
            (L.T("Dateiformat"), fileFormatText),
            (L.T("Kompression"), $"Stufe {_settings.CompressionRate}")
        });
        var cardSummary = new SettingsCard(L.T("Scaneinstellungen")) { ShowDividers = false };
        cardSummary.Add(summary);

        var stack = new ModernStack { Dock = DockStyle.Top, Padding = new Padding(16, 16, 16, 16), Spacing = 12 };
        stack.Controls.Add(cardTarget);
        stack.Controls.Add(cardSummary);
        var contentHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        contentHost.Controls.Add(stack);

        Body.Controls.Add(contentHost);
        Body.Controls.Add(previewHost);

        // === Footer: page navigation (left), Abbrechen / Speichern (right) ===
        _btnPrev = new Button { Size = new Size(32, 32), Font = UiFont, Margin = new Padding(0), AccessibleName = L.T("Vorherige Seite") };
        _btnNext = new Button { Size = new Size(32, 32), Font = UiFont, Margin = new Padding(0), AccessibleName = L.T("Nächste Seite") };
        _btnPrev.Image = _chevronLeft = CreateChevron(left: true);
        _btnNext.Image = _chevronRight = CreateChevron(left: false);

        _lblPageCounter = new Label
        {
            Text = L.T("Seite 1"),
            Font = UiFont,
            AutoSize = false,
            Size = new Size(110, 32),
            TextAlign = ContentAlignment.MiddleCenter,
            UseMnemonic = false
        };
        ThemeManager.SetRole(_lblPageCounter, ThemeRole.Secondary);

        AddFooterControl(_btnPrev, alignLeft: true);
        AddFooterControl(_lblPageCounter, alignLeft: true);
        AddFooterControl(_btnNext, alignLeft: true);

        _btnCancel = AddFooterButton(L.T("Abbrechen"), ThemeRole.Secondary, Glyph.Cross);
        _btnSave = AddFooterButton(L.T("Speichern"), ThemeRole.Primary, Glyph.Check);

        AcceptButton = _btnSave;
        CancelButton = _btnCancel;

        // Wire events
        _btnSave.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_txtFolderPath.Text))
            {
                TrayMessageBox.Show(this, L.T("Bitte wählen Sie einen Zielordner."), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult = DialogResult.OK;
        };
        _btnCancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        _btnPrev.Click += (_, _) => NavigatePage(-1);
        _btnNext.Click += (_, _) => NavigatePage(1);

        // Set initial preview
        UpdatePreview();
        ThemeManager.Apply(this);
        ResumeLayout(true);
    }

    private readonly Bitmap _chevronLeft;
    private readonly Bitmap _chevronRight;

    /// <summary>Chevron for the page buttons (drawn dark; the themed button tints it).</summary>
    private static Bitmap CreateChevron(bool left)
    {
        var bmp = ThemeGlyphs.CreateBitmap(Glyph.ChevronRight, 16, Color.Black);
        if (left) bmp.RotateFlip(RotateFlipType.RotateNoneFlipX);
        return bmp;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _chevronLeft.Dispose();
            _chevronRight.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _txtTitle.SelectAll();
        _txtTitle.Focus();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult != DialogResult.OK && ConfirmDiscardOnCancel
            && e.CloseReason is CloseReason.UserClosing or CloseReason.None)
        {
            var answer = TrayMessageBox.Show(this,
                L.T("Scan verwerfen?\n\nJa: Der Scan wird gelöscht.\nNein: Zurück zum Dialog."),
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question, defaultSecondButton: true);
            if (answer != DialogResult.Yes)
            {
                e.Cancel = true;
                DialogResult = DialogResult.None;
                return;
            }
            DiscardConfirmed = true;
        }
        base.OnFormClosing(e);
    }

    private void BrowseFolder()
    {
        using var dlg = new FolderBrowserDialog
        {
            SelectedPath = _txtFolderPath.Text,
            Description = L.T("Zielordner wählen")
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _txtFolderPath.Text = dlg.SelectedPath;
        }
    }

    private void NavigatePage(int delta)
    {
        int newIndex = _currentPageIndex + delta;
        if (newIndex < 0 || newIndex >= _scanImages.Count) return;
        _currentPageIndex = newIndex;
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (_scanImages.Count > 0 && _currentPageIndex < _scanImages.Count)
        {
            _imgPreview.Image = _scanImages[_currentPageIndex];
            _lblPageCounter.Text = _scanImages.Count > 1
                ? L.F("Seite {0} von {1}", _currentPageIndex + 1, _scanImages.Count)
                : $"Seite {_currentPageIndex + 1}";
        }
        else
        {
            _imgPreview.Image = null;
            _lblPageCounter.Text = L.T("Seite 1");
        }

        _btnPrev.Enabled = _currentPageIndex > 0;
        _btnNext.Enabled = _currentPageIndex < _scanImages.Count - 1;
    }

    private static string GetColorModeText(ColorMode mode) => mode switch
    {
        ColorMode.Automatic => L.T("Farbe"),
        ColorMode.Color => L.T("Farbe"),
        ColorMode.Grayscale => L.T("Graustufen"),
        ColorMode.BlackWhite => "S/W",
        _ => L.T("Farbe")
    };

    private static string GetScanSideText(ScanSide side) => side switch
    {
        ScanSide.Automatic => L.T("Duplex"),
        ScanSide.Simplex => L.T("Simplex"),
        ScanSide.Duplex => L.T("Duplex"),
        ScanSide.Flatbed => L.T("Flachbett"),
        _ => L.T("Duplex")
    };

    /// <summary>
    /// Scanned page on a rounded canvas (light and dark): the page is drawn zoomed to fit with
    /// a thin outline so a white page stands out; "Kein Vorschaubild verfügbar" when empty.
    /// Also used by <see cref="MultiFeedWarningDialog"/>.
    /// </summary>
    internal sealed class PagePreview : Control, IThemeAware
    {
        private Image? _image;

        public PagePreview()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            AccessibleRole = AccessibleRole.Graphic;
        }

        public Image? Image
        {
            get => _image;
            set { _image = value; Invalidate(); }
        }

        /// <summary>Text shown without an image.</summary>
        public string EmptyText { get; set; } = L.T("Kein Vorschaubild verfügbar");

        public void ApplyTheme(ThemePalette palette) => Invalidate();

        private float S => DeviceDpi / 96f;

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = ThemeManager.Current;
            var g = e.Graphics;
            g.Clear(ModernLayout.ParentBack(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            float s = S;

            // Canvas: a recessed surface in both themes
            Color canvas = p.IsDark ? p.Tertiary : p.Secondary;
            var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = ModernLayout.RoundedRect(rect, 8 * s))
            {
                using (var brush = new SolidBrush(canvas))
                    g.FillPath(brush, path);
                using var pen = new Pen(p.IsDark ? p.Tertiary : p.CardBorder);
                g.DrawPath(pen, path);
            }

            if (_image == null || _image.Width <= 0 || _image.Height <= 0)
            {
                TextRenderer.DrawText(g, EmptyText, ThemeFonts.Regular, ClientRectangle, p.TextMuted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                return;
            }

            float pad = 14 * s;
            float availW = Math.Max(1, Width - 2 * pad), availH = Math.Max(1, Height - 2 * pad);
            float scale = Math.Min(availW / _image.Width, availH / _image.Height);
            float w = _image.Width * scale, h = _image.Height * scale;
            var page = new RectangleF((Width - w) / 2f, (Height - h) / 2f, w, h);

            // soft shadow
            for (int i = 3; i >= 1; i--)
            {
                using var shadow = new SolidBrush(Color.FromArgb(p.IsDark ? 34 : 14, 0, 0, 0));
                var sr = RectangleF.Inflate(page, i * s, i * s);
                sr.Offset(0, 1.5f * s);
                using var sp = ModernLayout.RoundedRect(sr, (2 + i) * s);
                g.FillPath(shadow, sp);
            }
            g.DrawImage(_image, page);
            using (var outline = new Pen(p.IsDark ? Color.FromArgb(70, 0, 0, 0) : p.CardBorder))
                g.DrawRectangle(outline, page.X, page.Y, page.Width, page.Height);
        }
    }

    /// <summary>
    /// Summary of the scan settings: the preset (accent) followed by label/value lines.
    /// </summary>
    private sealed class SummaryList : Control, IThemeAware, IHeightForWidth
    {
        private readonly string _preset;
        private readonly (string Label, string Value)[] _rows;

        public SummaryList(string preset, (string Label, string Value)[] rows)
        {
            _preset = preset;
            _rows = rows;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            AccessibleRole = AccessibleRole.StaticText;
            AccessibleName = L.T("Scaneinstellungen");
            AccessibleDescription = preset + ", " + string.Join(", ", rows.Select(r => $"{r.Label}: {r.Value}"));
        }

        public void ApplyTheme(ThemePalette palette) => Invalidate();

        private int S(float v) => ModernLayout.Scale(this, v);

        private int LineHeight => S(25);

        public int GetHeightForWidth(int width) => S(4) + S(28) + _rows.Length * LineHeight + S(8);

        public override Size GetPreferredSize(Size proposedSize) => new(Width, GetHeightForWidth(Width));

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = ThemeManager.Current;
            var g = e.Graphics;
            var back = ModernLayout.ParentBack(this);
            g.Clear(back);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Preset chip
            var flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
            var size = TextRenderer.MeasureText(g, _preset, ThemeFonts.Semibold, Size.Empty, flags);
            var chip = new RectangleF(0, S(4), size.Width + S(20), S(24));
            using (var path = ModernLayout.RoundedRect(chip, chip.Height / 2f))
            using (var brush = new SolidBrush(ThemePalette.Blend(back, p.Accent, p.IsDark ? 0.22f : 0.12f)))
                g.FillPath(brush, path);
            TextRenderer.DrawText(g, _preset, ThemeFonts.Semibold, Rectangle.Round(chip),
                p.IsDark ? ThemePalette.Blend(p.Accent, Color.White, 0.45f) : p.AccentHover,
                flags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            int y = S(4) + S(28) + S(2);
            int labelW = S(110);
            using var divider = new Pen(p.CardDivider);
            for (int i = 0; i < _rows.Length; i++)
            {
                var r = new Rectangle(0, y, Width, LineHeight);
                TextRenderer.DrawText(g, _rows[i].Label, ModernFonts.RowTitle, new Rectangle(r.X, r.Y, labelW, r.Height), p.TextMuted,
                    flags | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g, _rows[i].Value, ModernFonts.RowTitle, new Rectangle(r.X + labelW, r.Y, Math.Max(0, Width - labelW), r.Height), p.Text,
                    flags | TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.EndEllipsis);
                if (i < _rows.Length - 1)
                    g.DrawLine(divider, 0, r.Bottom - 1, Width, r.Bottom - 1);
                y += LineHeight;
            }
        }
    }
}
