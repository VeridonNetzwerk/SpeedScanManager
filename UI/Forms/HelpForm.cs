using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Help viewer in the modern window style (<see cref="ModernWindow"/>): header with logo, title
/// (subtitle = current topic) and round minimize / maximize / close buttons; a resizable
/// sidebar with the navigation "Inhalt / Index / Suchen" (NavSidebar look) above the topic tree,
/// index list or search; on the right a toolbar (Ausblenden, Startseite, Zurück, Vorwärts, Drucken,
/// Optionen) and a WebBrowser that renders the help pages of <see cref="HelpRenderer"/>
/// (start page with topic cards, group pages, pages with pictures; re-rendered when the theme changes).
///
/// The window is borderless but resizable: the edges (5 px) are hit-tested locally
/// (WM_NCHITTEST of the form plus a message filter for the child windows, including the
/// WebBrowser) and the maximized size is limited to the working area of the monitor.
/// </summary>
internal class HelpForm : ModernWindow
{
    private const int WM_NCHITTEST = 0x0084;
    private const int WM_GETMINMAXINFO = 0x0024;
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WS_MAXIMIZEBOX = 0x00010000;
    private const int HTCLIENT = 1;
    private const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;
    private const int ResizeBorder = 5;
    private const int MinWidth = 600, MinHeight = 420;

    private readonly SplitContainer _split;
    private readonly NavList _navTabs;
    private readonly SectionHeading _navCaption;
    private readonly Panel _navContent;
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

    private readonly ToolButton _tsBtnHide;
    private readonly ToolButton _tsBtnBack;
    private readonly ToolButton _tsBtnForward;
    private readonly ToolButton _tsBtnOptions;
    private readonly ContextMenuStrip _optionsMenu;
    private readonly ToolStripMenuItem _miToggleNav;
    private readonly ToolStripMenuItem _miBack;
    private readonly ToolStripMenuItem _miForward;
    private readonly ImageList _treeImages;
    private readonly HeaderIconButton _btnMinimize;
    private readonly MaximizeButton _btnMaximize;
    private readonly EdgeResizeFilter _resizeFilter;

    private readonly string _initialTopic;
    private int _activeNavTab = 0; // 0=Inhalt, 1=Index, 2=Suchen

    private static readonly string[] NavCaptions = { "Themen", "Stichwörter", "Suchergebnisse" };

    /// <summary>"Tour starten" was clicked: the caller starts the guided tour after closing.</summary>
    public bool TourRequested { get; private set; }

    /// <param name="offerTour">Shows "Tour starten" in the toolbar (help opened from the settings window).</param>
    public HelpForm(string? topicId = null, bool offerTour = false)
    {
        SuspendLayout();
        _initialTopic = topicId ?? "root";
        Text = "SpeedScan Manager Hilfe";
        Title = "SpeedScan Manager Hilfe";
        HeaderButtons = ModernHeaderButtons.Close;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        CloseOnEscape = false; // Esc never closed the help window
        ClientSize = new Size(1000, 680);

        // Build help topic tree
        _rootTopic = HelpContent.BuildTree();
        _topicMap = HelpContent.BuildTopicMap(_rootTopic);

        // === Header: minimize + maximize/restore (the base provides close) ===
        _btnMinimize = new HeaderIconButton(HeaderIconButton.Kind.Minimize) { Size = new Size(30, 30), AccessibleName = "Minimieren" };
        _btnMinimize.Click += (_, _) => WindowState = FormWindowState.Minimized;
        _btnMaximize = new MaximizeButton { Size = new Size(30, 30), AccessibleName = "Maximieren" };
        _btnMaximize.Click += (_, _) => ToggleMaximize();
        Header.Controls.Add(_btnMinimize);
        Header.Controls.Add(_btnMaximize);
        Header.Layout += (_, _) => LayoutHeaderButtons();
        // Double-click on the header toggles maximize (raised before the header starts dragging).
        Header.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left && e.Clicks == 2)
                ToggleMaximize();
        };

        // === SplitContainer: sidebar | content ===
        _split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 5,
            FixedPanel = FixedPanel.Panel1
        };
        ThemeManager.SetRole(_split.Panel1, ThemeRole.Secondary);
        ThemeManager.SetRole(_split.Panel2, ThemeRole.Surface);
        _split.Paint += Split_Paint;

        // === Sidebar: navigation (Inhalt/Index/Suchen), caption, tree ===
        _navTabs = new NavList { Dock = DockStyle.Top, Height = 10 + 3 * 38 + 4 };
        _navTabs.Add("Inhalt", DrawBookIcon);
        _navTabs.Add("Index", DrawIndexIcon);
        _navTabs.Add("Suchen", DrawSearchIcon);
        _navTabs.SelectedIndexChanged += (_, _) =>
        {
            _activeNavTab = _navTabs.SelectedIndex;
            OnNavTabChanged();
        };

        var captionHost = new Panel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(20, 0, 10, 6) };
        _navCaption = new SectionHeading(NavCaptions[0]) { Dock = DockStyle.Fill };
        captionHost.Controls.Add(_navCaption);
        captionHost.Paint += (_, e) =>
        {
            // divider between the navigation and the list
            using var pen = new Pen(ThemeManager.Current.IsDark ? ThemeManager.Current.Border : ThemeManager.Current.CardDivider);
            int x = ModernLayout.Scale(captionHost, 12);
            e.Graphics.DrawLine(pen, x, 0, captionHost.Width - x, 0);
        };

        _navContent = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 0, 4, 8) };

        _treeView = new SidebarTreeView
        {
            Dock = DockStyle.Fill,
            Font = UiFont,
            BorderStyle = BorderStyle.None,
            ShowLines = false,
            FullRowSelect = true,
            HotTracking = true,
            Indent = 16,
            ItemHeight = 26,
            ShowNodeToolTips = true
        };

        // Tree icons: 0 = book (root/branch), 1 = page (leaf) - created in UpdateThemeImages()
        _treeImages = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
        _treeView.ImageList = _treeImages;

        var rootNode = BuildTreeNode(_rootTopic);
        _treeView.Nodes.Add(rootNode);
        rootNode.Expand();
        _treeView.AfterSelect += TreeView_AfterSelect;

        _navContent.Controls.Add(_treeView);
        _split.Panel1.Controls.Add(_navContent);
        _split.Panel1.Controls.Add(captionHost);
        _split.Panel1.Controls.Add(_navTabs);

        // === Content: toolbar + WebBrowser ===
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 52,
            Padding = new Padding(12, 10, 12, 10),
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight
        };
        ThemeManager.SetRole(toolbar, ThemeRole.Surface);
        toolbar.Paint += (_, e) =>
        {
            var p = ThemeManager.Current;
            using var pen = new Pen(p.IsDark ? p.Tertiary : p.CardBorder);
            e.Graphics.DrawLine(pen, 0, toolbar.Height - 1, toolbar.Width, toolbar.Height - 1);
        };

        _tsBtnHide = new ToolButton("Ausblenden", DrawSidebarIcon) { ToolTipText = "Navigation aus- oder einblenden" };
        _tsBtnHide.Click += (s, e) => ToggleNavigation();

        var tsBtnHome = new ToolButton("Startseite", DrawHomeIcon) { ToolTipText = "Zur Startseite der Hilfe" };
        tsBtnHome.Click += (s, e) => NavigateTo("root");

        _tsBtnBack = new ToolButton("Zurück", (g, c) => DrawArrowIcon(g, c, left: true)) { Enabled = false };
        _tsBtnBack.Click += (s, e) => GoBack();

        _tsBtnForward = new ToolButton("Vorwärts", (g, c) => DrawArrowIcon(g, c, left: false)) { Enabled = false };
        _tsBtnForward.Click += (s, e) => GoForward();

        var tsBtnPrint = new ToolButton("Drucken", (g, c) => ThemeGlyphs.Draw(g, Glyph.Printer, new RectangleF(0, 0, 16, 16), c));
        tsBtnPrint.Click += (s, e) => _webBrowser?.Print();

        _tsBtnOptions = new ToolButton("Optionen", (g, c) => ThemeGlyphs.Draw(g, Glyph.Gear, new RectangleF(0, 0, 16, 16), c)) { DropDown = true };

        _optionsMenu = new ContextMenuStrip();
        _miToggleNav = (ToolStripMenuItem)_optionsMenu.Items.Add("Navigation ausblenden", null, (s, e) => ToggleNavigation());
        _miBack = (ToolStripMenuItem)_optionsMenu.Items.Add("Zurück", null, (s, e) => GoBack());
        _miBack.Enabled = false;
        _miForward = (ToolStripMenuItem)_optionsMenu.Items.Add("Vorwärts", null, (s, e) => GoForward());
        _miForward.Enabled = false;
        _optionsMenu.Items.Add("Startseite", null, (s, e) => NavigateTo("root"));
        _optionsMenu.Items.Add(new ToolStripSeparator());
        _optionsMenu.Items.Add("Drucken...", null, (s, e) => _webBrowser?.Print());
        _tsBtnOptions.Click += (s, e) =>
        {
            _tsBtnOptions.Pressed = true;
            _optionsMenu.Show(_tsBtnOptions, new Point(0, _tsBtnOptions.Height + ModernLayout.Scale(_tsBtnOptions, 4)));
        };
        _optionsMenu.Closed += (_, _) => _tsBtnOptions.Pressed = false;

        toolbar.Controls.AddRange(new Control[]
        {
            _tsBtnHide,
            new ToolSeparator(),
            tsBtnHome,
            _tsBtnBack,
            _tsBtnForward,
            new ToolSeparator(),
            tsBtnPrint,
            _tsBtnOptions
        });
        if (offerTour)
        {
            var tsBtnTour = new ToolButton("Tour starten", (g, c) => ThemeGlyphs.Draw(g, Glyph.Sparkle, new RectangleF(0, 0, 16, 16), c))
            {
                ToolTipText = "Die Einführung durch das Einstellungsfenster noch einmal zeigen"
            };
            tsBtnTour.Click += (s, e) =>
            {
                TourRequested = true;
                Close();
            };
            toolbar.Controls.Add(new ToolSeparator());
            toolbar.Controls.Add(tsBtnTour);
        }

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
        _split.Panel2.Controls.Add(toolbar);

        Body.Controls.Add(_split);

        UpdateThemeImages();
        ThemeManager.Apply(this);
        ThemeManager.Apply(_optionsMenu);
        ThemeManager.ThemeChanged += OnThemeChanged;

        _resizeFilter = new EdgeResizeFilter(this);
        _navTabs.SelectedIndex = 0;

        // Navigate to the initial topic on load (SplitterDistance set here too, when the form has its actual width)
        Load += (s, e) =>
        {
            _split.Panel1MinSize = ModernLayout.Scale(this, 170);
            _split.Panel2MinSize = ModernLayout.Scale(this, 300);
            _split.SplitterDistance = ModernLayout.Scale(this, 272);
            NavigateTo(_initialTopic);
        };
        ResumeLayout(false);
    }

    /// <summary>
    /// Recursively builds TreeView nodes from the HelpTopic tree.
    /// </summary>
    private static TreeNode BuildTreeNode(HelpTopic topic)
    {
        var node = new TreeNode(topic.Title)
        {
            Tag = topic.Id,
            ToolTipText = topic.Title,
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
        Subtitle = topic.Id == "root" ? "" : topic.Title;
        _webBrowser.DocumentText = HelpRenderer.RenderPage(topic, ThemeManager.Current);
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
            var topicId = url.Substring(7).TrimEnd('/');
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

    private void OnNavTabChanged()
    {
        if (_activeNavTab is >= 0 and <= 2)
            _navCaption.Text = NavCaptions[_activeNavTab];
        _navCaption.Invalidate();
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
            View = View.Details,
            HeaderStyle = ColumnHeaderStyle.None,
            Font = UiFont,
            BorderStyle = BorderStyle.None,
            FullRowSelect = true,
            MultiSelect = false,
            ShowItemToolTips = true,
            Sorting = SortOrder.Ascending
        };
        _indexListView.Columns.Add("Stichwort");
        // Row height like the topic tree (a 1 px wide image list sets the height)
        _indexRowImages = new ImageList { ImageSize = new Size(1, ModernLayout.Scale(this, 26)) };
        _indexListView.SmallImageList = _indexRowImages;
        _indexListView.ClientSizeChanged += (s, e) => FitIndexColumn();

        var leafTopics = _topicMap.Values
            .Where(t => !string.IsNullOrEmpty(t.Html))
            .OrderBy(t => t.Title, StringComparer.OrdinalIgnoreCase);

        foreach (var topic in leafTopics)
        {
            var item = new ListViewItem(topic.Title) { Tag = topic.Id, ToolTipText = topic.Title };
            _indexListView.Items.Add(item);
        }

        _indexListView.DoubleClick += (s, e) =>
        {
            if (_indexListView.SelectedItems.Count > 0 && _indexListView.SelectedItems[0].Tag is string id)
                NavigateTo(id);
        };

        _navContent.Controls.Add(_indexListView);
        _indexListView.BringToFront();
        _indexListView.Hide();
        FitIndexColumn();
    }

    private ImageList? _indexRowImages;

    /// <summary>The single index column always fills the list (no horizontal scrolling).</summary>
    private void FitIndexColumn()
    {
        if (_indexListView == null || _indexListView.Columns.Count == 0) return;
        int w = Math.Max(40, _indexListView.ClientSize.Width - 1);
        if (_indexListView.Columns[0].Width != w)
            _indexListView.Columns[0].Width = w;
    }

    private void EnsureSearchPanel()
    {
        if (_searchPanel != null) return;

        _searchPanel = new Panel { Dock = DockStyle.Fill };

        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(4, 2, 4, 8)
        };

        _searchBox = new TextBox
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Font = UiFont,
            PlaceholderText = "Suchbegriff",
            AccessibleName = "Suchen"
        };
        _searchBox.TextChanged += (s, e) => PerformSearch();

        var btnSearch = new Button
        {
            Text = "Suchen",
            Dock = DockStyle.Right,
            Width = 76,
            Font = UiFont
        };
        ThemeManager.SetRole(btnSearch, ThemeRole.Primary);
        btnSearch.Click += (s, e) => PerformSearch();

        topPanel.Controls.Add(_searchBox);
        topPanel.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 6 });
        topPanel.Controls.Add(btnSearch);

        _searchResults = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = UiFont,
            BorderStyle = BorderStyle.None,
            IntegralHeight = false
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

        _navContent.Controls.Add(_searchPanel);
        _searchPanel.BringToFront();
        _searchPanel.Hide();
        ApplySidebarListColors();
    }

    /// <summary>The search result list sits on the sidebar surface (not an input field).</summary>
    private void ApplySidebarListColors()
    {
        if (_searchResults != null)
        {
            _searchResults.BackColor = ThemeManager.Current.Secondary;
            _searchResults.ItemHeight = ModernLayout.Scale(_searchResults, 26);
            _searchResults.Invalidate();
        }
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

        var text = HelpRenderer.PlainText(topic.Summary + " " + topic.Html);
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
        _miToggleNav.Text = _navVisible ? "Navigation ausblenden" : "Navigation einblenden";
        _tsBtnHide.Text = _navVisible ? "Ausblenden" : "Einblenden";
    }

    // ───────────────────────── Window: maximize, resize ─────────────────────────

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.Style |= WS_MAXIMIZEBOX; // system menu / taskbar "Maximieren"
            return cp;
        }
    }

    private void ToggleMaximize()
        => WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;

    private void LayoutHeaderButtons()
    {
        int size = ModernLayout.Scale(Header, 30);
        int gap = ModernLayout.Scale(Header, 4);
        int y = (Header.Height - size) / 2;
        // The base places the close button at the right (12 px margin): minimize, maximize, close.
        int closeX = Header.Width - ModernLayout.Scale(Header, 12) - size;
        _btnMaximize.Bounds = new Rectangle(closeX - gap - size, y, size, size);
        _btnMinimize.Bounds = new Rectangle(closeX - 2 * (gap + size), y, size, size);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_btnMaximize == null) return;
        bool max = WindowState == FormWindowState.Maximized;
        if (_btnMaximize.Restore != max)
        {
            _btnMaximize.Restore = max;
            _btnMaximize.AccessibleName = max ? "Wiederherstellen" : "Maximieren";
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Application.AddMessageFilter(_resizeFilter);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        Application.RemoveMessageFilter(_resizeFilter);
        base.OnHandleDestroyed(e);
    }

    /// <summary>Resize hit code for a point in client coordinates (0 = none).</summary>
    private int HitTestEdge(Point client)
    {
        if (WindowState != FormWindowState.Normal) return 0;
        int b = ModernLayout.Scale(this, ResizeBorder);
        int w = ClientSize.Width, h = ClientSize.Height;
        if (client.X < 0 || client.Y < 0 || client.X >= w || client.Y >= h) return 0;
        bool left = client.X < b, right = client.X >= w - b;
        bool top = client.Y < b, bottom = client.Y >= h - b;
        int corner = b * 2; // larger grab area in the corners
        if ((top && client.X < corner) || (left && client.Y < corner)) return HTTOPLEFT;
        if ((top && client.X >= w - corner) || (right && client.Y < corner)) return HTTOPRIGHT;
        if ((bottom && client.X < corner) || (left && client.Y >= h - corner)) return HTBOTTOMLEFT;
        if ((bottom && client.X >= w - corner) || (right && client.Y >= h - corner)) return HTBOTTOMRIGHT;
        if (left) return HTLEFT;
        if (right) return HTRIGHT;
        if (top) return HTTOP;
        if (bottom) return HTBOTTOM;
        return 0;
    }

    private static Cursor CursorFor(int hit) => hit switch
    {
        HTLEFT or HTRIGHT => Cursors.SizeWE,
        HTTOP or HTBOTTOM => Cursors.SizeNS,
        HTTOPLEFT or HTBOTTOMRIGHT => Cursors.SizeNWSE,
        _ => Cursors.SizeNESW
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize;
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hWnd, int flags);

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_NCHITTEST:
            {
                base.WndProc(ref m);
                if ((int)m.Result == HTCLIENT)
                {
                    long lp = m.LParam.ToInt64();
                    var screen = new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF));
                    int hit = HitTestEdge(PointToClient(screen));
                    if (hit != 0) m.Result = (IntPtr)hit;
                }
                return;
            }
            case WM_GETMINMAXINFO:
            {
                base.WndProc(ref m);
                // Borderless: maximize to the working area (not over the taskbar) and keep a minimum size.
                var info = Marshal.PtrToStructure<MINMAXINFO>(m.LParam);
                var screen = IsHandleCreated ? Screen.FromHandle(Handle) : Screen.PrimaryScreen!;
                var work = screen.WorkingArea;
                var bounds = screen.Bounds;
                info.ptMaxPosition = new POINT { X = work.X - bounds.X, Y = work.Y - bounds.Y };
                info.ptMaxSize = new POINT { X = work.Width, Y = work.Height };
                info.ptMinTrackSize = new POINT { X = ModernLayout.Scale(this, MinWidth), Y = ModernLayout.Scale(this, MinHeight) };
                Marshal.StructureToPtr(info, m.LParam, false);
                return;
            }
        }
        base.WndProc(ref m);
    }

    /// <summary>
    /// Resizing at the window edges over child windows (header, sidebar, WebBrowser): shows the
    /// size cursor and starts the system resize on a left click within <see cref="ResizeBorder"/>.
    /// </summary>
    private sealed class EdgeResizeFilter : IMessageFilter
    {
        private readonly HelpForm _form;

        public EdgeResizeFilter(HelpForm form) => _form = form;

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WM_MOUSEMOVE && m.Msg != WM_LBUTTONDOWN) return false;
            if (!_form.IsHandleCreated || !_form.Visible || _form.WindowState != FormWindowState.Normal) return false;
            if (m.HWnd == _form.Handle || GetAncestor(m.HWnd, 2 /* GA_ROOT */) != _form.Handle) return false;

            var screen = Control.MousePosition;
            int hit = _form.HitTestEdge(_form.PointToClient(screen));
            if (hit == 0) return false;

            if (m.Msg == WM_MOUSEMOVE)
            {
                Cursor.Current = CursorFor(hit);
                return true;
            }
            ReleaseCapture();
            SendMessage(_form.Handle, WM_NCLBUTTONDOWN, (IntPtr)hit, (IntPtr)((screen.Y << 16) | (screen.X & 0xFFFF)));
            return true;
        }
    }

    // ───────────────────────── Theme ─────────────────────────

    private void Split_Paint(object? sender, PaintEventArgs e)
    {
        // Splitter: content surface with a hairline next to the sidebar (no gray bar).
        var p = ThemeManager.Current;
        var r = _split.SplitterRectangle;
        using (var brush = new SolidBrush(p.Background))
            e.Graphics.FillRectangle(brush, r);
        if (!p.IsDark)
        {
            using var pen = new Pen(p.CardBorder);
            e.Graphics.DrawLine(pen, r.X, r.Y, r.X, r.Bottom);
        }
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        UpdateThemeImages();
        ApplySidebarListColors();
        _split.Invalidate();
        // Re-render the current page with the new style sheet
        if (_historyIndex >= 0 && _historyIndex < _history.Count && _topicMap.TryGetValue(_history[_historyIndex], out var topic))
            RenderTopic(topic);
    }

    /// <summary>(Re)creates the tree icons in the colors of the current theme.</summary>
    private void UpdateThemeImages()
    {
        var p = ThemeManager.Current;
        _treeImages.Images.Clear();
        _treeImages.Images.Add(CreateBookIcon(p));   // 0: book (root/branch)
        _treeImages.Images.Add(CreatePageIcon(p));   // 1: page (leaf)
        _treeView.Invalidate();
    }

    // ───────────────────────── Icons (16 x 16 grid) ─────────────────────────

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

    private static void DrawHomeIcon(Graphics g, Color color)
    {
        using var pen = LinePen(color);
        // roof and house body with a door
        g.DrawLines(pen, new PointF[] { new(1.75f, 7.5f), new(8f, 2f), new(14.25f, 7.5f) });
        g.DrawLines(pen, new PointF[] { new(3.5f, 6.25f), new(3.5f, 13.75f), new(12.5f, 13.75f), new(12.5f, 6.25f) });
        g.DrawLines(pen, new PointF[] { new(6.5f, 13.75f), new(6.5f, 10f), new(9.5f, 10f), new(9.5f, 13.75f) });
    }

    private static void DrawArrowIcon(Graphics g, Color color, bool left)
    {
        using var pen = LinePen(color, 1.75f);
        float tip = left ? 2.75f : 13.25f, back = left ? 7f : 9f;
        g.DrawLine(pen, 2.75f, 8f, 13.25f, 8f);
        g.DrawLines(pen, new PointF[] { new(back, 3.75f), new(tip, 8f), new(back, 12.25f) });
    }

    private static void DrawBookIcon(Graphics g, Color color)
    {
        using var pen = LinePen(color);
        // open book
        g.DrawLines(pen, new PointF[] { new(8f, 4.25f), new(5.5f, 3f), new(1.75f, 3f), new(1.75f, 12.25f), new(5.5f, 12.25f), new(8f, 13.5f) });
        g.DrawLines(pen, new PointF[] { new(8f, 4.25f), new(10.5f, 3f), new(14.25f, 3f), new(14.25f, 12.25f), new(10.5f, 12.25f), new(8f, 13.5f) });
        g.DrawLine(pen, 8f, 4.25f, 8f, 13.5f);
    }

    private static void DrawIndexIcon(Graphics g, Color color)
    {
        using var pen = LinePen(color);
        using var brush = new SolidBrush(color);
        for (int i = 0; i < 3; i++)
        {
            float y = 3.75f + i * 4.25f;
            g.FillEllipse(brush, 1.5f, y - 1.1f, 2.2f, 2.2f);
            g.DrawLine(pen, 6f, y, 14.25f, y);
        }
    }

    private static void DrawSearchIcon(Graphics g, Color color)
    {
        using var pen = LinePen(color, 1.6f);
        g.DrawEllipse(pen, 2f, 2f, 8.5f, 8.5f);
        using var thick = LinePen(color, 2f);
        g.DrawLine(thick, 9.6f, 9.6f, 13.75f, 13.75f);
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

    /// <summary>Draws a 16-grid icon painter into <paramref name="bounds"/>.</summary>
    private static void PaintIcon(Graphics g, Action<Graphics, Color> icon, RectangleF bounds, Color color)
    {
        var state = g.Save();
        try
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TranslateTransform(bounds.X, bounds.Y);
            g.ScaleTransform(bounds.Width / 16f, bounds.Height / 16f);
            icon(g, color);
        }
        finally
        {
            g.Restore(state);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // UiFont is static readonly — do not dispose
            ThemeManager.ThemeChanged -= OnThemeChanged;
            Application.RemoveMessageFilter(_resizeFilter);
            _treeImages.Dispose();
            _indexRowImages?.Dispose();
            _optionsMenu.Dispose();
        }
        base.Dispose(disposing);
    }

    // ───────────────────────── Local controls ─────────────────────────

    /// <summary>Topic tree without horizontal scroll bar (long titles: tooltip, wider sidebar).</summary>
    private sealed class SidebarTreeView : TreeView
    {
        private const int TVS_NOHSCROLL = 0x8000;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.Style |= TVS_NOHSCROLL;
                return cp;
            }
        }
    }

    /// <summary>
    /// Vertical navigation list in the look of <see cref="NavSidebar"/> (rounded highlight of
    /// the selected item, accent icon) with custom 16-grid icons. Keyboard: Up/Down/Home/End.
    /// </summary>
    private sealed class NavList : Control, IThemeAware
    {
        private readonly List<(string Text, Action<Graphics, Color> Icon)> _items = new();
        private int _selected = -1;
        private int _hover = -1;

        public NavList()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            AccessibleRole = AccessibleRole.PageTabList;
            AccessibleName = "Hilfe-Navigation";
        }

        public event EventHandler? SelectedIndexChanged;

        public void Add(string text, Action<Graphics, Color> icon)
        {
            _items.Add((text, icon));
            Invalidate();
        }

        public int SelectedIndex
        {
            get => _selected;
            set
            {
                if (value < 0 || value >= _items.Count || value == _selected) return;
                _selected = value;
                Invalidate();
                AccessibilityNotifyClients(AccessibleEvents.Selection, value);
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void ApplyTheme(ThemePalette palette)
        {
            BackColor = palette.Secondary;
            Invalidate();
        }

        private int S(float v) => ModernLayout.Scale(this, v);

        private Rectangle ItemBounds(int i) => new(S(10), S(10) + i * S(38), Math.Max(0, Width - S(20)), S(36));

        private int HitTest(Point pt)
        {
            for (int i = 0; i < _items.Count; i++)
                if (ItemBounds(i).Contains(pt)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hit = HitTest(e.Location);
            Cursor = hit >= 0 ? Cursors.Hand : Cursors.Default;
            if (hit != _hover) { _hover = hit; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hover != -1) { _hover = -1; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            int hit = HitTest(e.Location);
            if (hit >= 0)
            {
                Focus();
                SelectedIndex = hit;
            }
        }

        protected override bool IsInputKey(Keys keyData)
            => keyData is Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            int next = e.KeyCode switch
            {
                Keys.Up => Math.Max(0, _selected - 1),
                Keys.Down => Math.Min(_items.Count - 1, _selected + 1),
                Keys.Home => 0,
                Keys.End => _items.Count - 1,
                _ => -1
            };
            if (next >= 0)
            {
                SelectedIndex = next;
                e.Handled = true;
            }
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = ThemeManager.Current;
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            for (int i = 0; i < _items.Count; i++)
            {
                var r = ItemBounds(i);
                bool selected = i == _selected;
                bool hover = i == _hover;
                if (selected || hover)
                {
                    using var path = ModernLayout.RoundedRect(new RectangleF(r.X, r.Y, r.Width, r.Height), S(4));
                    using var brush = new SolidBrush(selected ? p.Selected : p.Hover);
                    g.FillPath(brush, path);
                }
                if (selected && Focused && ShowFocusCues)
                {
                    using var path = ModernLayout.RoundedRect(new RectangleF(r.X + 0.75f, r.Y + 0.75f, r.Width - 1.5f, r.Height - 1.5f), S(4));
                    using var pen = new Pen(Color.FromArgb(180, p.Accent), 1.5f);
                    g.DrawPath(pen, path);
                }

                Color fore = selected || hover ? p.Text : p.TextSecondary;
                Color icon = selected ? p.Accent : hover ? p.TextSecondary : p.TextMuted;
                int size = S(18);
                PaintIcon(g, _items[i].Icon, new RectangleF(r.X + S(10), r.Y + (r.Height - size) / 2f, size, size), icon);
                var textRect = new Rectangle(r.X + S(38), r.Y, Math.Max(0, r.Width - S(42)), r.Height);
                TextRenderer.DrawText(g, _items[i].Text, selected ? ThemeFonts.Semibold : ModernFonts.RowTitle, textRect, fore,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }
        }
    }

    /// <summary>Flat toolbar button (icon + text, rounded hover), not focusable like the former tool strip.</summary>
    private sealed class ToolButton : Control, IThemeAware
    {
        private readonly Action<Graphics, Color> _icon;
        private readonly ToolTip _toolTip = new();
        private bool _hover;
        private bool _pressed;
        private bool _menuOpen;

        public ToolButton(string text, Action<Graphics, Color> icon)
        {
            Text = text;
            AccessibleName = text;
            _icon = icon;
            AutoSize = true;
            Margin = new Padding(0, 0, 4, 0);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            AccessibleRole = AccessibleRole.PushButton;
        }

        /// <summary>Shows a chevron (button opens a menu).</summary>
        public bool DropDown { get; set; }

        /// <summary>Kept highlighted while its menu is open.</summary>
        public bool Pressed
        {
            get => _menuOpen;
            set { _menuOpen = value; Invalidate(); }
        }

        public string ToolTipText
        {
            get => _toolTip.GetToolTip(this) ?? "";
            set => _toolTip.SetToolTip(this, value);
        }

        public void ApplyTheme(ThemePalette palette) => Invalidate();

        private int S(float v) => ModernLayout.Scale(this, v);

        public override Size GetPreferredSize(Size proposedSize)
        {
            int textW = TextRenderer.MeasureText(Text, ThemeFonts.Regular, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
            int w = S(10) + S(16) + S(7) + textW + (DropDown ? S(4) + S(10) : 0) + S(11);
            return new Size(w, S(32));
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            AccessibleName = Text;
            Parent?.PerformLayout();
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = ThemeManager.Current;
            var g = e.Graphics;
            var back = ModernLayout.ParentBack(this);
            g.Clear(back);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            bool active = Enabled && (_hover || _pressed || _menuOpen);
            if (active)
            {
                using var path = ModernLayout.RoundedRect(new RectangleF(0, 0, Width - 0.5f, Height - 0.5f), S(6));
                using var brush = new SolidBrush(_pressed || _menuOpen ? p.Selected : p.Hover);
                g.FillPath(brush, path);
            }

            Color fore = Enabled ? (active ? p.Text : p.TextSecondary) : ModernLayout.Disabled(p.TextMuted, back);
            Color iconColor = Enabled ? (active ? p.Text : p.TextMuted) : fore;
            int icon = S(16);
            PaintIcon(g, _icon, new RectangleF(S(10), (Height - icon) / 2f, icon, icon), iconColor);

            int tx = S(10) + icon + S(7);
            int chevron = DropDown ? S(14) : 0;
            TextRenderer.DrawText(g, Text, ThemeFonts.Regular, new Rectangle(tx, 0, Math.Max(0, Width - tx - chevron - S(6)), Height), fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            if (DropDown)
            {
                int cs = S(10);
                ThemeGlyphs.Draw(g, Glyph.ChevronDown, new RectangleF(Width - S(10) - cs, (Height - cs) / 2f, cs, cs), iconColor);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _toolTip.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>Thin vertical separator of the toolbar.</summary>
    private sealed class ToolSeparator : Control, IThemeAware
    {
        public ToolSeparator()
        {
            Size = new Size(1, 20);
            Margin = new Padding(6, 6, 10, 6);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.Selectable, false);
        }

        public void ApplyTheme(ThemePalette palette) => Invalidate();

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = ThemeManager.Current;
            e.Graphics.Clear(p.IsDark ? p.Border : p.CardBorder);
        }
    }

    /// <summary>Round header button "maximize" / "restore" (same look as the other header buttons).</summary>
    private sealed class MaximizeButton : Control, IThemeAware
    {
        private bool _hover;
        private bool _pressed;
        private bool _restore;

        public MaximizeButton()
        {
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            AccessibleRole = AccessibleRole.PushButton;
        }

        /// <summary>Shows the "restore" symbol (window is maximized).</summary>
        public bool Restore
        {
            get => _restore;
            set { _restore = value; Invalidate(); }
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
            float s = DeviceDpi / 96f;

            Color fore = p.TextMuted;
            if (_hover || _pressed)
            {
                using var brush = new SolidBrush(_pressed ? p.Selected : p.IsDark ? p.ButtonSecondary : p.Hover);
                g.FillEllipse(brush, 0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
                fore = p.Text;
            }

            using var pen = new Pen(fore, 1.6f * s) { LineJoin = LineJoin.Round };
            float cx = Width / 2f, cy = Height / 2f;
            if (!_restore)
            {
                float h = 4.75f * s;
                using var path = ThemeGlyphs.Rounded(new RectangleF(cx - h, cy - h, 2 * h, 2 * h), 1.5f * s);
                g.DrawPath(pen, path);
            }
            else
            {
                float h = 3.75f * s, o = 1.75f * s;
                using (var back = new GraphicsPath())
                {
                    // back window: only the visible top/right edges
                    back.AddLines(new[]
                    {
                        new PointF(cx - h + o, cy - h - o + 0.5f * s),
                        new PointF(cx - h + o, cy - h - o),
                        new PointF(cx + h + o, cy - h - o),
                        new PointF(cx + h + o, cy + h - o),
                        new PointF(cx + h + o - 0.5f * s, cy + h - o)
                    });
                    g.DrawPath(pen, back);
                }
                using var front = ThemeGlyphs.Rounded(new RectangleF(cx - h - o * 0.5f, cy - h + o * 0.5f, 2 * h, 2 * h), 1.25f * s);
                g.DrawPath(pen, front);
            }
        }
    }
}
