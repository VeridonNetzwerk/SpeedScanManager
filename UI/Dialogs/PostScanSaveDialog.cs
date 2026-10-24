using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Post-scan "In Ordner speichern" dialog shown when destination is Scan to Folder.
/// Lets the user preview scanned images, choose a title and destination folder before saving.
/// Themed (light/dark) via <see cref="ThemeManager"/>: page preview on a canvas surface,
/// input fields on the right, footer bar with page navigation and the buttons.
/// </summary>
internal class PostScanSaveDialog : Form
{
    private readonly IReadOnlyList<Bitmap> _scanImages;
    private readonly string _defaultTitle;
    private readonly ScanSettings _settings;
    private int _currentPageIndex;

    private static readonly Font UiFont = ThemeFonts.Regular;
    private static readonly Font BoldFont = ThemeFonts.Semibold;

    private readonly SplitContainer _split;
    private readonly PictureBox _imgPreview;
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

        Text = "In Ordner speichern";
        Icon = TrayIcons.GetAppIcon();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        // Owner is the invisible tray window: center on screen and stay on top, otherwise the
        // dialog may open behind other windows (scan started with the hardware button).
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        ClientSize = new Size(560, 420);
        ShowInTaskbar = true;
        AutoScaleMode = AutoScaleMode.None;
        Font = UiFont;

        // === SplitContainer ===
        _split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 1,
            IsSplitterFixed = true,
            FixedPanel = FixedPanel.Panel1
        };

        // === Left side: Preview ===
        // Canvas surface around the page so a white page stands out in both themes
        var previewPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };
        ThemeManager.SetRole(previewPanel, ThemeRole.Sidebar);

        _imgPreview = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = null,
            Dock = DockStyle.Fill
        };
        previewPanel.Controls.Add(_imgPreview);
        _split.Panel1.Controls.Add(previewPanel);

        _lblPageCounter = new Label
        {
            Text = "Seite 1",
            Font = UiFont,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _btnPrev = new Button { Size = new Size(28, 28), Font = UiFont, Anchor = AnchorStyles.None, Margin = new Padding(0), AccessibleName = "Vorherige Seite" };
        _btnNext = new Button { Size = new Size(28, 28), Font = UiFont, Anchor = AnchorStyles.None, Margin = new Padding(0), AccessibleName = "Nächste Seite" };
        _btnPrev.Image = _chevronLeft = CreateChevron(left: true);
        _btnNext.Image = _chevronRight = CreateChevron(left: false);

        // === Right side: Title + Folder ===
        var contentPanel = new Panel { Dock = DockStyle.Fill };

        var lblTitle = new Label
        {
            Text = "Titel festlegen",
            Font = BoldFont,
            Location = new Point(14, 12),
            AutoSize = true
        };

        _txtTitle = new TextBox
        {
            Font = UiFont,
            Location = new Point(14, 32),
            Size = new Size(260, 24),
            Text = _defaultTitle
        };

        var lblFolder = new Label
        {
            Text = "Zielordner festlegen",
            Font = BoldFont,
            Location = new Point(14, 74),
            AutoSize = true
        };

        _txtFolderPath = new TextBox
        {
            ReadOnly = true,
            Font = UiFont,
            Location = new Point(14, 94),
            Size = new Size(260, 24),
            Text = defaultFolder
        };

        _btnBrowse = new Button
        {
            Text = "Durchsuchen...",
            Size = new Size(135, 28),
            Font = UiFont,
            Location = new Point(14, 124)
        };
        ThemeManager.SetGlyph(_btnBrowse, Glyph.Folder);
        _btnBrowse.Click += (_, _) => BrowseFolder();

        // Scan settings summary
        var grpSummary = new GroupBox
        {
            Text = "Scaneinstellungen",
            Location = new Point(14, 168),
            Size = new Size(260, 142),
            Font = UiFont
        };

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
            ImageQuality.Normal => "Kleine Datei",
            ImageQuality.Fine => "Fein",
            ImageQuality.Best => "Beste Qualität",
            ImageQuality.Excellent => "Exzellent",
            _ => "Empfohlen"
        };

        var fileFormatText = _settings.FileFormat switch
        {
            FileFormat.Pdf => "PDF",
            FileFormat.Jpeg => "JPEG",
            FileFormat.Png => "PNG",
            _ => "PDF"
        };

        var lblPreset = new Label
        {
            Text = presetText,
            Font = ThemeFonts.Semibold,
            Location = new Point(10, 22),
            AutoSize = true
        };
        ThemeManager.SetRole(lblPreset, ThemeRole.Accent);

        var lblDpi = new Label
        {
            Text = $"Auflösung: {dpiValue} dpi",
            Font = UiFont,
            Location = new Point(10, 40),
            AutoSize = true
        };

        var lblColor = new Label
        {
            Text = $"Farbmodus: {GetColorModeText(_settings.ColorMode)}",
            Font = UiFont,
            Location = new Point(10, 58),
            AutoSize = true
        };

        var lblSide = new Label
        {
            Text = $"Scanseite: {GetScanSideText(_settings.ScanSide)}",
            Font = UiFont,
            Location = new Point(10, 76),
            AutoSize = true
        };

        var lblFormat = new Label
        {
            Text = $"Dateiformat: {fileFormatText}",
            Font = UiFont,
            Location = new Point(10, 94),
            AutoSize = true
        };

        var lblCompression = new Label
        {
            Text = $"Kompression: Stufe {_settings.CompressionRate}",
            Font = UiFont,
            Location = new Point(10, 112),
            AutoSize = true
        };

        foreach (var row in new[] { lblDpi, lblColor, lblSide, lblFormat, lblCompression })
            ThemeManager.SetRole(row, ThemeRole.Muted);
        grpSummary.Controls.AddRange(new Control[] { lblPreset, lblDpi, lblColor, lblSide, lblFormat, lblCompression });

        contentPanel.Controls.AddRange(new Control[] { lblTitle, _txtTitle, lblFolder, _txtFolderPath, _btnBrowse, grpSummary });
        _split.Panel2.Controls.Add(contentPanel);

        // === Footer ===
        var footerPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            Padding = new Padding(8, 0, 8, 0)
        };
        ThemeManager.SetRole(footerPanel, ThemeRole.Secondary);

        _btnSave = new Button
        {
            Text = "Speichern",
            Size = new Size(110, 30),
            Font = UiFont,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0)
        };
        ThemeManager.SetRole(_btnSave, ThemeRole.Primary);
        ThemeManager.SetGlyph(_btnSave, Glyph.Check);

        _btnCancel = new Button
        {
            Text = "Abbrechen",
            Size = new Size(110, 30),
            Font = UiFont,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0)
        };
        ThemeManager.SetRole(_btnCancel, ThemeRole.Secondary);
        ThemeManager.SetGlyph(_btnCancel, Glyph.Cross);

        var footerTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            Margin = new Padding(0)
        };
        footerTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        footerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28));
        footerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        footerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28));
        footerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        footerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        footerTable.Controls.Add(_btnPrev, 0, 0);
        footerTable.Controls.Add(_lblPageCounter, 1, 0);
        footerTable.Controls.Add(_btnNext, 2, 0);
        footerTable.Controls.Add(new Panel { Dock = DockStyle.Fill }, 3, 0);
        footerTable.Controls.Add(_btnCancel, 4, 0);
        footerTable.Controls.Add(_btnSave, 5, 0);
        footerPanel.Controls.Add(footerTable);

        Controls.Add(_split);
        Controls.Add(footerPanel);
        _split.SplitterDistance = 260;

        AcceptButton = _btnSave;
        CancelButton = _btnCancel;

        // Wire events
        _btnSave.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_txtFolderPath.Text))
            {
                TrayMessageBox.Show(this, "Bitte wählen Sie einen Zielordner.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
        WindowActivation.BringToFront(this);
        _txtTitle.SelectAll();
        _txtTitle.Focus();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult != DialogResult.OK && ConfirmDiscardOnCancel
            && e.CloseReason is CloseReason.UserClosing or CloseReason.None)
        {
            var answer = TrayMessageBox.Show(this,
                "Scan verwerfen?\n\nJa: Der Scan wird gelöscht.\nNein: Zurück zum Dialog.",
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
            Description = "Zielordner wählen"
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
            _lblPageCounter.Text = $"Seite {_currentPageIndex + 1}";
        }
        else
        {
            _imgPreview.Image = null;
            _lblPageCounter.Text = "Seite 1";
        }

        _btnPrev.Enabled = _currentPageIndex > 0;
        _btnNext.Enabled = _currentPageIndex < _scanImages.Count - 1;
    }

    private static string GetColorModeText(ColorMode mode) => mode switch
    {
        ColorMode.Automatic => "Farbe",
        ColorMode.Color => "Farbe",
        ColorMode.Grayscale => "Graustufen",
        ColorMode.BlackWhite => "S/W",
        _ => "Farbe"
    };

    private static string GetScanSideText(ScanSide side) => side switch
    {
        ScanSide.Automatic => "Duplex",
        ScanSide.Simplex => "Simplex",
        ScanSide.Duplex => "Duplex",
        ScanSide.Flatbed => "Flachbett",
        _ => "Duplex"
    };
}
