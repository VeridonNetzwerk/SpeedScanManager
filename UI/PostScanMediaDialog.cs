using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Post-scan media selection dialog shown when Quick-Menü is enabled.
/// Offers 8 actions: Scan to Folder, E-mail, Print, Word, Excel, PowerPoint, Picture Folder, PDF Edit.
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

    private readonly List<Button> _actionButtons = new();
    private int _selectedIndex = 0;
    private static readonly Color SelectedBg = Color.FromArgb(160, 200, 245);
    private static readonly Color SelectedBorder = Color.FromArgb(80, 130, 200);
    private static readonly Color NormalBg = Color.White;

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

    public PostScanMediaDialog(List<string> filePaths, List<Bitmap>? images)
    {
        Text = "Gescanntes Dokument bearbeiten";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(560, 320);
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;
        Font = new Font("Microsoft Sans Serif", 8.25f);
        BackColor = Color.FromArgb(240, 240, 240);

        // Navigation bar
        var navPanel = new Panel
        {
            Height = 32,
            Dock = DockStyle.Top,
            BackColor = Color.White
        };
        var lblNavLeft = new Label
        {
            Text = "\u25C0",
            Location = new Point(8, 6),
            AutoSize = true,
            Font = new Font("Segoe UI", 10F),
            ForeColor = Color.FromArgb(120, 120, 120)
        };
        var lblPageInfo = new Label
        {
            Text = $"Page 1/{Math.Max(1, filePaths.Count)}",
            Location = new Point(32, 8),
            AutoSize = true,
            Font = new Font("Microsoft Sans Serif", 8.25f),
            ForeColor = Color.FromArgb(80, 80, 80)
        };
        var lblNavRight = new Label
        {
            Text = "\u25B6",
            Location = new Point(32 + lblPageInfo.Width + 8, 6),
            AutoSize = true,
            Font = new Font("Segoe UI", 10F),
            ForeColor = Color.FromArgb(120, 120, 120)
        };
        navPanel.Controls.AddRange(new Control[] { lblNavLeft, lblPageInfo, lblNavRight });

        // Icon grid
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 220,
            ColumnCount = 4,
            RowCount = 2,
            Location = new Point(0, 32),
            Padding = new Padding(8, 8, 8, 8),
            BackColor = Color.FromArgb(240, 240, 240)
        };
        for (int i = 0; i < 4; i++)
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

        for (int i = 0; i < 8; i++)
        {
            var btn = CreateActionButton(i);
            _actionButtons.Add(btn);
            grid.Controls.Add(btn, i % 4, i / 4);
        }

        // Bottom buttons
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            BackColor = Color.FromArgb(240, 240, 240)
        };

        var btnOk = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Size = new Size(80, 28),
            Location = new Point(380, 6),
            UseVisualStyleBackColor = true
        };
        btnOk.Click += (_, _) =>
        {
            SelectedMediaAction = (MediaAction)_selectedIndex;
        };

        var btnCancel = new Button
        {
            Text = "Abbrechen",
            DialogResult = DialogResult.Cancel,
            Size = new Size(80, 28),
            Location = new Point(470, 6),
            UseVisualStyleBackColor = true
        };

        bottomPanel.Controls.AddRange(new Control[] { btnOk, btnCancel });

        Controls.AddRange(new Control[] { bottomPanel, grid, navPanel });

        AcceptButton = btnOk;
        CancelButton = btnCancel;

        UpdateSelection();
    }

    private Button CreateActionButton(int index)
    {
        var btn = new Button
        {
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = false,
            Font = new Font("Microsoft Sans Serif", 8.25f),
            Size = new Size(120, 90),
            Margin = new Padding(4),
            BackColor = NormalBg,
            Tag = index,
            Text = ""
        };

        btn.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
        btn.FlatAppearance.BorderSize = 1;

        // Custom paint for icon + text
        btn.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = btn.ClientRectangle;

            // Draw icon (centered, top portion)
            int iconSize = 40;
            int iconX = (rect.Width - iconSize) / 2;
            int iconY = 8;
            DrawActionIcon(g, index, iconX, iconY, iconSize);

            // Draw text (bottom portion)
            var textRect = new Rectangle(2, iconY + iconSize + 4, rect.Width - 4, rect.Height - iconY - iconSize - 6);
            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString(_labels[index], btn.Font, Brushes.Black, textRect, sf);
        };

        btn.Click += (_, _) =>
        {
            _selectedIndex = index;
            UpdateSelection();
        };

        return btn;
    }

    private void UpdateSelection()
    {
        for (int i = 0; i < _actionButtons.Count; i++)
        {
            var btn = _actionButtons[i];
            if (i == _selectedIndex)
            {
                btn.BackColor = SelectedBg;
                btn.FlatAppearance.BorderColor = SelectedBorder;
                btn.FlatAppearance.BorderSize = 2;
            }
            else
            {
                btn.BackColor = NormalBg;
                btn.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
                btn.FlatAppearance.BorderSize = 1;
            }
            btn.Invalidate();
        }
    }

    private static void DrawActionIcon(Graphics g, int actionIndex, int x, int y, int size)
    {
        var rect = new Rectangle(x, y, size, size);

        switch ((MediaAction)actionIndex)
        {
            case MediaAction.ScanToFolder:
                DrawFolderIcon(g, rect, Color.FromArgb(220, 180, 40), Color.FromArgb(180, 140, 20));
                break;

            case MediaAction.ScanToEmail:
                DrawEmailIcon(g, rect, Color.FromArgb(180, 120, 60), Color.FromArgb(100, 140, 200));
                break;

            case MediaAction.ScanToPrint:
                DrawPrinterIcon(g, rect, Color.FromArgb(100, 100, 100), Color.FromArgb(80, 160, 80));
                break;

            case MediaAction.ScanToWord:
                DrawWordIcon(g, rect);
                break;

            case MediaAction.ScanToExcel:
                DrawExcelIcon(g, rect);
                break;

            case MediaAction.ScanToPowerPoint:
                DrawPowerPointIcon(g, rect);
                break;

            case MediaAction.ScanPictureFolder:
                DrawPictureFolderIcon(g, rect);
                break;

            case MediaAction.EditWithPdf:
                DrawPdfEditIcon(g, rect);
                break;
        }
    }

    private static void DrawFolderIcon(Graphics g, Rectangle rect, Color fill, Color edge)
    {
        using var brush = new SolidBrush(fill);
        using var pen = new Pen(edge, 1.5f);
        var tabRect = new Rectangle(rect.X, rect.Y, rect.Width / 2, 8);
        g.FillRectangle(brush, tabRect);
        g.DrawRectangle(pen, tabRect);
        var bodyRect = new Rectangle(rect.X, rect.Y + 7, rect.Width, rect.Height - 7);
        g.FillRectangle(brush, bodyRect);
        g.DrawRectangle(pen, bodyRect);
    }

    private static void DrawEmailIcon(Graphics g, Rectangle rect, Color envelope, Color flap)
    {
        using var brush = new SolidBrush(envelope);
        using var pen = new Pen(Color.FromArgb(80, 80, 80), 1.5f);
        g.FillRectangle(brush, rect);
        g.DrawRectangle(pen, rect);
        using var flapPen = new Pen(flap, 2f);
        g.DrawLine(flapPen, rect.X, rect.Y, rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        g.DrawLine(flapPen, rect.X + rect.Width, rect.Y, rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
    }

    private static void DrawPrinterIcon(Graphics g, Rectangle rect, Color body, Color paper)
    {
        using var bodyBrush = new SolidBrush(body);
        using var paperBrush = new SolidBrush(paper);
        using var pen = new Pen(Color.FromArgb(60, 60, 60), 1.5f);
        // Paper input (top)
        var topRect = new Rectangle(rect.X + 6, rect.Y, rect.Width - 12, 10);
        g.FillRectangle(paperBrush, topRect);
        g.DrawRectangle(pen, topRect);
        // Printer body (middle)
        var midRect = new Rectangle(rect.X, rect.Y + 10, rect.Width, 16);
        g.FillRectangle(bodyBrush, midRect);
        g.DrawRectangle(pen, midRect);
        // Paper output (bottom)
        var botRect = new Rectangle(rect.X + 6, rect.Y + 26, rect.Width - 12, rect.Height - 26);
        g.FillRectangle(paperBrush, botRect);
        g.DrawRectangle(pen, botRect);
    }

    private static void DrawWordIcon(Graphics g, Rectangle rect)
    {
        using var brush = new SolidBrush(Color.FromArgb(40, 80, 180));
        using var pen = new Pen(Color.FromArgb(20, 50, 130), 1.5f);
        g.FillRectangle(brush, rect);
        g.DrawRectangle(pen, rect);
        using var font = new Font("Arial", 16F, FontStyle.Bold);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("W", font, Brushes.White, rect, sf);
    }

    private static void DrawExcelIcon(Graphics g, Rectangle rect)
    {
        using var brush = new SolidBrush(Color.FromArgb(40, 140, 60));
        using var pen = new Pen(Color.FromArgb(20, 100, 40), 1.5f);
        g.FillRectangle(brush, rect);
        g.DrawRectangle(pen, rect);
        using var font = new Font("Arial", 16F, FontStyle.Bold);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("X", font, Brushes.White, rect, sf);
    }

    private static void DrawPowerPointIcon(Graphics g, Rectangle rect)
    {
        using var brush = new SolidBrush(Color.FromArgb(200, 70, 40));
        using var pen = new Pen(Color.FromArgb(160, 50, 20), 1.5f);
        g.FillRectangle(brush, rect);
        g.DrawRectangle(pen, rect);
        using var font = new Font("Arial", 14F, FontStyle.Bold);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("P", font, Brushes.White, rect, sf);
    }

    private static void DrawPictureFolderIcon(Graphics g, Rectangle rect)
    {
        DrawFolderIcon(g, rect, Color.FromArgb(80, 160, 200), Color.FromArgb(40, 100, 160));
        // Draw small image icon on folder
        using var pen = new Pen(Color.White, 1.5f);
        var imgRect = new Rectangle(rect.X + 8, rect.Y + 14, rect.Width - 16, rect.Height - 20);
        g.DrawRectangle(pen, imgRect);
        // Mountain triangle
        var p1 = new PointF(imgRect.X + 4, imgRect.Bottom - 4);
        var p2 = new PointF(imgRect.X + imgRect.Width / 3, imgRect.Y + imgRect.Height / 2);
        var p3 = new PointF(imgRect.X + imgRect.Width * 2 / 3, imgRect.Bottom - 4);
        var p4 = new PointF(imgRect.Right - 4, imgRect.Y + imgRect.Height / 3);
        g.DrawLines(pen, new[] { p1, p2, p3, p4 });
    }

    private static void DrawPdfEditIcon(Graphics g, Rectangle rect)
    {
        using var brush = new SolidBrush(Color.FromArgb(200, 50, 50));
        using var pen = new Pen(Color.FromArgb(140, 30, 30), 1.5f);
        g.FillRectangle(brush, rect);
        g.DrawRectangle(pen, rect);
        using var font = new Font("Arial", 11F, FontStyle.Bold);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("PDF", font, Brushes.White, rect, sf);
        // Magnifying glass overlay
        var glassRect = new Rectangle(rect.Right - 14, rect.Bottom - 14, 12, 12);
        g.DrawEllipse(new Pen(Color.White, 2f), glassRect);
        g.DrawLine(new Pen(Color.White, 2f), glassRect.Right - 1, glassRect.Bottom - 1, glassRect.Right + 4, glassRect.Bottom + 4);
    }
}
