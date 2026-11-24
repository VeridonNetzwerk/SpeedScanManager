using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace SpeedScanManager;

/// <summary>
/// Translations. German is the source language: every visible text is written in German in the
/// code and passed through <see cref="T"/>, which looks it up in the translation file of the
/// current language (embedded "lang/&lt;code&gt;.json": German text → translation). A missing
/// entry falls back to the German text, so nothing ever shows empty.
///
/// The language is fixed for the app's lifetime (chosen in <c>Program.Main</c> before any window
/// or static text exists); changing it in the settings restarts the app.
/// </summary>
internal static class L
{
    public sealed record Language(string Code, string NativeName, string FontFamily, string SemiboldFamily, bool BoldForSemibold);

    private const string Segoe = "Segoe UI", SegoeSemibold = "Segoe UI Semibold";

    /// <summary>Supported languages (German = source, English = fallback for other Windows languages).</summary>
    public static readonly IReadOnlyList<Language> Languages = new Language[]
    {
        new("de-DE", "Deutsch", Segoe, SegoeSemibold, false),
        new("en-US", "English", Segoe, SegoeSemibold, false),
        new("ja-JP", "日本語", "Yu Gothic UI", "Yu Gothic UI Semibold", false),
        new("fr-FR", "Français", Segoe, SegoeSemibold, false),
        new("es-ES", "Español", Segoe, SegoeSemibold, false),
        new("zh-CN", "简体中文", "Microsoft YaHei UI", "Microsoft YaHei UI", true),
        new("zh-TW", "繁體中文", "Microsoft JhengHei UI", "Microsoft JhengHei UI", true),
        new("pt-BR", "Português (Brasil)", Segoe, SegoeSemibold, false),
        new("ru-RU", "Русский", Segoe, SegoeSemibold, false),
        new("ko-KR", "한국어", "Malgun Gothic", "Malgun Gothic", true),
        new("it-IT", "Italiano", Segoe, SegoeSemibold, false),
        new("pl-PL", "Polski", Segoe, SegoeSemibold, false),
        new("nl-NL", "Nederlands", Segoe, SegoeSemibold, false),
        new("tr-TR", "Türkçe", Segoe, SegoeSemibold, false),
    };

    private static Dictionary<string, string>? _map;

    /// <summary>Current language (German until <see cref="Init"/> ran).</summary>
    public static Language Current { get; private set; } = Languages[0];

    public static bool IsGerman => Current.Code == "de-DE";

    /// <summary>
    /// Chooses the language: <paramref name="setting"/> ("" = automatic), else the default the
    /// setup stored next to the program, else the Windows display language.
    /// </summary>
    public static void Init(string? setting)
    {
        var code = Find(setting)?.Code ?? Find(InstallDefault())?.Code ?? Detect();
        Current = Find(code) ?? Languages[1];
        _map = Current.Code == "de-DE" ? null : Load(Current.Code);
        try
        {
            var culture = CultureInfo.GetCultureInfo(Current.Code);
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }
        catch (CultureNotFoundException) { }
    }

    /// <summary>Translation of the German text <paramref name="german"/>.</summary>
    public static string T(string german) =>
        _map != null && _map.TryGetValue(german, out var text) && text.Length > 0 ? text : german;

    /// <summary>Translated format string ("Version {0} ist da") filled with <paramref name="args"/>.</summary>
    public static string F(string german, params object?[] args)
    {
        var format = T(german);
        try { return string.Format(CultureInfo.CurrentCulture, format, args); }
        catch (FormatException) { return string.Format(CultureInfo.CurrentCulture, german, args); }
    }

    public static Language? Find(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null
        : Languages.FirstOrDefault(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

    /// <summary>Best match for the Windows display language, English if none fits.</summary>
    public static string Detect() => Match(CultureInfo.CurrentUICulture.Name);

    public static string Match(string cultureName)
    {
        if (Find(cultureName) is { } exact) return exact.Code;
        var lower = cultureName.ToLowerInvariant();
        if (lower.StartsWith("zh"))
            return lower.Contains("hant") || lower.EndsWith("-tw") || lower.EndsWith("-hk") || lower.EndsWith("-mo") ? "zh-TW" : "zh-CN";
        var two = lower.Split('-')[0];
        return Languages.FirstOrDefault(l => l.Code.StartsWith(two + "-", StringComparison.OrdinalIgnoreCase))?.Code ?? "en-US";
    }

    /// <summary>File "language.txt" next to the program, written by the setup (language chosen there).</summary>
    private static string? InstallDefault()
    {
        try
        {
            var dir = AppContext.BaseDirectory;
            var file = Path.Combine(dir, "language.txt");
            return File.Exists(file) ? File.ReadAllText(file).Trim() : null;
        }
        catch { return null; }
    }

    private static Dictionary<string, string>? Load(string code)
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"lang.{code}.json");
            if (stream == null) return null;
            return JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
        }
        catch (Exception ex)
        {
            DiagLog.Write($"[Lang] loading {code} failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>Help topics of the current language: id → (title, summary, html); null = German built-in.</summary>
    public static Dictionary<string, HelpText>? LoadHelp()
    {
        if (IsGerman) return null;
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"lang.help.{Current.Code}.json");
            if (stream == null) return null;
            return JsonSerializer.Deserialize<Dictionary<string, HelpText>>(stream);
        }
        catch (Exception ex)
        {
            DiagLog.Write($"[Lang] loading help {Current.Code} failed: {ex.Message}");
            return null;
        }
    }

    public sealed class HelpText
    {
        public string Title { get; set; } = "";
        public string Summary { get; set; } = "";
        public string Html { get; set; } = "";
    }
}
