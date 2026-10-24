using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// HTML Help viewer style form, themed (light/dark) via <see cref="ThemeManager"/>.
/// Two-panel layout: left navigation (Inhalt/Index/Suchen + TreeView),
/// right content area with WebBrowser rendering HTML help topics (themed style sheet,
/// re-rendered when the theme changes).
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

    private static readonly Font UiFont = ThemeFonts.Regular;

    private readonly ToolStripButton _tsBtnHide;
    private readonly ToolStripButton _tsBtnPrint;
    private readonly ToolStripDropDownButton _tsBtnOptions;
    private readonly ImageList _treeImages;
    private readonly List<Image> _themeImages = new();
    private int _hotNavTab = -1;

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
            Height = 44,
            GripStyle = ToolStripGripStyle.Hidden,
            Padding = new Padding(6, 2, 6, 2)
        };

        var btnHide = new ToolStripButton("Ausblenden")
        {
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            ImageAlign = ContentAlignment.TopCenter,
            TextAlign = ContentAlignment.BottomCenter,
            TextImageRelation = TextImageRelation.ImageAboveText
        };
        btnHide.Click += (s, e) => ToggleNavigation();
        _tsBtnHide = btnHide;

        var tsBtnBack = new ToolStripButton("Zurück")
        {
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            ImageAlign = ContentAlignment.TopCenter,
            TextAlign = ContentAlignment.BottomCenter,
            TextImageRelation = TextImageRelation.ImageAboveText,
            Enabled = false
        };
        tsBtnBack.Click += (s, e) => GoBack();

        var tsBtnForward = new ToolStripButton("Vorwärts")
        {
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            ImageAlign = ContentAlignment.TopCenter,
            TextAlign = ContentAlignment.BottomCenter,
            TextImageRelation = TextImageRelation.ImageAboveText,
            Enabled = false
        };
        tsBtnForward.Click += (s, e) => GoForward();

        var btnPrint = new ToolStripButton("Drucken")
        {
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            ImageAlign = ContentAlignment.TopCenter,
            TextAlign = ContentAlignment.BottomCenter,
            TextImageRelation = TextImageRelation.ImageAboveText
        };
        btnPrint.Click += (s, e) => _webBrowser?.Print();
        _tsBtnPrint = btnPrint;

        var btnOptions = new ToolStripDropDownButton("Optionen")
        {
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            ImageAlign = ContentAlignment.TopCenter,
            TextAlign = ContentAlignment.BottomCenter,
            TextImageRelation = TextImageRelation.ImageAboveText
        };
        _tsBtnOptions = btnOptions;
        _miToggleNav = (ToolStripMenuItem)btnOptions.DropDownItems.Add("Registerkarten ausblenden", null, (s, e) => ToggleNavigation());
        _miBack = (ToolStripMenuItem)btnOptions.DropDownItems.Add("Zurück", null, (s, e) => GoBack());
        _miBack.Enabled = false;
        _miForward = (ToolStripMenuItem)btnOptions.DropDownItems.Add("Vorwärts", null, (s, e) => GoForward());
        _miForward.Enabled = false;
        btnOptions.DropDownItems.Add("Startseite", null, (s, e) => NavigateTo("root"));
        btnOptions.DropDownItems.Add(new ToolStripSeparator());
        btnOptions.DropDownItems.Add("Drucken...", null, (s, e) => _webBrowser?.Print());

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
            SplitterWidth = 4
        };
        // Navigation side = secondary surface (Discord sidebar), content = main background
        ThemeManager.SetRole(_split.Panel1, ThemeRole.Secondary);

        // === Left panel: tab strip + TreeView ===
        _navTabStrip = new Panel
        {
            Dock = DockStyle.Top,
            Height = 34
        };
        _navTabStrip.Paint += NavTabStrip_Paint;
        _navTabStrip.Click += NavTabStrip_Click;
        _navTabStrip.MouseMove += (s, e) =>
        {
            int hot = e.X / GetTabWidth();
            hot = hot is >= 0 and <= 2 ? hot : -1;
            if (hot != _hotNavTab) { _hotNavTab = hot; _navTabStrip.Invalidate(); }
        };
        _navTabStrip.MouseLeave += (s, e) => { _hotNavTab = -1; _navTabStrip.Invalidate(); };
        _navTabStrip.Cursor = Cursors.Hand;
        EnableDoubleBuffer(_navTabStrip);

        _treeView = new TreeView
        {
            Dock = DockStyle.Fill,
            Font = UiFont,
            BorderStyle = BorderStyle.None,
            ShowLines = false,
            FullRowSelect = true,
            HotTracking = true,
            Indent = 19,
            ItemHeight = 22
        };

        // Build tree nodes from help topic tree
        _treeImages = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
        // 0: book (root/branch), 1: page (leaf) - created in UpdateThemeImages()
        _treeView.ImageList = _treeImages;

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

        UpdateThemeImages();
        ThemeManager.Apply(this);
        ThemeManager.ThemeChanged += OnThemeChanged;

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
    private readonly ToolStripMenuItem _miToggleNav;
    private readonly ToolStripMenuItem _miBack;
    private readonly ToolStripMenuItem _miForward;

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
            _webBrowser.DocumentText = HelpContent.ApplyTheme(topic.Html, ThemeManager.Current);
        }
        else
        {
            // Branch node: show child topic links using shared CSS
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<html><head><meta http-equiv='X-UA-Compatible' content='IE=edge'><meta charset='utf-8'><style>{HelpContent.ThemedCss(ThemeManager.Current)}</style></head><body>");
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
        bool canBack = _historyIndex > 0;
        bool canForward = _historyIndex < _history.Count - 1;
        _tsBtnBack.Enabled = canBack;
        _tsBtnForward.Enabled = canForward;
        _miBack.Enabled = canBack;
        _miForward.Enabled = canForward;
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
        var p = ThemeManager.Current;
        var g = e.Graphics;
        g.Clear(_navTabStrip.BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        string[] tabs = { "Inhalt", "Index", "Suchen" };
        int tabW = GetTabWidth();
        for (int i = 0; i < tabs.Length; i++)
        {
            var rect = new Rectangle(i * tabW, 0, tabW, _navTabStrip.Height - 1);
            bool active = i == _activeNavTab;
            bool hot = i == _hotNavTab && !active;
            var pill = new RectangleF(rect.X + 4, rect.Y + 5, rect.Width - 8, rect.Height - 10);
            if (active || hot)
            {
                using var path = ThemeGlyphs.Rounded(pill, 4);
                using var bg = new SolidBrush(active ? p.Selected : p.Hover);
                g.FillPath(bg, path);
            }
            TextRenderer.DrawText(g, tabs[i], active ? ThemeFonts.Semibold : UiFont, rect,
                active ? p.Text : hot ? p.TextSecondary : p.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
        using var linePen = new Pen(p.Border);
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
            Height = 36
        };

        var lblSearch = new Label
        {
            Text = "Suchen:",
            Location = new Point(6, 10),
            Font = UiFont,
            AutoSize = true
        };

        _searchBox = new TextBox
        {
            Location = new Point(58, 7),
            Size = new Size(148, 23),
            Font = UiFont
        };
        _searchBox.TextChanged += (s, e) => PerformSearch();

        var btnSearch = new Button
        {
            Text = "Suchen",
            Location = new Point(212, 5),
            Size = new Size(62, 27),
            Font = UiFont
        };
        ThemeManager.SetRole(btnSearch, ThemeRole.Primary);
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

    private static readonly Dictionary<string, string> PlainTextCache = new();

    /// <summary>
    /// Visible text of a topic (style block and markup removed, entities decoded),
    /// so that searching never matches CSS or tag/attribute names.
    /// </summary>
    private static string GetPlainText(HelpTopic topic)
    {
        if (PlainTextCache.TryGetValue(topic.Id, out var cached))
            return cached;

        var html = System.Text.RegularExpressions.Regex.Replace(
            topic.Html, @"<(style|script)\b[^>]*>.*?</\1\s*>", " ",
            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        html = System.Text.RegularExpressions.Regex.Replace(html, @"<[^>]*>", " ");
        var text = System.Text.RegularExpressions.Regex.Replace(System.Net.WebUtility.HtmlDecode(html), @"\s+", " ").Trim();
        PlainTextCache[topic.Id] = text;
        return text;
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
                    GetPlainText(t).Contains(query, StringComparison.OrdinalIgnoreCase)))
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
        _miToggleNav.Text = _navVisible ? "Registerkarten ausblenden" : "Registerkarten einblenden";
    }

    // === Themed icons (16x16) ===

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        UpdateThemeImages();
        _navTabStrip.Invalidate();
        // Re-render the current page with the new style sheet
        if (_historyIndex >= 0 && _historyIndex < _history.Count && _topicMap.TryGetValue(_history[_historyIndex], out var topic))
            RenderTopic(topic);
    }

    /// <summary>(Re)creates the toolbar and tree icons in the colors of the current theme.</summary>
    private void UpdateThemeImages()
    {
        var p = ThemeManager.Current;
        var color = p.TextSecondary;
        var old = _themeImages.ToArray();
        _themeImages.Clear();

        Image Own(Image image) { _themeImages.Add(image); return image; }

        _tsBtnHide.Image = Own(DrawIcon16(g => DrawSidebarIcon(g, color)));
        _tsBtnBack.Image = Own(DrawIcon16(g => DrawArrowIcon(g, color, left: true)));
        _tsBtnForward.Image = Own(DrawIcon16(g => DrawArrowIcon(g, color, left: false)));
        _tsBtnPrint.Image = Own(ThemeGlyphs.CreateBitmap(Glyph.Printer, 16, color));
        _tsBtnOptions.Image = Own(ThemeGlyphs.CreateBitmap(Glyph.Gear, 16, color));

        _treeImages.Images.Clear();
        _treeImages.Images.Add(CreateBookIcon(p));   // 0: book (root/branch)
        _treeImages.Images.Add(CreatePageIcon(p));   // 1: page (leaf)

        foreach (var image in old) image.Dispose();
        _toolStrip.Invalidate();
        _treeView.Invalidate();
    }

    private static Pen LinePen(Color color, float width = 1.5f) => new(color, width)
    {
        StartCap = LineCap.Round,
        EndCap = LineCap.Round,
        LineJoin = LineJoin.Round
    };

    private static void DrawSidebarIcon(Graphics g, Color color)
    {
        using var pen = LinePen(color);
        using (var path = ThemeGlyphs.Rounded(new RectangleF(1.75f, 2.75f, 12.5f, 10.5f), 2f))
            g.DrawPath(pen, path);
        g.DrawLine(pen, 6f, 3f, 6f, 13f);
        using var thin = LinePen(color, 1.25f);
        g.DrawLine(thin, 3.5f, 5.75f, 4.25f, 5.75f);
        g.DrawLine(thin, 3.5f, 8f, 4.25f, 8f);
    }

    private static void DrawArrowIcon(Graphics g, Color color, bool left)
    {
        using var pen = LinePen(color, 1.75f);
        float tip = left ? 2.75f : 13.25f, back = left ? 7f : 9f;
        g.DrawLine(pen, 2.75f, 8f, 13.25f, 8f);
        g.DrawLines(pen, new PointF[] { new(back, 3.75f), new(tip, 8f), new(back, 12.25f) });
    }

    private static Bitmap CreateBookIcon(ThemePalette p) => DrawIcon16(g =>
    {
        // Closed book in the accent color with light pages
        using (var cover = ThemeGlyphs.Rounded(new RectangleF(2.5f, 1.5f, 11f, 13f), 1.75f))
        using (var brush = new SolidBrush(p.Accent))
            g.FillPath(brush, cover);
        using (var spine = new SolidBrush(p.AccentPressed))
            g.FillRectangle(spine, 2.5f, 2.5f, 2.25f, 11f);
        using (var pages = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
            g.FillRectangle(pages, 6.5f, 4f, 5f, 1.5f);
        using (var pages = new SolidBrush(Color.FromArgb(150, 255, 255, 255)))
            g.FillRectangle(pages, 6.5f, 7f, 3.5f, 1.25f);
    });

    private static Bitmap CreatePageIcon(ThemePalette p) => DrawIcon16(g =>
    {
        var line = p.IsDark ? p.TextMuted : ThemePalette.Blend(p.TextMuted, p.Secondary, 0.15f);
        var fill = p.IsDark ? p.Background : Color.White;
        var outline = new PointF[] { new(3.25f, 1.75f), new(9.5f, 1.75f), new(12.75f, 5f), new(12.75f, 14.25f), new(3.25f, 14.25f) };
        using (var brush = new SolidBrush(fill))
            g.FillPolygon(brush, outline);
        using var pen = LinePen(line, 1.25f);
        g.DrawPolygon(pen, outline);
        g.DrawLines(pen, new PointF[] { new(9.5f, 1.75f), new(9.5f, 5f), new(12.75f, 5f) });
        using var text = LinePen(ThemePalette.Blend(line, fill, 0.35f), 1f);
        g.DrawLine(text, 5.5f, 8f, 10.5f, 8f);
        g.DrawLine(text, 5.5f, 10.25f, 10.5f, 10.25f);
        g.DrawLine(text, 5.5f, 12.25f, 8.75f, 12.25f);
    });

    private static Bitmap DrawIcon16(Action<Graphics> draw)
    {
        var bmp = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.Clear(Color.Transparent);
        draw(g);
        return bmp;
    }

    private static void EnableDoubleBuffer(Control control)
    {
        try
        {
            typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(control, true);
        }
        catch
        {
            // optional
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // UiFont is static readonly — do not dispose
            ThemeManager.ThemeChanged -= OnThemeChanged;
            _treeImages.Dispose();
            foreach (var image in _themeImages) image.Dispose();
            _themeImages.Clear();
        }
        base.Dispose(disposing);
    }
}
