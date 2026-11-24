using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Scanner and driver information (opened from the version info via "Detail..."):
/// scanner name, manufacturer, product family, driver version and the file versions of the
/// program files. Modern layout: two cards, help in the header, OK in the footer.
/// </summary>
internal class ScannerDriverInfoDialog : ModernWindow
{
    public ScannerDriverInfoDialog(ScannerState? scannerState = null)
    {
        SuspendLayout();
        Text = L.T("SpeedScan Manager - Scanner- und Treiberinformationen");
        Title = L.T("Scanner- und Treiberinformationen");
        Subtitle = "SpeedScan Manager";
        HeaderButtons = ModernHeaderButtons.Help | ModernHeaderButtons.Close;
        HelpTopic = "driver-info";
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        AutoSizeToContent = true;
        MinimumContentWidth = 540;

        // === Scanner info rows ===
        var cardScanner = new SettingsCard(L.T("Scanner"));
        cardScanner.AddRow(L.T("Scannername"), null, InfoBox(scannerState?.SourceName ?? L.T("Kein Scanner verbunden")), 300);
        cardScanner.AddRow(L.T("Hersteller"), null, InfoBox(scannerState?.Manufacturer ?? "—"), 300);
        cardScanner.AddRow(L.T("Produktfamilie"), null, InfoBox(scannerState?.ProductFamily ?? "—"), 300);
        cardScanner.AddRow(L.T("Treiberversion"), null, InfoBox(scannerState?.DriverVersion ?? "—"), 300);

        // === File version section ===
        var lvwVersions = new ListView
        {
            Height = 128,
            View = View.Details,
            FullRowSelect = false,
            HideSelection = true,
            GridLines = false,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = Padding.Empty
        };

        lvwVersions.Columns.Add(L.T("Dateiname"), 200, HorizontalAlignment.Left);
        lvwVersions.Columns.Add(L.T("Version"), 90, HorizontalAlignment.Center);
        lvwVersions.Columns.Add(L.T("Zeit"), 150, HorizontalAlignment.Right);
        // "Dateiname" takes the remaining width of the card.
        lvwVersions.Resize += (s, e) =>
        {
            int rest = lvwVersions.ClientSize.Width - lvwVersions.Columns[1].Width - lvwVersions.Columns[2].Width;
            if (rest > 0) lvwVersions.Columns[0].Width = rest;
        };

        var asm = Assembly.GetExecutingAssembly();
        var exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
        var ver = asm.GetName().Version?.ToString() ?? "0.1.0.0";
        var buildDate = !string.IsNullOrEmpty(exePath)
            ? System.IO.File.GetLastWriteTime(exePath).ToString("yyyy.MM.dd HH:mm:ss")
            : "—";
        if (!string.IsNullOrEmpty(exePath))
        {
            var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
            ver = fvi.FileVersion ?? ver;
        }

        lvwVersions.Items.Add(new ListViewItem(new[] { "SpeedScanManager.exe", ver, buildDate }));
        foreach (var dll in new[] { "NTwain.dll", "Tesseract.dll", "PdfSharpCore.dll", "DocumentFormat.OpenXml.dll" })
            lvwVersions.Items.Add(new ListViewItem(GetFileVersionRow(dll)));

        var cardFiles = new SettingsCard(L.T("Dateiversion")) { ShowDividers = false };
        cardFiles.Add(lvwVersions);
        // Space below the list inside the card.
        cardFiles.Add(new Panel { Height = 12 });

        var stack = new ModernStack { Dock = DockStyle.Top, Padding = new Padding(20, 18, 20, 20) };
        stack.Controls.Add(cardScanner);
        stack.Controls.Add(cardFiles);
        Body.Controls.Add(stack);

        // === Footer (OK; help is the header's "?" button) ===
        var btnOk = AddFooterButton("OK", ThemeRole.Primary, Glyph.Check, DialogResult.OK);
        AcceptButton = btnOk;

        ThemeManager.Apply(this);
        ResumeLayout(true);
    }

    /// <summary>Read-only, selectable value box (the values can be copied).</summary>
    private static TextBox InfoBox(string text) => new()
    {
        Text = text,
        ReadOnly = true,
        TabStop = false,
        AutoSize = false,
        Size = new Size(300, 28)
    };

    /// <summary>File name, file version and modification time of a DLL next to the exe.</summary>
    private static string[] GetFileVersionRow(string fileName)
    {
        try
        {
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, fileName);
            if (System.IO.File.Exists(path))
            {
                var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(path);
                return new[]
                {
                    fileName,
                    fvi.FileVersion ?? "—",
                    System.IO.File.GetLastWriteTime(path).ToString("yyyy.MM.dd HH:mm:ss")
                };
            }
        }
        catch
        {
            // fall through
        }
        return new[] { fileName, "—", "—" };
    }
}
