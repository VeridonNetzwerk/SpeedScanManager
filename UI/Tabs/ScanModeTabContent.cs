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
/// Content of the "Scanmodus" settings page: image quality, color mode and scan side
/// dropdowns with icons, the continue-scan switch and the options sub-dialog, grouped in
/// settings cards.
/// </summary>
internal class ScanModeTabContent : ModernStack
{
    private readonly ComboBox _cbImageQuality;
    private readonly ComboBox _cbColorMode;
    private readonly ComboBox _cbScanSide;
    private readonly CheckBox _chkContinueScanning;
    private readonly Button _btnOptions;

    private readonly ScanSettings _settings;
    private bool _loading;

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
            new("Grau", ColorMode.Grayscale, TabIcons.CreateColorGrayIcon()),
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
    public event Action<ScanSide>? ScanSideChanged;

    public void ApplyPreset(ImageQuality quality)
    {
        _settings.ImageQuality = quality;
        _loading = true;
        try { SelectComboItem(_cbImageQuality, QualityItems, quality); }
        finally { _loading = false; }
    }

    /// <summary>
    /// Refreshes all controls from the settings without raising change notifications.
    /// </summary>
    public void LoadFromSettings()
    {
        _loading = true;
        try
        {
            SelectComboItem(_cbImageQuality, QualityItems, _settings.ImageQuality);
            SelectComboItem(_cbColorMode, ColorItems, _settings.ColorMode);
            SelectComboItem(_cbScanSide, ScanSideItems, _settings.ScanSide);
            _chkContinueScanning.Checked = _settings.ContinueScanning;
        }
        finally
        {
            _loading = false;
        }
    }

    public ScanModeTabContent(ScanSettings settings)
    {
        _settings = settings;

        _cbImageQuality = CreateIconComboBox(QualityItems);
        _cbImageQuality.SelectedIndexChanged += (s, e) =>
        {
            if (_loading) return;
            if (_cbImageQuality.SelectedIndex >= 0 && _cbImageQuality.SelectedIndex < QualityItems.Count)
                _settings.ImageQuality = (ImageQuality)QualityItems[_cbImageQuality.SelectedIndex].Value;
        };

        _cbColorMode = CreateIconComboBox(ColorItems);
        _cbColorMode.SelectedIndexChanged += (s, e) =>
        {
            if (_loading) return;
            if (_cbColorMode.SelectedIndex >= 0 && _cbColorMode.SelectedIndex < ColorItems.Count)
            {
                _settings.ColorMode = (ColorMode)ColorItems[_cbColorMode.SelectedIndex].Value;
                ColorModeChanged?.Invoke(_settings.ColorMode);
            }
        };

        _cbScanSide = CreateIconComboBox(ScanSideItems);
        _cbScanSide.SelectedIndexChanged += (s, e) =>
        {
            if (_loading) return;
            if (_cbScanSide.SelectedIndex >= 0 && _cbScanSide.SelectedIndex < ScanSideItems.Count)
            {
                _settings.ScanSide = (ScanSide)ScanSideItems[_cbScanSide.SelectedIndex].Value;
                ScanSideChanged?.Invoke(_settings.ScanSide);
            }
        };

        _chkContinueScanning = new ToggleSwitch();
        _chkContinueScanning.CheckedChanged += (s, e) =>
        {
            if (!_loading)
                _settings.ContinueScanning = _chkContinueScanning.Checked;
        };

        _btnOptions = new Button
        {
            Text = "Option...",
            Size = new Size(130, 32)
        };
        ThemeManager.SetGlyph(_btnOptions, Glyph.Gear);
        _btnOptions.Click += (s, e) => OpenOptionsDialog();

        var cardMode = new SettingsCard();
        cardMode.AddRow("Bildqualität", "Eine höhere Auflösung ergibt schärfere Bilder und größere Dateien.", _cbImageQuality, ComboWidth);
        cardMode.AddRow("Farbmodus", "Farbe, Graustufen oder Schwarzweiß.", _cbColorMode, ComboWidth);
        cardMode.AddRow("Scan-Seite",
            "Die automatische Papiergrößenerkennung steht für das Flachbettscannen nicht zur Verfügung.",
            _cbScanSide, ComboWidth);

        var cardOptions = new SettingsCard("Weitere Einstellungen");
        cardOptions.AddRow("Scanvorgang nach aktuellem Scan fortsetzen",
            "Nach dem Scan fragen, ob weitere Dokumente an diesen Scan angehängt werden sollen.", _chkContinueScanning);
        cardOptions.AddRow("Optionen für den Scanmodus",
            "Helligkeit, Textdokumente, leere Seiten, Schräglauf, Bilddrehung und Einlegerichtung.", _btnOptions);

        Controls.Add(cardMode);
        Controls.Add(cardOptions);
        LoadFromSettings();
    }

    private const int ComboWidth = 330;

    private static ComboBox CreateIconComboBox(List<ComboItem> items)
    {
        var cb = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Standard,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 18,
            IntegralHeight = true,
            Size = new Size(ComboWidth, 24),
            DropDownWidth = 400
        };

        foreach (var item in items)
            cb.Items.Add(item);

        // Shared draw handler
        cb.DrawItem += (s, e) => DrawComboItem(s, e, items);

        return cb;
    }

    private static void DrawComboItem(object? sender, DrawItemEventArgs e, List<ComboItem> items)
    {
        var cb = (ComboBox)sender!;
        Color fg = ThemeManager.DrawItemBackground(cb, e);
        if (e.Index < 0) return;
        var item = items[e.Index];

        // Icon
        if (item.Icon != null)
        {
            int iconY = e.Bounds.Y + (e.Bounds.Height - 16) / 2;
            e.Graphics.DrawImage(item.Icon, e.Bounds.X + 4, iconY, 16, 16);
        }

        // Text
        var textRect = new Rectangle(e.Bounds.X + 26, e.Bounds.Y, e.Bounds.Width - 29, e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, item.Text, cb.Font, textRect, fg,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
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

        if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
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
