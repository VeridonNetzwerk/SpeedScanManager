using System.IO;
using System.Net.Http;

namespace SpeedScanManager;

/// <summary>
/// Where the Tesseract language models are and how missing ones are installed.
///
/// Layout (both "best" = <see cref="OcrQuality.Genau"/> and "fast" = <see cref="OcrQuality.Schnell"/>):
///   [app]\tessdata\osd.traineddata            orientation / script detection (always shipped)
///   [app]\tessdata\best|fast\xxx.traineddata  installed by the setup (setup language + English)
///   %LocalAppData%\SpeedScanManager\tessdata\best|fast\xxx.traineddata   installed later by the app
/// A flat [app]\tessdata\xxx.traineddata (older versions, dev builds) counts as "fast".
///
/// Tesseract reads all languages of one engine ("deu+eng") from a single folder. If they are
/// spread over several folders, they are copied together into a "combined" folder.
/// </summary>
internal static class OcrData
{
    /// <summary>Codes of all supported OCR languages.</summary>
    public static readonly string[] AllCodes =
        { "deu", "eng", "jpn", "fra", "spa", "chi_sim", "chi_tra", "por", "rus", "kor", "ita", "pol", "nld", "tur", "ara" };

    /// <summary>
    /// tessdata_fast models that scatter random accents over the text (measured: 40–80 % wrong
    /// characters). "Schnell" uses the accurate model for them; they aren't downloaded at all.
    /// </summary>
    private static readonly HashSet<string> BrokenFast = new(StringComparer.OrdinalIgnoreCase) { "ita", "nld", "tur", "ara" };

    /// <summary>Approximate download size in MB (best + fast unless broken), for the question to the user.</summary>
    private static readonly Dictionary<string, int> SizeMb = new()
    {
        ["deu"] = 10, ["eng"] = 20, ["jpn"] = 17, ["fra"] = 5, ["spa"] = 16, ["chi_sim"] = 16, ["chi_tra"] = 15,
        ["por"] = 10, ["rus"] = 19, ["kor"] = 14, ["ita"] = 9, ["pol"] = 17, ["nld"] = 9, ["tur"] = 8, ["ara"] = 13,
    };

    private const string DownloadUrl = "https://github.com/tesseract-ocr/tessdata_{0}/raw/main/{1}.traineddata";

    public static string ShippedRoot => Path.Combine(AppContext.BaseDirectory, "tessdata");

    public static string UserRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpeedScanManager", "tessdata");

    private static string Sub(OcrQuality quality) => quality == OcrQuality.Genau ? "best" : "fast";

    private static IEnumerable<string> Dirs(OcrQuality quality)
    {
        yield return Path.Combine(ShippedRoot, Sub(quality));
        yield return Path.Combine(UserRoot, Sub(quality));
        if (quality == OcrQuality.Schnell) yield return ShippedRoot; // flat layout of older versions
    }

    private static string? Find(string code, OcrQuality quality) =>
        Dirs(quality).Select(d => Path.Combine(d, code + ".traineddata")).FirstOrDefault(File.Exists);

    /// <summary>Model file for <paramref name="code"/>: the wanted quality, otherwise the other one.</summary>
    private static string? FindAny(string code, OcrQuality quality)
    {
        if (quality == OcrQuality.Schnell && BrokenFast.Contains(code)) quality = OcrQuality.Genau;
        var file = Find(code, quality);
        if (file != null) return file;
        var other = quality == OcrQuality.Genau ? OcrQuality.Schnell : OcrQuality.Genau;
        return other == OcrQuality.Schnell && BrokenFast.Contains(code) ? null : Find(code, other);
    }

    public static bool IsInstalled(string code) => FindAny(code, OcrQuality.Genau) != null;

    /// <summary>True if all codes of "deu+eng" are installed.</summary>
    public static bool AreInstalled(string codes) =>
        codes.Split('+', StringSplitOptions.RemoveEmptyEntries).All(IsInstalled);

    /// <summary>Installed language codes (without "osd").</summary>
    public static IReadOnlyList<string> InstalledLanguages() => AllCodes.Where(IsInstalled).ToList();

    /// <summary>
    /// Folder that contains all <paramref name="codes"/> in the given quality (missing quality
    /// replaced by the other one). Null if a code isn't installed.
    /// </summary>
    public static string? DataPath(OcrQuality quality, IReadOnlyCollection<string> codes)
    {
        var files = codes.Select(c => FindAny(c, quality)).ToList();
        if (files.Count == 0 || files.Any(f => f == null)) return null;
        var dirs = files.Select(f => Path.GetDirectoryName(f!)!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (dirs.Count == 1) return dirs[0];

        var target = Path.Combine(UserRoot, "combined", Sub(quality));
        Directory.CreateDirectory(target);
        foreach (var (code, file) in codes.Zip(files))
        {
            var dst = Path.Combine(target, code + ".traineddata");
            if (!File.Exists(dst) || new FileInfo(dst).Length != new FileInfo(file!).Length)
                File.Copy(file!, dst, true);
        }
        return target;
    }

    /// <summary>Approximate download size of a language in MB.</summary>
    public static int DownloadSizeMb(string code) => SizeMb.TryGetValue(code, out var mb) ? mb : 15;

    /// <summary>
    /// Downloads the models of <paramref name="code"/> (accurate and fast) into the user folder.
    /// <paramref name="progress"/> gets 0..1. Throws on errors and cancellation; leaves no partial files.
    /// </summary>
    public static async Task DownloadAsync(string code, IProgress<double>? progress, CancellationToken ct)
    {
        if (!AllCodes.Contains(code)) throw new ArgumentException(code);
        var parts = new List<OcrQuality> { OcrQuality.Genau };
        if (!BrokenFast.Contains(code)) parts.Add(OcrQuality.Schnell);

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("SpeedScanManager");
        for (int i = 0; i < parts.Count; i++)
        {
            var dir = Path.Combine(UserRoot, Sub(parts[i]));
            Directory.CreateDirectory(dir);
            var dst = Path.Combine(dir, code + ".traineddata");
            var tmp = dst + ".part";
            try
            {
                using var response = await http.GetAsync(string.Format(DownloadUrl, Sub(parts[i]), code),
                    HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();
                long total = response.Content.Headers.ContentLength ?? 0;
                await using (var src = await response.Content.ReadAsStreamAsync(ct))
                await using (var file = File.Create(tmp))
                {
                    var buffer = new byte[81920];
                    long done = 0;
                    int read;
                    while ((read = await src.ReadAsync(buffer, ct)) > 0)
                    {
                        await file.WriteAsync(buffer.AsMemory(0, read), ct);
                        done += read;
                        if (total > 0) progress?.Report((i + (double)done / total) / parts.Count);
                    }
                }
                // A model is several MB; an error page is not
                if (new FileInfo(tmp).Length < 100_000) throw new InvalidDataException(L.T("Die heruntergeladene Datei ist ungültig."));
                File.Move(tmp, dst, true);
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }
        progress?.Report(1);
        DiagLog.Write($"OCR: downloaded '{code}' ({string.Join(", ", parts.Select(Sub))})");
    }

    /// <summary>OCR language code for an app language ("it-IT" → "ita").</summary>
    public static string? CodeForAppLanguage(string appLanguage) => appLanguage switch
    {
        "de-DE" => "deu", "en-US" => "eng", "ja-JP" => "jpn", "fr-FR" => "fra", "es-ES" => "spa",
        "zh-CN" => "chi_sim", "zh-TW" => "chi_tra", "pt-BR" => "por", "ru-RU" => "rus", "ko-KR" => "kor",
        "it-IT" => "ita", "pl-PL" => "pol", "nl-NL" => "nld", "tr-TR" => "tur",
        _ => null
    };

    /// <summary>OCR language setting for a code ("ita" → Italienisch).</summary>
    public static OcrLanguage LanguageOf(string code) =>
        Enum.GetValues<OcrLanguage>().FirstOrDefault(l => l != OcrLanguage.Automatisch && OcrProcessor.GetLanguageCode(l) == code);
}
