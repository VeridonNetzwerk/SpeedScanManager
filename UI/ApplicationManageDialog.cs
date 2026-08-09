using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Dialog for managing custom scan applications (add/remove/edit, max 10).
/// Built-in applications (Scan to Folder) are not shown here – only user-added ones.
/// </summary>
internal class ApplicationManageDialog : Form
{
    private readonly ListBox _listBox;
    private readonly Button _btnAdd;
    private readonly Button _btnRemove;
    private readonly Button _btnEdit;
    private readonly Button _btnClose;

    private const int MaxEntries = 10;

    public List<ApplicationEntry> CustomApps { get; } = new();

    public ApplicationManageDialog(List<ApplicationEntry> existingApps)
    {
        CustomApps.AddRange(existingApps);

        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Installieren/Deinstallieren";
        ClientSize = new Size(590, 200);
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;

        var font = new Font("Microsoft Sans Serif", 8.25f);
        Font = font;

        // === List ===
        var lblList = new Label
        {
            Text = "Hinzugefügt:",
            Location = new Point(12, 12),
            AutoSize = true,
            Font = font
        };

        _listBox = new ListBox
        {
            Location = new Point(12, 34),
            Size = new Size(444, 134),
            Font = font,
            IntegralHeight = false
        };
        _listBox.SelectedIndexChanged += (s, e) => UpdateButtonStates();

        // === Buttons (right column, stacked) ===
        _btnAdd = new Button
        {
            Text = "Hinzufügen...",
            Location = new Point(468, 12),
            Size = new Size(120, 28),
            Font = font
        };
        _btnAdd.Click += (s, e) => AddEntry();

        _btnRemove = new Button
        {
            Text = "Entfernen",
            Location = new Point(468, 46),
            Size = new Size(120, 28),
            Font = font,
            Enabled = false
        };
        _btnRemove.Click += (s, e) => RemoveEntry();

        _btnEdit = new Button
        {
            Text = "Ändern...",
            Location = new Point(468, 80),
            Size = new Size(120, 28),
            Font = font,
            Enabled = false
        };
        _btnEdit.Click += (s, e) => EditEntry();

        var btnHelp = new Button
        {
            Text = "Hilfe",
            Location = new Point(468, 114),
            Size = new Size(120, 28),
            Font = font
        };
        btnHelp.Click += (s, e) => MessageBox.Show(
            "Hier können Sie benutzerdefinierte Anwendungen hinzufügen, entfernen oder bearbeiten.\n" +
            "Es sind maximal 10 Einträge erlaubt.",
            "SpeedScan Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);

        _btnClose = new Button
        {
            Text = "Schließen",
            DialogResult = DialogResult.OK,
            Location = new Point(468, 148),
            Size = new Size(120, 28),
            Font = font
        };

        var lblHint = new Label
        {
            Text = "Bis zu 10 Anwendungen können hinzugefügt werden.",
            Location = new Point(12, 174),
            AutoSize = true,
            Font = font,
            ForeColor = SystemColors.GrayText
        };

        Controls.AddRange(new Control[]
        {
            lblList,
            _listBox,
            _btnAdd, _btnRemove, _btnEdit,
            btnHelp,
            _btnClose,
            lblHint
        });

        AcceptButton = _btnClose;
        CancelButton = _btnClose;

        RefreshList();
    }

    private void RefreshList()
    {
        _listBox.Items.Clear();
        foreach (var app in CustomApps)
        {
            _listBox.Items.Add(app.Name);
        }
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        bool hasSelection = _listBox.SelectedIndex >= 0;
        _btnRemove.Enabled = hasSelection;
        _btnEdit.Enabled = hasSelection;
    }

    private void AddEntry()
    {
        if (CustomApps.Count >= MaxEntries)
        {
            MessageBox.Show($"Maximal {MaxEntries} Einträge sind erlaubt.",
                "SpeedScan Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dlg = new ApplicationEditDialog();
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            CustomApps.Add(new ApplicationEntry(dlg.EntryName, dlg.EntryType));
            RefreshList();
            _listBox.SelectedIndex = CustomApps.Count - 1;
        }
    }

    private void RemoveEntry()
    {
        int idx = _listBox.SelectedIndex;
        if (idx >= 0 && idx < CustomApps.Count)
        {
            CustomApps.RemoveAt(idx);
            RefreshList();
        }
    }

    private void EditEntry()
    {
        int idx = _listBox.SelectedIndex;
        if (idx < 0 || idx >= CustomApps.Count) return;

        var existing = CustomApps[idx];
        using var dlg = new ApplicationEditDialog
        {
            EntryName = existing.Name,
            EntryType = existing.Type
        };

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            CustomApps[idx] = new ApplicationEntry(dlg.EntryName, dlg.EntryType);
            RefreshList();
            _listBox.SelectedIndex = idx;
        }
    }
}

/// <summary>
/// Simple input dialog for a custom application entry: name + type.
/// </summary>
internal class ApplicationEditDialog : Form
{
    private readonly TextBox _txtName;
    private readonly ComboBox _cbType;
    private readonly Button _btnOk;
    private readonly Button _btnCancel;

    public string EntryName { get; set; } = "";
    public ApplicationType EntryType { get; set; } = ApplicationType.ScanToFolder;

    public ApplicationEditDialog()
    {
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Anwendung hinzufügen";
        ClientSize = new Size(340, 180);
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;

        var font = new Font("Microsoft Sans Serif", 8.25f);
        Font = font;

        var lblName = new Label { Text = "Name:", Location = new Point(12, 16), AutoSize = true, Font = font };
        _txtName = new TextBox { Location = new Point(100, 13), Size = new Size(210, 24), Font = font };

        var lblType = new Label { Text = "Typ:", Location = new Point(12, 48), AutoSize = true, Font = font };
        _cbType = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(100, 45),
            Size = new Size(210, 24),
            Font = font
        };
        _cbType.Items.AddRange(new object[]
        {
            "Scan to Folder",
            "Scan to E-Mail",
            "Scan to Print"
        });
        _cbType.SelectedIndex = 0;

        _btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(150, 104), Size = new Size(75, 28), Font = font };
        _btnCancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, Location = new Point(235, 104), Size = new Size(75, 28), Font = font };

        Controls.AddRange(new Control[] { lblName, _txtName, lblType, _cbType, _btnOk, _btnCancel });
        AcceptButton = _btnOk;
        CancelButton = _btnCancel;
    }

    protected override void OnShown(EventArgs e)
    {
        _txtName.Text = EntryName;
        _cbType.SelectedIndex = (int)EntryType;
        base.OnShown(e);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK)
        {
            if (string.IsNullOrWhiteSpace(_txtName.Text))
            {
                MessageBox.Show("Bitte einen Namen eingeben.", "SpeedScan Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.Cancel = true;
                return;
            }
            EntryName = _txtName.Text.Trim();
            EntryType = (ApplicationType)_cbType.SelectedIndex;
        }
        base.OnFormClosing(e);
    }
}
