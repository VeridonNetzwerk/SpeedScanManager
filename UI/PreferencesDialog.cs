using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

internal class PreferencesDialog : Form
{
    private readonly ScanSettings _settings;
    private readonly CheckBox _cbCommNotification;
    private readonly CheckBox _cbScanStatus;
    private readonly CheckBox _cbStartupConfirmation;
    private readonly CheckBox _cbFlatbedConfirmation;

    public PreferencesDialog(ScanSettings settings)
    {
        _settings = settings;

        Text = "SpeedScan Manager - Präferenzen";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(485, 255);
        AutoScaleMode = AutoScaleMode.None;
        Font = new Font("Microsoft Sans Serif", 8.25f);
        ShowInTaskbar = false;

        var font = new Font("Microsoft Sans Serif", 8.25f);

        // === Footer (bottom panel with OK | Abbrechen | Hilfe) ===
        var footerPanel = new Panel
        {
            Height = 36,
            Dock = DockStyle.Bottom,
            BackColor = Color.FromArgb(240, 240, 240)
        };

        var btnOk = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Size = new Size(75, 22),
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            Font = font,
            Anchor = AnchorStyles.None
        };

        var btnCancel = new Button
        {
            Text = "Abbrechen",
            DialogResult = DialogResult.Cancel,
            Size = new Size(75, 22),
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            Font = font,
            Anchor = AnchorStyles.None
        };

        var btnHelp = new Button
        {
            Text = "Hilfe",
            Size = new Size(75, 22),
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            Font = font,
            Anchor = AnchorStyles.None
        };
        btnHelp.Click += (s, e) =>
        {
            using var help = new HelpForm();
            help.Show(this);
        };

        var footerTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            ColumnStyles =
            {
                new ColumnStyle(SizeType.Percent, 100f),    // Spacer links
                new ColumnStyle(SizeType.Absolute, 75f),     // OK
                new ColumnStyle(SizeType.Absolute, 12f),     // Spacer
                new ColumnStyle(SizeType.Absolute, 75f),     // Abbrechen
                new ColumnStyle(SizeType.Absolute, 12f),     // Spacer
                new ColumnStyle(SizeType.Absolute, 87f)      // Hilfe + rechter Rand
            },
            RowCount = 1,
            RowStyles = { new RowStyle(SizeType.Percent, 100f) },
            Margin = Padding.Empty
        };
        footerTable.Controls.Add(new Panel(), 0, 0);
        footerTable.Controls.Add(btnOk, 1, 0);
        footerTable.Controls.Add(new Panel(), 2, 0);
        footerTable.Controls.Add(btnCancel, 3, 0);
        footerTable.Controls.Add(new Panel(), 4, 0);
        footerTable.Controls.Add(btnHelp, 5, 0);
        footerPanel.Controls.Add(footerTable);

        // === TabControl ===
        var tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = font,
            Padding = new Point(6, 3)
        };

        // --- Tab 1: Statusanzeige ---
        var tabStatus = new TabPage("Statusanzeige");

        var grpComm = new GroupBox
        {
            Text = "Benachrichtigung des Kommunikationsstatus",
            Location = new Point(14, 26),
            Size = new Size(454, 80),
            Font = font
        };

        var lblCommDesc = new Label
        {
            Text = "Wählen Sie hier, ob der Scanner-Kommunikationsstatus als Popup-Benachrichtigung angezeigt werden soll.",
            Location = new Point(13, 14),
            Size = new Size(428, 28),
            AutoSize = false,
            Font = font
        };

        _cbCommNotification = new CheckBox
        {
            Text = "Eine Benachrichtigung des Kommunikationsstatus anzeigen",
            Location = new Point(13, 46),
            AutoSize = true,
            Font = font,
            Checked = true
        };

        grpComm.Controls.Add(lblCommDesc);
        grpComm.Controls.Add(_cbCommNotification);

        var grpScanStatus = new GroupBox
        {
            Text = "Status des Scanvorgangs anzeigen",
            Location = new Point(14, 115),
            Size = new Size(454, 65),
            Font = font
        };

        var lblScanStatusDesc = new Label
        {
            Text = "Wählen Sie, ob der Status des Scanvorgangs angezeigt werden soll oder nicht.",
            Location = new Point(13, 14),
            Size = new Size(428, 28),
            AutoSize = false,
            Font = font
        };

        _cbScanStatus = new CheckBox
        {
            Text = "Den Status des Scanvorgangs anzeigen (empfohlen)",
            Location = new Point(13, 34),
            AutoSize = true,
            Font = font,
            Checked = true
        };

        grpScanStatus.Controls.Add(lblScanStatusDesc);
        grpScanStatus.Controls.Add(_cbScanStatus);

        tabStatus.Controls.Add(grpComm);
        tabStatus.Controls.Add(grpScanStatus);

        // --- Tab 2: Bestätigung ---
        var tabConfirm = new TabPage("Bestätigung");

        var grpStartup = new GroupBox
        {
            Text = "Bestätigungsmeldung beim Start",
            Location = new Point(14, 26),
            Size = new Size(454, 50),
            Font = font
        };

        var lblStartupDesc = new Label
        {
            Text = "Beim Start von SpeedScan Manager eine Bestätigungsmeldung anzeigen.",
            Location = new Point(13, 14),
            Size = new Size(428, 20),
            AutoSize = false,
            Font = font
        };

        _cbStartupConfirmation = new CheckBox
        {
            Text = "Die Meldung anzeigen",
            Location = new Point(13, 30),
            AutoSize = true,
            Font = font,
            Checked = true
        };

        grpStartup.Controls.Add(lblStartupDesc);
        grpStartup.Controls.Add(_cbStartupConfirmation);

        var grpFlatbed = new GroupBox
        {
            Text = "Bestätigungsmeldung beim Flachbettscannen",
            Location = new Point(14, 85),
            Size = new Size(454, 75),
            Font = font
        };

        var lblFlatbedDesc = new Label
        {
            Text = "Eine Bestätigungsmeldung wird angezeigt, wenn der automatische Erkennungsmodus aktiviert ist.",
            Location = new Point(13, 14),
            Size = new Size(428, 26),
            AutoSize = false,
            Font = font
        };

        _cbFlatbedConfirmation = new CheckBox
        {
            Text = "Die Meldung anzeigen",
            Location = new Point(13, 38),
            AutoSize = true,
            Font = font,
            Checked = true
        };

        grpFlatbed.Controls.Add(lblFlatbedDesc);
        grpFlatbed.Controls.Add(_cbFlatbedConfirmation);

        tabConfirm.Controls.Add(grpStartup);
        tabConfirm.Controls.Add(grpFlatbed);

        tabControl.TabPages.Add(tabStatus);
        tabControl.TabPages.Add(tabConfirm);

        // Add controls: footer first (Dock.Bottom), then TabControl (Dock.Fill)
        Controls.Add(footerPanel);
        Controls.Add(tabControl);

        LoadSettings();

        // Save on OK
        btnOk.Click += (s, e) => SaveSettings();
    }

    private void LoadSettings()
    {
        _cbCommNotification.Checked = _settings.ShowCommStatusNotification;
        _cbScanStatus.Checked = _settings.ShowScanStatusNotification;
        _cbStartupConfirmation.Checked = _settings.ShowStartupConfirmation;
        _cbFlatbedConfirmation.Checked = _settings.ShowFlatbedConfirmation;
    }

    private void SaveSettings()
    {
        _settings.ShowCommStatusNotification = _cbCommNotification.Checked;
        _settings.ShowScanStatusNotification = _cbScanStatus.Checked;
        _settings.ShowStartupConfirmation = _cbStartupConfirmation.Checked;
        _settings.ShowFlatbedConfirmation = _cbFlatbedConfirmation.Checked;
    }
}
