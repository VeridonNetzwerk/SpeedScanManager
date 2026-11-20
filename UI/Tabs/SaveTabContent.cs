using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Content of the "Speichern" settings page: folder selection, file name format (with an
/// example file name) and the rename option, grouped in settings cards.
/// </summary>
internal class SaveTabContent : ModernStack
{
    private readonly FolderPathPicker _folderPicker;
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

        // === Folder selection: picker field with integrated Durchsuchen button ===
        _folderPicker = new FolderPathPicker("Durchsuchen...") { Path = _settings.FolderPath };
        _folderPicker.BrowseButton.Click += (s, e) => BrowseFolder();

        // === File name format ===
        _btnFileNameFormat = ModernButtons.SubDialog("Dateinamenformat...");
        _btnFileNameFormat.Click += (s, e) => OpenFileNameFormatDialog();

        // === Rename after scan ===
        _chkRenameAfterScan = new ToggleSwitch();
        _chkRenameAfterScan.CheckedChanged += (s, e) =>
        {
            if (!_loading)
                _settings.RenameAfterScan = _chkRenameAfterScan.Checked;
        };

        var cardFolder = new SettingsCard { Dense = true };
        var rowFolder = cardFolder.AddRow("Speicherordner für Bilddaten", "Die gescannten Dateien werden in diesem Ordner gespeichert.", _folderPicker);
        rowFolder.Stacked = true;

        var cardName = new SettingsCard("Dateiname") { Dense = true };
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
            _folderPicker.Path = _settings.FolderPath;
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
            _folderPicker.Path = _settings.FolderPath;
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
