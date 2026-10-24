using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Content panel for the "Dateiart" tab (base, without OCR).
/// Contains file format dropdown, PDF options button, and reserved space for OCR (Phase 11).
/// </summary>
internal class FileTypeTabContent : Panel
{
    private readonly ComboBox _cbFileFormat;
    private readonly Button _btnOptions;
    private readonly ScanSettings _settings;
    private bool _jpegAvailable = true;
    private bool _loading;
    private readonly Label _lblJpegHint;

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
    private readonly Label _lblKeywordTarget;
    private readonly RadioButton _rbFirstSection;
    private readonly RadioButton _rbAllSections;

    // OCR controls
    private readonly CheckBox _chkOcr;
    private readonly ComboBox _cbOcrLanguage;
    private readonly RadioButton _rbFirstPage;
    private readonly RadioButton _rbAllPages;
    private readonly Label _lblOcrLanguage;
    private readonly Label _lblOcrTarget;

    // GroupBoxes for enable/disable
    private readonly GroupBox _grpOcrOptions;

    public FileTypeTabContent(ScanSettings settings)
    {
        _settings = settings;

        var font = UiFonts.Default;
        Dock = DockStyle.Fill;
        BackColor = Color.White;

        // === Info bar ===
        var infoBar = new GradientInfoBar("Geben Sie die gewünschten Dateioptionen an.");

        // === File format dropdown ===
        var lblFileFormat = new Label
        {
            Text = "Dateiformat:",
            Location = new Point(92, 24),
            AutoSize = true,
            Font = font
        };

        _cbFileFormat = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(176, 21),
            Size = new Size(435, 24),
            Font = font,
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

        // === JPEG hint text ===
        _lblJpegHint = new Label
        {
            Text = "JPEG steht nicht zur Verfügung, wenn \"Schwarzweiß\" unter [Farbmodus]\ngewählt wurde.",
            Location = new Point(176, 48),
            Size = new Size(420, 26),
            AutoSize = false,
            Font = font,
            ForeColor = Color.FromArgb(70, 70, 70)
        };

        // === GroupBox: Texterkennung wählen ===
        var grpChoose = new GroupBox
        {
            Text = "Texterkennung wählen",
            Location = new Point(38, 78),
            Size = new Size(598, 68),
            Font = font,
            BackColor = Color.White
        };

        _chkAddKeyword = new CheckBox
        {
            Text = "Markierten Text als Schlüsselwort der PDF-Datei hinzufügen",
            Location = new Point(10, 14),
            AutoSize = true,
            Font = font
        };
        _chkAddKeyword.CheckedChanged += (s, e) =>
        {
            if (!_loading)
                _settings.AddKeywordToPdf = _chkAddKeyword.Checked;
            UpdateKeywordControlsEnabled();
        };

        _lblKeywordTarget = new Label
        {
            Text = "Zielmarkierung:",
            Location = new Point(10, 36),
            AutoSize = true,
            Font = font
        };

        _rbFirstSection = new RadioButton
        {
            Text = "Erste markierte Sektion",
            Location = new Point(110, 36),
            AutoSize = true,
            Font = font
        };
        _rbFirstSection.CheckedChanged += (s, e) =>
        {
            if (!_loading && _rbFirstSection.Checked)
                _settings.KeywordTarget = KeywordTarget.FirstMarkedSection;
        };

        _rbAllSections = new RadioButton
        {
            Text = "Alle markierten Sektionen",
            Location = new Point(270, 36),
            AutoSize = true,
            Font = font
        };
        _rbAllSections.CheckedChanged += (s, e) =>
        {
            if (!_loading && _rbAllSections.Checked)
                _settings.KeywordTarget = KeywordTarget.AllMarkedSections;
        };

        _chkOcr = new CheckBox
        {
            Text = "In durchsuchbare PDF konvertieren",
            Location = new Point(10, 52),
            AutoSize = true,
            Font = font
        };
        _chkOcr.CheckedChanged += (s, e) =>
        {
            if (!_loading)
                _settings.OcrEnabled = _chkOcr.Checked;
            UpdateOcrSubControlsEnabled();
        };

        grpChoose.Controls.AddRange(new Control[]
        {
            _chkAddKeyword,
            _lblKeywordTarget, _rbFirstSection, _rbAllSections,
            _chkOcr
        });

        // === GroupBox: Texterkennungsoptionen ===
        _grpOcrOptions = new GroupBox
        {
            Text = "Texterkennungsoptionen",
            Location = new Point(38, 150),
            Size = new Size(598, 56),
            Font = font,
            BackColor = Color.White
        };

        _lblOcrLanguage = new Label
        {
            Text = "Sprache:",
            Location = new Point(12, 20),
            AutoSize = true,
            Font = font
        };

        _cbOcrLanguage = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(100, 17),
            Size = new Size(320, 24),
            Font = font,
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

        _lblOcrTarget = new Label
        {
            Text = "Zielseiten:",
            Location = new Point(12, 40),
            AutoSize = true,
            Font = font
        };

        _rbFirstPage = new RadioButton
        {
            Text = "Erste Seite",
            Location = new Point(100, 40),
            AutoSize = true,
            Font = font
        };
        _rbFirstPage.CheckedChanged += (s, e) =>
        {
            if (!_loading && _rbFirstPage.Checked)
                _settings.OcrTargetPages = OcrTargetPages.FirstPage;
        };

        _rbAllPages = new RadioButton
        {
            Text = "Alle Seiten",
            Location = new Point(200, 40),
            AutoSize = true,
            Font = font
        };
        _rbAllPages.CheckedChanged += (s, e) =>
        {
            if (!_loading && _rbAllPages.Checked)
                _settings.OcrTargetPages = OcrTargetPages.AllPages;
        };

        _grpOcrOptions.Controls.AddRange(new Control[]
        {
            _lblOcrLanguage, _cbOcrLanguage,
            _lblOcrTarget, _rbFirstPage, _rbAllPages
        });

        // === Options button (bottom-right, anchored) ===
        _btnOptions = new Button
        {
            Text = "Option...",
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            Size = new Size(90, 24),
            Font = font,
            Image = TabIcons.CreateGearIcon(),
            ImageAlign = ContentAlignment.MiddleLeft,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(2, 0, 4, 0),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        _btnOptions.Click += (s, e) => OpenPdfOptionsDialog();

        Layout += (s, e) =>
        {
            _btnOptions.Location = new Point(
                ClientSize.Width - _btnOptions.Width - 12,
                ClientSize.Height - _btnOptions.Height - 8);
        };

        Controls.AddRange(new Control[]
        {
            infoBar,
            lblFileFormat, _cbFileFormat,
            _lblJpegHint,
            grpChoose,
            _grpOcrOptions,
            _btnOptions
        });

        LoadFromSettings();
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
        _lblJpegHint.ForeColor = _jpegAvailable ? Color.FromArgb(70, 70, 70) : Color.FromArgb(180, 60, 40);

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
        e.DrawBackground();

        if (e.Index < 0) return;

        string text = _cbFileFormat.Items[e.Index]!.ToString()!;
        bool isJpeg = e.Index == (int)FileFormat.Jpeg;
        bool disabled = isJpeg && !_jpegAvailable;

        var color = disabled ? SystemColors.GrayText : SystemColors.ControlText;

        // Draw file type icon before text
        var icon = e.Index >= 0 && e.Index < FileTypeIcons.Length ? FileTypeIcons[e.Index] : null;
        var textBounds = e.Bounds;
        if (icon != null)
        {
            var iconRect = new Rectangle(e.Bounds.X + 1, e.Bounds.Y + (e.Bounds.Height - 16) / 2, 16, 16);
            using var ia = new System.Drawing.Imaging.ImageAttributes();
            if (disabled)
            {
                var cm = new System.Drawing.Imaging.ColorMatrix
                {
                    Matrix00 = 0.3f, Matrix11 = 0.3f, Matrix22 = 0.3f, Matrix33 = 1f, Matrix44 = 1f
                };
                ia.SetColorMatrix(cm);
            }
            e.Graphics.DrawImage(icon,
                new Rectangle(iconRect.X, iconRect.Y, iconRect.Width, iconRect.Height),
                0, 0, icon.Width, icon.Height,
                GraphicsUnit.Pixel, ia);
            textBounds = new Rectangle(iconRect.Right + 4, e.Bounds.Y, e.Bounds.Width - iconRect.Width - 5, e.Bounds.Height);
        }

        TextRenderer.DrawText(e.Graphics, text, _cbFileFormat.Font, textBounds, color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

        e.DrawFocusRectangle();
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
        e.DrawBackground();

        if (e.Index < 0) return;

        var entry = _cbOcrLanguage.Items[e.Index];
        string text = entry?.ToString() ?? "";
        bool enabled = _cbOcrLanguage.Enabled;

        var color = enabled ? SystemColors.ControlText : SystemColors.GrayText;

        int flagIndex = entry is OcrLanguageItem item ? (int)item.Language : -1;
        var flag = flagIndex >= 0 && flagIndex < OcrLanguageFlags.Length ? OcrLanguageFlags[flagIndex] : null;
        var textBounds = e.Bounds;
        if (flag != null)
        {
            var iconRect = new Rectangle(e.Bounds.X + 1, e.Bounds.Y + (e.Bounds.Height - 16) / 2, 16, 16);
            if (!enabled)
            {
                using var ia = new System.Drawing.Imaging.ImageAttributes();
                var cm = new System.Drawing.Imaging.ColorMatrix
                {
                    Matrix00 = 0.3f, Matrix11 = 0.3f, Matrix22 = 0.3f, Matrix33 = 1f, Matrix44 = 1f
                };
                ia.SetColorMatrix(cm);
                e.Graphics.DrawImage(flag,
                    new Rectangle(iconRect.X, iconRect.Y, iconRect.Width, iconRect.Height),
                    0, 0, flag.Width, flag.Height,
                    GraphicsUnit.Pixel, ia);
            }
            else
            {
                e.Graphics.DrawImage(flag, iconRect);
            }
            textBounds = new Rectangle(iconRect.Right + 4, e.Bounds.Y, e.Bounds.Width - iconRect.Width - 5, e.Bounds.Height);
        }

        TextRenderer.DrawText(e.Graphics, text, _cbOcrLanguage.Font, textBounds, color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

        e.DrawFocusRectangle();
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
