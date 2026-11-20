using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Content of the "Speichern" settings page: folder selection, file name format (with an
/// example file name) and the rename option, grouped in settings cards.
/// </summary>
internal class SaveTabContent : ModernStack
{
    private readonly TextBox _txtFolderPath;
    private readonly Button _btnBrowse;
    private readonly Button _btnFileNameFormat;
    private readonly SettingRow _rowFileName;
    private readonly CheckBox _chkRenameAfterScan;
    private readonly ScanSettings _settings;
    private bool _loading;

    public string FolderPath => _settings.FolderPath;
    public FileNameFormatDialog.FormatMode FormatMode => _settings.FileNameFormat;
    public string CustomFileName => _settings.CustomFileName;
    public int CounterDigits => _settings.CounterDigits;

    public (string folder, FileNameFormatDialog.FormatMode mode, string customName, int digits) GetSaveConfig()
        => (_settings.FolderPath, _settings.FileNameFormat, _settings.CustomFileName, _settings.CounterDigits);

    public void RestoreSaveConfig(string folderPath, FileNameFormatDialog.FormatMode mode, string customName, int digits)
    {
        _settings.FolderPath = folderPath;
        _settings.FileNameFormat = mode;
        _settings.CustomFileName = customName;
        _settings.CounterDigits = digits;
        LoadFromSettings();
    }

    public SaveTabContent(ScanSettings settings)
    {
        _settings = settings;

        if (string.IsNullOrEmpty(_settings.FolderPath))
            _settings.FolderPath = ScanSettings.DefaultFolderPath;

        // === Folder selection: path box + Durchsuchen ===
        _txtFolderPath = new TextBox
        {
            Text = _settings.FolderPath,
            ReadOnly = true,
            AutoSize = false,
            Size = new Size(300, 28),
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(0, 2, 8, 2)
        };
        _btnBrowse = new Button
        {
            Text = "Durchsuchen...",
            Size = new Size(140, 32),
            Anchor = AnchorStyles.Right,
            Margin = Padding.Empty
        };
        ThemeManager.SetGlyph(_btnBrowse, Glyph.Folder);
        _btnBrowse.Click += (s, e) => BrowseFolder();

        var folderBox = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            Size = new Size(400, 32),
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        folderBox.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        folderBox.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        folderBox.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        folderBox.Controls.Add(_txtFolderPath, 0, 0);
        folderBox.Controls.Add(_btnBrowse, 1, 0);

        // === File name format ===
        _btnFileNameFormat = new Button
        {
            Text = "Dateinamenformat...",
            Size = new Size(180, 32)
        };
        ThemeManager.SetGlyph(_btnFileNameFormat, Glyph.FileType);
        _btnFileNameFormat.Click += (s, e) => OpenFileNameFormatDialog();

        // === Rename after scan ===
        _chkRenameAfterScan = new ToggleSwitch();
        _chkRenameAfterScan.CheckedChanged += (s, e) =>
        {
            if (!_loading)
                _settings.RenameAfterScan = _chkRenameAfterScan.Checked;
        };

        var cardFolder = new SettingsCard();
        var rowFolder = cardFolder.AddRow("Speicherordner für Bilddaten", "Die gescannten Dateien werden in diesem Ordner gespeichert.", folderBox);
        rowFolder.Stacked = true;

        var cardName = new SettingsCard("Dateiname");
        _rowFileName = cardName.AddRow("Dateinamenformat", "", _btnFileNameFormat);
        cardName.AddRow("Datei nach Scan umbenennen", "Nach dem Scan können Sie einen eigenen Dateinamen eingeben.", _chkRenameAfterScan);

        Controls.Add(cardFolder);
        Controls.Add(cardName);
        LoadFromSettings();
    }

    /// <summary>
    /// Refreshes all controls from the settings without raising change notifications.
    /// </summary>
    public void LoadFromSettings()
    {
        _loading = true;
        try
        {
            _txtFolderPath.Text = _settings.FolderPath;
            _chkRenameAfterScan.Checked = _settings.RenameAfterScan;
            UpdatePreview();
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>
    /// Updates the example file name (also called when the file format changes).
    /// </summary>
    public void UpdatePreview()
    {
        _rowFileName.Description = $"z. B. {GenerateExampleFileName()}{FileNamePattern.GetExtension(_settings.FileFormat)}";
    }

    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            SelectedPath = FolderPath,
            Description = "Speicherordner für gescannte Dokumente wählen"
        };

        if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
        {
            _settings.FolderPath = dialog.SelectedPath;
            _txtFolderPath.Text = _settings.FolderPath;
        }
    }

    private void OpenFileNameFormatDialog()
    {
        using var dialog = new FileNameFormatDialog
        {
            // Restore previous settings
            SelectedMode = _settings.FileNameFormat,
            CustomFileName = _settings.CustomFileName,
            CounterDigits = _settings.CounterDigits,
            PreviewFormat = _settings.FileFormat
        };

        dialog.ApplyClicked += (s, e) =>
        {
            _settings.FileNameFormat = dialog.SelectedMode;
            _settings.CustomFileName = dialog.CustomFileName;
            _settings.CounterDigits = dialog.CounterDigits;
            UpdatePreview();
        };

        if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
        {
            _settings.FileNameFormat = dialog.SelectedMode;
            _settings.CustomFileName = dialog.CustomFileName;
            _settings.CounterDigits = dialog.CounterDigits;
            UpdatePreview();
        }
    }

    /// <summary>
    /// Generates an example file name (without extension) based on the current format settings.
    /// </summary>
    public string GenerateExampleFileName()
        => FileNamePattern.BuildBaseName(_settings, DateTime.Now, 1);
}
