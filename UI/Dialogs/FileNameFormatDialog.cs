using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Dialog for configuring the file name format for scanned documents.
/// Three modes: OS default, timestamp pattern, or custom name with counter.
/// Modern layout (<see cref="ModernWindow"/>): the options in one card (one radio group),
/// the example file name in a second card.
/// </summary>
internal class FileNameFormatDialog : ModernWindow
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
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        Text = L.T("Dateinamenformat");
        Title = L.T("Dateinamenformat");
        Subtitle = L.T("Benennung der gescannten Dateien");
        HeaderButtons = ModernHeaderButtons.Help | ModernHeaderButtons.Close;
        HelpTopic = "filename-format";
        HeaderIcon = ColorIcon.Tile(Glyph.TextField, IconColors.Amber);
        AutoSizeToContent = true;
        MinimumContentWidth = 540;

        // All three radio buttons live in the same card (one radio group).
        var lblHeading = new SectionHeading(L.T("Datum und Uhrzeit")) { Height = 30 };

        _rbOsDefault = new RadioButton
        {
            Text = L.T("Benutzen Sie die Einstellung Ihres Betriebssystems"),
            AutoSize = false,
            Height = 32
        };

        _rbTimestamp = new RadioButton
        {
            Text = L.T("JJJJMMTThhmmss"),
            AutoSize = false,
            Height = 32,
            Checked = true
        };

        var lblCustomHeading = new SectionHeading(L.T("Benutzerdefiniert")) { Height = 30 };

        _rbCustom = new RadioButton
        {
            Text = L.T("Benutzerdefinierter Dateiname"),
            AutoSize = false,
            Height = 32
        };

        _txtCustomName = new TextBox
        {
            Text = FileNamePattern.DefaultCustomName,
            MaxLength = FileNamePattern.MaxCustomNameLength,
            Size = new Size(240, 24),
            Enabled = false
        };

        _cbCounterDigits = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Size = new Size(140, 24),
            Enabled = false
        };
        _cbCounterDigits.Items.AddRange(new object[] { "3 Ziffern", "4 Ziffern", "5 Ziffern" });
        _cbCounterDigits.SelectedIndex = 0;

        var cardFormat = new SettingsCard(L.T("Dateiname")) { ShowDividers = false, Dense = true, Icon = ColorIcon.Tile(Glyph.TextField, IconColors.Amber) };
        cardFormat.Add(lblHeading);
        cardFormat.Add(_rbOsDefault);
        cardFormat.Add(_rbTimestamp);
        cardFormat.Add(new Panel { Height = 6 });
        cardFormat.Add(lblCustomHeading);
        cardFormat.Add(_rbCustom);
        cardFormat.AddRow(L.T("Dateiname"), null, _txtCustomName, 240).Icon = ColorIcon.Tile(Glyph.TextField, IconColors.Blue);
        cardFormat.AddRow(L.T("Zähler"), null, _cbCounterDigits, 140).Icon = ColorIcon.Tile(Glyph.Hash, IconColors.Purple);

        // === Preview card ===
        _lblPreview = new Label
        {
            Text = "",
            AutoSize = false,
            Height = 26,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false,
            AutoEllipsis = true,
            Font = ThemeFonts.Semibold
        };
        _cardPreview = new SettingsCard(L.T("Vorschau")) { Dense = true, ShowDividers = false, Icon = FileIcon(FileFormat.Pdf) };
        _cardPreview.Add(_lblPreview);
        _cardPreview.Add(new Panel { Height = 6 });
        var cardPreview = _cardPreview;

        var stack = new ModernStack { Dock = DockStyle.Fill, Padding = new Padding(16, 16, 16, 16), Spacing = 12 };
        stack.Controls.Add(cardFormat);
        stack.Controls.Add(cardPreview);
        Body.Controls.Add(stack);

        // === Footer: Übernehmen left, Abbrechen and OK right ===
        var btnApply = AddFooterButton(L.T("Übernehmen"), ThemeRole.Secondary, Glyph.Apply, alignLeft: true);
        btnApply.Enabled = false;
        btnApply.Click += (s, e) => ApplySettings();
        _btnApply = btnApply;

        var btnCancel = AddFooterButton(L.T("Abbrechen"), ThemeRole.Secondary, Glyph.Cross, DialogResult.Cancel);
        var btnOk = AddFooterButton("OK", ThemeRole.Primary, Glyph.Check, DialogResult.OK);

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

        ThemeManager.Apply(this);
    }

    private readonly SettingsCard _cardPreview;

    private static ColorIcon FileIcon(FileFormat format) => format switch
    {
        FileFormat.Jpeg => ColorIcons.Jpeg,
        FileFormat.Png => ColorIcons.Png,
        _ => ColorIcons.Pdf
    };

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

        _cardPreview.Icon = FileIcon(PreviewFormat);
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
        TrayMessageBox.Show(this, error, "SpeedScan Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
