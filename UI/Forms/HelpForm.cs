using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Classic Windows HTML Help viewer style form.
/// Two-panel layout: left navigation (Inhalt/Index/Suchen + TreeView),
/// right content area with WebBrowser rendering HTML help topics.
/// </summary>
internal class HelpForm : Form
{
    private readonly SplitContainer _split;
    private readonly ToolStrip _toolStrip;
    private readonly Panel _navTabStrip;
    private readonly TreeView _treeView;
    private readonly WebBrowser _webBrowser;
    private ListView? _indexListView;
    private Panel? _searchPanel;
    private TextBox? _searchBox;
    private ListBox? _searchResults;
    private bool _navVisible = true;

    private readonly HelpTopic _rootTopic;
    private readonly Dictionary<string, HelpTopic> _topicMap;
    private readonly List<string> _history = new();
    private int _historyIndex = -1;

    private static readonly Color NavTabActive = Color.FromArgb(220, 230, 245);
    private static readonly Color NavTabInactive = SystemColors.Control;
    private static readonly Font UiFont = new("Microsoft Sans Serif", 8.25f);

    private readonly string _initialTopic;

    public HelpForm(string? topicId = null)
    {
        _initialTopic = topicId ?? "root";
        Text = "SpeedScan Manager Hilfe";
        Icon = TrayIcons.GetAppIcon();
        ClientSize = new Size(780, 560);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = true;
        MaximizeBox = true;
        Font = UiFont;
        AutoScaleMode = AutoScaleMode.None;

        // Build help topic tree
        _rootTopic = HelpContent.BuildTree();
        _topicMap = HelpContent.BuildTopicMap(_rootTopic);

        // === ToolStrip (toolbar) ===
        _toolStrip = new ToolStrip
        {
            Dock = DockStyle.Top,
            Height = 38,
            RenderMode = ToolStripRenderMode.System,
            GripStyle = ToolStripGripStyle.Hidden
        };

        var btnHide = new ToolStripButton("Ausblenden")
        {
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            Image = CreateHideIcon(),
            ImageAlign = ContentAlignment.TopCenter,
            TextAlign = ContentAlignment.BottomCenter,
            TextImageRelation = TextImageRelation.ImageAboveText
        };
        btnHide.Click += (s, e) => ToggleNavigation();

        var tsBtnBack = new ToolStripButton("Zurück")
        {
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            Image = CreateBackIcon(),
            ImageAlign = ContentAlignment.TopCenter,
            TextAlign = ContentAlignment.BottomCenter,
            TextImageRelation = TextImageRelation.ImageAboveText,
            Enabled = false
        };
        tsBtnBack.Click += (s, e) => GoBack();

        var tsBtnForward = new ToolStripButton("Vorwärts")
        {
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            Image = CreateForwardIcon(),
            ImageAlign = ContentAlignment.TopCenter,
            TextAlign = ContentAlignment.BottomCenter,
            TextImageRelation = TextImageRelation.ImageAboveText,
            Enabled = false
        };
        tsBtnForward.Click += (s, e) => GoForward();

        var btnPrint = new ToolStripButton("Drucken")
        {
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            Image = CreatePrintIcon(),
            ImageAlign = ContentAlignment.TopCenter,
            TextAlign = ContentAlignment.BottomCenter,
            TextImageRelation = TextImageRelation.ImageAboveText
        };
        btnPrint.Click += (s, e) => _webBrowser?.Print();

        var btnOptions = new ToolStripDropDownButton("Optionen")
        {
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            Image = CreateOptionsIcon(),
            ImageAlign = ContentAlignment.TopCenter,
            TextAlign = ContentAlignment.BottomCenter,
            TextImageRelation = TextImageRelation.ImageAboveText
        };
        btnOptions.DropDownItems.Add("Registerkarten ausblenden", null, (s, e) => ToggleNavigation());
        btnOptions.DropDownItems.Add("Zurück", null, (s, e) => GoBack()).Enabled = false;
        btnOptions.DropDownItems.Add("Vorwärts", null, (s, e) => GoForward()).Enabled = false;
        btnOptions.DropDownItems.Add("Startseite", null, (s, e) => NavigateTo("root"));
        btnOptions.DropDownItems.Add(new ToolStripSeparator());
        btnOptions.DropDownItems.Add("Drucken...", null, (s, e) => _webBrowser?.Print());
        btnOptions.DropDownItems.Add("Suchbegriffhervorhebung deaktivieren", null, (s, e) => { });

        _toolStrip.Items.AddRange(new ToolStripItem[]
        {
            btnHide,
            new ToolStripSeparator(),
            tsBtnBack,
            tsBtnForward,
            new ToolStripSeparator(),
            btnPrint,
            new ToolStripSeparator(),
            btnOptions
        });

        _tsBtnBack = tsBtnBack;
        _tsBtnForward = tsBtnForward;

        // === SplitContainer ===
        _split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 4,
            BackColor = SystemColors.Control
        };

        // === Left panel: tab strip + TreeView ===
        _navTabStrip = new Panel
        {
            Dock = DockStyle.Top,
            Height = 24,
            BackColor = SystemColors.Control
        };
        _navTabStrip.Paint += NavTabStrip_Paint;
        _navTabStrip.Click += NavTabStrip_Click;

        _treeView = new TreeView
        {
            Dock = DockStyle.Fill,
            Font = UiFont,
            BorderStyle = BorderStyle.None,
            ShowLines = true,
            Indent = 19,
            ItemHeight = 20
        };

        // Build tree nodes from help topic tree
        var imgList = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
        imgList.Images.Add(CreateBookIcon());       // 0: book (root/branch)
        imgList.Images.Add(CreatePageIcon());       // 1: page (leaf)
        _treeView.ImageList = imgList;

        var rootNode = BuildTreeNode(_rootTopic);
        _treeView.Nodes.Add(rootNode);
        rootNode.Expand();
        _treeView.AfterSelect += TreeView_AfterSelect;

        _split.Panel1.Controls.Add(_treeView);
        _split.Panel1.Controls.Add(_navTabStrip);

        // === Right panel: WebBrowser ===
        _webBrowser = new WebBrowser
        {
            Dock = DockStyle.Fill,
            AllowWebBrowserDrop = false,
            AllowNavigation = true,
            ScrollBarsEnabled = true,
            IsWebBrowserContextMenuEnabled = false,
            WebBrowserShortcutsEnabled = false
        };
        _webBrowser.Navigating += WebBrowser_Navigating;

        _split.Panel2.Controls.Add(_webBrowser);

        // === Assemble ===
        Controls.AddRange(new Control[] { _split, _toolStrip });

        // Navigate to root topic on load (SplitterDistance set here too, when form has actual width)
        Load += (s, e) =>
        {
            _split.Panel1MinSize = 150;
            _split.Panel2MinSize = 300;
            _split.SplitterDistance = 280;
            NavigateTo(_initialTopic);
        };
    }

    private readonly ToolStripButton _tsBtnBack;
    private readonly ToolStripButton _tsBtnForward;

    private int _activeNavTab = 0; // 0=Inhalt, 1=Index, 2=Suchen

    /// <summary>
    /// Recursively builds TreeView nodes from the HelpTopic tree.
    /// </summary>
    private static TreeNode BuildTreeNode(HelpTopic topic)
    {
        var node = new TreeNode(topic.Title)
        {
            Tag = topic.Id,
            ImageIndex = topic.Children.Count > 0 ? 0 : 1,
            SelectedImageIndex = topic.Children.Count > 0 ? 0 : 1
        };
        foreach (var child in topic.Children)
        {
            node.Nodes.Add(BuildTreeNode(child));
            if (child.Children.Count > 0)
                node.Nodes[node.Nodes.Count - 1].Expand();
        }
        return node;
    }

    /// <summary>
    /// Navigates to a topic by ID and updates history.
    /// </summary>
    private void NavigateTo(string topicId)
    {
        if (!_topicMap.TryGetValue(topicId, out var topic))
            return;

        // Update history
        if (_historyIndex < _history.Count - 1)
            _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
        _history.Add(topicId);
        _historyIndex = _history.Count - 1;
        UpdateNavButtons();

        RenderTopic(topic);

        // Select corresponding tree node without triggering AfterSelect recursion
        _treeView.AfterSelect -= TreeView_AfterSelect;
        SelectTreeNode(_treeView.Nodes[0], topicId);
        _treeView.AfterSelect += TreeView_AfterSelect;
    }

    /// <summary>
    /// Renders a topic's HTML in the WebBrowser.
    /// </summary>
    private void RenderTopic(HelpTopic topic)
    {
        if (!string.IsNullOrEmpty(topic.Html))
        {
            _webBrowser.DocumentText = topic.Html;
        }
        else
        {
            // Branch node: show child topic links using shared CSS
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<html><head><meta charset='utf-8'><style>{HelpContent.Css}</style></head><body>");
            sb.AppendLine($"<h1>{topic.Title}</h1>");
            sb.AppendLine("<ul>");
            foreach (var child in topic.Children)
                sb.AppendLine($"  <li><a href='help://{child.Id}'>{child.Title}</a></li>");
            sb.AppendLine("</ul>");
            sb.AppendLine("</body></html>");
            _webBrowser.DocumentText = sb.ToString();
        }
    }

    /// <summary>
    /// Recursively selects the tree node matching the given topic ID.
    /// </summary>
    private bool SelectTreeNode(TreeNode parent, string topicId)
    {
        if (parent.Tag is string id && id == topicId)
        {
            _treeView.SelectedNode = parent;
            return true;
        }
        foreach (TreeNode child in parent.Nodes)
        {
            if (SelectTreeNode(child, topicId))
            {
                parent.Expand();
                return true;
            }
        }
        return false;
    }

    private void GoBack()
    {
        if (_historyIndex > 0)
        {
            _historyIndex--;
            UpdateNavButtons();
            var topicId = _history[_historyIndex];
            if (_topicMap.TryGetValue(topicId, out var topic))
            {
                RenderTopic(topic);
                _treeView.AfterSelect -= TreeView_AfterSelect;
                SelectTreeNode(_treeView.Nodes[0], topicId);
                _treeView.AfterSelect += TreeView_AfterSelect;
            }
        }
    }

    private void GoForward()
    {
        if (_historyIndex < _history.Count - 1)
        {
            _historyIndex++;
            UpdateNavButtons();
            var topicId = _history[_historyIndex];
            if (_topicMap.TryGetValue(topicId, out var topic))
            {
                RenderTopic(topic);
                _treeView.AfterSelect -= TreeView_AfterSelect;
                SelectTreeNode(_treeView.Nodes[0], topicId);
                _treeView.AfterSelect += TreeView_AfterSelect;
            }
        }
    }

    private void UpdateNavButtons()
    {
        _tsBtnBack.Enabled = _historyIndex > 0;
        _tsBtnForward.Enabled = _historyIndex < _history.Count - 1;
    }

    private void TreeView_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        if (e.Node?.Tag is string topicId)
            NavigateTo(topicId);
    }

    /// <summary>
    /// Intercepts navigation to handle internal help:// links and
    /// about:blank#fragment links from HTML DocumentText.
    /// </summary>
    private void WebBrowser_Navigating(object? sender, WebBrowserNavigatingEventArgs e)
    {
        var url = e.Url?.ToString() ?? string.Empty;

        // Internal help:// protocol links
        if (url.StartsWith("help://"))
        {
            e.Cancel = true;
            var topicId = url.Substring(7);
            var hashIdx = topicId.IndexOf('#');
            if (hashIdx >= 0)
            {
                var fragment = topicId.Substring(hashIdx + 1);
                NavigateTo(fragment.Length > 0 ? fragment : topicId.Substring(0, hashIdx));
            }
            else
            {
                NavigateTo(topicId);
            }
            return;
        }

        // Fragment links from DocumentText appear as about:blank#topic-id
        var fragment2 = e.Url?.Fragment;
        if (!string.IsNullOrEmpty(fragment2))
        {
            e.Cancel = true;
            var frag = fragment2.TrimStart('#');
            if (_topicMap.ContainsKey(frag))
                NavigateTo(frag);
            return;
        }

        // Fallback: parse fragment from the full URL string
        var hashInUrl = url.IndexOf('#');
        if (hashInUrl >= 0)
        {
            e.Cancel = true;
            var frag = url.Substring(hashInUrl + 1);
            if (_topicMap.ContainsKey(frag))
                NavigateTo(frag);
            return;
        }

        // Cancel any other external navigation
        if (!url.StartsWith("about:blank"))
            e.Cancel = true;
    }

    private static int GetTabWidth()
    {
        string[] tabs = { "Inhalt", "Index", "Suchen" };
        int maxW = 0;
        using var bmp = new Bitmap(1, 1);
        using var g = Graphics.FromImage(bmp);
        foreach (var tab in tabs)
        {
            var sz = TextRenderer.MeasureText(g, tab, UiFont);
            if (sz.Width > maxW) maxW = sz.Width;
        }
        return Math.Max(maxW + 16, 60);
    }

    private void NavTabStrip_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        string[] tabs = { "Inhalt", "Index", "Suchen" };
        int tabW = GetTabWidth();
        for (int i = 0; i < tabs.Length; i++)
        {
            var rect = new Rectangle(i * tabW, 0, tabW, _navTabStrip.Height - 1);
            bool active = i == _activeNavTab;
            using var bg = new SolidBrush(active ? NavTabActive : NavTabInactive);
            g.FillRectangle(bg, rect);
            if (i > 0)
            {
                using var sepPen = new Pen(Color.FromArgb(180, 180, 180));
                g.DrawLine(sepPen, rect.X, 2, rect.X, rect.Bottom - 2);
            }
            if (!active)
            {
                using var bottomPen = new Pen(Color.FromArgb(160, 160, 160));
                g.DrawLine(bottomPen, rect.X, rect.Bottom, rect.Right, rect.Bottom);
            }
            TextRenderer.DrawText(g, tabs[i], UiFont, rect,
                SystemColors.ControlText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
        using var linePen = new Pen(Color.FromArgb(160, 160, 160));
        g.DrawLine(linePen, 0, _navTabStrip.Height - 1, _navTabStrip.Width, _navTabStrip.Height - 1);
    }

    private void NavTabStrip_Click(object? sender, EventArgs e)
    {
        var mp = _navTabStrip.PointToClient(MousePosition);
        int tabW = GetTabWidth();
        int tab = mp.X / tabW;
        if (tab >= 0 && tab <= 2 && tab != _activeNavTab)
        {
            _activeNavTab = tab;
            _navTabStrip.Invalidate();
            OnNavTabChanged();
        }
    }

    private void OnNavTabChanged()
    {
        switch (_activeNavTab)
        {
            case 0: // Inhalt
                _treeView.Visible = true;
                _indexListView?.Hide();
                _searchPanel?.Hide();
                break;
            case 1: // Index
                EnsureIndexListView();
                _treeView.Visible = false;
                _indexListView!.Visible = true;
                _searchPanel?.Hide();
                break;
            case 2: // Suchen
                EnsureSearchPanel();
                _treeView.Visible = false;
                _indexListView?.Hide();
                _searchPanel!.Visible = true;
                break;
        }
    }

    private void EnsureIndexListView()
    {
        if (_indexListView != null) return;

        _indexListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.List,
            Font = UiFont,
            BorderStyle = BorderStyle.None,
            FullRowSelect = true,
            Sorting = SortOrder.Ascending
        };

        var leafTopics = _topicMap.Values
            .Where(t => !string.IsNullOrEmpty(t.Html))
            .OrderBy(t => t.Title, StringComparer.OrdinalIgnoreCase);

        foreach (var topic in leafTopics)
        {
            var item = new ListViewItem(topic.Title) { Tag = topic.Id };
            _indexListView.Items.Add(item);
        }

        _indexListView.DoubleClick += (s, e) =>
        {
            if (_indexListView.SelectedItems.Count > 0 && _indexListView.SelectedItems[0].Tag is string id)
                NavigateTo(id);
        };

        _split.Panel1.Controls.Add(_indexListView);
        _indexListView.BringToFront();
        _indexListView.Hide();
    }

    private void EnsureSearchPanel()
    {
        if (_searchPanel != null) return;

        _searchPanel = new Panel { Dock = DockStyle.Fill };

        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 32,
            BackColor = SystemColors.Control
        };

        var lblSearch = new Label
        {
            Text = "Suchen:",
            Location = new Point(6, 7),
            Font = UiFont,
            AutoSize = true
        };

        _searchBox = new TextBox
        {
            Location = new Point(58, 5),
            Size = new Size(180, 22),
            Font = UiFont
        };
        _searchBox.TextChanged += (s, e) => PerformSearch();

        var btnSearch = new Button
        {
            Text = "Suchen",
            Location = new Point(244, 4),
            Size = new Size(60, 23),
            Font = UiFont,
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true
        };
        btnSearch.Click += (s, e) => PerformSearch();

        topPanel.Controls.AddRange(new Control[] { lblSearch, _searchBox, btnSearch });

        _searchResults = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = UiFont,
            BorderStyle = BorderStyle.None
        };
        _searchResults.DoubleClick += (s, e) =>
        {
            if (_searchResults.SelectedItem is string title)
            {
                var topic = _topicMap.Values.FirstOrDefault(t => t.Title == title);
                if (topic != null) NavigateTo(topic.Id);
            }
        };

        _searchPanel.Controls.Add(_searchResults);
        _searchPanel.Controls.Add(topPanel);

        _split.Panel1.Controls.Add(_searchPanel);
        _searchPanel.BringToFront();
        _searchPanel.Hide();
    }

    private void PerformSearch()
    {
        if (_searchResults == null || _searchBox == null) return;
        var query = _searchBox.Text.Trim();
        _searchResults.Items.Clear();

        if (string.IsNullOrEmpty(query)) return;

        var results = _topicMap.Values
            .Where(t => !string.IsNullOrEmpty(t.Html) &&
                   (t.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    t.Html.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(t => t.Title, StringComparer.OrdinalIgnoreCase);

        foreach (var topic in results)
            _searchResults.Items.Add(topic.Title);

        if (_searchResults.Items.Count > 0)
            _searchResults.SelectedIndex = 0;
    }

    private void ToggleNavigation()
    {
        _navVisible = !_navVisible;
        _split.Panel1Collapsed = !_navVisible;
    }

    // === Icon creation (16x16) ===

    private static Bitmap CreateHideIcon() => DrawIcon16(g =>
    {
        g.FillRectangle(new SolidBrush(Color.FromArgb(220, 230, 245)), 2, 3, 12, 10);
        g.DrawRectangle(new Pen(Color.FromArgb(60, 90, 150)), 2, 3, 12, 10);
        g.FillRectangle(new SolidBrush(Color.FromArgb(60, 90, 150)), 2, 3, 5, 10);
        g.DrawLine(new Pen(Color.White), 4, 6, 4, 10);
    });

    private static Bitmap CreateBackIcon() => DrawIcon16(g =>
    {
        using var arrowPen = new Pen(Color.FromArgb(60, 90, 150), 2f);
        g.DrawLine(arrowPen, 12, 8, 4, 8);
        g.DrawLine(arrowPen, 4, 8, 7, 5);
        g.DrawLine(arrowPen, 4, 8, 7, 11);
    });

    private static Bitmap CreateForwardIcon() => DrawIcon16(g =>
    {
        using var arrowPen = new Pen(Color.FromArgb(160, 160, 160), 2f);
        g.DrawLine(arrowPen, 4, 8, 12, 8);
        g.DrawLine(arrowPen, 12, 8, 9, 5);
        g.DrawLine(arrowPen, 12, 8, 9, 11);
    });

    private static Bitmap CreatePrintIcon() => DrawIcon16(g =>
    {
        g.FillRectangle(new SolidBrush(Color.FromArgb(200, 200, 200)), 3, 6, 10, 6);
        g.DrawRectangle(new Pen(Color.FromArgb(80, 80, 80)), 3, 6, 10, 6);
        g.FillRectangle(new SolidBrush(Color.White), 5, 2, 6, 5);
        g.DrawRectangle(new Pen(Color.FromArgb(80, 80, 80)), 5, 2, 6, 5);
        g.FillRectangle(new SolidBrush(Color.White), 5, 12, 6, 2);
        g.DrawRectangle(new Pen(Color.FromArgb(80, 80, 80)), 5, 12, 6, 2);
        g.FillRectangle(new SolidBrush(Color.FromArgb(60, 160, 60)), 11, 8, 1, 1);
    });

    private static Bitmap CreateOptionsIcon() => DrawIcon16(g =>
    {
        g.DrawEllipse(new Pen(Color.FromArgb(60, 90, 150), 1.5f), 3, 3, 8, 8);
        g.FillEllipse(new SolidBrush(Color.FromArgb(220, 230, 245)), 5, 5, 4, 4);
        using var arrowPen = new Pen(Color.FromArgb(60, 90, 150), 1.5f);
        g.DrawLine(arrowPen, 11, 12, 13, 12);
        g.DrawLine(arrowPen, 11, 12, 12, 13);
        g.DrawLine(arrowPen, 13, 12, 12, 13);
    });

    private static Bitmap CreateBookIcon() => DrawIcon16(g =>
    {
        var bookColor = new SolidBrush(Color.FromArgb(60, 90, 170));
        var darkColor = new Pen(Color.FromArgb(40, 60, 120));
        g.FillRectangle(bookColor, 3, 3, 10, 11);
        g.DrawRectangle(darkColor, 3, 3, 10, 11);
        g.FillRectangle(new SolidBrush(Color.FromArgb(40, 60, 120)), 3, 3, 2, 11);
        g.FillRectangle(new SolidBrush(Color.White), 6, 5, 6, 7);
        g.DrawRectangle(darkColor, 6, 5, 6, 7);
        g.DrawLine(new Pen(Color.FromArgb(180, 180, 180)), 7, 7, 11, 7);
        g.DrawLine(new Pen(Color.FromArgb(180, 180, 180)), 7, 9, 11, 9);
        g.DrawLine(new Pen(Color.FromArgb(180, 180, 180)), 7, 11, 10, 11);
    });

    private static Bitmap CreatePageIcon() => DrawIcon16(g =>
    {
        // White page with folded corner
        g.FillRectangle(new SolidBrush(Color.White), 4, 2, 8, 12);
        g.DrawRectangle(new Pen(Color.FromArgb(120, 120, 120)), 4, 2, 8, 12);
        // Folded corner
        g.FillPolygon(new SolidBrush(Color.FromArgb(220, 220, 220)),
            new Point[] { new(9, 2), new(12, 5), new(9, 5) });
        g.DrawLine(new Pen(Color.FromArgb(120, 120, 120)), 9, 2, 12, 5);
        g.DrawLine(new Pen(Color.FromArgb(120, 120, 120)), 9, 5, 12, 5);
        // Text lines
        g.DrawLine(new Pen(Color.FromArgb(180, 180, 180)), 5, 7, 10, 7);
        g.DrawLine(new Pen(Color.FromArgb(180, 180, 180)), 5, 9, 10, 9);
        g.DrawLine(new Pen(Color.FromArgb(180, 180, 180)), 5, 11, 9, 11);
    });

    private static Bitmap DrawIcon16(Action<Graphics> draw)
    {
        var bmp = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.Clear(Color.Transparent);
        draw(g);
        return bmp;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // UiFont is static readonly — do not dispose
        }
        base.Dispose(disposing);
    }
}
