using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Settings window (Discord settings style, <see cref="ModernWindow"/>): sidebar navigation on
/// the left, one page per area on the right (Allgemein, Anwendung, Speichern, Scanmodus,
/// Dateiart, Papier, Dateigröße), footer with Abbrechen / Übernehmen / OK.
/// All pages edit a cloned working copy of the settings; only OK/Übernehmen copy it into
/// the live settings and raise <see cref="SettingsApplied"/>. Abbrechen, Esc and the
/// window's close button discard unapplied changes.
/// </summary>
internal class MainForm : ModernWindow, IOwnedWindowHost
{
    private const int PageGeneral = 0;
    private const int PageApplication = 1;
    private const int PageSave = 2;
    private const int PageScanMode = 3;
    private const int PageFileType = 4;
    private const int PagePaper = 5;
    private const int PageFileSize = 6;

    private static readonly string[] PageHelpTopics =
    {
        "settings", "app-tab", "save-tab", "scanmode-tab", "filetype-tab", "paper-tab", "filesize-tab"
    };

    private readonly CheckBox _quickMenuCheckBox;
    private readonly CheckBox _autoStartCheckBox;
    private readonly CheckBox _updateCheckBox;

    private readonly TileButton _btnRecommended;
    private readonly TileButton _btnSmallFile;
    private readonly TileButton _btnHighQuality;
    private readonly TileButton _btnCustom;

    private readonly ComboBox _profileDropdown;
    private readonly SettingsCard _profileCard;
    private readonly SettingsCard _startCard;
    private TutorialOverlay? _tutorial;

    private readonly NavSidebar _nav;
    private readonly Panel _pageHost;
    private readonly List<Panel> _pages = new();
    private readonly SaveTabContent _saveTabContent;
    private readonly ApplicationTabContent _applicationTabContent;
    private readonly ScanModeTabContent _scanModeTabContent;
    private readonly FileTypeTabContent _fileTypeTabContent;
    private readonly PaperTabContent _paperTabContent;
    private readonly FileSizeTabContent _fileSizeTabContent;

    /// <summary>Live settings owned by the tray (only written on OK/Übernehmen).</summary>
    private readonly ScanSettings _liveSettings;
    /// <summary>Working copy edited by all pages.</summary>
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

    /// <summary>OK/Übernehmen saved another language; the app restarts to show it.</summary>
    public event EventHandler? LanguageChanged;

    private readonly ComboBox _languageDropdown;

    /// <summary>Language shown in the dropdown when the window opened (the one the app runs in).</summary>
    private string SavedLanguage => L.Find(_appSettings.Language)?.Code ?? L.Current.Code;

    private string SelectedLanguage =>
        _languageDropdown.SelectedIndex >= 0 ? L.Languages[_languageDropdown.SelectedIndex].Code : SavedLanguage;

    /// <summary>
    /// Raised when the list of custom applications changed (persist immediately, independent
    /// of OK/Abbrechen like ScanSnap).
    /// </summary>
    public event EventHandler? CustomApplicationsChanged;

    /// <summary>The round scan button was clicked (unsaved changes were handled before).</summary>
    public event EventHandler? ScanRequested;

    private readonly Control _scanFab;
    private bool _closeDecided;

    private readonly Button _okButton;
    private readonly Button _cancelButton;
    private readonly Button _applyButton;

    private readonly ComboBox _themeDropdown;

    private bool _suppressChange;
    private bool _suppressProfileEvent;
    private bool _suppressThemeEvent;

    public MainForm(ScanSettings liveSettings, ProfileManager profileManager, AppSettings appSettings)
    {
        _liveSettings = liveSettings;
        _liveSettings.Normalize();
        _working = liveSettings.Clone();
        _sessionBaseline = _working.Clone();
        _profileManager = profileManager;
        _appSettings = appSettings;
        _activeProfile = _profileManager.GetByName(_appSettings.SelectedProfileName);
        // Profilverwaltung isn't modal any more: follow its changes while both are open
        _profileManager.Changed += OnProfilesChanged;
        FormClosed += (_, _) => _profileManager.Changed -= OnProfilesChanged;

        SuspendLayout();
        StartPosition = FormStartPosition.CenterScreen;
        Text = L.T("SpeedScan Manager – Einstellungen");
        Title = L.T("Einstellungen");
        Subtitle = "SpeedScan Manager";
        HeaderButtons = ModernHeaderButtons.Help | ModernHeaderButtons.Minimize | ModernHeaderButtons.Close;
        HelpClicked += (s, e) =>
        {
            int page = Math.Max(0, _nav!.SelectedIndex);
            var help = new HelpForm(PageHelpTopics[page], offerTour: true);
            OwnedWindows.Show(help, this, key: "help:main", onClosed: () =>
            {
                if (help.TourRequested && !IsDisposed && Visible)
                    BeginInvoke(StartTutorial);
            });
        };
        ShowInTaskbar = true;
        // Every page fits without scrolling at this size (compact cards, two columns on Allgemein).
        ClientSize = new Size(920, 706);

        // === Sidebar + page area ===
        _nav = new NavSidebar { Dock = DockStyle.Left, Width = 212 };
        _pageHost = new Panel { Dock = DockStyle.Fill };
        Body.Controls.Add(_pageHost);
        Body.Controls.Add(_nav);

        _nav.AddHeading("SpeedScan Manager");
        _nav.AddItem(L.T("Allgemein"), Glyph.Gear, IconColors.Slate);
        _nav.AddHeading(L.T("Scaneinstellungen"));
        _nav.AddItem(L.T("Anwendung"), Glyph.Apps, IconColors.Purple);
        _nav.AddItem(L.T("Speichern"), Glyph.Folder, IconColors.Amber);
        _nav.AddItem(L.T("Scanmodus"), Glyph.Scanner, IconColors.Blue);
        _nav.AddItem(L.T("Dateiart"), Glyph.FileType, IconColors.Red);
        _nav.AddItem(L.T("Papier"), Glyph.Paper, IconColors.Teal);
        _nav.AddItem(L.T("Dateigröße"), Glyph.Compress, IconColors.Green);

        // === Page "Allgemein" ===
        _quickMenuCheckBox = new ToggleSwitch { Checked = _appSettings.QuickMenuEnabled };
        _autoStartCheckBox = new ToggleSwitch { Checked = _appSettings.AutoStart };
        _updateCheckBox = new ToggleSwitch { Checked = _appSettings.CheckForUpdates };

        // Two columns: start options + design | profiles; the quality presets below.
        _themeDropdown = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 20,
            Size = new Size(130, 24)
        };
        foreach (ThemeMode mode in Enum.GetValues<ThemeMode>())
            _themeDropdown.Items.Add(ThemeManager.GetModeName(mode));
        ComboLists.SetRowHeight(_themeDropdown, ComboLists.IconRow);
        _themeDropdown.DrawItem += (s, e) =>
        {
            if (e.Index < 0)
            {
                ThemeManager.DrawItemBackground(_themeDropdown, e);
                return;
            }
            ComboItemPainter.Draw(_themeDropdown, e, ThemeModeIcon((ThemeMode)e.Index), _themeDropdown.Items[e.Index]?.ToString() ?? "", null);
        };
        _themeDropdown.SelectedIndex = (int)ThemeManager.Mode;
        _themeDropdown.SelectedIndexChanged += (s, e) =>
        {
            // Design selection (System / Hell / Dunkel), applied immediately
            if (!_suppressThemeEvent && _themeDropdown.SelectedIndex >= 0)
                ThemeManager.SetMode((ThemeMode)_themeDropdown.SelectedIndex);
        };

        _languageDropdown = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Size = new Size(130, 24),
            DropDownWidth = 180,
            MaxDropDownItems = 14,
        };
        foreach (var language in L.Languages)
            _languageDropdown.Items.Add(language.NativeName);
        _languageDropdown.SelectedIndex = Math.Max(0, L.Languages.ToList().FindIndex(l => l.Code == SavedLanguage));

        var cardStart = new SettingsCard { Dense = true };
        cardStart.AddRow(L.T("Quick-Menü verwenden"),
            L.T("Nach jedem Scan das Ziel wählen."),
            _quickMenuCheckBox);
        cardStart.AddRow(L.T("Mit Windows starten"),
            L.T("Startet mit der Windows-Anmeldung."), _autoStartCheckBox);
        cardStart.AddRow(L.T("Nach Updates suchen"),
            L.T("Hinweis bei einer neuen Version."), _updateCheckBox);
        cardStart.AddRow(L.T("Design"), L.T("„System“ folgt Windows."), _themeDropdown, 130);
        cardStart.AddRow(L.T("Sprache"), L.T("Wird nach einem Neustart übernommen."), _languageDropdown, 130);
        _startCard = cardStart;

        _profileDropdown = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Size = new Size(160, 24),
            DropDownWidth = 240,
            MaxDropDownItems = 16
        };
        RefreshProfileDropdown();
        _profileDropdown.SelectedIndexChanged += (s, e) => OnProfileSelected();

        var btnAddProfile = ModernButtons.SubDialog(L.T("Profil hinzufügen..."));
        btnAddProfile.Click += (s, e) => AddProfile();
        var btnManageProfiles = ModernButtons.SubDialog("Profilverwaltung...");
        btnManageProfiles.Click += (s, e) => OpenProfileManagement();
        var profileButtons = new ButtonRow { Height = 30 };
        profileButtons.Controls.Add(btnAddProfile);
        profileButtons.Controls.Add(btnManageProfiles);

        _profileCard = new SettingsCard(L.T("Profil"), L.T("Speichert alle Scaneinstellungen (nur ohne Quick-Menü).")) { Dense = true, Icon = ColorIcon.Tile(Glyph.User, IconColors.Blue) };
        _profileCard.AddRow(L.T("Aktives Profil"), null, _profileDropdown, 160);
        _profileCard.AddRow("", null, profileButtons).Stacked = true;

        var topRow = new ModernColumns();
        topRow.Controls.Add(cardStart);
        topRow.Controls.Add(_profileCard);

        // Duotone chips: Empfohlen = award (amber), Kleine Datei = squeezed file (green),
        // Hohe Bildqualität unchanged (accent), Benutzerdefiniert = sliders (violet).
        _btnRecommended = CreatePresetTile(L.T("Empfohlen"), Glyph.Award, L.T("Automatisch\nKomprimierung 3"), IconColors.Amber);
        _btnSmallFile = CreatePresetTile(L.T("Kleine Datei"), Glyph.FileZip, L.T("Normal\nKomprimierung 5"), IconColors.Green);
        _btnHighQuality = CreatePresetTile(L.T("Hohe Bildqualität"), Glyph.ImageHq, L.T("Fein\nKomprimierung 1"), null);
        _btnCustom = CreatePresetTile(L.T("Benutzerdefiniert"), Glyph.Sliders, L.T("Eigene\nEinstellungen"), IconColors.Purple);
        var presetGroup = new TileGroup { Columns = 4, TileHeight = 116, VerticalPadding = 4 };
        presetGroup.Controls.AddRange(new Control[] { _btnRecommended, _btnSmallFile, _btnHighQuality, _btnCustom });
        foreach (var tile in new[] { _btnRecommended, _btnSmallFile, _btnHighQuality, _btnCustom })
        {
            tile.Click += PresetButton_Click;
            tile.Click += (s, e) => OnSettingsChanged();
        }
        var cardQuality = new SettingsCard(L.T("Scanqualität"), L.T("Voreinstellungen für Bildqualität und Komprimierungsrate.")) { ShowDividers = false, Dense = true, Icon = ColorIcon.Tile(Glyph.Star, IconColors.Pink) };
        cardQuality.Add(presetGroup);
        cardQuality.Add(new Panel { Height = 10 });

        var general = new ModernStack { Spacing = 14 };
        general.Controls.AddRange(new Control[] { topRow, cardQuality });

        // === Scan setting pages ===
        _applicationTabContent = new ApplicationTabContent(_working, _appSettings.CustomApplications);
        _applicationTabContent.CustomApplicationsChanged += (s, e) => CustomApplicationsChanged?.Invoke(this, EventArgs.Empty);
        _saveTabContent = new SaveTabContent(_working);
        _scanModeTabContent = new ScanModeTabContent(_working);
        _fileTypeTabContent = new FileTypeTabContent(_working);
        _paperTabContent = new PaperTabContent(_working);
        _fileSizeTabContent = new FileSizeTabContent(_working);

        AddPage(L.T("Allgemein"), L.T("Start, Quick-Menü, Scanqualität, Profile und Design."), general);
        AddPage(L.T("Anwendung"), L.T("Wählen Sie eine Anwendung."), _applicationTabContent);
        AddPage(L.T("Speichern"), L.T("Geben Sie den zu benutzenden Ordner und das gewünschte Dateinamenformat an."), _saveTabContent);
        AddPage(L.T("Scanmodus"), L.T("Wählen Sie den gewünschten Scanmodus."), _scanModeTabContent);
        AddPage(L.T("Dateiart"), L.T("Geben Sie die gewünschten Dateioptionen an."), _fileTypeTabContent);
        AddPage(L.T("Papier"), L.T("Wählen Sie ein Papier."), _paperTabContent);
        AddPage(L.T("Dateigröße"), L.T("Geben Sie die Komprimierungsrate an (nur für Farb- und Graustufenbilder)."), _fileSizeTabContent);
        _nav.SelectedIndexChanged += (s, e) => ShowPage(_nav.SelectedIndex);

        // Cross-page dependencies
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

        // === Footer: Abbrechen / Übernehmen / OK ===
        _cancelButton = AddFooterButton(L.T("Abbrechen"), ThemeRole.Secondary, Glyph.Cross);
        // Asks in OnFormClosing whether unsaved changes should be saved.
        _cancelButton.Click += (s, e) => Close();

        _applyButton = AddFooterButton(L.T("Übernehmen"), ThemeRole.Secondary, Glyph.Apply);
        _applyButton.Enabled = false;
        _applyButton.Click += (s, e) => ApplySettings();

        _okButton = AddFooterButton("OK", ThemeRole.Primary, Glyph.Check);
        _okButton.Click += (s, e) => { if (ApplySettings()) Close(); };

        _scanFab = AddFooterFab(Glyph.Scanner, L.T("Jetzt scannen"));
        _scanFab.Click += (s, e) => OnScanClick();

        // Wire page content changes to enable the Apply button
        WireTabContentChanges();

        AcceptButton = _okButton;
        CancelButton = _cancelButton; // Esc = Abbrechen

        LoadAllTabs();
        _nav.SelectedIndex = PageGeneral;
        OnQuickMenuToggled();
        UpdateApplyButton();

        ThemeManager.Apply(this);
        ThemeManager.ModeChanged += OnThemeModeChanged;

        ResumeLayout(false);
        PerformLayout();
    }

    private void OnScanClick()
    {
        if (HasUnappliedChanges())
        {
            switch (UnsavedChangesDialog.Ask(this, UnsavedChangesDialog.Purpose.Scan, DescribeChanges()))
            {
                case UnsavedChangesDialog.Choice.Save:
                    if (!ApplySettings()) return;
                    break;
                case UnsavedChangesDialog.Choice.Discard:
                    CancelSettings();
                    break;
                case UnsavedChangesDialog.Choice.KeepUnsaved:
                    break;
                default:
                    return;
            }
        }
        ScanRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!e.Cancel && !_closeDecided && e.CloseReason == CloseReason.UserClosing && HasUnappliedChanges())
        {
            switch (UnsavedChangesDialog.Ask(this, UnsavedChangesDialog.Purpose.Close, DescribeChanges()))
            {
                case UnsavedChangesDialog.Choice.Save:
                    if (!ApplySettings()) e.Cancel = true;
                    break;
                case UnsavedChangesDialog.Choice.Discard:
                    CancelSettings();
                    break;
                default:
                    e.Cancel = true;
                    break;
            }
        }
        base.OnFormClosing(e);
    }

    /// <summary>Closes without asking; unsaved changes are discarded (app exit).</summary>
    public void CloseDiscarding()
    {
        _closeDecided = true;
        Close();
    }

    /// <summary>
    /// Guided tour through the settings window (first start, or "Tour starten" in the help).
    /// Only switches pages; settings are not changed.
    /// </summary>
    public void StartTutorial()
    {
        if (_tutorial != null || IsDisposed) return;
        // After the window is shown and laid out
        if (!Visible || !IsHandleCreated)
        {
            Shown += (_, _) => BeginInvoke(StartTutorial);
            return;
        }
        if (WindowState == FormWindowState.Minimized)
            WindowState = FormWindowState.Normal;

        int startPage = Math.Max(0, _nav.SelectedIndex);
        Rectangle Screen(Control c, Rectangle r) => c.RectangleToScreen(r);
        Func<Rectangle?> Nav(int page) => () => Screen(_nav, _nav.GetItemBounds(page));
        Action Page(int page) => () => _nav.SelectedIndex = page;
        Func<Rectangle?> Of(Control c) => () => c.Visible ? Screen(c, c.ClientRectangle) : null;

        var steps = new List<TutorialOverlay.Step>
        {
            new(L.T("Willkommen bei SpeedScan Manager"),
                L.T("Eine kurze Tour durch die wichtigsten Einstellungen. Du kannst sie jederzeit überspringen und später über die Hilfe (?) wieder starten."),
                Glyph.Sparkle, IconColors.Purple, null, Page(PageGeneral)),
            new(L.T("Anwendung"),
                L.T("Hier legst du fest, was nach dem Scan passiert: in einem Ordner speichern, per E-Mail senden, drucken oder in einem Programm öffnen. Das gilt, solange das Quick-Menü aus ist."),
                Glyph.Apps, IconColors.Purple, Nav(PageApplication), Page(PageApplication)),
            new(L.T("Speichern"),
                L.T("Ordner und Dateiname deiner Scans. Ohne Änderung landen sie in „Dokumente\\SpeedScanManager“."),
                Glyph.Folder, IconColors.Amber, Nav(PageSave), Page(PageSave)),
            new(L.T("Scanmodus"),
                L.T("Farbe, Auflösung und ob eine oder beide Seiten gescannt werden. Unter „Optionen...“ stellst du z. B. das automatische Drehen ein."),
                Glyph.Scanner, IconColors.Blue, Nav(PageScanMode), Page(PageScanMode)),
            new(L.T("Dateiart"),
                L.T("PDF oder Bild. Mit Texterkennung wird die PDF durchsuchbar und du kannst Text daraus kopieren."),
                Glyph.FileType, IconColors.Red, Nav(PageFileType), Page(PageFileType)),
            new(L.T("Quick-Menü – jetzt an"),
                L.T("Ich habe es für dich eingeschaltet. Nach jedem Scan erscheint ein kleines Menü, in dem du wählst, wohin das Dokument geht: Ordner, E-Mail, Drucker oder ein Programm.\n\n")
                + L.T("+  Flexibel: jedes Dokument kann woanders hin.\n")
                + L.T("+  Du siehst sofort, dass der Scan fertig ist.\n")
                + L.T("–  Ein Klick mehr pro Scan.\n\n")
                + L.T("Aus ist es ideal, wenn du immer gleich scannst: Dann gilt ohne Nachfrage die Seite „Anwendung“. Gespeichert wird es erst mit OK."),
                Glyph.Apps, IconColors.Pink, Of(_quickMenuCheckBox.Parent ?? _startCard), () =>
                {
                    _nav.SelectedIndex = PageGeneral;
                    _quickMenuCheckBox.Checked = true;
                }),
            new(L.T("Scannen"),
                L.T("Papier einlegen und auf den runden Scan-Button klicken – oder einfach die Scan-Taste am Scanner drücken. Auch über das SpeedScan-Symbol unten rechts in der Taskleiste kannst du scannen."),
                Glyph.Scanner, IconColors.Green, () => ((FooterFab)_scanFab).ButtonScreenBounds, Page(PageGeneral), Round: true),
            new(L.T("Hilfe"),
                L.T("Das Fragezeichen öffnet die Hilfe mit Bildern zur jeweiligen Seite. Dort findest du auch „Tour starten“."),
                Glyph.Question, IconColors.Blue, Of(HeaderHelpButton), null),
            new("Fertig!",
                L.T("OK speichert deine Einstellungen und schließt das Fenster, „Übernehmen“ speichert nur. Viel Spaß beim Scannen!"),
                Glyph.Check, IconColors.Green, Of(_okButton), null),
        };

        _tutorial = new TutorialOverlay(this, steps);
        _tutorial.Finished += (_, _) =>
        {
            _tutorial = null;
            if (!IsDisposed) _nav.SelectedIndex = startPage;
        };
        _tutorial.Start();
    }

    /// <summary>Test helper (screenshots): the tour without showing it.</summary>
    internal TutorialOverlay? Tutorial => _tutorial;

    private void OnThemeModeChanged(object? sender, EventArgs e)
    {
        _suppressThemeEvent = true;
        try { _themeDropdown.SelectedIndex = (int)ThemeManager.Mode; }
        finally { _suppressThemeEvent = false; }
    }

    /// <summary>Adds a scrollable page: title, description and the page content.</summary>
    private void AddPage(string title, string description, Control content)
    {
        var host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Visible = false };
        var stack = new ModernStack { Dock = DockStyle.Top, Padding = new Padding(28, 20, 28, 20), Spacing = 14 };
        stack.Controls.Add(new PageHeader(title, description));
        stack.Controls.Add(content);
        host.Controls.Add(stack);
        _pages.Add(host);
        _pageHost.Controls.Add(host);
    }

    private void ShowPage(int index)
    {
        if (index < 0 || index >= _pages.Count) return;
        _pageHost.SuspendLayout();
        for (int i = 0; i < _pages.Count; i++)
        {
            if (i == index)
            {
                _pages[i].AutoScrollPosition = Point.Empty;
                _pages[i].Visible = true;
            }
        }
        for (int i = 0; i < _pages.Count; i++)
            if (i != index) _pages[i].Visible = false;
        _pageHost.ResumeLayout(true);
    }

    /// <summary>Ctrl+Tab / Ctrl+Shift+Tab / Ctrl+PageDown / Ctrl+PageUp switch pages (like the former tabs).</summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        int step = keyData switch
        {
            Keys.Control | Keys.Tab or Keys.Control | Keys.PageDown => 1,
            Keys.Control | Keys.Shift | Keys.Tab or Keys.Control | Keys.PageUp => -1,
            _ => 0
        };
        if (step != 0)
        {
            int count = _nav.ItemCount;
            int i = _nav.SelectedIndex;
            for (int n = 0; n < count; n++)
            {
                i = (i + step + count) % count;
                if (_nav.IsItemEnabled(i))
                {
                    _nav.SelectedIndex = i;
                    break;
                }
            }
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private static TileButton CreatePresetTile(string text, Glyph glyph, string description, Color? color)
        => new(text, null, glyph, description) { Focusable = true, GlyphColor = color };

    private static readonly ColorIcon[] ThemeModeIcons =
    {
        ColorIcon.Tile(Glyph.Monitor, IconColors.Slate),
        ColorIcon.Tile(Glyph.Sun, IconColors.Amber),
        ColorIcon.Tile(Glyph.Moon, IconColors.Indigo)
    };

    private static ColorIcon? ThemeModeIcon(ThemeMode mode)
        => (int)mode >= 0 && (int)mode < ThemeModeIcons.Length ? ThemeModeIcons[(int)mode] : null;

    private void PresetButton_Click(object? sender, EventArgs e)
    {
        if (sender is TileButton btn)
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
                // Formerly expanded the detail view; the detailed settings are the scan pages.
                _nav.SelectedIndex = PageScanMode;
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
    /// Highlights the preset tile that matches the current settings
    /// (Benutzerdefiniert when no preset matches).
    /// </summary>
    private void UpdatePresetHighlight()
    {
        TileButton active = (_working.ImageQuality, _working.CompressionRate) switch
        {
            (ImageQuality.Automatic, 3) => _btnRecommended,
            (ImageQuality.Normal, 5) => _btnSmallFile,
            (ImageQuality.Fine, 1) => _btnHighQuality,
            _ => _btnCustom
        };

        foreach (var b in new[] { _btnRecommended, _btnSmallFile, _btnHighQuality, _btnCustom })
            b.Selected = b == active;
    }

    private void OnQuickMenuToggled()
    {
        bool useQuick = _quickMenuCheckBox.Checked;
        // Profiles are only used when the Quick-Menü is off
        _profileCard.Enabled = !useQuick;

        // Disable the Anwendung page when Quick-Menü is on
        _applicationTabContent.Enabled = !useQuick;
        _nav.SetItemEnabled(PageApplication, !useQuick);

        // If Anwendung is shown while Quick-Menü turns on, switch to Speichern
        if (useQuick && _nav.SelectedIndex == PageApplication)
            _nav.SelectedIndex = PageSave;
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
        if (idx < 0 || idx >= _profileManager.Profiles.Count) return;

        var profile = _profileManager.Profiles[idx];
        if (!ReferenceEquals(profile, _activeProfile))
            LoadProfile(profile);
    }

    private void LoadProfile(ScanProfile profile)
    {
        OwnedWindows.CancelPrefix("edit:"); // they hold values of the previous profile
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
            TrayMessageBox.Show(this, L.F("Es können höchstens {0} Profile angelegt werden.", ProfileManager.MaxProfiles),
                "SpeedScan Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string? name = ProfileNameInput.Show(this, _profileManager, L.T("Neues Profil hinzufügen"), "Profilname:", "", null);
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
        // This window follows the changes itself (ProfileManager.Changed)
        var dlg = new ProfileManagementDialog(_profileManager) { StartPosition = FormStartPosition.CenterParent };
        if (!_appSettings.ProfileTourShown && OwnedWindows.Find<ProfileManagementDialog>() == null)
        {
            _appSettings.ProfileTourShown = true;
            _appSettings.Save();
            dlg.StartTutorial(); // starts once the window is shown
        }
        OwnedWindows.Show(dlg, this);
    }

    /// <summary>
    /// Refreshes every page from the working copy without triggering change handling.
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
                var answer = TrayMessageBox.Show(this,
                    L.F("Das Standardprofil \"{0}\" kann nicht geändert werden.\n\n", _activeProfile.DisplayName) +
                    L.T("Möchten Sie die geänderten Einstellungen als neues Profil speichern?\n\n") +
                    L.T("Ja: Als neues Profil speichern\n") +
                    L.T("Nein: Nur als aktuelle Einstellungen übernehmen"),
                    "SpeedScan Manager", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (answer == DialogResult.Cancel)
                    return false;
                if (answer == DialogResult.Yes)
                {
                    if (!_profileManager.CanAddProfile)
                    {
                        TrayMessageBox.Show(this, L.F("Es können höchstens {0} Profile angelegt werden.", ProfileManager.MaxProfiles),
                            "SpeedScan Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return false;
                    }
                    string? name = ProfileNameInput.Show(this, _profileManager, L.T("Neues Profil hinzufügen"), "Profilname:", "", null);
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
        _appSettings.CheckForUpdates = _updateCheckBox.Checked;
        bool languageChanged = SelectedLanguage != SavedLanguage;
        if (languageChanged) _appSettings.Language = SelectedLanguage;
        _appSettings.SelectedProfileName = _activeProfile?.Name ?? "";
        _sessionBaseline.RestoreFrom(_working);

        SettingsApplied?.Invoke(this, EventArgs.Empty);
        if (languageChanged)
            BeginInvoke(() => LanguageChanged?.Invoke(this, EventArgs.Empty));
        UpdateApplyButton();
        return true;
    }

    /// <summary>
    /// Discards unapplied edits and reloads all pages from the live settings.
    /// </summary>
    private void CancelSettings()
    {
        OwnedWindows.CancelPrefix("edit:");
        _working.RestoreFrom(_liveSettings);
        _sessionBaseline.RestoreFrom(_working);
        _activeProfile = _profileManager.GetByName(_appSettings.SelectedProfileName);
        _quickMenuCheckBox.Checked = _appSettings.QuickMenuEnabled;
        _autoStartCheckBox.Checked = _appSettings.AutoStart;
        _updateCheckBox.Checked = _appSettings.CheckForUpdates;
        _languageDropdown.SelectedIndex = Math.Max(0, L.Languages.ToList().FindIndex(l => l.Code == SavedLanguage));
        LoadAllTabs();
        RefreshProfileDropdown();
        UpdateApplyButton();
    }

    /// <summary>
    /// What differs from the saved state. Compares the normalized working copy, so setting a
    /// value back to what it was counts as unchanged again.
    /// </summary>
    private List<SettingsChangeList.Change> DescribeChanges()
    {
        var edited = _working.Clone();
        edited.Normalize();
        var list = SettingsChangeList.Of(edited, _liveSettings);
        var general = new List<SettingsChangeList.Change>();
        if (_quickMenuCheckBox.Checked != _appSettings.QuickMenuEnabled)
            general.Add(new(L.T("Allgemein"), L.T("Quick-Menü"), SettingsChangeList.OnOff(_appSettings.QuickMenuEnabled), SettingsChangeList.OnOff(_quickMenuCheckBox.Checked)));
        if (_autoStartCheckBox.Checked != _appSettings.AutoStart)
            general.Add(new(L.T("Allgemein"), L.T("Mit Windows starten"), SettingsChangeList.OnOff(_appSettings.AutoStart), SettingsChangeList.OnOff(_autoStartCheckBox.Checked)));
        if (SelectedLanguage != SavedLanguage)
            general.Add(new(L.T("Allgemein"), L.T("Sprache"), L.Find(SavedLanguage)!.NativeName, L.Find(SelectedLanguage)!.NativeName));
        if (_updateCheckBox.Checked != _appSettings.CheckForUpdates)
            general.Add(new(L.T("Allgemein"), L.T("Nach Updates suchen"), SettingsChangeList.OnOff(_appSettings.CheckForUpdates), SettingsChangeList.OnOff(_updateCheckBox.Checked)));
        if ((_activeProfile?.Name ?? "") != _appSettings.SelectedProfileName)
            general.Add(new(L.T("Allgemein"), L.T("Profil"),
                _profileManager.GetByName(_appSettings.SelectedProfileName)?.DisplayName ?? "–",
                _activeProfile?.DisplayName ?? "–"));
        general.AddRange(list);
        return general;
    }

    private bool HasUnappliedChanges() => DescribeChanges().Count > 0;

    private void UpdateApplyButton()
    {
        bool changed = HasUnappliedChanges();
        _applyButton.Enabled = changed;
        FooterHint = changed ? L.T("Nicht übernommene Änderungen") : "";
    }

    /// <summary>An option window of a page (PDF, Dateiname, …) was closed with OK.</summary>
    void IOwnedWindowHost.OwnedWindowApplied() => OnSettingsChanged();

    private void OnProfilesChanged()
    {
        // Coalesce: profile changes come in bursts (save, rename, …) and during own actions
        if (_profilesRefreshPending || IsDisposed || !IsHandleCreated) return;
        _profilesRefreshPending = true;
        BeginInvoke(() =>
        {
            _profilesRefreshPending = false;
            if (IsDisposed) return;
            RefreshProfileDropdown();
            UpdateApplyButton();
        });
    }

    private bool _profilesRefreshPending;

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
                case ModernSlider slider:
                    slider.ValueChanged += (s, e) => OnSettingsChanged();
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
        // Wire quick-menu and autostart switches
        _quickMenuCheckBox.CheckedChanged += (s, e) => { OnQuickMenuToggled(); OnSettingsChanged(); };
        _autoStartCheckBox.CheckedChanged += (s, e) => OnSettingsChanged();
        _updateCheckBox.CheckedChanged += (s, e) => OnSettingsChanged();
        _languageDropdown.SelectedIndexChanged += (s, e) => OnSettingsChanged();

        // Wire the scan setting pages (the Allgemein page is wired explicitly above)
        foreach (var content in new Control[]
                 {
                     _applicationTabContent, _saveTabContent, _scanModeTabContent,
                     _fileTypeTabContent, _paperTabContent, _fileSizeTabContent
                 })
        {
            WireControlChanges(content);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            ThemeManager.ModeChanged -= OnThemeModeChanged;
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
/// Info bar at the top of dialogs. Formerly a blue gradient; now the flat themed banner
/// (<see cref="ThemedBanner"/>).
/// </summary>
internal class GradientInfoBar : ThemedBanner
{
    public GradientInfoBar(string text) : base(text)
    {
    }
}
