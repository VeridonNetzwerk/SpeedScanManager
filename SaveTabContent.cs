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

    private FileNameFormatDialog.FormatMode _formatMode = FileNameFormatDialog.FormatMode.Timestamp;
    private string _customFileName = "unbenannt";
    private int _counterDigits = 3;

    public string FolderPath { get; private set; }
    public FileNameFormatDialog.FormatMode FormatMode => _formatMode;
    public string CustomFileName => _customFileName;
    public int CounterDigits => _counterDigits;

    public (string folder, FileNameFormatDialog.FormatMode mode, string customName, int digits) GetSaveConfig()
        => (FolderPath, _formatMode, _customFileName, _counterDigits);

    public void RestoreSaveConfig(string folderPath, FileNameFormatDialog.FormatMode mode, string customName, int digits)
    {
        FolderPath = folderPath;
        _formatMode = mode;
        _customFileName = customName;
        _counterDigits = digits;
        _txtFolderPath.Text = folderPath;
        _lblFilePreview.Text = $"z.B.) {GenerateExampleFileName()}.pdf";
    }

    public SaveTabContent()
    {
        // Default folder: Documents\SpeedScanManager in user profile
        string defaultFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SpeedScanManager");
        Directory.CreateDirectory(defaultFolder);
        FolderPath = defaultFolder;

        var font = new Font("Microsoft Sans Serif", 8.25f);
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
            Text = FolderPath,
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
            Text = "Dateiname-Format...",
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
            Text = $"z.B.) {GenerateExampleFileName()}.pdf",
            Location = new Point(176, 116),
            AutoSize = true,
            Font = font,
            ForeColor = SystemColors.ControlText
        };

        // === Rename checkbox (initially disabled) ===
        _chkRenameAfterScan = new CheckBox
        {
            Text = "Datei nach Scan umbenennen",
            Location = new Point(176, 141),
            AutoSize = true,
            Font = font,
            Enabled = false
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
    }

    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            SelectedPath = FolderPath,
            Description = "Speicherordner für gescannte Dokumente wählen"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            FolderPath = dialog.SelectedPath;
            _txtFolderPath.Text = FolderPath;
        }
    }

    private void OpenFileNameFormatDialog()
    {
        using var dialog = new FileNameFormatDialog();

        // Restore previous settings
        switch (_formatMode)
        {
            case FileNameFormatDialog.FormatMode.OsDefault:
                dialog.SelectedMode = FileNameFormatDialog.FormatMode.OsDefault;
                break;
            case FileNameFormatDialog.FormatMode.Timestamp:
                dialog.SelectedMode = FileNameFormatDialog.FormatMode.Timestamp;
                break;
            case FileNameFormatDialog.FormatMode.Custom:
                dialog.SelectedMode = FileNameFormatDialog.FormatMode.Custom;
                break;
        }
        dialog.CustomFileName = _customFileName;
        dialog.CounterDigits = _counterDigits;

        dialog.ApplyClicked += (s, e) =>
        {
            _formatMode = dialog.SelectedMode;
            _customFileName = dialog.CustomFileName;
            _counterDigits = dialog.CounterDigits;
            _lblFilePreview.Text = $"z.B.) {GenerateExampleFileName()}.pdf";
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _formatMode = dialog.SelectedMode;
            _customFileName = dialog.CustomFileName;
            _counterDigits = dialog.CounterDigits;

            // Update preview in main tab
            _lblFilePreview.Text = $"z.B.) {GenerateExampleFileName()}.pdf";
        }
    }

    /// <summary>
    /// Generates an example file name based on the current format settings.
    /// </summary>
    public string GenerateExampleFileName()
    {
        return _formatMode switch
        {
            FileNameFormatDialog.FormatMode.OsDefault => DateTime.Now.ToString("yyyyMMdd_HHmmss"),
            FileNameFormatDialog.FormatMode.Timestamp => DateTime.Now.ToString("yyyyMMddHHmmss"),
            FileNameFormatDialog.FormatMode.Custom => $"{_customFileName}_{new string('0', _counterDigits)}",
            _ => "unbenannt_000"
        };
    }
}
