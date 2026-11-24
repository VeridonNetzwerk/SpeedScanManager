namespace SpeedScanManager;

/// <summary>
/// Readable list of changed settings ("Scanmodus · Farbmodus: Farbe → Graustufen"), like a
/// BIOS asking before it saves.
/// </summary>
internal static class SettingsChangeList
{
    public sealed record Change(string Area, string Name, string Old, string New);

    private static readonly Dictionary<string, (string Area, string Name)> Labels = new()
    {
        ["ImageQuality"] = ("Scanmodus", "Bildqualität"),
        ["ColorMode"] = ("Scanmodus", "Farbmodus"),
        ["ScanSide"] = ("Scanmodus", "Seiten"),
        ["ContinueScanning"] = ("Scanmodus", "Weiterscannen"),
        ["Brightness"] = ("Scanmodus", "Helligkeit"),
        ["TextOnlySettings"] = ("Scanmodus", "Textoptimierung"),
        ["AllowDeleteBlankPages"] = ("Scanmodus", "Leere Seiten entfernen"),
        ["AllowDeskew"] = ("Scanmodus", "Schräglage korrigieren"),
        ["AllowAutoRotate"] = ("Scanmodus", "Automatisch drehen"),
        ["FaceUpFeeding"] = ("Scanmodus", "Vorderseite oben"),
        ["FileFormat"] = ("Dateiart", "Format"),
        ["PdfSplitMode"] = ("Dateiart", "PDF aufteilen"),
        ["PdfSplitPages"] = ("Dateiart", "Seiten pro PDF"),
        ["PdfUsePassword"] = ("Dateiart", "PDF-Passwort"),
        ["PdfPassword"] = ("Dateiart", "Passwort"),
        ["OcrEnabled"] = ("Dateiart", "Texterkennung"),
        ["OcrLanguage"] = ("Dateiart", "Texterkennung-Sprache"),
        ["OcrTargetPages"] = ("Dateiart", "Texterkennung-Seiten"),
        ["AddKeywordToPdf"] = ("Dateiart", "Schlüsselwörter"),
        ["KeywordTarget"] = ("Dateiart", "Schlüsselwort-Bereich"),
        ["EmailRecipient"] = ("Anwendung", "E-Mail-Empfänger"),
        ["EmailSubjectTemplate"] = ("Anwendung", "E-Mail-Betreff"),
        ["PrinterName"] = ("Anwendung", "Drucker"),
        ["ApplicationType"] = ("Anwendung", "Anwendung"),
        ["CustomApplicationName"] = ("Anwendung", "Eigene Anwendung"),
        ["CustomApplicationPath"] = ("Anwendung", "Programmpfad"),
        ["CustomApplicationArguments"] = ("Anwendung", "Programmparameter"),
        ["CarrierSheetEnabled"] = ("Papier", "Trägerblatt"),
        ["CarrierSheetOutputSize"] = ("Papier", "Trägerblatt-Ausgabe"),
        ["CarrierSheetCustomWidth"] = ("Papier", "Trägerblatt-Breite"),
        ["CarrierSheetCustomHeight"] = ("Papier", "Trägerblatt-Höhe"),
        ["PaperSize"] = ("Papier", "Papiergröße"),
        ["SelectedCustomSizeIndex"] = ("Papier", "Eigene Größe"),
        ["MultiFeedDetection"] = ("Papier", "Mehrfacheinzug"),
        ["CustomPaperSizes"] = ("Papier", "Eigene Papiergrößen"),
        ["CompressionRate"] = ("Dateigröße", "Komprimierung"),
        ["FolderPath"] = ("Speichern", "Ordner"),
        ["FileNameFormat"] = ("Speichern", "Dateiname"),
        ["CustomFileName"] = ("Speichern", "Eigener Dateiname"),
        ["CounterDigits"] = ("Speichern", "Zählerstellen"),
        ["RenameAfterScan"] = ("Speichern", "Nach Scan umbenennen"),
    };

    private static readonly Dictionary<string, string> EnumTexts = new()
    {
        ["ImageQuality.Automatic"] = "Automatisch", ["ImageQuality.Normal"] = "Normal", ["ImageQuality.Fine"] = "Fein",
        ["ImageQuality.Best"] = "Beste", ["ImageQuality.Excellent"] = "Hervorragend",
        ["ColorMode.Automatic"] = "Automatisch", ["ColorMode.Color"] = "Farbe", ["ColorMode.Grayscale"] = "Graustufen",
        ["ColorMode.BlackWhite"] = "Schwarzweiß",
        ["ScanSide.Automatic"] = "Automatisch", ["ScanSide.Simplex"] = "Einseitig", ["ScanSide.Duplex"] = "Beidseitig",
        ["ScanSide.Flatbed"] = "Flachbett",
        ["FileFormat.Pdf"] = "PDF", ["FileFormat.Jpeg"] = "JPEG", ["FileFormat.Png"] = "PNG",
        ["PdfSplitMode.MultiPage"] = "Eine Datei", ["PdfSplitMode.SplitByPageCount"] = "Nach Seitenzahl",
        ["OcrTargetPages.FirstPage"] = "Erste Seite", ["OcrTargetPages.AllPages"] = "Alle Seiten",
        ["KeywordTarget.FirstMarkedSection"] = "Erster Bereich", ["KeywordTarget.AllMarkedSections"] = "Alle Bereiche",
        ["MultiFeedDetection.Off"] = "Aus", ["MultiFeedDetection.OverlapUltrasound"] = "Ultraschall",
        ["MultiFeedDetection.Length"] = "Länge", ["MultiFeedDetection.Both"] = "Ultraschall und Länge",
        ["FormatMode.OsDefault"] = "Windows-Standard", ["FormatMode.Timestamp"] = "Zeitstempel", ["FormatMode.Custom"] = "Eigener Name",
        ["ApplicationType.ScanToFolder"] = "Scan to Folder", ["ApplicationType.ScanToEmail"] = "Scan to E-Mail",
        ["ApplicationType.ScanToPrint"] = "Scan to Print", ["ApplicationType.ScanToWord"] = "Scan to Word",
        ["ApplicationType.ScanToExcel"] = "Scan to Excel", ["ApplicationType.ScanToPowerPoint"] = "Scan to PowerPoint",
        ["ApplicationType.ScanPictureFolder"] = "Bilderordner", ["ApplicationType.EditWithPdf"] = "PDF bearbeiten",
        ["ApplicationType.Custom"] = "Eigene Anwendung",
        ["PaperSizeMode.Automatic"] = "Automatisch", ["CarrierSheetOutputSize.Automatic"] = "Automatisch",
        ["CarrierSheetOutputSize.Custom"] = "Eigene Größe",
    };

    /// <summary>Changes of the scan settings (<paramref name="edited"/> against <paramref name="saved"/>).</summary>
    public static List<Change> Of(ScanSettings edited, ScanSettings saved)
    {
        var list = new List<Change>();
        foreach (var name in edited.DifferingSettings(saved))
        {
            var (area, label) = Labels.TryGetValue(name, out var l) ? l : ("Einstellungen", name);
            if (name == "CustomPaperSizes")
            {
                list.Add(new Change(area, label, $"{saved.CustomPaperSizes.Count} Einträge", $"{edited.CustomPaperSizes.Count} Einträge"));
                continue;
            }
            var prop = typeof(ScanSettings).GetProperty(name)!;
            if (name == "PdfPassword")
            {
                list.Add(new Change(area, label, "••••", "geändert"));
                continue;
            }
            list.Add(new Change(area, label, Format(prop.GetValue(saved)), Format(prop.GetValue(edited))));
        }
        // Order like the pages in the window
        string[] order = { "Allgemein", "Anwendung", "Speichern", "Scanmodus", "Dateiart", "Papier", "Dateigröße", "Einstellungen" };
        return list.OrderBy(c => Array.IndexOf(order, c.Area)).ThenBy(c => c.Name).ToList();
    }

    public static string OnOff(bool value) => value ? "An" : "Aus";

    public static string Format(object? value)
    {
        switch (value)
        {
            case null: return "–";
            case bool b: return OnOff(b);
            case string s:
                if (s.Length == 0) return "(leer)";
                return s.Length > 32 ? "…" + s[^31..] : s;
            case Enum e:
                return EnumTexts.TryGetValue($"{e.GetType().Name}.{e}", out var text) ? text : e.ToString();
            default:
                return value.ToString() ?? "";
        }
    }
}
