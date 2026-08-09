using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Combo item with display text, internal enum value, and optional icon.
/// </summary>
internal sealed class ComboItem
{
    public string Text { get; }
    public object Value { get; }
    public Bitmap? Icon { get; }

    public ComboItem(string text, object value, Bitmap? icon = null)
    {
        Text = text;
        Value = value;
        Icon = icon;
    }

    public override string ToString() => Text;
}

/// <summary>
/// Content panel for the "Scanmodus" tab.
/// Contains image quality, color mode, scan side dropdowns with icons,
/// continue-scan checkbox, and an options sub-dialog.
/// </summary>
internal class ScanModeTabContent : Panel
{
    private readonly ComboBox _cbImageQuality;
    private readonly ComboBox _cbColorMode;
    private readonly ComboBox _cbScanSide;
    private readonly CheckBox _chkContinueScanning;
    private readonly Button _btnOptions;

    private readonly ScanSettings _settings;

    // Cached icon lists (avoid recreating on each draw)
    private static readonly List<ComboItem> QualityItems;
    private static readonly List<ComboItem> ColorItems;
    private static readonly List<ComboItem> ScanSideItems;

    static ScanModeTabContent()
    {
        QualityItems = new List<ComboItem>
        {
            new("Automatisch", ImageQuality.Automatic, TabIcons.CreateQualityAutoIcon()),
            new("Normal (Farbe/Grau: 150 dpi, S&W: 300 dpi)", ImageQuality.Normal, TabIcons.CreateQualityNormalIcon()),
            new("Fein (Farbe/Grau: 200 dpi, S&W: 400 dpi)", ImageQuality.Fine, TabIcons.CreateQualityFineIcon()),
            new("Beste (Farbe/Grau: 300 dpi, S&W: 600 dpi)", ImageQuality.Best, TabIcons.CreateQualityBestIcon()),
            new("Hervorragend (Farbe/Grau: 600 dpi, S&W: 1200 dpi)", ImageQuality.Excellent, TabIcons.CreateQualityExcellentIcon())
        };

        ColorItems = new List<ComboItem>
        {
            new("Automatische Farberkennung", ColorMode.Automatic, TabIcons.CreateColorAutoIcon()),
            new("Farbe", ColorMode.Color, TabIcons.CreateColorColorIcon()),
            new("Grau (umgekehrt)", ColorMode.Grayscale, TabIcons.CreateColorGrayIcon()),
            new("Schwarzweiß", ColorMode.BlackWhite, TabIcons.CreateColorBWIcon())
        };

        ScanSideItems = new List<ComboItem>
        {
            new("Duplex-Scan (doppelseitig)", ScanSide.Duplex, TabIcons.CreateScanSideDuplexIcon()),
            new("Simplex-Scan (einseitig)", ScanSide.Simplex, TabIcons.CreateScanSideSimplexIcon()),
            new("Flachbettscannen", ScanSide.Flatbed, TabIcons.CreateScanSideFlatbedIcon()),
            new("Automatisch", ScanSide.Automatic, TabIcons.CreateScanSideAutoIcon())
        };
    }

    public ScanSettings Settings => _settings;
    public event Action<ColorMode>? ColorModeChanged;

    public void ApplyPreset(ImageQuality quality)
    {
        _settings.ImageQuality = quality;
        SelectComboItem(_cbImageQuality, QualityItems, quality);
    }

    public ScanModeTabContent(ScanSettings settings)
    {
        _settings = settings;

        var font = new Font("Microsoft Sans Serif", 8.25f);
        Dock = DockStyle.Fill;
        BackColor = Color.White;

        // === Info bar ===
        var infoBar = new GradientInfoBar("Wählen Sie den gewünschten Scan-Modus.");

        // === Image Quality ===
        var lblImageQuality = new Label
        {
            Text = "Bildqualität:",
            Location = new Point(92, 30),
            AutoSize = true,
            Font = font
        };

        _cbImageQuality = CreateIconComboBox(QualityItems, new Point(176, 27));
        SelectComboItem(_cbImageQuality, QualityItems, _settings.ImageQuality);
        _cbImageQuality.SelectedIndexChanged += (s, e) =>
        {
            if (_cbImageQuality.SelectedIndex >= 0 && _cbImageQuality.SelectedIndex < QualityItems.Count)
                _settings.ImageQuality = (ImageQuality)QualityItems[_cbImageQuality.SelectedIndex].Value;
        };

        // === Color Mode ===
        var lblColorMode = new Label
        {
            Text = "Farbmodus:",
            Location = new Point(92, 62),
            AutoSize = true,
            Font = font
        };

        _cbColorMode = CreateIconComboBox(ColorItems, new Point(176, 59));
        SelectComboItem(_cbColorMode, ColorItems, _settings.ColorMode);
        _cbColorMode.SelectedIndexChanged += (s, e) =>
        {
            if (_cbColorMode.SelectedIndex >= 0 && _cbColorMode.SelectedIndex < ColorItems.Count)
            {
                _settings.ColorMode = (ColorMode)ColorItems[_cbColorMode.SelectedIndex].Value;
                ColorModeChanged?.Invoke(_settings.ColorMode);
            }
        };

        // === Scan Side ===
        var lblScanSide = new Label
        {
            Text = "Scan-Seite:",
            Location = new Point(92, 94),
            AutoSize = true,
            Font = font
        };

        _cbScanSide = CreateIconComboBox(ScanSideItems, new Point(176, 91));
        SelectComboItem(_cbScanSide, ScanSideItems, _settings.ScanSide);
        _cbScanSide.SelectedIndexChanged += (s, e) =>
        {
            if (_cbScanSide.SelectedIndex >= 0 && _cbScanSide.SelectedIndex < ScanSideItems.Count)
                _settings.ScanSide = (ScanSide)ScanSideItems[_cbScanSide.SelectedIndex].Value;
        };

        // === Hint label under Scan-Seite ===
        var lblHint = new Label
        {
            Text = "Die automatische Papiergrößenerkennung steht für das\r\nFlachbettscannen nicht zur Verfügung.",
            Location = new Point(176, 119),
            Size = new Size(420, 34),
            AutoSize = false,
            Font = font,
            ForeColor = Color.FromArgb(90, 90, 90)
        };

        // === Continue scanning checkbox ===
        _chkContinueScanning = new CheckBox
        {
            Text = "Scanvorgang nach aktuellem Scan fortsetzen",
            Location = new Point(176, 155),
            AutoSize = true,
            Font = font
        };
        _chkContinueScanning.CheckedChanged += (s, e) =>
            _settings.ContinueScanning = _chkContinueScanning.Checked;

        // === Options button (right-aligned) ===
        _btnOptions = new Button
        {
            Text = "Option...",
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            Location = new Point(511, 174),
            Size = new Size(84, 26),
            Font = font
        };
        _btnOptions.Click += (s, e) => OpenOptionsDialog();

        Controls.AddRange(new Control[]
        {
            infoBar,
            lblImageQuality, _cbImageQuality,
            lblColorMode, _cbColorMode,
            lblScanSide, _cbScanSide,
            lblHint,
            _chkContinueScanning,
            _btnOptions
        });
    }

    private static ComboBox CreateIconComboBox(List<ComboItem> items, Point location)
    {
        var cb = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Standard,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 18,
            IntegralHeight = true,
            Location = location,
            Size = new Size(420, 24),
            DropDownWidth = 420,
            Font = new Font("Microsoft Sans Serif", 8.25f)
        };

        foreach (var item in items)
            cb.Items.Add(item);

        // Shared draw handler
        cb.DrawItem += (s, e) => DrawComboItem(s, e, items);

        return cb;
    }

    private static void DrawComboItem(object? sender, DrawItemEventArgs e, List<ComboItem> items)
    {
        if (e.Index < 0) return;
        var cb = (ComboBox)sender!;
        var item = items[e.Index];

        // Background
        bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        Color bg = selected ? SystemColors.Highlight : cb.BackColor;
        Color fg = selected ? SystemColors.HighlightText : SystemColors.ControlText;

        using var bgBrush = new SolidBrush(bg);
        e.Graphics.FillRectangle(bgBrush, e.Bounds);

        // Icon
        if (item.Icon != null)
        {
            int iconY = e.Bounds.Y + (e.Bounds.Height - 16) / 2;
            e.Graphics.DrawImage(item.Icon, e.Bounds.X + 3, iconY, 16, 16);
        }

        // Text
        var textRect = new Rectangle(e.Bounds.X + 24, e.Bounds.Y, e.Bounds.Width - 27, e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, item.Text, cb.Font, textRect, fg,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        e.DrawFocusRectangle();
    }

    private static void SelectComboItem(ComboBox cb, List<ComboItem> items, object value)
    {
        int idx = items.FindIndex(item => item.Value.Equals(value));
        cb.SelectedIndex = idx >= 0 ? idx : 0;
    }

    private void OpenOptionsDialog()
    {
        using var dialog = new ScanModeOptionsDialog(_settings.ColorMode)
        {
            Brightness = _settings.Brightness,
            TextOnlySettings = _settings.TextOnlySettings,
            AllowDeleteBlankPages = _settings.AllowDeleteBlankPages,
            AllowDeskew = _settings.AllowDeskew,
            AllowAutoRotate = _settings.AllowAutoRotate,
            FaceUpFeeding = _settings.FaceUpFeeding
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _settings.Brightness = dialog.Brightness;
            _settings.TextOnlySettings = dialog.TextOnlySettings;
            _settings.AllowDeleteBlankPages = dialog.AllowDeleteBlankPages;
            _settings.AllowDeskew = dialog.AllowDeskew;
            _settings.AllowAutoRotate = dialog.AllowAutoRotate;
            _settings.FaceUpFeeding = dialog.FaceUpFeeding;
        }
    }
}
