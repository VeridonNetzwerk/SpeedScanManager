namespace SpeedScanManager;

/// <summary>
/// Holds all scan-related settings that will eventually be applied to TWAIN in Phase 8.
/// </summary>
internal class ScanSettings
{
    // Scan mode tab
    public ImageQuality ImageQuality { get; set; } = ImageQuality.Automatic;
    public ColorMode ColorMode { get; set; } = ColorMode.Color;
    public ScanSide ScanSide { get; set; } = ScanSide.Automatic;
    public bool ContinueScanning { get; set; }

    // Scan mode options dialog
    public int Brightness { get; set; } // -3 to +3, 0 = normal
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
    public string PdfPassword { get; set; } = "";

    // OCR settings (Phase 11)
    public bool OcrEnabled { get; set; }
    public OcrLanguage OcrLanguage { get; set; } = OcrLanguage.Automatisch;
    public OcrTargetPages OcrTargetPages { get; set; } = OcrTargetPages.AllPages;

    // Keyword marking settings (Dateiart tab)
    public bool AddKeywordToPdf { get; set; }
    public KeywordTarget KeywordTarget { get; set; } = KeywordTarget.FirstMarkedSection;

    // Scan to E-Mail settings (Phase 12)
    public string EmailRecipient { get; set; } = "";
    public string EmailSubjectTemplate { get; set; } = "Gescanntes Dokument";

    // Scan to Print settings (Phase 13)
    public string PrinterName { get; set; } = "";

    // Carrier sheet settings (Phase 14)
    public bool CarrierSheetEnabled { get; set; }
    public CarrierSheetOutputSize CarrierSheetOutputSize { get; set; } = CarrierSheetOutputSize.Automatic;
    public int CarrierSheetCustomWidth { get; set; } = 297;  // mm
    public int CarrierSheetCustomHeight { get; set; } = 420; // mm

    // Paper tab
    public PaperSizeMode PaperSize { get; set; } = PaperSizeMode.Automatic;
    public List<CustomPaperSize> CustomPaperSizes { get; } = new();
    public int SelectedCustomSizeIndex { get; set; } = -1;
    public MultiFeedDetection MultiFeedDetection { get; set; } = MultiFeedDetection.Off;

    // File size tab
    public int CompressionRate { get; set; } = 3; // 1 (low/large file) to 5 (high/small file)

    // Save tab (persisted alongside other settings so Apply/Cancel works)
    public string FolderPath { get; set; } = "";
    public FileNameFormatDialog.FormatMode FileNameFormat { get; set; } = FileNameFormatDialog.FormatMode.Timestamp;
    public string CustomFileName { get; set; } = "unbenannt";
    public int CounterDigits { get; set; } = 3;

    // Application tab
    public ApplicationType ApplicationType { get; set; } = ApplicationType.ScanToFolder;

    /// <summary>
    /// Compares all setting values with another instance.
    /// </summary>
    public bool SettingsEqual(ScanSettings other)
    {
        if (ImageQuality != other.ImageQuality) return false;
        if (ColorMode != other.ColorMode) return false;
        if (ScanSide != other.ScanSide) return false;
        if (ContinueScanning != other.ContinueScanning) return false;
        if (Brightness != other.Brightness) return false;
        if (TextOnlySettings != other.TextOnlySettings) return false;
        if (AllowDeleteBlankPages != other.AllowDeleteBlankPages) return false;
        if (AllowDeskew != other.AllowDeskew) return false;
        if (AllowAutoRotate != other.AllowAutoRotate) return false;
        if (FaceUpFeeding != other.FaceUpFeeding) return false;
        if (FileFormat != other.FileFormat) return false;
        if (PdfSplitMode != other.PdfSplitMode) return false;
        if (PdfSplitPages != other.PdfSplitPages) return false;
        if (PdfUsePassword != other.PdfUsePassword) return false;
        if (PdfPassword != other.PdfPassword) return false;
        if (OcrEnabled != other.OcrEnabled) return false;
        if (OcrLanguage != other.OcrLanguage) return false;
        if (OcrTargetPages != other.OcrTargetPages) return false;
        if (AddKeywordToPdf != other.AddKeywordToPdf) return false;
        if (KeywordTarget != other.KeywordTarget) return false;
        if (EmailRecipient != other.EmailRecipient) return false;
        if (EmailSubjectTemplate != other.EmailSubjectTemplate) return false;
        if (PrinterName != other.PrinterName) return false;
        if (CarrierSheetEnabled != other.CarrierSheetEnabled) return false;
        if (CarrierSheetOutputSize != other.CarrierSheetOutputSize) return false;
        if (CarrierSheetCustomWidth != other.CarrierSheetCustomWidth) return false;
        if (CarrierSheetCustomHeight != other.CarrierSheetCustomHeight) return false;
        if (PaperSize != other.PaperSize) return false;
        if (SelectedCustomSizeIndex != other.SelectedCustomSizeIndex) return false;
        if (MultiFeedDetection != other.MultiFeedDetection) return false;
        if (CompressionRate != other.CompressionRate) return false;
        if (FolderPath != other.FolderPath) return false;
        if (FileNameFormat != other.FileNameFormat) return false;
        if (CustomFileName != other.CustomFileName) return false;
        if (CounterDigits != other.CounterDigits) return false;
        if (ApplicationType != other.ApplicationType) return false;
        if (CustomPaperSizes.Count != other.CustomPaperSizes.Count) return false;
        for (int i = 0; i < CustomPaperSizes.Count; i++)
        {
            if (CustomPaperSizes[i] != other.CustomPaperSizes[i]) return false;
        }
        return true;
    }

    /// <summary>
    /// Creates a deep copy of the current settings for snapshot/restore.
    /// </summary>
    public ScanSettings Clone()
    {
        var clone = (ScanSettings)MemberwiseClone();
        var paperSizes = new List<CustomPaperSize>(CustomPaperSizes);
        clone.CustomPaperSizes.Clear();
        clone.CustomPaperSizes.AddRange(paperSizes);
        return clone;
    }

    /// <summary>
    /// Restores all values from a snapshot.
    /// </summary>
    public void RestoreFrom(ScanSettings snapshot)
    {
        ImageQuality = snapshot.ImageQuality;
        ColorMode = snapshot.ColorMode;
        ScanSide = snapshot.ScanSide;
        ContinueScanning = snapshot.ContinueScanning;
        Brightness = snapshot.Brightness;
        TextOnlySettings = snapshot.TextOnlySettings;
        AllowDeleteBlankPages = snapshot.AllowDeleteBlankPages;
        AllowDeskew = snapshot.AllowDeskew;
        AllowAutoRotate = snapshot.AllowAutoRotate;
        FaceUpFeeding = snapshot.FaceUpFeeding;
        FileFormat = snapshot.FileFormat;
        PdfSplitMode = snapshot.PdfSplitMode;
        PdfSplitPages = snapshot.PdfSplitPages;
        PdfUsePassword = snapshot.PdfUsePassword;
        PdfPassword = snapshot.PdfPassword;
        OcrEnabled = snapshot.OcrEnabled;
        OcrLanguage = snapshot.OcrLanguage;
        OcrTargetPages = snapshot.OcrTargetPages;
        AddKeywordToPdf = snapshot.AddKeywordToPdf;
        KeywordTarget = snapshot.KeywordTarget;
        EmailRecipient = snapshot.EmailRecipient;
        EmailSubjectTemplate = snapshot.EmailSubjectTemplate;
        PrinterName = snapshot.PrinterName;
        CarrierSheetEnabled = snapshot.CarrierSheetEnabled;
        CarrierSheetOutputSize = snapshot.CarrierSheetOutputSize;
        CarrierSheetCustomWidth = snapshot.CarrierSheetCustomWidth;
        CarrierSheetCustomHeight = snapshot.CarrierSheetCustomHeight;
        PaperSize = snapshot.PaperSize;
        CustomPaperSizes.Clear();
        CustomPaperSizes.AddRange(snapshot.CustomPaperSizes);
        SelectedCustomSizeIndex = snapshot.SelectedCustomSizeIndex;
        MultiFeedDetection = snapshot.MultiFeedDetection;
        CompressionRate = snapshot.CompressionRate;
        FolderPath = snapshot.FolderPath;
        FileNameFormat = snapshot.FileNameFormat;
        CustomFileName = snapshot.CustomFileName;
        CounterDigits = snapshot.CounterDigits;
        ApplicationType = snapshot.ApplicationType;
    }
}

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

internal record CustomPaperSize(string Name, int WidthMm, int LengthMm);

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
    Arabisch
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

internal enum CarrierSheetOutputSize
{
    Automatic,
    Custom
}
