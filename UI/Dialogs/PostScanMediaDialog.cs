using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Post-scan media selection dialog shown when Quick-Menü is enabled.
/// Offers 8 actions: Scan to Folder, E-mail, Print, Word, Excel, PowerPoint, Picture Folder, PDF Edit.
/// Themed (light/dark, Discord style): borderless rounded window with a header zone, a grid of
/// action tiles (hover, accent selection) and a footer bar with the buttons.
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

    private readonly List<MediaTile> _itemPanels = new();
    private int _selectedIndex = 0;

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

    private static readonly Font TitleFont = new("Segoe UI Semibold", 12f);

    private const int CornerRadius = 8;
    private readonly bool _useRegion = !ThemeNative.IsWindows11;
    private readonly Bitmap[] _icons;
    private readonly Panel _headerBar;
    private readonly string _subtitle;
    private Point? _dragStart;

    /// <param name="pageCount">Number of pages in the output (not the number of files).</param>
    public PostScanMediaDialog(int pageCount)
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
        _subtitle = pageCount == 1 ? "1 Seite gescannt" : $"{pageCount} Seiten gescannt";

        SuspendLayout();
        FormBorderStyle = FormBorderStyle.None;
        // The owner is the invisible tray window: center on screen, stay on top and show in
        // the taskbar so the Quick Menu can't get lost behind other windows.
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        Text = "SpeedScan Manager – Quick-Menü";
        ClientSize = new Size(520, 410);
        Icon = TrayIcons.GetAppIcon();
        ShowInTaskbar = true;
        AutoScaleMode = AutoScaleMode.None;
        Font = ThemeFonts.Regular;
        KeyPreview = true;

        // Windows 11 rounds the borderless window itself (DWM, anti-aliased, with border);
        // older versions get a clipping region and a painted outline.
        if (_useRegion)
        {
            using (var outline = RoundedRect(new Rectangle(0, 0, ClientSize.Width, ClientSize.Height), CornerRadius))
                Region = new Region(outline);
            Paint += (_, e) =>
            {
                var p = ThemeManager.Current;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(p.IsDark ? p.Tertiary : p.Border, 1f);
                using var path = RoundedRect(new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1), CornerRadius);
                e.Graphics.DrawPath(pen, path);
            };
        }

        // === Header: logo, title, page count, help and close ===
        _headerBar = new DoubleBufferedPanel { Dock = DockStyle.Top, Height = 60 };
        ThemeManager.SetRole(_headerBar, ThemeRole.Header);
        _headerBar.Paint += PaintHeader;
        _headerBar.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) _dragStart = e.Location; };
        _headerBar.MouseMove += (_, e) =>
        {
            if (_dragStart.HasValue && e.Button == MouseButtons.Left)
                Location = new Point(Location.X + (e.X - _dragStart.Value.X), Location.Y + (e.Y - _dragStart.Value.Y));
        };
        _headerBar.MouseUp += (_, _) => _dragStart = null;

        var btnHelp = new HeaderButton(HeaderButton.Kind.Help)
        {
            Size = new Size(30, 30),
            Location = new Point(ClientSize.Width - 76, 15),
            AccessibleName = "Hilfe"
        };
        btnHelp.Click += (_, _) => { using var h = new HelpForm("postscan-media"); h.ShowDialog(this); };

        var btnClose = new HeaderButton(HeaderButton.Kind.Close)
        {
            Size = new Size(30, 30),
            Location = new Point(ClientSize.Width - 42, 15),
            AccessibleName = "Schließen"
        };
        btnClose.Click += (_, _) => Close();
        _headerBar.Controls.AddRange(new Control[] { btnHelp, btnClose });

        // === Content: 4 x 2 grid of action tiles ===
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 2,
            Padding = new Padding(14, 14, 14, 14),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None
        };
        for (int i = 0; i < 4; i++)
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

        for (int i = 0; i < 8; i++)
        {
            var tile = CreateItemPanel(i);
            _itemPanels.Add(tile);
            grid.Controls.Add(tile, i % 4, i / 4);
        }

        // === Footer ===
        var footerPanel = new Panel { Dock = DockStyle.Bottom, Height = 56 };
        ThemeManager.SetRole(footerPanel, ThemeRole.Secondary);

        var btnSave = new Button
        {
            Text = "Speichern",
            Size = new Size(124, 32),
            Location = new Point(ClientSize.Width - 140, 12)
        };
        ThemeManager.SetRole(btnSave, ThemeRole.Primary);
        ThemeManager.SetGlyph(btnSave, Glyph.Check);
        btnSave.Click += (_, _) =>
        {
            SelectedMediaAction = (MediaAction)_selectedIndex;
            DialogResult = DialogResult.OK;
            Close();
        };

        var btnCancel = new Button
        {
            Text = "Abbrechen",
            Size = new Size(124, 32),
            Location = new Point(ClientSize.Width - 272, 12),
            DialogResult = DialogResult.Cancel
        };
        ThemeManager.SetRole(btnCancel, ThemeRole.Secondary);
        ThemeManager.SetGlyph(btnCancel, Glyph.Cross);

        var lblHint = new Label
        {
            Text = "Doppelklick startet direkt",
            AutoSize = true,
            Location = new Point(18, 20)
        };
        ThemeManager.SetRole(lblHint, ThemeRole.Muted);

        footerPanel.Controls.AddRange(new Control[] { lblHint, btnCancel, btnSave });

        // Add Fill control first so Top/Bottom docked controls correctly reserve their space
        Controls.AddRange(new Control[] { grid, footerPanel, _headerBar });

        AcceptButton = btnSave;
        CancelButton = btnCancel;

        UpdateSelection();
        ThemeManager.Apply(this);
        ResumeLayout(true);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int CS_DROPSHADOW = 0x00020000;
            var cp = base.CreateParams;
            cp.ClassStyle |= CS_DROPSHADOW;
            return cp;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        WindowActivation.BringToFront(this);
    }

    /// <summary>Arrow keys move the selection through the grid.</summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        int next = keyData switch
        {
            Keys.Left => _selectedIndex % 4 > 0 ? _selectedIndex - 1 : _selectedIndex,
            Keys.Right => _selectedIndex % 4 < 3 ? _selectedIndex + 1 : _selectedIndex,
            Keys.Up => _selectedIndex >= 4 ? _selectedIndex - 4 : _selectedIndex,
            Keys.Down => _selectedIndex < 4 ? _selectedIndex + 4 : _selectedIndex,
            _ => -1
        };
        if (next >= 0)
        {
            _selectedIndex = next;
            UpdateSelection();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var icon in _icons) icon.Dispose();
            Region?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void PaintHeader(object? sender, PaintEventArgs e)
    {
        var p = ThemeManager.Current;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // Logo in a rounded accent-tinted tile
        var tile = new RectangleF(16, 12, 36, 36);
        using (var path = ThemeGlyphs.Rounded(tile, 8))
        using (var brush = new SolidBrush(p.IsDark ? p.Background : p.Secondary))
            g.FillPath(brush, path);
        g.DrawImage(AppResources.Logo, new RectangleF(tile.X + 6, tile.Y + 6, 24, 24));

        TextRenderer.DrawText(g, "Wohin möchtest du scannen?", TitleFont, new Point(62, 10), p.Text,
            TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, _subtitle, ThemeFonts.Regular, new Point(63, 33), p.TextMuted,
            TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

        if (!p.IsDark)
        {
            using var pen = new Pen(p.Border);
            g.DrawLine(pen, 0, _headerBar.Height - 1, _headerBar.Width, _headerBar.Height - 1);
        }
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

    private MediaTile CreateItemPanel(int index)
    {
        var tile = new MediaTile(_icons[index], _labels[index])
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(5),
            Tag = index
        };
        tile.Click += (_, _) =>
        {
            _selectedIndex = index;
            UpdateSelection();
        };
        // Double-click to select and confirm
        tile.DoubleClick += (_, _) =>
        {
            _selectedIndex = index;
            SelectedMediaAction = (MediaAction)index;
            DialogResult = DialogResult.OK;
            Close();
        };
        return tile;
    }

    private void UpdateSelection()
    {
        for (int i = 0; i < _itemPanels.Count; i++)
            _itemPanels[i].Selected = i == _selectedIndex;
    }

    private sealed class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }

    /// <summary>Action tile: rounded surface with icon and label; hover and accent selection.</summary>
    private sealed class MediaTile : Control, IThemeAware
    {
        private readonly Image _icon;
        private bool _selected;
        private bool _hover;
        private bool _pressed;

        public MediaTile(Image icon, string text)
        {
            _icon = icon;
            Text = text;
            Font = ThemeFonts.Regular;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.StandardDoubleClick, true);
            SetStyle(ControlStyles.Selectable, false);
            AccessibleRole = AccessibleRole.PushButton;
            AccessibleName = text;
        }

        public bool Selected
        {
            get => _selected;
            set
            {
                if (_selected == value) return;
                _selected = value;
                Invalidate();
            }
        }

        public void ApplyTheme(ThemePalette palette) => Invalidate();

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = ThemeManager.Current;
            var g = e.Graphics;
            var parentBg = Parent?.BackColor ?? p.Background;
            g.Clear(parentBg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            Color fill = _selected
                ? ThemePalette.Blend(p.Secondary, p.Accent, p.IsDark ? 0.22f : 0.12f)
                : _pressed ? p.Selected : _hover ? p.Hover : p.Secondary;
            Color border = _selected ? p.Accent
                : _hover ? (p.IsDark ? p.ButtonSecondary : p.TextMuted)
                : p.IsDark ? p.Border : p.InputBorder;

            var rect = new RectangleF(1f, 1f, Width - 2f, Height - 2f);
            using (var path = ThemeGlyphs.Rounded(rect, 8))
            {
                using (var brush = new SolidBrush(fill))
                    g.FillPath(brush, path);
                using var pen = new Pen(border, _selected ? 2f : 1f);
                g.DrawPath(pen, path);
            }

            // Icon
            const int iconSize = 44;
            int iconY = Math.Max(10, (Height - iconSize - 40) / 2);
            var iconRect = new Rectangle((Width - iconSize) / 2, iconY, iconSize, iconSize);
            if (_pressed) iconRect.Offset(0, 1);
            g.DrawImage(_icon, iconRect);

            // Label (up to two lines)
            var textRect = new Rectangle(4, iconRect.Bottom + 8, Width - 8, Height - iconRect.Bottom - 12);
            var font = _selected ? ThemeFonts.Semibold : ThemeFonts.Regular;
            TextRenderer.DrawText(g, Text, font, textRect, _selected ? p.Text : p.TextSecondary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak
                | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

            // Check badge of the selected tile
            if (_selected)
            {
                var badge = new RectangleF(Width - 25f, 7f, 18f, 18f);
                using (var brush = new SolidBrush(p.Accent))
                    g.FillEllipse(brush, badge);
                using var pen = new Pen(p.TextOnAccent, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                g.DrawLines(pen, new[]
                {
                    new PointF(badge.X + 5f, badge.Y + 9.5f),
                    new PointF(badge.X + 8f, badge.Y + 12.5f),
                    new PointF(badge.X + 13f, badge.Y + 6.5f)
                });
            }
        }
    }

    /// <summary>Round icon button of the header (help "?", close "x").</summary>
    private sealed class HeaderButton : Control, IThemeAware
    {
        public enum Kind { Help, Close }

        private readonly Kind _kind;
        private bool _hover;
        private bool _pressed;

        public HeaderButton(Kind kind)
        {
            _kind = kind;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            AccessibleRole = AccessibleRole.PushButton;
        }

        public void ApplyTheme(ThemePalette palette) => Invalidate();

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = ThemeManager.Current;
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? p.Header);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Color fore = p.TextMuted;
            if (_hover || _pressed)
            {
                Color bg = _kind == Kind.Close
                    ? (_pressed ? p.DangerHover : p.Danger)
                    : (_pressed ? p.Selected : p.IsDark ? p.ButtonSecondary : p.Hover);
                using var brush = new SolidBrush(bg);
                g.FillEllipse(brush, 0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
                fore = _kind == Kind.Close ? p.TextOnAccent : p.Text;
            }

            float size = 16;
            var r = new RectangleF((Width - size) / 2f, (Height - size) / 2f, size, size);
            if (_kind == Kind.Close)
            {
                using var pen = new Pen(fore, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(pen, r.X + 4f, r.Y + 4f, r.Right - 4f, r.Bottom - 4f);
                g.DrawLine(pen, r.Right - 4f, r.Y + 4f, r.X + 4f, r.Bottom - 4f);
            }
            else
            {
                using var pen = new Pen(fore, 1.6f);
                g.DrawEllipse(pen, r.X + 0.5f, r.Y + 0.5f, r.Width - 1f, r.Height - 1f);
                TextRenderer.DrawText(g, "?", ThemeFonts.Semibold, Rectangle.Round(new RectangleF(r.X, r.Y - 0.5f, r.Width + 1, r.Height + 1)), fore,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            }
        }
    }
}
