using System.Drawing;
using System.Drawing.Printing;

namespace SpeedScanManager;

/// <summary>
/// Prints scanned images to the specified printer (or system default).
/// Uses System.Drawing.Printing.PrintDocument.
/// </summary>
internal static class PrintHelper
{
    /// <summary>
    /// Prints the given images, one per page, to the specified printer.
    /// If printerName is empty, uses the system default printer.
    /// </summary>
    public static bool PrintImages(List<Bitmap> images, string printerName)
    {
        if (images.Count == 0)
            return false;

        try
        {
            var settings = new PrinterSettings();
            if (!string.IsNullOrWhiteSpace(printerName))
            {
                // Verify the printer exists
                bool found = false;
                foreach (string name in PrinterSettings.InstalledPrinters)
                {
                    if (string.Equals(name, printerName, StringComparison.OrdinalIgnoreCase))
                    {
                        settings.PrinterName = name;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    // Fall back to default printer
                    System.Diagnostics.Debug.WriteLine($"Printer '{printerName}' not found, using default.");
                }
            }

            int currentIndex = 0;

            var doc = new PrintDocument
            {
                PrinterSettings = settings,
                DocumentName = "SpeedScanManager – Gescanntes Dokument"
            };

            doc.PrintPage += (sender, e) =>
            {
                if (currentIndex < images.Count)
                {
                    var img = images[currentIndex];

                    // Scale image to fit the page margins while preserving aspect ratio
                    var pageBounds = e.MarginBounds;
                    double ratio = Math.Min(
                        (double)pageBounds.Width / img.Width,
                        (double)pageBounds.Height / img.Height);
                    int drawWidth = (int)(img.Width * ratio);
                    int drawHeight = (int)(img.Height * ratio);
                    int x = pageBounds.X + (pageBounds.Width - drawWidth) / 2;
                    int y = pageBounds.Y + (pageBounds.Height - drawHeight) / 2;

                    e.Graphics!.DrawImage(img, x, y, drawWidth, drawHeight);

                    currentIndex++;
                    e.HasMorePages = currentIndex < images.Count;
                }
                else
                {
                    e.HasMorePages = false;
                }
            };

            doc.Print();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Print failed: {ex.Message}");
            return false;
        }
    }
}
