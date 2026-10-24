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
    private readonly Image _leftIcon;
    private readonly Image _rightIcon;

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

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Scanmodus-Optionen";
        Icon = TrayIcons.GetAppIcon();
        ClientSize = new Size(400, 312);
        ShowInTaskbar = false;

        var font = UiFonts.Default;
        Font = font;
        _leftIcon = TabIcons.CreateScanIcon();
        _rightIcon = TabIcons.CreatePaperIcon();

        // === Brightness group ===
        var grpBrightness = new GroupBox
        {
            Text = "Helligkeit (nur Schwarzweiß-Scan)",
            Location = new Point(12, 12),
            Size = new Size(376, 92),
            Font = font
        };

        var picLeftIcon = new PictureBox
        {
            Image = _leftIcon,
            SizeMode = PictureBoxSizeMode.AutoSize,
            Location = new Point(16, 28),
            Margin = Padding.Empty
        };

        // Slider: negative values (left) = darker, positive (right) = lighter (TWAIN brightness)
        var lblDark = new Label
        {
            Text = "Dunkel",
            Location = new Point(30, 32),
            AutoSize = true,
            Font = font
        };

        var lblBright = new Label
        {
            Text = "Hell",
            Location = new Point(322, 32),
            AutoSize = true,
            Font = font
        };

        var picRightIcon = new PictureBox
        {
            Image = _rightIcon,
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
            Location = new Point(66, 64),
            Size = new Size(250, 18),
            AutoSize = false,
            Font = UiFonts.Bold,
            TextAlign = ContentAlignment.MiddleCenter
        };
        ThemeManager.SetRole(_lblBrightnessValue, ThemeRole.Accent);

        grpBrightness.Controls.AddRange(new Control[] { picLeftIcon, lblDark, lblBright, picRightIcon, _brightnessSlider, _lblBrightnessValue });

        // === Checkboxes ===
        int cbY = 116;
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
            Text = "Schräglauf automatisch korrigieren",
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
        btnHelp.Click += (s, e) => { using var h = new HelpForm("scanmode-options"); h.ShowDialog(this); };

        var buttonPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 46,
            Margin = Padding.Empty,
            Padding = new Padding(0, 0, 12, 10),
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
        ResumeLayout(false);
        PerformLayout();

        ThemeManager.Apply(this);
    }

    private void UpdateBrightnessLabel()
    {
        int val = _brightnessSlider.Value;
        _lblBrightnessValue.Text = val switch
        {
            0 => "Normal",
            _ when val < 0 => $"Dunkler {val}",
            _ => $"Heller +{val}"
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _leftIcon.Dispose();
            _rightIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
