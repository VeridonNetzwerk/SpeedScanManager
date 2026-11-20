using System.Drawing;
using System.Runtime.InteropServices;
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

    // Cached Windows file type icons for PDF, JPEG, PNG
    private static readonly Image?[] FileTypeIcons = LoadFileTypeIcons();

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
        ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_SMALLICON = 0x000000001;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private static Image?[] LoadFileTypeIcons()
    {
        var extensions = new[] { ".pdf", ".jpg", ".png" };
        var icons = new Image?[extensions.Length];
        for (int i = 0; i < extensions.Length; i++)
        {
            try
            {
                var shfi = new SHFILEINFO();
                var hImg = SHGetFileInfo(extensions[i], 0, ref shfi,
                    (uint)Marshal.SizeOf<SHFILEINFO>(),
                    SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES);
                if (shfi.hIcon != IntPtr.Zero)
                {
                    var icon = Icon.FromHandle(shfi.hIcon);
                    icons[i] = icon.ToBitmap();
                    DestroyIcon(shfi.hIcon);
                }
            }
            catch { }
        }
        return icons;
    }

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
            Size = new Size(300, 24),
            DrawMode = DrawMode.OwnerDrawFixed
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
        _btnOptions = new Button
        {
            Text = "Option...",
            Size = new Size(130, 32)
        };
        ThemeManager.SetGlyph(_btnOptions, Glyph.Gear);
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
            Size = new Size(260, 24),
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 18
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
        var cardFormat = new SettingsCard();
        _rowFileFormat = cardFormat.AddRow("Dateiformat",
            "JPEG steht nicht zur Verfügung, wenn \"Schwarzweiß\" unter [Farbmodus] gewählt wurde.",
            _cbFileFormat, 300);
        cardFormat.AddRow("PDF-Optionen", "PDF-Seiten teilen und Kennwort für die PDF-Datei (nur PDF).", _btnOptions);

        var cardChoose = new SettingsCard("Texterkennung wählen");
        cardChoose.AddRow("Markierten Text als Schlüsselwort der PDF-Datei hinzufügen",
            "Mit Textmarker hervorgehobene Textstellen werden als Schlüsselwörter in die PDF-Eigenschaften eingetragen.", _chkAddKeyword);
        _lblKeywordTarget = cardChoose.AddRow("Zielmarkierung", null, RadioGroup(_rbFirstSection, _rbAllSections));
        cardChoose.AddRow("In durchsuchbare PDF konvertieren",
            "Texterkennung (OCR): Der erkannte Text wird als unsichtbare Schicht über das Bild gelegt.", _chkOcr);

        _grpOcrOptions = new SettingsCard("Texterkennungsoptionen");
        _lblOcrLanguage = _grpOcrOptions.AddRow("Sprache", "Sprache des Dokuments für die Texterkennung.", _cbOcrLanguage, 260);
        _lblOcrTarget = _grpOcrOptions.AddRow("Zielseiten", null, RadioGroup(_rbFirstPage, _rbAllPages));

        Controls.Add(cardFormat);
        Controls.Add(cardChoose);
        Controls.Add(_grpOcrOptions);

        LoadFromSettings();
    }

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

        // Draw file type icon before text
        var icon = e.Index >= 0 && e.Index < FileTypeIcons.Length ? FileTypeIcons[e.Index] : null;
        var textBounds = e.Bounds;
        if (icon != null)
        {
            var iconRect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y + (e.Bounds.Height - 16) / 2, 16, 16);
            using var ia = new System.Drawing.Imaging.ImageAttributes();
            if (disabled)
            {
                ia.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = 0.4f });
            }
            e.Graphics.DrawImage(icon,
                new Rectangle(iconRect.X, iconRect.Y, iconRect.Width, iconRect.Height),
                0, 0, icon.Width, icon.Height,
                GraphicsUnit.Pixel, ia);
            textBounds = new Rectangle(iconRect.Right + 6, e.Bounds.Y, e.Bounds.Right - iconRect.Right - 6, e.Bounds.Height);
        }

        TextRenderer.DrawText(e.Graphics, text, _cbFileFormat.Font, textBounds, color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
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

    private static readonly Image?[] OcrLanguageFlags = LoadOcrLanguageFlags();

    private static Image?[] LoadOcrLanguageFlags()
    {
        return new Image?[]
        {
            FlagIcons.Globe,
            FlagIcons.Germany,
            FlagIcons.Japan,
            FlagIcons.UK,
            FlagIcons.France,
            FlagIcons.Italy,
            FlagIcons.Spain,
            FlagIcons.ChinaSimplified,
            FlagIcons.ChinaTraditional,
            FlagIcons.Korea,
            FlagIcons.Russia,
            FlagIcons.Portugal,
            FlagIcons.Arabic
        };
    }

    private void DrawOcrLanguageItem(DrawItemEventArgs e)
    {
        var color = ThemeManager.DrawItemBackground(_cbOcrLanguage, e);

        if (e.Index < 0) return;

        var entry = _cbOcrLanguage.Items[e.Index];
        string text = entry?.ToString() ?? "";
        bool enabled = _cbOcrLanguage.Enabled;

        int flagIndex = entry is OcrLanguageItem item ? (int)item.Language : -1;
        var flag = flagIndex >= 0 && flagIndex < OcrLanguageFlags.Length ? OcrLanguageFlags[flagIndex] : null;
        var textBounds = e.Bounds;
        if (flag != null)
        {
            var iconRect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y + (e.Bounds.Height - 16) / 2, 16, 16);
            if (!enabled)
            {
                using var ia = new System.Drawing.Imaging.ImageAttributes();
                ia.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = 0.4f });
                e.Graphics.DrawImage(flag,
                    new Rectangle(iconRect.X, iconRect.Y, iconRect.Width, iconRect.Height),
                    0, 0, flag.Width, flag.Height,
                    GraphicsUnit.Pixel, ia);
            }
            else
            {
                e.Graphics.DrawImage(flag, iconRect);
            }
            textBounds = new Rectangle(iconRect.Right + 6, e.Bounds.Y, e.Bounds.Right - iconRect.Right - 6, e.Bounds.Height);
        }

        TextRenderer.DrawText(e.Graphics, text, _cbOcrLanguage.Font, textBounds, color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
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
