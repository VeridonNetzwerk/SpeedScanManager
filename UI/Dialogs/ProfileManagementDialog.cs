using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Profile management window: list profiles, rename, delete, reorder.
/// </summary>
internal class ProfileManagementDialog : Form
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
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "SpeedScan Manager – Profilverwaltung";
        Icon = TrayIcons.GetAppIcon();
        ClientSize = new Size(411, 304);
        ShowInTaskbar = false;

        var font = UiFonts.Default;
        Font = font;

        // === List ===
        _listBox = new ListBox
        {
            Location = new Point(12, 12),
            Size = new Size(260, 280),
            Font = font
        };
        _listBox.SelectedIndexChanged += (s, e) => UpdateButtonStates();

        // === Buttons ===
        _btnAdd = new Button
        {
            Text = "Hinzufügen...",
            Location = new Point(299, 12),
            Size = new Size(100, 28),
            Font = font
        };
        _btnAdd.Click += (s, e) => AddProfile();

        _btnRename = new Button
        {
            Text = "Umbenennen...",
            Location = new Point(299, 46),
            Size = new Size(100, 28),
            Font = font
        };
        _btnRename.Click += (s, e) => RenameProfile();

        _btnDelete = new Button
        {
            Text = "Löschen",
            Location = new Point(299, 80),
            Size = new Size(100, 28),
            Font = font
        };
        _btnDelete.Click += (s, e) => DeleteProfile();

        _btnUp = new Button
        {
            Text = "Nach oben",
            Location = new Point(299, 114),
            Size = new Size(100, 28),
            Font = font
        };
        _btnUp.Click += (s, e) => MoveUp();

        _btnDown = new Button
        {
            Text = "Nach unten",
            Location = new Point(299, 148),
            Size = new Size(100, 28),
            Font = font
        };
        _btnDown.Click += (s, e) => MoveDown();

        _btnClose = new Button
        {
            Text = "Schließen",
            DialogResult = DialogResult.OK,
            Location = new Point(299, 264),
            Size = new Size(100, 28),
            Font = font
        };

        Controls.AddRange(new Control[]
        {
            _listBox,
            _btnAdd, _btnRename, _btnDelete, _btnUp, _btnDown,
            _btnClose
        });

        AcceptButton = _btnClose;
        CancelButton = _btnClose;

        RefreshList();
        ResumeLayout(false);
        PerformLayout();

        ThemeManager.SetRole(_btnDelete, ThemeRole.Danger);
        ThemeManager.Apply(this);
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
            TrayMessageBox.Show(this, $"Es können höchstens {ProfileManager.MaxProfiles} Profile angelegt werden.",
                "SpeedScan Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string? name = ShowInputDialog("Neues Profil", "Name des neuen Profils:", "Neues Profil", null);
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
        string? newName = ShowInputDialog("Profil umbenennen", "Neuer Name:", currentName, profile);

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
            $"Profil \"{_manager.Profiles[idx].DisplayName}\" wirklich löschen?",
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
        using var dlg = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            ShowInTaskbar = false
        };
        dlg.SuspendLayout();
        dlg.AutoScaleDimensions = new SizeF(96F, 96F);
        dlg.AutoScaleMode = AutoScaleMode.Dpi;
        dlg.ClientSize = new Size(320, 110);

        var dlgFont = UiFonts.Default;
        dlg.Font = dlgFont;
        var lbl = new Label { Text = label, Location = new Point(12, 12), AutoSize = true, Font = dlgFont };
        var txt = new TextBox { Text = defaultValue, Location = new Point(12, 34), Size = new Size(280, 24), Font = dlgFont, MaxLength = ProfileManager.MaxNameLength };
        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(130, 68), Size = new Size(75, 28), Font = dlgFont };
        var btnCancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, Location = new Point(217, 68), Size = new Size(75, 28), Font = dlgFont };

        dlg.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
        dlg.AcceptButton = btnOk;
        dlg.CancelButton = btnCancel;
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
        dlg.ResumeLayout(false);
        dlg.PerformLayout();
        ThemeManager.Apply(dlg);

        return dlg.ShowDialog(owner) == DialogResult.OK ? txt.Text.Trim() : null;
    }
}
