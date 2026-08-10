using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Post-scan "Verify and save" dialog shown when destination is Scan to Folder.
/// Lets the user preview scanned images, choose a title and destination folder before saving.
/// </summary>
internal class PostScanSaveDialog : Form
{
    private readonly List<Bitmap> _scanImages;
    private readonly ScanSettings _settings;
    private int _currentPageIndex;

    private static readonly Font UiFont = new("Microsoft Sans Serif", 8.25f);

    private readonly SplitContainer _split;
    private readonly PictureBox _imgPreview;
    private readonly Label _lblPageCounter;
    private readonly ComboBox _cbTitle;
    private readonly TextBox _txtFolderPath;
    private readonly Button _btnBrowse;
    private readonly Button _btnSave;
    private readonly Button _btnCancel;
    private readonly Button _btnPrev;
    private readonly Button _btnNext;

    public string SelectedTitle => _cbTitle.SelectedItem?.ToString() ?? "ScanSnap";
    public string SelectedFolderPath => _txtFolderPath.Text;

    public PostScanSaveDialog(List<Bitmap> scanImages, ScanSettings settings)
    {
        _scanImages = scanImages;
        _settings = settings;
        _currentPageIndex = 0;

        Text = "Verify and save";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 340);
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;
        Font = UiFont;
        BackColor = Color.FromArgb(240, 240, 240);

        // === SplitContainer ===
        _split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 4,
            BackColor = SystemColors.Control,
            SplitterDistance = 260
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
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = null
        };

        _lblPageCounter = new Label
        {
            Text = "1 / 1",
            Font = UiFont,
            AutoSize = true,
            BackColor = Color.FromArgb(240, 240, 240)
        };

        _btnPrev = new Button { Text = "\u25C0", FlatStyle = FlatStyle.Standard, Size = new Size(28, 22) };
        _btnNext = new Button { Text = "\u25B6", FlatStyle = FlatStyle.Standard, Size = new Size(28, 22) };
        var btnZoom = new Button { Text = "\u2795", FlatStyle = FlatStyle.Standard, Size = new Size(28, 22) };
        var btnComment = new Button { Text = "\u270E", FlatStyle = FlatStyle.Standard, Size = new Size(28, 22) };

        // Navigation bar at bottom of preview
        var navPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 28,
            BackColor = Color.FromArgb(240, 240, 240)
        };
        var navTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1
        };
        for (int i = 0; i < 6; i++)
            navTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32));
        navTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        navTable.Controls.Add(_btnPrev, 0, 0);
        navTable.Controls.Add(_btnNext, 1, 0);
        navTable.Controls.Add(btnZoom, 2, 0);
        navTable.Controls.Add(btnComment, 3, 0);
        navTable.Controls.Add(new Panel { Dock = DockStyle.Fill }, 4, 0);
        navTable.Controls.Add(_lblPageCounter, 5, 0);
        navPanel.Controls.Add(navTable);

        previewPanel.Controls.AddRange(new Control[] { _imgPreview, navPanel });
        _split.Panel1.Controls.Add(previewPanel);

        // === Right side: Title + Folder ===
        var contentPanel = new Panel { Dock = DockStyle.Fill };

        var lblTitle = new Label
        {
            Text = "Specify a title",
            Font = UiFont,
            Location = new Point(12, 12),
            AutoSize = true,
            ForeColor = Color.FromArgb(80, 80, 80)
        };

        _cbTitle = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = UiFont,
            Location = new Point(12, 30),
            Size = new Size(230, 24)
        };
        _cbTitle.Items.AddRange(new string[] { "ScanSnap", "Neuer Titel...", "Benutzerdefiniert..." });
        _cbTitle.SelectedIndex = 0;

        var lblFolder = new Label
        {
            Text = "Specify destination folder",
            Font = UiFont,
            Location = new Point(12, 70),
            AutoSize = true,
            ForeColor = Color.FromArgb(80, 80, 80)
        };

        // Default folder from save config
        string defaultFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SpeedScanManager");

        _txtFolderPath = new TextBox
        {
            ReadOnly = true,
            Font = UiFont,
            Location = new Point(12, 90),
            Size = new Size(230, 24),
            BackColor = SystemColors.Control,
            Text = defaultFolder
        };

        _btnBrowse = new Button
        {
            Text = "Browse...",
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            Size = new Size(75, 24),
            Font = UiFont,
            Location = new Point(12, 120)
        };
        _btnBrowse.Click += (_, _) => BrowseFolder();

        // Scan settings summary
        var grpSummary = new GroupBox
        {
            Text = "Scan settings summary",
            Location = new Point(12, 158),
            Size = new Size(230, 60),
            Font = UiFont,
            BackColor = Color.White
        };

        var presetText = _settings.ImageQuality switch
        {
            ImageQuality.Automatic => "Empfohlen",
            ImageQuality.Normal => "Kleine Datei",
            ImageQuality.Fine => "Fein",
            ImageQuality.Best => "Beste Qualität",
            ImageQuality.Excellent => "Exzellent",
            _ => "Empfohlen"
        };

        var qualityDetail = $"{GetColorModeText(_settings.ColorMode)} · {GetScanSideText(_settings.ScanSide)}";

        var lblPreset = new Label
        {
            Text = presetText,
            Font = new Font("Microsoft Sans Serif", 9f, FontStyle.Bold),
            Location = new Point(10, 16),
            AutoSize = true
        };

        var lblQuality = new Label
        {
            Text = qualityDetail,
            Font = UiFont,
            Location = new Point(10, 34),
            AutoSize = true,
            ForeColor = Color.FromArgb(80, 80, 80)
        };

        grpSummary.Controls.AddRange(new Control[] { lblPreset, lblQuality });

        contentPanel.Controls.AddRange(new Control[] { lblTitle, _cbTitle, lblFolder, _txtFolderPath, _btnBrowse, grpSummary });
        _split.Panel2.Controls.Add(contentPanel);

        // === Footer ===
        var footerPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            BackColor = Color.FromArgb(240, 240, 240)
        };

        _btnSave = new Button
        {
            Text = "Save",
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            Size = new Size(80, 26),
            Font = UiFont,
            BackColor = Color.FromArgb(59, 130, 246),
            ForeColor = Color.White
        };

        _btnCancel = new Button
        {
            Text = "Cancel",
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            Size = new Size(80, 26),
            Font = UiFont
        };

        var footerTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1
        };
        footerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
        footerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
        footerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12));
        footerTable.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 0);
        footerTable.Controls.Add(_btnCancel, 1, 0);
        footerTable.Controls.Add(_btnSave, 2, 0);
        footerTable.Controls.Add(new Panel { Dock = DockStyle.Fill }, 3, 0);
        footerPanel.Controls.Add(footerTable);

        Controls.Add(footerPanel);
        Controls.Add(_split);

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
            _lblPageCounter.Text = $"{_currentPageIndex + 1} / {_scanImages.Count}";
        }
        else
        {
            _imgPreview.Image = null;
            _lblPageCounter.Text = "0 / 0";
        }

        _btnPrev.Enabled = _currentPageIndex > 0;
        _btnNext.Enabled = _currentPageIndex < _scanImages.Count - 1;
    }

    private static string GetColorModeText(ColorMode mode) => mode switch
    {
        ColorMode.Automatic => "Automatisch",
        ColorMode.Color => "Farbe",
        ColorMode.Grayscale => "Graustufen",
        ColorMode.BlackWhite => "S/W",
        _ => "Automatisch"
    };

    private static string GetScanSideText(ScanSide side) => side switch
    {
        ScanSide.Automatic => "Automatisch",
        ScanSide.Simplex => "Simplex",
        ScanSide.Duplex => "Duplex",
        ScanSide.Flatbed => "Flachbett",
        _ => "Automatisch"
    };
}
