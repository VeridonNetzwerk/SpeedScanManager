using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Main settings form with collapsed/expanded modes.
/// Collapsed: header + quick-menu checkbox + preset buttons + action buttons.
/// Expanded: additionally shows a TabControl with 6 tabs.
/// All tabs edit a cloned working copy of the settings; only OK/Übernehmen copy it into
/// the live settings and raise <see cref="SettingsApplied"/>. Abbrechen, Esc and the
/// window's close button discard unapplied changes.
/// </summary>
internal class MainForm : Form
{
    private readonly CheckBox _quickMenuCheckBox;
    private readonly CheckBox _autoStartCheckBox;

    private readonly Panel _presetPanel;
    private readonly Button _btnRecommended;
    private readonly Button _btnSmallFile;
    private readonly Button _btnHighQuality;
    private readonly Button _btnCustom;

    private readonly ComboBox _profileDropdown;
    private readonly Label _profileLabel;
    private readonly Panel _profileSelectorPanel;

    private readonly TabControl _tabControl;
    private readonly ImageList _tabImageList;
    private readonly List<Image> _ownedImages = new();
    private readonly SaveTabContent _saveTabContent;
    private readonly ApplicationTabContent _applicationTabContent;
    private readonly ScanModeTabContent _scanModeTabContent;
    private readonly FileTypeTabContent _fileTypeTabContent;
    private readonly PaperTabContent _paperTabContent;
    private readonly FileSizeTabContent _fileSizeTabContent;

    /// <summary>Live settings owned by the tray (only written on OK/Übernehmen).</summary>
    private readonly ScanSettings _liveSettings;
    /// <summary>Working copy edited by all tabs.</summary>
    private readonly ScanSettings _working;
    /// <summary>
    /// The working copy as it was when this editing session started (window opened, OK/
    /// Übernehmen, Abbrechen). Fields that differ from it were edited by the user here.
    /// </summary>
    private readonly ScanSettings _sessionBaseline;
    private readonly AppSettings _appSettings;
    private readonly ProfileManager _profileManager;
    private ScanProfile? _activeProfile;

    /// <summary>Working copy of the settings being edited (not yet applied).</summary>
    public ScanSettings Settings => _working;
    public SaveTabContent? SaveTab => _saveTabContent;
    public ApplicationTabContent? ApplicationTab => _applicationTabContent;
    public PaperTabContent? PaperTab => _paperTabContent;
    public bool QuickMenuEnabled => _quickMenuCheckBox.Checked;

    /// <summary>
    /// Raised after OK/Übernehmen copied the working copy into the live ScanSettings and
    /// updated AppSettings (QuickMenuEnabled, AutoStart, SelectedProfileName). The handler
    /// is expected to persist (AppSettings.Save()).
    /// </summary>
    public event EventHandler? SettingsApplied;

    /// <summary>
    /// Raised when the list of custom applications changed (persist immediately, independent
    /// of OK/Abbrechen like ScanSnap).
    /// </summary>
    public event EventHandler? CustomApplicationsChanged;

    private readonly Button _detailButton;
    private readonly Button _okButton;
    private readonly Button _cancelButton;
    private readonly Button _applyButton;

    private readonly PictureBox _headerLogo;
    private readonly ComboBox _themeDropdown;
    private Bitmap? _headerLogoImage;

    private bool _expanded;
    private bool _suppressChange;
    private bool _suppressProfileEvent;
    private bool _suppressThemeEvent;

    // Layout constants (96 dpi; use Px() for values applied after construction)
    private const int ClientWidth = 686;
    private const int ExpandedClientHeight = 503; // Segoe UI needs a little more room than the old 8.25 pt font
    private const int CollapsedClientHeight = 173; // 133 top + 40 footer
    private const int HeaderHeight = 44; // logo zone
    private const int TopPanelHeight = 133; // 44 header + 89 control zone
    private const int BottomBarHeight = 40;
    private const int SeparatorHeight = 1;
    private const int HeaderLogoHeight = 26;
    private const int PresetMargin = 18;
    private const int PresetSpacing = 8;

    private readonly Panel _topPanel;
    private readonly Panel _separatorPanel;
    private readonly Panel _bottomPanel;

    public MainForm(ScanSettings liveSettings, ProfileManager profileManager, AppSettings appSettings)
    {
        _liveSettings = liveSettings;
        _liveSettings.Normalize();
        _working = liveSettings.Clone();
        _sessionBaseline = _working.Clone();
        _profileManager = profileManager;
        _appSettings = appSettings;
        _activeProfile = _profileManager.GetByName(_appSettings.SelectedProfileName);

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        HelpButton = true;
        HelpButtonClicked += (s, e) =>
        {
            e.Cancel = true;
            using var help = new HelpForm("settings");
            help.ShowDialog(this);
        };
        StartPosition = FormStartPosition.CenterScreen;
        Text = "SpeedScan Manager – Einstellungen";
        Icon = TrayIcons.GetAppIcon();
        ClientSize = new Size(ClientWidth, CollapsedClientHeight);
        ShowInTaskbar = true;
        Font = UiFonts.Default;

        var contentFont = UiFonts.Default;
        var presetFont = UiFonts.Default;

        // === Top panel (control zone, contains header + checkbox + presets + separator) ===
        _topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = TopPanelHeight
        };
        ThemeManager.SetRole(_topPanel, ThemeRole.Secondary);

        // === Logo area ===
        var headerPanel = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(ClientWidth, HeaderHeight)
        };
        ThemeManager.SetRole(headerPanel, ThemeRole.Header);

        var textBmp = AppResources.TextLogo;
        int hdrTextW = (int)(textBmp.Width * (HeaderLogoHeight / (double)textBmp.Height));
        _headerLogo = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.Zoom,
            AutoSize = false,
            Location = new Point(16, 9),
            Size = new Size(hdrTextW, HeaderLogoHeight)
        };
        headerPanel.Controls.Add(_headerLogo);

        // === Design selection (System / Hell / Dunkel), applied immediately ===
        const int themeDropdownWidth = 110;
        _themeDropdown = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = contentFont,
            Size = new Size(themeDropdownWidth, 24),
            Location = new Point(ClientWidth - 18 - themeDropdownWidth, 10)
        };
        foreach (ThemeMode mode in Enum.GetValues<ThemeMode>())
            _themeDropdown.Items.Add(ThemeManager.GetModeName(mode));
        _themeDropdown.SelectedIndex = (int)ThemeManager.Mode;
        _themeDropdown.SelectedIndexChanged += (s, e) =>
        {
            if (!_suppressThemeEvent && _themeDropdown.SelectedIndex >= 0)
                ThemeManager.SetMode((ThemeMode)_themeDropdown.SelectedIndex);
        };
        var themeLabel = new Label
        {
            Text = "Design:",
            AutoSize = true,
            Font = contentFont
        };
        int themeLabelWidth = TextRenderer.MeasureText(themeLabel.Text, ThemeFonts.Regular).Width;
        themeLabel.Location = new Point(_themeDropdown.Left - 6 - themeLabelWidth, 14);
        ThemeManager.SetRole(themeLabel, ThemeRole.Muted);
        headerPanel.Controls.Add(themeLabel);
        headerPanel.Controls.Add(_themeDropdown);
        _topPanel.Controls.Add(headerPanel);

        // === Quick-Menü checkbox (in gray zone) ===
        _quickMenuCheckBox = new CheckBox
        {
            Text = "Quick-Menü verwenden",
            Checked = _appSettings.QuickMenuEnabled,
            Location = new Point(18, 56), // 44px header + 12px into gray zone
            AutoSize = true,
            Font = contentFont
        };
        _topPanel.Controls.Add(_quickMenuCheckBox);

        // === Autostart checkbox (same row) ===
        _autoStartCheckBox = new CheckBox
        {
            Text = "SpeedScan Manager beim Anmelden starten",
            Checked = _appSettings.AutoStart,
            Location = new Point(200, 56),
            AutoSize = true,
            Font = contentFont
        };
        _topPanel.Controls.Add(_autoStartCheckBox);

        // === Preset buttons (TableLayoutPanel for equal width/spacing) ===
        _presetPanel = new TableLayoutPanel
        {
            Location = new Point(0, 86), // 44px header + 42px into gray zone
            Size = new Size(ClientWidth, 25),
            ColumnCount = 9,
            ColumnStyles =
            {
                new ColumnStyle(SizeType.Absolute, PresetMargin),
                new ColumnStyle(SizeType.Percent, 25f),
                new ColumnStyle(SizeType.Absolute, PresetSpacing),
                new ColumnStyle(SizeType.Percent, 25f),
                new ColumnStyle(SizeType.Absolute, PresetSpacing),
                new ColumnStyle(SizeType.Percent, 25f),
                new ColumnStyle(SizeType.Absolute, PresetSpacing),
                new ColumnStyle(SizeType.Percent, 25f),
                new ColumnStyle(SizeType.Absolute, PresetMargin)
            },
            RowCount = 1,
            RowStyles = { new RowStyle(SizeType.Percent, 100f) },
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        _btnRecommended = CreatePresetButton("Empfohlen", 0, 0, presetFont);
        _btnSmallFile = CreatePresetButton("Kleine Datei", 0, 0, presetFont);
        _btnHighQuality = CreatePresetButton("Hohe Bildqualität", 0, 0, presetFont);
        _btnCustom = CreatePresetButton("Benutzerdefiniert", 0, 0, presetFont);

        _btnRecommended.Dock = DockStyle.Fill;
        _btnSmallFile.Dock = DockStyle.Fill;
        _btnHighQuality.Dock = DockStyle.Fill;
        _btnCustom.Dock = DockStyle.Fill;

        var presetTable = (TableLayoutPanel)_presetPanel;
        presetTable.Controls.Add(_btnRecommended, 1, 0);
        presetTable.Controls.Add(_btnSmallFile, 3, 0);
        presetTable.Controls.Add(_btnHighQuality, 5, 0);
        presetTable.Controls.Add(_btnCustom, 7, 0);

        foreach (var btn in new[] { _btnRecommended, _btnSmallFile, _btnHighQuality, _btnCustom })
        {
            btn.Click += PresetButton_Click;
            btn.Click += (s, e) => OnSettingsChanged();
        }
        _topPanel.Controls.Add(_presetPanel);

        // === Separator line under preset buttons ===
        _separatorPanel = new Panel
        {
            Location = new Point(0, 124), // still at bottom of top panel
            Size = new Size(ClientWidth, SeparatorHeight)
        };
        ThemeManager.SetRole(_separatorPanel, ThemeRole.Divider);
        _topPanel.Controls.Add(_separatorPanel);

        // === Profile dropdown (shown in 4th column when Quick-Menü is off) ===
        _profileLabel = new Label
        {
            Text = "Profil:",
            AutoSize = true,
            Font = contentFont,
            Visible = false
        };

        _profileDropdown = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = contentFont,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Visible = false,
            DropDownWidth = 220
        };

        _profileSelectorPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Visible = false
        };

        RefreshProfileDropdown();
        _profileDropdown.SelectedIndexChanged += (s, e) => OnProfileSelected();

        // Add profile selector to the 4th column (replaces Benutzerdefiniert when Quick-Menü is off)
        presetTable.Controls.Add(_profileSelectorPanel, 7, 0);

        // Position profile label and dropdown above/in the 4th preset column
        _topPanel.Layout += (s, e) =>
        {
            if (_presetPanel.Width > 0)
            {
                var widths = ((TableLayoutPanel)_presetPanel).GetColumnWidths();
                int x = 0;
                for (int i = 0; i < 7; i++) x += widths[i];
                int colWidth = widths.Length > 7 ? widths[7] : Px(100);
                int presetTop = _presetPanel.Top;
                _profileLabel.Location = new Point(x, presetTop - Px(18));
                _profileDropdown.Location = new Point(x, presetTop + Px(2));
                _profileDropdown.Width = colWidth;
                _profileDropdown.BringToFront();
                _profileLabel.BringToFront();
            }
        };
        _topPanel.Controls.Add(_profileDropdown);
        _topPanel.Controls.Add(_profileLabel);
        Load += (s, e) => _topPanel.PerformLayout();
        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Visible = false,
            Font = contentFont,
            Appearance = TabAppearance.FlatButtons,
            DrawMode = TabDrawMode.OwnerDrawFixed,
            // Normal lets WinForms size every tab independently from its own text
            // (plus the ImageList width). The tab strip itself is painted by the theme
            // (TabControlSubclass), each tab by TabControl_DrawItem.
            SizeMode = TabSizeMode.Normal,
            Padding = new Point(20, 5)
        };
        _tabControl.DrawItem += TabControl_DrawItem;
        _tabControl.Selecting += TabControl_Selecting;

        // Create ImageList with 16x16 icons for tabs
        _tabImageList = new ImageList
        {
            ImageSize = new Size(16, 16),
            ColorDepth = ColorDepth.Depth32Bit
        };
        AddTabIcon("app", TabIcons.CreateAppIcon());
        AddTabIcon("save", TabIcons.CreateSaveIcon());
        AddTabIcon("scan", TabIcons.CreateScanIcon());
        AddTabIcon("file", TabIcons.CreateFileIcon());
        AddTabIcon("paper", TabIcons.CreatePaperIcon());
        AddTabIcon("size", TabIcons.CreateSizeIcon());
        _tabControl.ImageList = _tabImageList;

        var tabDefs = new[]
        {
            ("Anwendung", "app"),
            ("Speichern", "save"),
            ("Scanmodus", "scan"),
            ("Dateiart", "file"),
            ("Papier", "paper"),
            ("Dateigröße", "size")
        };
        foreach (var (name, key) in tabDefs)
        {
            var page = new TabPage(name)
            {
                ImageKey = key
            };
            _tabControl.TabPages.Add(page);
        }

        // Populate each tab with content wrapped in an inner panel
        _applicationTabContent = new ApplicationTabContent(_working, _appSettings.CustomApplications);
        WrapTabContent(_tabControl.TabPages[0], _applicationTabContent);
        _applicationTabContent.CustomApplicationsChanged += (s, e) => CustomApplicationsChanged?.Invoke(this, EventArgs.Empty);

        _saveTabContent = new SaveTabContent(_working);
        WrapTabContent(_tabControl.TabPages[1], _saveTabContent);

        _scanModeTabContent = new ScanModeTabContent(_working);
        WrapTabContent(_tabControl.TabPages[2], _scanModeTabContent);

        _fileTypeTabContent = new FileTypeTabContent(_working);
        WrapTabContent(_tabControl.TabPages[3], _fileTypeTabContent);

        _paperTabContent = new PaperTabContent(_working);
        WrapTabContent(_tabControl.TabPages[4], _paperTabContent);

        _fileSizeTabContent = new FileSizeTabContent(_working);
        WrapTabContent(_tabControl.TabPages[5], _fileSizeTabContent);

        // Cross-tab dependencies
        _scanModeTabContent.ColorModeChanged += (mode) =>
        {
            _fileTypeTabContent.OnColorModeChanged(mode);
            _fileSizeTabContent.UpdateEnabledState();
        };
        _scanModeTabContent.ScanSideChanged += (side) => _paperTabContent.OnScanSideChanged(side);
        _fileTypeTabContent.FileFormatChanged += (fmt) =>
        {
            _fileSizeTabContent.UpdateEnabledState();
            _saveTabContent.UpdatePreview();
        };

        // === Bottom bar ===
        _bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = BottomBarHeight
        };
        ThemeManager.SetRole(_bottomPanel, ThemeRole.Secondary);
        _bottomPanel.Paint += (s, e) =>
        {
            using var pen = new Pen(ThemeManager.Current.Border);
            e.Graphics.DrawLine(pen, 0, 0, _bottomPanel.Width, 0);
        };

        _detailButton = new Button
        {
            Text = "Detail",
            Size = new Size(125, 28),
            Font = contentFont,
            Dock = DockStyle.Fill,
            Margin = new Padding(18, 6, 0, 6)
        };
        ThemeManager.SetGlyph(_detailButton, Glyph.ChevronDown);
        _detailButton.Click += (s, e) => ToggleExpanded();

        _okButton = new Button
        {
            Text = "OK",
            Size = new Size(116, 28),
            Font = contentFont,
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 6, 0, 6)
        };
        ThemeManager.SetRole(_okButton, ThemeRole.Primary);
        ThemeManager.SetGlyph(_okButton, Glyph.Check);
        _okButton.Click += (s, e) => { if (ApplySettings()) Close(); };

        _cancelButton = new Button
        {
            Text = "Abbrechen",
            Size = new Size(116, 28),
            Font = contentFont,
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 6, 0, 6)
        };
        ThemeManager.SetGlyph(_cancelButton, Glyph.Cross);
        _cancelButton.Click += (s, e) => { CancelSettings(); Close(); };

        _applyButton = new Button
        {
            Text = "Übernehmen",
            Size = new Size(116, 28),
            Font = contentFont,
            Enabled = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 6, 0, 6)
        };
        ThemeManager.SetGlyph(_applyButton, Glyph.Apply);
        _applyButton.Click += (s, e) => ApplySettings();

        // Wire tab content changes to enable Apply button
        WireTabContentChanges();

        var bottomTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            ColumnStyles =
            {
                new ColumnStyle(SizeType.Absolute, 143f),  // detail button + left margin
                new ColumnStyle(SizeType.Percent, 100f),   // flexible gap
                new ColumnStyle(SizeType.Absolute, 116f),  // OK
                new ColumnStyle(SizeType.Absolute, 116f),  // Abbrechen
                new ColumnStyle(SizeType.Absolute, 116f),  // Übernehmen
                new ColumnStyle(SizeType.Absolute, 18f)    // right margin
            },
            RowCount = 1,
            RowStyles = { new RowStyle(SizeType.Percent, 100f) },
            Margin = Padding.Empty,
            Padding = new Padding(0, 1, 0, 0) // keep the divider line drawn by _bottomPanel visible
        };

        bottomTable.Controls.Add(_detailButton, 0, 0);
        bottomTable.Controls.Add(new Panel { Dock = DockStyle.Fill }, 1, 0);
        bottomTable.Controls.Add(_okButton, 2, 0);
        bottomTable.Controls.Add(_cancelButton, 3, 0);
        bottomTable.Controls.Add(_applyButton, 4, 0);
        bottomTable.Controls.Add(new Panel { Dock = DockStyle.Fill }, 5, 0);

        _bottomPanel.Controls.Add(bottomTable);

        // Add controls in docking order (first = docked last = Fill)
        Controls.AddRange(new Control[]
        {
            _tabControl,
            _bottomPanel,
            _topPanel
        });

        AcceptButton = _okButton;
        CancelButton = _cancelButton; // Esc = Abbrechen

        LoadAllTabs();
        OnQuickMenuToggled();
        UpdateApplyButton();

        ThemeManager.Apply(this);
        UpdateHeaderLogo();
        ThemeManager.ThemeChanged += OnThemeChanged;
        ThemeManager.ModeChanged += OnThemeModeChanged;

        ResumeLayout(false);
        PerformLayout();
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        UpdateHeaderLogo();
        UpdatePresetHighlight();
    }

    private void OnThemeModeChanged(object? sender, EventArgs e)
    {
        _suppressThemeEvent = true;
        try { _themeDropdown.SelectedIndex = (int)ThemeManager.Mode; }
        finally { _suppressThemeEvent = false; }
    }

    /// <summary>
    /// Logo wordmark pre-scaled to its display size (crisp at any DPI); in dark mode it is
    /// drawn in white instead of blue for contrast.
    /// </summary>
    private void UpdateHeaderLogo()
    {
        var src = AppResources.TextLogo;
        int h = Px(HeaderLogoHeight);
        int w = Math.Max(1, (int)Math.Round(src.Width * (h / (double)src.Height)));
        var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            if (ThemeManager.IsDark)
            {
                // Keep the shape (alpha), replace the color.
                using var ia = new System.Drawing.Imaging.ImageAttributes();
                ia.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix(new[]
                {
                    new float[] { 0, 0, 0, 0, 0 },
                    new float[] { 0, 0, 0, 0, 0 },
                    new float[] { 0, 0, 0, 0, 0 },
                    new float[] { 0, 0, 0, 1, 0 },
                    new float[] { 0.95f, 0.95f, 0.97f, 0, 1 }
                }));
                g.DrawImage(src, new Rectangle(0, 0, w, h), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, ia);
            }
            else
            {
                g.DrawImage(src, new Rectangle(0, 0, w, h));
            }
        }
        var old = _headerLogoImage;
        _headerLogoImage = bmp;
        _headerLogo.Image = bmp;
        old?.Dispose();
    }

    /// <summary>
    /// Converts a 96-dpi layout value to device pixels (for values applied after construction).
    /// </summary>
    private int Px(int value) => (int)Math.Round(value * DeviceDpi / 96f);

    private void AddTabIcon(string key, Image image)
    {
        _ownedImages.Add(image);
        _tabImageList.Images.Add(key, image);
    }

    private static Button CreatePresetButton(string text, int x, int width, Font font)
    {
        return new Button
        {
            Text = text,
            Location = new Point(x, 0),
            Size = new Size(width, 23),
            Font = font,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 0, 0, 0)
        };
    }

    private void PresetButton_Click(object? sender, EventArgs e)
    {
        if (sender is Button btn)
        {
            if (btn == _btnRecommended)
            {
                ApplyPreset(ImageQuality.Automatic, 3);
            }
            else if (btn == _btnSmallFile)
            {
                ApplyPreset(ImageQuality.Normal, 5);
            }
            else if (btn == _btnHighQuality)
            {
                ApplyPreset(ImageQuality.Fine, 1);
            }
            else if (btn == _btnCustom)
            {
                if (!_expanded)
                    ToggleExpanded();
            }
            UpdatePresetHighlight();
        }
    }

    private void ApplyPreset(ImageQuality quality, int compression)
    {
        _working.ImageQuality = quality;
        _working.CompressionRate = compression;

        _suppressChange = true;
        try
        {
            _scanModeTabContent.ApplyPreset(quality);
            _fileSizeTabContent.ApplyPreset(compression);
        }
        finally
        {
            _suppressChange = false;
        }
    }

    /// <summary>
    /// Highlights the preset button that matches the current settings
    /// (Benutzerdefiniert when no preset matches).
    /// </summary>
    private void UpdatePresetHighlight()
    {
        Button? active = (_working.ImageQuality, _working.CompressionRate) switch
        {
            (ImageQuality.Automatic, 3) => _btnRecommended,
            (ImageQuality.Normal, 5) => _btnSmallFile,
            (ImageQuality.Fine, 1) => _btnHighQuality,
            _ => _btnCustom
        };

        // The active preset is shown as primary (accent) button.
        foreach (var b in new[] { _btnRecommended, _btnSmallFile, _btnHighQuality, _btnCustom })
            ThemeManager.SetRole(b, b == active ? ThemeRole.Primary : ThemeRole.Secondary);
    }

    private void OnQuickMenuToggled()
    {
        bool useQuick = _quickMenuCheckBox.Checked;
        // Preset buttons (first 3) are always visible
        _btnRecommended.Visible = true;
        _btnSmallFile.Visible = true;
        _btnHighQuality.Visible = true;
        // 4th column: Benutzerdefiniert (Quick on) vs Profil dropdown (Quick off)
        _btnCustom.Visible = useQuick;
        _profileSelectorPanel.Visible = !useQuick;
        _profileLabel.Visible = !useQuick;
        _profileDropdown.Visible = !useQuick;

        // Disable Anwendung tab when Quick-Menü is on
        _applicationTabContent.Enabled = !useQuick;
        _tabControl.Invalidate();

        // If Anwendung tab is selected while Quick-Menü turns on, switch to Speichern
        if (useQuick && _tabControl.SelectedIndex == 0)
            _tabControl.SelectedIndex = 1;
    }

    /// <summary>
    /// Rebuilds the profile dropdown and selects the active profile (no profile is loaded).
    /// </summary>
    public void RefreshProfileDropdown()
    {
        if (_activeProfile != null && !_profileManager.Profiles.Contains(_activeProfile))
            _activeProfile = null; // deleted in Profilverwaltung

        _suppressProfileEvent = true;
        try
        {
            _profileDropdown.Items.Clear();
            foreach (var profile in _profileManager.Profiles)
            {
                _profileDropdown.Items.Add(profile.DisplayName);
            }
            // Add separator items
            _profileDropdown.Items.Add("-");
            _profileDropdown.Items.Add("Profil hinzufügen...");
            _profileDropdown.Items.Add("Profilverwaltung...");
            RestoreProfileSelection();
        }
        finally
        {
            _suppressProfileEvent = false;
        }
    }

    private void RestoreProfileSelection()
    {
        bool previous = _suppressProfileEvent;
        _suppressProfileEvent = true;
        try
        {
            _profileDropdown.SelectedIndex = _activeProfile == null ? -1 : _profileManager.Profiles.IndexOf(_activeProfile);
        }
        finally
        {
            _suppressProfileEvent = previous;
        }
    }

    private void OnProfileSelected()
    {
        if (_suppressProfileEvent) return;
        int idx = _profileDropdown.SelectedIndex;
        if (idx < 0) return;

        int profileCount = _profileManager.Profiles.Count;

        if (idx == profileCount + 1) // "Profil hinzufügen..."
        {
            // Defer so the dropdown has closed before the dialog opens
            BeginInvoke(() => { RestoreProfileSelection(); AddProfile(); });
            return;
        }

        if (idx == profileCount + 2) // "Profilverwaltung..."
        {
            BeginInvoke(() => { RestoreProfileSelection(); OpenProfileManagement(); });
            return;
        }

        if (idx == profileCount) // separator
        {
            RestoreProfileSelection();
            return;
        }

        var profile = _profileManager.Profiles[idx];
        if (!ReferenceEquals(profile, _activeProfile))
            LoadProfile(profile);
    }

    private void LoadProfile(ScanProfile profile)
    {
        profile.ApplyTo(_working);
        _working.Normalize();
        _activeProfile = profile;
        LoadAllTabs();
        RestoreProfileSelection();
        UpdateApplyButton();
    }

    private void AddProfile()
    {
        if (!_profileManager.CanAddProfile)
        {
            MessageBox.Show(this, $"Es können höchstens {ProfileManager.MaxProfiles} Profile angelegt werden.",
                "SpeedScan Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string? name = ProfileNameInput.Show(this, _profileManager, "Neues Profil hinzufügen", "Profilname:", "", null);
        if (name == null) return;

        // New profile = current (working) settings
        _working.Normalize();
        var profile = ScanProfile.FromCurrent(_working, name, isBuiltIn: false);
        _profileManager.AddProfile(profile);
        _activeProfile = profile;
        RefreshProfileDropdown();
        UpdateApplyButton();
    }

    private void OpenProfileManagement()
    {
        using var dlg = new ProfileManagementDialog(_profileManager);
        dlg.ShowDialog(this);
        RefreshProfileDropdown();
        UpdateApplyButton();
    }

    private void ToggleExpanded()
    {
        _expanded = !_expanded;

        if (_expanded)
        {
            ClientSize = new Size(Px(ClientWidth), Px(ExpandedClientHeight));
            _detailButton.Text = "Ausblenden";
            ThemeManager.SetGlyph(_detailButton, Glyph.ChevronUp);
            _tabControl.Visible = true;

            // When Quick-Menü is on, never show the Anwendung tab
            if (_quickMenuCheckBox.Checked && _tabControl.SelectedIndex == 0)
                _tabControl.SelectedIndex = 1; // Speichern
        }
        else
        {
            ClientSize = new Size(Px(ClientWidth), Px(CollapsedClientHeight));
            _detailButton.Text = "Detail";
            ThemeManager.SetGlyph(_detailButton, Glyph.ChevronDown);
            _tabControl.Visible = false;
        }
    }

    private static void WrapTabContent(TabPage page, Control content)
    {
        var innerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8),
            Padding = new Padding(0)
        };
        innerPanel.Controls.Add(content);
        page.Controls.Add(innerPanel);
    }

    /// <summary>
    /// Refreshes every tab from the working copy without triggering change handling.
    /// </summary>
    private void LoadAllTabs()
    {
        _suppressChange = true;
        try
        {
            _applicationTabContent.LoadFromSettings();
            _saveTabContent.LoadFromSettings();
            _scanModeTabContent.LoadFromSettings();
            _fileTypeTabContent.LoadFromSettings();
            _paperTabContent.LoadFromSettings();
            _fileSizeTabContent.LoadFromSettings();
        }
        finally
        {
            _suppressChange = false;
        }
        UpdatePresetHighlight();
    }

    /// <summary>
    /// A setting was changed outside this window (tray menu E-Mail-/Druckeinstellungen):
    /// apply it to the working copy and to the session baseline, so it is neither reverted
    /// by OK/Übernehmen nor treated as an edit of the user in this window.
    /// </summary>
    public void ApplyExternalChange(Action<ScanSettings> change)
    {
        change(_working);
        change(_sessionBaseline);
        LoadAllTabs();
        UpdateApplyButton();
    }

    /// <summary>
    /// True if the working copy differs from <paramref name="profile"/> in a setting the user
    /// changed in this window (differences that already existed when the window was opened,
    /// e.g. e-mail settings changed from the tray, don't count).
    /// </summary>
    private bool HasUserChangesAgainst(ScanProfile profile)
    {
        var edited = _working.DifferingSettings(_sessionBaseline);
        if (edited.Count == 0) return false;
        return _working.DifferingSettings(profile.Settings).Overlaps(edited);
    }

    /// <summary>
    /// Copies the working copy into the live settings (OK/Übernehmen).
    /// Returns false when the user cancelled a follow-up question.
    /// </summary>
    private bool ApplySettings()
    {
        _working.Normalize();
        LoadAllTabs();

        // Profile handling (profiles are only active when the Quick-Menü is off)
        if (!_quickMenuCheckBox.Checked && _activeProfile != null && !_working.SettingsEqual(_activeProfile.Settings)
            && (!_activeProfile.IsBuiltIn || HasUserChangesAgainst(_activeProfile)))
        {
            if (!_activeProfile.IsBuiltIn)
            {
                // Changes to a user profile are saved into it (ScanSnap behaviour)
                _activeProfile.UpdateFrom(_working);
                _profileManager.Save();
            }
            else
            {
                var answer = MessageBox.Show(this,
                    $"Das Standardprofil \"{_activeProfile.DisplayName}\" kann nicht geändert werden.\n\n" +
                    "Möchten Sie die geänderten Einstellungen als neues Profil speichern?\n\n" +
                    "Ja: Als neues Profil speichern\n" +
                    "Nein: Nur als aktuelle Einstellungen übernehmen",
                    "SpeedScan Manager", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (answer == DialogResult.Cancel)
                    return false;
                if (answer == DialogResult.Yes)
                {
                    if (!_profileManager.CanAddProfile)
                    {
                        MessageBox.Show(this, $"Es können höchstens {ProfileManager.MaxProfiles} Profile angelegt werden.",
                            "SpeedScan Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return false;
                    }
                    string? name = ProfileNameInput.Show(this, _profileManager, "Neues Profil hinzufügen", "Profilname:", "", null);
                    if (name == null)
                        return false;
                    var profile = ScanProfile.FromCurrent(_working, name, isBuiltIn: false);
                    _profileManager.AddProfile(profile);
                    _activeProfile = profile;
                    RefreshProfileDropdown();
                }
            }
        }

        _liveSettings.RestoreFrom(_working);
        _appSettings.QuickMenuEnabled = _quickMenuCheckBox.Checked;
        _appSettings.AutoStart = _autoStartCheckBox.Checked;
        _appSettings.SelectedProfileName = _activeProfile?.Name ?? "";
        _sessionBaseline.RestoreFrom(_working);

        SettingsApplied?.Invoke(this, EventArgs.Empty);
        UpdateApplyButton();
        return true;
    }

    /// <summary>
    /// Discards unapplied edits and reloads all tabs from the live settings.
    /// </summary>
    private void CancelSettings()
    {
        _working.RestoreFrom(_liveSettings);
        _sessionBaseline.RestoreFrom(_working);
        _activeProfile = _profileManager.GetByName(_appSettings.SelectedProfileName);
        _quickMenuCheckBox.Checked = _appSettings.QuickMenuEnabled;
        _autoStartCheckBox.Checked = _appSettings.AutoStart;
        LoadAllTabs();
        RefreshProfileDropdown();
        UpdateApplyButton();
    }

    private bool HasUnappliedChanges()
        => !_working.SettingsEqual(_liveSettings)
           || _quickMenuCheckBox.Checked != _appSettings.QuickMenuEnabled
           || _autoStartCheckBox.Checked != _appSettings.AutoStart
           || (_activeProfile?.Name ?? "") != _appSettings.SelectedProfileName;

    private void UpdateApplyButton()
    {
        _applyButton.Enabled = HasUnappliedChanges();
    }

    private void OnSettingsChanged()
    {
        if (_suppressChange) return;
        UpdatePresetHighlight();
        UpdateApplyButton();
    }

    /// <summary>
    /// Recursively hooks change events on all child controls of the given control.
    /// When any ComboBox, CheckBox, RadioButton, TrackBar, or NumericUpDown changes,
    /// OnSettingsChanged is called to enable the Apply button.
    /// </summary>
    private void WireControlChanges(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            switch (c)
            {
                case ComboBox cb:
                    cb.SelectedIndexChanged += (s, e) => OnSettingsChanged();
                    break;
                case CheckBox chk:
                    chk.CheckedChanged += (s, e) => OnSettingsChanged();
                    break;
                case RadioButton rb:
                    rb.CheckedChanged += (s, e) => OnSettingsChanged();
                    break;
                case TrackBar tb:
                    tb.ValueChanged += (s, e) => OnSettingsChanged();
                    break;
                case NumericUpDown nud:
                    nud.ValueChanged += (s, e) => OnSettingsChanged();
                    break;
                case Button btn:
                    btn.Click += (s, e) => OnSettingsChanged();
                    break;
            }
            // Recurse into children
            WireControlChanges(c);
        }
    }

    private void WireTabContentChanges()
    {
        // Wire quick-menu and autostart checkboxes
        _quickMenuCheckBox.CheckedChanged += (s, e) => { OnQuickMenuToggled(); OnSettingsChanged(); };
        _autoStartCheckBox.CheckedChanged += (s, e) => OnSettingsChanged();

        // Wire all tab pages
        foreach (TabPage page in _tabControl.TabPages)
        {
            WireControlChanges(page);
        }
    }

    private void TabControl_Selecting(object? sender, TabControlCancelEventArgs e)
    {
        // Prevent selecting the Anwendung tab when Quick-Menü is on
        if (e.TabPageIndex == 0 && _quickMenuCheckBox.Checked)
        {
            e.Cancel = true;
            // Use BeginInvoke to switch to Speichern after the event completes
            _tabControl.BeginInvoke(() => _tabControl.SelectedIndex = 1);
        }
    }

    private static readonly Glyph[] TabGlyphs =
    {
        Glyph.Apps, Glyph.Folder, Glyph.Scanner, Glyph.FileType, Glyph.Paper, Glyph.Compress
    };

    /// <summary>
    /// Draws one tab (called by the themed tab strip): rounded pill for the selected / hovered
    /// tab, monochrome glyph and text in the theme colors.
    /// </summary>
    private void TabControl_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _tabControl.TabPages.Count) return;

        var p = ThemeManager.Current;
        var g = e.Graphics;
        var page = _tabControl.TabPages[e.Index];
        var tabRect = e.Bounds;
        bool isSelected = _tabControl.SelectedIndex == e.Index;
        // Anwendung is not available while the Quick-Menü is on
        bool isDisabled = e.Index == 0 && _quickMenuCheckBox.Checked;
        bool isHot = !isDisabled && (e.State & DrawItemState.HotLight) != 0;

        if (isSelected || isHot)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            var pill = RectangleF.Inflate(tabRect, -Px(2), -Px(3));
            using var path = ThemeGlyphs.Rounded(pill, Px(4));
            using var brush = new SolidBrush(isSelected ? p.Selected : p.Hover);
            g.FillPath(brush, path);
        }

        Color textColor = isDisabled ? ThemePalette.Blend(p.TextMuted, _tabControl.Parent?.BackColor ?? p.Background, 0.5f)
                        : isSelected ? p.Text
                        : isHot ? p.TextSecondary
                        : p.TextMuted;

        // Glyph at x+10 (16px), text starts at x+32; the tab width includes the ImageList image.
        int iconSize = Px(16);
        if (e.Index < TabGlyphs.Length)
        {
            var iconRect = new RectangleF(tabRect.X + Px(10), tabRect.Y + (tabRect.Height - iconSize) / 2f, iconSize, iconSize);
            ThemeGlyphs.Draw(g, TabGlyphs[e.Index], iconRect, isSelected ? p.Accent : textColor);
        }

        int textLeft = tabRect.Left + Px(32);
        var textRect = new Rectangle(textLeft, tabRect.Top, Math.Max(0, tabRect.Right - textLeft - Px(4)), tabRect.Height);
        // Same font as TabControl.Font, which WinForms uses to size the tabs
        TextRenderer.DrawText(g, page.Text, _tabControl.Font, textRect, textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            ThemeManager.ModeChanged -= OnThemeModeChanged;
            _tabImageList.Dispose();
            foreach (var img in _ownedImages)
                img.Dispose();
            _headerLogo.Image = null;
            _headerLogoImage?.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Shared fonts for all settings UI (created once instead of per control).
/// ThemeManager.Apply maps them to Segoe UI (see <see cref="ThemeFonts.Map"/>).
/// </summary>
internal static class UiFonts
{
    public static readonly Font Default = new("Microsoft Sans Serif", 8.25f);
    public static readonly Font Bold = new("Microsoft Sans Serif", 8.25f, FontStyle.Bold);
    public static readonly Font Large = new("Microsoft Sans Serif", 14f, FontStyle.Regular);
}

/// <summary>
/// Info bar at the top of tab content panels and dialogs. Formerly a blue gradient; now
/// the flat themed banner (<see cref="ThemedBanner"/>).
/// </summary>
internal class GradientInfoBar : ThemedBanner
{
    public GradientInfoBar(string text) : base(text)
    {
    }
}
