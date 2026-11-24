using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SpeedScanManager;

/// <summary>
/// Optional styling hint for a control (see <see cref="ThemeManager.SetRole"/>). The meaning
/// depends on the control type; roles that don't fit a control are ignored.
/// </summary>
internal enum ThemeRole
{
    /// <summary>Default styling for the control type.</summary>
    Auto,
    /// <summary>Opt out: the control and its children are left untouched.</summary>
    None,

    // Buttons
    /// <summary>Button: accent filled (the form's AcceptButton gets it automatically).</summary>
    Primary,
    /// <summary>Button: neutral filled. Container: secondary surface (bars, banners).</summary>
    Secondary,
    /// <summary>Button: red filled (destructive). Label/menu item: red text.</summary>
    Danger,
    /// <summary>Button: green filled. Label: green text.</summary>
    Success,

    // Containers
    /// <summary>Container: header zone at the top of a window (logo area).</summary>
    Header,
    /// <summary>Container: main background.</summary>
    Surface,
    /// <summary>Container: tertiary surface (side bars).</summary>
    Sidebar,
    /// <summary>Panel: rounded card (secondary surface with border).</summary>
    Card,
    /// <summary>Panel/Label: 1 px divider line (Border color).</summary>
    Divider,

    // Text
    /// <summary>Label/CheckBox: secondary, muted text (hints).</summary>
    Muted,
    /// <summary>Label: section heading (semibold, slightly larger).</summary>
    Heading,
    /// <summary>Label: accent colored text.</summary>
    Accent,
    /// <summary>Label: warning colored text.</summary>
    Warning
}

/// <summary>Implemented by custom controls that style themselves.</summary>
internal interface IThemeAware
{
    void ApplyTheme(ThemePalette palette);
}

/// <summary>
/// Fonts of the themed UI: Segoe UI, or the Windows UI font of the language for Japanese,
/// Chinese and Korean (Segoe UI has no CJK characters; GDI+ text would show boxes).
/// </summary>
internal static class ThemeFonts
{
    /// <summary>Regular UI font family of the current language.</summary>
    public static string Family => L.Current.FontFamily;

    /// <summary>Semibold family (for CJK the regular family drawn bold).</summary>
    public static string SemiboldFamily => L.Current.SemiboldFamily;

    /// <summary>New regular UI font (caller owns it when not static).</summary>
    public static Font New(float size, GraphicsUnit unit = GraphicsUnit.Point) => new(Family, size, FontStyle.Regular, unit);

    /// <summary>New semibold UI font.</summary>
    public static Font NewSemibold(float size, GraphicsUnit unit = GraphicsUnit.Point) =>
        new(SemiboldFamily, size, L.Current.BoldForSemibold ? FontStyle.Bold : FontStyle.Regular, unit);

    public static readonly Font Regular = New(9f);
    public static readonly Font Semibold = NewSemibold(9f);
    public static readonly Font Heading = NewSemibold(10.5f);
    public static readonly Font Large = New(14f);

    private static readonly Dictionary<(string, float, FontStyle), Font> Cache = new();

    private static readonly HashSet<string> LegacyFamilies = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft Sans Serif", "MS Sans Serif", "Tahoma"
    };

    /// <summary>Maps legacy UI fonts (Microsoft Sans Serif 8.25 etc.) to Segoe UI; other fonts are returned unchanged.</summary>
    public static Font Map(Font font)
    {
        if (!LegacyFamilies.Contains(font.FontFamily.Name))
            return font;
        bool bold = font.Bold;
        var style = font.Style & ~FontStyle.Bold;
        float size = font.SizeInPoints <= 8.6f ? 9f : font.SizeInPoints;
        string family = bold ? SemiboldFamily : Family;
        if (bold && L.Current.BoldForSemibold) style |= FontStyle.Bold;
        if (style == FontStyle.Regular && size == 9f)
            return bold ? Semibold : Regular;
        if (style == FontStyle.Regular && size == 14f && !bold)
            return Large;
        return Get(family, size, style);
    }

    /// <summary>Semibold variant of a font (same size).</summary>
    public static Font SemiboldOf(Font font)
    {
        if (font.FontFamily.Name.Equals(SemiboldFamily, StringComparison.OrdinalIgnoreCase)
            && (!L.Current.BoldForSemibold || font.Bold))
            return font;
        if (font.SizeInPoints == 9f && font.Style == FontStyle.Regular)
            return Semibold;
        return Get(SemiboldFamily, font.SizeInPoints, L.Current.BoldForSemibold ? font.Style | FontStyle.Bold : font.Style & ~FontStyle.Bold);
    }

    private static Font Get(string family, float size, FontStyle style)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue((family, size, style), out var f))
            {
                f = new Font(family, size, style);
                Cache[(family, size, style)] = f;
            }
            return f;
        }
    }
}

/// <summary>Per-control theme bookkeeping (attached via a ConditionalWeakTable).</summary>
internal sealed class ThemeState
{
    public ThemeRole Role;
    public bool Hooked;
    public bool PaintHooked;
    public bool FontMapped;
    public bool PaddingAdjusted;
    public bool ItemsConverted;
    public Color? LastFore;
    public ThemeRole InferredTextRole;
    public bool Hover;
    public bool Pressed;
    public Glyph Glyph;
    public Glyph TrailingGlyph;
    public Color? GlyphColor;
    public bool? TintImage;
    public Image? TintSource;
    public Color TintColor;
    public Bitmap? Tinted;
    public NativeWindow? Subclass;
}

/// <summary>
/// Central theme service (light/dark, Discord-inspired).
///
/// Usage: call <see cref="Apply(Control)"/> once for a form (end of the constructor); all
/// standard controls in it — and controls added later — are styled, and the form is
/// re-styled automatically when the theme changes. Optional hints via
/// <see cref="SetRole"/> / <see cref="SetGlyph"/>. Custom paint code reads colors from
/// <see cref="Current"/> and listens to <see cref="ThemeChanged"/> if it caches anything.
/// All members must be used on the UI thread.
/// </summary>
internal static class ThemeManager
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private static ThemeMode _mode = ThemeMode.System;
    private static bool _systemDark;
    private static bool _systemEventsHooked;
    private static bool _systemDarkRead;
    private static SynchronizationContext? _sync;
    private static readonly List<WeakReference<Control>> Roots = new();
    private static readonly List<WeakReference<ToolStrip>> Strips = new();
    private static readonly ConditionalWeakTable<Control, ThemeState> States = new();
    private static readonly ConditionalWeakTable<ToolStripItem, StrongBox<ThemeRole>> ItemRoles = new();
    private static readonly ConditionalWeakTable<ToolStripItem, object> ItemsStyled = new();
    private static readonly ConditionalWeakTable<ToolStripItem, StrongBox<Glyph>> ItemGlyphs = new();
    private static readonly ThemedToolStripRenderer Renderer = new();

    /// <summary>The user's choice (System/Light/Dark).</summary>
    public static ThemeMode Mode => _mode;

    /// <summary>The effective palette.</summary>
    public static ThemePalette Current { get; private set; } = ThemePalette.Light;

    public static bool IsDark => Current.IsDark;

    /// <summary>The effective palette changed (mode changed or Windows switched light/dark). Raised after all registered forms were re-styled.</summary>
    public static event EventHandler? ThemeChanged;

    /// <summary><see cref="Mode"/> changed (persist it).</summary>
    public static event EventHandler? ModeChanged;

    /// <summary>Sets the mode and starts following the Windows setting (call once at startup on the UI thread).</summary>
    public static void Initialize(ThemeMode mode)
    {
        if (!_systemEventsHooked)
        {
            _systemEventsHooked = true;
            _sync = SynchronizationContext.Current;
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }
        _mode = Enum.IsDefined(mode) ? mode : ThemeMode.System;
        _systemDark = ReadSystemDark();
        _systemDarkRead = true;
        UpdateEffective(force: true);
    }

    /// <summary>Stops listening to Windows setting changes (app exit).</summary>
    public static void Shutdown()
    {
        if (!_systemEventsHooked) return;
        _systemEventsHooked = false;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    /// <summary>Changes the mode; raises <see cref="ModeChanged"/> and, if the palette changes, re-styles everything.</summary>
    public static void SetMode(ThemeMode mode)
    {
        if (!Enum.IsDefined(mode)) mode = ThemeMode.System;
        if (mode == _mode && _systemDarkRead) return;
        bool changed = mode != _mode;
        _mode = mode;
        if (changed) ModeChanged?.Invoke(null, EventArgs.Empty);
        UpdateEffective(force: false);
    }

    /// <summary>German display name of a mode.</summary>
    public static string GetModeName(ThemeMode mode) => mode switch
    {
        ThemeMode.Light => L.T("Hell"),
        ThemeMode.Dark => L.T("Dunkel"),
        _ => L.T("System")
    };

    private static bool ReadSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle or UserPreferenceCategory.Color))
            return;

        void Update()
        {
            bool dark = ReadSystemDark();
            if (dark == _systemDark) return;
            _systemDark = dark;
            if (_mode == ThemeMode.System)
                UpdateEffective(force: false);
        }

        if (_sync != null && SynchronizationContext.Current != _sync)
            _sync.Post(_ => Update(), null);
        else
            Update();
    }

    private static void UpdateEffective(bool force)
    {
        if (!_systemDarkRead)
        {
            _systemDark = ReadSystemDark();
            _systemDarkRead = true;
        }
        bool dark = _mode == ThemeMode.Dark || (_mode == ThemeMode.System && _systemDark);
        var palette = dark ? ThemePalette.Dark : ThemePalette.Light;
        if (!force && ReferenceEquals(palette, Current))
            return;
        Current = palette;
        ThemeNative.SetAppDarkMode(dark);
        ReapplyAll();
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    private static void ReapplyAll()
    {
        foreach (var root in Alive(Roots))
        {
            root.SuspendLayout();
            try { ApplyTree(root); }
            finally { root.ResumeLayout(true); }
            root.Invalidate(true);
        }
        foreach (var strip in Alive(Strips))
        {
            ApplyToolStripCore(strip);
            strip.Invalidate();
        }
    }

    private static List<T> Alive<T>(List<WeakReference<T>> list) where T : class
    {
        var result = new List<T>();
        list.RemoveAll(w => !w.TryGetTarget(out var t) || (t is Control c && c.IsDisposed));
        foreach (var w in list)
            if (w.TryGetTarget(out var t)) result.Add(t);
        return result;
    }

    // ───────────────────────── Public styling API ─────────────────────────

    /// <summary>
    /// Styles <paramref name="root"/> (typically a Form) and all child controls, and keeps
    /// them styled: children added later and theme changes are handled automatically.
    /// Safe to call more than once.
    /// </summary>
    public static void Apply(Control root)
    {
        if (!_systemDarkRead) UpdateEffective(force: true);
        if (!Roots.Any(w => w.TryGetTarget(out var t) && ReferenceEquals(t, root)))
        {
            Roots.Add(new WeakReference<Control>(root));
            root.Disposed += (_, _) => Roots.RemoveAll(w => !w.TryGetTarget(out var t) || ReferenceEquals(t, root));
        }
        ApplyTree(root);
    }

    /// <summary>Styles a menu/tool strip (e.g. the tray ContextMenuStrip) including all drop-downs.</summary>
    public static void Apply(ToolStrip strip)
    {
        if (!_systemDarkRead) UpdateEffective(force: true);
        if (!Strips.Any(w => w.TryGetTarget(out var t) && ReferenceEquals(t, strip)))
        {
            Strips.Add(new WeakReference<ToolStrip>(strip));
            strip.Disposed += (_, _) => Strips.RemoveAll(w => !w.TryGetTarget(out var t) || ReferenceEquals(t, strip));
        }
        ApplyToolStripCore(strip);
    }

    /// <summary>Sets a styling role; takes effect immediately if the control is already styled.</summary>
    public static void SetRole(Control control, ThemeRole role)
    {
        var st = GetState(control);
        if (st.Role == role) return;
        st.Role = role;
        st.LastFore = null;
        if (st.Hooked && role != ThemeRole.None)
        {
            ApplyControl(control, st);
            control.Invalidate();
        }
    }

    public static ThemeRole GetRole(Control control) => GetState(control).Role;

    /// <summary>Shows a monochrome vector glyph on a button (drawn in the button's text color; replaces Image).</summary>
    public static void SetGlyph(ButtonBase button, Glyph glyph)
    {
        GetState(button).Glyph = glyph;
        button.Invalidate();
    }

    /// <summary>
    /// Shows a colored vector glyph on a (secondary) button; on Primary/Danger/Success buttons
    /// and when disabled the glyph follows the text color.
    /// </summary>
    public static void SetGlyph(ButtonBase button, Glyph glyph, Color color)
    {
        var st = GetState(button);
        st.Glyph = glyph;
        st.GlyphColor = color;
        button.Invalidate();
    }

    /// <summary>
    /// Shows a small glyph after the button text (e.g. a chevron on buttons that open a
    /// sub-dialog), drawn in a muted variant of the text color.
    /// </summary>
    public static void SetTrailingGlyph(ButtonBase button, Glyph glyph)
    {
        GetState(button).TrailingGlyph = glyph;
        button.Invalidate();
    }

    /// <summary>
    /// Whether a button's Image is drawn as a monochrome glyph in the text color
    /// (default: yes for images up to 24 px, no for larger pictures).
    /// </summary>
    public static void SetImageTint(ButtonBase button, bool tint)
    {
        GetState(button).TintImage = tint;
        button.Invalidate();
    }

    /// <summary>Role of a menu item: <see cref="ThemeRole.Danger"/> = red text / red highlight.</summary>
    public static void SetMenuItemRole(ToolStripItem item, ThemeRole role)
    {
        ItemRoles.AddOrUpdate(item, new StrongBox<ThemeRole>(role));
        item.Invalidate();
    }

    internal static ThemeRole GetMenuItemRole(ToolStripItem item)
        => ItemRoles.TryGetValue(item, out var box) ? box.Value : ThemeRole.Auto;

    /// <summary>
    /// Line glyph shown in the image margin of a menu item, drawn in the item's text color
    /// (the drop-down needs ShowImageMargin = true).
    /// </summary>
    public static void SetMenuItemGlyph(ToolStripItem item, Glyph glyph)
    {
        ItemGlyphs.AddOrUpdate(item, new StrongBox<Glyph>(glyph));
        item.Invalidate();
    }

    internal static Glyph GetMenuItemGlyph(ToolStripItem item)
        => ItemGlyphs.TryGetValue(item, out var box) ? box.Value : Glyph.None;

    /// <summary>
    /// For owner-drawn ComboBox/ListBox items: fills the item background (themed, no system
    /// highlight) and returns the text color to use. <paramref name="itemEnabled"/> false grays the item.
    /// </summary>
    public static Color DrawItemBackground(Control owner, DrawItemEventArgs e, bool itemEnabled = true)
    {
        var p = Current;
        bool edit = (e.State & DrawItemState.ComboBoxEdit) != 0;
        bool selected = !edit && (e.State & DrawItemState.Selected) != 0;
        var bg = selected ? p.Selected : owner.BackColor;
        using (var brush = new SolidBrush(owner.BackColor))
            e.Graphics.FillRectangle(brush, e.Bounds);
        if (selected)
        {
            // Rounded highlight inset from the list edges (Win11 / Discord list style)
            var state = e.Graphics.Save();
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            float inset = Scale(owner, 3);
            var r = new RectangleF(e.Bounds.X + inset, e.Bounds.Y + 1, e.Bounds.Width - 2 * inset, e.Bounds.Height - 2);
            using (var path = ThemeGlyphs.Rounded(r, Scale(owner, 4)))
            using (var brush = new SolidBrush(bg))
                e.Graphics.FillPath(brush, path);
            e.Graphics.Restore(state);
        }
        if (!owner.Enabled || !itemEnabled)
            return ThemePalette.Blend(p.TextMuted, bg, 0.25f);
        return p.Text;
    }

    /// <summary>Converts a 96-dpi value to device pixels of <paramref name="control"/>.</summary>
    public static int Scale(Control control, float value) => (int)Math.Round(value * control.DeviceDpi / 96f);

    internal static ThemeState GetState(Control control) => States.GetValue(control, _ => new ThemeState());

    // ───────────────────────── Tree walk ─────────────────────────

    private static void ApplyTree(Control c)
    {
        var st = GetState(c);
        if (st.Role == ThemeRole.None || c is WebBrowserBase)
            return;

        ApplyControl(c, st);
        if (!st.Hooked)
        {
            st.Hooked = true;
            c.ControlAdded += (_, e) =>
            {
                if (e.Control != null && GetState(c).Role != ThemeRole.None)
                    ApplyTree(e.Control);
            };
            c.HandleCreated += (_, _) => ApplyNative(c);
        }
        if (c.IsHandleCreated)
            ApplyNative(c);

        if (c.ContextMenuStrip != null)
            Apply(c.ContextMenuStrip);

        if (c is ToolStrip or UpDownBase or ComboBox or TextBoxBase or TrackBar)
            return;
        foreach (Control child in c.Controls)
            ApplyTree(child);
    }

    private static void ApplyControl(Control c, ThemeState st)
    {
        var p = Current;
        if (!st.FontMapped)
        {
            st.FontMapped = true;
            var mapped = ThemeFonts.Map(c.Font);
            // A form scaled by font would re-layout itself; only map fonts set on its children.
            if (!ReferenceEquals(mapped, c.Font) && !(c is ContainerControl cc && cc.AutoScaleMode == AutoScaleMode.Font))
                c.Font = mapped;
        }

        switch (c)
        {
            case IThemeAware aware:
                aware.ApplyTheme(p);
                break;

            case Form form:
                form.BackColor = SurfaceColor(st.Role, p) ?? p.Background;
                form.ForeColor = p.Text;
                break;

            case TabPage page:
                page.UseVisualStyleBackColor = false;
                page.BackColor = SurfaceColor(st.Role, p) ?? p.Background;
                page.ForeColor = p.Text;
                break;

            case TabControl tabs:
                st.Subclass ??= new TabControlSubclass(tabs);
                tabs.Invalidate();
                break;

            case SplitContainer split:
                split.BackColor = p.Border;
                break;

            case SplitterPanel splitPanel:
                splitPanel.BackColor = SurfaceColor(st.Role, p) ?? splitPanel.Parent?.Parent?.BackColor ?? p.Background;
                break;

            case GroupBox group:
                ApplySurface(group, st, p);
                group.ForeColor = p.Text;
                HookPaint(group, st, (s, e) => ThemePainters.PaintGroupBox((GroupBox)s!, e.Graphics));
                break;

            case LinkLabel link:
                ApplyLabel(link, st, p);
                link.LinkColor = p.Accent;
                link.ActiveLinkColor = p.AccentHover;
                link.VisitedLinkColor = p.Accent;
                link.DisabledLinkColor = p.TextMuted;
                break;

            case Label label:
                ApplyLabel(label, st, p);
                break;

            case CheckBox check when check.Appearance == Appearance.Normal:
                ApplyCheck(check, st, p);
                break;

            case RadioButton radio when radio.Appearance == Appearance.Normal:
                ApplyCheck(radio, st, p);
                break;

            case ButtonBase button:
                ApplyButton(button, st, p);
                break;

            case TextBoxBase text:
                text.BackColor = p.Input;
                text.ForeColor = p.Text;
                if (st.Subclass == null && text is TextBox { BorderStyle: not BorderStyle.None, Multiline: false } single)
                    st.Subclass = new TextBoxSubclass(single);
                else if (st.Subclass == null && text is TextBox { BorderStyle: not BorderStyle.None } or RichTextBox { BorderStyle: not BorderStyle.None })
                    st.Subclass = new BorderSubclass(text, focusAccent: true);
                ThemePainters.RedrawFrame(text);
                break;

            case UpDownBase upDown:
                upDown.BackColor = p.Input;
                upDown.ForeColor = p.Text;
                HookPaint(upDown, st, (s, e) => ThemePainters.PaintUpDownBorder((UpDownBase)s!, e.Graphics));
                st.Subclass ??= new UpDownSubclass(upDown);
                (st.Subclass as UpDownSubclass)?.InvalidateButtons();
                upDown.Invalidate();
                break;

            case ComboBox combo:
                ApplyCombo(combo, st, p);
                break;

            case ListBox list:
                list.BackColor = p.Input;
                list.ForeColor = p.Text;
                if (!st.ItemsConverted && list.DrawMode == DrawMode.Normal && list is not CheckedListBox)
                {
                    st.ItemsConverted = true;
                    list.DrawMode = DrawMode.OwnerDrawFixed;
                    list.ItemHeight = Math.Max(list.ItemHeight, list.Font.Height + Scale(list, 5));
                    list.DrawItem += ThemePainters.DrawListItemDefault;
                }
                if (st.Subclass == null && list.BorderStyle != BorderStyle.None)
                    st.Subclass = new BorderSubclass(list, focusAccent: false);
                ThemePainters.RedrawFrame(list);
                list.Invalidate();
                break;

            case ListView listView:
                listView.BackColor = p.Secondary;
                listView.ForeColor = p.Text;
                st.Subclass ??= new ListViewSubclass(listView);
                ThemePainters.RedrawFrame(listView);
                break;

            case TreeView tree:
                tree.BackColor = p.Secondary;
                tree.ForeColor = p.Text;
                tree.LineColor = p.Border;
                if (st.Subclass == null && tree.BorderStyle != BorderStyle.None)
                    st.Subclass = new BorderSubclass(tree, focusAccent: false);
                ThemePainters.RedrawFrame(tree);
                break;

            case TrackBar track:
                track.BackColor = track.Parent?.BackColor ?? p.Background;
                if (track.Orientation == Orientation.Horizontal)
                    st.Subclass ??= new TrackBarSubclass(track);
                track.Invalidate();
                break;

            case ToolStrip strip:
                Apply(strip);
                break;

            case PictureBox picture:
                ApplySurface(picture, st, p);
                break;

            case ProgressBar:
                break;

            case Panel or ContainerControl or SplitContainer:
                ApplySurface(c, st, p);
                break;
        }
    }

    /// <summary>Color for container roles, or null for "inherit".</summary>
    private static Color? SurfaceColor(ThemeRole role, ThemePalette p) => role switch
    {
        ThemeRole.Header => p.Header,
        ThemeRole.Secondary => p.Secondary,
        ThemeRole.Sidebar => p.Tertiary,
        ThemeRole.Surface => p.Background,
        ThemeRole.Card => p.Secondary,
        ThemeRole.Divider => p.Border,
        _ => null
    };

    /// <summary>Surface color of a container role set on <paramref name="c"/>, or null.</summary>
    internal static Color? SurfaceColorFor(Control c) => SurfaceColor(GetState(c).Role, Current);

    private static void ApplySurface(Control c, ThemeState st, ThemePalette p)
    {
        var color = SurfaceColor(st.Role, p);
        if (color == null && st.Role == ThemeRole.Auto && IsDividerLike(c))
            color = p.Border;

        if (color != null)
            c.BackColor = color.Value;
        else if (c.BackColor != Color.Transparent)
            c.ResetBackColor();
        c.ResetForeColor();

        if (st.Role == ThemeRole.Card)
            HookPaint(c, st, (s, e) => ThemePainters.PaintCard((Control)s!, e.Graphics));
        if (c is ScrollableControl { AutoScroll: true })
            c.Invalidate();
    }

    /// <summary>An empty, thin panel with its own color is a separator line.</summary>
    private static bool IsDividerLike(Control c)
        => c.Controls.Count == 0
           && (c.Height <= 3 || c.Width <= 3)
           && IsExplicit(c, nameof(Control.BackColor))
           && c.BackColor != Color.Transparent;

    private static bool IsExplicit(Control c, string property)
    {
        try
        {
            return TypeDescriptor.GetProperties(c)[property]?.ShouldSerializeValue(c) ?? false;
        }
        catch
        {
            return false;
        }
    }

    private static void ApplyLabel(Label label, ThemeState st, ThemePalette p)
    {
        var surface = SurfaceColor(st.Role, p);
        if (surface != null && st.Role != ThemeRole.Divider)
            label.BackColor = surface.Value;
        else if (st.Role == ThemeRole.Divider)
            label.BackColor = p.Border;
        else if (label.BackColor != Color.Transparent)
            label.ResetBackColor();

        if (st.Role == ThemeRole.Heading && !ReferenceEquals(label.Font, ThemeFonts.Heading))
            label.Font = ThemeFonts.Heading;
        label.ForeColor = TextColor(label, st, p);
        // Disabled text: themed instead of the system's etched gray
        HookPaint(label, st, (s, e) => ThemePainters.PaintDisabledLabel((Label)s!, e.Graphics));
    }

    /// <summary>Text color from the role, or inferred from the color the code had set.</summary>
    private static Color TextColor(Control c, ThemeState st, ThemePalette p)
    {
        ThemeRole role = st.Role;
        if (role is not (ThemeRole.Muted or ThemeRole.Accent or ThemeRole.Danger or ThemeRole.Success or ThemeRole.Warning))
        {
            if (st.LastFore != null && c.ForeColor == st.LastFore.Value)
                role = st.InferredTextRole;
            else if (st.LastFore == null && !IsExplicit(c, nameof(Control.ForeColor)))
                role = ThemeRole.Auto;
            else
                role = InferTextRole(c.ForeColor);
            st.InferredTextRole = role;
        }

        var color = role switch
        {
            ThemeRole.Muted => p.TextMuted,
            ThemeRole.Accent => p.Accent,
            ThemeRole.Danger => p.Danger,
            ThemeRole.Success => p.Success,
            ThemeRole.Warning => p.Warning,
            _ => p.Text
        };
        st.LastFore = color;
        return color;
    }

    private static ThemeRole InferTextRole(Color color)
    {
        if (color.IsEmpty || color.A == 0) return ThemeRole.Auto;
        if (color.IsSystemColor)
            return color == SystemColors.GrayText ? ThemeRole.Muted : ThemeRole.Auto;
        int r = color.R, g = color.G, b = color.B;
        int max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        if (max - min > 50)
        {
            if (r >= g && r >= b) return r - b > 60 && g > 150 ? ThemeRole.Warning : ThemeRole.Danger;
            if (g >= r && g >= b) return ThemeRole.Success;
            return ThemeRole.Accent;
        }
        double lum = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
        return lum is > 0.25 and < 0.8 ? ThemeRole.Muted : ThemeRole.Auto;
    }

    private static void ApplyButton(ButtonBase button, ThemeState st, ThemePalette p)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.UseVisualStyleBackColor = false;
        button.BackColor = button.Parent?.BackColor ?? p.Background;
        button.ForeColor = p.Text;
        if (!st.PaintHooked)
        {
            SetDoubleBuffered(button);
            button.MouseEnter += (_, _) => { st.Hover = true; button.Invalidate(); };
            button.MouseLeave += (_, _) => { st.Hover = false; st.Pressed = false; button.Invalidate(); };
            button.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { st.Pressed = true; button.Invalidate(); } };
            button.MouseUp += (_, _) => { st.Pressed = false; button.Invalidate(); };
            button.EnabledChanged += (_, _) => { st.Hover = false; st.Pressed = false; button.Invalidate(); };
            button.ParentChanged += (_, _) => { if (button.Parent != null) button.BackColor = button.Parent.BackColor; };
        }
        HookPaint(button, st, (s, e) => ThemePainters.PaintButton((ButtonBase)s!, e.Graphics, GetState((ButtonBase)s!)));
        button.Invalidate();
    }

    private static void ApplyCheck(ButtonBase check, ThemeState st, ThemePalette p)
    {
        if (check.BackColor != Color.Transparent)
            check.ResetBackColor();
        check.ForeColor = TextColor(check, st, p);
        if (!st.PaddingAdjusted)
        {
            st.PaddingAdjusted = true;
            // The themed glyph (16 px + 6 px gap) is wider than the system glyph.
            var pad = check.Padding;
            check.Padding = new Padding(pad.Left + Scale(check, 5), pad.Top, pad.Right, pad.Bottom);
        }
        if (!st.PaintHooked)
        {
            SetDoubleBuffered(check);
            check.MouseEnter += (_, _) => { st.Hover = true; check.Invalidate(); };
            check.MouseLeave += (_, _) => { st.Hover = false; check.Invalidate(); };
        }
        HookPaint(check, st, (s, e) => ThemePainters.PaintCheck((ButtonBase)s!, e.Graphics, GetState((ButtonBase)s!)));
        check.Invalidate();
    }

    private static void ApplyCombo(ComboBox combo, ThemeState st, ThemePalette p)
    {
        combo.BackColor = p.Input;
        combo.ForeColor = p.Text;
        if (combo.FlatStyle != FlatStyle.Standard)
            combo.FlatStyle = FlatStyle.Standard;
        if (!st.ItemsConverted && combo.DropDownStyle == ComboBoxStyle.DropDownList && combo.DrawMode == DrawMode.Normal)
        {
            st.ItemsConverted = true;
            combo.DrawMode = DrawMode.OwnerDrawFixed;
            combo.ItemHeight = Math.Max(combo.ItemHeight, combo.Font.Height + Scale(combo, 3));
            combo.DrawItem += ThemePainters.DrawComboItemDefault;
        }
        st.Subclass ??= new ComboSubclass(combo);
        ComboLists.Attach(combo);
        combo.Invalidate();
    }

    private static void HookPaint(Control c, ThemeState st, PaintEventHandler handler)
    {
        if (st.PaintHooked) return;
        st.PaintHooked = true;
        c.Paint += handler;
    }

    private static readonly PropertyInfo? DoubleBufferedProperty =
        typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);

    private static void SetDoubleBuffered(Control c)
    {
        try { DoubleBufferedProperty?.SetValue(c, true); }
        catch { }
    }

    /// <summary>Native parts that need a window handle (re-run when the handle is recreated).</summary>
    private static void ApplyNative(Control c)
    {
        if (!c.IsHandleCreated) return;
        bool dark = Current.IsDark;
        switch (c)
        {
            case Form form:
                ThemeNative.ApplyTitleBar(form.Handle, Current);
                break;
            case ComboBox combo:
                ThemePainters.ApplyComboNative(combo, dark);
                break;
            case ListView listView:
                ThemeNative.ApplyExplorerTheme(listView.Handle, dark);
                ThemePainters.ApplyListViewHeaderTheme(listView, dark);
                break;
            case TreeView or ListBox:
                ThemeNative.ApplyExplorerTheme(c.Handle, dark);
                break;
            case TextBoxBase { Multiline: true }:
                ThemeNative.ApplyExplorerTheme(c.Handle, dark);
                break;
            case ScrollableControl { AutoScroll: true }:
                ThemeNative.ApplyExplorerTheme(c.Handle, dark);
                break;
        }
    }

    // ───────────────────────── Tool strips ─────────────────────────

    private static void ApplyToolStripCore(ToolStrip strip)
    {
        var p = Current;
        strip.Renderer = Renderer;
        strip.BackColor = strip is ToolStripDropDown ? p.Menu : p.Secondary;
        strip.ForeColor = p.Text;
        if (!ReferenceEquals(ThemeFonts.Map(strip.Font), strip.Font))
            strip.Font = ThemeFonts.Map(strip.Font);

        var st = GetState(strip);
        if (!st.Hooked)
        {
            st.Hooked = true;
            strip.ItemAdded += (_, e) => { if (e.Item != null) ApplyItem(e.Item); };
            if (strip is ToolStripDropDown dropDown)
            {
                dropDown.Padding = new Padding(dropDown.Padding.Left, Scale(dropDown, 6), dropDown.Padding.Right, Scale(dropDown, 6));
                dropDown.HandleCreated += (_, _) => ThemeNative.ApplyPopupCorners(dropDown.Handle, Current);
                dropDown.Opening += (_, _) => { if (dropDown.IsHandleCreated) ThemeNative.ApplyPopupCorners(dropDown.Handle, Current); };
            }
        }
        if (strip is ToolStripDropDown dd && dd.IsHandleCreated)
            ThemeNative.ApplyPopupCorners(dd.Handle, p);

        foreach (ToolStripItem item in strip.Items)
            ApplyItem(item);
    }

    private static void ApplyItem(ToolStripItem item)
    {
        item.ForeColor = Current.Text;
        if (!ItemsStyled.TryGetValue(item, out _))
        {
            ItemsStyled.Add(item, new object());
            if (item.Owner is ToolStripDropDownMenu && item is ToolStripMenuItem)
            {
                int v = item.Owner != null ? Scale(item.Owner, 4) : 4;
                item.Padding = new Padding(item.Padding.Left, v, item.Padding.Right, v);
            }
        }
        if (item is ToolStripDropDownItem dropItem && dropItem.HasDropDownItems)
            ApplyToolStripCore(dropItem.DropDown);
    }
}
