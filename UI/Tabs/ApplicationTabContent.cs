using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Represents a scan application type (e.g. Scan to Folder, Scan to E-Mail, Scan to Print).
/// Custom = a user-added program (see ScanSettings.CustomApplicationName/Path/Arguments).
/// </summary>
internal enum ApplicationType
{
    ScanToFolder,
    ScanToEmail,
    ScanToPrint,
    ScanToWord,
    ScanToExcel,
    ScanToPowerPoint,
    ScanPictureFolder,
    EditWithPdf,
    Custom
}

/// <summary>
/// Represents an entry of the application dropdown. For Type == Custom, ExePath and
/// Arguments describe the program to launch.
/// </summary>
internal record ApplicationEntry(string Name, ApplicationType Type, string? ExePath = null, string? Arguments = null);

/// <summary>
/// Content of the "Anwendung" settings page: application dropdown, e-mail/print options
/// (shown for Scan to E-Mail / Scan to Print) and the manage-applications button, grouped in
/// settings cards.
/// </summary>
internal class ApplicationTabContent : ModernStack
{
    private readonly ComboBox _cbApplication;
    private readonly Button _btnManage;
    private readonly Button _btnEmailOptions;
    private readonly Button _btnPrintOptions;
    private readonly SettingRow _rowEmailOptions;
    private readonly SettingRow _rowPrintOptions;
    private readonly ScanSettings _settings;
    private readonly List<CustomApplication> _customApps;
    private bool _loading;

    public ApplicationEntry SelectedApplication { get; private set; }
    public ApplicationType SelectedApplicationType => SelectedApplication.Type;

    /// <summary>Raised when the list of custom applications was changed by the user.</summary>
    public event EventHandler? CustomApplicationsChanged;

    private static readonly List<ApplicationEntry> BuiltInApps = new()
    {
        new ApplicationEntry("Scan to Folder", ApplicationType.ScanToFolder),
        new ApplicationEntry("Scan to E-Mail", ApplicationType.ScanToEmail),
        new ApplicationEntry("Scan to Print", ApplicationType.ScanToPrint),
        new ApplicationEntry("Scan to Word", ApplicationType.ScanToWord),
        new ApplicationEntry("Scan to Excel", ApplicationType.ScanToExcel),
        new ApplicationEntry("Scan to PowerPoint", ApplicationType.ScanToPowerPoint),
        new ApplicationEntry("Scan Picture Folder", ApplicationType.ScanPictureFolder),
        new ApplicationEntry("Edit with PDF", ApplicationType.EditWithPdf)
    };

    /// <param name="settings">Working copy of the scan settings.</param>
    /// <param name="customApps">Working copy of the user-added applications (edited in place).</param>
    public ApplicationTabContent(ScanSettings settings, List<CustomApplication> customApps)
    {
        _settings = settings;
        _customApps = customApps;
        SelectedApplication = BuiltInApps[0];

        _cbApplication = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Size = new Size(300, 24),
            MaxDropDownItems = 16
        };
        _cbApplication.SelectedIndexChanged += (s, e) =>
        {
            int idx = _cbApplication.SelectedIndex;
            var allApps = GetAllApps();
            if (idx >= 0 && idx < allApps.Count)
            {
                SelectedApplication = allApps[idx];
                if (!_loading)
                    WriteSelectionToSettings();
            }
            UpdateEmailOptionsVisibility();
            UpdatePrintOptionsVisibility();
        };

        _btnEmailOptions = new Button { Text = "E-Mail-Optionen...", Size = new Size(170, 32) };
        ThemeManager.SetGlyph(_btnEmailOptions, Glyph.Mail);
        _btnEmailOptions.Click += (s, e) => OpenEmailOptionsDialog();

        _btnPrintOptions = new Button { Text = "Druck-Optionen...", Size = new Size(170, 32) };
        ThemeManager.SetGlyph(_btnPrintOptions, Glyph.Printer);
        _btnPrintOptions.Click += (s, e) => OpenPrintOptionsDialog();

        _btnManage = new Button { Text = "Installieren/Deinstallieren...", Size = new Size(230, 32) };
        ThemeManager.SetGlyph(_btnManage, Glyph.PlusMinus);
        _btnManage.Click += (s, e) => OpenManageDialog();

        var cardApp = new SettingsCard("Anwendung nach dem Scan",
            "Die gescannten Dateien werden an diese Anwendung übergeben.");
        cardApp.AddRow("Anwendung", "Programm, das nach dem Scan gestartet wird.", _cbApplication, 300);
        _rowEmailOptions = cardApp.AddRow("E-Mail-Optionen", "Empfänger und Betreff der E-Mail.", _btnEmailOptions);
        _rowPrintOptions = cardApp.AddRow("Druck-Optionen", "Drucker, auf dem gedruckt wird.", _btnPrintOptions);
        _rowEmailOptions.Visible = false;
        _rowPrintOptions.Visible = false;

        var cardManage = new SettingsCard();
        cardManage.AddRow("Anwendungen verwalten", "Eigene Programme zur Liste hinzufügen oder daraus entfernen.", _btnManage);

        Controls.Add(cardApp);
        Controls.Add(cardManage);
        LoadFromSettings();
    }

    private List<ApplicationEntry> GetAllApps() => BuiltInApps
        .Concat(_customApps.Select(a => new ApplicationEntry(a.Name, ApplicationType.Custom, a.ExePath, a.Arguments)))
        .ToList();

    private void RefreshApplicationList()
    {
        _cbApplication.Items.Clear();
        foreach (var app in GetAllApps())
        {
            _cbApplication.Items.Add(app.Name);
        }
    }

    private void WriteSelectionToSettings()
    {
        _settings.ApplicationType = SelectedApplication.Type;
        if (SelectedApplication.Type == ApplicationType.Custom)
        {
            _settings.CustomApplicationName = SelectedApplication.Name;
            _settings.CustomApplicationPath = SelectedApplication.ExePath ?? "";
            _settings.CustomApplicationArguments = SelectedApplication.Arguments ?? "";
        }
        else
        {
            _settings.CustomApplicationName = "";
            _settings.CustomApplicationPath = "";
            _settings.CustomApplicationArguments = "";
        }
    }

    /// <summary>
    /// Index of the entry matching the settings (custom apps matched by name), or -1.
    /// </summary>
    private int FindIndex(ApplicationType type, string customName)
    {
        var allApps = GetAllApps();
        for (int i = 0; i < allApps.Count; i++)
        {
            if (allApps[i].Type != type) continue;
            if (type != ApplicationType.Custom || allApps[i].Name == customName)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Refreshes all controls from the settings without raising change notifications.
    /// </summary>
    public void LoadFromSettings()
    {
        _loading = true;
        try
        {
            RefreshApplicationList();
            int idx = FindIndex(_settings.ApplicationType, _settings.CustomApplicationName);
            _cbApplication.SelectedIndex = idx >= 0 ? idx : 0;
        }
        finally
        {
            _loading = false;
        }

        // Settings referenced an application that no longer exists -> fall back to Scan to Folder
        if (FindIndex(_settings.ApplicationType, _settings.CustomApplicationName) < 0)
            WriteSelectionToSettings();
        else if (SelectedApplication.Type == ApplicationType.Custom)
            WriteSelectionToSettings(); // pick up a changed path/arguments
    }

    private void OpenManageDialog()
    {
        var previous = SelectedApplication;
        using var dialog = new ApplicationManageDialog(_customApps);
        if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
        {
            _customApps.Clear();
            _customApps.AddRange(dialog.CustomApps.Select(a => a.Clone()));

            _loading = true;
            try
            {
                RefreshApplicationList();
                // Keep previous selection if it still exists
                int idx = FindIndex(previous.Type, previous.Name);
                _cbApplication.SelectedIndex = idx >= 0 ? idx : 0;
            }
            finally
            {
                _loading = false;
            }
            WriteSelectionToSettings();
            CustomApplicationsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateEmailOptionsVisibility()
    {
        _rowEmailOptions.Visible = SelectedApplication.Type == ApplicationType.ScanToEmail;
    }

    private void UpdatePrintOptionsVisibility()
    {
        _rowPrintOptions.Visible = SelectedApplication.Type == ApplicationType.ScanToPrint;
    }

    public void SetApplicationType(ApplicationType type)
    {
        int idx = FindIndex(type, _settings.CustomApplicationName);
        if (idx >= 0)
            _cbApplication.SelectedIndex = idx;
    }

    private void OpenEmailOptionsDialog()
    {
        using var dlg = new ScanToEmailOptionsDialog
        {
            Recipient = _settings.EmailRecipient,
            SubjectTemplate = _settings.EmailSubjectTemplate
        };

        if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
        {
            _settings.EmailRecipient = dlg.Recipient;
            _settings.EmailSubjectTemplate = dlg.SubjectTemplate;
        }
    }

    private void OpenPrintOptionsDialog()
    {
        using var dlg = new ScanToPrintOptionsDialog
        {
            SelectedPrinterName = _settings.PrinterName
        };

        if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
        {
            _settings.PrinterName = dlg.SelectedPrinterName;
        }
    }
}
