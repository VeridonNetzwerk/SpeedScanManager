using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace SpeedScanManager;

internal class VersionInfoDialog : Form
{
    public VersionInfoDialog()
    {
        Text = "SpeedScan Manager for fi Series \u2013 Versionsinformationen";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(460, 310);
        Font = new Font("Microsoft Sans Serif", 8.25f);

        var font = new Font("Microsoft Sans Serif", 8.25f);

        // === Logo line: "SpeedScan" (red bold) + "for fi Series" (dark gray) ===
        var lblLogo = new Label
        {
            Text = "SpeedScan",
            Font = new Font("Microsoft Sans Serif", 14f, FontStyle.Bold),
            ForeColor = Color.FromArgb(200, 40, 40),
            Location = new Point(24, 18),
            AutoSize = true
        };
        var lblForFi = new Label
        {
            Text = " for fi Series",
            Font = new Font("Microsoft Sans Serif", 14f, FontStyle.Bold),
            ForeColor = Color.FromArgb(60, 60, 60),
            Location = new Point(24 + lblLogo.PreferredWidth, 18),
            AutoSize = true
        };

        // === Version title line ===
        var asm = Assembly.GetExecutingAssembly();
        var exePath = Environment.ProcessPath ?? asm.Location ?? "";
        var version = !string.IsNullOrEmpty(exePath)
            ? (System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath).FileVersion ?? asm.GetName().Version?.ToString() ?? "1.0.0.0")
            : (asm.GetName().Version?.ToString() ?? "1.0.0.0");

        var lblVersionTitle = new Label
        {
            Text = $"SpeedScan Manager for fi Series Version {version}",
            Font = new Font("Microsoft Sans Serif", 9f, FontStyle.Bold),
            Location = new Point(24, 58),
            AutoSize = true
        };

        // === Info text block ===
        var lblLicense = new Label
        {
            Text = "SpeedScan Manager for fi Series\n\n" +
                   "Open-Source Scanning-Verwaltungssoftware für Fujitsu fi-Series Scanner.\n\n" +
                   "TWAIN-Unterstützung durch NTwain.\n" +
                   "OCR-Engine: Tesseract.NET.\n" +
                   "PDF-Verarbeitung: PdfPig.\n\n" +
                   "Copyright © 2024-2026 SpeedScan Manager Projekt",
            Font = font,
            Location = new Point(24, 90),
            Size = new Size(400, 180),
            AutoSize = false,
            BackColor = Color.White
        };

        // === Detail button ===
        var btnDetail = new Button
        {
            Text = "Detail...",
            Location = new Point(370, 130),
            Size = new Size(65, 22),
            Font = font,
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true
        };
        btnDetail.Click += (s, e) =>
        {
            using var dlg = new ScannerDriverInfoDialog();
            dlg.ShowDialog(this);
        };

        // === Footer (OK + Hilfe) ===
        var footerTable = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 36,
            ColumnCount = 5,
            ColumnStyles =
            {
                new ColumnStyle(SizeType.Percent, 100f),
                new ColumnStyle(SizeType.Absolute, 75f),
                new ColumnStyle(SizeType.Absolute, 12f),
                new ColumnStyle(SizeType.Absolute, 75f),
                new ColumnStyle(SizeType.Percent, 100f)
            },
            RowCount = 1,
            RowStyles = { new RowStyle(SizeType.Percent, 100f) },
            BackColor = Color.FromArgb(240, 240, 240)
        };

        var btnOk = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Size = new Size(75, 24),
            Font = font,
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            Anchor = AnchorStyles.None
        };

        var btnHelp = new Button
        {
            Text = "Hilfe",
            Size = new Size(75, 24),
            Font = font,
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            Anchor = AnchorStyles.None
        };
        btnHelp.Click += (s, e) =>
        {
            using var help = new HelpForm();
            help.Show(this);
        };

        footerTable.Controls.Add(new Panel(), 0, 0);
        footerTable.Controls.Add(btnOk, 1, 0);
        footerTable.Controls.Add(new Panel(), 2, 0);
        footerTable.Controls.Add(btnHelp, 3, 0);
        footerTable.Controls.Add(new Panel(), 4, 0);

        Controls.Add(footerTable);
        Controls.Add(lblLicense);
        Controls.Add(btnDetail);
        Controls.Add(lblVersionTitle);
        Controls.Add(lblLogo);
        Controls.Add(lblForFi);

        AcceptButton = btnOk;
    }
}
