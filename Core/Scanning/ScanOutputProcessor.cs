using System.Drawing;
using System.Drawing.Imaging;
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;
using System.IO;

namespace SpeedScanManager;

/// <summary>
/// Handles post-processing of scanned images and output file generation (PDF/JPEG).
/// </summary>
internal class ScanOutputProcessor
{
    private readonly ScanSettings _settings;

    public ScanOutputProcessor(ScanSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Full pipeline: post-process images, generate output file(s), return list of created file paths.
    /// </summary>
    public List<string> ProcessAndSave(List<Bitmap> images, string targetFolder,
        FileNameFormatDialog.FormatMode formatMode = FileNameFormatDialog.FormatMode.Timestamp,
        string customFileName = "unbenannt", int counterDigits = 3)
    {
        if (images.Count == 0)
            return new List<string>();

        // Apply post-processing
        var processed = ApplyPostProcessing(images);

        // Generate output based on format
        return _settings.FileFormat switch
        {
            FileFormat.Pdf => GeneratePdf(processed, targetFolder, formatMode, customFileName, counterDigits),
            FileFormat.Jpeg => GenerateJpeg(processed, targetFolder, formatMode, customFileName, counterDigits),
            FileFormat.Png => GeneratePng(processed, targetFolder, formatMode, customFileName, counterDigits),
            _ => GeneratePdf(processed, targetFolder, formatMode, customFileName, counterDigits)
        };
    }

    private List<Bitmap> ApplyPostProcessing(List<Bitmap> images)
    {
        // Carrier sheet mode: merge pairs of consecutive images side-by-side
        if (_settings.CarrierSheetEnabled && images.Count >= 2)
        {
            var merged = new List<Bitmap>();
            for (int i = 0; i < images.Count; i += 2)
            {
                if (i + 1 < images.Count)
                {
                    merged.Add(MergeSideBySide(images[i], images[i + 1]));
                }
                else
                {
                    // Odd image left over – keep as-is
                    merged.Add(images[i]);
                }
            }
            // Replace image list with merged results
            images = merged;
        }

        var result = new List<Bitmap>();

        foreach (var bmp in images)
        {
            var processed = bmp;

            // Deskew
            if (_settings.AllowDeskew)
            {
                processed = ApplyDeskew(processed);
            }

            // Auto-rotate: detect text orientation and rotate if needed
            if (_settings.AllowAutoRotate)
            {
                processed = ApplyAutoRotate(processed);
            }

            // Blank page detection
            if (_settings.AllowDeleteBlankPages && IsBlankPage(processed))
            {
                processed.Dispose();
                continue;
            }

            result.Add(processed);
        }

        return result;
    }

    /// <summary>
    /// Fast blank page detection using LockBits.
    /// Computes the mean brightness and non-white pixel ratio.
    /// A page is blank if >98% of pixels are near-white and mean brightness > 245.
    /// </summary>
    private bool IsBlankPage(Bitmap bmp)
    {
        int width = bmp.Width;
        int height = bmp.Height;

        // Downscale large images for faster sampling
        int maxSampleDim = 500;
        int sampleW = Math.Min(width, maxSampleDim);
        int sampleH = Math.Min(height, (int)(height * (double)sampleW / width));
        if (sampleH < 1) sampleH = 1;

        using var sampled = new Bitmap(sampleW, sampleH, bmp.PixelFormat == System.Drawing.Imaging.PixelFormat.Format8bppIndexed
            ? bmp.PixelFormat : System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(sampled))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
            g.DrawImage(bmp, 0, 0, sampleW, sampleH);
        }

        int totalPixels = sampleW * sampleH;
        long brightnessSum = 0;
        int nonWhitePixels = 0;

        var rect = new Rectangle(0, 0, sampleW, sampleH);
        var data = sampled.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly,
            sampled.PixelFormat);

        try
        {
            int bytesPerPixel = System.Drawing.Bitmap.GetPixelFormatSize(sampled.PixelFormat) / 8;
            int stride = data.Stride;
            var buffer = new byte[stride * sampleH];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

            for (int y = 0; y < sampleH; y++)
            {
                int rowOffset = y * stride;
                for (int x = 0; x < sampleW; x++)
                {
                    int idx = rowOffset + x * bytesPerPixel;
                    // For 24bpp: B, G, R order; for 8bpp: single luminance byte
                    int b, g, r;
                    if (bytesPerPixel == 1)
                    {
                        b = g = r = buffer[idx];
                    }
                    else
                    {
                        b = buffer[idx];
                        g = buffer[idx + 1];
                        r = buffer[idx + 2];
                    }
                    int luminance = (r * 299 + g * 587 + b * 114) / 1000;
                    brightnessSum += luminance;
                    if (luminance < 240)
                        nonWhitePixels++;
                }
            }
        }
        finally
        {
            sampled.UnlockBits(data);
        }

        double meanBrightness = (double)brightnessSum / totalPixels;
        double nonWhiteRatio = (double)nonWhitePixels / totalPixels;

        // Blank if mean brightness is very high and almost no non-white pixels
        return meanBrightness > 245.0 && nonWhiteRatio < 0.02;
    }

    private Bitmap ApplyDeskew(Bitmap bmp)
    {
        // Basic deskew: detect skew angle and rotate
        // This is a simplified implementation – a full deskew would use Hough transform
        // For now, we keep the image as-is (real deskew can be added later)
        return bmp;
    }

    /// <summary>
    /// Auto-rotate: uses Tesseract OSD to detect text orientation (0/90/180/270 degrees)
    /// and rotates the image so text is upright.
    /// Falls back to no rotation if Tesseract or osd.traineddata is not available.
    /// </summary>
    private Bitmap ApplyAutoRotate(Bitmap bmp)
    {
        try
        {
            var tessDataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
            if (!File.Exists(Path.Combine(tessDataPath, "osd.traineddata")))
            {
                return bmp;
            }

            using var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
            using var pix = Tesseract.Pix.LoadFromMemory(ms.ToArray());

            using var osdEngine = new Tesseract.TesseractEngine(tessDataPath, "osd", Tesseract.EngineMode.Default);
            osdEngine.DefaultPageSegMode = Tesseract.PageSegMode.OsdOnly;
            using var page = osdEngine.Process(pix);

            var text = page.GetText();
            if (string.IsNullOrEmpty(text))
                return bmp;

            int rotationDegrees = 0;
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("Orientation in degrees:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmed.Split(':');
                    if (parts.Length >= 2 && int.TryParse(parts[1].Trim(), out int deg))
                    {
                        rotationDegrees = deg;
                        break;
                    }
                }
            }

            if (rotationDegrees == 0)
                return bmp;

            // Use RotateFlip which handles dimension swapping internally
            var flipType = rotationDegrees switch
            {
                90 => RotateFlipType.Rotate90FlipNone,
                180 => RotateFlipType.Rotate180FlipNone,
                270 => RotateFlipType.Rotate270FlipNone,
                _ => RotateFlipType.RotateNoneFlipNone
            };

            if (flipType == RotateFlipType.RotateNoneFlipNone)
                return bmp;

            bmp.RotateFlip(flipType);
            return bmp;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Auto-rotate failed: {ex.Message}");
            return bmp;
        }
    }

    /// <summary>
    /// Merges two bitmaps side-by-side into a single output bitmap.
    /// Both images are scaled to the same height, then placed horizontally adjacent.
    /// </summary>
    private Bitmap MergeSideBySide(Bitmap left, Bitmap right)
    {
        int targetHeight = Math.Max(left.Height, right.Height);

        // Scale both images to the same height while preserving aspect ratio
        int leftWidth = (int)((double)left.Width * targetHeight / left.Height);
        int rightWidth = (int)((double)right.Width * targetHeight / right.Height);

        // Natural merged size
        int naturalWidth = leftWidth + rightWidth;
        int naturalHeight = targetHeight;

        // Apply custom output size if configured
        int outputWidth = naturalWidth;
        int outputHeight = naturalHeight;

        if (_settings.CarrierSheetOutputSize == CarrierSheetOutputSize.Custom)
        {
            // Convert mm to pixels at 96 DPI
            int customWidthPx = (int)(_settings.CarrierSheetCustomWidth * 96.0 / 25.4);
            int customHeightPx = (int)(_settings.CarrierSheetCustomHeight * 96.0 / 25.4);
            if (customWidthPx > 0 && customHeightPx > 0)
            {
                outputWidth = customWidthPx;
                outputHeight = customHeightPx;
            }
        }

        var result = new Bitmap(outputWidth, outputHeight);
        result.SetResolution(96, 96);

        using (var g = Graphics.FromImage(result))
        {
            g.Clear(Color.White);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            // Scale the natural merged image to fill the output canvas
            double scaleX = (double)outputWidth / naturalWidth;
            double scaleY = (double)outputHeight / naturalHeight;
            double scale = Math.Min(scaleX, scaleY);

            int scaledWidth = (int)(naturalWidth * scale);
            int scaledHeight = (int)(naturalHeight * scale);

            // Center the merged image in the output canvas
            int offsetX = (outputWidth - scaledWidth) / 2;
            int offsetY = (outputHeight - scaledHeight) / 2;

            int scaledLeftWidth = (int)(leftWidth * scale);
            int scaledRightWidth = (int)(rightWidth * scale);

            // Draw left image
            g.DrawImage(left, offsetX, offsetY, scaledLeftWidth, scaledHeight);

            // Draw right image to the right of left image
            g.DrawImage(right, offsetX + scaledLeftWidth, offsetY, scaledRightWidth, scaledHeight);
        }

        // Dispose originals since they've been merged
        left.Dispose();
        right.Dispose();

        return result;
    }

    private List<string> GeneratePdf(List<Bitmap> images, string targetFolder,
        FileNameFormatDialog.FormatMode formatMode, string customFileName, int counterDigits)
    {
        var result = new List<string>();
        string baseFileName = GenerateFileName(formatMode, customFileName, counterDigits);

        // Run OCR if enabled
        List<List<OcrWord>>? ocrData = null;
        if (_settings.OcrEnabled)
        {
            try
            {
                using var ocr = new OcrProcessor(_settings.OcrLanguage);
                if (ocr.IsAvailable)
                {
                    ocrData = ocr.RecognizeAll(images, _settings.OcrTargetPages);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("OCR engine not available – tessdata folder may be missing.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OCR failed: {ex.Message}");
            }
        }

        if (_settings.PdfSplitMode == PdfSplitMode.MultiPage)
        {
            // All pages in one PDF
            string filePath = Path.Combine(targetFolder, $"{baseFileName}.pdf");
            CreatePdfFile(images, filePath, ocrData);
            result.Add(filePath);
        }
        else
        {
            // Split by page count
            int pagesPerFile = _settings.PdfSplitPages;
            int fileCounter = 1;
            for (int i = 0; i < images.Count; i += pagesPerFile)
            {
                var chunk = images.Skip(i).Take(pagesPerFile).ToList();
                var chunkOcr = ocrData?.Skip(i).Take(pagesPerFile).ToList();
                string filePath = Path.Combine(targetFolder, $"{baseFileName}_{fileCounter:D3}.pdf");
                CreatePdfFile(chunk, filePath, chunkOcr);
                result.Add(filePath);
                fileCounter++;
            }
        }

        return result;
    }

    private void CreatePdfFile(List<Bitmap> images, string filePath,
        List<List<OcrWord>>? ocrData)
    {
        var doc = new PdfDocument();

        // Set password if configured
        if (_settings.PdfUsePassword && !string.IsNullOrEmpty(_settings.PdfPassword))
        {
            doc.SecuritySettings.UserPassword = _settings.PdfPassword;
            doc.SecuritySettings.OwnerPassword = _settings.PdfPassword;
        }

        // Compression quality based on settings (1-5)
        int jpegQuality = GetJpegQuality();

        for (int i = 0; i < images.Count; i++)
        {
            var bmp = images[i];
            var page = doc.AddPage();
            double dpi = bmp.HorizontalResolution > 0 ? bmp.HorizontalResolution : 96;
            page.Width = new XUnit(bmp.Width / dpi * 72);
            page.Height = new XUnit(bmp.Height / (bmp.VerticalResolution > 0 ? bmp.VerticalResolution : 96) * 72);

            // Draw the image
            using var ms = new MemoryStream();
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)jpegQuality);
            bmp.Save(ms, GetJpegEncoder(), encoderParams);
            ms.Position = 0;

            using var xImg = XImage.FromStream(() => new MemoryStream(ms.ToArray()));
            using var gfx = XGraphics.FromPdfPage(page);
            gfx.DrawImage(xImg, 0, 0, page.Width, page.Height);

            // Embed invisible text layer if OCR data is available for this page
            if (ocrData != null && i < ocrData.Count && ocrData[i].Count > 0)
            {
                EmbedInvisibleText(gfx, ocrData[i], bmp.Width, bmp.Height, page.Width, page.Height);
            }
        }

        doc.Save(filePath);
    }

    /// <summary>
    /// Embeds recognized text as an invisible (transparent) layer over the image.
    /// Text is placed at the correct positions so PDF readers can find it via Ctrl+F.
    /// </summary>
    private void EmbedInvisibleText(XGraphics gfx, List<OcrWord> words,
        double imgWidth, double imgHeight, XUnit pdfWidth, XUnit pdfHeight)
    {
        // Scale factors from image pixels to PDF points
        double scaleX = pdfWidth.Point / imgWidth;
        double scaleY = pdfHeight.Point / imgHeight;

        foreach (var word in words)
        {
            if (string.IsNullOrWhiteSpace(word.Text)) continue;

            // Calculate position in PDF coordinates
            // PDF origin is bottom-left, image origin is top-left, so flip Y
            double x = word.Bounds.X * scaleX;
            double y = pdfHeight.Point - (word.Bounds.Y + word.Bounds.Height) * scaleY;
            double height = word.Bounds.Height * scaleY;

            // Use font size proportional to word height
            double fontSize = height * 0.8;
            if (fontSize < 0.5) fontSize = 0.5;
            var wordFont = new XFont("Arial", fontSize, XFontStyle.Regular);

            // Draw text transparently so it's invisible but searchable
            gfx.DrawString(word.Text, wordFont, XBrushes.Transparent,
                x, y + fontSize, XStringFormats.TopLeft);
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

    private ImageCodecInfo GetJpegEncoder()
    {
        var encoders = ImageCodecInfo.GetImageEncoders();
        return encoders.FirstOrDefault(e => e.FormatID == ImageFormat.Jpeg.Guid)
            ?? throw new InvalidOperationException("No JPEG encoder available.");
    }

    private List<string> GenerateJpeg(List<Bitmap> images, string targetFolder,
        FileNameFormatDialog.FormatMode formatMode, string customFileName, int counterDigits)
    {
        var result = new List<string>();
        string baseFileName = GenerateFileName(formatMode, customFileName, counterDigits);

        int jpegQuality = GetJpegQuality();

        var encoderParams = new EncoderParameters(1);
        encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)jpegQuality);
        var jpegEncoder = GetJpegEncoder();

        for (int i = 0; i < images.Count; i++)
        {
            string suffix = images.Count > 1 ? $"_{i + 1:D3}" : "";
            string filePath = Path.Combine(targetFolder, $"{baseFileName}{suffix}.jpg");
            images[i].Save(filePath, jpegEncoder, encoderParams);
            result.Add(filePath);
        }

        return result;
    }

    private List<string> GeneratePng(List<Bitmap> images, string targetFolder,
        FileNameFormatDialog.FormatMode formatMode, string customFileName, int counterDigits)
    {
        var result = new List<string>();
        string baseFileName = GenerateFileName(formatMode, customFileName, counterDigits);

        for (int i = 0; i < images.Count; i++)
        {
            string suffix = images.Count > 1 ? $"_{i + 1:D3}" : "";
            string filePath = Path.Combine(targetFolder, $"{baseFileName}{suffix}.png");
            images[i].Save(filePath, ImageFormat.Png);
            result.Add(filePath);
        }

        return result;
    }

    /// <summary>
    /// Generates a file name with full format options from SaveTabContent.
    /// </summary>
    public static string GenerateFileName(
        FileNameFormatDialog.FormatMode formatMode,
        string customFileName,
        int counterDigits)
    {
        return formatMode switch
        {
            FileNameFormatDialog.FormatMode.OsDefault => DateTime.Now.ToString("yyyyMMddHHmmss"),
            FileNameFormatDialog.FormatMode.Timestamp => DateTime.Now.ToString("yyyyMMddHHmmss"),
            FileNameFormatDialog.FormatMode.Custom => $"{customFileName}_{new string('0', counterDigits)}",
            _ => DateTime.Now.ToString("yyyyMMddHHmmss")
        };
    }
}
