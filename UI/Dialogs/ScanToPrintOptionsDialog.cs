using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Options dialog for "Scan to Print" application.
/// Allows selecting the target printer from all installed printers. The first entry
/// "(Standarddrucker)" is stored as an empty name, so the current Windows default printer
/// is used at print time.
/// </summary>
internal class ScanToPrintOptionsDialog : Form
{
    private const string DefaultPrinterEntry = "(Standarddrucker)";

    private readonly ComboBox _cbPrinter;
    private readonly Button _btnOk;
    private readonly Button _btnCancel;

    /// <summary>Selected printer; "" = Windows default printer.</summary>
    public string SelectedPrinterName { get; set; } = "";

    public ScanToPrintOptionsDialog()
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Scan to Print – Optionen";
        Icon = TrayIcons.GetAppIcon();
        ClientSize = new Size(380, 120);
        ShowInTaskbar = false;

        var font = UiFonts.Default;
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

        _cbPrinter.Items.Add(DefaultPrinterEntry);
        try
        {
            foreach (string printer in PrinterSettings.InstalledPrinters)
                _cbPrinter.Items.Add(printer);
        }
        catch (Exception ex)
        {
            DiagLog.Write($"ScanToPrintOptionsDialog: printer enumeration failed: {ex.Message}");
        }
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
        ResumeLayout(false);
    }

    protected override void OnShown(EventArgs e)
    {
        // Select the previously saved printer; keep a no longer installed one visible.
        if (!string.IsNullOrEmpty(SelectedPrinterName))
        {
            int idx = -1;
            for (int i = 1; i < _cbPrinter.Items.Count; i++)
            {
                if (string.Equals(_cbPrinter.Items[i]?.ToString(), SelectedPrinterName, StringComparison.OrdinalIgnoreCase))
                {
                    idx = i;
                    break;
                }
            }
            if (idx < 0)
                idx = _cbPrinter.Items.Add(SelectedPrinterName);
            _cbPrinter.SelectedIndex = idx;
        }
        base.OnShown(e);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK)
        {
            SelectedPrinterName = _cbPrinter.SelectedIndex <= 0
                ? ""
                : _cbPrinter.SelectedItem?.ToString() ?? "";
        }
        base.OnFormClosing(e);
    }
}
