using System.Drawing;
using System.Reflection;

namespace SpeedScanManager;

/// <summary>
/// Loads embedded PNG resources (logo, text) from the assembly manifest.
/// </summary>
internal static class AppResources
{
    private static Bitmap? _logo;
    private static Bitmap? _textLogo;

    /// <summary>
    /// The SpeedScan Manager logo icon image (scanner/document icon).
    /// </summary>
    public static Bitmap Logo
    {
        get
        {
            if (_logo == null)
                _logo = LoadBitmap("SpeedScanManager_Logo.png");
            return _logo;
        }
    }

    /// <summary>
    /// The SpeedScan Manager text logo (wordmark / Schriftzug).
    /// </summary>
    public static Bitmap TextLogo
    {
        get
        {
            if (_textLogo == null)
                _textLogo = LoadBitmap("SpeedScanManager_Text.png");
            return _textLogo;
        }
    }

    private static Bitmap LoadBitmap(string resourceName)
    {
        var asm = Assembly.GetExecutingAssembly();
        var stream = asm.GetManifestResourceStream(resourceName);
        if (stream == null)
            throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        return new Bitmap(stream);
    }
}
