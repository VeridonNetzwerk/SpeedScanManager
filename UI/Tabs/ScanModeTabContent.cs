using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Combo item with display text, internal enum value, an optional colorful icon and an optional
/// muted detail (shown as a second line in the drop-down list only, e.g. the resolution).
/// </summary>
internal sealed class ComboItem
{
    /// <summary>Full text (name and detail; used for accessibility and ToString).</summary>
    public string Text { get; }
    /// <summary>Short name shown in the closed combo box.</summary>
    public string Name { get; }
    /// <summary>Muted detail shown under the name in the drop-down list.</summary>
    public string Detail { get; }
    public object Value { get; }
    public ColorIcon? Icon { get; }

    public ComboItem(string name, object value, ColorIcon? icon = null, string? detail = null)
    {
        Name = name;
        Detail = detail ?? "";
        Text = Detail.Length > 0 ? $"{name} ({Detail})" : name;
        Value = value;
        Icon = icon;
    }

    public override string ToString() => Text;
}

/// <summary>Owner-draw helper of the combo boxes with icons and optional detail lines.</summary>
internal static class ComboItemPainter
{
    /// <summary>
    /// Sets up <paramref name="cb"/> for <see cref="Draw"/>: list rows with icons, two-line
    /// rows for items with a detail (<paramref name="hasDetail"/> per index).
    /// </summary>
    public static void Setup(ComboBox cb, Func<int, bool> hasDetail)
        => ComboLists.SetRowHeight(cb, i => hasDetail(i) ? ComboLists.TwoLineRow : ComboLists.IconRow);

    /// <summary>
    /// Draws a <see cref="ComboItem"/>: colorful icon, name and, in the drop-down list, the
    /// muted detail as a second line. <paramref name="enabled"/> false grays the item.
    /// </summary>
    public static void Draw(ComboBox cb, DrawItemEventArgs e, ComboItem? item, bool enabled = true)
    {
        if (item == null)
        {
            ThemeManager.DrawItemBackground(cb, e, enabled);
            return;
        }
        Draw(cb, e, item.Icon, item.Name, item.Detail, enabled);
    }

    /// <summary>
    /// Draws one row with the detail as a muted right-aligned column (in the box and in the
    /// list), e.g. paper sizes: "A4" ... "210 x 297 mm".
    /// </summary>
    public static void DrawInline(ComboBox cb, DrawItemEventArgs e, ColorIcon? icon, string name, string? detail, bool enabled = true)
    {
        Color fg = ThemeManager.DrawItemBackground(cb, e, enabled);
        var p = ThemeManager.Current;
        bool edit = (e.State & DrawItemState.ComboBoxEdit) != 0;
        bool active = cb.Enabled && enabled;
        int S(float v) => ThemeManager.Scale(cb, v);
        int x = e.Bounds.X + S(edit ? 6 : 10);
        if (icon != null)
        {
            int size = S(edit ? 18 : 20);
            icon.Draw(e.Graphics, new RectangleF(x, e.Bounds.Y + (e.Bounds.Height - size) / 2f, size, size), active);
            x += size + S(edit ? 8 : 10);
        }
        var flags = TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine
                    | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter;
        int right = e.Bounds.Right - S(8);
        int detailW = 0;
        if (!string.IsNullOrEmpty(detail))
        {
            detailW = Math.Min(TextRenderer.MeasureText(e.Graphics, detail, ModernFonts.Small, Size.Empty,
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width + 1,
                Math.Max(0, (right - x) * 2 / 3));
            TextRenderer.DrawText(e.Graphics, detail, ModernFonts.Small, new Rectangle(right - detailW, e.Bounds.Y, detailW, e.Bounds.Height),
                active ? p.TextMuted : fg, flags);
            detailW += S(10);
        }
        TextRenderer.DrawText(e.Graphics, name, cb.Font, new Rectangle(x, e.Bounds.Y, Math.Max(0, right - detailW - x), e.Bounds.Height), fg, flags);
    }

    /// <summary>Draws one row: <paramref name="icon"/>, <paramref name="name"/> and (list only) <paramref name="detail"/>.</summary>
    public static void Draw(ComboBox cb, DrawItemEventArgs e, ColorIcon? icon, string name, string? detail, bool enabled = true,
        string? hint = null)
    {
        Color fg = ThemeManager.DrawItemBackground(cb, e, enabled);
        var p = ThemeManager.Current;
        bool edit = (e.State & DrawItemState.ComboBoxEdit) != 0;
        bool active = cb.Enabled && enabled;
        int S(float v) => ThemeManager.Scale(cb, v);
        int x = e.Bounds.X + S(edit ? 6 : 10);

        if (icon != null)
        {
            int size = S(edit ? 18 : 20);
            icon.Draw(e.Graphics, new RectangleF(x, e.Bounds.Y + (e.Bounds.Height - size) / 2f, size, size), active);
            x += size + S(edit ? 8 : 10);
        }

        var flags = TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine
                    | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
        int right = e.Bounds.Right - S(8);
        var muted = active ? p.TextMuted : fg;
        bool twoLine = !edit && !string.IsNullOrEmpty(detail);
        if (twoLine)
        {
            int nameH = TextRenderer.MeasureText(e.Graphics, "Ag", cb.Font, Size.Empty, flags).Height;
            int detailH = TextRenderer.MeasureText(e.Graphics, "Ag", ModernFonts.Small, Size.Empty, flags).Height;
            int top = e.Bounds.Y + (e.Bounds.Height - nameH - detailH - S(1)) / 2;
            TextRenderer.DrawText(e.Graphics, name, cb.Font, new Rectangle(x, top, Math.Max(0, right - x), nameH), fg, flags);
            TextRenderer.DrawText(e.Graphics, detail, ModernFonts.Small, new Rectangle(x, top + nameH + S(1), Math.Max(0, right - x), detailH), muted, flags);
        }
        else
        {
            var rect = new Rectangle(x, e.Bounds.Y, Math.Max(0, right - x), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, name, cb.Font, rect, fg, flags | TextFormatFlags.VerticalCenter);
            if (!edit && !string.IsNullOrEmpty(hint))
            {
                int nameW = TextRenderer.MeasureText(e.Graphics, name, cb.Font, rect.Size, flags).Width;
                var hintRect = new Rectangle(x + nameW + S(8), e.Bounds.Y, Math.Max(0, right - x - nameW - S(8)), e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, hint, ModernFonts.Small, hintRect, p.TextMuted, flags | TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
            }
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
            // Level meters on tiles that get "hotter" as the resolution grows.
            new(L.T("Automatisch"), ImageQuality.Automatic, ColorIcon.Auto),
            new(L.T("Normal"), ImageQuality.Normal, ColorIcon.Tile(Glyph.Level1, IconColors.Teal), L.T("Farbe/Grau: 150 dpi, S&W: 300 dpi")),
            new(L.T("Fein"), ImageQuality.Fine, ColorIcon.Tile(Glyph.Level2, IconColors.Sky), L.T("Farbe/Grau: 200 dpi, S&W: 400 dpi")),
            new(L.T("Beste"), ImageQuality.Best, ColorIcon.Tile(Glyph.Level3, IconColors.Blue), L.T("Farbe/Grau: 300 dpi, S&W: 600 dpi")),
            new(L.T("Hervorragend"), ImageQuality.Excellent, ColorIcon.Tile(Glyph.Level4, IconColors.Indigo), L.T("Farbe/Grau: 600 dpi, S&W: 1200 dpi"))
        };

        ColorItems = new List<ComboItem>
        {
            new(L.T("Automatische Farberkennung"), ColorMode.Automatic, ColorIcon.Auto),
            new(L.T("Farbe"), ColorMode.Color, ColorIcon.Gradient(Glyph.Palette, IconColors.Orange, IconColors.Pink)),
            new(L.T("Grau"), ColorMode.Grayscale, ColorIcon.Tile(Glyph.Grayscale, IconColors.Slate)),
            new(L.T("Schwarzweiß"), ColorMode.BlackWhite, ColorIcon.Tile(Glyph.Contrast, IconColors.Graphite))
        };

        ScanSideItems = new List<ComboItem>
        {
            new(L.T("Duplex-Scan (doppelseitig)"), ScanSide.Duplex, ColorIcon.Tile(Glyph.PageDuplex, IconColors.Blue)),
            new(L.T("Simplex-Scan (einseitig)"), ScanSide.Simplex, ColorIcon.Tile(Glyph.PageSingle, IconColors.Teal)),
            new(L.T("Flachbettscannen"), ScanSide.Flatbed, ColorIcon.Tile(Glyph.Scanner, IconColors.Orange)),
            new(L.T("Automatisch"), ScanSide.Automatic, ColorIcon.Auto)
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

        _btnOptions = ModernButtons.SubDialog(L.T("Optionen..."), Glyph.Sliders, IconColors.Blue);
        _btnOptions.Click += (s, e) => OpenOptionsDialog();

        var cardMode = new SettingsCard { Dense = true };
        cardMode.AddRow(L.T("Bildqualität"), L.T("Höhere Auflösung: schärfere Bilder, größere Dateien."), _cbImageQuality, ComboWidth);
        cardMode.AddRow(L.T("Farbmodus"), L.T("Farbe, Graustufen oder Schwarzweiß."), _cbColorMode, ComboWidth);
        cardMode.AddRow(L.T("Scan-Seite"),
            L.T("Flachbettscannen ohne automatische Papiergrößenerkennung."),
            _cbScanSide, ComboWidth);

        var cardOptions = new SettingsCard(L.T("Weitere Einstellungen")) { Dense = true, Icon = ColorIcon.Tile(Glyph.Sliders, IconColors.Blue) };
        cardOptions.AddRow(L.T("Scanvorgang nach aktuellem Scan fortsetzen"),
            L.T("Nach dem Scan fragen, ob weitere Dokumente angehängt werden sollen."), _chkContinueScanning);
        cardOptions.AddRow(L.T("Optionen für den Scanmodus"),
            L.T("Helligkeit, Textdokumente, leere Seiten, Schräglauf, Drehung, Einlegerichtung."), _btnOptions);

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
            Size = new Size(ComboWidth, 24)
        };
        ComboItemPainter.Setup(cb, i => i >= 0 && i < items.Count && items[i].Detail.Length > 0);

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
        var dialog = new ScanModeOptionsDialog(_settings.ColorMode)
        {
            Brightness = _settings.Brightness,
            TextOnlySettings = _settings.TextOnlySettings,
            AllowDeleteBlankPages = _settings.AllowDeleteBlankPages,
            AllowDeskew = _settings.AllowDeskew,
            AllowAutoRotate = _settings.AllowAutoRotate,
            FaceUpFeeding = _settings.FaceUpFeeding
        };

        OwnedWindows.Show(dialog, FindForm(), key: "edit:ScanModeOptionsDialog", onOk: () =>
        {
            _settings.Brightness = dialog.Brightness;
            _settings.TextOnlySettings = dialog.TextOnlySettings;
            _settings.AllowDeleteBlankPages = dialog.AllowDeleteBlankPages;
            _settings.AllowDeskew = dialog.AllowDeskew;
            _settings.AllowAutoRotate = dialog.AllowAutoRotate;
            _settings.FaceUpFeeding = dialog.FaceUpFeeding;
        });
    }
}
