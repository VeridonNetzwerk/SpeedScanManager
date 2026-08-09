using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace SpeedScanManager;

internal class ScannerDriverInfoDialog : Form
{
    public ScannerDriverInfoDialog()
    {
        Text = "SpeedScan Manager for fi Series - Scanner- und Treiberinformationen";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(460, 280);
        Font = new Font("Microsoft Sans Serif", 8.25f);

        var font = new Font("Microsoft Sans Serif", 8.25f);

        // === Scanner name row ===
        var lblScannerName = new Label
        {
            Text = "Scannename:",
            Location = new Point(20, 24),
            AutoSize = true,
            Font = font
        };

        var txtScannerName = new TextBox
        {
            Text = "",
            ReadOnly = true,
            Enabled = false,
            Font = font,
            Location = new Point(108, 22),
            Size = new Size(246, 22),
            BackColor = Color.FromArgb(240, 240, 240)
        };

        // === File version section ===
        var lblDateiversion = new Label
        {
            Text = "Dateiversion:",
            Location = new Point(30, 78),
            Size = new Size(90, 16),
            AutoSize = false,
            Font = new Font("Microsoft Sans Serif", 8.25f, FontStyle.Bold)
        };

        var lvwVersions = new ListView
        {
            Location = new Point(28, 100),
            Size = new Size(404, 122),
            View = View.Details,
            FullRowSelect = false,
            HideSelection = true,
            GridLines = false,
            BorderStyle = BorderStyle.FixedSingle,
            Font = font
        };

        lvwVersions.Columns.Add("Dateiname", 160, HorizontalAlignment.Left);
        lvwVersions.Columns.Add("Version", 80, HorizontalAlignment.Center);
        lvwVersions.Columns.Add("Zeit", 146, HorizontalAlignment.Right);

        var asm = Assembly.GetExecutingAssembly();
        var exePath = Environment.ProcessPath ?? asm.Location ?? "";
        var ver = asm.GetName().Version?.ToString() ?? "1.0.0.0";
        var buildDate = !string.IsNullOrEmpty(exePath)
            ? System.IO.File.GetLastWriteTime(exePath).ToString("yyyy.MM.dd HH:mm:ss")
            : "—";
        if (!string.IsNullOrEmpty(exePath))
        {
            var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
            ver = fvi.FileVersion ?? ver;
        }

        lvwVersions.Items.Add(new ListViewItem(new[] { "SpeedScanManager.exe", ver, buildDate }));
        lvwVersions.Items.Add(new ListViewItem(new[] { "NTwain.dll", "4.0.0.0", "—" }));
        lvwVersions.Items.Add(new ListViewItem(new[] { "Tesseract.dll", "5.0.0.0", "—" }));
        lvwVersions.Items.Add(new ListViewItem(new[] { "PdfPig.dll", "0.1.9.0", "—" }));

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
        Controls.Add(lvwVersions);
        Controls.Add(lblDateiversion);
        Controls.Add(txtScannerName);
        Controls.Add(lblScannerName);

        AcceptButton = btnOk;
    }
}
