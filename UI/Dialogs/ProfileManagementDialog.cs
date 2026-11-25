using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Profile management window: list profiles, rename, delete, reorder.
/// Modern layout (<see cref="ModernWindow"/>): the profile list on the left, the actions as a
/// button column on the right, "Schließen" in the footer.
/// </summary>
internal class ProfileManagementDialog : ModernWindow
{
    private readonly ListBox _listBox;
    private readonly Button _btnAdd;
    private readonly Button _btnRename;
    private readonly Button _btnDelete;
    private readonly Button _btnUp;
    private readonly Button _btnDown;
    private readonly Button _btnClose;
    private readonly ProfileManager _manager;

    public ProfileManagementDialog(ProfileManager manager)
    {
        _manager = manager;

        SuspendLayout();
        StartPosition = FormStartPosition.CenterParent;
        Text = L.T("SpeedScan Manager – Profilverwaltung");
        Title = L.T("Profilverwaltung");
        Subtitle = L.T("Profile hinzufügen, umbenennen, löschen und sortieren");
        HeaderButtons = ModernHeaderButtons.Close;
        HeaderIcon = ColorIcon.Tile(Glyph.User, IconColors.Blue);
        ClientSize = new Size(520, 430);
        ShowInTaskbar = false;

        // === List ===
        _listBox = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            Margin = new Padding(0, 0, 16, 0)
        };
        // Rows: icon of the profile's scan target (same icons as the Anwendung list), name
        ModernLists.Setup(_listBox, i =>
        {
            var profile = i < _manager.Profiles.Count ? _manager.Profiles[i] : null;
            return (profile?.Settings == null ? null : ColorIcons.Target(profile.Settings.ApplicationType),
                _listBox.Items[i]?.ToString() ?? "", null);
        });
        _listBox.SelectedIndexChanged += (s, e) => UpdateButtonStates();

        // === Buttons ===
        _btnAdd = CreateActionButton(L.T("Hinzufügen..."), Glyph.Plus, IconColors.Green);
        _btnAdd.Click += (s, e) => AddProfile();

        _btnRename = CreateActionButton(L.T("Umbenennen..."), Glyph.TextField, IconColors.Blue);
        _btnRename.Click += (s, e) => RenameProfile();

        _btnDelete = CreateActionButton(L.T("Löschen"), Glyph.Cross);
        ThemeManager.SetRole(_btnDelete, ThemeRole.Danger);
        _btnDelete.Click += (s, e) => DeleteProfile();

        _btnUp = CreateActionButton(L.T("Nach oben"), Glyph.ChevronUp);
        _btnUp.Click += (s, e) => MoveUp();

        _btnDown = CreateActionButton(L.T("Nach unten"), Glyph.ChevronDown);
        _btnDown.Click += (s, e) => MoveDown();

        // Same tab order as before: list, Hinzufügen, Umbenennen, Löschen, Nach oben, Nach unten, Schließen.
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        actions.Controls.AddRange(new Control[] { _btnAdd, _btnRename, _btnDelete, _btnUp, _btnDown });
        _btnDelete.Margin = new Padding(0, 0, 0, 20);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = new Padding(20, 18, 20, 20)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.Controls.Add(_listBox, 0, 0);
        layout.Controls.Add(actions, 1, 0);
        Body.Controls.Add(layout);

        // === Footer ===
        FooterHint = L.F("Höchstens {0} Profile", ProfileManager.MaxProfiles);
        _btnClose = AddFooterButton(L.T("Schließen"), ThemeRole.Primary, Glyph.Check, DialogResult.OK);

        AcceptButton = _btnClose;
        CancelButton = _btnClose;

        RefreshList();
        ThemeManager.Apply(this);
        ResumeLayout(true);

        // The settings window may change profiles while this one is open (it isn't locked)
        _manager.Changed += OnProfilesChanged;
        FormClosed += (_, _) => _manager.Changed -= OnProfilesChanged;
    }

    private void OnProfilesChanged()
    {
        if (IsDisposed) return;
        string? selected = _listBox.SelectedItem?.ToString();
        RefreshList();
        int idx = selected == null ? -1 : _listBox.Items.IndexOf(selected);
        if (idx >= 0) _listBox.SelectedIndex = idx;
    }

    private TutorialOverlay? _tour;

    /// <summary>Guided tour through the profile management (first time it is opened).</summary>
    public void StartTutorial()
    {
        if (_tour != null || IsDisposed) return;
        if (!Visible || !IsHandleCreated)
        {
            Shown += (_, _) => BeginInvoke(StartTutorial);
            return;
        }
        Func<Rectangle?> Of(params Control[] controls) => () =>
        {
            var r = controls[0].RectangleToScreen(controls[0].ClientRectangle);
            foreach (var c in controls.Skip(1))
                r = Rectangle.Union(r, c.RectangleToScreen(c.ClientRectangle));
            return r;
        };

        var steps = new List<TutorialOverlay.Step>
        {
            new(L.T("Profile"),
                L.T("Ein Profil speichert alle Scaneinstellungen unter einem Namen – z. B. „Belege“ in Schwarzweiß und „Fotos“ in Farbe. So wechselst du mit einem Klick zwischen ihnen."),
                Glyph.User, IconColors.Blue),
            new(L.T("Deine Profile"),
                L.T("Hier stehen alle Profile. Die Standardprofile sind fest, eigene Profile kannst du umbenennen, löschen und sortieren."),
                Glyph.Apps, IconColors.Purple, Of(_listBox)),
            new(L.T("Neues Profil"),
                L.T("„Hinzufügen...“ legt ein neues Profil an. Wähle es danach im Einstellungsfenster unter „Aktives Profil“ aus und stell alles ein – mit OK wird es im Profil gespeichert."),
                Glyph.Plus, IconColors.Green, Of(_btnAdd)),
            new(L.T("Umbenennen und löschen"),
                L.T("Für eigene Profile: erst in der Liste auswählen, dann umbenennen oder löschen."),
                Glyph.TextField, IconColors.Blue, Of(_btnRename, _btnDelete)),
            new(L.T("Reihenfolge"),
                L.T("„Nach oben“ und „Nach unten“ sortieren die Profile – in dieser Reihenfolge erscheinen sie auch in der Auswahl."),
                Glyph.ChevronUp, IconColors.Slate, Of(_btnUp, _btnDown)),
            new(L.T("Gut zu wissen"),
                L.T("Profile gelten nur, wenn das Quick-Menü aus ist. Mit dem Quick-Menü wählst du das Ziel nach jedem Scan selbst."),
                Glyph.Check, IconColors.Green, Of(_btnClose)),
        };
        _tour = new TutorialOverlay(this, steps);
        _tour.Finished += (_, _) => _tour = null;
        _tour.Start();
    }

    private static Button CreateActionButton(string text, Glyph glyph, Color? color = null)
    {
        var button = new Button
        {
            Text = text,
            Size = new Size(150, 32),
            Margin = new Padding(0, 0, 0, 8)
        };
        ThemeManager.SetRole(button, ThemeRole.Secondary);
        if (color is Color c) ThemeManager.SetGlyph(button, glyph, c);
        else ThemeManager.SetGlyph(button, glyph);
        return button;
    }

    private void RefreshList()
    {
        _listBox.Items.Clear();
        foreach (var profile in _manager.Profiles)
        {
            _listBox.Items.Add(profile.DisplayName);
        }
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        int idx = _listBox.SelectedIndex;
        bool hasSelection = idx >= 0;
        bool isBuiltIn = hasSelection && idx < _manager.Profiles.Count && _manager.Profiles[idx].IsBuiltIn;

        _btnAdd.Enabled = _manager.CanAddProfile;
        _btnRename.Enabled = hasSelection && !isBuiltIn;
        _btnDelete.Enabled = hasSelection && !isBuiltIn;
        _btnUp.Enabled = hasSelection && idx > 0;
        _btnDown.Enabled = hasSelection && idx < _manager.Profiles.Count - 1;
    }

    private void AddProfile()
    {
        if (!_manager.CanAddProfile)
        {
            TrayMessageBox.Show(this, L.F("Es können höchstens {0} Profile angelegt werden.", ProfileManager.MaxProfiles),
                "SpeedScan Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string? name = ShowInputDialog(L.T("Neues Profil"), L.T("Name des neuen Profils:"), L.T("Neues Profil"), null);
        if (name == null)
            return;

        var settings = new ScanSettings();
        settings.Normalize();
        var profile = ScanProfile.FromCurrent(settings, name, isBuiltIn: false);

        _manager.AddProfile(profile);
        RefreshList();
        _listBox.SelectedIndex = _manager.Profiles.Count - 1;
    }

    private void RenameProfile()
    {
        int idx = _listBox.SelectedIndex;
        if (idx < 0 || idx >= _manager.Profiles.Count) return;

        var profile = _manager.Profiles[idx];
        if (profile.IsBuiltIn) return;
        string currentName = profile.Name;
        string? newName = ShowInputDialog(L.T("Profil umbenennen"), L.T("Neuer Name:"), currentName, profile);

        if (newName != null && newName != currentName)
        {
            _manager.RenameProfile(idx, newName);
            RefreshList();
            _listBox.SelectedIndex = idx;
        }
    }

    private void DeleteProfile()
    {
        int idx = _listBox.SelectedIndex;
        if (idx < 0 || idx >= _manager.Profiles.Count || _manager.Profiles[idx].IsBuiltIn) return;

        var result = TrayMessageBox.Show(this,
            L.F("Profil \"{0}\" wirklich löschen?", _manager.Profiles[idx].DisplayName),
            "SpeedScan Manager", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            _manager.RemoveProfile(idx);
            RefreshList();
        }
    }

    private void MoveUp()
    {
        int idx = _listBox.SelectedIndex;
        _manager.MoveUp(idx);
        RefreshList();
        if (idx - 1 >= 0) _listBox.SelectedIndex = idx - 1;
    }

    private void MoveDown()
    {
        int idx = _listBox.SelectedIndex;
        _manager.MoveDown(idx);
        RefreshList();
        if (idx + 1 < _manager.Profiles.Count) _listBox.SelectedIndex = idx + 1;
    }

    /// <summary>
    /// Asks for a profile name; validates it (unique, case-insensitive) before closing.
    /// Returns the trimmed name, or null when cancelled.
    /// </summary>
    private string? ShowInputDialog(string title, string label, string defaultValue, ScanProfile? except)
        => ProfileNameInput.Show(this, _manager, title, label, defaultValue, except);
}

/// <summary>
/// Small modal input box for profile names (shared by MainForm and ProfileManagementDialog).
/// </summary>
internal static class ProfileNameInput
{
    public static string? Show(IWin32Window owner, ProfileManager manager, string title, string label,
        string defaultValue, ScanProfile? except)
    {
        using var dlg = new ModernWindow
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            ShowInTaskbar = false
        };
        dlg.SuspendLayout();
        dlg.Title = title;
        dlg.HeaderHeight = 52;
        dlg.HeaderButtons = ModernHeaderButtons.Close;
        dlg.HeaderIcon = ColorIcon.Tile(Glyph.User, IconColors.Blue);
        dlg.AutoSizeToContent = true;
        dlg.MinimumContentWidth = 380;

        var txt = new TextBox { Text = defaultValue, AutoSize = false, Height = 28, MaxLength = ProfileManager.MaxNameLength };
        var card = new SettingsCard { Dense = true };
        var row = card.AddRow(label.TrimEnd(':'), null, txt);
        row.Stacked = true;
        row.Icon = ColorIcon.Tile(Glyph.TextField, IconColors.Blue);
        var stack = new ModernStack { Dock = DockStyle.Top, Padding = new Padding(16, 16, 16, 16) };
        stack.Controls.Add(card);
        dlg.Body.Controls.Add(stack);

        var btnCancel = dlg.AddFooterButton(L.T("Abbrechen"), ThemeRole.Secondary, Glyph.Cross, DialogResult.Cancel);
        var btnOk = dlg.AddFooterButton("OK", ThemeRole.Primary, Glyph.Check, DialogResult.OK);
        dlg.AcceptButton = btnOk;
        dlg.CancelButton = btnCancel;
        dlg.Shown += (s, e) =>
        {
            txt.Focus();
            txt.SelectAll();
        };
        dlg.FormClosing += (s, e) =>
        {
            if (dlg.DialogResult != DialogResult.OK) return;
            string? error = manager.ValidateName(txt.Text, except);
            if (error != null)
            {
                TrayMessageBox.Show(dlg, error, "SpeedScan Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.Cancel = true;
            }
        };
        ThemeManager.Apply(dlg);
        dlg.ResumeLayout(true);

        return dlg.ShowDialog(owner) == DialogResult.OK ? txt.Text.Trim() : null;
    }
}
