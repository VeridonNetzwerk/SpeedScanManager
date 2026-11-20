using System.Drawing;

namespace SpeedScanManager;

/// <summary>Design selection of the user (persisted in <see cref="AppSettings.ThemeMode"/>).</summary>
internal enum ThemeMode
{
    /// <summary>Follow the Windows app mode (AppsUseLightTheme).</summary>
    System,
    Light,
    Dark
}

/// <summary>
/// Colors of one theme (Discord-inspired). All UI code reads colors from
/// <see cref="ThemeManager.Current"/> instead of hard-coded values.
/// </summary>
internal sealed class ThemePalette
{
    public bool IsDark { get; init; }

    /// <summary>Main content background (tab pages, dialogs).</summary>
    public Color Background { get; init; }
    /// <summary>Secondary surfaces: tool/button bars, cards, info banners.</summary>
    public Color Secondary { get; init; }
    /// <summary>Tertiary surfaces: sidebars, dark header zones.</summary>
    public Color Tertiary { get; init; }
    /// <summary>Header zone at the very top of a window (logo area).</summary>
    public Color Header { get; init; }
    /// <summary>Background of input fields (TextBox, ComboBox, ListBox).</summary>
    public Color Input { get; init; }
    /// <summary>Border of input fields (may equal <see cref="Input"/> in dark mode).</summary>
    public Color InputBorder { get; init; }
    public Color Hover { get; init; }
    public Color Selected { get; init; }
    /// <summary>Borders and divider lines.</summary>
    public Color Border { get; init; }
    public Color Text { get; init; }
    public Color TextSecondary { get; init; }
    public Color TextMuted { get; init; }
    public Color Accent { get; init; }
    public Color AccentHover { get; init; }
    public Color AccentPressed { get; init; }
    /// <summary>Text on accent/danger/success filled surfaces.</summary>
    public Color TextOnAccent { get; init; }
    public Color Success { get; init; }
    public Color SuccessHover { get; init; }
    public Color Warning { get; init; }
    public Color Danger { get; init; }
    public Color DangerHover { get; init; }
    public Color ButtonSecondary { get; init; }
    public Color ButtonSecondaryHover { get; init; }
    public Color ButtonSecondaryPressed { get; init; }
    public Color ButtonSecondaryText { get; init; }
    /// <summary>Background of context menus / dropdown menus.</summary>
    public Color Menu { get; init; }
    /// <summary>Thumb of sliders.</summary>
    public Color SliderThumb { get; init; }
    /// <summary>Settings cards (<see cref="SettingsCard"/>).</summary>
    public Color Card { get; init; }
    /// <summary>Outline of settings cards.</summary>
    public Color CardBorder { get; init; }
    /// <summary>Divider lines between the rows of a card.</summary>
    public Color CardDivider { get; init; }
    /// <summary>Fill of a <see cref="TileButton"/> placed on a card.</summary>
    public Color TileOnCard { get; init; }

    public static readonly ThemePalette Dark = new()
    {
        IsDark = true,
        Background = Hex(0x313338),
        Secondary = Hex(0x2B2D31),
        Tertiary = Hex(0x1E1F22),
        Header = Hex(0x1E1F22),
        Input = Hex(0x1E1F22),
        InputBorder = Hex(0x1E1F22),
        Hover = Hex(0x3A3C42),
        Selected = Hex(0x404249),
        Border = Hex(0x3F4147),
        Text = Hex(0xDBDEE1),
        TextSecondary = Hex(0xB5BAC1),
        TextMuted = Hex(0x949BA4),
        Accent = Hex(0x5865F2),
        AccentHover = Hex(0x4752C4),
        AccentPressed = Hex(0x3C45A5),
        TextOnAccent = Color.White,
        Success = Hex(0x23A55A),
        SuccessHover = Hex(0x1A8B4A),
        Warning = Hex(0xF0B232),
        Danger = Hex(0xDA373C),
        DangerHover = Hex(0xA12828),
        ButtonSecondary = Hex(0x4E5058),
        ButtonSecondaryHover = Hex(0x6D6F78),
        ButtonSecondaryPressed = Hex(0x80848E),
        ButtonSecondaryText = Color.White,
        Menu = Hex(0x111214),
        SliderThumb = Color.White,
        Card = Hex(0x2B2D31),
        CardBorder = Hex(0x3A3C42),
        CardDivider = Hex(0x393B41),
        TileOnCard = Hex(0x313338)
    };

    public static readonly ThemePalette Light = new()
    {
        IsDark = false,
        Background = Hex(0xFFFFFF),
        Secondary = Hex(0xF2F3F5),
        Tertiary = Hex(0xE3E5E8),
        Header = Hex(0xFFFFFF),
        Input = Hex(0xEBEDEF),
        InputBorder = Hex(0xD4D7DC),
        Hover = Hex(0xE8E9EC),
        Selected = Hex(0xD7D9DC),
        Border = Hex(0xD4D7DC),
        Text = Hex(0x313338),
        TextSecondary = Hex(0x4E5058),
        TextMuted = Hex(0x5C5E66),
        Accent = Hex(0x5865F2),
        AccentHover = Hex(0x4752C4),
        AccentPressed = Hex(0x3C45A5),
        TextOnAccent = Color.White,
        Success = Hex(0x23A55A),
        SuccessHover = Hex(0x1A8B4A),
        Warning = Hex(0xC98A0B),
        Danger = Hex(0xDA373C),
        DangerHover = Hex(0xA12828),
        ButtonSecondary = Hex(0xE3E5E8),
        ButtonSecondaryHover = Hex(0xD4D7DC),
        ButtonSecondaryPressed = Hex(0xC4C9CE),
        ButtonSecondaryText = Hex(0x313338),
        Menu = Hex(0xFFFFFF),
        SliderThumb = Color.White,
        Card = Hex(0xF7F8F9),
        CardBorder = Hex(0xE1E3E7),
        CardDivider = Hex(0xE6E8EB),
        TileOnCard = Hex(0xFFFFFF)
    };

    private static Color Hex(int rgb) => Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);

    /// <summary>Linear blend: <paramref name="amount"/> 0 = <paramref name="a"/>, 1 = <paramref name="b"/>.</summary>
    public static Color Blend(Color a, Color b, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            (int)Math.Round(a.R + (b.R - a.R) * amount),
            (int)Math.Round(a.G + (b.G - a.G) * amount),
            (int)Math.Round(a.B + (b.B - a.B) * amount));
    }
}
