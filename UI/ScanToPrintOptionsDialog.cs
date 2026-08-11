using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Options dialog for "Scan to Print" application.
/// Allows selecting the target printer from all installed printers.
/// </summary>
internal class ScanToPrintOptionsDialog : Form
{
    private readonly ComboBox _cbPrinter;
    private readonly Button _btnOk;
    private readonly Button _btnCancel;

    public string SelectedPrinterName { get; set; } = "";

    public ScanToPrintOptionsDialog()
    {
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Scan to Print – Optionen";
        Icon = TrayIcons.GetAppIcon();
        ClientSize = new Size(380, 150);
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;

        var font = new Font("Microsoft Sans Serif", 8.25f);
        Font = font;

        var lblPrinter = new Label
        {
            Text = "Zieldrucker:",
            Location = new Point(12, 16),
            AutoSize = true,
            Font = font
        };

        _cbPrinter = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(120, 13),
            Size = new Size(240, 24),
            Font = font
        };

        // Populate with installed printers
        foreach (string printer in PrinterSettings.InstalledPrinters)
        {
            _cbPrinter.Items.Add(printer);
        }

        // Select default printer
        string defaultPrinter = new PrinterSettings().PrinterName;
        int defaultIdx = _cbPrinter.Items.IndexOf(defaultPrinter);
        if (defaultIdx >= 0)
            _cbPrinter.SelectedIndex = defaultIdx;
        else if (_cbPrinter.Items.Count > 0)
            _cbPrinter.SelectedIndex = 0;

        _btnOk = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(190, 74),
            Size = new Size(75, 28),
            Font = font
        };

        _btnCancel = new Button
        {
            Text = "Abbrechen",
            DialogResult = DialogResult.Cancel,
            Location = new Point(285, 74),
            Size = new Size(75, 28),
            Font = font
        };

        Controls.AddRange(new Control[]
        {
            lblPrinter, _cbPrinter,
            _btnOk, _btnCancel
        });

        AcceptButton = _btnOk;
        CancelButton = _btnCancel;
    }

    protected override void OnShown(EventArgs e)
    {
        // Select the previously saved printer if it exists
        if (!string.IsNullOrEmpty(SelectedPrinterName))
        {
            int idx = _cbPrinter.Items.IndexOf(SelectedPrinterName);
            if (idx >= 0)
                _cbPrinter.SelectedIndex = idx;
        }
        base.OnShown(e);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK)
        {
            SelectedPrinterName = _cbPrinter.SelectedItem?.ToString() ?? "";
        }
        base.OnFormClosing(e);
    }
}
