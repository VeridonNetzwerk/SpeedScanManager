using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Post-scan media selection dialog shown when Quick-Menü is enabled.
/// Offers 8 actions: Scan to Folder, E-mail, Print, Word, Excel, PowerPoint, Picture Folder, PDF Edit.
/// Reference design of the modern UI (<see cref="ModernWindow"/>): header zone, a grid of
/// action tiles (<see cref="TileButton"/>: hover, accent selection) and a footer bar.
/// </summary>
internal class PostScanMediaDialog : ModernWindow
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

    private readonly List<TileButton> _itemPanels = new();
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

    private readonly Bitmap[] _icons;

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

        SuspendLayout();
        // The owner is the invisible tray window: center on screen, stay on top and show in
        // the taskbar so the Quick Menu can't get lost behind other windows.
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        ShowInTaskbar = true;
        Text = "SpeedScan Manager – Quick-Menü";
        Title = "Wohin möchtest du scannen?";
        Subtitle = pageCount == 1 ? "1 Seite gescannt" : $"{pageCount} Seiten gescannt";
        HeaderButtons = ModernHeaderButtons.Help | ModernHeaderButtons.Close;
        HelpTopic = "postscan-media";
        ClientSize = new Size(520, 410);

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
        Body.Controls.Add(grid);

        // === Footer ===
        FooterHint = "Doppelklick startet direkt";
        var btnCancel = AddFooterButton("Abbrechen", ThemeRole.Secondary, Glyph.Cross, DialogResult.Cancel);
        var btnSave = AddFooterButton("Speichern", ThemeRole.Primary, Glyph.Check);
        btnSave.Click += (_, _) =>
        {
            SelectedMediaAction = (MediaAction)_selectedIndex;
            DialogResult = DialogResult.OK;
            Close();
        };

        AcceptButton = btnSave;
        CancelButton = btnCancel;

        UpdateSelection();
        ThemeManager.Apply(this);
        ResumeLayout(true);
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
        }
        base.Dispose(disposing);
    }

    private TileButton CreateItemPanel(int index)
    {
        var tile = new TileButton(_labels[index], _icons[index])
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
}
