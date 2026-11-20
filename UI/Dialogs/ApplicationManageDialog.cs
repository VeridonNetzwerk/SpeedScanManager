using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Dialog for managing custom scan applications (add/remove/edit, max 10).
/// Built-in applications (Scan to Folder) are not shown here – only user-added ones.
/// Modern layout (<see cref="ModernWindow"/>): one card with a toolbar (add/edit/remove) and
/// the list; help in the header.
/// </summary>
internal class ApplicationManageDialog : ModernWindow
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
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        Text = "Installieren/Deinstallieren";
        Title = "Installieren/Deinstallieren";
        Subtitle = "Eigene Anwendungen hinzufügen, ändern oder entfernen";
        HeaderButtons = ModernHeaderButtons.Help | ModernHeaderButtons.Close;
        HelpTopic = "app-manage";
        HeaderIcon = ColorIcon.Tile(Glyph.Apps, IconColors.Purple);
        AutoSizeToContent = true;
        MinimumContentWidth = 600;

        // === Toolbar: add / edit / remove ===
        _btnAdd = new Button
        {
            Text = "Hinzufügen...",
            Size = new Size(132, 32),
            Margin = new Padding(0, 0, 8, 0)
        };
        ThemeManager.SetGlyph(_btnAdd, Glyph.Plus, IconColors.Green);
        _btnAdd.Click += (s, e) => AddEntry();

        _btnEdit = new Button
        {
            Text = "Ändern...",
            Size = new Size(116, 32),
            Margin = new Padding(0, 0, 8, 0),
            Enabled = false
        };
        ThemeManager.SetGlyph(_btnEdit, Glyph.Sliders, IconColors.Blue);
        _btnEdit.Click += (s, e) => EditEntry();

        _btnRemove = new Button
        {
            Text = "Entfernen",
            Size = new Size(116, 32),
            Margin = Padding.Empty,
            Enabled = false
        };
        ThemeManager.SetGlyph(_btnRemove, Glyph.Minus);
        _btnRemove.Click += (s, e) => RemoveEntry();

        var toolbar = new FlowLayoutPanel
        {
            AutoSize = false,
            Height = 56,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 12, 0, 12),
            Margin = Padding.Empty
        };
        toolbar.Controls.AddRange(new Control[] { _btnAdd, _btnEdit, _btnRemove });

        // === List ===
        _listBox = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            AccessibleName = "Hinzugefügt"
        };
        // Rows: app icon, name, program path right-aligned
        ModernLists.Setup(_listBox, i => i < CustomApps.Count
            ? (ColorIcons.Target(ApplicationType.Custom), CustomApps[i].Name, CustomApps[i].ExePath)
            : (null, _listBox.Items[i]?.ToString() ?? "", null));
        _listBox.SelectedIndexChanged += (s, e) => UpdateButtonStates();
        _listBox.DoubleClick += (s, e) => EditEntry();

        var listHost = new Panel { Height = 176, Padding = new Padding(0, 0, 0, 14) };
        listHost.Controls.Add(_listBox);

        var card = new SettingsCard("Hinzugefügt", $"Bis zu {MaxEntries} Anwendungen können hinzugefügt werden.")
        {
            Dense = true,
            Icon = ColorIcon.Tile(Glyph.Apps, IconColors.Purple)
        };
        card.Add(toolbar);
        card.Add(listHost);

        var stack = new ModernStack { Dock = DockStyle.Fill, Padding = new Padding(16, 16, 16, 16), Spacing = 12 };
        stack.Controls.Add(card);
        Body.Controls.Add(stack);

        // === Footer ===
        _btnClose = AddFooterButton("Schließen", ThemeRole.Primary, Glyph.Check, DialogResult.OK);

        AcceptButton = _btnClose;
        CancelButton = _btnClose;

        RefreshList();
        ResumeLayout(false);

        ThemeManager.SetRole(_btnRemove, ThemeRole.Danger);
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
            TrayMessageBox.Show(this, $"Maximal {MaxEntries} Einträge sind erlaubt.",
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
internal class ApplicationEditDialog : ModernWindow
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
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        Text = "Anwendung hinzufügen";
        Title = Text;
        Subtitle = "Programm, das die gescannten Dateien öffnet";
        HeaderButtons = ModernHeaderButtons.Close;
        HeaderIcon = ColorIcon.Tile(Glyph.Apps, IconColors.Purple);
        AutoSizeToContent = true;
        MinimumContentWidth = 500;

        _txtName = new TextBox { MaxLength = 64, AccessibleName = "Name" };

        _txtPath = new TextBox { AccessibleName = "Programm" };
        var btnBrowse = new Button { Text = "Durchsuchen...", Size = new Size(132, 32) };
        ThemeManager.SetGlyph(btnBrowse, Glyph.Folder, IconColors.Amber);
        btnBrowse.Click += (s, e) => BrowseExe();
        var pathPicker = new PathPicker(_txtPath, btnBrowse);

        _txtArgs = new TextBox { AccessibleName = "Argumente" };

        // Label above the input (full width for long paths).
        var card = new SettingsCard { Dense = true };
        var rowName = card.AddRow("Name", null, _txtName);
        rowName.Stacked = true;
        rowName.Icon = ColorIcon.Tile(Glyph.TextField, IconColors.Blue);
        var rowPath = card.AddRow("Programm", null, pathPicker);
        rowPath.Stacked = true;
        rowPath.Icon = ColorIcon.Tile(Glyph.Apps, IconColors.Purple);
        var rowArgs = card.AddRow("Argumente", "Optional. Die gescannten Dateien werden danach angehängt.", _txtArgs);
        rowArgs.Stacked = true;
        rowArgs.Icon = ColorIcon.Tile(Glyph.Sliders, IconColors.Slate);

        var stack = new ModernStack { Dock = DockStyle.Fill, Padding = new Padding(16, 16, 16, 16), Spacing = 12 };
        stack.Controls.Add(card);
        Body.Controls.Add(stack);

        var btnCancel = AddFooterButton("Abbrechen", ThemeRole.Secondary, Glyph.Cross, DialogResult.Cancel);
        var btnOk = AddFooterButton("OK", ThemeRole.Primary, Glyph.Check, DialogResult.OK);
        AcceptButton = btnOk;
        CancelButton = btnCancel;
        ResumeLayout(false);

        ThemeManager.Apply(this);
    }

    /// <summary>The caption (set by the caller, e.g. "Anwendung ändern") is also the header title.</summary>
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Title = Text;
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
                TrayMessageBox.Show(this, error, "SpeedScan Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.Cancel = true;
                return;
            }
            EntryName = name;
            ExePath = path;
            Arguments = _txtArgs.Text.Trim();
        }
        base.OnFormClosing(e);
    }

    /// <summary>Path text box (fills the width) with the browse button on its right.</summary>
    private sealed class PathPicker : Panel
    {
        private readonly TextBox _text;
        private readonly Button _button;

        public PathPicker(TextBox text, Button button)
        {
            _text = text;
            _button = button;
            Height = 32;
            Margin = Padding.Empty;
            Controls.Add(text);
            Controls.Add(button);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            int gap = ModernLayout.Scale(this, 8);
            _button.Location = new Point(Width - _button.Width, (Height - _button.Height) / 2);
            _text.SetBounds(0, (Height - _text.Height) / 2, Math.Max(0, _button.Left - gap), _text.Height);
        }
    }
}
