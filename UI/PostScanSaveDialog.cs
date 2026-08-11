using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Post-scan "In Ordner speichern" dialog shown when destination is Scan to Folder.
/// Lets the user preview scanned images, choose a title and destination folder before saving.
/// Styled to match the rest of SpeedScan Manager (gray panels, blue accents, icon buttons).
/// </summary>
internal class PostScanSaveDialog : Form
{
    private static readonly Color PanelGray = Color.FromArgb(235, 235, 235);
    private static readonly Color AccentBlue = Color.FromArgb(45, 90, 170);
    private static readonly Color TextGray = Color.FromArgb(80, 85, 95);

    private readonly List<Bitmap> _scanImages;
    private readonly ScanSettings _settings;
    private int _currentPageIndex;

    private static readonly Font UiFont = new("Microsoft Sans Serif", 8.25f);
    private static readonly Font BoldFont = new("Microsoft Sans Serif", 8.25f, FontStyle.Bold);

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

    public string SelectedTitle => string.IsNullOrWhiteSpace(_txtTitle.Text) ? "ScanSnap" : _txtTitle.Text;
    public string SelectedFolderPath => _txtFolderPath.Text;

    public PostScanSaveDialog(List<Bitmap> scanImages, ScanSettings settings, string defaultTitle, string defaultFolder)
    {
        _scanImages = scanImages;
        _settings = settings;
        _currentPageIndex = 0;

        Text = "In Ordner speichern";
        Icon = TrayIcons.GetAppIcon();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(560, 420);
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;
        Font = UiFont;
        BackColor = PanelGray;

        // === SplitContainer ===
        _split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 4,
            BackColor = PanelGray
        };

        // === Left side: Preview ===
        var previewPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        _imgPreview = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = null,
            Size = new Size(240, 339),
            Anchor = AnchorStyles.None
        };

        _lblPageCounter = new Label
        {
            Text = "Seite 1",
            Font = UiFont,
            AutoSize = true,
            BackColor = PanelGray,
            ForeColor = TextGray,
            Anchor = AnchorStyles.None
        };

        _btnPrev = new Button { Text = "\u25C0", FlatStyle = FlatStyle.Standard, UseVisualStyleBackColor = true, Size = new Size(28, 22), Font = UiFont, Anchor = AnchorStyles.None };
        _btnNext = new Button { Text = "\u25B6", FlatStyle = FlatStyle.Standard, UseVisualStyleBackColor = true, Size = new Size(28, 22), Font = UiFont, Anchor = AnchorStyles.None };

        // Navigation bar at bottom of preview
        var navPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 28,
            BackColor = PanelGray
        };
        var navTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1
        };
        navTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        navTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        navTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        navTable.Controls.Add(_btnPrev, 0, 0);
        navTable.Controls.Add(_lblPageCounter, 1, 0);
        navTable.Controls.Add(_btnNext, 2, 0);
        navPanel.Controls.Add(navTable);

        previewPanel.Controls.AddRange(new Control[] { navPanel, _imgPreview });
        previewPanel.Resize += (_, _) =>
        {
            int x = (previewPanel.Width - _imgPreview.Width) / 2;
            int y = (previewPanel.Height - navPanel.Height - _imgPreview.Height) / 2;
            _imgPreview.Location = new Point(x < 0 ? 0 : x, y < 0 ? 0 : y);
        };
        _split.Panel1.Controls.Add(previewPanel);

        // === Right side: Title + Folder ===
        var contentPanel = new Panel { Dock = DockStyle.Fill, BackColor = PanelGray };

        var lblTitle = new Label
        {
            Text = "Titel festlegen",
            Font = BoldFont,
            Location = new Point(14, 12),
            AutoSize = true,
            ForeColor = Color.FromArgb(40, 40, 40)
        };

        _txtTitle = new TextBox
        {
            Font = UiFont,
            Location = new Point(14, 32),
            Size = new Size(260, 24),
            BackColor = Color.White,
            Text = defaultTitle
        };

        var lblFolder = new Label
        {
            Text = "Zielordner festlegen",
            Font = BoldFont,
            Location = new Point(14, 74),
            AutoSize = true,
            ForeColor = Color.FromArgb(40, 40, 40)
        };

        _txtFolderPath = new TextBox
        {
            ReadOnly = true,
            Font = UiFont,
            Location = new Point(14, 94),
            Size = new Size(260, 24),
            BackColor = Color.White,
            Text = defaultFolder
        };

        _btnBrowse = new Button
        {
            Text = "Durchsuchen...",
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            Size = new Size(135, 26),
            Font = UiFont,
            Location = new Point(14, 124),
            Image = TabIcons.CreateBrowseIcon(),
            ImageAlign = ContentAlignment.MiddleLeft,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(4, 0, 2, 0)
        };
        _btnBrowse.Click += (_, _) => BrowseFolder();

        // Scan settings summary
        var grpSummary = new GroupBox
        {
            Text = "Scaneinstellungen",
            Location = new Point(14, 168),
            Size = new Size(260, 130),
            Font = UiFont,
            ForeColor = TextGray,
            BackColor = PanelGray
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
            Font = new Font("Microsoft Sans Serif", 9f, FontStyle.Bold),
            Location = new Point(10, 16),
            AutoSize = true,
            ForeColor = AccentBlue
        };

        var lblDpi = new Label
        {
            Text = $"Auflösung: {dpiValue} dpi",
            Font = UiFont,
            Location = new Point(10, 34),
            AutoSize = true,
            ForeColor = TextGray
        };

        var lblColor = new Label
        {
            Text = $"Farbmodus: {GetColorModeText(_settings.ColorMode)}",
            Font = UiFont,
            Location = new Point(10, 52),
            AutoSize = true,
            ForeColor = TextGray
        };

        var lblSide = new Label
        {
            Text = $"Scanseite: {GetScanSideText(_settings.ScanSide)}",
            Font = UiFont,
            Location = new Point(10, 70),
            AutoSize = true,
            ForeColor = TextGray
        };

        var lblFormat = new Label
        {
            Text = $"Dateiformat: {fileFormatText}",
            Font = UiFont,
            Location = new Point(10, 88),
            AutoSize = true,
            ForeColor = TextGray
        };

        var lblCompression = new Label
        {
            Text = $"Kompression: Stufe {_settings.CompressionRate}",
            Font = UiFont,
            Location = new Point(10, 106),
            AutoSize = true,
            ForeColor = TextGray
        };

        grpSummary.Controls.AddRange(new Control[] { lblPreset, lblDpi, lblColor, lblSide, lblFormat, lblCompression });

        contentPanel.Controls.AddRange(new Control[] { lblTitle, _txtTitle, lblFolder, _txtFolderPath, _btnBrowse, grpSummary });
        _split.Panel2.Controls.Add(contentPanel);

        // === Footer ===
        var footerPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 42,
            BackColor = PanelGray
        };
        footerPanel.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(200, 200, 200), 1f);
            e.Graphics.DrawLine(pen, 0, 0, footerPanel.Width, 0);
        };

        _btnSave = new Button
        {
            Text = "Speichern",
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            Size = new Size(116, 26),
            Font = UiFont,
            Image = TabIcons.CreateCheckIcon(),
            ImageAlign = ContentAlignment.MiddleLeft,
            TextAlign = ContentAlignment.MiddleRight,
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            Margin = new Padding(0, 7, 0, 0)
        };

        _btnCancel = new Button
        {
            Text = "Abbrechen",
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            Size = new Size(116, 26),
            Font = UiFont,
            Image = TabIcons.CreateCrossIcon(),
            ImageAlign = ContentAlignment.MiddleLeft,
            TextAlign = ContentAlignment.MiddleRight,
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            Margin = new Padding(0, 7, 0, 0)
        };

        var footerTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = PanelGray
        };
        footerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        footerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        footerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 14));
        footerTable.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = PanelGray }, 0, 0);
        footerTable.Controls.Add(_btnCancel, 1, 0);
        footerTable.Controls.Add(_btnSave, 2, 0);
        footerTable.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = PanelGray }, 3, 0);
        footerPanel.Controls.Add(footerTable);

        Controls.Add(footerPanel);
        Controls.Add(_split);
        _split.SplitterDistance = 260;

        AcceptButton = _btnSave;
        CancelButton = _btnCancel;

        // Wire events
        _btnSave.Click += (_, _) => DialogResult = DialogResult.OK;
        _btnCancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        _btnPrev.Click += (_, _) => NavigatePage(-1);
        _btnNext.Click += (_, _) => NavigatePage(1);

        // Set initial preview
        UpdatePreview();
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
