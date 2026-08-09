using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Represents a scan application type (e.g. Scan to Folder, Scan to E-Mail, Scan to Print).
/// Extensible for future phases.
/// </summary>
internal enum ApplicationType
{
    ScanToFolder,
    ScanToEmail,   // Phase 12
    ScanToPrint    // Phase 13
}

/// <summary>
/// Represents a user-configured application entry.
/// </summary>
internal record ApplicationEntry(string Name, ApplicationType Type);

/// <summary>
/// Content panel for the "Anwendung" tab.
/// Contains application dropdown and manage-applications button.
/// </summary>
internal class ApplicationTabContent : Panel
{
    private readonly ComboBox _cbApplication;
    private readonly Button _btnManage;
    private readonly Button _btnEmailOptions;
    private readonly Button _btnPrintOptions;
    private readonly ScanSettings _settings;

    public ApplicationEntry SelectedApplication { get; private set; }
    public ApplicationType SelectedApplicationType => SelectedApplication.Type;

    private readonly List<ApplicationEntry> _builtInApps = new()
    {
        new ApplicationEntry("Scan to Folder", ApplicationType.ScanToFolder),
        new ApplicationEntry("Scan to E-Mail", ApplicationType.ScanToEmail),
        new ApplicationEntry("Scan to Print", ApplicationType.ScanToPrint)
    };

    private readonly List<ApplicationEntry> _customApps = new();

    public ApplicationTabContent(ScanSettings? settings = null)
    {
        _settings = settings ?? new ScanSettings();
        SelectedApplication = _builtInApps[0];

        var font = new Font("Microsoft Sans Serif", 8.25f);
        Dock = DockStyle.Fill;
        BackColor = Color.White;

        // === Info bar ===
        var infoBar = new GradientInfoBar("Wählen Sie eine Anwendung.");

        // === Application dropdown ===
        var lblApplication = new Label
        {
            Text = "Anwendung:",
            Location = new Point(92, 30),
            AutoSize = true,
            Font = font
        };

        _cbApplication = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(176, 27),
            Size = new Size(435, 24),
            Font = font
        };
        RefreshApplicationList();
        _cbApplication.SelectedIndex = 0;
        _cbApplication.SelectedIndexChanged += (s, e) =>
        {
            int idx = _cbApplication.SelectedIndex;
            var allApps = GetAllApps();
            if (idx >= 0 && idx < allApps.Count)
                SelectedApplication = allApps[idx];
            UpdateEmailOptionsVisibility();
            UpdatePrintOptionsVisibility();
        };

        // === Email options button ===
        _btnEmailOptions = new Button
        {
            Text = "E-Mail-Optionen...",
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            Location = new Point(176, 64),
            Size = new Size(140, 26),
            Font = font,
            Visible = false
        };
        _btnEmailOptions.Click += (s, e) => OpenEmailOptionsDialog();

        // === Print options button ===
        _btnPrintOptions = new Button
        {
            Text = "Druck-Optionen...",
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            Location = new Point(176, 64),
            Size = new Size(140, 26),
            Font = font,
            Visible = false
        };
        _btnPrintOptions.Click += (s, e) => OpenPrintOptionsDialog();

        // === Manage button ===
        _btnManage = new Button
        {
            Text = "Installieren/Deinstallieren...",
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            Location = new Point(176, 112),
            Size = new Size(160, 26),
            Font = font,
            Image = TabIcons.CreateManageIcon(),
            ImageAlign = ContentAlignment.MiddleLeft,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(2, 0, 4, 0)
        };
        _btnManage.Click += (s, e) => OpenManageDialog();

        Controls.AddRange(new Control[]
        {
            infoBar,
            lblApplication, _cbApplication,
            _btnEmailOptions,
            _btnPrintOptions,
            _btnManage
        });

        Dock = DockStyle.Fill;
    }

    private List<ApplicationEntry> GetAllApps() => _builtInApps.Concat(_customApps).ToList();

    private void RefreshApplicationList()
    {
        _cbApplication.Items.Clear();
        foreach (var app in GetAllApps())
        {
            _cbApplication.Items.Add(app.Name);
        }
    }

    private void OpenManageDialog()
    {
        using var dialog = new ApplicationManageDialog(_customApps);
        if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
        {
            _customApps.Clear();
            _customApps.AddRange(dialog.CustomApps);
            RefreshApplicationList();

            // Keep selection valid
            if (_cbApplication.SelectedIndex < 0 || _cbApplication.SelectedIndex >= GetAllApps().Count)
                _cbApplication.SelectedIndex = 0;
        }
    }

    private void UpdateEmailOptionsVisibility()
    {
        _btnEmailOptions.Visible = SelectedApplication.Type == ApplicationType.ScanToEmail;
    }

    private void UpdatePrintOptionsVisibility()
    {
        _btnPrintOptions.Visible = SelectedApplication.Type == ApplicationType.ScanToPrint;
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
