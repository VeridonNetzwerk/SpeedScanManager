using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Content of the "Dateiart" settings page: file format (with the JPEG hint), PDF options,
/// keyword marking and OCR (searchable PDF) with its options, grouped in settings cards.
/// </summary>
internal class FileTypeTabContent : ModernStack
{
    private readonly ComboBox _cbFileFormat;
    private readonly Button _btnOptions;
    private readonly ScanSettings _settings;
    private bool _jpegAvailable = true;
    private bool _loading;
    private readonly SettingRow _rowFileFormat;

    /// <summary>Raised when the user picks another file format.</summary>
    public event Action<FileFormat>? FileFormatChanged;

    // Keyword marking controls
    private readonly CheckBox _chkAddKeyword;
    private readonly SettingRow _lblKeywordTarget;
    private readonly RadioButton _rbFirstSection;
    private readonly RadioButton _rbAllSections;

    // OCR controls
    private readonly CheckBox _chkOcr;
    private readonly ComboBox _cbOcrLanguage;
    private readonly RadioButton _rbFirstPage;
    private readonly RadioButton _rbAllPages;
    private readonly SettingRow _lblOcrLanguage;
    private readonly SettingRow _lblOcrTarget;

    // Card of the OCR options (enabled for PDF only)
    private readonly SettingsCard _grpOcrOptions;

    public FileTypeTabContent(ScanSettings settings)
    {
        _settings = settings;

        // === File format dropdown ===
        _cbFileFormat = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Size = new Size(ComboWidth, 24),
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 20
        };
        _cbFileFormat.Items.AddRange(new object[] { "PDF (*.pdf)", "JPEG (*.jpg)", "PNG (*.png)" });
        _cbFileFormat.DrawItem += (s, e) => DrawFileFormatItem(e);
        _cbFileFormat.SelectedIndexChanged += (s, e) =>
        {
            if (_loading || _cbFileFormat.SelectedIndex < 0) return;
            var fmt = (FileFormat)_cbFileFormat.SelectedIndex;
            if (fmt == FileFormat.Jpeg && !_jpegAvailable)
            {
                _cbFileFormat.SelectedIndex = (int)_settings.FileFormat;
                return;
            }
            _settings.FileFormat = fmt;
            UpdateOptionsButtonState();
            UpdateOcrControlsVisibility();
            FileFormatChanged?.Invoke(fmt);
        };

        // === PDF options button ===
        _btnOptions = ModernButtons.SubDialog("Optionen...");
        _btnOptions.Click += (s, e) => OpenPdfOptionsDialog();

        // === Keyword marking ===
        _chkAddKeyword = new ToggleSwitch();
        _chkAddKeyword.CheckedChanged += (s, e) =>
        {
            if (!_loading)
                _settings.AddKeywordToPdf = _chkAddKeyword.Checked;
            UpdateKeywordControlsEnabled();
        };

        _rbFirstSection = new RadioButton
        {
            Text = "Erste markierte Sektion",
            AutoSize = true,
            Margin = new Padding(0, 0, 14, 0)
        };
        _rbFirstSection.CheckedChanged += (s, e) =>
        {
            if (!_loading && _rbFirstSection.Checked)
                _settings.KeywordTarget = KeywordTarget.FirstMarkedSection;
        };

        _rbAllSections = new RadioButton
        {
            Text = "Alle markierten Sektionen",
            AutoSize = true,
            Margin = Padding.Empty
        };
        _rbAllSections.CheckedChanged += (s, e) =>
        {
            if (!_loading && _rbAllSections.Checked)
                _settings.KeywordTarget = KeywordTarget.AllMarkedSections;
        };

        // === OCR ===
        _chkOcr = new ToggleSwitch();
        _chkOcr.CheckedChanged += (s, e) =>
        {
            if (!_loading)
                _settings.OcrEnabled = _chkOcr.Checked;
            UpdateOcrSubControlsEnabled();
        };

        _cbOcrLanguage = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Size = new Size(ComboWidth, 24),
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 20,
            DropDownWidth = 300
        };
        // Items (installed languages only) are filled in LoadFromSettings.
        _cbOcrLanguage.DrawItem += (s, e) => DrawOcrLanguageItem(e);
        _cbOcrLanguage.SelectedIndexChanged += (s, e) =>
        {
            if (!_loading && _cbOcrLanguage.SelectedItem is OcrLanguageItem item)
                _settings.OcrLanguage = item.Language;
        };

        _rbFirstPage = new RadioButton
        {
            Text = "Erste Seite",
            AutoSize = true,
            Margin = new Padding(0, 0, 14, 0)
        };
        _rbFirstPage.CheckedChanged += (s, e) =>
        {
            if (!_loading && _rbFirstPage.Checked)
                _settings.OcrTargetPages = OcrTargetPages.FirstPage;
        };

        _rbAllPages = new RadioButton
        {
            Text = "Alle Seiten",
            AutoSize = true,
            Margin = Padding.Empty
        };
        _rbAllPages.CheckedChanged += (s, e) =>
        {
            if (!_loading && _rbAllPages.Checked)
                _settings.OcrTargetPages = OcrTargetPages.AllPages;
        };

        // === Cards ===
        var cardFormat = new SettingsCard { Dense = true };
        _rowFileFormat = cardFormat.AddRow("Dateiformat",
            "JPEG steht bei Farbmodus „Schwarzweiß“ nicht zur Verfügung.",
            _cbFileFormat, ComboWidth);
        cardFormat.AddRow("PDF-Optionen", "PDF-Seiten teilen und Kennwort (nur PDF).", _btnOptions);

        var cardChoose = new SettingsCard("Texterkennung wählen") { Dense = true };
        cardChoose.AddRow("Markierten Text als Schlüsselwort der PDF-Datei hinzufügen",
            "Mit Textmarker hervorgehobene Stellen werden Schlüsselwörter der PDF-Datei.", _chkAddKeyword);
        _lblKeywordTarget = cardChoose.AddRow("Zielmarkierung", null, RadioGroup(_rbFirstSection, _rbAllSections));
        cardChoose.AddRow("In durchsuchbare PDF konvertieren",
            "Der erkannte Text (OCR) wird unsichtbar über das Bild gelegt.", _chkOcr);

        _grpOcrOptions = new SettingsCard("Texterkennungsoptionen") { Dense = true };
        _lblOcrLanguage = _grpOcrOptions.AddRow("Sprache", null, _cbOcrLanguage, ComboWidth);
        _lblOcrTarget = _grpOcrOptions.AddRow("Zielseiten", null, RadioGroup(_rbFirstPage, _rbAllPages));

        Controls.Add(cardFormat);
        Controls.Add(cardChoose);
        Controls.Add(_grpOcrOptions);

        LoadFromSettings();
    }

    /// <summary>Width of the combo boxes on this page (96-dpi units).</summary>
    private const int ComboWidth = 220;

    /// <summary>Horizontal group of radio buttons (one group per container).</summary>
    private static FlowLayoutPanel RadioGroup(params RadioButton[] buttons)
    {
        var flow = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        flow.Controls.AddRange(buttons);
        return flow;
    }

    /// <summary>
    /// Refreshes all controls from the settings without raising change notifications.
    /// </summary>
    public void LoadFromSettings()
    {
        _loading = true;
        try
        {
            _jpegAvailable = _settings.ColorMode != ColorMode.BlackWhite;
            _cbFileFormat.SelectedIndex = Math.Clamp((int)_settings.FileFormat, 0, _cbFileFormat.Items.Count - 1);
            _chkAddKeyword.Checked = _settings.AddKeywordToPdf;
            _rbFirstSection.Checked = _settings.KeywordTarget == KeywordTarget.FirstMarkedSection;
            _rbAllSections.Checked = _settings.KeywordTarget == KeywordTarget.AllMarkedSections;
            _chkOcr.Checked = _settings.OcrEnabled;
            PopulateOcrLanguages(_settings.OcrLanguage);
            _rbFirstPage.Checked = _settings.OcrTargetPages == OcrTargetPages.FirstPage;
            _rbAllPages.Checked = _settings.OcrTargetPages == OcrTargetPages.AllPages;
        }
        finally
        {
            _loading = false;
        }
        UpdateJpegAvailability();
        UpdateOptionsButtonState();
        UpdateOcrControlsVisibility();
    }

    /// <summary>
    /// Called by MainForm when the color mode in the Scanmodus tab changes.
    /// JPEG is not available for black and white output (ScanSnap behaviour);
    /// with automatic color detection JPEG is allowed.
    /// </summary>
    public void OnColorModeChanged(ColorMode newMode)
    {
        UpdateJpegAvailability(newMode);
    }

    private void UpdateJpegAvailability(ColorMode? mode = null)
    {
        ColorMode cm = mode ?? _settings.ColorMode;
        _jpegAvailable = cm != ColorMode.BlackWhite;
        _rowFileFormat.DescriptionRole = _jpegAvailable ? ThemeRole.Muted : ThemeRole.Danger;

        if (!_jpegAvailable && _cbFileFormat.SelectedIndex == (int)FileFormat.Jpeg)
        {
            _loading = true;
            try { _cbFileFormat.SelectedIndex = (int)FileFormat.Pdf; }
            finally { _loading = false; }
            _settings.FileFormat = FileFormat.Pdf;
            UpdateOptionsButtonState();
            UpdateOcrControlsVisibility();
            FileFormatChanged?.Invoke(FileFormat.Pdf);
        }
        _cbFileFormat.Invalidate();
    }

    private void DrawFileFormatItem(DrawItemEventArgs e)
    {
        if (e.Index < 0)
        {
            ThemeManager.DrawItemBackground(_cbFileFormat, e);
            return;
        }

        string text = _cbFileFormat.Items[e.Index]!.ToString()!;
        bool isJpeg = e.Index == (int)FileFormat.Jpeg;
        bool disabled = isJpeg && !_jpegAvailable;

        var color = ThemeManager.DrawItemBackground(_cbFileFormat, e, itemEnabled: !disabled);
        var textBounds = new Rectangle(e.Bounds.X + ThemeManager.Scale(_cbFileFormat, 6), e.Bounds.Y,
            Math.Max(0, e.Bounds.Width - ThemeManager.Scale(_cbFileFormat, 8)), e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, text, _cbFileFormat.Font, textBounds, color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        if (disabled && (e.State & DrawItemState.ComboBoxEdit) == 0)
        {
            int w = TextRenderer.MeasureText(e.Graphics, text, _cbFileFormat.Font, textBounds.Size, TextFormatFlags.NoPrefix).Width;
            var hint = new Rectangle(textBounds.X + w, textBounds.Y, Math.Max(0, textBounds.Width - w), textBounds.Height);
            TextRenderer.DrawText(e.Graphics, "nicht bei Schwarzweiß", ModernFonts.Small, hint, ThemeManager.Current.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>Entry of the OCR language list.</summary>
    private sealed record OcrLanguageItem(OcrLanguage Language, string Text)
    {
        public override string ToString() => Text;
    }

    private static string GetOcrLanguageName(OcrLanguage language) => language switch
    {
        OcrLanguage.Automatisch => "Automatische Erkennung",
        OcrLanguage.Deutsch => "Deutsch",
        OcrLanguage.Japanisch => "Japanisch",
        OcrLanguage.English => "Englisch",
        OcrLanguage.Franzoesisch => "Französisch",
        OcrLanguage.Italienisch => "Italienisch",
        OcrLanguage.Spanisch => "Spanisch",
        OcrLanguage.ChinesischVereinfacht => "Chinesisch (vereinfacht)",
        OcrLanguage.ChinesischTraditionell => "Chinesisch (traditionell)",
        OcrLanguage.Koreanisch => "Koreanisch",
        OcrLanguage.Russisch => "Russisch",
        OcrLanguage.Portugiesisch => "Portugiesisch",
        OcrLanguage.Arabisch => "Arabisch",
        _ => language.ToString()
    };

    /// <summary>
    /// Fills the language list with "Automatische Erkennung" and the languages whose
    /// traineddata is installed. A saved language that is not installed stays visible
    /// (marked "(nicht installiert)") so the setting is not changed silently.
    /// </summary>
    private void PopulateOcrLanguages(OcrLanguage selected)
    {
        var installed = OcrProcessor.GetInstalledLanguages();
        _cbOcrLanguage.BeginUpdate();
        try
        {
            _cbOcrLanguage.Items.Clear();
            OcrLanguageItem? selectedItem = null;
            foreach (OcrLanguage language in Enum.GetValues<OcrLanguage>())
            {
                bool available = language == OcrLanguage.Automatisch
                    || OcrProcessor.GetLanguageCode(language).Split('+')
                        .All(c => installed.Contains(c, StringComparer.OrdinalIgnoreCase));
                if (!available && language != selected)
                    continue;

                var item = new OcrLanguageItem(language,
                    available ? GetOcrLanguageName(language) : $"{GetOcrLanguageName(language)} (nicht installiert)");
                _cbOcrLanguage.Items.Add(item);
                if (language == selected) selectedItem = item;
            }
            _cbOcrLanguage.SelectedItem = selectedItem ?? _cbOcrLanguage.Items[0];
        }
        finally
        {
            _cbOcrLanguage.EndUpdate();
        }
    }

    /// <summary>Short language code shown as a badge in front of the language name.</summary>
    private static string GetOcrLanguageCode(OcrLanguage language) => language switch
    {
        OcrLanguage.Deutsch => "DE",
        OcrLanguage.Japanisch => "JA",
        OcrLanguage.English => "EN",
        OcrLanguage.Franzoesisch => "FR",
        OcrLanguage.Italienisch => "IT",
        OcrLanguage.Spanisch => "ES",
        OcrLanguage.ChinesischVereinfacht => "ZH",
        OcrLanguage.ChinesischTraditionell => "ZH",
        OcrLanguage.Koreanisch => "KO",
        OcrLanguage.Russisch => "RU",
        OcrLanguage.Portugiesisch => "PT",
        OcrLanguage.Arabisch => "AR",
        _ => ""
    };

    private static readonly Font BadgeFont = new("Segoe UI Semibold", 7f);

    private void DrawOcrLanguageItem(DrawItemEventArgs e)
    {
        var color = ThemeManager.DrawItemBackground(_cbOcrLanguage, e);
        if (e.Index < 0) return;

        var entry = _cbOcrLanguage.Items[e.Index];
        string text = entry?.ToString() ?? "";
        bool enabled = _cbOcrLanguage.Enabled;
        var p = ThemeManager.Current;
        var g = e.Graphics;
        int Sc(float v) => ThemeManager.Scale(_cbOcrLanguage, v);

        // Badge: language code, or a wand for automatic detection (accent tinted)
        var badge = new RectangleF(e.Bounds.X + Sc(6), e.Bounds.Y + (e.Bounds.Height - Sc(15)) / 2f, Sc(24), Sc(15));
        bool auto = entry is OcrLanguageItem { Language: OcrLanguage.Automatisch };
        var bg = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0 ? p.Selected : _cbOcrLanguage.BackColor;
        Color badgeFill = auto ? ThemePalette.Blend(bg, p.Accent, p.IsDark ? 0.28f : 0.16f) : ThemePalette.Blend(bg, p.TextMuted, p.IsDark ? 0.22f : 0.16f);
        Color badgeText = auto ? (p.IsDark ? ThemePalette.Blend(p.Accent, Color.White, 0.35f) : p.Accent) : p.TextSecondary;
        if (!enabled)
        {
            badgeFill = ThemePalette.Blend(badgeFill, bg, 0.5f);
            badgeText = color;
        }
        var state = g.Save();
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var path = ModernLayout.RoundedRect(badge, Sc(4)))
        using (var brush = new SolidBrush(badgeFill))
            g.FillPath(brush, path);
        g.Restore(state);
        if (auto)
        {
            float gs = Sc(12);
            ThemeGlyphs.Draw(g, Glyph.Wand, new RectangleF(badge.X + (badge.Width - gs) / 2f, badge.Y + (badge.Height - gs) / 2f, gs, gs), badgeText);
        }
        else if (entry is OcrLanguageItem item)
        {
            TextRenderer.DrawText(g, GetOcrLanguageCode(item.Language), BadgeFont, Rectangle.Round(badge), badgeText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }

        int tx = (int)badge.Right + Sc(8);
        var textBounds = new Rectangle(tx, e.Bounds.Y, Math.Max(0, e.Bounds.Right - tx - 2), e.Bounds.Height);
        TextRenderer.DrawText(g, text, _cbOcrLanguage.Font, textBounds, color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
    }

    private void UpdateOptionsButtonState()
    {
        _btnOptions.Enabled = _cbFileFormat.SelectedIndex == (int)FileFormat.Pdf;
    }

    private void UpdateOcrControlsVisibility()
    {
        bool isPdf = _cbFileFormat.SelectedIndex == (int)FileFormat.Pdf;
        _chkOcr.Enabled = isPdf;
        _chkAddKeyword.Enabled = isPdf;
        _grpOcrOptions.Enabled = isPdf;
        UpdateKeywordControlsEnabled();
        UpdateOcrSubControlsEnabled();
    }

    private void UpdateKeywordControlsEnabled()
    {
        bool on = _cbFileFormat.SelectedIndex == (int)FileFormat.Pdf && _chkAddKeyword.Checked;
        _lblKeywordTarget.Enabled = on;
        _rbFirstSection.Enabled = on;
        _rbAllSections.Enabled = on;
    }

    private void UpdateOcrSubControlsEnabled()
    {
        bool isPdf = _cbFileFormat.SelectedIndex == (int)FileFormat.Pdf;
        bool ocrOn = isPdf && _chkOcr.Checked;
        _lblOcrLanguage.Enabled = ocrOn;
        _cbOcrLanguage.Enabled = ocrOn;
        _lblOcrTarget.Enabled = ocrOn;
        _rbFirstPage.Enabled = ocrOn;
        _rbAllPages.Enabled = ocrOn;
    }

    private void OpenPdfOptionsDialog()
    {
        using var dialog = new PdfOptionsDialog
        {
            SplitMode = _settings.PdfSplitMode,
            SplitPages = _settings.PdfSplitPages,
            UsePassword = _settings.PdfUsePassword,
            Password = _settings.PdfPassword
        };

        if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
        {
            _settings.PdfSplitMode = dialog.SplitMode;
            _settings.PdfSplitPages = dialog.SplitPages;
            _settings.PdfUsePassword = dialog.UsePassword;
            _settings.PdfPassword = dialog.Password;
        }
    }
}
