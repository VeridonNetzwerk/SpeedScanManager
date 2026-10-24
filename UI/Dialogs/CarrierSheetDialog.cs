using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Dialog for carrier sheet (Trägerblatt) settings.
/// Explains the carrier sheet workflow and allows configuring output size.
/// </summary>
internal class CarrierSheetDialog : Form
{
    private readonly CheckBox _chkEnabled;
    private readonly ComboBox _cbMode;
    private readonly ComboBox _cbOutputSize;
    private readonly NumericUpDown _numCustomWidth;
    private readonly NumericUpDown _numCustomHeight;
    private readonly Label _lblCustomWidth;
    private readonly Label _lblCustomHeight;
    private readonly Button _btnOk;
    private readonly Button _btnCancel;

    public bool CarrierSheetEnabled { get; set; }
    public CarrierSheetOutputSize OutputSize { get; set; } = CarrierSheetOutputSize.Automatic;
    public int CustomWidth { get; set; } = 297;
    public int CustomHeight { get; set; } = 420;

    /// <summary>Output sizes in display order (help: [Ausgabebildgröße] Auswahlliste).</summary>
    private static readonly (string Text, CarrierSheetOutputSize Value)[] OutputSizes =
    {
        ("Automatische Erkennung", CarrierSheetOutputSize.Automatic),
        ("A3 (297 x 420 mm)", CarrierSheetOutputSize.A3),
        ("A4 (210 x 297 mm)", CarrierSheetOutputSize.A4),
        ("A5 (148 x 210 mm)", CarrierSheetOutputSize.A5),
        ("A6 (105 x 148 mm)", CarrierSheetOutputSize.A6),
        ("B4(JIS) (257 x 364 mm)", CarrierSheetOutputSize.B4),
        ("B5(JIS) (182 x 257 mm)", CarrierSheetOutputSize.B5),
        ("B6(JIS) (128 x 182 mm)", CarrierSheetOutputSize.B6),
        ("Letter (216 x 279,4 mm)", CarrierSheetOutputSize.Letter),
        ("Double Letter (279,4 x 431,8 mm)", CarrierSheetOutputSize.DoubleLetter),
        ("Postkarte (100 x 148 mm)", CarrierSheetOutputSize.Postcard),
        ("Visitenkarte (55 x 91 mm)", CarrierSheetOutputSize.BusinessCard),
        ("Trägerblattgröße (220 x 297 mm)", CarrierSheetOutputSize.CarrierSheet),
        ("Benutzerdefiniert...", CarrierSheetOutputSize.Custom)
    };

    private static int IndexOfSize(CarrierSheetOutputSize value)
        => Math.Max(0, Array.FindIndex(OutputSizes, o => o.Value == value));

    public CarrierSheetDialog()
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Trägerblatteinstellungen";
        Icon = TrayIcons.GetAppIcon();
        ClientSize = new Size(466, 488);
        ShowInTaskbar = false;

        var font = UiFonts.Default;
        var smallFont = UiFonts.Default;
        Font = font;

        // === Enable checkbox ===
        _chkEnabled = new CheckBox
        {
            Text = "Trägerblatt-Modus aktivieren",
            Location = new Point(12, 12),
            AutoSize = true,
            Font = font
        };

        // === Mode dropdown ===
        var lblMode = new Label
        {
            Text = "Modus:",
            Location = new Point(12, 44),
            AutoSize = true,
            Font = font
        };

        _cbMode = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(140, 41),
            Size = new Size(300, 24),
            Font = font,
            Enabled = false
        };
        _cbMode.Items.Add("Zwei Seiten in einem Bild erstellen");
        _cbMode.SelectedIndex = 0;

        // === Output size dropdown ===
        var lblOutputSize = new Label
        {
            Text = "Ausgabebildgröße:",
            Location = new Point(12, 76),
            AutoSize = true,
            Font = font
        };

        _cbOutputSize = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(140, 73),
            Size = new Size(240, 24),
            Font = font,
            Enabled = false,
            MaxDropDownItems = OutputSizes.Length
        };
        foreach (var (text, _) in OutputSizes)
            _cbOutputSize.Items.Add(text);
        _cbOutputSize.SelectedIndex = 0;
        _cbOutputSize.SelectedIndexChanged += (s, e) => UpdateCustomSizeVisibility();

        // === Custom size inputs ===
        _lblCustomWidth = new Label
        {
            Text = "Breite (mm):",
            Location = new Point(140, 108),
            AutoSize = true,
            Font = font,
            Visible = false
        };

        _numCustomWidth = new NumericUpDown
        {
            Location = new Point(220, 105),
            Size = new Size(60, 24),
            Font = font,
            Minimum = ScanSettings.MinCarrierSheetSizeMm,
            Maximum = ScanSettings.MaxCarrierSheetSizeMm,
            Value = 297,
            Visible = false
        };

        _lblCustomHeight = new Label
        {
            Text = "Höhe (mm):",
            Location = new Point(296, 108),
            AutoSize = true,
            Font = font,
            Visible = false
        };

        _numCustomHeight = new NumericUpDown
        {
            Location = new Point(372, 105),
            Size = new Size(60, 24),
            Font = font,
            Minimum = ScanSettings.MinCarrierSheetSizeMm,
            Maximum = ScanSettings.MaxCarrierSheetSizeMm,
            Value = 420,
            Visible = false
        };

        // === Instruction text ===
        var lblInstructions = new Label
        {
            Text = "Anleitung:\n\n" +
                   "Für Dokumente, die größer als das normale Scan-Format sind (z. B. A3), " +
                   "falten Sie das Dokument entlang der Mittelachse. Legen Sie es in ein " +
                   "Trägerblatt ein und führen Sie das Trägerblatt in den Scanner ein. " +
                   "Beide Seiten werden gescannt und im Ausgabebild nebeneinander " +
                   "zusammengefügt.",
            Location = new Point(12, 136),
            Size = new Size(440, 94),
            Font = smallFont
        };

        // === Illustrations ===
        var pnlIllustrations = new Panel
        {
            Location = new Point(12, 234),
            Size = new Size(440, 166)
        };
        ThemeManager.SetRole(pnlIllustrations, ThemeRole.Card);
        typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(pnlIllustrations, true);

        var lblIllustrations = new Label
        {
            Text = "1) Dokument falten    2) In Trägerblatt einlegen    3) In Scanner führen",
            Location = new Point(12, 404),
            Size = new Size(440, 20),
            Font = smallFont,
            TextAlign = ContentAlignment.MiddleCenter
        };

        // === Buttons ===
        _btnOk = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(290, 444),
            Size = new Size(75, 28),
            Font = font
        };

        _btnCancel = new Button
        {
            Text = "Abbrechen",
            DialogResult = DialogResult.Cancel,
            Location = new Point(377, 444),
            Size = new Size(75, 28),
            Font = font
        };

        _chkEnabled.CheckedChanged += (s, e) => UpdateEnabledState();

        Controls.AddRange(new Control[]
        {
            _chkEnabled,
            lblMode, _cbMode,
            lblOutputSize, _cbOutputSize,
            _lblCustomWidth, _numCustomWidth,
            _lblCustomHeight, _numCustomHeight,
            lblInstructions,
            pnlIllustrations,
            lblIllustrations,
            _btnOk, _btnCancel
        });

        AcceptButton = _btnOk;
        CancelButton = _btnCancel;
        ResumeLayout(false);
        PerformLayout();

        ThemeManager.Apply(this);
        // After Apply: the card background is painted first, the illustrations on top.
        pnlIllustrations.Paint += (s, e) => DrawIllustrations(e.Graphics, pnlIllustrations.Size);
    }

    private void UpdateEnabledState()
    {
        bool on = _chkEnabled.Checked;
        _cbMode.Enabled = on;
        _cbOutputSize.Enabled = on;
        UpdateCustomSizeVisibility();
    }

    private void UpdateCustomSizeVisibility()
    {
        bool showCustom = _chkEnabled.Checked && _cbOutputSize.SelectedIndex >= 0
                          && OutputSizes[_cbOutputSize.SelectedIndex].Value == CarrierSheetOutputSize.Custom;
        _lblCustomWidth.Visible = showCustom;
        _numCustomWidth.Visible = showCustom;
        _lblCustomHeight.Visible = showCustom;
        _numCustomHeight.Visible = showCustom;
    }

    /// <summary>
    /// Draws three simple schematic illustrations for the carrier sheet workflow.
    /// </summary>
    private void DrawIllustrations(Graphics g, Size size)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int panelW = size.Width / 3;
        int centerY = size.Height / 2;

        // Theme colors (the panel is a card on the secondary surface)
        var p = ThemeManager.Current;
        var bg = p.Secondary;
        using var pen = new Pen(p.Accent, 2);
        using var foldPen = new Pen(p.Danger, 1.5f) { DashStyle = DashStyle.Dash };
        using var fillBrush = new SolidBrush(ThemePalette.Blend(p.Accent, bg, p.IsDark ? 0.78f : 0.86f));
        using var docBrush = new SolidBrush(ThemePalette.Blend(p.Warning, bg, p.IsDark ? 0.62f : 0.8f));
        using var docPen = new Pen(p.Warning, 1);
        using var arrowPen = new Pen(p.TextMuted, 2);
        using var scannerBrush = new SolidBrush(p.IsDark ? p.ButtonSecondary : p.Tertiary);
        using var scannerPen = new Pen(p.TextMuted, 1);
        using var textBrush = new SolidBrush(p.Text);
        var font = ThemeFonts.Semibold;

        // === Illustration 1: Folded document ===
        int x1 = 20;
        // Left half (folded document)
        g.FillRectangle(docBrush, x1, centerY - 40, 60, 80);
        g.DrawRectangle(pen, x1, centerY - 40, 60, 80);
        // Fold line in the middle
        g.DrawLine(foldPen, x1, centerY, x1 + 60, centerY);
        // Arrow showing fold
        g.DrawCurve(arrowPen, new[] {
            new Point(x1 + 70, centerY - 30),
            new Point(x1 + 85, centerY),
            new Point(x1 + 70, centerY + 30)
        });
        // Result: folded (narrower)
        g.FillRectangle(docBrush, x1 + 100, centerY - 40, 30, 80);
        g.DrawRectangle(pen, x1 + 100, centerY - 40, 30, 80);
        g.DrawString("1)", font, textBrush, x1 + 5, 5);

        // === Illustration 2: Document in carrier sheet ===
        int x2 = panelW + 20;
        // Carrier sheet (larger, light blue)
        g.FillRectangle(fillBrush, x2, centerY - 50, 80, 100);
        g.DrawRectangle(pen, x2, centerY - 50, 80, 100);
        // Folded document inside (yellowish)
        g.FillRectangle(docBrush, x2 + 20, centerY - 35, 40, 70);
        g.DrawRectangle(docPen, x2 + 20, centerY - 35, 40, 70);
        g.DrawString("2)", font, textBrush, x2 + 5, 5);

        // === Illustration 3: Carrier sheet going into scanner ===
        int x3 = panelW * 2 + 20;
        // Scanner (simple rectangle at bottom)
        g.FillRectangle(scannerBrush, x3, centerY + 20, 90, 30);
        g.DrawRectangle(scannerPen, x3, centerY + 20, 90, 30);
        // Feed slot
        g.DrawLine(scannerPen, x3 + 10, centerY + 20, x3 + 80, centerY + 20);
        // Carrier sheet being fed in
        g.FillRectangle(fillBrush, x3 + 20, centerY - 40, 50, 60);
        g.DrawRectangle(pen, x3 + 20, centerY - 40, 50, 60);
        // Arrow pointing down into scanner
        g.DrawLine(arrowPen, x3 + 45, centerY - 45, x3 + 45, centerY + 10);
        g.DrawLine(arrowPen, x3 + 45, centerY + 10, x3 + 38, centerY + 2);
        g.DrawLine(arrowPen, x3 + 45, centerY + 10, x3 + 52, centerY + 2);
        g.DrawString("3)", font, textBrush, x3 + 5, 5);
    }

    protected override void OnShown(EventArgs e)
    {
        _chkEnabled.Checked = CarrierSheetEnabled;
        _cbOutputSize.SelectedIndex = IndexOfSize(OutputSize);
        _numCustomWidth.Value = Math.Clamp(CustomWidth, ScanSettings.MinCarrierSheetSizeMm, ScanSettings.MaxCarrierSheetSizeMm);
        _numCustomHeight.Value = Math.Clamp(CustomHeight, ScanSettings.MinCarrierSheetSizeMm, ScanSettings.MaxCarrierSheetSizeMm);
        UpdateEnabledState();
        base.OnShown(e);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK)
        {
            CarrierSheetEnabled = _chkEnabled.Checked;
            OutputSize = OutputSizes[Math.Max(0, _cbOutputSize.SelectedIndex)].Value;
            CustomWidth = (int)_numCustomWidth.Value;
            CustomHeight = (int)_numCustomHeight.Value;
        }
        base.OnFormClosing(e);
    }
}
