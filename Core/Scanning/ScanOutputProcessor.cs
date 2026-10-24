using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace SpeedScanManager;

/// <summary>
/// Options for <see cref="ScanOutputProcessor.Process"/>.
/// </summary>
internal sealed class ScanOutputOptions
{
    public FileNameFormatDialog.FormatMode FormatMode { get; set; } = FileNameFormatDialog.FormatMode.Timestamp;
    public string CustomFileName { get; set; } = "unbenannt";
    public int CounterDigits { get; set; } = 3;

    /// <summary>
    /// Explicit base file name (e.g. the title typed in the post-scan dialog). Overrides
    /// <see cref="FormatMode"/>; it is sanitized and still made unique ("Titel", "Titel_1", ...).
    /// </summary>
    public string? BaseName { get; set; }

    /// <summary>Return the final pages in <see cref="ScanOutputResult.ProcessedPages"/> (costs memory).</summary>
    public bool ProduceProcessedPages { get; set; } = true;

    /// <summary>
    /// Pages alternate front/back (duplex). Null = derive from the settings (ScanSide.Duplex).
    /// Use <see cref="ScanResult.WasDuplex"/> when available.
    /// </summary>
    public bool? WasDuplex { get; set; }

    /// <summary>Remove blank back sides even if blank page removal is off (<see cref="ScanResult.RemoveBlankBacks"/>).</summary>
    public bool RemoveBlankBacks { get; set; }

    /// <summary>Convenience: options taking duplex information from a scan result.</summary>
    public static ScanOutputOptions FromScanResult(ScanResult scan, ScanSettings settings) => new()
    {
        FormatMode = settings.FileNameFormat,
        CustomFileName = settings.CustomFileName,
        CounterDigits = settings.CounterDigits,
        WasDuplex = scan.WasDuplex,
        RemoveBlankBacks = scan.RemoveBlankBacks
    };
}

/// <summary>
/// Result of <see cref="ScanOutputProcessor.Process"/>. Dispose it to release
/// <see cref="ProcessedPages"/> (unless the caller took them over).
/// </summary>
internal sealed class ScanOutputResult : IDisposable
{
    /// <summary>Created files (full paths). Empty if all pages were blank.</summary>
    public List<string> Files { get; } = new();

    /// <summary>
    /// Final pages after blank removal, deskew, rotation, carrier merge and color conversion
    /// (for printing / Office export). Owned by the caller. Empty if not requested.
    /// </summary>
    public List<Bitmap> ProcessedPages { get; } = new();

    /// <summary>Number of pages in the output.</summary>
    public int PageCount { get; internal set; }
    public int BlankPagesRemoved { get; internal set; }

    /// <summary>Every page was blank; nothing was saved.</summary>
    public bool AllPagesBlank { get; internal set; }

    /// <summary>Non-fatal notes for the user (German), e.g. OCR language fallback.</summary>
    public List<string> Warnings { get; } = new();

    /// <summary>Keywords written to the PDF (highlighted text), if any.</summary>
    public List<string> Keywords { get; } = new();

    public void Dispose()
    {
        foreach (var p in ProcessedPages) p.Dispose();
        ProcessedPages.Clear();
    }
}

/// <summary>
/// Handles post-processing of scanned images and output file generation (PDF/JPEG/PNG).
///
/// OWNERSHIP: input bitmaps are never modified or disposed; the caller keeps owning them.
/// Intermediate bitmaps are disposed internally; final pages are only handed out through
/// <see cref="ScanOutputResult.ProcessedPages"/>.
/// The settings are snapshotted (Clone) at the start of every call.
/// </summary>
internal class ScanOutputProcessor
{
    private const float MinOsdConfidence = 3.0f;

    /// <summary>Brush of the OCR text layer. Transparent; a test harness may set a visible brush to check positions.</summary>
    internal static XBrush OcrTextBrush { get; set; } = XBrushes.Transparent;

    private readonly ScanSettings _settingsSource;
    private ScanSettings _settings;

    private static void LogDiag(string msg) => DiagLog.Write(msg);

    public ScanOutputProcessor(ScanSettings settings)
    {
        _settingsSource = settings;
        _settings = settings;
    }

    /// <summary>
    /// Full pipeline: post-process images, generate output file(s), return list of created file paths.
    /// Does not dispose or modify <paramref name="images"/>. Kept for compatibility — prefer
    /// <see cref="Process"/>, which also reports blank pages, warnings and the processed pages.
    /// </summary>
    public List<string> ProcessAndSave(List<Bitmap> images, string targetFolder,
        FileNameFormatDialog.FormatMode formatMode = FileNameFormatDialog.FormatMode.Timestamp,
        string customFileName = "unbenannt", int counterDigits = 3)
    {
        using var result = Process(images, targetFolder, new ScanOutputOptions
        {
            FormatMode = formatMode,
            CustomFileName = customFileName,
            CounterDigits = counterDigits,
            ProduceProcessedPages = false
        });
        foreach (var w in result.Warnings) LogDiag($"ProcessAndSave warning: {w}");
        return new List<string>(result.Files);
    }

    private sealed class WorkPage
    {
        public Bitmap Bmp = null!;
        public bool Owned;
        public bool IsBack;
        public string? Script;
        public List<Rectangle>? Highlights;

        public void Replace(Bitmap next)
        {
            if (ReferenceEquals(next, Bmp)) return;
            if (Owned) Bmp.Dispose();
            Bmp = next;
            Owned = true;
        }
    }

    /// <summary>
    /// Post-processes the images and writes the output files. The input bitmaps are not
    /// disposed or modified. Throws on I/O errors (e.g. folder not writable); intermediate
    /// bitmaps are released in any case.
    /// </summary>
    public ScanOutputResult Process(IReadOnlyList<Bitmap> images, string targetFolder, ScanOutputOptions options)
    {
        _settings = _settingsSource.Clone();
        var s = _settings;
        var result = new ScanOutputResult();
        if (images.Count == 0)
            return result;

        bool duplex = options.WasDuplex ?? s.ScanSide == ScanSide.Duplex;
        var pages = new List<WorkPage>();
        OsdDetector? osd = null;

        try
        {
            // 1. Carrier sheet: merge front/back (duplex) side by side, apply the output size.
            BuildPages(images, duplex, pages);

            // 2. Blank pages — before OSD/OCR so they cost nothing further.
            for (int i = pages.Count - 1; i >= 0; i--)
            {
                var page = pages[i];
                bool check = s.AllowDeleteBlankPages || (options.RemoveBlankBacks && page.IsBack);
                if (check && ImageProcessing.IsBlankPage(page.Bmp))
                {
                    if (page.Owned) page.Bmp.Dispose();
                    pages.RemoveAt(i);
                    result.BlankPagesRemoved++;
                }
            }

            if (pages.Count == 0)
            {
                result.AllPagesBlank = true;
                LogDiag("Process: all pages blank, nothing saved");
                return result;
            }

            // 3. Deskew
            if (s.AllowDeskew)
            {
                foreach (var page in pages)
                {
                    try
                    {
                        double angle = ImageProcessing.EstimateDeskewAngle(page.Bmp);
                        if (angle != 0)
                            page.Replace(ImageProcessing.RotateArbitrary(page.Bmp, angle, s.TextOnlySettings));
                    }
                    catch (OutOfMemoryException)
                    {
                        AddWarning(result, "Zu wenig Arbeitsspeicher für die Schräglagenkorrektur; sie wurde übersprungen.");
                    }
                }
            }

            // 4. Auto-rotate with one OSD engine for the whole batch.
            bool autoOcrLanguage = s.FileFormat == FileFormat.Pdf && s.OcrEnabled && s.OcrLanguage == OcrLanguage.Automatisch;
            if (s.AllowAutoRotate || autoOcrLanguage)
                osd = new OsdDetector();

            if (s.AllowAutoRotate && osd != null)
            {
                if (!osd.IsAvailable)
                    AddWarning(result, "Die automatische Bilddrehung ist nicht verfügbar (osd.traineddata fehlt).");
                else
                    foreach (var page in pages) AutoRotate(page, osd);
            }

            // 5. Highlighter marks (need the color image, so before color conversion).
            bool wantKeywords = s.FileFormat == FileFormat.Pdf && s.AddKeywordToPdf;
            if (wantKeywords && !s.OcrEnabled)
            {
                AddWarning(result, "Markierte Schlüsselwörter benötigen die Texterkennung (durchsuchbare PDF); sie wurden übersprungen.");
                wantKeywords = false;
            }
            if (wantKeywords)
            {
                for (int i = 0; i < pages.Count; i++)
                {
                    if (s.OcrTargetPages == OcrTargetPages.FirstPage && i > 0) break;
                    pages[i].Highlights = ImageProcessing.DetectHighlightedRegions(pages[i].Bmp);
                }
            }

            // 6. Automatic color detection: convert each page to its class.
            if (s.ColorMode == ColorMode.Automatic)
            {
                foreach (var page in pages)
                {
                    var cls = ImageProcessing.ClassifyColor(page.Bmp, s.TextOnlySettings);
                    if (cls == PageColorClass.Gray && ImageProcessing.GetKind(page.Bmp) != PageKind.Gray)
                        page.Replace(ImageProcessing.ToGray8(page.Bmp));
                    else if (cls == PageColorClass.BlackWhite && ImageProcessing.GetKind(page.Bmp) != PageKind.Binary)
                        page.Replace(ImageProcessing.ToBinary(page.Bmp, s.TextOnlySettings));
                }
            }

            result.PageCount = pages.Count;
            Directory.CreateDirectory(targetFolder);

            // 7. Output
            switch (s.FileFormat)
            {
                case FileFormat.Jpeg:
                    GenerateImages(pages, targetFolder, options, ".jpg", result);
                    break;
                case FileFormat.Png:
                    GenerateImages(pages, targetFolder, options, ".png", result);
                    break;
                default:
                    GeneratePdf(pages, targetFolder, options, osd, wantKeywords, result);
                    break;
            }

            // 8. Hand out the final pages.
            if (options.ProduceProcessedPages)
            {
                foreach (var page in pages)
                {
                    result.ProcessedPages.Add(page.Owned ? page.Bmp : ImageProcessing.DeepCopy(page.Bmp));
                    page.Owned = false; // transferred (or not ours)
                }
            }

            LogDiag($"Process: {result.PageCount} page(s), {result.BlankPagesRemoved} blank removed, files=[{string.Join(", ", result.Files)}]");
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
        finally
        {
            foreach (var page in pages)
                if (page.Owned) page.Bmp.Dispose();
            osd?.Dispose();
        }
    }

    private static void AddWarning(ScanOutputResult result, string warning)
    {
        if (!result.Warnings.Contains(warning))
            result.Warnings.Add(warning);
    }

    private void BuildPages(IReadOnlyList<Bitmap> images, bool duplex, List<WorkPage> pages)
    {
        var s = _settings;
        if (!s.CarrierSheetEnabled)
        {
            for (int i = 0; i < images.Count; i++)
                pages.Add(new WorkPage { Bmp = images[i], IsBack = duplex && i % 2 == 1 });
            return;
        }

        var outputSize = GetCarrierOutputSizeMm(s);
        LogDiag($"Carrier sheet: duplex={duplex}, output={s.CarrierSheetOutputSize} {(outputSize.HasValue ? $"{outputSize.Value.widthMm}x{outputSize.Value.heightMm} mm" : "(auto)")}");

        for (int i = 0; i < images.Count; i++)
        {
            Bitmap? composed = null;
            if (duplex && i + 1 < images.Count)
            {
                // Front and back of the carrier sheet form one spread.
                composed = ImageProcessing.ComposeSideBySide(new[] { images[i], images[i + 1] }, outputSize);
                composed = ImageProcessing.RestoreKind(composed, ImageProcessing.GetKind(images[i]), s.TextOnlySettings);
                i++;
            }
            else if (outputSize.HasValue)
            {
                composed = ImageProcessing.ComposeSideBySide(new[] { images[i] }, outputSize);
                composed = ImageProcessing.RestoreKind(composed, ImageProcessing.GetKind(images[i]), s.TextOnlySettings);
            }

            pages.Add(composed != null
                ? new WorkPage { Bmp = composed, Owned = true }
                : new WorkPage { Bmp = images[i] });
        }
    }

    /// <summary>
    /// Output sizes for carrier sheet scans in mm (portrait), keyed by the
    /// <see cref="CarrierSheetOutputSize"/> value name. Unknown values (and "Automatic")
    /// mean "natural size". Add new sizes here.
    /// </summary>
    private static readonly Dictionary<string, (double widthMm, double heightMm)> CarrierOutputSizes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["A3"] = (297, 420),
            ["A4"] = (210, 297),
            ["A5"] = (148, 210),
            ["A6"] = (105, 148),
            ["B4"] = (257, 364),
            ["B4Jis"] = (257, 364),
            ["B5"] = (182, 257),
            ["B5Jis"] = (182, 257),
            ["B6"] = (128, 182),
            ["B6Jis"] = (128, 182),
            ["Letter"] = (215.9, 279.4),
            ["DoubleLetter"] = (279.4, 431.8),
            ["Postcard"] = (100, 148),
            ["Postkarte"] = (100, 148),
            ["BusinessCard"] = (55, 91),
            ["Visitenkarte"] = (55, 91),
            ["CarrierSheet"] = (220, 297),
            ["Traegerblatt"] = (220, 297),
        };

    private static (double widthMm, double heightMm)? GetCarrierOutputSizeMm(ScanSettings s)
    {
        var name = s.CarrierSheetOutputSize.ToString();
        if (name == nameof(CarrierSheetOutputSize.Custom))
        {
            return s.CarrierSheetCustomWidth > 0 && s.CarrierSheetCustomHeight > 0
                ? (s.CarrierSheetCustomWidth, s.CarrierSheetCustomHeight)
                : null;
        }
        return CarrierOutputSizes.TryGetValue(name, out var mm) ? mm : null;
    }

    private void AutoRotate(WorkPage page, OsdDetector osd)
    {
        var osdResult = osd.Detect(page.Bmp);
        if (osdResult == null) return;
        page.Script = osdResult.Value.Script;

        int rotate = osdResult.Value.RotateClockwise;
        if (rotate == 0) return;
        if (osdResult.Value.OrientationConfidence < MinOsdConfidence)
        {
            LogDiag($"AutoRotate: confidence {osdResult.Value.OrientationConfidence:0.0} too low, not rotating");
            return;
        }

        var flipType = rotate switch
        {
            90 => RotateFlipType.Rotate90FlipNone,
            180 => RotateFlipType.Rotate180FlipNone,
            270 => RotateFlipType.Rotate270FlipNone,
            _ => RotateFlipType.RotateNoneFlipNone
        };
        if (flipType == RotateFlipType.RotateNoneFlipNone) return;

        // RotateFlip works in place: never on a caller-owned bitmap.
        if (!page.Owned)
            page.Replace(ImageProcessing.DeepCopy(page.Bmp));
        page.Bmp.RotateFlip(flipType);
        LogDiag($"AutoRotate: rotated {rotate}° clockwise");
    }

    // ───────────────────────── PDF ─────────────────────────

    private void GeneratePdf(List<WorkPage> pages, string targetFolder, ScanOutputOptions options,
        OsdDetector? osd, bool wantKeywords, ScanOutputResult result)
    {
        var s = _settings;

        // Run OCR if enabled
        List<List<OcrWord>>? ocrData = null;
        if (s.OcrEnabled)
        {
            try
            {
                using var ocr = new OcrProcessor(s.OcrLanguage, OcrProcessor.DefaultTessDataPath, osd);
                foreach (var w in ocr.Warnings) AddWarning(result, w);
                if (ocr.IsAvailable)
                {
                    ocrData = new List<List<OcrWord>>();
                    for (int i = 0; i < pages.Count; i++)
                    {
                        bool target = s.OcrTargetPages == OcrTargetPages.AllPages || i == 0;
                        ocrData.Add(target ? ocr.Recognize(pages[i].Bmp, pages[i].Script) : new List<OcrWord>());
                    }
                }
                else
                {
                    AddWarning(result, "Die Texterkennung ist nicht verfügbar (tessdata fehlt).");
                }
            }
            catch (Exception ex)
            {
                LogDiag($"OCR failed: {ex.Message}");
                AddWarning(result, "Die Texterkennung ist fehlgeschlagen.");
            }
        }

        int pagesPerFile = s.PdfSplitMode == PdfSplitMode.SplitByPageCount
            ? Math.Max(1, s.PdfSplitPages)
            : pages.Count;
        int fileCount = (pages.Count + pagesPerFile - 1) / pagesPerFile;
        var paths = ResolveOutputPaths(targetFolder, options, ".pdf", fileCount);

        for (int f = 0; f < fileCount; f++)
        {
            int start = f * pagesPerFile;
            var chunk = pages.Skip(start).Take(pagesPerFile).ToList();
            var chunkOcr = ocrData?.Skip(start).Take(pagesPerFile).ToList();

            var keywords = wantKeywords && chunkOcr != null ? CollectKeywords(chunk, chunkOcr) : new List<string>();
            foreach (var k in keywords)
                if (!result.Keywords.Contains(k)) result.Keywords.Add(k);

            var path = CreatePdfFile(chunk.Select(p => p.Bmp).ToList(), paths[f], chunkOcr, keywords);
            result.Files.Add(path);
        }
    }

    /// <summary>
    /// ScanSnap "Markierte Zeichenfolge als Schlüsselwort": OCR words inside highlighted
    /// regions. FirstMarkedSection = only the first marked region of the document.
    /// </summary>
    private List<string> CollectKeywords(List<WorkPage> pages, List<List<OcrWord>> ocr)
    {
        var keywords = new List<string>();
        for (int p = 0; p < pages.Count && p < ocr.Count; p++)
        {
            var regions = pages[p].Highlights;
            if (regions == null || regions.Count == 0 || ocr[p].Count == 0) continue;

            foreach (var region in regions)
            {
                var words = ocr[p].Where(w =>
                {
                    var inter = Rectangle.Intersect(w.Bounds, region);
                    long wa = (long)w.Bounds.Width * w.Bounds.Height;
                    return !inter.IsEmpty && wa > 0 && (long)inter.Width * inter.Height * 2 >= wa;
                }).ToList();
                if (words.Count == 0) continue;

                int lineHeight = Math.Max(1, (int)words.Average(w => w.Bounds.Height));
                var text = string.Join(" ", words
                    .OrderBy(w => (w.Bounds.Top + w.Bounds.Height / 2) / lineHeight)
                    .ThenBy(w => w.Bounds.Left)
                    .Select(w => w.Text)).Trim(' ', ',', ';', '.', ':');
                if (text.Length == 0 || keywords.Contains(text)) continue;

                keywords.Add(text);
                if (_settings.KeywordTarget == KeywordTarget.FirstMarkedSection)
                    return keywords;
            }
        }
        return keywords;
    }

    private string CreatePdfFile(List<Bitmap> images, string filePath,
        List<List<OcrWord>>? ocrData, List<string> keywords)
    {
        var doc = new PdfDocument();
        try
        {
            // Set password if configured
            if (_settings.PdfUsePassword && !string.IsNullOrEmpty(_settings.PdfPassword))
            {
                doc.SecuritySettings.UserPassword = _settings.PdfPassword;
                doc.SecuritySettings.OwnerPassword = _settings.PdfPassword;
            }

            if (keywords.Count > 0)
                doc.Info.Keywords = string.Join(", ", keywords);
            doc.Info.Creator = "SpeedScan Manager";

            int jpegQuality = GetJpegQuality();
            var fontCache = new Dictionary<double, XFont>();

            for (int i = 0; i < images.Count; i++)
            {
                var bmp = images[i];
                var page = doc.AddPage();
                page.Width = new XUnit(bmp.Width / ImageProcessing.GetDpiX(bmp) * 72);
                page.Height = new XUnit(bmp.Height / ImageProcessing.GetDpiY(bmp) * 72);

                PdfImageEmbedder.DrawFullPage(doc, page, bmp, jpegQuality, i);

                // Embed invisible text layer if OCR data is available for this page
                if (ocrData != null && i < ocrData.Count && ocrData[i].Count > 0)
                {
                    using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                    EmbedInvisibleText(gfx, ocrData[i], bmp.Width, bmp.Height, page.Width, page.Height, fontCache);
                }
            }

            return SaveNew(filePath, stream => doc.Save(stream, false));
        }
        finally
        {
            doc.Dispose();
        }
    }

    /// <summary>
    /// Embeds recognized text as an invisible (transparent) layer over the image.
    /// Text is placed at the correct positions so PDF readers can find it via Ctrl+F.
    /// XGraphics uses a top-left origin like the image, so no Y flip is needed.
    /// </summary>
    private static void EmbedInvisibleText(XGraphics gfx, List<OcrWord> words,
        double imgWidth, double imgHeight, XUnit pdfWidth, XUnit pdfHeight, Dictionary<double, XFont> fontCache)
    {
        // Scale factors from image pixels to PDF points
        double scaleX = pdfWidth.Point / imgWidth;
        double scaleY = pdfHeight.Point / imgHeight;

        foreach (var word in words)
        {
            if (string.IsNullOrWhiteSpace(word.Text)) continue;

            double x = word.Bounds.X * scaleX;
            double y = word.Bounds.Y * scaleY;
            double width = word.Bounds.Width * scaleX;
            double height = word.Bounds.Height * scaleY;

            // Font size ≈ word box height, bucketed to 0.5 pt so fonts can be reused.
            double fontSize = Math.Clamp(Math.Round(height * 2) / 2, 1, 200);
            if (!fontCache.TryGetValue(fontSize, out var font))
            {
                font = new XFont("Arial", fontSize, XFontStyle.Regular, new XPdfFontOptions(PdfFontEncoding.Unicode));
                fontCache[fontSize] = font;
            }

            // Stretch horizontally so the text covers the word box (selection matches the image).
            double measured = gfx.MeasureString(word.Text, font).Width;
            double stretch = measured > 0 ? Math.Clamp(width / measured, 0.2, 5) : 1;

            var state = gfx.Save();
            gfx.TranslateTransform(x, y);
            gfx.ScaleTransform(stretch, 1);
            // Draw text transparently so it's invisible but searchable
            gfx.DrawString(word.Text, font, OcrTextBrush, 0, 0, XStringFormats.TopLeft);
            gfx.Restore(state);
        }
    }

    private int GetJpegQuality() => _settings.CompressionRate switch
    {
        1 => 100,
        2 => 85,
        3 => 70,
        4 => 50,
        5 => 30,
        _ => 70
    };

    internal static ImageCodecInfo GetJpegEncoder()
    {
        var encoders = ImageCodecInfo.GetImageEncoders();
        return encoders.FirstOrDefault(e => e.FormatID == ImageFormat.Jpeg.Guid)
            ?? throw new InvalidOperationException("No JPEG encoder available.");
    }

    // ───────────────────────── JPEG / PNG ─────────────────────────

    private void GenerateImages(List<WorkPage> pages, string targetFolder, ScanOutputOptions options,
        string extension, ScanOutputResult result)
    {
        var paths = ResolveOutputPaths(targetFolder, options, extension, pages.Count);
        bool jpeg = extension == ".jpg";
        var jpegEncoder = jpeg ? GetJpegEncoder() : null;

        using var encoderParams = new EncoderParameters(1);
        encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)GetJpegQuality());

        for (int i = 0; i < pages.Count; i++)
        {
            var bmp = pages[i].Bmp;
            string path;
            if (jpeg)
            {
                // JPEG can't store 1 bpp; write those pages as 8 bpp gray.
                if (ImageProcessing.GetKind(bmp) == PageKind.Binary)
                {
                    using var gray = ImageProcessing.ToGray8(bmp);
                    path = SaveNew(paths[i], stream => gray.Save(stream, jpegEncoder!, encoderParams));
                }
                else
                {
                    path = SaveNew(paths[i], stream => bmp.Save(stream, jpegEncoder!, encoderParams));
                }
            }
            else
            {
                path = SaveNew(paths[i], stream => bmp.Save(stream, ImageFormat.Png));
            }
            result.Files.Add(path);
        }
    }

    // ───────────────────────── File names ─────────────────────────

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    /// <summary>
    /// Replaces characters that are invalid in file names, trims trailing dots/spaces and
    /// avoids reserved device names. Returns "Scan" for empty input.
    /// </summary>
    public static string SanitizeFileName(string? name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder();
        foreach (var c in name ?? "")
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        var result = sb.ToString().Trim().TrimEnd('.', ' ');
        if (result.Length == 0) result = "Scan";
        if (ReservedNames.Contains(result)) result += "_";
        if (result.Length > 180) result = result.Substring(0, 180);
        return result;
    }

    /// <summary>
    /// Returns a path in <paramref name="folder"/> for <paramref name="fileName"/> that does not
    /// exist yet: "name.ext", "name_1.ext", "name_2.ext", ... (the name is sanitized).
    /// Use it for copies as well.
    /// </summary>
    public static string MakeUniquePath(string folder, string fileName)
    {
        var ext = Path.GetExtension(fileName);
        var baseName = SanitizeFileName(Path.GetFileNameWithoutExtension(fileName));
        var path = Path.Combine(folder, baseName + ext);
        for (int k = 1; File.Exists(path) || Directory.Exists(path); k++)
            path = Path.Combine(folder, $"{baseName}_{k}{ext}");
        return path;
    }

    /// <summary>
    /// Generates a file name with full format options from SaveTabContent (without folder
    /// check; for display). Timestamp and OS default use "yyyyMMddHHmmss"; custom names get
    /// the first counter value. Use <see cref="GenerateFileName(FileNameFormatDialog.FormatMode, string, int, string, string)"/>
    /// for a name that is unique in a folder.
    /// </summary>
    public static string GenerateFileName(
        FileNameFormatDialog.FormatMode formatMode,
        string customFileName,
        int counterDigits)
    {
        int digits = Math.Clamp(counterDigits, 1, 9);
        return formatMode == FileNameFormatDialog.FormatMode.Custom
            ? $"{SanitizeFileName(customFileName)}_{1.ToString("D" + digits, CultureInfo.InvariantCulture)}"
            : DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Generates a base file name (without extension) that is unique in <paramref name="folder"/>
    /// for a single file with <paramref name="extension"/>: custom names use the next free
    /// counter, timestamps get "_1", "_2" on collisions.
    /// </summary>
    public static string GenerateFileName(
        FileNameFormatDialog.FormatMode formatMode,
        string customFileName,
        int counterDigits,
        string folder,
        string extension)
    {
        var path = ResolveOutputPaths(folder, new ScanOutputOptions
        {
            FormatMode = formatMode,
            CustomFileName = customFileName,
            CounterDigits = counterDigits
        }, extension, 1)[0];
        return Path.GetFileNameWithoutExtension(path);
    }

    /// <summary>
    /// Returns <paramref name="fileCount"/> non-existing paths. One file: "base.ext";
    /// several files (split PDF, one image per page): "base_001.ext", "base_002.ext", ...
    /// using the counter digits.
    /// </summary>
    private static List<string> ResolveOutputPaths(string folder, ScanOutputOptions options, string extension, int fileCount)
    {
        int digits = Math.Clamp(options.CounterDigits, 1, 9);
        string fmt = "D" + digits;

        List<string> Build(string baseName) => fileCount <= 1
            ? new List<string> { Path.Combine(folder, baseName + extension) }
            : Enumerable.Range(1, fileCount)
                .Select(i => Path.Combine(folder, $"{baseName}_{i.ToString(fmt, CultureInfo.InvariantCulture)}{extension}"))
                .ToList();

        bool AllFree(List<string> paths) => paths.All(p => !File.Exists(p) && !Directory.Exists(p));

        if (options.BaseName == null && options.FormatMode == FileNameFormatDialog.FormatMode.Custom)
        {
            var name = SanitizeFileName(options.CustomFileName);
            for (int n = NextCounter(folder, name, extension); ; n++)
            {
                var paths = Build($"{name}_{n.ToString(fmt, CultureInfo.InvariantCulture)}");
                if (AllFree(paths)) return paths;
            }
        }

        var baseName = options.BaseName != null
            ? SanitizeFileName(options.BaseName)
            : DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        for (int k = 0; ; k++)
        {
            var paths = Build(k == 0 ? baseName : $"{baseName}_{k}");
            if (AllFree(paths)) return paths;
        }
    }

    /// <summary>Highest existing "name_N[...].ext" counter in the folder + 1 (starts at 1).</summary>
    private static int NextCounter(string folder, string name, string extension)
    {
        int max = 0;
        try
        {
            if (!Directory.Exists(folder)) return 1;
            var regex = new Regex("^" + Regex.Escape(name) + @"_(\d+)(?:_\d+)?" + Regex.Escape(extension) + "$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            foreach (var file in Directory.EnumerateFiles(folder, name + "_*" + extension))
            {
                var m = regex.Match(Path.GetFileName(file));
                if (m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int n))
                    max = Math.Max(max, n);
            }
        }
        catch (Exception ex)
        {
            LogDiag($"NextCounter failed: {ex.Message}");
        }
        return max + 1;
    }

    /// <summary>
    /// Writes a new file without ever overwriting: if the path appeared in the meantime,
    /// a unique variant is used. Returns the path written.
    /// </summary>
    private static string SaveNew(string path, Action<Stream> write)
    {
        for (int attempt = 0; ; attempt++)
        {
            FileStream fs;
            try
            {
                fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            }
            catch (IOException) when (File.Exists(path) && attempt < 100)
            {
                path = MakeUniquePath(Path.GetDirectoryName(path)!, Path.GetFileName(path));
                continue;
            }

            try
            {
                using (fs) write(fs);
                return path;
            }
            catch
            {
                try { File.Delete(path); } catch { }
                throw;
            }
        }
    }
}

/// <summary>
/// Writes page images directly as PDF image XObjects:
/// - 1 bpp pages: lossless, 1 bit per pixel, Flate compressed (small and sharp)
/// - gray/color pages: the JPEG produced with the configured quality is embedded as is
///   (DCTDecode, no second encoding), with DeviceGray or DeviceRGB as appropriate.
/// </summary>
internal static class PdfImageEmbedder
{
    public static void DrawFullPage(PdfDocument doc, PdfPage page, Bitmap bmp, int jpegQuality, int index)
    {
        var image = new PdfDictionary(doc);
        image.Elements.SetName("/Type", "/XObject");
        image.Elements.SetName("/Subtype", "/Image");
        image.Elements.SetInteger("/Width", bmp.Width);
        image.Elements.SetInteger("/Height", bmp.Height);

        byte[] data;
        if (bmp.PixelFormat == PixelFormat.Format1bppIndexed)
        {
            data = Pack1Bpp(bmp, out bool zeroIsWhite);
            image.Elements.SetName("/ColorSpace", "/DeviceGray");
            image.Elements.SetInteger("/BitsPerComponent", 1);
            image.Elements.SetName("/Filter", "/FlateDecode");
            if (zeroIsWhite)
                image.Elements["/Decode"] = new PdfArray(doc, new PdfInteger(1), new PdfInteger(0));
        }
        else
        {
            data = EncodeJpeg(bmp, jpegQuality);
            int components = GetJpegComponents(data);
            image.Elements.SetName("/ColorSpace", components == 1 ? "/DeviceGray" : components == 4 ? "/DeviceCMYK" : "/DeviceRGB");
            image.Elements.SetInteger("/BitsPerComponent", 8);
            image.Elements.SetName("/Filter", "/DCTDecode");
        }

        image.CreateStream(data);
        doc.Internals.AddObject(image);

        var resources = page.Resources;
        var xObjects = resources.Elements.GetDictionary("/XObject");
        if (xObjects == null)
        {
            xObjects = new PdfDictionary(doc);
            resources.Elements["/XObject"] = xObjects;
        }
        string name = $"/SsmImg{index}";
        xObjects.Elements.SetReference(name, image);

        var content = page.Contents.AppendContent();
        var ops = string.Format(CultureInfo.InvariantCulture,
            "q {0:0.####} 0 0 {1:0.####} 0 0 cm {2} Do Q\n", page.Width.Point, page.Height.Point, name);
        content.CreateStream(Encoding.ASCII.GetBytes(ops));
    }

    private static byte[] EncodeJpeg(Bitmap bmp, int quality)
    {
        using var ms = new MemoryStream();
        using var encoderParams = new EncoderParameters(1);
        encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)quality);
        bmp.Save(ms, ScanOutputProcessor.GetJpegEncoder(), encoderParams);
        return ms.ToArray();
    }

    /// <summary>Number of color components from the JPEG SOF marker (default 3).</summary>
    private static int GetJpegComponents(byte[] jpeg)
    {
        int i = 2;
        while (i + 9 < jpeg.Length)
        {
            if (jpeg[i] != 0xFF) { i++; continue; }
            byte marker = jpeg[i + 1];
            if (marker is >= 0xC0 and <= 0xCF && marker is not 0xC4 and not 0xC8 and not 0xCC)
                return jpeg[i + 9];
            int len = (jpeg[i + 2] << 8) | jpeg[i + 3];
            i += 2 + len;
        }
        return 3;
    }

    /// <summary>
    /// Packs a 1 bpp bitmap into PDF sample rows (MSB first, rows byte aligned) and compresses
    /// them with zlib. <paramref name="zeroIsWhite"/> is true if palette index 0 is the bright color.
    /// </summary>
    private static byte[] Pack1Bpp(Bitmap bmp, out bool zeroIsWhite)
    {
        var entries = bmp.Palette.Entries;
        zeroIsWhite = entries.Length >= 2 && entries[0].GetBrightness() > entries[1].GetBrightness();

        int w = bmp.Width, h = bmp.Height;
        int rowBytes = (w + 7) / 8;
        var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format1bppIndexed);
        try
        {
            using var output = new MemoryStream();
            using (var z = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                var row = new byte[rowBytes];
                int padBits = rowBytes * 8 - w;
                for (int y = 0; y < h; y++)
                {
                    Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, rowBytes);
                    // Padding bits are ignored by PDF readers, but keep them white/defined.
                    if (padBits > 0)
                    {
                        byte mask = (byte)(0xFF >> (8 - padBits));
                        row[rowBytes - 1] = zeroIsWhite ? (byte)(row[rowBytes - 1] & ~mask) : (byte)(row[rowBytes - 1] | mask);
                    }
                    z.Write(row, 0, rowBytes);
                }
            }
            return output.ToArray();
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }
}
