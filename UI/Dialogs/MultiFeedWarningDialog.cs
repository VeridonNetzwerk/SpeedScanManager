using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Dialog shown when multi-feed (double feed) is detected during scanning.
/// Shows preview images of the current page and the previous page (if available),
/// and offers three options: rescan, keep as-is, or disable multi-feed detection.
/// </summary>
internal class MultiFeedWarningDialog : Form
{
    /// <summary>
    /// The user's choice from the dialog.
    /// </summary>
    public enum MultiFeedAction
    {
        Rescan,
        KeepAsIs,
        DisableDetection
    }

    public MultiFeedAction Action { get; private set; } = MultiFeedAction.Rescan;

    public MultiFeedWarningDialog(Bitmap? currentPage, Bitmap? previousPage)
    {
        Text = "SpeedScan Manager – Mehrfacheinzug erkannt";
        Icon = TrayIcons.GetAppIcon();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        Width = 720;
        Height = 480;

        var lblHeader = new Label
        {
            Text = "Mehrfacheinzug erkannt! Es wurden möglicherweise zwei Seiten gleichzeitig eingezogen.",
            Location = new Point(12, 12),
            Size = new Size(696, 36),
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.Firebrick
        };

        // Preview panels
        var lblPrev = new Label
        {
            Text = "Vorherige Seite:",
            Location = new Point(12, 55),
            Size = new Size(300, 20),
            Font = new Font("Segoe UI", 9F)
        };

        var lblCurr = new Label
        {
            Text = "Aktuelle Seite (Mehrfacheinzug):",
            Location = new Point(370, 55),
            Size = new Size(300, 20),
            Font = new Font("Segoe UI", 9F)
        };

        var prevPanel = CreatePreviewPanel(currentPage, 12, 78, 300, 260);
        var currPanel = CreatePreviewPanel(previousPage, 370, 78, 300, 260);

        // Buttons
        var btnRescan = new Button
        {
            Text = "Wiederholen",
            Location = new Point(12, 360),
            Size = new Size(160, 36),
            Font = new Font("Segoe UI", 9F)
        };
        btnRescan.Click += (_, _) =>
        {
            Action = MultiFeedAction.Rescan;
            DialogResult = DialogResult.OK;
            Close();
        };

        var btnKeep = new Button
        {
            Text = "So übernehmen",
            Location = new Point(190, 360),
            Size = new Size(160, 36),
            Font = new Font("Segoe UI", 9F)
        };
        btnKeep.Click += (_, _) =>
        {
            Action = MultiFeedAction.KeepAsIs;
            DialogResult = DialogResult.OK;
            Close();
        };

        var btnDisable = new Button
        {
            Text = "Erkennung ausschalten",
            Location = new Point(370, 360),
            Size = new Size(200, 36),
            Font = new Font("Segoe UI", 9F)
        };
        btnDisable.Click += (_, _) =>
        {
            Action = MultiFeedAction.DisableDetection;
            DialogResult = DialogResult.OK;
            Close();
        };

        var lblHint = new Label
        {
            Text = "Tipp: \"Erkennung ausschalten\" deaktiviert die Mehrfacheinzugserkennung\r\nfür den restlichen Scan und übernimmt die aktuelle Seite.",
            Location = new Point(12, 405),
            Size = new Size(696, 36),
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 8F)
        };

        Controls.AddRange(new Control[]
        {
            lblHeader, lblPrev, lblCurr, prevPanel, currPanel,
            btnRescan, btnKeep, btnDisable, lblHint
        });

        AcceptButton = btnRescan;
    }

    private static Panel CreatePreviewPanel(Bitmap? image, int x, int y, int width, int height)
    {
        var panel = new Panel
        {
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(x, y),
            Size = new Size(width, height),
            BackColor = Color.FromArgb(240, 240, 240)
        };

        if (image != null)
        {
            var pb = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.Zoom,
                Dock = DockStyle.Fill,
                Image = image
            };
            panel.Controls.Add(pb);
        }
        else
        {
            var lbl = new Label
            {
                Text = "Kein Vorschaubild verfügbar",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Gray
            };
            panel.Controls.Add(lbl);
        }

        return panel;
    }
}
