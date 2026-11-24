using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace SpeedScanInstaller;

/// <summary>
/// Translations of the setup and the uninstaller. German is the source language: texts are
/// written in German (in XAML and code) and looked up in the embedded "lang.&lt;code&gt;.json"
/// (German text → translation); a missing entry stays German.
///
/// XAML texts are translated by <see cref="Apply"/>, which walks a window after
/// InitializeComponent and remembers the German originals, so the language can be switched
/// while the setup is open.
/// </summary>
internal static class I18n
{
    public sealed record Language(string Code, string NativeName);

    /// <summary>Same list and order as the app (Core/Localization/L.cs).</summary>
    public static readonly IReadOnlyList<Language> Languages = new Language[]
    {
        new("de-DE", "Deutsch"),
        new("en-US", "English"),
        new("ja-JP", "日本語"),
        new("fr-FR", "Français"),
        new("es-ES", "Español"),
        new("zh-CN", "简体中文"),
        new("zh-TW", "繁體中文"),
        new("pt-BR", "Português (Brasil)"),
        new("ru-RU", "Русский"),
        new("ko-KR", "한국어"),
        new("it-IT", "Italiano"),
        new("pl-PL", "Polski"),
        new("nl-NL", "Nederlands"),
        new("tr-TR", "Türkçe"),
    };

    /// <summary>File next to the installed program with the language chosen in the setup.</summary>
    public const string LanguageFileName = "language.txt";

    private static Dictionary<string, string>? _map;

    public static Language Current { get; private set; } = Languages[0];

    public static event Action? Changed;

    public static void Set(string? code)
    {
        Current = Find(code) ?? Languages[1];
        _map = Current.Code == "de-DE" ? null : Load(Current.Code);
        try
        {
            var culture = CultureInfo.GetCultureInfo(Current.Code);
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }
        catch (CultureNotFoundException) { }
        Changed?.Invoke();
    }

    /// <summary>
    /// Language the setup starts in: the app's own setting, the language of the existing
    /// installation, otherwise the Windows display language.
    /// </summary>
    public static string Initial(string? installDir)
    {
        var fromSettings = Find(ReadAppSetting())?.Code;
        if (fromSettings != null) return fromSettings;
        if (installDir != null)
        {
            try
            {
                var file = Path.Combine(installDir, LanguageFileName);
                if (File.Exists(file) && Find(File.ReadAllText(file).Trim()) is { } installed)
                    return installed.Code;
            }
            catch { }
        }
        return Match(CultureInfo.CurrentUICulture.Name);
    }

    /// <summary>"Language" in %AppData%\SpeedScanManager\settings.json, if the user set one in the app.</summary>
    private static string? ReadAppSetting()
    {
        try
        {
            var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SpeedScanManager", "settings.json");
            if (!File.Exists(file)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            return doc.RootElement.TryGetProperty("Language", out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() : null;
        }
        catch { return null; }
    }

    public static string T(string german) =>
        _map != null && _map.TryGetValue(german, out var text) && text.Length > 0 ? text : german;

    public static string F(string german, params object?[] args)
    {
        try { return string.Format(CultureInfo.CurrentCulture, T(german), args); }
        catch (FormatException) { return string.Format(CultureInfo.CurrentCulture, german, args); }
    }

    public static Language? Find(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null
        : Languages.FirstOrDefault(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

    public static string Match(string cultureName)
    {
        if (Find(cultureName) is { } exact) return exact.Code;
        var lower = cultureName.ToLowerInvariant();
        if (lower.StartsWith("zh"))
            return lower.Contains("hant") || lower.EndsWith("-tw") || lower.EndsWith("-hk") || lower.EndsWith("-mo") ? "zh-TW" : "zh-CN";
        var two = lower.Split('-')[0];
        return Languages.FirstOrDefault(l => l.Code.StartsWith(two + "-", StringComparison.OrdinalIgnoreCase))?.Code ?? "en-US";
    }

    private static Dictionary<string, string>? Load(string code)
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"lang.{code}.json");
            return stream == null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
        }
        catch { return null; }
    }

    // ===== XAML texts =====

    /// <summary>German original of every translated property, per element.</summary>
    private static readonly ConditionalWeakTable<DependencyObject, Dictionary<DependencyProperty, (string German, string Shown)>> Originals = new();

    /// <summary>Translates Text, Content, ToolTip and Title of <paramref name="root"/> and everything below it.</summary>
    public static void Apply(DependencyObject root)
    {
        Translate(root);
        foreach (var child in LogicalTreeHelper.GetChildren(root))
            if (child is DependencyObject d)
                Apply(d);
    }

    private static void Translate(DependencyObject element)
    {
        switch (element)
        {
            case TextBlock tb: Swap(tb, TextBlock.TextProperty); break;
            case Window w:
                Swap(w, Window.TitleProperty);
                // Right glyph shapes for Chinese / Japanese / Korean in the font fallback
                w.Language = System.Windows.Markup.XmlLanguage.GetLanguage(Current.Code);
                break;
            case ContentControl cc when cc.Content is string || Originals.TryGetValue(cc, out _):
                Swap(cc, ContentControl.ContentProperty); break;
        }
        if (element is FrameworkElement fe && (fe.ToolTip is string || Originals.TryGetValue(fe, out _)))
            Swap(fe, FrameworkElement.ToolTipProperty);
    }

    private static void Swap(DependencyObject element, DependencyProperty property)
    {
        // Only local values: texts from bindings or dynamic resources are left alone
        var local = element.ReadLocalValue(property);
        var originals = Originals.GetOrCreateValue(element);
        string german;
        if (originals.TryGetValue(property, out var known) && Equals(local, known.Shown))
        {
            german = known.German;
        }
        else
        {
            // First time, or set from code meanwhile (then that German text is the new original;
            // code that sets translated texts itself is re-run by the window on a language change)
            if (local is not string s || s.Length == 0) return;
            german = s;
        }
        var shown = T(german);
        originals[property] = (german, shown);
        element.SetValue(property, shown);
    }
}
