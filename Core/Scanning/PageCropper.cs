using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SpeedScanManager;

/// <summary>
/// Cuts a scanned image down to the sheet of paper. Drivers without automatic border detection
/// deliver the full scan frame: the scanner's grey background beside the paper and white filler
/// rows after its end.
///
/// The background is only recognised as an even, neutral grey band along the left or right image
/// edge that is clearly darker than the paper. Edges are fitted as straight lines; slanted
/// remains of the background are painted in the paper colour, so no paper content is cut off.
/// If anything is unclear the image stays as it is.
/// </summary>
internal static class PageCropper
{
    private const double AnalysisDpi = 150;
    private const double MinPaperMm = 15;

    private static void LogDiag(string msg) => DiagLog.Write(msg);

    /// <summary>Edge line: position = A + B * t (t = row for left/right, column for top/bottom).</summary>
    private readonly record struct Edge(double A, double B)
    {
        public double At(double t) => A + B * t;
    }

    /// <summary>Returns the cropped page as a new bitmap, or null if there is nothing to crop.</summary>
    public static Bitmap? Crop(Bitmap src)
        => ImageProcessing.GetKind(src) == PageKind.Binary ? CropBinary(src) : CropTone(src);

    // ───────────────────────── Color and gray ─────────────────────────

    private static Bitmap? CropTone(Bitmap src)
    {
        using var sample = ImageProcessing.CreateSample(src, AnalysisDpi, 6_000_000, out double scale);
        int w = sample.Width, h = sample.Height;
        double mm = ImageProcessing.GetDpiX(src) * scale / 25.4;
        ImageProcessing.ReadRgb(sample, out var r, out var g, out var b, out var lum);

        // 1. White filler rows after the end of the paper (pure white over the full width).
        int bottom = h;
        while (bottom > 0 && IsWhiteRow(lum, w, bottom - 1, 254)) bottom--;
        if (bottom < MinPaperMm * mm) return null;
        bool filler = h - bottom >= 2 * mm;
        if (!filler) bottom = h;

        // 2. Paper color (median of the inner area) and scanner background (edge bands).
        Color paper = MedianColor(r, g, b, w, (int)(w * 0.25), (int)(w * 0.75), (int)(bottom * 0.1), (int)(bottom * 0.9));
        int paperLum = Lum(paper);
        if (filler && paperLum >= 253) // overexposed paper looks the same as filler
        {
            filler = false;
            bottom = h;
        }
        else if (filler && paperLum <= 248)
        {
            // The last filler rows can be slightly off white (JPEG transfer, downscaling).
            while (bottom > MinPaperMm * mm && IsWhiteRow(lum, w, bottom - 1, 251)) bottom--;
        }

        int band = Math.Max(3, (int)(1.0 * mm));
        var leftColor = BackgroundBand(r, g, b, lum, w, bottom, 0, band, paperLum);
        var rightColor = BackgroundBand(r, g, b, lum, w, bottom, w - band, w, paperLum);
        Color? back = leftColor ?? rightColor;

        double x0 = 0, x1 = w, y0 = 0, y1 = bottom;
        Edge? left = null, right = null, top = null, low = null;
        if (back is { } bg)
        {
            int bgLum = Lum(bg);
            int brighter = Math.Max(10, (paperLum - bgLum) / 2);
            int diffRg = bg.R - bg.G, diffBg = bg.B - bg.G;

            bool IsPaper(int x, int y)
            {
                int i = y * w + x;
                int d = lum[i] - bgLum;
                return d >= brighter || d <= -35
                    || Math.Abs(r[i] - g[i] - diffRg) > 28 || Math.Abs(b[i] - g[i] - diffBg) > 28;
            }

            const int run = 4; // ignore dust on the background
            bool Run(int x, int y, int dx, int dy)
            {
                for (int k = 0; k < run; k++)
                {
                    int px = x + k * dx, py = y + k * dy;
                    if (px < 0 || py < 0 || px >= w || py >= bottom || !IsPaper(px, py)) return false;
                }
                return true;
            }

            int step = Math.Max(1, bottom / 300);
            var lp = new List<(double t, double v)>();
            var rp = new List<(double t, double v)>();
            for (int y = 0; y < bottom; y += step)
            {
                if (leftColor != null)
                    for (int x = 0; x < w - run; x++)
                        if (Run(x, y, 1, 0)) { lp.Add((y, x)); break; }
                if (rightColor != null)
                    for (int x = w - 1; x >= run; x--)
                        if (Run(x, y, -1, 0)) { rp.Add((y, x + 1)); break; }
            }
            if (leftColor != null) left = Fit(lp, 1.0 * mm);
            if (rightColor != null) right = Fit(rp, 1.0 * mm);
            if (left is { } l0) x0 = Math.Max(0, Math.Min(l0.At(0), l0.At(bottom)));
            if (right is { } r0) x1 = Math.Min(w, Math.Max(r0.At(0), r0.At(bottom)));
            if (x1 - x0 < MinPaperMm * mm) return null;

            // Top and bottom edge, looking only at columns well inside the paper.
            int cx0 = (int)(x0 + 4 * mm), cx1 = (int)(x1 - 4 * mm);
            int cstep = Math.Max(1, (cx1 - cx0) / 200);
            var tp = new List<(double t, double v)>();
            var bp = new List<(double t, double v)>();
            for (int x = cx0; x < cx1; x += cstep)
            {
                for (int y = 0; y < bottom - run; y++)
                    if (Run(x, y, 0, 1)) { tp.Add((x, y)); break; }
                for (int y = bottom - 1; y >= run; y--)
                    if (Run(x, y, 0, -1)) { bp.Add((x, y + 1)); break; }
            }
            top = Fit(tp, 1.0 * mm);
            low = Fit(bp, 1.0 * mm);
            if (top is { } t0) y0 = Math.Max(0, Math.Min(t0.At(x0), t0.At(x1)));
            if (low is { } b0) y1 = Math.Min(bottom, Math.Max(b0.At(x0), b0.At(x1)));
        }

        if (x1 - x0 < MinPaperMm * mm || y1 - y0 < MinPaperMm * mm) return null;
        bool worthIt = x0 >= 0.5 * mm || w - x1 >= 0.5 * mm || y0 >= 0.5 * mm || h - y1 >= 0.5 * mm;
        if (!worthIt) return null;

        double inv = 1.0 / scale;
        var rect = Rectangle.Intersect(new Rectangle(0, 0, src.Width, src.Height), Rectangle.FromLTRB(
            (int)Math.Floor(x0 * inv), (int)Math.Floor(y0 * inv),
            (int)Math.Ceiling(x1 * inv), (int)Math.Ceiling(y1 * inv)));
        LogDiag($"Crop: {src.Width}x{src.Height} -> {rect.Width}x{rect.Height} at {rect.X},{rect.Y} " +
                $"({rect.Width / ImageProcessing.GetDpiX(src) * 25.4:0} x {rect.Height / ImageProcessing.GetDpiY(src) * 25.4:0} mm), filler={filler}, " +
                $"paper={paperLum}, background={(back is { } c ? Lum(c).ToString() : "none")}");

        var dst = CopyRect(src, rect);
        try
        {
            // Background left over next to slanted edges (plus the shadow line of the paper edge).
            double inset = 0.35 * mm;
            PaintOutside(dst, paper, (px, py) =>
            {
                double x = (px + rect.X + 0.5) * scale, y = (py + rect.Y + 0.5) * scale;
                return (left is { } le && x < le.At(y) + inset)
                    || (right is { } ri && x > ri.At(y) - inset)
                    || (top is { } to && y0 > 0 && y < to.At(x) + inset)
                    || (low is { } lo && y > lo.At(x) - inset);
            }, EdgeZone(left, right, top, low, w, bottom, mm, inv));
            return dst;
        }
        catch
        {
            dst.Dispose();
            throw;
        }
    }

    private static int Lum(Color c) => (c.R * 299 + c.G * 587 + c.B * 114) / 1000;

    private static bool IsWhiteRow(byte[] lum, int w, int y, int minLum)
    {
        int o = y * w;
        for (int x = 0; x < w; x++)
            if (lum[o + x] < minLum) return false;
        return true;
    }

    private static Color MedianColor(byte[] r, byte[] g, byte[] b, int w, int x0, int x1, int y0, int y1)
    {
        var hr = new int[256];
        var hg = new int[256];
        var hb = new int[256];
        long n = 0;
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
            {
                int i = y * w + x;
                hr[r[i]]++; hg[g[i]]++; hb[b[i]]++; n++;
            }
        return Color.FromArgb(ImageProcessing.Percentile(hr, n, 0.5), ImageProcessing.Percentile(hg, n, 0.5), ImageProcessing.Percentile(hb, n, 0.5));
    }

    /// <summary>
    /// Color of an edge band if it is scanner background: neutral grey, even over nearly the
    /// whole height and clearly darker than the paper.
    /// </summary>
    private static Color? BackgroundBand(byte[] r, byte[] g, byte[] b, byte[] lum, int w, int bottom, int x0, int x1, int paperLum)
    {
        var c = MedianColor(r, g, b, w, x0, x1, 0, bottom);
        int l = Lum(c);
        int chroma = Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B));
        if (l < 90 || l > 235 || chroma > 30 || paperLum - l < 18) return null;

        long same = 0, n = 0;
        for (int y = 0; y < bottom; y++)
            for (int x = x0; x < x1; x++)
            {
                n++;
                if (Math.Abs(lum[y * w + x] - l) <= 12) same++;
            }
        return same >= n * 0.9 ? c : null;
    }

    /// <summary>Robust line fit (median of pairwise slopes); null if most points are not on one nearly straight edge.</summary>
    private static Edge? Fit(List<(double t, double v)> points, double tolerance)
    {
        if (points.Count < 20) return null;
        var slopes = new List<double>();
        int half = points.Count / 2;
        for (int i = 0; i + half < points.Count; i++)
        {
            double dt = points[i + half].t - points[i].t;
            if (dt > 0) slopes.Add((points[i + half].v - points[i].v) / dt);
        }
        if (slopes.Count == 0) return null;
        slopes.Sort();
        double slope = slopes[slopes.Count / 2];
        if (Math.Abs(slope) > 0.12) slope = 0; // more than about 7 degrees: not a paper edge

        var offsets = points.Select(p => p.v - slope * p.t).OrderBy(v => v).ToList();
        double offset = offsets[offsets.Count / 2];
        int inliers = offsets.Count(v => Math.Abs(v - offset) <= tolerance);
        return inliers >= points.Count * 0.6 ? new Edge(offset, slope) : null;
    }

    /// <summary>Width in source pixels of the border zone in which background can remain.</summary>
    private static int EdgeZone(Edge? left, Edge? right, Edge? top, Edge? low, int w, int bottom, double mm, double inv)
    {
        double max = 0;
        if (left is { } l) max = Math.Max(max, Math.Abs(l.B) * bottom);
        if (right is { } r) max = Math.Max(max, Math.Abs(r.B) * bottom);
        if (top is { } t) max = Math.Max(max, Math.Abs(t.B) * w);
        if (low is { } b) max = Math.Max(max, Math.Abs(b.B) * w);
        return (int)Math.Ceiling((max + 2 * mm) * inv);
    }

    /// <summary>Copies a rectangle into a new bitmap of the same kind.</summary>
    private static Bitmap CopyRect(Bitmap src, Rectangle rect)
    {
        using var part = src.Clone(rect, src.PixelFormat);
        var copy = ImageProcessing.DeepCopy(part);
        copy.SetResolution(src.HorizontalResolution, src.VerticalResolution);
        return copy;
    }

    private static void PaintOutside(Bitmap bmp, Color fill, Func<int, int, bool> outside, int zone)
    {
        int bytes = bmp.PixelFormat switch
        {
            PixelFormat.Format24bppRgb => 3,
            PixelFormat.Format32bppRgb or PixelFormat.Format32bppArgb => 4,
            PixelFormat.Format8bppIndexed => 1,
            _ => 0
        };
        if (bytes == 0) return;
        byte index = bytes == 1 ? NearestIndex(bmp.Palette, Lum(fill)) : (byte)0;

        int w = bmp.Width, h = bmp.Height;
        var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, bmp.PixelFormat);
        try
        {
            var row = new byte[Math.Abs(data.Stride)];
            for (int y = 0; y < h; y++)
            {
                bool edgeRow = y < zone || y >= h - zone;
                bool changed = false;
                Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, row.Length);
                for (int x = 0; x < w; x++)
                {
                    if (!edgeRow && x >= zone && x < w - zone)
                    {
                        x = w - zone - 1; // the inner area never needs painting
                        continue;
                    }
                    if (!outside(x, y)) continue;
                    changed = true;
                    int i = x * bytes;
                    if (bytes == 1) { row[i] = index; continue; }
                    row[i] = fill.B;
                    row[i + 1] = fill.G;
                    row[i + 2] = fill.R;
                }
                if (changed) Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, row.Length);
            }
        }
        finally { bmp.UnlockBits(data); }
    }

    private static byte NearestIndex(ColorPalette palette, int lum)
    {
        int best = 0, bestDist = int.MaxValue;
        for (int i = 0; i < palette.Entries.Length; i++)
        {
            int d = Math.Abs(Lum(palette.Entries[i]) - lum);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return (byte)best;
    }

    // ───────────────────────── Black and white ─────────────────────────

    /// <summary>
    /// In black and white the background cannot be told from the paper. If the frame is larger
    /// than A4 and everything outside a (centred) A4 sheet is empty, the empty part is removed.
    /// </summary>
    private static Bitmap? CropBinary(Bitmap src)
    {
        double dpiX = ImageProcessing.GetDpiX(src), dpiY = ImageProcessing.GetDpiY(src);
        int a4W = (int)Math.Round(210 / 25.4 * dpiX), a4H = (int)Math.Round(297 / 25.4 * dpiY);
        if (src.Height <= a4H * 1.03 && src.Width <= a4W * 1.03) return null;

        using var sample = ImageProcessing.CreateSample(src, 100, 4_000_000, out double scale);
        int w = sample.Width, h = sample.Height;
        ImageProcessing.ReadRgb(sample, out _, out _, out _, out var lum);

        int x0 = 0, x1 = src.Width, y1 = src.Height;
        if (src.Height > a4H * 1.03 && IsEmpty(lum, w, 0, w, (int)(a4H * scale), h))
            y1 = a4H;
        int side = (src.Width - a4W) / 2;
        if (src.Width > a4W * 1.03
            && IsEmpty(lum, w, 0, (int)(side * scale), 0, h)
            && IsEmpty(lum, w, (int)((side + a4W) * scale), w, 0, h))
        {
            x0 = side;
            x1 = side + a4W;
        }
        if (x0 == 0 && x1 == src.Width && y1 == src.Height) return null;

        LogDiag($"Crop: b/w {src.Width}x{src.Height} -> {x1 - x0}x{y1} at {x0},0");
        return CopyRect(src, Rectangle.FromLTRB(x0, 0, x1, y1));
    }

    private static bool IsEmpty(byte[] lum, int w, int x0, int x1, int y0, int y1)
    {
        int dark = 0, limit = 3 + (x1 - x0) * Math.Max(0, y1 - y0) / 20_000;
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
                if (lum[y * w + x] < 160 && ++dark > limit) return false;
        return true;
    }
}
