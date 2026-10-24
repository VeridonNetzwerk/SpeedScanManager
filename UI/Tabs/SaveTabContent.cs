using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Content panel for the "Speichern" tab.
/// Contains folder selection, file name format configuration, and rename checkbox.
/// </summary>
internal class SaveTabContent : Panel
{
    private readonly TextBox _txtFolderPath;
    private readonly Button _btnBrowse;
    private readonly Button _btnFileNameFormat;
    private readonly Label _lblFilePreview;
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

        var font = UiFonts.Default;
        Dock = DockStyle.Fill;
        BackColor = Color.White;

        // === Blue gradient info bar (Dock.Top) ===
        var infoBar = new GradientInfoBar("Geben Sie den zu benutzenden Ordner und das gewünschte Dateinamenformat an.");

        // === Content panel below info bar ===
        var contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White
        };

        // === Folder selection ===
        var lblFolder = new Label
        {
            Text = "Speicherordner für Bilddaten:",
            Location = new Point(16, 11),
            AutoSize = true,
            Font = font
        };

        _txtFolderPath = new TextBox
        {
            Text = _settings.FolderPath,
            Location = new Point(176, 8),
            ReadOnly = true,
            Font = font,
            BackColor = SystemColors.Control,
            AutoSize = false,
            Size = new Size(438, 22)
        };

        // Browse button positioned BELOW the textbox, right-aligned to its right edge
        _btnBrowse = new Button
        {
            Text = "Durchsuchen...",
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            Location = new Point(514, 39),
            Size = new Size(100, 25),
            Font = font
        };
        _btnBrowse.Click += (s, e) => BrowseFolder();

        // === File name format ===
        _btnFileNameFormat = new Button
        {
            Text = "Dateinamenformat...",
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            Location = new Point(176, 79),
            Size = new Size(165, 26),
            Font = font
        };
        _btnFileNameFormat.Click += (s, e) => OpenFileNameFormatDialog();

        // Preview label directly under the button
        _lblFilePreview = new Label
        {
            Text = "",
            Location = new Point(176, 116),
            AutoSize = true,
            Font = font,
            ForeColor = SystemColors.ControlText
        };

        // === Rename checkbox ===
        _chkRenameAfterScan = new CheckBox
        {
            Text = "Datei nach Scan umbenennen",
            Location = new Point(176, 141),
            AutoSize = true,
            Font = font
        };
        _chkRenameAfterScan.CheckedChanged += (s, e) =>
        {
            if (!_loading)
                _settings.RenameAfterScan = _chkRenameAfterScan.Checked;
        };

        contentPanel.Controls.AddRange(new Control[]
        {
            lblFolder,
            _txtFolderPath,
            _btnBrowse,
            _btnFileNameFormat,
            _lblFilePreview,
            _chkRenameAfterScan
        });

        Controls.AddRange(new Control[] { contentPanel, infoBar });
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
        _lblFilePreview.Text = $"z. B. {GenerateExampleFileName()}{FileNamePattern.GetExtension(_settings.FileFormat)}";
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
