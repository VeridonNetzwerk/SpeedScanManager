using System.Drawing;
using Tesseract;

namespace SpeedScanManager;

/// <summary>
/// Represents a single recognized word with its bounding box in image pixel coordinates.
/// Used to build an invisible text layer in the PDF.
/// </summary>
internal record OcrWord(string Text, Rectangle Bounds);

/// <summary>Result of an orientation and script detection (Tesseract OSD).</summary>
internal readonly record struct OsdResult(int RotateClockwise, float OrientationConfidence, string Script, float ScriptConfidence);

/// <summary>
/// Orientation and script detection with one shared Tesseract "osd" engine.
/// Create one instance per batch (engine creation is expensive) and dispose it afterwards.
/// </summary>
internal sealed class OsdDetector : IDisposable
{
    private readonly string _tessDataPath;
    private TesseractEngine? _engine;
    private bool _initAttempted;

    public OsdDetector(string? tessDataPath = null)
    {
        _tessDataPath = tessDataPath ?? OcrProcessor.DefaultTessDataPath;
    }

    public bool IsAvailable => File.Exists(Path.Combine(_tessDataPath, "osd.traineddata"));

    private static void LogDiag(string msg) => DiagLog.Write(msg);

    private TesseractEngine? GetEngine()
    {
        if (_engine != null || _initAttempted) return _engine;
        _initAttempted = true;
        if (!IsAvailable)
        {
            LogDiag("OSD: osd.traineddata missing");
            return null;
        }
        try
        {
            _engine = new TesseractEngine(_tessDataPath, "osd", EngineMode.Default);
        }
        catch (Exception ex)
        {
            LogDiag($"OSD: engine init failed: {ex.Message}");
        }
        return _engine;
    }

    /// <summary>
    /// Detects page orientation and script. <see cref="OsdResult.RotateClockwise"/> is the
    /// clockwise rotation (0/90/180/270) that makes the text upright.
    /// Runs on a copy of at most ~300 dpi. Returns null if detection failed (e.g. too little text).
    /// </summary>
    public OsdResult? Detect(Bitmap bmp)
    {
        var engine = GetEngine();
        if (engine == null) return null;

        try
        {
            using var sample = ImageProcessing.CreateSample(bmp, 300, 9_000_000, out _);
            using var pix = OcrProcessor.ToPix(sample);
            using var page = engine.Process(pix, PageSegMode.OsdOnly);
            page.DetectBestOrientationAndScript(out int orientDeg, out float orientConf, out string script, out float scriptConf);

            // Tesseract reports the orientation of the text ("page right" = 90: the top of the
            // text points to the right, the page must be turned 90° counter-clockwise).
            int deg = ((orientDeg % 360) + 360) % 360;
            int rotateClockwise = deg switch
            {
                > 45 and <= 135 => 270,   // PageRight -> 90° counter-clockwise
                > 135 and <= 225 => 180,  // PageDown
                > 225 and <= 315 => 90,   // PageLeft -> 90° clockwise
                _ => 0
            };
            LogDiag($"OSD: orientation={orientDeg} (conf {orientConf:0.0}) -> rotate {rotateClockwise}° cw, script={script} (conf {scriptConf:0.0})");
            return new OsdResult(rotateClockwise, orientConf, script ?? "", scriptConf);
        }
        catch (Exception ex)
        {
            LogDiag($"OSD: detection failed: {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        _engine?.Dispose();
        _engine = null;
    }
}

/// <summary>
/// Runs Tesseract OCR on scanned images and returns word-level data
/// with positions for embedding as invisible text layer in PDF.
///
/// TESSDATA SETUP:
///   Tesseract requires traineddata files in a "tessdata" folder.
///   Download from: https://github.com/tesseract-ocr/tessdata_fast
///   Shipped: deu.traineddata, eng.traineddata, osd.traineddata (orientation/script detection)
///   Optional: jpn, fra, ita, spa, chi_sim, chi_tra, kor, rus, por, ara
///   Place them in: [app directory]/tessdata/
///   Missing languages fall back to the installed ones (see <see cref="Warnings"/>).
/// </summary>
internal class OcrProcessor : IDisposable
{
    private TesseractEngine? _engine;
    private readonly string _tessDataPath;
    private readonly bool _isAutoMode;
    private readonly OsdDetector? _sharedOsd;
    private OsdDetector? _ownOsd;
    private string _currentLanguage = "";
    private bool _disposed;

    /// <summary>
    /// Path to the tessdata folder (next to the executable).
    /// </summary>
    internal static string DefaultTessDataPath =>
        Path.Combine(AppContext.BaseDirectory, "tessdata");

    /// <summary>User-facing (German) notes, e.g. missing language data and the fallback used.</summary>
    public List<string> Warnings { get; } = new();

    /// <summary>The Tesseract language code(s) currently loaded (e.g. "deu+eng").</summary>
    public string CurrentLanguage => _currentLanguage;

    public OcrProcessor(OcrLanguage language) : this(language, DefaultTessDataPath)
    {
    }

    public OcrProcessor(OcrLanguage language, string tessDataPath) : this(language, tessDataPath, null)
    {
    }

    /// <summary>
    /// <paramref name="sharedOsd"/>: optional OSD engine shared with auto-rotation
    /// (not disposed by this instance).
    /// </summary>
    public OcrProcessor(OcrLanguage language, string tessDataPath, OsdDetector? sharedOsd)
    {
        _tessDataPath = tessDataPath;
        _sharedOsd = sharedOsd;
        _isAutoMode = language == OcrLanguage.Automatisch;

        var requested = GetLanguageCode(language);
        var resolved = ResolveInstalled(requested, out var missing);
        if (missing.Count > 0)
        {
            if (resolved.Length > 0)
                Warnings.Add(L.F("OCR-Sprachdaten fehlen ({0}); es wird '{1}' verwendet.", string.Join(", ", missing), resolved));
            else
                Warnings.Add(L.T("Es sind keine OCR-Sprachdaten installiert; die Texterkennung wurde übersprungen."));
        }

        if (resolved.Length > 0)
            SwitchEngine(resolved);
    }

    private static void LogDiag(string msg) => DiagLog.Write(msg);

    /// <summary>Tesseract language code(s) for a UI language. Automatic starts with deu+eng.</summary>
    public static string GetLanguageCode(OcrLanguage language) => language switch
    {
        OcrLanguage.Automatisch => "deu+eng", // start value; real language detected per page
        OcrLanguage.Deutsch => "deu",
        OcrLanguage.Japanisch => "jpn",
        OcrLanguage.English => "eng",
        OcrLanguage.Franzoesisch => "fra",
        OcrLanguage.Italienisch => "ita",
        OcrLanguage.Spanisch => "spa",
        OcrLanguage.ChinesischVereinfacht => "chi_sim",
        OcrLanguage.ChinesischTraditionell => "chi_tra",
        OcrLanguage.Koreanisch => "kor",
        OcrLanguage.Russisch => "rus",
        OcrLanguage.Portugiesisch => "por",
        OcrLanguage.Arabisch => "ara",
        _ => "deu+eng"
    };

    /// <summary>
    /// Lists the installed OCR languages (traineddata files in tessdata, without "osd"),
    /// e.g. ["deu", "eng"].
    /// </summary>
    public static IReadOnlyList<string> GetInstalledLanguages(string? tessDataPath = null)
    {
        var path = tessDataPath ?? DefaultTessDataPath;
        try
        {
            if (!Directory.Exists(path)) return Array.Empty<string>();
            return Directory.GetFiles(path, "*.traineddata")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .Where(n => !n.Equals("osd", StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>True if the traineddata for all codes of <paramref name="languageCode"/> ("deu+eng") exist.</summary>
    public static bool IsLanguageInstalled(string languageCode, string? tessDataPath = null)
    {
        var installed = GetInstalledLanguages(tessDataPath);
        return languageCode.Split('+', StringSplitOptions.RemoveEmptyEntries)
            .All(c => installed.Contains(c, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Keeps the installed codes of a "a+b" request. If none is installed, falls back to
    /// installed deu/eng, else to all installed languages. Returns "" if nothing is installed.
    /// </summary>
    private string ResolveInstalled(string requested, out List<string> missing)
    {
        var installed = GetInstalledLanguages(_tessDataPath);
        var codes = requested.Split('+', StringSplitOptions.RemoveEmptyEntries);
        missing = codes.Where(c => !installed.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
        var present = codes.Where(c => installed.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
        if (present.Count > 0) return string.Join("+", present);

        var fallback = installed.Where(c => c is "deu" or "eng").ToList();
        if (fallback.Count == 0) fallback = installed.ToList();
        return string.Join("+", fallback);
    }

    public bool IsAvailable => _engine != null;

    /// <summary>
    /// Maps a Tesseract OSD script name to the best installed language code(s).
    /// </summary>
    private string ScriptToLanguageCode(string script)
    {
        var s = script.ToLowerInvariant();
        string? code = null;
        if (s.Contains("japanese") || s.Contains("hiragana") || s.Contains("katakana")) code = "jpn";
        else if (s.Contains("hangul") || s.Contains("korean")) code = "kor";
        else if (s == "han" || s.Contains("chinese"))
            code = IsTessDataAvailable("chi_sim") || !IsTessDataAvailable("chi_tra") ? "chi_sim" : "chi_tra";
        else if (s.Contains("arabic")) code = "ara";
        else if (s.Contains("cyrillic")) code = "rus";

        if (code != null && IsTessDataAvailable(code)) return code;
        // Latin scripts (or missing data): deu+eng is a good general-purpose combination.
        return ResolveInstalled("deu+eng", out _);
    }

    /// <summary>
    /// Checks if a .traineddata file exists in the tessdata folder.
    /// </summary>
    private bool IsTessDataAvailable(string langCode)
    {
        return File.Exists(Path.Combine(_tessDataPath, langCode + ".traineddata"));
    }

    private OsdDetector? Osd => _sharedOsd ?? (_ownOsd ??= new OsdDetector(_tessDataPath));

    /// <summary>
    /// Runs OCR on a single bitmap and returns recognized words with positions.
    /// Coordinates are in image pixel space and need to be scaled to PDF points by the caller.
    /// In automatic mode, the script is taken from <paramref name="scriptHint"/> (OSD result of
    /// auto-rotation) or detected, and the engine is switched if the language differs.
    /// </summary>
    public List<OcrWord> Recognize(Bitmap bitmap, string? scriptHint = null)
    {
        var result = new List<OcrWord>();

        if (_engine == null)
        {
            LogDiag("OCR: Tesseract engine not available, skipping OCR.");
            return result;
        }

        try
        {
            // In automatic mode, detect the script and switch engine if needed
            if (_isAutoMode)
            {
                var script = scriptHint;
                if (string.IsNullOrEmpty(script))
                    script = Osd?.Detect(bitmap)?.Script;
                if (!string.IsNullOrEmpty(script))
                {
                    var detectedLang = ScriptToLanguageCode(script);
                    if (detectedLang.Length > 0 && detectedLang != _currentLanguage)
                    {
                        LogDiag($"Auto-OCR: script '{script}' -> {detectedLang}");
                        SwitchEngine(detectedLang);
                        if (_engine == null) return result;
                    }
                }
            }

            // Large images: OCR on a ~300 dpi copy (enough for recognition, keeps the x86
            // process within memory limits), then scale the boxes back.
            double scale = 1.0;
            Bitmap ocrInput = bitmap;
            Bitmap? ownedInput = null;
            if (ImageProcessing.GetDpiX(bitmap) > 400)
            {
                ownedInput = ImageProcessing.CreateSample(bitmap, 300, 12_000_000, out scale);
                ocrInput = ownedInput;
            }

            try
            {
                using var pix = ToPix(ocrInput);
                using var page = _engine.Process(pix);
                using var iter = page.GetIterator();
                if (iter == null) return result;

                iter.Begin();
                double inv = 1.0 / scale;
                do
                {
                    string? word = iter.GetText(PageIteratorLevel.Word);
                    if (!string.IsNullOrWhiteSpace(word)
                        && iter.TryGetBoundingBox(PageIteratorLevel.Word, out Rect bounds))
                    {
                        result.Add(new OcrWord(word.Trim(), new Rectangle(
                            (int)(bounds.X1 * inv), (int)(bounds.Y1 * inv),
                            Math.Max(1, (int)(bounds.Width * inv)), Math.Max(1, (int)(bounds.Height * inv)))));
                    }
                }
                while (iter.Next(PageIteratorLevel.Word));
            }
            finally
            {
                ownedInput?.Dispose();
            }
        }
        catch (Exception ex)
        {
            LogDiag($"OCR recognition failed: {ex.Message}");
        }

        return result;
    }

    /// <summary>Converts a bitmap to a Leptonica Pix (via an in-memory BMP).</summary>
    internal static Pix ToPix(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
        return Pix.LoadFromMemory(ms.ToArray());
    }

    /// <summary>
    /// Disposes the current engine and creates a new one with the given language code.
    /// </summary>
    private void SwitchEngine(string langCode)
    {
        try
        {
            _engine?.Dispose();
            _engine = new TesseractEngine(_tessDataPath, langCode, EngineMode.Default);
            _engine.DefaultPageSegMode = PageSegMode.Auto;
            _currentLanguage = langCode;
        }
        catch (Exception ex)
        {
            LogDiag($"SwitchEngine to '{langCode}' failed: {ex.Message}");
            _engine = null;
            // Try fallback to the installed default languages
            var fallback = ResolveInstalled("deu+eng", out _);
            if (fallback.Length > 0 && fallback != langCode)
            {
                try
                {
                    _engine = new TesseractEngine(_tessDataPath, fallback, EngineMode.Default);
                    _engine.DefaultPageSegMode = PageSegMode.Auto;
                    _currentLanguage = fallback;
                }
                catch
                {
                    _engine = null;
                }
            }
        }
    }

    /// <summary>
    /// Runs OCR on multiple bitmaps. Only processes pages according to targetPages setting.
    /// Returns a list (one entry per image, empty list for skipped pages).
    /// </summary>
    public List<List<OcrWord>> RecognizeAll(List<Bitmap> bitmaps, OcrTargetPages targetPages)
    {
        var results = new List<List<OcrWord>>();

        for (int i = 0; i < bitmaps.Count; i++)
        {
            if (targetPages == OcrTargetPages.FirstPage && i > 0)
            {
                results.Add(new List<OcrWord>());
                continue;
            }

            results.Add(Recognize(bitmaps[i]));
        }

        return results;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _engine?.Dispose();
            _engine = null;
            _ownOsd?.Dispose();
            _ownOsd = null;
            _disposed = true;
        }
    }
}
