using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Dialog for adding, removing, and editing custom paper sizes (max 10 entries).
/// </summary>
internal class CustomSizeDialog : Form
{
    private readonly ListBox _listBox;
    private readonly Button _btnAdd;
    private readonly Button _btnRemove;
    private readonly Button _btnEdit;
    private readonly Button _btnHelp;
    private readonly Button _btnClose;
    private readonly TextBox _txtWidth;
    private readonly TextBox _txtLength;

    private const int MaxEntries = 10;

    public List<CustomPaperSize> CustomSizes { get; } = new();

    public CustomSizeDialog(List<CustomPaperSize> existingSizes)
    {
        CustomSizes.AddRange(existingSizes);

        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Hinzufügen oder Entfernen von benutzerdefinierten Größen";
        ClientSize = new Size(600, 340);
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;

        var font = new Font("Microsoft Sans Serif", 8.25f);
        Font = font;

        // === List ===
        _listBox = new ListBox
        {
            Font = font,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };
        _listBox.SelectedIndexChanged += (s, e) => OnSelectionChanged();

        // === Right button column ===
        _btnAdd = new Button
        {
            Text = "Hinzufügen...",
            Size = new Size(100, 28),
            Font = font,
            Dock = DockStyle.Top,
            Margin = Padding.Empty
        };
        _btnAdd.Click += (s, e) => AddSize();

        _btnRemove = new Button
        {
            Text = "Entfernen",
            Size = new Size(100, 28),
            Font = font,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 8, 0, 0),
            Enabled = false
        };
        _btnRemove.Click += (s, e) => RemoveSize();

        _btnEdit = new Button
        {
            Text = "Ändern...",
            Size = new Size(100, 28),
            Font = font,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 8, 0, 0),
            Enabled = false
        };
        _btnEdit.Click += (s, e) => EditSize();

        _btnHelp = new Button
        {
            Text = "Hilfe",
            Size = new Size(100, 28),
            Font = font,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 20, 0, 0)
        };
        _btnHelp.Click += (s, e) => MessageBox.Show(
            "Benutzerdefinierte Größen erlauben es, Papierformate zu definieren, " +
            "die nicht in der Standardliste enthalten sind.\n\n" +
            "Klicken Sie auf „Hinzufügen...“, um eine neue Größe mit Name, Breite und Länge zu erstellen.\n" +
            "Wählen Sie einen Eintrag aus und klicken Sie auf „Entfernen“ oder „Ändern...“, " +
            "um ihn zu löschen oder zu bearbeiten.\n\n" +
            $"Es können bis zu {MaxEntries} benutzerdefinierte Größen angelegt werden.",
            "Hilfe – Benutzerdefinierte Größen",
            MessageBoxButtons.OK, MessageBoxIcon.Information);

        _btnClose = new Button
        {
            Text = "Schließen",
            DialogResult = DialogResult.OK,
            Size = new Size(100, 28),
            Font = font,
            Dock = DockStyle.Bottom,
            Margin = Padding.Empty
        };

        var buttonColumn = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            ColumnStyles = { new ColumnStyle(SizeType.Absolute, 100f) },
            RowCount = 6,
            RowStyles =
            {
                new RowStyle(SizeType.Absolute, 28f),
                new RowStyle(SizeType.Absolute, 36f),
                new RowStyle(SizeType.Absolute, 36f),
                new RowStyle(SizeType.Percent, 100f),
                new RowStyle(SizeType.Absolute, 48f),
                new RowStyle(SizeType.Absolute, 28f)
            },
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        buttonColumn.Controls.Add(_btnAdd, 0, 0);
        buttonColumn.Controls.Add(_btnRemove, 0, 1);
        buttonColumn.Controls.Add(_btnEdit, 0, 2);
        buttonColumn.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 3);
        buttonColumn.Controls.Add(_btnHelp, 0, 4);
        buttonColumn.Controls.Add(_btnClose, 0, 5);

        // === Top row: ListBox + button column ===
        var topTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            ColumnStyles =
            {
                new ColumnStyle(SizeType.Percent, 100f),
                new ColumnStyle(SizeType.Absolute, 110f)
            },
            RowCount = 1,
            RowStyles = { new RowStyle(SizeType.Percent, 100f) },
            Margin = Padding.Empty
        };
        topTable.Controls.Add(_listBox, 0, 0);
        topTable.Controls.Add(buttonColumn, 1, 0);

        // === Dimensions row ===
        _txtWidth = new TextBox
        {
            Size = new Size(70, 22),
            Font = font,
            Margin = Padding.Empty,
            TextAlign = HorizontalAlignment.Center
        };

        _txtLength = new TextBox
        {
            Size = new Size(70, 22),
            Font = font,
            Margin = Padding.Empty,
            TextAlign = HorizontalAlignment.Center
        };

        // Labels above text fields: clean 6-column layout, labels directly above textboxes
        var dimTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            ColumnStyles =
            {
                new ColumnStyle(SizeType.Absolute, 75f),  // width label + textbox
                new ColumnStyle(SizeType.Absolute, 20f),  // ×
                new ColumnStyle(SizeType.Absolute, 75f),  // length label + textbox
                new ColumnStyle(SizeType.Absolute, 30f),  // mm
                new ColumnStyle(SizeType.Percent, 100f)   // filler
            },
            RowCount = 2,
            RowStyles =
            {
                new RowStyle(SizeType.Absolute, 16f),  // label row
                new RowStyle(SizeType.Absolute, 24f)    // textbox row
            },
            Margin = new Padding(60, 0, 0, 0),
            Height = 40
        };
        // Row 0: labels directly above their textboxes
        var lblBreite = new Label
        {
            Text = "Breite",
            Font = font,
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
            Margin = Padding.Empty
        };
        dimTable.Controls.Add(lblBreite, 0, 0);

        var lblLaenge = new Label
        {
            Text = "Länge",
            Font = font,
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
            Margin = Padding.Empty
        };
        dimTable.Controls.Add(lblLaenge, 2, 0);

        // Row 1: textboxes and × and mm
        dimTable.Controls.Add(_txtWidth, 0, 1);
        dimTable.Controls.Add(new Label
        {
            Text = "×",
            Font = font,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(4, 0, 0, 0),
            TextAlign = ContentAlignment.MiddleCenter
        }, 1, 1);
        dimTable.Controls.Add(_txtLength, 2, 1);
        dimTable.Controls.Add(new Label
        {
            Text = "mm",
            Font = font,
            AutoSize = false,
            Width = 30,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(4, 0, 0, 0)
        }, 3, 1);

        // === Hint label ===
        var hintLabel = new Label
        {
            Text = "Bis zu 10 benutzerdefinierte Dokumente können hinzugefügt werden.",
            Font = font,
            AutoSize = true,
            Margin = Padding.Empty
        };

        // === Main layout ===
        var rootTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            ColumnStyles = { new ColumnStyle(SizeType.Percent, 100f) },
            RowCount = 4,
            RowStyles =
            {
                new RowStyle(SizeType.Absolute, 180f),
                new RowStyle(SizeType.Absolute, 45f),
                new RowStyle(SizeType.Absolute, 25f),
                new RowStyle(SizeType.Percent, 100f)
            },
            Padding = new Padding(28, 15, 20, 15),
            Margin = Padding.Empty
        };

        rootTable.Controls.Add(topTable, 0, 0);
        rootTable.Controls.Add(dimTable, 0, 1);
        rootTable.Controls.Add(hintLabel, 0, 2);
        rootTable.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 3);

        Controls.Add(rootTable);

        AcceptButton = _btnClose;
        CancelButton = _btnClose;

        RefreshList();
    }

    private void RefreshList()
    {
        _listBox.Items.Clear();
        foreach (var size in CustomSizes)
        {
            _listBox.Items.Add($"{size.Name}  ({size.WidthMm} x {size.LengthMm} mm)");
        }
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
            MessageBox.Show($"Maximal {MaxEntries} Einträge sind erlaubt.",
                "SpeedScanManager", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
internal class CustomSizeEditDialog : Form
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
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Benutzerdefinierte Größe";
        ClientSize = new Size(300, 185);
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;

        var font = new Font("Microsoft Sans Serif", 8.25f);
        Font = font;

        var lblName = new Label
        {
            Text = "Name:",
            Font = font,
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty
        };
        _txtName = new TextBox
        {
            Size = new Size(170, 22),
            Font = font,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };

        var lblWidth = new Label
        {
            Text = "Breite (mm):",
            Font = font,
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty
        };
        _numWidth = new NumericUpDown
        {
            Size = new Size(80, 22),
            Font = font,
            Minimum = 1,
            Maximum = 9999,
            Value = 210,
            Dock = DockStyle.Left,
            Margin = Padding.Empty
        };

        var lblLength = new Label
        {
            Text = "Länge (mm):",
            Font = font,
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty
        };
        _numLength = new NumericUpDown
        {
            Size = new Size(80, 22),
            Font = font,
            Minimum = 1,
            Maximum = 9999,
            Value = 297,
            Dock = DockStyle.Left,
            Margin = Padding.Empty
        };

        _btnOk = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Size = new Size(75, 28),
            Font = font,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Margin = Padding.Empty
        };
        _btnCancel = new Button
        {
            Text = "Abbrechen",
            DialogResult = DialogResult.Cancel,
            Size = new Size(75, 28),
            Font = font,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Margin = Padding.Empty
        };

        var inputTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            ColumnStyles =
            {
                new ColumnStyle(SizeType.Absolute, 100f),
                new ColumnStyle(SizeType.Percent, 100f)
            },
            RowCount = 3,
            RowStyles =
            {
                new RowStyle(SizeType.Absolute, 30f),
                new RowStyle(SizeType.Absolute, 30f),
                new RowStyle(SizeType.Absolute, 30f)
            },
            Margin = Padding.Empty,
            Padding = new Padding(12, 12, 12, 0)
        };
        inputTable.Controls.Add(lblName, 0, 0);
        inputTable.Controls.Add(_txtName, 1, 0);
        inputTable.Controls.Add(lblWidth, 0, 1);
        inputTable.Controls.Add(_numWidth, 1, 1);
        inputTable.Controls.Add(lblLength, 0, 2);
        inputTable.Controls.Add(_numLength, 1, 2);

        var buttonRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Height = 40,
            Padding = new Padding(0, 0, 12, 8),
            Margin = Padding.Empty
        };
        buttonRow.Controls.Add(_btnCancel);
        buttonRow.Controls.Add(_btnOk);

        Controls.Add(inputTable);
        Controls.Add(buttonRow);

        AcceptButton = _btnOk;
        CancelButton = _btnCancel;
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
                MessageBox.Show("Bitte einen Namen eingeben.", "SpeedScanManager",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
