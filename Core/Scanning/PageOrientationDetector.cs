using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Tesseract;

namespace SpeedScanManager;

/// <summary>
/// Finds the rotation that makes a page upright. The orientation detection of Tesseract (OSD) is
/// often unsure on lists, tables and capital letters, so its result is checked by reading a
/// text-rich part of the page: text in a wrong direction yields almost no real words.
/// One instance per batch (engine creation is expensive).
/// </summary>
internal sealed class PageOrientationDetector : IDisposable
{
    /// <summary>OSD confidence that is accepted for upright pages without reading text.</summary>
    private const float SureOsdConfidence = 10f;
    /// <summary>OSD confidence that is accepted if reading gives no clear result (e.g. other scripts).</summary>
    private const float FallbackOsdConfidence = 3f;
    /// <summary>Edge length of the part of the page that is read.</summary>
    private const double PartInches = 4.4;

    private readonly OsdDetector _osd;
    private readonly string _languages;
    private TesseractEngine? _engine;
    private bool _engineAttempted;

    public PageOrientationDetector(OsdDetector osd, OcrLanguage language)
    {
        _osd = osd;
        var installed = OcrProcessor.GetInstalledLanguages();
        var codes = OcrProcessor.GetLanguageCode(language).Split('+')
            .Where(c => installed.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
        if (codes.Count == 0) codes = installed.Where(c => c is "deu" or "eng").ToList();
        _languages = string.Join("+", codes);
    }

    private static void LogDiag(string msg) => DiagLog.Write(msg);

    private readonly record struct ReadScore(int GoodChars, int GoodWords, int Words)
    {
        /// <summary>Clearly readable text. Wrong directions reach about 12 % good words.</summary>
        public bool IsClear => GoodChars >= 40 && GoodWords >= 8 && GoodWords >= Words * 0.45;
    }

    /// <summary>
    /// Clockwise rotation (0/90/180/270) that makes the page upright.
    /// <paramref name="script"/> is the script found by OSD (null if unknown).
    /// </summary>
    public int Detect(Bitmap page, out string? script)
    {
        var osd = _osd.Detect(page);
        script = osd?.Script;
        int osdRotate = osd?.RotateClockwise ?? 0;
        float conf = osd?.OrientationConfidence ?? 0;
        if (osd != null && osdRotate == 0 && conf >= SureOsdConfidence) return 0;

        var engine = GetEngine();
        if (engine != null)
        {
            try
            {
                using var part = TextPart(page);
                if (part == null)
                {
                    LogDiag("AutoRotate: too little text, not rotating");
                    return 0;
                }

                // Most likely direction first.
                var order = new[] { osdRotate, 0, 180, 90, 270 }.Distinct().ToList();
                var scores = new Dictionary<int, ReadScore>();
                foreach (int rotate in order)
                {
                    var score = Read(engine, part, rotate);
                    scores[rotate] = score;
                    if (score.IsClear)
                    {
                        LogDiag($"AutoRotate: text readable at {rotate}° ({score.GoodWords}/{score.Words} words, OSD {osdRotate}° conf {conf:0.0})");
                        return rotate;
                    }
                }

                var best = scores.OrderByDescending(kv => kv.Value.GoodChars).First();
                int second = scores.Where(kv => kv.Key != best.Key).Max(kv => kv.Value.GoodChars);
                LogDiag("AutoRotate: " + string.Join(", ", scores.Select(kv => $"{kv.Key}°={kv.Value.GoodChars} chars {kv.Value.GoodWords}/{kv.Value.Words} words"))
                        + $", OSD {osdRotate}° conf {conf:0.0}");
                if (best.Value.GoodChars >= 15 && best.Value.GoodChars >= second * 2)
                    return best.Key;
            }
            catch (Exception ex)
            {
                LogDiag($"AutoRotate: reading failed: {ex.Message}");
            }
        }

        if (osdRotate != 0 && conf < FallbackOsdConfidence)
        {
            LogDiag($"AutoRotate: OSD confidence {conf:0.0} too low, not rotating");
            return 0;
        }
        return osdRotate;
    }

    private TesseractEngine? GetEngine()
    {
        if (_engine != null || _engineAttempted) return _engine;
        _engineAttempted = true;
        if (_languages.Length == 0) return null;
        try
        {
            _engine = new TesseractEngine(
                OcrData.DataPath(OcrQuality.Schnell, _languages.Split('+')) ?? OcrProcessor.DefaultTessDataPath, _languages, EngineMode.Default);
        }
        catch (Exception ex)
        {
            LogDiag($"AutoRotate: OCR engine init failed: {ex.Message}");
        }
        return _engine;
    }

    /// <summary>
    /// The square part with the most text-like ink as a 24 bpp copy of at most 300 dpi;
    /// null if the page has hardly any.
    /// </summary>
    private static Bitmap? TextPart(Bitmap page)
    {
        using var sample = ImageProcessing.CreateSample(page, 50, 1_000_000, out double scale);
        int w = sample.Width, h = sample.Height;
        ImageProcessing.ReadRgb(sample, out _, out _, out _, out var lum);
        var hist = new int[256];
        foreach (var l in lum) hist[l]++;
        int threshold = ImageProcessing.Percentile(hist, lum.Length, 0.5) - 35;

        // Summed-area table of dark pixels next to a brightness step, so that photos and
        // filled areas do not win over text.
        int sw = w + 1;
        var sum = new int[sw * (h + 1)];
        for (int y = 0; y < h; y++)
        {
            int rowSum = 0;
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (x > 0 && lum[i] < threshold && Math.Abs(lum[i] - lum[i - 1]) > 15) rowSum++;
                sum[(y + 1) * sw + x + 1] = sum[y * sw + x + 1] + rowSum;
            }
        }

        int side = (int)Math.Round(PartInches * ImageProcessing.GetDpiX(page) * scale);
        int bw = Math.Min(w, side), bh = Math.Min(h, side);
        int bestInk = -1, bx = 0, by = 0;
        int step = Math.Max(1, side / 16);
        for (int y = 0; y <= h - bh; y += step)
        {
            for (int x = 0; x <= w - bw; x += step)
            {
                int ink = sum[(y + bh) * sw + x + bw] - sum[y * sw + x + bw] - sum[(y + bh) * sw + x] + sum[y * sw + x];
                if (ink > bestInk) { bestInk = ink; bx = x; by = y; }
            }
        }
        if (bestInk < 40) return null;

        double inv = 1.0 / scale;
        var rect = Rectangle.Intersect(new Rectangle(0, 0, page.Width, page.Height),
            new Rectangle((int)(bx * inv), (int)(by * inv), (int)(bw * inv), (int)(bh * inv)));
        double factor = Math.Min(1.0, 300.0 / ImageProcessing.GetDpiX(page));
        var part = new Bitmap(Math.Max(1, (int)(rect.Width * factor)), Math.Max(1, (int)(rect.Height * factor)), PixelFormat.Format24bppRgb);
        part.SetResolution((float)(ImageProcessing.GetDpiX(page) * factor), (float)(ImageProcessing.GetDpiY(page) * factor));
        using (var g = Graphics.FromImage(part))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(page, new Rectangle(0, 0, part.Width, part.Height), rect, GraphicsUnit.Pixel);
        }
        return part;
    }

    private static ReadScore Read(TesseractEngine engine, Bitmap part, int rotate)
    {
        using var turned = ImageProcessing.DeepCopy(part);
        turned.RotateFlip(ToFlip(rotate));
        using var pix = OcrProcessor.ToPix(turned);
        // Not PageSegMode.Auto: that also reads text turned by 90° and would hide the difference.
        using var page = engine.Process(pix, PageSegMode.SingleBlock);
        using var iter = page.GetIterator();
        int goodChars = 0, goodWords = 0, words = 0;
        if (iter != null)
        {
            iter.Begin();
            do
            {
                var text = iter.GetText(PageIteratorLevel.Word)?.Trim();
                if (string.IsNullOrEmpty(text)) continue;
                words++;
                int letters = text.Count(char.IsLetterOrDigit);
                if (letters >= 3 && iter.GetConfidence(PageIteratorLevel.Word) >= 70)
                {
                    goodWords++;
                    goodChars += letters;
                }
            }
            while (iter.Next(PageIteratorLevel.Word));
        }
        return new ReadScore(goodChars, goodWords, words);
    }

    public static RotateFlipType ToFlip(int rotateClockwise) => rotateClockwise switch
    {
        90 => RotateFlipType.Rotate90FlipNone,
        180 => RotateFlipType.Rotate180FlipNone,
        270 => RotateFlipType.Rotate270FlipNone,
        _ => RotateFlipType.RotateNoneFlipNone
    };

    public void Dispose()
    {
        _engine?.Dispose();
        _engine = null;
    }
}
