using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Post-scan media selection dialog shown when Quick-Menü is enabled.
/// Offers 8 actions: Scan to Folder, E-mail, Print, Word, Excel, PowerPoint, Picture Folder, PDF Edit.
/// Styled to match the rest of SpeedScan Manager (blue gradient header, rounded window, accent-blue selection).
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
    private static readonly Color AccentBlue = Color.FromArgb(45, 90, 170);
    private static readonly Color HeaderTop = Color.FromArgb(70, 110, 175);
    private static readonly Color HeaderBottom = Color.FromArgb(40, 75, 140);
    private static readonly Color SelectedBg = Color.FromArgb(220, 230, 245);
    private static readonly Color SelectedBorder = AccentBlue;
    private static readonly Color NormalBg = Color.White;
    private static readonly Color NormalBorder = Color.FromArgb(210, 210, 210);
    private static readonly Color PanelGray = Color.FromArgb(240, 240, 240);

    private readonly string[] _labels =
    {
        "In Ordner speichern",
        "Per E-Mail versenden",
        "Drucken",
        "Nach Word",
        "Nach Excel",
        "Nach PowerPoint",
        "In Bilderordner",
        "Mit PDF Edit bearbeiten"
    };

    private readonly Bitmap[] _icons;
    private Point? _dragStart;

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
        ClientSize = new Size(520, 410);
        Icon = TrayIcons.GetAppIcon();
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;
        Font = new Font("Microsoft Sans Serif", 8.25f);
        BackColor = PanelGray;

        // Rounded window outline + thin border
        Region = new Region(RoundedRect(new Rectangle(0, 0, ClientSize.Width, ClientSize.Height), 8));
        Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(140, 150, 165), 1f);
            var rect = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            using var path = RoundedRect(rect, 8);
            e.Graphics.DrawPath(pen, path);
        };

        // === Custom gradient header bar ===
        var headerBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = HeaderTop
        };
        headerBar.Paint += (_, e) =>
        {
            var rect = new Rectangle(0, 0, headerBar.Width, headerBar.Height);
            using var brush = new LinearGradientBrush(rect, HeaderTop, HeaderBottom, LinearGradientMode.Vertical);
            e.Graphics.FillRectangle(brush, rect);
        };
        headerBar.MouseDown += (s, e) => _dragStart = e.Location;
        headerBar.MouseMove += (s, e) =>
        {
            if (_dragStart.HasValue && e.Button == MouseButtons.Left)
            {
                Location = new Point(Location.X + (e.X - _dragStart.Value.X), Location.Y + (e.Y - _dragStart.Value.Y));
            }
        };
        headerBar.MouseUp += (s, e) => _dragStart = null;

        var pbLogo = new PictureBox
        {
            Image = AppResources.Logo,
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(24, 24),
            Location = new Point(12, 10),
            BackColor = Color.Transparent
        };

        var lblTitle = new Label
        {
            Text = "Wohin möchtest du scannen?",
            Font = new Font("Microsoft Sans Serif", 10f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(44, 6),
            AutoSize = true,
            BackColor = Color.Transparent
        };

        int fileCount = filePaths?.Count ?? images?.Count ?? 0;
        var lblSubtitle = new Label
        {
            Text = fileCount == 1 ? "1 Seite gescannt" : $"{fileCount} Seiten gescannt",
            Font = new Font("Microsoft Sans Serif", 8.25f),
            ForeColor = Color.FromArgb(215, 225, 245),
            Location = new Point(45, 24),
            AutoSize = true,
            BackColor = Color.Transparent
        };

        var btnHelp = new CircleButton("?", Color.FromArgb(80, 130, 195))
        {
            Size = new Size(22, 22),
            Location = new Point(ClientSize.Width - 58, 11)
        };
        btnHelp.Click += (s, e) => { using var h = new HelpForm("postscan-media"); h.ShowDialog(this); };

        var btnClose = new CircleButton("\u2715", Color.FromArgb(190, 70, 70))
        {
            Size = new Size(22, 22),
            Location = new Point(ClientSize.Width - 30, 11)
        };
        btnClose.Click += (_, _) => Close();

        headerBar.Controls.AddRange(new Control[] { pbLogo, lblTitle, lblSubtitle, btnHelp, btnClose });

        // === Content area with grid ===
        var contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = PanelGray,
            Padding = new Padding(0)
        };

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 2,
            Padding = new Padding(16, 20, 16, 20),
            BackColor = PanelGray,
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
            BackColor = PanelGray
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
            Size = new Size(116, 26),
            Font = new Font("Microsoft Sans Serif", 8.25f),
            Image = TabIcons.CreateCheckIcon(),
            ImageAlign = ContentAlignment.MiddleLeft,
            TextAlign = ContentAlignment.MiddleRight,
            Location = new Point(ClientSize.Width - 244, 7)
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
            Size = new Size(116, 26),
            Font = new Font("Microsoft Sans Serif", 8.25f),
            Image = TabIcons.CreateCrossIcon(),
            ImageAlign = ContentAlignment.MiddleLeft,
            TextAlign = ContentAlignment.MiddleRight,
            Location = new Point(ClientSize.Width - 122, 7),
            DialogResult = DialogResult.Cancel
        };

        footerPanel.Controls.AddRange(new Control[] { btnCancel, btnSave });

        // Add Fill control first so Top/Bottom docked controls correctly reserve their space
        Controls.AddRange(new Control[] { contentPanel, footerPanel, headerBar });

        AcceptButton = btnSave;
        CancelButton = btnCancel;

        UpdateSelection();
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private Panel CreateItemPanel(int index)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = NormalBg,
            Margin = new Padding(4),
            Padding = new Padding(2),
            Tag = index
        };
        panel.Paint += (_, e) => PaintItemBorder(panel, index, e.Graphics);

        var imgBox = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(40, 40),
            Image = _icons[index],
            Dock = DockStyle.Top,
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };

        var lblText = new Label
        {
            Text = _labels[index],
            Font = new Font("Microsoft Sans Serif", 8.25f),
            ForeColor = Color.FromArgb(50, 50, 50),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            BackColor = Color.Transparent
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

    private void PaintItemBorder(Panel panel, int index, Graphics g)
    {
        bool selected = index == _selectedIndex;
        var rect = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
        using var path = RoundedRect(rect, 6);
        using var pen = new Pen(selected ? SelectedBorder : NormalBorder, selected ? 2f : 1f);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.DrawPath(pen, path);
    }

    private void UpdateSelection()
    {
        for (int i = 0; i < _itemPanels.Count; i++)
        {
            var panel = _itemPanels[i];
            panel.BackColor = i == _selectedIndex ? SelectedBg : NormalBg;
            panel.Invalidate();
        }
    }

    /// <summary>
    /// Small circular icon button used for Help/Close in the header bar.
    /// </summary>
    private class CircleButton : Button
    {
        private readonly Color _bg;

        public CircleButton(string text, Color bg)
        {
            _bg = bg;
            Text = text;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            ForeColor = Color.White;
            Font = new Font("Microsoft Sans Serif", 9f, FontStyle.Bold);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var brush = new SolidBrush(_bg);
            g.FillEllipse(brush, rect);
            TextRenderer.DrawText(g, Text, Font, rect, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        protected override void OnParentBackColorChanged(EventArgs e) { }
    }
}
