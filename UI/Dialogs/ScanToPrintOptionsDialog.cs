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
internal class ScanToPrintOptionsDialog : ModernWindow
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
        StartPosition = FormStartPosition.CenterParent;
        Text = "Scan to Print – Optionen";
        Title = "Scan to Print";
        Subtitle = "Optionen für den Druck";
        HeaderButtons = ModernHeaderButtons.Close;
        ShowInTaskbar = false;
        AutoSizeToContent = true;
        MinimumContentWidth = 500;

        _cbPrinter = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList
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

        var card = new SettingsCard();
        card.AddRow("Zieldrucker", "Drucker für den Ausdruck.", _cbPrinter, 260);

        var stack = new ModernStack { Dock = DockStyle.Top, Padding = new Padding(20, 18, 20, 20) };
        stack.Controls.Add(card);
        Body.Controls.Add(stack);

        _btnCancel = AddFooterButton("Abbrechen", ThemeRole.Secondary, Glyph.Cross, DialogResult.Cancel);
        _btnOk = AddFooterButton("OK", ThemeRole.Primary, Glyph.Check, DialogResult.OK);

        AcceptButton = _btnOk;
        CancelButton = _btnCancel;

        ThemeManager.Apply(this);
        ResumeLayout(true);
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
