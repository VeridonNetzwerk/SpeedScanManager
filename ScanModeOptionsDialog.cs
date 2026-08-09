using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Sub-dialog for scan mode options: brightness slider (B/W only),
/// blank page deletion, deskew, auto-rotate, face-up feeding.
/// </summary>
internal class ScanModeOptionsDialog : Form
{
    private readonly TrackBar _brightnessSlider;
    private readonly Label _lblBrightnessValue;
    private readonly CheckBox _chkTextOnly;
    private readonly CheckBox _chkDeleteBlankPages;
    private readonly CheckBox _chkDeskew;
    private readonly CheckBox _chkAutoRotate;
    private readonly CheckBox _chkFaceUp;

    private readonly ColorMode _colorMode;

    public int Brightness { get; set; }
    public bool TextOnlySettings { get; set; }
    public bool AllowDeleteBlankPages { get; set; } = true;
    public bool AllowDeskew { get; set; }
    public bool AllowAutoRotate { get; set; } = true;
    public bool FaceUpFeeding { get; set; }

    /// <param name="colorMode">Current color mode – brightness slider only enabled for BlackWhite.</param>
    public ScanModeOptionsDialog(ColorMode colorMode)
    {
        _colorMode = colorMode;

        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Scanmodus Option";
        ClientSize = new Size(400, 360);
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;

        var font = new Font("Microsoft Sans Serif", 8.25f);

        // === Brightness group ===
        var grpBrightness = new GroupBox
        {
            Text = "Helligkeit (nur Schwarz und Weiß-Scan)",
            Location = new Point(12, 12),
            Size = new Size(372, 80),
            Font = font
        };

        var picLeftIcon = new PictureBox
        {
            Image = TabIcons.CreateScanIcon(),
            SizeMode = PictureBoxSizeMode.AutoSize,
            Location = new Point(16, 28),
            Margin = Padding.Empty
        };

        var lblBright = new Label
        {
            Text = "Hell",
            Location = new Point(38, 32),
            AutoSize = true,
            Font = font
        };

        var lblDark = new Label
        {
            Text = "Dunkel",
            Location = new Point(320, 32),
            AutoSize = true,
            Font = font
        };

        var picRightIcon = new PictureBox
        {
            Image = TabIcons.CreatePaperIcon(),
            SizeMode = PictureBoxSizeMode.AutoSize,
            Location = new Point(348, 28),
            Margin = Padding.Empty
        };

        _brightnessSlider = new TrackBar
        {
            Location = new Point(66, 18),
            Size = new Size(250, 45),
            Minimum = -3,
            Maximum = 3,
            Value = 0,
            TickFrequency = 1,
            SmallChange = 1,
            LargeChange = 1,
            Enabled = colorMode == ColorMode.BlackWhite
        };
        _brightnessSlider.ValueChanged += (s, e) => UpdateBrightnessLabel();

        _lblBrightnessValue = new Label
        {
            Text = "Normal",
            Location = new Point(160, 52),
            AutoSize = true,
            Font = new Font("Microsoft Sans Serif", 8.25f, FontStyle.Bold),
            ForeColor = Color.FromArgb(45, 90, 170),
            TextAlign = ContentAlignment.MiddleCenter
        };

        grpBrightness.Controls.AddRange(new Control[] { picLeftIcon, lblBright, lblDark, picRightIcon, _brightnessSlider, _lblBrightnessValue });

        // === Checkboxes ===
        int cbY = 104;
        int cbSpacing = 28;

        _chkTextOnly = new CheckBox
        {
            Text = "Einstellung nur für Textdokumente",
            Location = new Point(16, cbY),
            AutoSize = true,
            Font = font
        };

        _chkDeleteBlankPages = new CheckBox
        {
            Text = "Automatisches Löschen leerer Seiten zulassen",
            Location = new Point(16, cbY + cbSpacing),
            AutoSize = true,
            Font = font,
            Checked = true
        };

        _chkDeskew = new CheckBox
        {
            Text = "Automatische Korrektur schiefer Zeichen zulassen",
            Location = new Point(16, cbY + cbSpacing * 2),
            AutoSize = true,
            Font = font
        };

        _chkAutoRotate = new CheckBox
        {
            Text = "Automatische Bilddrehung zulassen",
            Location = new Point(16, cbY + cbSpacing * 3),
            AutoSize = true,
            Font = font,
            Checked = true
        };

        _chkFaceUp = new CheckBox
        {
            Text = "Dokumente mit der Vorderseite nach oben einlegen",
            Location = new Point(16, cbY + cbSpacing * 4),
            AutoSize = true,
            Font = font
        };

        // === Buttons ===
        var btnOk = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Size = new Size(75, 28),
            Font = font
        };

        var btnCancel = new Button
        {
            Text = "Abbrechen",
            DialogResult = DialogResult.Cancel,
            Size = new Size(75, 28),
            Font = font
        };

        var btnHelp = new Button
        {
            Text = "Hilfe",
            Size = new Size(75, 28),
            Font = font
        };
        btnHelp.Click += (s, e) => MessageBox.Show(
            "Der Helligkeits-Slider ist nur für Schwarz-Weiß-Scans verfügbar.\n" +
            "Verschieben Sie den Regler nach links für hellere oder nach rechts für dunklere Scans.",
            "Hilfe – Helligkeit",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);

        var buttonPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 36,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            ColumnCount = 6,
            ColumnStyles =
            {
                new ColumnStyle(SizeType.Percent, 100f),
                new ColumnStyle(SizeType.Absolute, 75f),
                new ColumnStyle(SizeType.Absolute, 8f),
                new ColumnStyle(SizeType.Absolute, 75f),
                new ColumnStyle(SizeType.Absolute, 8f),
                new ColumnStyle(SizeType.Absolute, 75f)
            },
            RowCount = 1,
            RowStyles = { new RowStyle(SizeType.Absolute, 36f) }
        };
        buttonPanel.Margin = new Padding(0, 0, 12, 0);

        btnOk.Dock = DockStyle.Fill;
        btnCancel.Dock = DockStyle.Fill;
        btnHelp.Dock = DockStyle.Fill;
        btnOk.Margin = new Padding(0, 4, 0, 4);
        btnCancel.Margin = new Padding(0, 4, 0, 4);
        btnHelp.Margin = new Padding(0, 4, 0, 4);

        buttonPanel.Controls.Add(btnOk, 1, 0);
        buttonPanel.Controls.Add(btnCancel, 3, 0);
        buttonPanel.Controls.Add(btnHelp, 5, 0);

        Controls.AddRange(new Control[]
        {
            buttonPanel,
            grpBrightness,
            _chkTextOnly,
            _chkDeleteBlankPages,
            _chkDeskew,
            _chkAutoRotate,
            _chkFaceUp
        });

        AcceptButton = btnOk;
        CancelButton = btnCancel;
    }

    private void UpdateBrightnessLabel()
    {
        int val = _brightnessSlider.Value;
        _lblBrightnessValue.Text = val switch
        {
            0 => "Normal",
            _ when val < 0 => $"Hell {val}",
            _ => $"Dunkel +{val}"
        };
    }

    protected override void OnShown(EventArgs e)
    {
        // Restore settings
        _brightnessSlider.Value = Math.Clamp(Brightness, -3, 3);
        _chkTextOnly.Checked = TextOnlySettings;
        _chkDeleteBlankPages.Checked = AllowDeleteBlankPages;
        _chkDeskew.Checked = AllowDeskew;
        _chkAutoRotate.Checked = AllowAutoRotate;
        _chkFaceUp.Checked = FaceUpFeeding;

        UpdateBrightnessLabel();
        base.OnShown(e);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK)
        {
            Brightness = _brightnessSlider.Value;
            TextOnlySettings = _chkTextOnly.Checked;
            AllowDeleteBlankPages = _chkDeleteBlankPages.Checked;
            AllowDeskew = _chkDeskew.Checked;
            AllowAutoRotate = _chkAutoRotate.Checked;
            FaceUpFeeding = _chkFaceUp.Checked;
        }
        base.OnFormClosing(e);
    }
}
