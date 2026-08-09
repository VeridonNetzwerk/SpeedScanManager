using System.Drawing;
using Tesseract;

namespace SpeedScanManager;

/// <summary>
/// Represents a single recognized word with its bounding box in image pixel coordinates.
/// Used to build an invisible text layer in the PDF.
/// </summary>
internal record OcrWord(string Text, Rectangle Bounds);

/// <summary>
/// Runs Tesseract OCR on scanned images and returns word-level data
/// with positions for embedding as invisible text layer in PDF.
///
/// TESSDATA SETUP:
///   Tesseract requires traineddata files in a "tessdata" folder.
///   Download from: https://github.com/tesseract-ocr/tessdata_fast
///   Required files: deu.traineddata, eng.traineddata
///   For automatic mode: osd.traineddata (script detection)
///   Optional: jpn, fra, ita, spa, chi_sim, chi_tra, kor, rus, por, ara
///   Place them in: [app directory]/tessdata/
/// </summary>
internal class OcrProcessor : IDisposable
{
    private TesseractEngine? _engine;
    private readonly string _tessDataPath;
    private readonly string _languageCode;
    private readonly bool _isAutoMode;
    private bool _disposed;

    /// <summary>
    /// Path to the tessdata folder (next to the executable).
    /// </summary>
    private static string DefaultTessDataPath =>
        Path.Combine(AppContext.BaseDirectory, "tessdata");

    public OcrProcessor(OcrLanguage language) : this(language, DefaultTessDataPath)
    {
    }

    public OcrProcessor(OcrLanguage language, string tessDataPath)
    {
        _tessDataPath = tessDataPath;
        _isAutoMode = language == OcrLanguage.Automatisch;
        _languageCode = language switch
        {
            OcrLanguage.Automatisch => "deu+eng", // fallback; real language detected per-page
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

        InitializeEngine();
    }

    private void InitializeEngine()
    {
        try
        {
            _engine = new TesseractEngine(_tessDataPath, _languageCode, EngineMode.Default);
            _engine.DefaultPageSegMode = PageSegMode.Auto;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Tesseract init failed: {ex.Message}");
            _engine = null;
        }
    }

    public bool IsAvailable => _engine != null;

    /// <summary>
    /// Detects the script of a page using a quick OSD pass and returns the
    /// best matching Tesseract language code(s).
    /// Falls back to deu+eng if detection fails or osd.traineddata is missing.
    /// </summary>
    private string DetectLanguageForPage(Pix pix)
    {
        try
        {
            using var osdEngine = new TesseractEngine(_tessDataPath, "osd", EngineMode.Default);
            osdEngine.DefaultPageSegMode = PageSegMode.OsdOnly;
            using var page = osdEngine.Process(pix);
            var properties = page.GetIterator();
            // OSD gives us script detection via the page properties
            // We check the detected script name from the page text confidence
            // Tesseract OSD reports properties via page.GetIterator() and confidence

            // Use GetOsdProperties if available (Tesseract C# wrapper)
            // Otherwise fall back to analyzing the text output
            var text = page.GetText();
            if (!string.IsNullOrEmpty(text))
            {
                // OSD output contains lines like "Page number: 0" and "Orientation in degrees: ..."
                // and "Script: Latin" or "Script: Japanese" etc.
                var lines = text.Split('\n');
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("Script:", StringComparison.OrdinalIgnoreCase))
                    {
                        var script = trimmed.Substring("Script:".Length).Trim();
                        return ScriptToLanguageCode(script);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OSD script detection failed: {ex.Message}");
        }
        return "deu+eng";
    }

    /// <summary>
    /// Maps a Tesseract OSD script name to the best language code(s).
    /// </summary>
    private string ScriptToLanguageCode(string script)
    {
        var s = script.ToLowerInvariant();
        if (s.Contains("japanese")) return IsTessDataAvailable("jpn") ? "jpn" : "deu+eng";
        if (s.Contains("chinese") && s.Contains("simplified")) return IsTessDataAvailable("chi_sim") ? "chi_sim" : "deu+eng";
        if (s.Contains("chinese") && s.Contains("traditional")) return IsTessDataAvailable("chi_tra") ? "chi_tra" : "deu+eng";
        if (s.Contains("chinese")) return IsTessDataAvailable("chi_sim") ? "chi_sim" : "deu+eng";
        if (s.Contains("korean")) return IsTessDataAvailable("kor") ? "kor" : "deu+eng";
        if (s.Contains("arabic")) return IsTessDataAvailable("ara") ? "ara" : "deu+eng";
        if (s.Contains("cyrillic")) return IsTessDataAvailable("rus") ? "rus" : "deu+eng";
        // Latin scripts: use deu+eng as a good general-purpose combo
        return "deu+eng";
    }

    /// <summary>
    /// Checks if a .traineddata file exists in the tessdata folder.
    /// </summary>
    private bool IsTessDataAvailable(string langCode)
    {
        return File.Exists(Path.Combine(_tessDataPath, langCode + ".traineddata"));
    }

    /// <summary>
    /// Runs OCR on a single bitmap and returns recognized words with positions.
    /// Coordinates are in image pixel space and need to be scaled to PDF points by the caller.
    /// In automatic mode, detects the script first, then re-initializes the engine
    /// with the appropriate language if it differs from the current one.
    /// </summary>
    public List<OcrWord> Recognize(Bitmap bitmap)
    {
        var result = new List<OcrWord>();

        if (_engine == null)
        {
            System.Diagnostics.Debug.WriteLine("Tesseract engine not available, skipping OCR.");
            return result;
        }

        try
        {
            // Convert Bitmap to Pix via byte array
            using var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
            using var pix = Pix.LoadFromMemory(ms.ToArray());

            // In automatic mode, detect the script and switch engine if needed
            if (_isAutoMode)
            {
                var detectedLang = DetectLanguageForPage(pix);
                if (detectedLang != _languageCode)
                {
                    System.Diagnostics.Debug.WriteLine($"Auto-OCR: detected script -> {detectedLang}");
                    SwitchEngine(detectedLang);
                    if (_engine == null) return result;
                }
            }

            using var page = _engine.Process(pix);

            using var iter = page.GetIterator();
            if (iter == null) return result;

            iter.Begin();

            do
            {
                // Get word text using GetText at Word level
                string? word = iter.GetText(PageIteratorLevel.Word);
                if (!string.IsNullOrWhiteSpace(word))
                {
                    // Get bounding box
                    if (iter.TryGetBoundingBox(PageIteratorLevel.Word, out Rect bounds))
                    {
                        result.Add(new OcrWord(word,
                            new Rectangle(bounds.X1, bounds.Y1, bounds.Width, bounds.Height)));
                    }
                }
            }
            while (iter.Next(PageIteratorLevel.Word, PageIteratorLevel.Word));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OCR recognition failed: {ex.Message}");
        }

        return result;
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
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SwitchEngine to '{langCode}' failed: {ex.Message}");
            // Try fallback to deu+eng
            try
            {
                _engine = new TesseractEngine(_tessDataPath, "deu+eng", EngineMode.Default);
                _engine.DefaultPageSegMode = PageSegMode.Auto;
            }
            catch
            {
                _engine = null;
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
            _disposed = true;
        }
    }
}
