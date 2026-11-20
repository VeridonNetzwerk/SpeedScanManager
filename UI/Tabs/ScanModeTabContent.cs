using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Combo item with display text, internal enum value, an optional line glyph and an optional
/// muted detail (shown in the drop-down list only, e.g. the resolution).
/// </summary>
internal sealed class ComboItem
{
    /// <summary>Full text (name and detail; used for accessibility and ToString).</summary>
    public string Text { get; }
    /// <summary>Short name shown in the closed combo box.</summary>
    public string Name { get; }
    /// <summary>Muted detail shown after the name in the drop-down list.</summary>
    public string Detail { get; }
    public object Value { get; }
    public Glyph Glyph { get; }

    public ComboItem(string name, object value, Glyph glyph = Glyph.None, string? detail = null)
    {
        Name = name;
        Detail = detail ?? "";
        Text = Detail.Length > 0 ? $"{name} ({Detail})" : name;
        Value = value;
        Glyph = glyph;
    }

    public override string ToString() => Text;
}

/// <summary>Owner-draw helper of the combo boxes with <see cref="ComboItem"/>s.</summary>
internal static class ComboItemPainter
{
    /// <summary>
    /// Draws a <see cref="ComboItem"/>: glyph (theme ink), name and, in the drop-down list,
    /// the muted detail. <paramref name="enabled"/> false grays the item.
    /// </summary>
    public static void Draw(ComboBox cb, DrawItemEventArgs e, ComboItem? item, bool enabled = true)
    {
        Color fg = ThemeManager.DrawItemBackground(cb, e, enabled);
        if (item == null) return;
        var p = ThemeManager.Current;
        bool edit = (e.State & DrawItemState.ComboBoxEdit) != 0;
        bool active = cb.Enabled && enabled;
        int x = e.Bounds.X + ThemeManager.Scale(cb, 6);

        if (item.Glyph != Glyph.None)
        {
            int size = ThemeManager.Scale(cb, 16);
            var glyphColor = active ? (edit ? p.TextSecondary : p.TextMuted) : fg;
            ThemeGlyphs.Draw(e.Graphics, item.Glyph,
                new RectangleF(x, e.Bounds.Y + (e.Bounds.Height - size) / 2f, size, size), glyphColor);
            x += size + ThemeManager.Scale(cb, 8);
        }

        var flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix
                    | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
        var rect = new Rectangle(x, e.Bounds.Y, Math.Max(0, e.Bounds.Right - x - 4), e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, item.Name, cb.Font, rect, fg, flags);
        if (!edit && item.Detail.Length > 0)
        {
            // Detail right-aligned in its own column
            int nameW = TextRenderer.MeasureText(e.Graphics, item.Name, cb.Font, rect.Size, flags).Width;
            int pad = ThemeManager.Scale(cb, 8);
            var detailRect = new Rectangle(rect.X + nameW + pad, rect.Y, Math.Max(0, e.Bounds.Right - pad - rect.X - nameW - pad), rect.Height);
            var muted = active ? p.TextMuted : fg;
            TextRenderer.DrawText(e.Graphics, item.Detail, ModernFonts.Small, detailRect, muted, flags | TextFormatFlags.Right);
        }
    }
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
            // Level meter glyphs: the resolution grows from Normal to Hervorragend.
            new("Automatisch", ImageQuality.Automatic, Glyph.Wand),
            new("Normal", ImageQuality.Normal, Glyph.Level1, "Farbe/Grau: 150 dpi, S&W: 300 dpi"),
            new("Fein", ImageQuality.Fine, Glyph.Level2, "Farbe/Grau: 200 dpi, S&W: 400 dpi"),
            new("Beste", ImageQuality.Best, Glyph.Level3, "Farbe/Grau: 300 dpi, S&W: 600 dpi"),
            new("Hervorragend", ImageQuality.Excellent, Glyph.Level4, "Farbe/Grau: 600 dpi, S&W: 1200 dpi")
        };

        ColorItems = new List<ComboItem>
        {
            new("Automatische Farberkennung", ColorMode.Automatic, Glyph.Wand),
            new("Farbe", ColorMode.Color, Glyph.Palette),
            new("Grau", ColorMode.Grayscale, Glyph.Grayscale),
            new("Schwarzweiß", ColorMode.BlackWhite, Glyph.Contrast)
        };

        ScanSideItems = new List<ComboItem>
        {
            new("Duplex-Scan (doppelseitig)", ScanSide.Duplex, Glyph.PageDuplex),
            new("Simplex-Scan (einseitig)", ScanSide.Simplex, Glyph.PageSingle),
            new("Flachbettscannen", ScanSide.Flatbed, Glyph.Scanner),
            new("Automatisch", ScanSide.Automatic, Glyph.Wand)
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

        _btnOptions = ModernButtons.SubDialog("Optionen...");
        _btnOptions.Click += (s, e) => OpenOptionsDialog();

        var cardMode = new SettingsCard { Dense = true };
        cardMode.AddRow("Bildqualität", "Höhere Auflösung: schärfere Bilder, größere Dateien.", _cbImageQuality, ComboWidth);
        cardMode.AddRow("Farbmodus", "Farbe, Graustufen oder Schwarzweiß.", _cbColorMode, ComboWidth);
        cardMode.AddRow("Scan-Seite",
            "Flachbettscannen ohne automatische Papiergrößenerkennung.",
            _cbScanSide, ComboWidth);

        var cardOptions = new SettingsCard("Weitere Einstellungen") { Dense = true };
        cardOptions.AddRow("Scanvorgang nach aktuellem Scan fortsetzen",
            "Nach dem Scan fragen, ob weitere Dokumente angehängt werden sollen.", _chkContinueScanning);
        cardOptions.AddRow("Optionen für den Scanmodus",
            "Helligkeit, Textdokumente, leere Seiten, Schräglauf, Drehung, Einlegerichtung.", _btnOptions);

        Controls.Add(cardMode);
        Controls.Add(cardOptions);
        LoadFromSettings();
    }

    /// <summary>Width of all combo boxes on this page (fits the longest short name).</summary>
    internal const int ComboWidth = 250;

    private static ComboBox CreateIconComboBox(List<ComboItem> items)
    {
        var cb = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Standard,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 20,
            IntegralHeight = true,
            Size = new Size(ComboWidth, 24),
            DropDownWidth = items.Any(i => i.Detail.Length > 0) ? 380 : ComboWidth
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
        ComboItemPainter.Draw(cb, e, e.Index >= 0 && e.Index < items.Count ? items[e.Index] : null);
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
