using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SpeedScanManager;

/// <summary>Color class of a scanned page (for automatic color detection).</summary>
internal enum PageColorClass
{
    Color,
    Gray,
    BlackWhite
}

/// <summary>Storage kind of a page bitmap.</summary>
internal enum PageKind
{
    Color,  // 24/32 bpp
    Gray,   // 8 bpp indexed, gray palette
    Binary  // 1 bpp indexed
}

/// <summary>
/// Pixel-level helpers for scan post-processing: copying, analysis on downscaled
/// samples (blank page, skew, color class, highlighter marks) and conversions.
/// All methods leave their input bitmaps untouched and return new bitmaps.
/// Analysis always runs on small 24 bpp samples to keep memory low (x86 process).
/// </summary>
internal static class ImageProcessing
{
    private static void LogDiag(string msg) => DiagLog.Write(msg);

    // ───────────────────────── Basics ─────────────────────────

    public static PageKind GetKind(Bitmap bmp) => bmp.PixelFormat switch
    {
        PixelFormat.Format1bppIndexed => PageKind.Binary,
        PixelFormat.Format8bppIndexed => PageKind.Gray,
        _ => PageKind.Color
    };

    public static float GetDpiX(Bitmap bmp) => bmp.HorizontalResolution > 1 ? bmp.HorizontalResolution : 96f;
    public static float GetDpiY(Bitmap bmp) => bmp.VerticalResolution > 1 ? bmp.VerticalResolution : GetDpiX(bmp);

    /// <summary>
    /// Creates an independent pixel copy with the same pixel format, palette and dpi
    /// (no reference to a source stream remains).
    /// </summary>
    public static Bitmap DeepCopy(Bitmap src)
    {
        var fmt = src.PixelFormat;
        bool directCopy = fmt is PixelFormat.Format1bppIndexed or PixelFormat.Format4bppIndexed
            or PixelFormat.Format8bppIndexed or PixelFormat.Format24bppRgb
            or PixelFormat.Format32bppRgb or PixelFormat.Format32bppArgb;

        if (!directCopy)
        {
            // Unusual formats (16 bpp, 48 bpp, ...): render to 24 bpp.
            var conv = new Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb);
            conv.SetResolution(src.HorizontalResolution, src.VerticalResolution);
            using var g = Graphics.FromImage(conv);
            g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height));
            return conv;
        }

        var dst = new Bitmap(src.Width, src.Height, fmt);
        try
        {
            if ((fmt & PixelFormat.Indexed) != 0)
                dst.Palette = src.Palette;
            dst.SetResolution(src.HorizontalResolution, src.VerticalResolution);

            var rect = new Rectangle(0, 0, src.Width, src.Height);
            var sd = src.LockBits(rect, ImageLockMode.ReadOnly, fmt);
            try
            {
                var dd = dst.LockBits(rect, ImageLockMode.WriteOnly, fmt);
                try
                {
                    int rowBytes = Math.Min(Math.Abs(sd.Stride), Math.Abs(dd.Stride));
                    var row = new byte[rowBytes];
                    for (int y = 0; y < src.Height; y++)
                    {
                        Marshal.Copy(sd.Scan0 + y * sd.Stride, row, 0, rowBytes);
                        Marshal.Copy(row, 0, dd.Scan0 + y * dd.Stride, rowBytes);
                    }
                }
                finally { dst.UnlockBits(dd); }
            }
            finally { src.UnlockBits(sd); }
            return dst;
        }
        catch
        {
            dst.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Renders a downscaled 24 bpp sample of the image at roughly <paramref name="targetDpi"/>
    /// (never upscaled, at most <paramref name="maxPixels"/> pixels).
    /// Returns the sample and the scale factor (sample px / source px).
    /// </summary>
    public static Bitmap CreateSample(Bitmap src, double targetDpi, int maxPixels, out double scale)
    {
        double dpi = GetDpiX(src);
        scale = Math.Min(1.0, targetDpi / dpi);
        double pixels = (double)src.Width * src.Height * scale * scale;
        if (pixels > maxPixels)
            scale *= Math.Sqrt(maxPixels / pixels);

        int w = Math.Max(1, (int)Math.Round(src.Width * scale));
        int h = Math.Max(1, (int)Math.Round(src.Height * scale));
        var sample = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        sample.SetResolution((float)(dpi * scale), (float)(GetDpiY(src) * scale));
        using (var g = Graphics.FromImage(sample))
        {
            // High quality modes pre-filter (area average) when downscaling, which
            // also suppresses single-pixel noise in the analysis.
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighSpeed;
            using var attrs = new ImageAttributes();
            attrs.SetWrapMode(WrapMode.TileFlipXY); // avoid dark/transparent borders
            g.DrawImage(src, new Rectangle(0, 0, w, h), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attrs);
        }
        return sample;
    }

    /// <summary>Reads a 24 bpp bitmap into separate R, G, B arrays plus luminance.</summary>
    private static void ReadRgb(Bitmap bmp24, out byte[] r, out byte[] g, out byte[] b, out byte[] lum)
    {
        int w = bmp24.Width, h = bmp24.Height;
        r = new byte[w * h]; g = new byte[w * h]; b = new byte[w * h]; lum = new byte[w * h];
        var data = bmp24.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            var row = new byte[Math.Abs(data.Stride)];
            for (int y = 0; y < h; y++)
            {
                Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, row.Length);
                int o = y * w;
                for (int x = 0; x < w; x++)
                {
                    int i = x * 3;
                    byte bb = row[i], gg = row[i + 1], rr = row[i + 2];
                    r[o + x] = rr; g[o + x] = gg; b[o + x] = bb;
                    lum[o + x] = (byte)((rr * 299 + gg * 587 + bb * 114) / 1000);
                }
            }
        }
        finally { bmp24.UnlockBits(data); }
    }

    private static byte[] ReadLuminance(Bitmap bmp24)
    {
        ReadRgb(bmp24, out _, out _, out _, out var lum);
        return lum;
    }

    private static int Percentile(int[] histogram, long total, double p)
    {
        long target = (long)(total * p);
        long acc = 0;
        for (int i = 0; i < 256; i++)
        {
            acc += histogram[i];
            if (acc > target) return i;
        }
        return 255;
    }

    // ───────────────────────── Blank page detection ─────────────────────────

    /// <summary>
    /// Adaptive blank page detection. Estimates the paper brightness (median of the inner
    /// area) and counts clearly darker pixels in 16x16 cells at ~100 dpi. Isolated specks
    /// (dust, sensor noise) are ignored; anything like a single address line, a signature
    /// or a stamp keeps the page. Borders (punch holes, scanner shadows) are ignored.
    /// Conservative by design: when in doubt the page is kept.
    /// </summary>
    public static bool IsBlankPage(Bitmap bmp)
    {
        using var sample = CreateSample(bmp, 100, 2_500_000, out double scale);
        int w = sample.Width, h = sample.Height;
        var lum = ReadLuminance(sample);

        int x0 = (int)(w * 0.075), x1 = w - x0;
        int y0 = (int)(h * 0.04), y1 = h - y0;
        if (x1 - x0 < 16 || y1 - y0 < 16) return false;

        var hist = new int[256];
        long n = 0;
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++) { hist[lum[y * w + x]]++; n++; }

        int background = Percentile(hist, n, 0.5);
        if (background < 100)
        {
            LogDiag($"IsBlankPage: dark page (bg={background}) -> not blank");
            return false;
        }

        int threshold = background - 45;
        const int cell = 16;
        double sampleDpi = GetDpiX(bmp) * scale;
        double areaFactor = Math.Max(0.25, (sampleDpi / 100.0) * (sampleDpi / 100.0));
        int minPerCell = Math.Max(2, (int)Math.Round(4 * areaFactor));
        long inkTotal = 0;
        int contentCells = 0;

        for (int cy = y0; cy < y1; cy += cell)
        {
            for (int cx = x0; cx < x1; cx += cell)
            {
                int count = 0;
                int ey = Math.Min(cy + cell, y1), ex = Math.Min(cx + cell, x1);
                for (int y = cy; y < ey; y++)
                {
                    int o = y * w;
                    for (int x = cx; x < ex; x++)
                        if (lum[o + x] < threshold) count++;
                }
                if (count >= minPerCell)
                {
                    contentCells++;
                    inkTotal += count;
                }
            }
        }

        bool blank = inkTotal < 30 * areaFactor;
        LogDiag($"IsBlankPage: bg={background}, sampleDpi={sampleDpi:0}, contentCells={contentCells}, ink={inkTotal} -> blank={blank}");
        return blank;
    }

    // ───────────────────────── Deskew ─────────────────────────

    /// <summary>
    /// Estimates the text skew in degrees (range ±5°) with a projection profile on a
    /// downscaled, binarised copy: the angle that maximises the variance of the row
    /// histogram of ink pixels. Returns the angle the image must be rotated by
    /// (GDI+ convention, positive = clockwise) to straighten the text, or 0.
    /// </summary>
    public static double EstimateDeskewAngle(Bitmap bmp)
    {
        using var sample = CreateSample(bmp, 120, 3_000_000, out _);
        int w = sample.Width, h = sample.Height;
        var lum = ReadLuminance(sample);

        int x0 = (int)(w * 0.03), x1 = w - x0, y0 = (int)(h * 0.03), y1 = h - y0;
        var hist = new int[256];
        long n = 0;
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++) { hist[lum[y * w + x]]++; n++; }
        int background = Percentile(hist, n, 0.5);
        int threshold = Math.Max(30, background - 70);

        var xs = new List<int>();
        var ys = new List<int>();
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
                if (lum[y * w + x] < threshold) { xs.Add(x); ys.Add(y); }

        if (xs.Count < 300)
        {
            LogDiag($"Deskew: too little ink ({xs.Count} px), skipped");
            return 0;
        }

        // Limit the work for very dark/dense pages.
        int step = Math.Max(1, xs.Count / 150_000);
        double cx = w / 2.0;
        int bins = h * 2 + 64;

        double Score(double angleDeg)
        {
            double t = Math.Tan(angleDeg * Math.PI / 180.0);
            var rows = new int[bins];
            for (int i = 0; i < xs.Count; i += step)
            {
                int r = (int)(ys[i] - (xs[i] - cx) * t) + h / 2 + 32;
                if (r >= 0 && r < bins) rows[r]++;
            }
            double s = 0;
            foreach (var c in rows) s += (double)c * c;
            return s;
        }

        double best = 0, bestScore = Score(0), zeroScore = bestScore;
        for (double a = -5.0; a <= 5.0001; a += 0.2)
        {
            double s = Score(a);
            if (s > bestScore) { bestScore = s; best = a; }
        }
        // Refine around the coarse maximum.
        double coarse = best;
        for (double a = coarse - 0.2; a <= coarse + 0.2001; a += 0.05)
        {
            double s = Score(a);
            if (s > bestScore) { bestScore = s; best = a; }
        }

        double gain = zeroScore > 0 ? bestScore / zeroScore : 1;
        LogDiag($"Deskew: lines tilted {best:0.00}°, gain={gain:0.000}, ink={xs.Count}");
        if (Math.Abs(best) <= 0.3 || gain < 1.03)
            return 0;

        // Text rows satisfy y = c + x*tan(a) (y down): rotate by -a to make them horizontal.
        return -best;
    }

    /// <summary>
    /// Rotates by an arbitrary angle (degrees, GDI+ clockwise) around the centre, keeping
    /// the page size and filling uncovered corners with white. Keeps the page kind
    /// (binary/gray/color) and dpi.
    /// </summary>
    public static Bitmap RotateArbitrary(Bitmap src, double angleDeg, bool textOnly)
    {
        var kind = GetKind(src);
        var rotated = new Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb);
        rotated.SetResolution(src.HorizontalResolution, src.VerticalResolution);
        using (var g = Graphics.FromImage(rotated))
        {
            g.Clear(Color.White);
            g.InterpolationMode = kind == PageKind.Color ? InterpolationMode.HighQualityBicubic : InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TranslateTransform(src.Width / 2f, src.Height / 2f);
            g.RotateTransform((float)angleDeg);
            g.TranslateTransform(-src.Width / 2f, -src.Height / 2f);
            g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height));
        }
        return RestoreKind(rotated, kind, textOnly);
    }

    /// <summary>
    /// Converts a 24 bpp working bitmap back to the given kind (disposes the input if converted).
    /// </summary>
    public static Bitmap RestoreKind(Bitmap bmp24, PageKind kind, bool textOnly)
    {
        if (kind == PageKind.Color) return bmp24;
        try
        {
            return kind == PageKind.Gray ? ToGray8(bmp24) : ToBinary(bmp24, textOnly, fixedThreshold: 128);
        }
        finally
        {
            bmp24.Dispose();
        }
    }

    // ───────────────────────── Color classification & conversion ─────────────────────────

    /// <summary>
    /// Classifies a page for "Automatische Farberkennung": color if a noticeable amount of
    /// clearly saturated pixels exists, gray if larger areas consist of mid tones (photos,
    /// gradients), otherwise black &amp; white. "Nur für Textdokumente" biases towards B/W.
    /// </summary>
    public static PageColorClass ClassifyColor(Bitmap bmp, bool textOnly)
    {
        if (GetKind(bmp) == PageKind.Binary) return PageColorClass.BlackWhite;

        using var sample = CreateSample(bmp, 150, 3_000_000, out _);
        int w = sample.Width, h = sample.Height;
        ReadRgb(sample, out var r, out var g, out var b, out var lum);

        var hist = new int[256];
        foreach (var l in lum) hist[l]++;
        int background = Percentile(hist, lum.Length, 0.5);

        long colored = 0;
        if (GetKind(bmp) == PageKind.Color)
        {
            for (int i = 0; i < lum.Length; i++)
            {
                int max = Math.Max(r[i], Math.Max(g[i], b[i]));
                int min = Math.Min(r[i], Math.Min(g[i], b[i]));
                if (max - min > 48 && lum[i] > 40 && lum[i] < 245)
                    colored++;
            }
        }
        double colorRatio = (double)colored / lum.Length;

        // Gray if a share of 16x16 cells is dominated by mid tones.
        const int cell = 16;
        int midHigh = Math.Max(90, background - 40);
        int cells = 0, grayCells = 0;
        for (int cy = 0; cy + cell <= h; cy += cell)
        {
            for (int cx = 0; cx + cell <= w; cx += cell)
            {
                int mid = 0;
                for (int y = cy; y < cy + cell; y++)
                {
                    int o = y * w;
                    for (int x = cx; x < cx + cell; x++)
                    {
                        int l = lum[o + x];
                        if (l > 80 && l < midHigh) mid++;
                    }
                }
                cells++;
                if (mid > cell * cell * 0.6) grayCells++;
            }
        }
        double grayRatio = cells > 0 ? (double)grayCells / cells : 0;

        double colorLimit = textOnly ? 0.004 : 0.002;
        double grayLimit = textOnly ? 0.06 : 0.02;
        var result = colorRatio > colorLimit ? PageColorClass.Color
            : grayRatio > grayLimit ? PageColorClass.Gray
            : PageColorClass.BlackWhite;
        LogDiag($"ClassifyColor: bg={background}, colorRatio={colorRatio:0.0000}, grayCells={grayRatio:0.000}, textOnly={textOnly} -> {result}");
        return result;
    }

    private static Bitmap NewIndexed(int w, int h, PixelFormat fmt, Bitmap dpiSource)
    {
        var bmp = new Bitmap(w, h, fmt);
        bmp.SetResolution(dpiSource.HorizontalResolution, dpiSource.VerticalResolution);
        var pal = bmp.Palette;
        if (fmt == PixelFormat.Format8bppIndexed)
        {
            for (int i = 0; i < 256; i++) pal.Entries[i] = Color.FromArgb(i, i, i);
        }
        else
        {
            pal.Entries[0] = Color.Black;
            pal.Entries[1] = Color.White;
        }
        bmp.Palette = pal;
        return bmp;
    }

    /// <summary>
    /// Calls <paramref name="rowAction"/> with the luminance of every row of any bitmap
    /// (24/32 bpp, 8/1 bpp indexed; other formats via a 24 bpp copy).
    /// </summary>
    private static void ForEachLuminanceRow(Bitmap src, Action<int, byte[]> rowAction)
    {
        int w = src.Width, h = src.Height;
        var fmt = src.PixelFormat;
        int bpp = fmt switch
        {
            PixelFormat.Format24bppRgb => 3,
            PixelFormat.Format32bppRgb or PixelFormat.Format32bppArgb => 4,
            PixelFormat.Format8bppIndexed => 1,
            PixelFormat.Format1bppIndexed => 0,
            _ => -1
        };

        if (bpp < 0)
        {
            using var tmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(tmp))
                g.DrawImage(src, new Rectangle(0, 0, w, h));
            ForEachLuminanceRow(tmp, rowAction);
            return;
        }

        byte[]? palLum = null;
        if (bpp <= 1)
        {
            var entries = src.Palette.Entries;
            palLum = new byte[256];
            for (int i = 0; i < entries.Length && i < 256; i++)
                palLum[i] = (byte)((entries[i].R * 299 + entries[i].G * 587 + entries[i].B * 114) / 1000);
        }

        var data = src.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, fmt);
        try
        {
            var raw = new byte[Math.Abs(data.Stride)];
            var lumRow = new byte[w];
            for (int y = 0; y < h; y++)
            {
                Marshal.Copy(data.Scan0 + y * data.Stride, raw, 0, raw.Length);
                switch (bpp)
                {
                    case 0:
                        for (int x = 0; x < w; x++)
                            lumRow[x] = palLum![(raw[x >> 3] >> (7 - (x & 7))) & 1];
                        break;
                    case 1:
                        for (int x = 0; x < w; x++)
                            lumRow[x] = palLum![raw[x]];
                        break;
                    default:
                        for (int x = 0, i = 0; x < w; x++, i += bpp)
                            lumRow[x] = (byte)((raw[i + 2] * 299 + raw[i + 1] * 587 + raw[i] * 114) / 1000);
                        break;
                }
                rowAction(y, lumRow);
            }
        }
        finally { src.UnlockBits(data); }
    }

    /// <summary>Converts any bitmap to 8 bpp grayscale (new bitmap).</summary>
    public static Bitmap ToGray8(Bitmap src)
    {
        int w = src.Width, h = src.Height;
        var dst = NewIndexed(w, h, PixelFormat.Format8bppIndexed, src);
        var dd = dst.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
        try
        {
            ForEachLuminanceRow(src, (y, lumRow) => Marshal.Copy(lumRow, 0, dd.Scan0 + y * dd.Stride, w));
        }
        finally { dst.UnlockBits(dd); }
        return dst;
    }

    /// <summary>
    /// Converts any bitmap to 1 bpp black &amp; white (new bitmap, palette 0 = black, 1 = white).
    /// The threshold adapts to the paper brightness; "Nur für Textdokumente" uses a higher
    /// threshold so faint strokes (pencil, handwriting) stay visible.
    /// </summary>
    public static Bitmap ToBinary(Bitmap src, bool textOnly, int? fixedThreshold = null)
    {
        int threshold = fixedThreshold ?? EstimateBinaryThreshold(src, textOnly);
        int w = src.Width, h = src.Height;
        var dst = NewIndexed(w, h, PixelFormat.Format1bppIndexed, src);
        var dd = dst.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format1bppIndexed);
        try
        {
            var packed = new byte[(w + 7) / 8];
            ForEachLuminanceRow(src, (y, lumRow) =>
            {
                Array.Clear(packed);
                for (int x = 0; x < w; x++)
                    if (lumRow[x] >= threshold)
                        packed[x >> 3] |= (byte)(0x80 >> (x & 7)); // 1 = white
                Marshal.Copy(packed, 0, dd.Scan0 + y * dd.Stride, packed.Length);
            });
        }
        finally { dst.UnlockBits(dd); }
        return dst;
    }

    private static int EstimateBinaryThreshold(Bitmap src, bool textOnly)
    {
        using var sample = CreateSample(src, 100, 2_000_000, out _);
        var lum = ReadLuminance(sample);
        var hist = new int[256];
        foreach (var l in lum) hist[l]++;
        int background = Percentile(hist, lum.Length, 0.5);
        int ink = Percentile(hist, lum.Length, 0.02);
        if (background - ink < 80) ink = Math.Max(0, background - 160);

        double factor = textOnly ? 0.75 : 0.55;
        int threshold = ink + (int)((background - ink) * factor);
        threshold = Math.Min(threshold, background - (textOnly ? 30 : 40));
        threshold = Math.Clamp(threshold, 60, 230);
        LogDiag($"ToBinary: bg={background}, ink={ink}, textOnly={textOnly} -> threshold={threshold}");
        return threshold;
    }

    // ───────────────────────── Highlighter marks ─────────────────────────

    /// <summary>
    /// Finds regions marked with a highlighter (light, saturated yellow/green/pink/blue/orange)
    /// on a color page. Returns rectangles in source pixel coordinates, in reading order.
    /// </summary>
    public static List<Rectangle> DetectHighlightedRegions(Bitmap bmp)
    {
        var result = new List<Rectangle>();
        if (GetKind(bmp) != PageKind.Color) return result;

        using var sample = CreateSample(bmp, 100, 2_500_000, out double scale);
        int w = sample.Width, h = sample.Height;
        ReadRgb(sample, out var r, out var g, out var b, out var lum);

        // Cell grid (4x4 px ≈ 1 mm at 100 dpi): a cell is marked if enough pixels are highlighter colored.
        const int cell = 4;
        int cw = (w + cell - 1) / cell, ch = (h + cell - 1) / cell;
        var counts = new int[cw * ch];
        for (int y = 0; y < h; y++)
        {
            int o = y * w;
            for (int x = 0; x < w; x++)
            {
                int i = o + x;
                int max = Math.Max(r[i], Math.Max(g[i], b[i]));
                int min = Math.Min(r[i], Math.Min(g[i], b[i]));
                if (lum[i] > 120 && max > 170 && max - min > 60)
                    counts[(y / cell) * cw + x / cell]++;
            }
        }

        var marked = new bool[cw * ch];
        for (int i = 0; i < counts.Length; i++) marked[i] = counts[i] >= 6;

        // Connected components (8-neighbourhood, bridging 1-cell horizontal gaps between words).
        var visited = new bool[cw * ch];
        var stack = new Stack<int>();
        double cellsPerMm = 1.0 / (cell / (GetDpiX(bmp) * scale / 25.4));
        int minWidthCells = (int)Math.Ceiling(6 * cellsPerMm);
        int minHeightCells = (int)Math.Ceiling(2 * cellsPerMm);

        for (int start = 0; start < marked.Length; start++)
        {
            if (!marked[start] || visited[start]) continue;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1, area = 0;
            stack.Push(start);
            visited[start] = true;
            while (stack.Count > 0)
            {
                int c = stack.Pop();
                int cxp = c % cw, cyp = c / cw;
                area++;
                minX = Math.Min(minX, cxp); maxX = Math.Max(maxX, cxp);
                minY = Math.Min(minY, cyp); maxY = Math.Max(maxY, cyp);
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int nx = cxp + dx, ny = cyp + dy;
                        if (nx < 0 || ny < 0 || nx >= cw || ny >= ch) continue;
                        int ni = ny * cw + nx;
                        if (marked[ni] && !visited[ni]) { visited[ni] = true; stack.Push(ni); }
                    }
                }
            }

            int widthCells = maxX - minX + 1, heightCells = maxY - minY + 1;
            if (widthCells < minWidthCells || heightCells < minHeightCells) continue;
            // Highlights are filled bands; sparse color (photos, colored text) is rejected.
            if (area < widthCells * heightCells * 0.35) continue;

            double inv = 1.0 / scale;
            result.Add(new Rectangle(
                (int)(minX * cell * inv), (int)(minY * cell * inv),
                (int)(widthCells * cell * inv), (int)(heightCells * cell * inv)));
        }

        result.Sort((a, c) => Math.Abs(a.Top - c.Top) > Math.Min(a.Height, c.Height) / 2
            ? a.Top.CompareTo(c.Top) : a.Left.CompareTo(c.Left));
        LogDiag($"Highlights: {result.Count} region(s)");
        return result;
    }

    // ───────────────────────── Canvas helpers ─────────────────────────

    /// <summary>
    /// Draws <paramref name="sources"/> side by side (scaled to a common height at the
    /// first image's dpi) onto a new white 24 bpp canvas. With an output size in mm, the
    /// canvas has that physical size at the source dpi and the content is fitted and centred.
    /// </summary>
    public static Bitmap ComposeSideBySide(IReadOnlyList<Bitmap> sources, (double widthMm, double heightMm)? outputSizeMm)
    {
        var first = sources[0];
        float dpiX = GetDpiX(first), dpiY = GetDpiY(first);

        // Natural size: same physical height for all parts, expressed at the first image's dpi.
        var widths = new int[sources.Count];
        int targetHeight = 0;
        foreach (var s in sources)
            targetHeight = Math.Max(targetHeight, (int)Math.Round(s.Height / GetDpiY(s) * dpiY));
        for (int i = 0; i < sources.Count; i++)
        {
            var s = sources[i];
            double physH = s.Height / GetDpiY(s), physW = s.Width / GetDpiX(s);
            widths[i] = (int)Math.Round(physW / physH * targetHeight / dpiY * dpiX);
        }
        int naturalWidth = widths.Sum();
        int naturalHeight = targetHeight;

        int outW = naturalWidth, outH = naturalHeight;
        if (outputSizeMm is { } mm && mm.widthMm > 0 && mm.heightMm > 0)
        {
            double wmm = mm.widthMm, hmm = mm.heightMm;
            // Use the orientation that matches the content.
            if ((naturalWidth > naturalHeight) != (wmm > hmm)) (wmm, hmm) = (hmm, wmm);
            outW = (int)Math.Round(wmm / 25.4 * dpiX);
            outH = (int)Math.Round(hmm / 25.4 * dpiY);
        }

        var result = new Bitmap(Math.Max(1, outW), Math.Max(1, outH), PixelFormat.Format24bppRgb);
        result.SetResolution(dpiX, dpiY);
        using var g = Graphics.FromImage(result);
        g.Clear(Color.White);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        double scale = Math.Min((double)outW / naturalWidth, (double)outH / naturalHeight);
        int scaledHeight = (int)Math.Round(naturalHeight * scale);
        int offsetX = (outW - (int)Math.Round(naturalWidth * scale)) / 2;
        int offsetY = (outH - scaledHeight) / 2;
        int x = offsetX;
        for (int i = 0; i < sources.Count; i++)
        {
            int sw = (int)Math.Round(widths[i] * scale);
            g.DrawImage(sources[i], new Rectangle(x, offsetY, sw, scaledHeight));
            x += sw;
        }
        return result;
    }
}
