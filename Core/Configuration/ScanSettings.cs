using System.Reflection;
using System.Text.Json.Serialization;

namespace SpeedScanManager;

/// <summary>
/// Holds all scan-related settings (all tabs of the settings window).
/// This is the full persisted payload: AppSettings.ScanSettings stores the current
/// settings and every ScanProfile stores its own copy.
/// Copy/compare logic is reflection based, so a newly added property is automatically
/// included in Clone/RestoreFrom/SettingsEqual and in profiles.
/// </summary>
internal class ScanSettings
{
    // Scan mode tab
    public ImageQuality ImageQuality { get; set; } = ImageQuality.Automatic;
    public ColorMode ColorMode { get; set; } = ColorMode.Color;
    public ScanSide ScanSide { get; set; } = ScanSide.Automatic;
    /// <summary>After a scan, ask whether to continue scanning (pages are appended).</summary>
    public bool ContinueScanning { get; set; }

    // Scan mode options dialog
    /// <summary>-3 (dunkel) to +3 (hell), 0 = normal. Only used for ColorMode.BlackWhite.
    /// Sign follows TWAIN ICAP_BRIGHTNESS: negative = darker.</summary>
    public int Brightness { get; set; }
    public bool TextOnlySettings { get; set; }
    public bool AllowDeleteBlankPages { get; set; } = true;
    public bool AllowDeskew { get; set; }
    public bool AllowAutoRotate { get; set; } = true;
    public bool FaceUpFeeding { get; set; }

    // File type tab
    public FileFormat FileFormat { get; set; } = FileFormat.Pdf;
    public PdfSplitMode PdfSplitMode { get; set; } = PdfSplitMode.MultiPage;
    public int PdfSplitPages { get; set; } = 1;
    public bool PdfUsePassword { get; set; }

    /// <summary>Plain-text PDF password, in memory only (never written to disk).</summary>
    [JsonIgnore]
    public string PdfPassword { get; set; } = "";

    /// <summary>
    /// Password protection is on but no password is available, e.g. the stored DPAPI value
    /// could not be decrypted (settings copied from another user or PC). The scan flow must
    /// ask for a new password instead of silently writing an unprotected PDF.
    /// </summary>
    [JsonIgnore]
    public bool PdfPasswordMissing => PdfUsePassword && string.IsNullOrEmpty(PdfPassword);

    /// <summary>DPAPI-protected (CurrentUser) form of <see cref="PdfPassword"/>, used for persistence.</summary>
    [DerivedSetting]
    public string PdfPasswordProtected
    {
        get => SecretProtector.Protect(PdfPassword);
        set => PdfPassword = SecretProtector.Unprotect(value);
    }

    // OCR settings (Phase 11)
    public bool OcrEnabled { get; set; }
    public OcrLanguage OcrLanguage { get; set; } = OcrLanguage.Automatisch;
    public OcrTargetPages OcrTargetPages { get; set; } = OcrTargetPages.AllPages;
    public OcrQuality OcrQuality { get; set; } = OcrQuality.Genau;

    // Keyword marking settings (Dateiart tab)
    public bool AddKeywordToPdf { get; set; }
    public KeywordTarget KeywordTarget { get; set; } = KeywordTarget.FirstMarkedSection;

    // Scan to E-Mail settings (Phase 12)
    public string EmailRecipient { get; set; } = "";
    public string EmailSubjectTemplate { get; set; } = L.T("Gescanntes Dokument");

    // Scan to Print settings (Phase 13)
    public string PrinterName { get; set; } = "";

    // Carrier sheet settings (Phase 14)
    public bool CarrierSheetEnabled { get; set; }
    public CarrierSheetOutputSize CarrierSheetOutputSize { get; set; } = CarrierSheetOutputSize.Automatic;
    public int CarrierSheetCustomWidth { get; set; } = 297;  // mm, used when CarrierSheetOutputSize == Custom
    public int CarrierSheetCustomHeight { get; set; } = 420; // mm

    // Paper tab
    public PaperSizeMode PaperSize { get; set; } = PaperSizeMode.Automatic;
    /// <summary>User-defined paper sizes (max <see cref="MaxCustomPaperSizes"/>), width/length in mm.</summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public List<CustomPaperSize> CustomPaperSizes { get; } = new();
    /// <summary>Index into <see cref="CustomPaperSizes"/>; valid when PaperSize == Custom, otherwise -1.</summary>
    public int SelectedCustomSizeIndex { get; set; } = -1;
    public MultiFeedDetection MultiFeedDetection { get; set; } = MultiFeedDetection.Off;

    // File size tab
    public int CompressionRate { get; set; } = 3; // 1 (low/large file) to 5 (high/small file)

    // Save tab
    public string FolderPath { get; set; } = "";
    public FileNameFormatDialog.FormatMode FileNameFormat { get; set; } = FileNameFormatDialog.FormatMode.Timestamp;
    public string CustomFileName { get; set; } = FileNamePattern.DefaultCustomName;
    public int CounterDigits { get; set; } = 3;
    /// <summary>Show the rename/save dialog after each scan (Scan to Folder).</summary>
    public bool RenameAfterScan { get; set; }

    // Application tab
    public ApplicationType ApplicationType { get; set; } = ApplicationType.ScanToFolder;
    /// <summary>When ApplicationType == Custom: display name of the user-added application.</summary>
    public string CustomApplicationName { get; set; } = "";
    /// <summary>When ApplicationType == Custom: full path of the executable to launch.</summary>
    public string CustomApplicationPath { get; set; } = "";
    /// <summary>When ApplicationType == Custom: optional extra arguments (placed before the file paths).</summary>
    public string CustomApplicationArguments { get; set; } = "";

    public const int MaxCustomPaperSizes = 10;
    public const int MaxCustomPaperSizeNameLength = 62;
    public const int MinCarrierSheetSizeMm = 50;
    public const int MaxCarrierSheetSizeMm = 2000;

    public static string DefaultFolderPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SpeedScanManager");

    /// <summary>All copyable scalar settings (read/write, not derived).</summary>
    private static readonly PropertyInfo[] ValueProperties = typeof(ScanSettings)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.CanRead && p.CanWrite && p.GetSetMethod() != null
                    && p.GetCustomAttribute<DerivedSettingAttribute>() == null)
        .ToArray();

    /// <summary>
    /// Names of the settings whose values differ from <paramref name="other"/>
    /// ("CustomPaperSizes" for the paper size list).
    /// </summary>
    public HashSet<string> DifferingSettings(ScanSettings other)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in ValueProperties)
        {
            if (!Equals(p.GetValue(this), p.GetValue(other))) result.Add(p.Name);
        }
        if (!CustomPaperSizes.SequenceEqual(other.CustomPaperSizes))
            result.Add(nameof(CustomPaperSizes));
        return result;
    }

    /// <summary>
    /// Compares all setting values with another instance (custom paper sizes element-wise).
    /// </summary>
    public bool SettingsEqual(ScanSettings other)
    {
        foreach (var p in ValueProperties)
        {
            if (!Equals(p.GetValue(this), p.GetValue(other))) return false;
        }
        return CustomPaperSizes.SequenceEqual(other.CustomPaperSizes);
    }

    /// <summary>
    /// Creates a deep copy of the current settings.
    /// </summary>
    public ScanSettings Clone()
    {
        var clone = new ScanSettings();
        clone.RestoreFrom(this);
        return clone;
    }

    /// <summary>
    /// Copies all values from <paramref name="snapshot"/> into this instance (deep copy of lists).
    /// </summary>
    public void RestoreFrom(ScanSettings snapshot)
    {
        if (ReferenceEquals(this, snapshot)) return;
        foreach (var p in ValueProperties)
            p.SetValue(this, p.GetValue(snapshot));

        var sizes = snapshot.CustomPaperSizes.Select(s => s with { }).ToList();
        CustomPaperSizes.Clear();
        CustomPaperSizes.AddRange(sizes);
    }

    /// <summary>
    /// Clamps every value to its valid range and resolves contradictory combinations.
    /// Call after loading from disk and before binding to UI controls.
    /// </summary>
    public void Normalize()
    {
        // Undefined enum values fall back to the property's initializer default.
        foreach (var p in EnumProperties)
        {
            if (!Enum.IsDefined(p.PropertyType, p.GetValue(this)!))
                p.SetValue(this, p.GetValue(Defaults));
        }

        Brightness = Math.Clamp(Brightness, -3, 3);
        CompressionRate = Math.Clamp(CompressionRate, 1, 5);
        PdfSplitPages = Math.Clamp(PdfSplitPages, 1, 999);
        CarrierSheetCustomWidth = Math.Clamp(CarrierSheetCustomWidth, MinCarrierSheetSizeMm, MaxCarrierSheetSizeMm);
        CarrierSheetCustomHeight = Math.Clamp(CarrierSheetCustomHeight, MinCarrierSheetSizeMm, MaxCarrierSheetSizeMm);
        CounterDigits = FileNamePattern.NormalizeCounterDigits(CounterDigits);

        PdfPassword ??= "";
        EmailRecipient ??= "";
        EmailSubjectTemplate ??= "";
        PrinterName ??= "";
        CustomApplicationName ??= "";
        CustomApplicationPath ??= "";
        CustomApplicationArguments ??= "";

        if (string.IsNullOrWhiteSpace(FolderPath))
            FolderPath = DefaultFolderPath;
        if (FileNamePattern.ValidateCustomName(CustomFileName) != null)
            CustomFileName = FileNamePattern.DefaultCustomName;
        // PdfUsePassword with an empty password is kept on purpose (see PdfPasswordMissing):
        // turning protection off here would make the next scan write an unprotected PDF.

        // Custom paper sizes: drop invalid entries, enforce limits
        var sizes = CustomPaperSizes
            .Where(s => s != null && !string.IsNullOrWhiteSpace(s.Name))
            .Take(MaxCustomPaperSizes)
            .Select(s => new CustomPaperSize(
                s.Name.Trim().Length > MaxCustomPaperSizeNameLength ? s.Name.Trim()[..MaxCustomPaperSizeNameLength] : s.Name.Trim(),
                Math.Clamp(s.WidthMm, 1, 9999),
                Math.Clamp(s.LengthMm, 1, 9999)))
            .ToList();
        CustomPaperSizes.Clear();
        CustomPaperSizes.AddRange(sizes);

        if (PaperSize == PaperSizeMode.Custom)
        {
            if (CustomPaperSizes.Count == 0)
            {
                PaperSize = PaperSizeMode.Automatic;
                SelectedCustomSizeIndex = -1;
            }
            else if (SelectedCustomSizeIndex < 0 || SelectedCustomSizeIndex >= CustomPaperSizes.Count)
            {
                SelectedCustomSizeIndex = 0;
            }
        }
        else
        {
            SelectedCustomSizeIndex = -1;
        }

        // Automatic paper size detection is not available for flatbed scanning
        if (ScanSide == ScanSide.Flatbed && PaperSize == PaperSizeMode.Automatic)
            PaperSize = PaperSizeMode.A4;

        // JPEG cannot hold black & white images (ScanSnap disables JPEG for B/W)
        if (ColorMode == ColorMode.BlackWhite && FileFormat == FileFormat.Jpeg)
            FileFormat = FileFormat.Pdf;

        if (ApplicationType == ApplicationType.Custom && string.IsNullOrWhiteSpace(CustomApplicationPath))
            ApplicationType = ApplicationType.ScanToFolder;
    }

    private static readonly PropertyInfo[] EnumProperties = ValueProperties
        .Where(p => p.PropertyType.IsEnum)
        .ToArray();

    /// <summary>Freshly constructed instance: the initializer defaults of every property.</summary>
    private static ScanSettings Defaults => _defaults ??= new ScanSettings();
    private static ScanSettings? _defaults;
}

/// <summary>
/// Marks a property that is computed from other settings (not copied or compared).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class DerivedSettingAttribute : Attribute { }

internal enum PaperSizeMode
{
    Automatic,
    A3,
    A4,
    A5,
    A6,
    B4Jis,
    B5Jis,
    B6Jis,
    Postcard,
    BusinessCard,
    Letter,
    DoubleLetter,
    Legal,
    Custom
}

internal enum MultiFeedDetection
{
    Off,
    OverlapUltrasound,
    Length,
    Both
}

/// <summary>
/// User-defined paper size. WidthMm and LengthMm are whole millimetres.
/// </summary>
internal record CustomPaperSize(string Name, int WidthMm, int LengthMm)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string DisplayText => $"{Name} ({WidthMm} x {LengthMm} mm)";
}

internal enum FileFormat
{
    Pdf,
    Jpeg,
    Png
}

internal enum PdfSplitMode
{
    MultiPage,
    SplitByPageCount
}

internal enum ImageQuality
{
    Automatic,
    Normal,
    Fine,
    Best,
    Excellent
}

internal enum ColorMode
{
    Automatic,
    Color,
    Grayscale,
    BlackWhite
}

internal enum ScanSide
{
    Automatic,
    Simplex,
    Duplex,
    Flatbed
}

internal enum OcrLanguage
{
    Automatisch,
    Deutsch,
    Japanisch,
    English,
    Franzoesisch,
    Italienisch,
    Spanisch,
    ChinesischVereinfacht,
    ChinesischTraditionell,
    Koreanisch,
    Russisch,
    Portugiesisch,
    Arabisch,
    Polnisch,
    Niederlaendisch,
    Tuerkisch
}

/// <summary>OCR model set: "best" (accurate, default) or "fast" (tessdata_best / tessdata_fast).</summary>
internal enum OcrQuality
{
    Genau,
    Schnell
}

internal enum OcrTargetPages
{
    FirstPage,
    AllPages
}

internal enum KeywordTarget
{
    FirstMarkedSection,
    AllMarkedSections
}

/// <summary>
/// Output image size for carrier sheet scans. Existing values keep their numbers
/// (Automatic = 0, Custom = 1); fixed sizes were appended. All sizes in mm (width x height).
/// </summary>
internal enum CarrierSheetOutputSize
{
    Automatic,
    Custom,          // CarrierSheetCustomWidth x CarrierSheetCustomHeight
    A3,              // 297 x 420
    A4,              // 210 x 297
    A5,              // 148 x 210
    A6,              // 105 x 148
    B4,              // 257 x 364 (JIS)
    B5,              // 182 x 257 (JIS)
    B6,              // 128 x 182 (JIS)
    Letter,          // 216 x 279.4
    DoubleLetter,    // 279.4 x 431.8
    Postcard,        // 100 x 148
    BusinessCard,    // 55 x 91
    CarrierSheet     // 220 x 297 (size of the carrier sheet itself)
}
