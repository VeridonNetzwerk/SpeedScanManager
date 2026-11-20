using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Dialog for adding, removing, and editing custom paper sizes (max 10 entries).
/// Modern layout (<see cref="ModernWindow"/>): one card with a toolbar (add/edit/remove),
/// the list and the dimensions of the selected entry; help in the header.
/// </summary>
internal class CustomSizeDialog : ModernWindow
{
    private readonly ListBox _listBox;
    private readonly Button _btnAdd;
    private readonly Button _btnRemove;
    private readonly Button _btnEdit;
    private readonly Button _btnClose;
    private readonly TextBox _txtWidth;
    private readonly TextBox _txtLength;

    private const int MaxEntries = ScanSettings.MaxCustomPaperSizes;

    public List<CustomPaperSize> CustomSizes { get; } = new();

    public CustomSizeDialog(IEnumerable<CustomPaperSize> existingSizes)
    {
        CustomSizes.AddRange(existingSizes.Select(s => s with { }).Take(MaxEntries));

        SuspendLayout();
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        Text = "Hinzufügen oder Entfernen von benutzerdefinierten Größen";
        Title = "Benutzerdefinierte Größen";
        Subtitle = "Hinzufügen oder Entfernen von benutzerdefinierten Größen";
        HeaderButtons = ModernHeaderButtons.Help | ModernHeaderButtons.Close;
        HelpTopic = "custom-size-dialog";
        HeaderIcon = ColorIcon.Tile(Glyph.Ruler, IconColors.Orange);
        AutoSizeToContent = true;
        MinimumContentWidth = 560;

        // === Toolbar: add / edit / remove ===
        _btnAdd = new Button
        {
            Text = "Hinzufügen...",
            Size = new Size(132, 32),
            Margin = new Padding(0, 0, 8, 0)
        };
        ThemeManager.SetGlyph(_btnAdd, Glyph.Plus, IconColors.Green);
        _btnAdd.Click += (s, e) => AddSize();

        _btnEdit = new Button
        {
            Text = "Ändern...",
            Size = new Size(116, 32),
            Margin = new Padding(0, 0, 8, 0),
            Enabled = false
        };
        ThemeManager.SetGlyph(_btnEdit, Glyph.Sliders, IconColors.Blue);
        _btnEdit.Click += (s, e) => EditSize();

        _btnRemove = new Button
        {
            Text = "Entfernen",
            Size = new Size(116, 32),
            Margin = Padding.Empty,
            Enabled = false
        };
        ThemeManager.SetGlyph(_btnRemove, Glyph.Minus);
        _btnRemove.Click += (s, e) => RemoveSize();

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
            Margin = Padding.Empty,
            AccessibleName = "Benutzerdefinierte Größen"
        };
        // Rows: ruler icon, name, dimensions right-aligned
        ModernLists.Setup(_listBox, i => i < CustomSizes.Count
            ? (SizeIcon, CustomSizes[i].Name, $"{CustomSizes[i].WidthMm} x {CustomSizes[i].LengthMm} mm")
            : (null, _listBox.Items[i]?.ToString() ?? "", null));
        _listBox.SelectedIndexChanged += (s, e) => OnSelectionChanged();

        var listHost = new Panel { Height = 180, Padding = new Padding(0, 0, 0, 12) };
        listHost.Controls.Add(_listBox);

        // === Dimensions of the selected entry (display only) ===
        _txtWidth = new TextBox
        {
            Size = new Size(70, 24),
            Margin = Padding.Empty,
            TextAlign = HorizontalAlignment.Center,
            ReadOnly = true, // display only; values are edited via "Ändern..."
            AccessibleName = "Breite"
        };

        _txtLength = new TextBox
        {
            Size = new Size(70, 24),
            Margin = Padding.Empty,
            TextAlign = HorizontalAlignment.Center,
            ReadOnly = true, // display only; values are edited via "Ändern..."
            AccessibleName = "Länge"
        };

        // Labels above the text fields
        var dimTable = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 4,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        dimTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70f));
        dimTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 22f));
        dimTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70f));
        dimTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        dimTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        dimTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var lblBreite = new Label { Text = "Breite", AutoSize = true, Margin = new Padding(0, 0, 0, 3) };
        var lblLaenge = new Label { Text = "Länge", AutoSize = true, Margin = new Padding(0, 0, 0, 3) };
        ThemeManager.SetRole(lblBreite, ThemeRole.Muted);
        ThemeManager.SetRole(lblLaenge, ThemeRole.Muted);
        dimTable.Controls.Add(lblBreite, 0, 0);
        dimTable.Controls.Add(lblLaenge, 2, 0);
        dimTable.Controls.Add(_txtWidth, 0, 1);
        dimTable.Controls.Add(new Label
        {
            Text = "×",
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            TextAlign = ContentAlignment.MiddleCenter
        }, 1, 1);
        dimTable.Controls.Add(_txtLength, 2, 1);
        dimTable.Controls.Add(new Label
        {
            Text = "mm",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(6, 0, 0, 0)
        }, 3, 1);

        var card = new SettingsCard("Größen", $"Bis zu {MaxEntries} benutzerdefinierte Größen können hinzugefügt werden.")
        {
            Dense = true,
            Icon = ColorIcon.Tile(Glyph.Ruler, IconColors.Orange)
        };
        card.Add(toolbar);
        card.Add(listHost);
        card.AddRow("Ausgewählte Größe", null, dimTable);

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

    private static readonly ColorIcon SizeIcon = ColorIcon.Tile(Glyph.PageSingle, IconColors.Orange);

    private void RefreshList()
    {
        _listBox.Items.Clear();
        foreach (var size in CustomSizes)
        {
            _listBox.Items.Add(size.DisplayText);
        }
        _btnAdd.Enabled = CustomSizes.Count < MaxEntries;
    }

    private void OnSelectionChanged()
    {
        bool hasSelection = _listBox.SelectedIndex >= 0;
        _btnRemove.Enabled = hasSelection;
        _btnEdit.Enabled = hasSelection;

        if (hasSelection && _listBox.SelectedIndex < CustomSizes.Count)
        {
            var size = CustomSizes[_listBox.SelectedIndex];
            _txtWidth.Text = size.WidthMm.ToString();
            _txtLength.Text = size.LengthMm.ToString();
        }
        else
        {
            _txtWidth.Text = "";
            _txtLength.Text = "";
        }
    }

    private void AddSize()
    {
        if (CustomSizes.Count >= MaxEntries)
        {
            TrayMessageBox.Show(this, $"Maximal {MaxEntries} Einträge sind erlaubt.",
                "SpeedScan Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dlg = new CustomSizeEditDialog();
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            CustomSizes.Add(new CustomPaperSize(dlg.SizeName, dlg.WidthMm, dlg.LengthMm));
            RefreshList();
            _listBox.SelectedIndex = CustomSizes.Count - 1;
        }
    }

    private void RemoveSize()
    {
        int idx = _listBox.SelectedIndex;
        if (idx >= 0 && idx < CustomSizes.Count)
        {
            CustomSizes.RemoveAt(idx);
            RefreshList();
            _txtWidth.Text = "";
            _txtLength.Text = "";
            _btnRemove.Enabled = false;
            _btnEdit.Enabled = false;
        }
    }

    private void EditSize()
    {
        int idx = _listBox.SelectedIndex;
        if (idx < 0 || idx >= CustomSizes.Count) return;

        var existing = CustomSizes[idx];
        using var dlg = new CustomSizeEditDialog
        {
            SizeName = existing.Name,
            WidthMm = existing.WidthMm,
            LengthMm = existing.LengthMm
        };

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            CustomSizes[idx] = new CustomPaperSize(dlg.SizeName, dlg.WidthMm, dlg.LengthMm);
            RefreshList();
            _listBox.SelectedIndex = idx;
        }
    }
}

/// <summary>
/// Simple input dialog for a single custom paper size: name, width, length in mm.
/// </summary>
internal class CustomSizeEditDialog : ModernWindow
{
    private readonly TextBox _txtName;
    private readonly NumericUpDown _numWidth;
    private readonly NumericUpDown _numLength;
    private readonly Button _btnOk;
    private readonly Button _btnCancel;

    public string SizeName { get; set; } = "";
    public int WidthMm { get; set; } = 210;
    public int LengthMm { get; set; } = 297;

    public CustomSizeEditDialog()
    {
        SuspendLayout();
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        Text = "Benutzerdefinierte Größe";
        Title = "Benutzerdefinierte Größe";
        Subtitle = "Name und Abmessungen in Millimetern";
        HeaderButtons = ModernHeaderButtons.Close;
        AutoSizeToContent = true;
        MinimumContentWidth = 400;

        _txtName = new TextBox
        {
            MaxLength = ScanSettings.MaxCustomPaperSizeNameLength,
            Size = new Size(200, 24)
        };

        _numWidth = new NumericUpDown
        {
            Size = new Size(90, 24),
            Minimum = 1,
            Maximum = 9999,
            Value = 210
        };

        _numLength = new NumericUpDown
        {
            Size = new Size(90, 24),
            Minimum = 1,
            Maximum = 9999,
            Value = 297
        };

        var card = new SettingsCard();
        card.AddRow("Name", null, _txtName, 200);
        card.AddRow("Breite (mm)", null, _numWidth, 90);
        card.AddRow("Länge (mm)", null, _numLength, 90);

        var stack = new ModernStack { Dock = DockStyle.Fill, Padding = new Padding(16, 16, 16, 16), Spacing = 12 };
        stack.Controls.Add(card);
        Body.Controls.Add(stack);

        _btnCancel = AddFooterButton("Abbrechen", ThemeRole.Secondary, Glyph.None, DialogResult.Cancel);
        _btnOk = AddFooterButton("OK", ThemeRole.Primary, Glyph.Check, DialogResult.OK);

        AcceptButton = _btnOk;
        CancelButton = _btnCancel;
        ResumeLayout(false);

        ThemeManager.Apply(this);
    }

    protected override void OnShown(EventArgs e)
    {
        _txtName.Text = SizeName;
        _numWidth.Value = Math.Clamp(WidthMm, 1, 9999);
        _numLength.Value = Math.Clamp(LengthMm, 1, 9999);
        base.OnShown(e);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK)
        {
            if (string.IsNullOrWhiteSpace(_txtName.Text))
            {
                TrayMessageBox.Show(this, "Bitte einen Namen eingeben.", "SpeedScan Manager",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.Cancel = true;
                return;
            }
            if (_txtName.Text.Trim().Length > ScanSettings.MaxCustomPaperSizeNameLength)
            {
                TrayMessageBox.Show(this, $"Der Name darf höchstens {ScanSettings.MaxCustomPaperSizeNameLength} Zeichen lang sein.",
                    "SpeedScan Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.Cancel = true;
                return;
            }
            SizeName = _txtName.Text.Trim();
            WidthMm = (int)_numWidth.Value;
            LengthMm = (int)_numLength.Value;
        }
        base.OnFormClosing(e);
    }
}
