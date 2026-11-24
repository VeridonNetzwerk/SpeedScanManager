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
        Text = L.T("Scan to Print – Optionen");
        Title = L.T("Scan to Print");
        Subtitle = L.T("Optionen für den Druck");
        HeaderButtons = ModernHeaderButtons.Close;
        HeaderIcon = ColorIcon.Tile(Glyph.Printer, IconColors.Slate);
        ShowInTaskbar = false;
        AutoSizeToContent = true;
        MinimumContentWidth = 540;

        _cbPrinter = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 20
        };
        ComboLists.SetRowHeight(_cbPrinter, ComboLists.IconRow);
        _cbPrinter.DrawItem += (s, e) =>
        {
            if (e.Index < 0)
            {
                ThemeManager.DrawItemBackground(_cbPrinter, e);
                return;
            }
            // Entry 0 = Windows default printer
            ComboItemPainter.Draw(_cbPrinter, e, e.Index == 0 ? DefaultPrinterIcon : PrinterIcon, _cbPrinter.Items[e.Index]?.ToString() ?? "", null);
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

        var card = new SettingsCard { Dense = true };
        card.AddRow(L.T("Zieldrucker"), L.T("Drucker für den Ausdruck."), _cbPrinter, 270).Icon = ColorIcon.Tile(Glyph.Printer, IconColors.Slate);

        var stack = new ModernStack { Dock = DockStyle.Top, Padding = new Padding(16, 16, 16, 16) };
        stack.Controls.Add(card);
        Body.Controls.Add(stack);

        _btnCancel = AddFooterButton(L.T("Abbrechen"), ThemeRole.Secondary, Glyph.Cross, DialogResult.Cancel);
        _btnOk = AddFooterButton("OK", ThemeRole.Primary, Glyph.Check, DialogResult.OK);

        AcceptButton = _btnOk;
        CancelButton = _btnCancel;

        ThemeManager.Apply(this);
        ResumeLayout(true);
    }

    private static readonly ColorIcon PrinterIcon = ColorIcon.Tile(Glyph.Printer, IconColors.Slate);
    private static readonly ColorIcon DefaultPrinterIcon = ColorIcon.Tile(Glyph.Printer, IconColors.Blue);

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
