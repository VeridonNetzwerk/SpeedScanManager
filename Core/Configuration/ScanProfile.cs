using System.Text.Json.Serialization;

namespace SpeedScanManager;

/// <summary>
/// A named snapshot of all settings across all 6 tabs.
/// Persisted as JSON by ProfileManager.
/// </summary>
internal class ScanProfile
{
    public string Name { get; set; } = "";
    public bool IsBuiltIn { get; set; }

    // Anwendung tab
    public ApplicationType ApplicationType { get; set; } = ApplicationType.ScanToFolder;

    // Speichern tab
    public string FolderPath { get; set; } = "";
    public FileNameFormatDialog.FormatMode FileNameFormat { get; set; } = FileNameFormatDialog.FormatMode.Timestamp;
    public string CustomFileName { get; set; } = "unbenannt";
    public int CounterDigits { get; set; } = 3;

    // Scanmodus tab
    public ImageQuality ImageQuality { get; set; } = ImageQuality.Automatic;
    public ColorMode ColorMode { get; set; } = ColorMode.Color;
    public ScanSide ScanSide { get; set; } = ScanSide.Automatic;
    public bool ContinueScanning { get; set; }

    // Scanmodus options
    public int Brightness { get; set; }
    public bool TextOnlySettings { get; set; }
    public bool AllowDeleteBlankPages { get; set; } = true;
    public bool AllowDeskew { get; set; }
    public bool AllowAutoRotate { get; set; } = true;
    public bool FaceUpFeeding { get; set; }

    // Dateiart tab
    public FileFormat FileFormat { get; set; } = FileFormat.Pdf;
    public PdfSplitMode PdfSplitMode { get; set; } = PdfSplitMode.MultiPage;
    public int PdfSplitPages { get; set; } = 1;
    public bool PdfUsePassword { get; set; }
    public string PdfPassword { get; set; } = "";

    // OCR settings (Phase 11)
    public bool OcrEnabled { get; set; }
    public OcrLanguage OcrLanguage { get; set; } = OcrLanguage.Automatisch;
    public OcrTargetPages OcrTargetPages { get; set; } = OcrTargetPages.AllPages;

    // Scan to E-Mail settings (Phase 12)
    public string EmailRecipient { get; set; } = "";
    public string EmailSubjectTemplate { get; set; } = "Gescanntes Dokument";

    // Scan to Print settings (Phase 13)
    public string PrinterName { get; set; } = "";

    // Carrier sheet settings (Phase 14)
    public bool CarrierSheetEnabled { get; set; }
    public CarrierSheetOutputSize CarrierSheetOutputSize { get; set; } = CarrierSheetOutputSize.Automatic;
    public int CarrierSheetCustomWidth { get; set; } = 297;
    public int CarrierSheetCustomHeight { get; set; } = 420;

    // Papier tab
    public PaperSizeMode PaperSize { get; set; } = PaperSizeMode.Automatic;
    public List<CustomPaperSize> CustomPaperSizes { get; set; } = new();
    public MultiFeedDetection MultiFeedDetection { get; set; } = MultiFeedDetection.Off;
    public int SelectedCustomSizeIndex { get; set; } = -1;

    // Dateigröße tab
    public int CompressionRate { get; set; } = 3;

    /// <summary>
    /// Creates a profile snapshot from current ScanSettings and save config.
    /// </summary>
    public static ScanProfile FromCurrent(
        ScanSettings settings,
        string folderPath,
        FileNameFormatDialog.FormatMode formatMode,
        string customFileName,
        int counterDigits,
        ApplicationType appType = ApplicationType.ScanToFolder,
        string name = "",
        bool isBuiltIn = false)
    {
        return new ScanProfile
        {
            Name = name,
            IsBuiltIn = isBuiltIn,
            ApplicationType = appType,
            FolderPath = folderPath,
            FileNameFormat = formatMode,
            CustomFileName = customFileName,
            CounterDigits = counterDigits,
            ImageQuality = settings.ImageQuality,
            ColorMode = settings.ColorMode,
            ScanSide = settings.ScanSide,
            ContinueScanning = settings.ContinueScanning,
            Brightness = settings.Brightness,
            TextOnlySettings = settings.TextOnlySettings,
            AllowDeleteBlankPages = settings.AllowDeleteBlankPages,
            AllowDeskew = settings.AllowDeskew,
            AllowAutoRotate = settings.AllowAutoRotate,
            FaceUpFeeding = settings.FaceUpFeeding,
            FileFormat = settings.FileFormat,
            PdfSplitMode = settings.PdfSplitMode,
            PdfSplitPages = settings.PdfSplitPages,
            PdfUsePassword = settings.PdfUsePassword,
            PdfPassword = settings.PdfPassword,
            OcrEnabled = settings.OcrEnabled,
            OcrLanguage = settings.OcrLanguage,
            OcrTargetPages = settings.OcrTargetPages,
            EmailRecipient = settings.EmailRecipient,
            EmailSubjectTemplate = settings.EmailSubjectTemplate,
            PrinterName = settings.PrinterName,
            CarrierSheetEnabled = settings.CarrierSheetEnabled,
            CarrierSheetOutputSize = settings.CarrierSheetOutputSize,
            CarrierSheetCustomWidth = settings.CarrierSheetCustomWidth,
            CarrierSheetCustomHeight = settings.CarrierSheetCustomHeight,
            PaperSize = settings.PaperSize,
            CustomPaperSizes = new List<CustomPaperSize>(settings.CustomPaperSizes),
            MultiFeedDetection = settings.MultiFeedDetection,
            SelectedCustomSizeIndex = settings.SelectedCustomSizeIndex,
            CompressionRate = settings.CompressionRate
        };
    }

    /// <summary>
    /// Applies this profile's settings to a ScanSettings instance.
    /// </summary>
    public void ApplyTo(ScanSettings settings)
    {
        settings.ImageQuality = ImageQuality;
        settings.ColorMode = ColorMode;
        settings.ScanSide = ScanSide;
        settings.ContinueScanning = ContinueScanning;
        settings.Brightness = Brightness;
        settings.TextOnlySettings = TextOnlySettings;
        settings.AllowDeleteBlankPages = AllowDeleteBlankPages;
        settings.AllowDeskew = AllowDeskew;
        settings.AllowAutoRotate = AllowAutoRotate;
        settings.FaceUpFeeding = FaceUpFeeding;
        settings.FileFormat = FileFormat;
        settings.PdfSplitMode = PdfSplitMode;
        settings.PdfSplitPages = PdfSplitPages;
        settings.PdfUsePassword = PdfUsePassword;
        settings.PdfPassword = PdfPassword;
        settings.OcrEnabled = OcrEnabled;
        settings.OcrLanguage = OcrLanguage;
        settings.OcrTargetPages = OcrTargetPages;
        settings.EmailRecipient = EmailRecipient;
        settings.EmailSubjectTemplate = EmailSubjectTemplate;
        settings.PrinterName = PrinterName;
        settings.CarrierSheetEnabled = CarrierSheetEnabled;
        settings.CarrierSheetOutputSize = CarrierSheetOutputSize;
        settings.CarrierSheetCustomWidth = CarrierSheetCustomWidth;
        settings.CarrierSheetCustomHeight = CarrierSheetCustomHeight;
        settings.PaperSize = PaperSize;
        settings.CustomPaperSizes.Clear();
        settings.CustomPaperSizes.AddRange(CustomPaperSizes);
        settings.MultiFeedDetection = MultiFeedDetection;
        settings.SelectedCustomSizeIndex = SelectedCustomSizeIndex;
        settings.CompressionRate = CompressionRate;
        settings.FolderPath = FolderPath;
        settings.FileNameFormat = FileNameFormat;
        settings.CustomFileName = CustomFileName;
        settings.CounterDigits = CounterDigits;
        settings.ApplicationType = ApplicationType;
    }
}
