using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Dialog for carrier sheet (Trägerblatt) settings.
/// Explains the carrier sheet workflow and allows configuring output size.
/// Modern layout (<see cref="ModernWindow"/>): a settings card and an instructions card
/// with the illustrations.
/// </summary>
internal class CarrierSheetDialog : ModernWindow
{
    private readonly CheckBox _chkEnabled;
    private readonly ComboBox _cbMode;
    private readonly ComboBox _cbOutputSize;
    private readonly NumericUpDown _numCustomWidth;
    private readonly NumericUpDown _numCustomHeight;
    private readonly Label _lblCustomWidth;
    private readonly Label _lblCustomHeight;
    private readonly SettingRow _rowCustomSize;
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
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        Text = "Trägerblatteinstellungen";
        Title = "Trägerblatteinstellungen";
        Subtitle = "Gefaltete Dokumente über das Trägerblatt scannen";
        HeaderButtons = ModernHeaderButtons.Close;
        HeaderIcon = ColorIcon.Tile(Glyph.Carrier, IconColors.Blue);
        AutoSizeToContent = true;
        MinimumContentWidth = 504;

        // === Enable switch ===
        _chkEnabled = new ToggleSwitch();

        // === Mode dropdown ===
        _cbMode = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Size = new Size(270, 24),
            Enabled = false
        };
        _cbMode.Items.Add("Zwei Seiten in einem Bild erstellen");
        _cbMode.SelectedIndex = 0;

        // === Output size dropdown ===
        _cbOutputSize = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Size = new Size(270, 24),
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
            AutoSize = true,
            Margin = new Padding(0, 4, 6, 0),
            Visible = false
        };

        _numCustomWidth = new NumericUpDown
        {
            Size = new Size(70, 24),
            Minimum = ScanSettings.MinCarrierSheetSizeMm,
            Maximum = ScanSettings.MaxCarrierSheetSizeMm,
            Value = 297,
            Margin = new Padding(0, 0, 16, 0),
            Visible = false
        };

        _lblCustomHeight = new Label
        {
            Text = "Höhe (mm):",
            AutoSize = true,
            Margin = new Padding(0, 4, 6, 0),
            Visible = false
        };

        _numCustomHeight = new NumericUpDown
        {
            Size = new Size(70, 24),
            Minimum = ScanSettings.MinCarrierSheetSizeMm,
            Maximum = ScanSettings.MaxCarrierSheetSizeMm,
            Value = 420,
            Margin = Padding.Empty,
            Visible = false
        };

        var customFlow = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        customFlow.Controls.AddRange(new Control[] { _lblCustomWidth, _numCustomWidth, _lblCustomHeight, _numCustomHeight });

        var cardSettings = new SettingsCard { Dense = true };
        cardSettings.AddRow("Trägerblatt-Modus aktivieren", null, _chkEnabled).Icon = ColorIcon.Tile(Glyph.Carrier, IconColors.Blue);
        cardSettings.AddRow("Modus", null, _cbMode, 270).Icon = ColorIcon.Tile(Glyph.Split, IconColors.Teal);
        cardSettings.AddRow("Ausgabebildgröße", null, _cbOutputSize, 270).Icon = ColorIcon.Tile(Glyph.Ruler, IconColors.Orange);
        _rowCustomSize = cardSettings.AddRow("Benutzerdefinierte Größe", null, customFlow);
        _rowCustomSize.Stacked = true;
        _rowCustomSize.Visible = false;

        // === Instructions with illustrations ===
        var pnlIllustrations = new Panel
        {
            Height = 170,
            Margin = Padding.Empty
        };
        // Painted completely by DrawIllustrations (inner tile + drawings).
        ThemeManager.SetRole(pnlIllustrations, ThemeRole.None);
        typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(pnlIllustrations, true);
        pnlIllustrations.Paint += (s, e) => DrawIllustrations(e.Graphics, pnlIllustrations);

        var lblIllustrations = new Label
        {
            Text = "1) Dokument falten    2) In Trägerblatt einlegen    3) In Scanner führen",
            AutoSize = false,
            Height = 34,
            TextAlign = ContentAlignment.MiddleCenter,
            UseMnemonic = false
        };
        ThemeManager.SetRole(lblIllustrations, ThemeRole.Muted);

        var cardInstructions = new SettingsCard("Anleitung",
            "Für Dokumente, die größer als das normale Scan-Format sind (z. B. A3), " +
            "falten Sie das Dokument entlang der Mittelachse. Legen Sie es in ein " +
            "Trägerblatt ein und führen Sie das Trägerblatt in den Scanner ein. " +
            "Beide Seiten werden gescannt und im Ausgabebild nebeneinander " +
            "zusammengefügt.") { ShowDividers = false, Dense = true, Icon = ColorIcon.Tile(Glyph.Question, IconColors.Sky) };
        cardInstructions.Add(new Panel { Height = 8 });
        cardInstructions.Add(pnlIllustrations);
        cardInstructions.Add(lblIllustrations);

        var stack = new ModernStack { Dock = DockStyle.Fill, Padding = new Padding(16, 16, 16, 16), Spacing = 12 };
        stack.Controls.Add(cardSettings);
        stack.Controls.Add(cardInstructions);
        Body.Controls.Add(stack);

        // === Footer ===
        _btnCancel = AddFooterButton("Abbrechen", ThemeRole.Secondary, Glyph.Cross, DialogResult.Cancel);
        _btnOk = AddFooterButton("OK", ThemeRole.Primary, Glyph.Check, DialogResult.OK);

        _chkEnabled.CheckedChanged += (s, e) => UpdateEnabledState();

        AcceptButton = _btnOk;
        CancelButton = _btnCancel;
        ResumeLayout(false);

        ThemeManager.Apply(this);
    }

    protected override void OnLoad(EventArgs e)
    {
        // Size the window for the initial state (custom size row shown or not).
        _rowCustomSize.Visible = CarrierSheetEnabled && OutputSize == CarrierSheetOutputSize.Custom;
        base.OnLoad(e);
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
        if (_rowCustomSize.Visible != showCustom)
        {
            _rowCustomSize.Visible = showCustom;
            // The window follows the content (row shown/hidden).
            if (IsLayoutScaled && AutoSizeToContent)
                FitToContent();
        }
    }

    /// <summary>
    /// Draws three simple schematic illustrations for the carrier sheet workflow.
    /// </summary>
    private void DrawIllustrations(Graphics g, Control panel)
    {
        var p = ThemeManager.Current;
        var size = panel.ClientSize;
        g.Clear(p.Card);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Inner tile (the drawings sit on it)
        var bg = p.TileOnCard;
        float s = panel.DeviceDpi / 96f;
        using (var tilePath = ThemeGlyphs.Rounded(new RectangleF(0.5f, 0.5f, size.Width - 1.5f, size.Height - 1.5f), 8 * s))
        {
            using (var tileBrush = new SolidBrush(bg))
                g.FillPath(tileBrush, tilePath);
            using var tilePen = new Pen(p.CardBorder);
            g.DrawPath(tilePen, tilePath);
        }

        int panelW = size.Width / 3;
        int centerY = size.Height / 2;
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
