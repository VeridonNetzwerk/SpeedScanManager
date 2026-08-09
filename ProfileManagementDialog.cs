using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Profile management window: list profiles, rename, delete, reorder.
/// "Standard" profile is protected – all buttons except "Schließen" disabled.
/// </summary>
internal class ProfileManagementDialog : Form
{
    private readonly ListBox _listBox;
    private readonly Button _btnRename;
    private readonly Button _btnDelete;
    private readonly Button _btnUp;
    private readonly Button _btnDown;
    private readonly Button _btnClose;
    private readonly ProfileManager _manager;

    public ProfileManagementDialog(ProfileManager manager)
    {
        _manager = manager;

        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "SpeedScanManager – Profilverwaltung";
        ClientSize = new Size(420, 340);
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;

        var font = new Font("Microsoft Sans Serif", 8.25f);
        Font = font;

        // === List ===
        _listBox = new ListBox
        {
            Location = new Point(12, 12),
            Size = new Size(260, 250),
            Font = font
        };
        _listBox.SelectedIndexChanged += (s, e) => UpdateButtonStates();

        // === Buttons ===
        _btnRename = new Button
        {
            Text = "Umbenennen...",
            Location = new Point(299, 12),
            Size = new Size(100, 28),
            Font = font
        };
        _btnRename.Click += (s, e) => RenameProfile();

        _btnDelete = new Button
        {
            Text = "Löschen",
            Location = new Point(299, 46),
            Size = new Size(100, 28),
            Font = font
        };
        _btnDelete.Click += (s, e) => DeleteProfile();

        _btnUp = new Button
        {
            Text = "Oben",
            Location = new Point(299, 80),
            Size = new Size(100, 28),
            Font = font
        };
        _btnUp.Click += (s, e) => MoveUp();

        _btnDown = new Button
        {
            Text = "Unten",
            Location = new Point(299, 114),
            Size = new Size(100, 28),
            Font = font
        };
        _btnDown.Click += (s, e) => MoveDown();

        _btnClose = new Button
        {
            Text = "Schließen",
            DialogResult = DialogResult.OK,
            Location = new Point(299, 248),
            Size = new Size(100, 28),
            Font = font
        };

        Controls.AddRange(new Control[]
        {
            _listBox,
            _btnRename, _btnDelete, _btnUp, _btnDown,
            _btnClose
        });

        AcceptButton = _btnClose;
        CancelButton = _btnClose;

        RefreshList();
    }

    private void RefreshList()
    {
        _listBox.Items.Clear();
        foreach (var profile in _manager.Profiles)
        {
            _listBox.Items.Add(profile.Name);
        }
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        int idx = _listBox.SelectedIndex;
        bool hasSelection = idx >= 0;
        bool isBuiltIn = hasSelection && idx < _manager.Profiles.Count && _manager.Profiles[idx].IsBuiltIn;

        _btnRename.Enabled = hasSelection && !isBuiltIn;
        _btnDelete.Enabled = hasSelection && !isBuiltIn;
        _btnUp.Enabled = hasSelection && !isBuiltIn && idx > 0;
        _btnDown.Enabled = hasSelection && !isBuiltIn && idx < _manager.Profiles.Count - 1;
    }

    private void RenameProfile()
    {
        int idx = _listBox.SelectedIndex;
        if (idx < 0 || idx >= _manager.Profiles.Count) return;
        if (_manager.Profiles[idx].IsBuiltIn) return;

        string currentName = _manager.Profiles[idx].Name;
        string? newName = ShowInputDialog("Profil umbenennen", "Neuer Name:", currentName);

        if (!string.IsNullOrWhiteSpace(newName) && newName != currentName)
        {
            _manager.RenameProfile(idx, newName.Trim());
            RefreshList();
            _listBox.SelectedIndex = idx;
        }
    }

    private void DeleteProfile()
    {
        int idx = _listBox.SelectedIndex;
        if (idx < 0 || idx >= _manager.Profiles.Count) return;
        if (_manager.Profiles[idx].IsBuiltIn) return;

        var result = MessageBox.Show(
            $"Profil \"{_manager.Profiles[idx].Name}\" wirklich löschen?",
            "SpeedScanManager", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

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
}
