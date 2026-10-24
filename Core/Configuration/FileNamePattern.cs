namespace SpeedScanManager;

/// <summary>
/// Single source of truth for output file names. Used by the previews in the settings UI
/// and intended for the scan output so that preview and real file names always match.
/// OsDefault and Timestamp both produce "yyyyMMddHHmmss"; Custom produces
/// "{name}_{counter:D<digits>}" with the counter starting at 1.
/// </summary>
internal static class FileNamePattern
{
    public const string TimestampFormat = "yyyyMMddHHmmss";
    public const string DefaultCustomName = "unbenannt";
    public const int MaxCustomNameLength = 30;
    public static readonly int[] AllowedCounterDigits = { 3, 4, 5 };

    private static readonly string[] ReservedNames =
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    /// <summary>
    /// Builds the file name without extension.
    /// </summary>
    public static string BuildBaseName(FileNameFormatDialog.FormatMode mode, string? customName,
        int counterDigits, DateTime time, int counter)
    {
        if (mode == FileNameFormatDialog.FormatMode.Custom)
        {
            string name = string.IsNullOrWhiteSpace(customName) ? DefaultCustomName : customName.Trim();
            int digits = NormalizeCounterDigits(counterDigits);
            return $"{name}_{Math.Max(0, counter).ToString("D" + digits)}";
        }
        return time.ToString(TimestampFormat);
    }

    /// <summary>
    /// Builds the base name from the save settings in <paramref name="settings"/>.
    /// </summary>
    public static string BuildBaseName(ScanSettings settings, DateTime time, int counter)
        => BuildBaseName(settings.FileNameFormat, settings.CustomFileName, settings.CounterDigits, time, counter);

    /// <summary>
    /// File extension (with dot) for the output format.
    /// </summary>
    public static string GetExtension(FileFormat format) => format switch
    {
        FileFormat.Jpeg => ".jpg",
        FileFormat.Png => ".png",
        _ => ".pdf"
    };

    /// <summary>
    /// Example name shown in the UI: first file of a scan made now.
    /// </summary>
    public static string BuildPreview(FileNameFormatDialog.FormatMode mode, string? customName,
        int counterDigits, FileFormat format)
        => BuildBaseName(mode, customName, counterDigits, DateTime.Now, 1) + GetExtension(format);

    public static int NormalizeCounterDigits(int digits)
        => Array.IndexOf(AllowedCounterDigits, digits) >= 0 ? digits : AllowedCounterDigits[0];

    /// <summary>
    /// Validates a custom file name prefix. Returns a German error message, or null when valid.
    /// </summary>
    public static string? ValidateCustomName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Bitte geben Sie einen Dateinamen ein.";
        name = name.Trim();
        if (name.Length > MaxCustomNameLength)
            return $"Der Dateiname darf höchstens {MaxCustomNameLength} Zeichen lang sein.";
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return "Der Dateiname ist ungültig. Folgende Zeichen sind nicht erlaubt: \\ / : * ? \" < > |";
        if (name.EndsWith('.'))
            return "Der Dateiname darf nicht mit einem Punkt enden.";
        string stem = name.Split('.')[0];
        if (ReservedNames.Contains(stem, StringComparer.OrdinalIgnoreCase))
            return $"\"{name}\" ist ein reservierter Name und kann nicht verwendet werden.";
        return null;
    }
}
