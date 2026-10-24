using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;

namespace SpeedScanManager;

/// <summary>
/// Prints scanned pages (one page per sheet) at their real size.
///
/// The physical size of a page is taken from its pixel size and resolution; it is only
/// scaled down (never up) when it doesn't fit the printable area of the sheet. Landscape
/// pages are printed on a landscape sheet (auto-rotate).
/// </summary>
internal static class PrintHelper
{
    private const float FallbackDpi = 200f;

    /// <summary>
    /// Checks the configured printer. Returns the printer name to use ("" = Windows default
    /// printer). <paramref name="configuredMissing"/> is true when a printer was configured
    /// but is no longer installed (the caller should ask before using the default printer).
    /// Returns null when no printer is installed at all.
    /// </summary>
    public static string? ResolvePrinter(string? configuredPrinter, out bool configuredMissing)
    {
        configuredMissing = false;
        var installed = PrinterSettings.InstalledPrinters.Cast<string>().ToList();
        if (installed.Count == 0)
            return null;

        if (string.IsNullOrWhiteSpace(configuredPrinter))
            return "";

        var match = installed.FirstOrDefault(n => string.Equals(n, configuredPrinter, StringComparison.OrdinalIgnoreCase));
        if (match != null)
            return match;

        configuredMissing = true;
        return "";
    }

    /// <summary>
    /// Prints the pages synchronously (no progress dialog, so it can run on a worker thread).
    /// The bitmaps are not disposed. Throws on printer errors.
    /// </summary>
    public static void PrintPages(IReadOnlyList<Bitmap> pages, string printerName, string documentName)
    {
        if (pages.Count == 0)
            return;

        var settings = new PrinterSettings();
        if (!string.IsNullOrEmpty(printerName))
            settings.PrinterName = printerName;
        if (!settings.IsValid)
            throw new InvalidOperationException($"Der Drucker \"{settings.PrinterName}\" ist nicht verfügbar.");

        int index = 0;
        using var doc = new PrintDocument
        {
            PrinterSettings = settings,
            DocumentName = documentName,
            PrintController = new StandardPrintController(),
            OriginAtMargins = false
        };

        // Orientation per page: landscape scans on landscape sheets.
        doc.QueryPageSettings += (_, e) =>
        {
            if (index < pages.Count)
                e.PageSettings.Landscape = pages[index].Width > pages[index].Height;
        };

        doc.PrintPage += (_, e) =>
        {
            if (index >= pages.Count || e.Graphics == null)
            {
                e.HasMorePages = false;
                return;
            }

            DrawPage(e, pages[index]);
            index++;
            e.HasMorePages = index < pages.Count;
        };

        // The first page's orientation must be set before printing starts.
        doc.DefaultPageSettings.Landscape = pages[0].Width > pages[0].Height;
        doc.Print();
    }

    private static void DrawPage(PrintPageEventArgs e, Bitmap img)
    {
        var g = e.Graphics!;
        g.PageUnit = GraphicsUnit.Display; // 1/100 inch for printers
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        float dpiX = img.HorizontalResolution > 1 ? img.HorizontalResolution : FallbackDpi;
        float dpiY = img.VerticalResolution > 1 ? img.VerticalResolution : FallbackDpi;

        // Real size in 1/100 inch.
        float width = img.Width / dpiX * 100f;
        float height = img.Height / dpiY * 100f;

        // Physical sheet (orientation applied) and the printable area within it.
        // With OriginAtMargins = false the graphics origin is the top-left of the printable area.
        RectangleF page = e.PageBounds;
        float hardX = e.PageSettings.HardMarginX;
        float hardY = e.PageSettings.HardMarginY;
        // VisibleClipBounds of a printer graphics is the printable area (orientation applied).
        var printable = g.VisibleClipBounds;
        float printableW = printable.Width;
        float printableH = printable.Height;
        if (printableW <= 1 || printableH <= 1)
        {
            printableW = page.Width - 2 * hardX;
            printableH = page.Height - 2 * hardY;
        }

        // Only shrink when the page doesn't fit (e.g. scan slightly larger than the sheet).
        float scale = Math.Min(1f, Math.Min(printableW / width, printableH / height));
        width *= scale;
        height *= scale;

        // Center on the physical sheet, but keep it inside the printable area.
        float x = (page.Width - width) / 2f - hardX;
        float y = (page.Height - height) / 2f - hardY;
        x = Math.Clamp(x, 0, Math.Max(0, printableW - width));
        y = Math.Clamp(y, 0, Math.Max(0, printableH - height));

        g.DrawImage(img, x, y, width, height);
    }
}
