using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Dialog for configuring the file name format for scanned documents.
/// Three modes: OS default, timestamp pattern, or custom name with counter.
/// </summary>
internal class FileNameFormatDialog : Form
{
    public enum FormatMode
    {
        OsDefault,
        Timestamp,
        Custom
    }

    public FormatMode SelectedMode { get; set; } = FormatMode.Timestamp;
    public string CustomFileName { get; set; } = "unbenannt";
    public int CounterDigits { get; set; } = 3;
    /// <summary>File format used for the extension in the preview.</summary>
    public FileFormat PreviewFormat { get; set; } = FileFormat.Pdf;

    public event EventHandler? ApplyClicked;

    private readonly RadioButton _rbOsDefault;
    private readonly RadioButton _rbTimestamp;
    private readonly RadioButton _rbCustom;
    private readonly TextBox _txtCustomName;
    private readonly ComboBox _cbCounterDigits;
    private readonly Label _lblPreview;
    private Button _btnApply;

    public FileNameFormatDialog()
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Dateinamenformat";
        Icon = TrayIcons.GetAppIcon();
        ClientSize = new Size(550, 270);
        ShowInTaskbar = false;

        var font = UiFonts.Default;
        Font = font;

        // === Root layout with real padding ===
        var rootPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 30, 14, 8),
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };

        // === Content table (no GroupBox, plain label heading) ===
        // 6 rows: heading, 2 radio buttons (indented), custom radio, input row, preview
        // 5 columns: label | textbox | gap | label | combobox
        var contentTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            ColumnStyles =
            {
                new ColumnStyle(SizeType.Absolute, 90f),    // Dateiname label
                new ColumnStyle(SizeType.Absolute, 210f),   // Dateiname textbox
                new ColumnStyle(SizeType.Absolute, 40f),    // gap
                new ColumnStyle(SizeType.Absolute, 60f),    // Zähler label
                new ColumnStyle(SizeType.Percent, 100f)     // Zähler combobox + remainder
            },
            RowCount = 6,
            RowStyles =
            {
                new RowStyle(SizeType.Absolute, 20f),  // "Datum und Uhrzeit" heading
                new RowStyle(SizeType.Absolute, 26f),  // rbOsDefault (indented)
                new RowStyle(SizeType.Absolute, 26f),  // rbTimestamp (indented)
                new RowStyle(SizeType.Absolute, 26f),  // rbCustom
                new RowStyle(SizeType.Absolute, 30f),  // combined Dateiname + Zähler row
                new RowStyle(SizeType.Absolute, 24f)   // Preview row
            },
            Margin = new Padding(12, 0, 8, 4),
            Padding = Padding.Empty
        };

        var lblHeading = new Label
        {
            Text = "Datum und Uhrzeit",
            Font = font,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };

        _rbOsDefault = new RadioButton
        {
            Text = "Benutzen Sie die Einstellung Ihres Betriebssystems",
            Font = font,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(20, 0, 0, 0)
        };

        _rbTimestamp = new RadioButton
        {
            Text = "JJJJMMTThhmmss",
            Font = font,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(20, 0, 0, 0),
            Checked = true
        };

        _rbCustom = new RadioButton
        {
            Text = "Benutzerdefinierter Dateiname",
            Font = font,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };

        var lblFileName = new Label
        {
            Text = "Dateiname:",
            Font = font,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(8, 0, 0, 0)
        };

        _txtCustomName = new TextBox
        {
            Text = FileNamePattern.DefaultCustomName,
            Font = font,
            MaxLength = FileNamePattern.MaxCustomNameLength,
            Size = new Size(200, 22),
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Enabled = false
        };

        var lblCounter = new Label
        {
            Text = "Zähler:",
            Font = font,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(16, 0, 0, 0)
        };

        _cbCounterDigits = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = font,
            Size = new Size(110, 22),
            Dock = DockStyle.Left,
            Margin = Padding.Empty,
            Enabled = false
        };
        _cbCounterDigits.Items.AddRange(new object[] { "3 Ziffern", "4 Ziffern", "5 Ziffern" });
        _cbCounterDigits.SelectedIndex = 0;

        _lblPreview = new Label
        {
            Text = "",
            Font = font,
            ForeColor = SystemColors.ControlText,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(16, 0, 0, 0)
        };

        // Add controls to content table
        // Row 0: "Datum und Uhrzeit" heading
        contentTable.Controls.Add(lblHeading, 0, 0);
        contentTable.SetColumnSpan(lblHeading, 5);

        // Row 1-2: RadioButtons under heading (indented)
        contentTable.Controls.Add(_rbOsDefault, 0, 1);
        contentTable.SetColumnSpan(_rbOsDefault, 5);

        contentTable.Controls.Add(_rbTimestamp, 0, 2);
        contentTable.SetColumnSpan(_rbTimestamp, 5);

        // Row 3: Custom radio button (separate, not under heading)
        contentTable.Controls.Add(_rbCustom, 0, 3);
        contentTable.SetColumnSpan(_rbCustom, 5);

        // Row 4: Combined Dateiname + Zähler in one horizontal row
        contentTable.Controls.Add(lblFileName, 0, 4);
        contentTable.Controls.Add(_txtCustomName, 1, 4);
        contentTable.Controls.Add(lblCounter, 3, 4);
        contentTable.Controls.Add(_cbCounterDigits, 4, 4);

        // Row 5: Preview label spanning all columns
        contentTable.Controls.Add(_lblPreview, 0, 5);
        contentTable.SetColumnSpan(_lblPreview, 5);

        // === Buttons ===
        var btnOk = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Size = new Size(95, 28),
            Font = font,
            Margin = Padding.Empty
        };

        var btnCancel = new Button
        {
            Text = "Abbrechen",
            DialogResult = DialogResult.Cancel,
            Size = new Size(95, 28),
            Font = font,
            Margin = Padding.Empty
        };

        var btnHelp = new Button
        {
            Text = "Hilfe",
            Size = new Size(95, 28),
            Font = font,
            Margin = Padding.Empty
        };
        btnHelp.Click += (s, e) => { using var h = new HelpForm("filename-format"); h.ShowDialog(this); };

        var btnApply = new Button
        {
            Text = "Übernehmen",
            Size = new Size(95, 28),
            Font = font,
            Margin = Padding.Empty,
            Enabled = false
        };
        btnApply.Click += (s, e) => ApplySettings();
        _btnApply = btnApply;

        // Button panel using TableLayoutPanel for reliable layout
        // Columns: filler | OK | gap | Abbrechen | gap | Übernehmen | gap | Hilfe | rightMargin
        var buttonPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 36,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            ColumnCount = 9,
            ColumnStyles =
            {
                new ColumnStyle(SizeType.Percent, 100f),  // filler
                new ColumnStyle(SizeType.Absolute, 95f),   // OK
                new ColumnStyle(SizeType.Absolute, 14f),   // gap
                new ColumnStyle(SizeType.Absolute, 95f),   // Abbrechen
                new ColumnStyle(SizeType.Absolute, 14f),   // gap
                new ColumnStyle(SizeType.Absolute, 95f),   // Übernehmen
                new ColumnStyle(SizeType.Absolute, 14f),   // gap
                new ColumnStyle(SizeType.Absolute, 95f),   // Hilfe
                new ColumnStyle(SizeType.Absolute, 16f)    // right margin
            },
            RowCount = 1,
            RowStyles = { new RowStyle(SizeType.Absolute, 36f) }
        };

        btnOk.Dock = DockStyle.Fill;
        btnCancel.Dock = DockStyle.Fill;
        btnApply.Dock = DockStyle.Fill;
        btnHelp.Dock = DockStyle.Fill;
        btnOk.Margin = new Padding(0, 4, 0, 4);
        btnCancel.Margin = new Padding(0, 4, 0, 4);
        btnApply.Margin = new Padding(0, 4, 0, 4);
        btnHelp.Margin = new Padding(0, 4, 0, 4);

        buttonPanel.Controls.Add(btnOk, 1, 0);
        buttonPanel.Controls.Add(btnCancel, 3, 0);
        buttonPanel.Controls.Add(btnApply, 5, 0);
        buttonPanel.Controls.Add(btnHelp, 7, 0);

        rootPanel.Controls.Add(contentTable);
        rootPanel.Controls.Add(buttonPanel);

        Controls.Add(rootPanel);

        AcceptButton = btnOk;
        CancelButton = btnCancel;

        // Event wiring
        _rbOsDefault.CheckedChanged += (s, e) => { OnSelectionChanged(); UpdateApplyButton(); };
        _rbTimestamp.CheckedChanged += (s, e) => { OnSelectionChanged(); UpdateApplyButton(); };
        _rbCustom.CheckedChanged += (s, e) => { OnSelectionChanged(); UpdateApplyButton(); };
        _txtCustomName.TextChanged += (s, e) => { UpdatePreview(); UpdateApplyButton(); };
        _cbCounterDigits.SelectedIndexChanged += (s, e) => { UpdatePreview(); UpdateApplyButton(); };

        UpdatePreview();
        ResumeLayout(false);
        PerformLayout();
    }

    protected override void OnShown(EventArgs e)
    {
        // Sync UI from restored property values
        _rbOsDefault.Checked = SelectedMode == FormatMode.OsDefault;
        _rbTimestamp.Checked = SelectedMode == FormatMode.Timestamp;
        _rbCustom.Checked = SelectedMode == FormatMode.Custom;

        _txtCustomName.Text = CustomFileName;
        _cbCounterDigits.SelectedIndex = CounterDigits switch
        {
            3 => 0,
            4 => 1,
            5 => 2,
            _ => 0
        };

        OnSelectionChanged();
        UpdateApplyButton();
        base.OnShown(e);
    }

    private void OnSelectionChanged()
    {
        bool isCustom = _rbCustom.Checked;
        _txtCustomName.Enabled = isCustom;
        _cbCounterDigits.Enabled = isCustom;
        UpdatePreview();
    }

    private FormatMode CurrentMode => _rbOsDefault.Checked ? FormatMode.OsDefault
        : _rbTimestamp.Checked ? FormatMode.Timestamp
        : FormatMode.Custom;

    private string CurrentName => string.IsNullOrWhiteSpace(_txtCustomName.Text)
        ? FileNamePattern.DefaultCustomName
        : _txtCustomName.Text.Trim();

    private int CurrentDigits => _cbCounterDigits.SelectedIndex switch { 1 => 4, 2 => 5, _ => 3 };

    private void UpdatePreview()
    {
        _lblPreview.Text = $"Beispiel: {GetExampleFileName()}{FileNamePattern.GetExtension(PreviewFormat)}";
    }

    /// <summary>
    /// Generates a file name example (without extension) based on the current dialog selections.
    /// </summary>
    public string GetExampleFileName()
        => FileNamePattern.BuildBaseName(CurrentMode, CurrentName, CurrentDigits, DateTime.Now, 1);

    private bool HasUnsavedChanges()
        => CurrentMode != SelectedMode || CurrentName != CustomFileName || CurrentDigits != CounterDigits;

    private void UpdateApplyButton()
    {
        _btnApply.Enabled = HasUnsavedChanges();
    }

    /// <summary>
    /// Validates the custom name (only when the custom mode is selected). Shows a message on error.
    /// </summary>
    private bool ValidateInput()
    {
        if (CurrentMode != FormatMode.Custom)
            return true;
        string? error = FileNamePattern.ValidateCustomName(CurrentName);
        if (error == null)
            return true;
        MessageBox.Show(this, error, "SpeedScan Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        _txtCustomName.Focus();
        _txtCustomName.SelectAll();
        return false;
    }

    private bool ApplySettings()
    {
        if (!ValidateInput())
            return false;

        SelectedMode = CurrentMode;
        CustomFileName = CurrentName;
        CounterDigits = CurrentDigits;
        UpdateApplyButton();
        ApplyClicked?.Invoke(this, EventArgs.Empty);
        return true;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK && !ApplySettings())
        {
            e.Cancel = true;
            return;
        }
        base.OnFormClosing(e);
    }
}
