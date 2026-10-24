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

    private const int MaxEntries = AppSettings.MaxCustomApplications;

    public List<CustomApplication> CustomApps { get; } = new();

    public ApplicationManageDialog(IEnumerable<CustomApplication> existingApps)
    {
        CustomApps.AddRange(existingApps.Select(a => a.Clone()));

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Installieren/Deinstallieren";
        Icon = TrayIcons.GetAppIcon();
        ClientSize = new Size(600, 206);
        ShowInTaskbar = false;

        var font = UiFonts.Default;
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
        _listBox.DoubleClick += (s, e) => EditEntry();

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
        btnHelp.Click += (s, e) => { using var h = new HelpForm("app-manage"); h.ShowDialog(this); };

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
            Text = $"Bis zu {MaxEntries} Anwendungen können hinzugefügt werden.",
            Location = new Point(12, 178),
            AutoSize = true,
            Font = font
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
        ResumeLayout(false);
        PerformLayout();

        ThemeManager.SetRole(_btnRemove, ThemeRole.Danger);
        ThemeManager.SetRole(lblHint, ThemeRole.Muted);
        ThemeManager.Apply(this);
    }

    private void RefreshList()
    {
        _listBox.Items.Clear();
        foreach (var app in CustomApps)
        {
            _listBox.Items.Add($"{app.Name}  ({app.ExePath})");
        }
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        bool hasSelection = _listBox.SelectedIndex >= 0;
        _btnRemove.Enabled = hasSelection;
        _btnEdit.Enabled = hasSelection;
        _btnAdd.Enabled = CustomApps.Count < MaxEntries;
    }

    private string? ValidateName(string name, int exceptIndex)
    {
        for (int i = 0; i < CustomApps.Count; i++)
        {
            if (i != exceptIndex && string.Equals(CustomApps[i].Name, name, StringComparison.OrdinalIgnoreCase))
                return $"Eine Anwendung mit dem Namen \"{name}\" existiert bereits.";
        }
        return null;
    }

    private void AddEntry()
    {
        if (CustomApps.Count >= MaxEntries)
        {
            MessageBox.Show($"Maximal {MaxEntries} Einträge sind erlaubt.",
                "SpeedScan Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dlg = new ApplicationEditDialog(name => ValidateName(name, -1)) { Text = "Anwendung hinzufügen" };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            CustomApps.Add(new CustomApplication { Name = dlg.EntryName, ExePath = dlg.ExePath, Arguments = dlg.Arguments });
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
        using var dlg = new ApplicationEditDialog(name => ValidateName(name, idx))
        {
            Text = "Anwendung ändern",
            EntryName = existing.Name,
            ExePath = existing.ExePath,
            Arguments = existing.Arguments
        };

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            CustomApps[idx] = new CustomApplication { Name = dlg.EntryName, ExePath = dlg.ExePath, Arguments = dlg.Arguments };
            RefreshList();
            _listBox.SelectedIndex = idx;
        }
    }
}

/// <summary>
/// Input dialog for a custom application entry: name, program (.exe) and optional arguments.
/// </summary>
internal class ApplicationEditDialog : Form
{
    private readonly TextBox _txtName;
    private readonly TextBox _txtPath;
    private readonly TextBox _txtArgs;
    private readonly Func<string, string?> _validateName;

    public string EntryName { get; set; } = "";
    public string ExePath { get; set; } = "";
    public string Arguments { get; set; } = "";

    public ApplicationEditDialog(Func<string, string?> validateName)
    {
        _validateName = validateName;

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Anwendung hinzufügen";
        Icon = TrayIcons.GetAppIcon();
        ClientSize = new Size(440, 176);
        ShowInTaskbar = false;

        var font = UiFonts.Default;
        Font = font;

        var lblName = new Label { Text = "Name:", Location = new Point(12, 16), AutoSize = true, Font = font };
        _txtName = new TextBox { Location = new Point(100, 13), Size = new Size(326, 24), Font = font, MaxLength = 64 };

        var lblPath = new Label { Text = "Programm:", Location = new Point(12, 48), AutoSize = true, Font = font };
        _txtPath = new TextBox { Location = new Point(100, 45), Size = new Size(220, 24), Font = font };
        var btnBrowse = new Button { Text = "Durchsuchen...", Location = new Point(326, 44), Size = new Size(100, 26), Font = font };
        btnBrowse.Click += (s, e) => BrowseExe();

        var lblArgs = new Label { Text = "Argumente:", Location = new Point(12, 80), AutoSize = true, Font = font };
        _txtArgs = new TextBox { Location = new Point(100, 77), Size = new Size(326, 24), Font = font };
        var lblArgsHint = new Label
        {
            Text = "Optional. Die gescannten Dateien werden danach angehängt.",
            Location = new Point(100, 103),
            AutoSize = true,
            Font = font
        };
        ThemeManager.SetRole(lblArgsHint, ThemeRole.Muted);

        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(266, 136), Size = new Size(75, 28), Font = font };
        var btnCancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, Location = new Point(351, 136), Size = new Size(75, 28), Font = font };

        Controls.AddRange(new Control[] { lblName, _txtName, lblPath, _txtPath, btnBrowse, lblArgs, _txtArgs, lblArgsHint, btnOk, btnCancel });
        AcceptButton = btnOk;
        CancelButton = btnCancel;
        ResumeLayout(false);
        PerformLayout();

        ThemeManager.Apply(this);
    }

    private void BrowseExe()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Anwendung auswählen",
            Filter = "Programme (*.exe)|*.exe",
            CheckFileExists = true
        };
        if (!string.IsNullOrEmpty(_txtPath.Text))
        {
            try { dlg.InitialDirectory = Path.GetDirectoryName(_txtPath.Text); } catch { }
        }
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _txtPath.Text = dlg.FileName;
            if (string.IsNullOrWhiteSpace(_txtName.Text))
                _txtName.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
        }
    }

    protected override void OnShown(EventArgs e)
    {
        _txtName.Text = EntryName;
        _txtPath.Text = ExePath;
        _txtArgs.Text = Arguments;
        base.OnShown(e);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK)
        {
            string name = _txtName.Text.Trim();
            string path = _txtPath.Text.Trim().Trim('"');
            string? error = string.IsNullOrWhiteSpace(name) ? "Bitte einen Namen eingeben." : _validateName(name);
            if (error == null && (string.IsNullOrWhiteSpace(path) || !File.Exists(path)))
                error = "Bitte wählen Sie eine vorhandene Programmdatei (*.exe) aus.";
            if (error != null)
            {
                MessageBox.Show(error, "SpeedScan Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.Cancel = true;
                return;
            }
            EntryName = name;
            ExePath = path;
            Arguments = _txtArgs.Text.Trim();
        }
        base.OnFormClosing(e);
    }
}
