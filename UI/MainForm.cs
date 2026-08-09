using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Main settings form with collapsed/expanded modes.
/// Collapsed: header + quick-menu checkbox + preset buttons + action buttons.
/// Expanded: additionally shows a TabControl with 5 empty tabs.
/// </summary>
internal class MainForm : Form
{
    private readonly Panel _headerPanel;
    readonly Label _logoLabel;

    private readonly CheckBox _quickMenuCheckBox;

    private readonly Panel _presetPanel;
    private readonly Button _btnRecommended;
    private readonly Button _btnSmallFile;
    private readonly Button _btnHighQuality;
    private readonly Button _btnCustom;

    private readonly ComboBox _profileDropdown;
    private readonly Label _profileLabel;
    private readonly Panel _profileSelectorPanel;

    private readonly TabControl _tabControl;
    private ImageList? _tabImageList;
    private SaveTabContent? _saveTabContent;
    private ApplicationTabContent? _applicationTabContent;
    private readonly ScanSettings _scanSettings;

    public ScanSettings Settings => _scanSettings;
    public SaveTabContent? SaveTab => _saveTabContent;
    public ApplicationTabContent? ApplicationTab => _applicationTabContent;
    public PaperTabContent? PaperTab => _paperTabContent;
    private ScanModeTabContent? _scanModeTabContent;
    private FileTypeTabContent? _fileTypeTabContent;
    private PaperTabContent? _paperTabContent;
    private FileSizeTabContent? _fileSizeTabContent;

    private readonly Button _detailButton;
    private readonly Button _okButton;
    private readonly Button _cancelButton;
    private readonly Button _applyButton;
    private Image? _downArrowIcon;
    private Image? _upArrowIcon;

    private bool _expanded;
    private Button? _activePreset;
    private readonly ProfileManager _profileManager;
    private ScanSettings? _snapshot;

    // Layout constants
    private const int ClientWidth = 686;
    private const int ExpandedClientHeight = 463;
    private const int CollapsedClientHeight = 173; // 133 top + 40 footer
    private const int HeaderHeight = 44; // white logo zone
    private const int TopPanelHeight = 133; // 44 white + 89 gray
    private const int BottomBarHeight = 40;
    private const int SeparatorHeight = 2;
    private const int PresetMargin = 18;
    private const int PresetSpacing = 8;

    private readonly Panel _topPanel;
    private readonly Panel _separatorPanel;
    private readonly Panel _bottomPanel;

    public MainForm(ScanSettings? settings = null)
    {
        _scanSettings = settings ?? new ScanSettings();
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        HelpButton = true;
        HelpButtonClicked += (s, e) =>
        {
            e.Cancel = true;
            using var help = new HelpForm();
            help.ShowDialog(this);
        };
        StartPosition = FormStartPosition.CenterScreen;
        Text = "SpeedScan Manager – Einstellungen";
        ClientSize = new Size(ClientWidth, CollapsedClientHeight);
        ShowInTaskbar = true;
        Font = new Font("Microsoft Sans Serif", 8.25f);

        var contentFont = new Font("Microsoft Sans Serif", 8.25f);
        var presetFont = new Font("Microsoft Sans Serif", 8.25f);

        // === Top panel (gray control zone, contains white header + checkbox + presets + separator) ===
        _topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = TopPanelHeight,
            BackColor = Color.FromArgb(235, 235, 235)
        };

        // === White logo area ===
        _headerPanel = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(ClientWidth, HeaderHeight),
            BackColor = Color.White
        };

        _logoLabel = new Label
        {
            Text = "SpeedScan Manager",
            Font = new Font("Microsoft Sans Serif", 14f, FontStyle.Bold),
            ForeColor = Color.FromArgb(35, 90, 170),
            AutoSize = true,
            Location = new Point(16, 10)
        };
        _headerPanel.Controls.Add(_logoLabel);
        _topPanel.Controls.Add(_headerPanel);

        // === Quick-Menü checkbox (in gray zone) ===
        _quickMenuCheckBox = new CheckBox
        {
            Text = "Quick-Menü verwenden",
            Checked = true,
            Location = new Point(18, 56), // 44px header + 12px into gray zone
            AutoSize = true,
            Font = contentFont
        };
        _topPanel.Controls.Add(_quickMenuCheckBox);

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
            Padding = Padding.Empty,
            BackColor = Color.FromArgb(235, 235, 235)
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

        // Default selection: Benutzerdefiniert
        SetActivePreset(_btnCustom);

        // === Separator line under preset buttons ===
        _separatorPanel = new Panel
        {
            Location = new Point(0, 124), // still at bottom of top panel
            Size = new Size(ClientWidth, SeparatorHeight),
            BackColor = Color.FromArgb(160, 175, 195)
        };
        _topPanel.Controls.Add(_separatorPanel);

        // === Profile manager ===
        _profileManager = new ProfileManager();

        // === Profile dropdown (shown in 4th column when Quick-Menü is off) ===
        _profileLabel = new Label
        {
            Text = "Profil:",
            AutoSize = true,
            Font = contentFont,
            Anchor = AnchorStyles.Left,
            Padding = new Padding(0, 3, 4, 0)
        };

        _profileDropdown = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = contentFont,
            Dock = DockStyle.Fill
        };

        _profileSelectorPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Visible = false,
            BackColor = Color.FromArgb(235, 235, 235)
        };
        var profileLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = false,
            Padding = new Padding(0, 1, 0, 0),
            BackColor = Color.FromArgb(235, 235, 235)
        };
        profileLayout.Controls.Add(_profileLabel);
        profileLayout.Controls.Add(_profileDropdown);
        _profileSelectorPanel.Controls.Add(profileLayout);

        RefreshProfileDropdown();
        _profileDropdown.SelectedIndexChanged += (s, e) => OnProfileSelected();

        // Add profile selector to the 4th column (replaces Benutzerdefiniert when Quick-Menü is off)
        presetTable.Controls.Add(_profileSelectorPanel, 7, 0);

        _topPanel.Controls.Add(_presetPanel);
        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Visible = false,
            Font = contentFont,
            Appearance = TabAppearance.FlatButtons,
            DrawMode = TabDrawMode.OwnerDrawFixed,
            // Normal lets WinForms size every tab independently from its own text.
            // Padding.X must be large enough to account for the 16px icon drawn
            // in TabControl_DrawItem: icon at x+6 (16px) + 3px gap + 5px right
            // padding = 30px total beyond text. WinForms adds Padding.X on both
            // sides, so Padding.X=16 gives textWidth+32px per tab.
            SizeMode = TabSizeMode.Normal,
            BackColor = Color.FromArgb(225, 225, 225),
            Padding = new Point(18, 2)
        };
        _tabControl.DrawItem += TabControl_DrawItem;
        _tabControl.Selecting += TabControl_Selecting;

        // Create ImageList with 16x16 icons for tabs
        _tabImageList = new ImageList
        {
            ImageSize = new Size(16, 16),
            ColorDepth = ColorDepth.Depth32Bit
        };
        _tabImageList.Images.Add("app", TabIcons.CreateAppIcon());
        _tabImageList.Images.Add("save", TabIcons.CreateSaveIcon());
        _tabImageList.Images.Add("scan", TabIcons.CreateScanIcon());
        _tabImageList.Images.Add("file", TabIcons.CreateFileIcon());
        _tabImageList.Images.Add("paper", TabIcons.CreatePaperIcon());
        _tabImageList.Images.Add("size", TabIcons.CreateSizeIcon());
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
                ImageKey = key,
                BackColor = Color.White
            };
            _tabControl.TabPages.Add(page);
        }

        // Populate each tab with content wrapped in an inner panel
        _applicationTabContent = new ApplicationTabContent(_scanSettings);
        WrapTabContent(_tabControl.TabPages[0], _applicationTabContent);

        _saveTabContent = new SaveTabContent();
        WrapTabContent(_tabControl.TabPages[1], _saveTabContent);

        _scanModeTabContent = new ScanModeTabContent(_scanSettings);
        WrapTabContent(_tabControl.TabPages[2], _scanModeTabContent);

        _fileTypeTabContent = new FileTypeTabContent(_scanSettings);
        WrapTabContent(_tabControl.TabPages[3], _fileTypeTabContent);

        // Connect color mode changes to file type tab (JPEG availability)
        _scanModeTabContent.ColorModeChanged += (mode) => _fileTypeTabContent.OnColorModeChanged(mode);

        _paperTabContent = new PaperTabContent(_scanSettings);
        WrapTabContent(_tabControl.TabPages[4], _paperTabContent);

        _fileSizeTabContent = new FileSizeTabContent(_scanSettings);
        WrapTabContent(_tabControl.TabPages[5], _fileSizeTabContent);

        // === Bottom bar ===
        _bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = BottomBarHeight,
            BackColor = Color.FromArgb(235, 235, 235)
        };
        _bottomPanel.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(180, 180, 180));
            e.Graphics.DrawLine(pen, 0, 0, _bottomPanel.Width, 0);
        };

        _downArrowIcon = TabIcons.CreateDownArrowIcon();
        _upArrowIcon = TabIcons.CreateUpArrowIcon();

        _detailButton = new Button
        {
            Text = "Detail",
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            Size = new Size(125, 21),
            Font = contentFont,
            Image = _downArrowIcon,
            ImageAlign = ContentAlignment.MiddleLeft,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(2, 0, 4, 0),
            Dock = DockStyle.Fill,
            Margin = new Padding(18, 9, 0, 9)
        };
        _detailButton.Click += (s, e) => ToggleExpanded();

        _okButton = new Button
        {
            Text = "OK",
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            Size = new Size(116, 21),
            Font = contentFont,
            Image = TabIcons.CreateCheckIcon(),
            ImageAlign = ContentAlignment.MiddleLeft,
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 9, 0, 9)
        };
        _okButton.Click += (s, e) => { ApplySettings(); Close(); };

        _cancelButton = new Button
        {
            Text = "Abbrechen",
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            Size = new Size(116, 21),
            Font = contentFont,
            Image = TabIcons.CreateCrossIcon(),
            ImageAlign = ContentAlignment.MiddleLeft,
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 9, 0, 9)
        };
        _cancelButton.Click += (s, e) => { CancelSettings(); Close(); };

        _applyButton = new Button
        {
            Text = "Übernehmen",
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            Size = new Size(116, 21),
            Font = contentFont,
            Enabled = false,
            Image = TabIcons.CreateApplyIcon(),
            ImageAlign = ContentAlignment.MiddleLeft,
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 9, 18, 9)
        };
        _applyButton.Click += (s, e) => ApplySettings();

        // Take initial snapshot for Cancel restore
        _snapshot = _scanSettings.Clone();

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
            Padding = Padding.Empty,
            BackColor = Color.FromArgb(235, 235, 235)
        };

        bottomTable.Controls.Add(_detailButton, 0, 0);
        bottomTable.Controls.Add(new Panel { BackColor = Color.FromArgb(235, 235, 235), Dock = DockStyle.Fill }, 1, 0);
        bottomTable.Controls.Add(_okButton, 2, 0);
        bottomTable.Controls.Add(_cancelButton, 3, 0);
        bottomTable.Controls.Add(_applyButton, 4, 0);
        bottomTable.Controls.Add(new Panel { BackColor = Color.FromArgb(235, 235, 235), Dock = DockStyle.Fill }, 5, 0);

        _bottomPanel.Controls.Add(bottomTable);

        // Add controls in docking order (first = docked last = Fill)
        Controls.AddRange(new Control[]
        {
            _tabControl,
            _bottomPanel,
            _topPanel
        });
    }

    private Button CreatePresetButton(string text, int x, int width, Font font)
    {
        return new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            Location = new Point(x, 0),
            Size = new Size(width, 23),
            Font = font,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 1, 0, 1)
        };
    }

    private void PresetButton_Click(object? sender, EventArgs e)
    {
        if (sender is Button btn)
        {
            SetActivePreset(btn);

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
            else if (btn == _btnCustom && !_expanded)
            {
                ToggleExpanded();
            }
        }
    }

    private void ApplyPreset(ImageQuality quality, int compression)
    {
        _scanSettings.ImageQuality = quality;
        _scanSettings.CompressionRate = compression;

        // Update UI in tabs if they exist
        _scanModeTabContent?.ApplyPreset(quality);
        _fileSizeTabContent?.ApplyPreset(compression);
    }

    private void SetActivePreset(Button btn)
    {
        // Reset all preset buttons to normal style
        foreach (Control c in _presetPanel.Controls)
        {
            if (c is Button b)
            {
                b.BackColor = SystemColors.Control;
                b.ForeColor = SystemColors.ControlText;
            }
        }

        // Highlight active button with subtle blue tint
        btn.BackColor = Color.FromArgb(220, 230, 245);
        _activePreset = btn;
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

        // Disable Anwendung tab when Quick-Menü is on
        _applicationTabContent!.Enabled = !useQuick;
        _tabControl.Invalidate();

        // If Anwendung tab is selected while Quick-Menü turns on, switch to Speichern
        if (useQuick && _tabControl.SelectedIndex == 0)
            _tabControl.SelectedIndex = 1;
    }

    private void RefreshProfileDropdown()
    {
        _profileDropdown.Items.Clear();
        foreach (var profile in _profileManager.Profiles)
        {
            _profileDropdown.Items.Add(profile.Name);
        }
        // Add separator items
        _profileDropdown.Items.Add("-");
        _profileDropdown.Items.Add("Profil hinzufügen...");
        _profileDropdown.Items.Add("Profilverwaltung...");
        _profileDropdown.SelectedIndex = 0;
    }

    private void OnProfileSelected()
    {
        int idx = _profileDropdown.SelectedIndex;
        if (idx < 0) return;

        int profileCount = _profileManager.Profiles.Count;

        // Check if it's a special entry
        if (idx == profileCount + 1) // "Profil hinzufügen..."
        {
            AddProfile();
            // Revert selection to first profile
            _profileDropdown.SelectedIndex = 0;
            return;
        }

        if (idx == profileCount + 2) // "Profilverwaltung..."
        {
            OpenProfileManagement();
            // Revert selection to first profile
            _profileDropdown.SelectedIndex = 0;
            return;
        }

        // Skip separator
        if (idx == profileCount)
        {
            _profileDropdown.SelectedIndex = 0;
            return;
        }

        // Load selected profile
        if (idx < profileCount)
        {
            LoadProfile(_profileManager.Profiles[idx]);
        }
    }

    private void LoadProfile(ScanProfile profile)
    {
        profile.ApplyTo(_scanSettings);

        // Restore save tab
        _saveTabContent?.RestoreSaveConfig(
            profile.FolderPath,
            profile.FileNameFormat,
            profile.CustomFileName,
            profile.CounterDigits);

        // Update tab UIs
        _scanModeTabContent?.ApplyPreset(profile.ImageQuality);
        _fileSizeTabContent?.ApplyPreset(profile.CompressionRate);
    }

    private void AddProfile()
    {
        string? name = ShowInputDialog("Profil hinzufügen", "Profilname:", "");
        if (string.IsNullOrWhiteSpace(name)) return;

        // Capture current settings
        var (folder, formatMode, customName, digits) = _saveTabContent?.GetSaveConfig()
            ?? ("", FileNameFormatDialog.FormatMode.Timestamp, "unbenannt", 3);

        var profile = ScanProfile.FromCurrent(
            _scanSettings, folder, formatMode, customName, digits,
            ApplicationType.ScanToFolder, name.Trim(), isBuiltIn: false);

        _profileManager.AddProfile(profile);
        RefreshProfileDropdown();
    }

    private void OpenProfileManagement()
    {
        using var dlg = new ProfileManagementDialog(_profileManager);
        dlg.ShowDialog(this);
        RefreshProfileDropdown();
    }

    private string? ShowInputDialog(string title, string label, string defaultValue)
    {
        using var dlg = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(320, 140),
            ShowInTaskbar = false,
            AutoScaleMode = AutoScaleMode.None
        };

        var dlgFont = new Font("Microsoft Sans Serif", 8.25f);
        var lbl = new Label { Text = label, Location = new Point(12, 12), AutoSize = true, Font = dlgFont };
        var txt = new TextBox { Text = defaultValue, Location = new Point(12, 34), Size = new Size(280, 24), Font = dlgFont };
        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(130, 68), Size = new Size(75, 28), Font = dlgFont };
        var btnCancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, Location = new Point(217, 68), Size = new Size(75, 28), Font = dlgFont };

        dlg.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
        dlg.AcceptButton = btnOk;
        dlg.CancelButton = btnCancel;

        return dlg.ShowDialog(this) == DialogResult.OK ? txt.Text : null;
    }

    private void ToggleExpanded()
    {
        _expanded = !_expanded;

        if (_expanded)
        {
            ClientSize = new Size(ClientWidth, ExpandedClientHeight);
            _detailButton.Text = "Ausblenden";
            _detailButton.Image = _upArrowIcon;
            _tabControl.Visible = true;
        }
        else
        {
            ClientSize = new Size(ClientWidth, CollapsedClientHeight);
            _detailButton.Text = "Detail";
            _detailButton.Image = _downArrowIcon;
            _tabControl.Visible = false;
        }
    }

    private void WrapTabContent(TabPage page, Control content)
    {
        var innerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(0)
        };
        innerPanel.Controls.Add(content);
        page.Controls.Add(innerPanel);
    }

    private void ApplySettings()
    {
        // Settings are already live in _scanSettings via direct modification.
        // Take a new snapshot so Cancel won't revert past this point.
        _snapshot = _scanSettings.Clone();
        _applyButton.Enabled = false;
    }

    private void CancelSettings()
    {
        // Restore settings to the last applied state
        if (_snapshot != null)
            _scanSettings.RestoreFrom(_snapshot);
    }

    private void OnSettingsChanged()
    {
        _applyButton.Enabled = true;
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
            }
            // Recurse into children
            WireControlChanges(c);
        }
    }

    private void WireTabContentChanges()
    {
        // Wire quick-menu checkbox
        _quickMenuCheckBox.CheckedChanged += (s, e) => OnSettingsChanged();

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
            e.Cancel = true;
    }

    private void TabControl_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _tabControl.TabPages.Count) return;

        var page = _tabControl.TabPages[e.Index];
        var tabRect = _tabControl.GetTabRect(e.Index);
        bool isSelected = _tabControl.SelectedIndex == e.Index;

        // Background
        Color bgColor = isSelected ? Color.White : Color.FromArgb(225, 225, 225);
        using var brush = new SolidBrush(bgColor);
        e.Graphics.FillRectangle(brush, tabRect);

        // Separator line between tabs (right edge, except last tab)
        if (e.Index < _tabControl.TabPages.Count - 1 && !isSelected)
        {
            using var pen = new Pen(Color.FromArgb(190, 195, 200));
            e.Graphics.DrawLine(pen, tabRect.Right - 1, tabRect.Top + 4, tabRect.Right - 1, tabRect.Bottom - 4);
        }

        // Blue accent line on top of selected tab
        if (isSelected)
        {
            using var pen = new Pen(Color.FromArgb(45, 90, 170), 2f);
            e.Graphics.DrawLine(pen, tabRect.Left, tabRect.Top, tabRect.Right, tabRect.Top);
        }

        // Icon
        if (_tabImageList != null && e.Index < _tabImageList.Images.Count)
        {
            var icon = _tabImageList.Images[e.Index];
            int iconY = tabRect.Y + (tabRect.Height - 16) / 2;
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            e.Graphics.DrawImage(icon, tabRect.X + 6, iconY, 16, 16);
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Default;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Default;
        }

        // Text — gray out Anwendung tab when Quick-Menü is on
        bool isDisabled = (e.Index == 0 && _quickMenuCheckBox.Checked);
        Color textColor = isDisabled ? Color.FromArgb(160, 160, 160)
                       : isSelected ? Color.Black : Color.FromArgb(90, 95, 105);
        // Use the same regular font as TabControl.Font. This is also the font
        // WinForms uses when calculating variable tab widths in Normal mode.
        using var textFont = new Font(_tabControl.Font, FontStyle.Regular);
        // Icon at x+6 (16px wide), text starts at x+25, right padding 5px.
        // Keep the text rectangle inside the actual, individually sized tab.
        int textLeft = tabRect.Left + 25;
        int textRightPadding = 5;
        var textRect = new Rectangle(
            textLeft,
            tabRect.Top,
            Math.Max(0, tabRect.Right - textLeft - textRightPadding),
            tabRect.Height);
        TextRenderer.DrawText(e.Graphics, page.Text, textFont, textRect, textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tabImageList?.Dispose();
            _downArrowIcon?.Dispose();
            _upArrowIcon?.Dispose();
            _okButton.Image?.Dispose();
            _cancelButton.Image?.Dispose();
            _applyButton.Image?.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Reusable gradient info bar for tab content panels.
/// Paints a blue gradient from (80,110,160) to (140,165,200) with white bold text.
/// </summary>
internal class GradientInfoBar : Panel
{
    private readonly string _text;
    private static readonly Font InfoFont = new("Microsoft Sans Serif", 8.25f, FontStyle.Bold);

    public GradientInfoBar(string text)
    {
        _text = text;
        Dock = DockStyle.Top;
        Height = 18;
        BackColor = Color.FromArgb(105, 135, 175);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var rect = new Rectangle(0, 0, Width, Height);
        using var brush = new LinearGradientBrush(rect,
            Color.FromArgb(105, 135, 175),
            Color.FromArgb(115, 145, 185),
            LinearGradientMode.Vertical);
        e.Graphics.FillRectangle(brush, rect);

        var textRect = new Rectangle(7, 0, Width - 10, Height);
        TextRenderer.DrawText(e.Graphics, _text, InfoFont, textRect, Color.White,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            InfoFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
