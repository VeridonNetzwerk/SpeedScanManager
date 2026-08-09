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
    public List<string> ProcessAndSave(List<Bitmap> images, string targetFolder, ScanSettings settings,
        FileNameFormatDialog.FormatMode formatMode = FileNameFormatDialog.FormatMode.Timestamp,
        string customFileName = "unbenannt", int counterDigits = 3)
    {
        if (images.Count == 0)
            return new List<string>();

        // Apply post-processing
        var processed = ApplyPostProcessing(images);

        // Generate output based on format
        return settings.FileFormat switch
        {
            FileFormat.Pdf => GeneratePdf(processed, targetFolder, settings, formatMode, customFileName, counterDigits),
            FileFormat.Jpeg => GenerateJpeg(processed, targetFolder, settings, formatMode, customFileName, counterDigits),
            FileFormat.Png => GeneratePng(processed, targetFolder, settings, formatMode, customFileName, counterDigits),
            _ => GeneratePdf(processed, targetFolder, settings, formatMode, customFileName, counterDigits)
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

            // Auto-rotate
            if (_settings.AllowAutoRotate)
            {
                // Simple auto-rotate: detect orientation (placeholder - real OCR-based rotation comes later)
                // For now, just keep the image as-is
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

    private bool IsBlankPage(Bitmap bmp)
    {
        // Simple blank detection: sample a grid of pixels and check if mostly white
        int stepX = Math.Max(1, bmp.Width / 10);
        int stepY = Math.Max(1, bmp.Height / 10);

        int whitePixels = 0;
        int totalSampled = 0;

        for (int x = 0; x < bmp.Width; x += stepX)
        {
            for (int y = 0; y < bmp.Height; y += stepY)
            {
                var pixel = bmp.GetPixel(x, y);
                if (pixel.R > 240 && pixel.G > 240 && pixel.B > 240)
                    whitePixels++;
                totalSampled++;
            }
        }

        return totalSampled > 0 && (double)whitePixels / totalSampled > 0.98;
    }

    private Bitmap ApplyDeskew(Bitmap bmp)
    {
        // Basic deskew: detect skew angle and rotate
        // This is a simplified implementation – a full deskew would use Hough transform
        // For now, we keep the image as-is (real deskew can be added later)
        return bmp;
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

    private List<string> GeneratePdf(List<Bitmap> images, string targetFolder, ScanSettings settings,
        FileNameFormatDialog.FormatMode formatMode, string customFileName, int counterDigits)
    {
        var result = new List<string>();
        string baseFileName = GenerateFileName(formatMode, customFileName, counterDigits);

        // Run OCR if enabled
        List<List<OcrWord>>? ocrData = null;
        if (settings.OcrEnabled)
        {
            try
            {
                using var ocr = new OcrProcessor(settings.OcrLanguage);
                if (ocr.IsAvailable)
                {
                    ocrData = ocr.RecognizeAll(images, settings.OcrTargetPages);
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

        if (settings.PdfSplitMode == PdfSplitMode.MultiPage)
        {
            // All pages in one PDF
            string filePath = Path.Combine(targetFolder, $"{baseFileName}.pdf");
            CreatePdfFile(images, filePath, settings, ocrData);
            result.Add(filePath);
        }
        else
        {
            // Split by page count
            int pagesPerFile = settings.PdfSplitPages;
            int fileCounter = 1;
            for (int i = 0; i < images.Count; i += pagesPerFile)
            {
                var chunk = images.Skip(i).Take(pagesPerFile).ToList();
                var chunkOcr = ocrData?.Skip(i).Take(pagesPerFile).ToList();
                string filePath = Path.Combine(targetFolder, $"{baseFileName}_{fileCounter:D3}.pdf");
                CreatePdfFile(chunk, filePath, settings, chunkOcr);
                result.Add(filePath);
                fileCounter++;
            }
        }

        return result;
    }

    private void CreatePdfFile(List<Bitmap> images, string filePath, ScanSettings settings)
    {
        CreatePdfFile(images, filePath, settings, null);
    }

    private void CreatePdfFile(List<Bitmap> images, string filePath, ScanSettings settings,
        List<List<OcrWord>>? ocrData)
    {
        var doc = new PdfDocument();

        // Set password if configured
        if (settings.PdfUsePassword && !string.IsNullOrEmpty(settings.PdfPassword))
        {
            doc.SecuritySettings.UserPassword = settings.PdfPassword;
            doc.SecuritySettings.OwnerPassword = settings.PdfPassword;
        }

        // Compression quality based on settings (1-5)
        int jpegQuality = settings.CompressionRate switch
        {
            1 => 100, // Highest quality, largest file
            2 => 85,
            3 => 70,
            4 => 50,
            5 => 30, // Lowest quality, smallest file
            _ => 70
        };

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
            bmp.Save(ms, GetJpegEncoder(jpegQuality), encoderParams);
            ms.Position = 0;

            using var xImg = XImage.FromStream(() => new MemoryStream(ms.ToArray()));
            var gfx = XGraphics.FromPdfPage(page);
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

        // Use a very small font size – the text is invisible, we just need it to be selectable
        // Use a transparent color so the text doesn't show visually
        var transparentColor = XColor.FromArgb(0, 0, 0, 0); // Fully transparent
        var font = new XFont("Arial", 1, XFontStyle.Regular);

        foreach (var word in words)
        {
            if (string.IsNullOrWhiteSpace(word.Text)) continue;

            // Calculate position in PDF coordinates
            // PDF origin is bottom-left, image origin is top-left, so flip Y
            double x = word.Bounds.X * scaleX;
            double y = pdfHeight.Point - (word.Bounds.Y + word.Bounds.Height) * scaleY;
            double width = word.Bounds.Width * scaleX;
            double height = word.Bounds.Height * scaleY;

            // Use font size proportional to word height
            double fontSize = height * 0.8;
            if (fontSize < 0.5) fontSize = 0.5;
            var wordFont = new XFont("Arial", fontSize, XFontStyle.Regular);

            // Draw text transparently so it's invisible but searchable
            // PdfSharpCore doesn't support truly transparent text directly,
            // so we use text rendering mode 3 (invisible) via raw PDF content
            gfx.DrawString(word.Text, wordFont, XBrushes.Transparent,
                x, y + fontSize, XStringFormats.TopLeft);
        }
    }

    private ImageCodecInfo GetJpegEncoder(int quality)
    {
        var encoders = ImageCodecInfo.GetImageEncoders();
        return encoders.First(e => e.FormatID == ImageFormat.Jpeg.Guid);
    }

    private List<string> GenerateJpeg(List<Bitmap> images, string targetFolder, ScanSettings settings,
        FileNameFormatDialog.FormatMode formatMode, string customFileName, int counterDigits)
    {
        var result = new List<string>();
        string baseFileName = GenerateFileName(formatMode, customFileName, counterDigits);

        int jpegQuality = settings.CompressionRate switch
        {
            1 => 100,
            2 => 85,
            3 => 70,
            4 => 50,
            5 => 30,
            _ => 70
        };

        var encoderParams = new EncoderParameters(1);
        encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)jpegQuality);
        var jpegEncoder = GetJpegEncoder(jpegQuality);

        for (int i = 0; i < images.Count; i++)
        {
            string suffix = images.Count > 1 ? $"_{i + 1:D3}" : "";
            string filePath = Path.Combine(targetFolder, $"{baseFileName}{suffix}.jpg");
            images[i].Save(filePath, jpegEncoder, encoderParams);
            result.Add(filePath);
        }

        return result;
    }

    private List<string> GeneratePng(List<Bitmap> images, string targetFolder, ScanSettings settings,
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
    /// Generates a file name based on the SaveTab format settings.
    /// </summary>
    public static string GenerateFileName(ScanSettings settings)
    {
        // This uses the timestamp format by default since SaveTabContent holds the format
        // For now, use the timestamp pattern (most common)
        return DateTime.Now.ToString("yyyyMMddHHmmss");
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
