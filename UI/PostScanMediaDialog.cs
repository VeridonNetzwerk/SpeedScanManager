using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Post-scan media selection dialog shown when Quick-Menü is enabled.
/// Offers 8 actions: Scan to Folder, E-mail, Print, Word, Excel, PowerPoint, Picture Folder, PDF Edit.
/// Styled after the Fujitsu ScanSnap original dialog design.
/// </summary>
internal class PostScanMediaDialog : Form
{
    public enum MediaAction
    {
        ScanToFolder,
        ScanToEmail,
        ScanToPrint,
        ScanToWord,
        ScanToExcel,
        ScanToPowerPoint,
        ScanPictureFolder,
        EditWithPdf
    }

    public MediaAction SelectedMediaAction { get; private set; } = MediaAction.ScanToFolder;

    private readonly List<Panel> _itemPanels = new();
    private int _selectedIndex = 0;
    private static readonly Color SelectedBg = Color.FromArgb(160, 200, 245);
    private static readonly Color SelectedBorder = Color.FromArgb(80, 130, 200);
    private static readonly Color NormalBg = Color.White;
    private static readonly Color NormalBorder = Color.FromArgb(180, 180, 180);
    private static readonly Color HeaderBg = Color.FromArgb(120, 150, 200);

    private readonly string[] _labels =
    {
        "Scan to Folder",
        "Scan to E-mail",
        "Scan to Print",
        "Scan to Word",
        "Scan to Excel",
        "Scan to PowerPoint(R)",
        "Scan Picture Folder",
        "Edit with PDF Edit"
    };

    private readonly Bitmap[] _icons;

    public PostScanMediaDialog(List<string> filePaths, List<Bitmap>? images)
    {
        _icons = new Bitmap[]
        {
            PostScanIcons.CreateFolderIcon(),
            PostScanIcons.CreateEmailIcon(),
            PostScanIcons.CreatePrintIcon(),
            PostScanIcons.CreateWordIcon(),
            PostScanIcons.CreateExcelIcon(),
            PostScanIcons.CreatePowerPointIcon(),
            PostScanIcons.CreatePictureFolderIcon(),
            PostScanIcons.CreatePdfEditIcon()
        };

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(580, 470);
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;
        Font = new Font("Microsoft Sans Serif", 8.25f);
        BackColor = Color.FromArgb(240, 240, 240);

        // === Custom header bar ===
        var headerBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = HeaderBg
        };
        var lblLogo = new Label
        {
            Text = "Scan",
            Font = new Font("Microsoft Sans Serif", 14f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(12, 6),
            AutoSize = true,
            BackColor = HeaderBg
        };
        var lblForFi = new Label
        {
            Text = " for fi Series",
            Font = new Font("Microsoft Sans Serif", 10f),
            ForeColor = Color.FromArgb(220, 230, 245),
            Location = new Point(68, 10),
            AutoSize = true,
            BackColor = HeaderBg
        };
        var btnHelp = new Button
        {
            Text = "?",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft Sans Serif", 10f),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(80, 120, 180),
            Size = new Size(32, 24),
            Location = new Point(ClientSize.Width - 72, 6),
            FlatAppearance = { BorderSize = 0 }
        };
        var btnClose = new Button
        {
            Text = "\u2715",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft Sans Serif", 12f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(180, 60, 60),
            Size = new Size(32, 24),
            Location = new Point(ClientSize.Width - 36, 6),
            FlatAppearance = { BorderSize = 0 }
        };
        btnClose.Click += (_, _) => Close();
        headerBar.Controls.AddRange(new Control[] { lblLogo, lblForFi, btnHelp, btnClose });

        // === Content area with grid ===
        var contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(240, 240, 240),
            Padding = new Padding(0)
        };

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 2,
            Padding = new Padding(12, 12, 12, 12),
            BackColor = Color.FromArgb(240, 240, 240),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None
        };
        for (int i = 0; i < 4; i++)
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

        for (int i = 0; i < 8; i++)
        {
            var panel = CreateItemPanel(i);
            _itemPanels.Add(panel);
            grid.Controls.Add(panel, i % 4, i / 4);
        }

        contentPanel.Controls.Add(grid);

        // === Footer ===
        var footerPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            BackColor = Color.FromArgb(245, 245, 245)
        };
        footerPanel.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(200, 200, 200), 1f);
            e.Graphics.DrawLine(pen, 0, 0, footerPanel.Width, 0);
        };

        var btnSave = new Button
        {
            Text = "Speichern",
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            Size = new Size(90, 26),
            Font = new Font("Microsoft Sans Serif", 8.25f),
            Location = new Point(ClientSize.Width - 190, 7)
        };
        btnSave.Click += (_, _) =>
        {
            SelectedMediaAction = (MediaAction)_selectedIndex;
            DialogResult = DialogResult.OK;
            Close();
        };

        var btnCancel = new Button
        {
            Text = "Abbrechen",
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            Size = new Size(90, 26),
            Font = new Font("Microsoft Sans Serif", 8.25f),
            Location = new Point(ClientSize.Width - 95, 7),
            DialogResult = DialogResult.Cancel
        };

        footerPanel.Controls.AddRange(new Control[] { btnCancel, btnSave });

        // Add controls in correct z-order (bottom and top dock first, then fill)
        Controls.AddRange(new Control[] { footerPanel, headerBar, contentPanel });

        AcceptButton = btnSave;
        CancelButton = btnCancel;

        UpdateSelection();
    }

    private Panel CreateItemPanel(int index)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = NormalBg,
            Margin = new Padding(3),
            Padding = new Padding(2),
            Tag = index
        };

        var imgBox = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(48, 48),
            Image = _icons[index],
            Dock = DockStyle.Top,
            Margin = Padding.Empty,
            BackColor = NormalBg
        };

        var lblText = new Label
        {
            Text = _labels[index],
            Font = new Font("Microsoft Sans Serif", 8.25f),
            ForeColor = Color.FromArgb(50, 50, 50),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            BackColor = NormalBg
        };

        panel.Controls.Add(lblText);
        panel.Controls.Add(imgBox);

        // Forward clicks from child controls to panel
        foreach (Control c in panel.Controls)
        {
            c.Click += (_, _) =>
            {
                _selectedIndex = index;
                UpdateSelection();
            };
        }
        panel.Click += (_, _) =>
        {
            _selectedIndex = index;
            UpdateSelection();
        };

        // Double-click to select and confirm
        panel.DoubleClick += (_, _) =>
        {
            _selectedIndex = index;
            SelectedMediaAction = (MediaAction)index;
            DialogResult = DialogResult.OK;
            Close();
        };
        foreach (Control c in panel.Controls)
        {
            c.DoubleClick += (_, _) =>
            {
                _selectedIndex = index;
                SelectedMediaAction = (MediaAction)index;
                DialogResult = DialogResult.OK;
                Close();
            };
        }

        return panel;
    }

    private void UpdateSelection()
    {
        for (int i = 0; i < _itemPanels.Count; i++)
        {
            var panel = _itemPanels[i];
            if (i == _selectedIndex)
            {
                panel.BackColor = SelectedBg;
                panel.BorderStyle = BorderStyle.FixedSingle;
            }
            else
            {
                panel.BackColor = NormalBg;
                panel.BorderStyle = BorderStyle.FixedSingle;
            }
            // Update child backcolors too
            foreach (Control c in panel.Controls)
                c.BackColor = panel.BackColor;
            panel.Invalidate();
        }
    }

}
