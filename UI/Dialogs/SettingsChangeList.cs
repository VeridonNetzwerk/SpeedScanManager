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
        ["ImageQuality"] = (L.T("Scanmodus"), L.T("Bildqualität")),
        ["ColorMode"] = (L.T("Scanmodus"), L.T("Farbmodus")),
        ["ScanSide"] = (L.T("Scanmodus"), L.T("Seiten")),
        ["ContinueScanning"] = (L.T("Scanmodus"), L.T("Weiterscannen")),
        [L.T("Brightness")] = (L.T("Scanmodus"), L.T("Helligkeit")),
        ["TextOnlySettings"] = (L.T("Scanmodus"), L.T("Textoptimierung")),
        ["AllowDeleteBlankPages"] = (L.T("Scanmodus"), L.T("Leere Seiten entfernen")),
        ["AllowDeskew"] = (L.T("Scanmodus"), L.T("Schräglage korrigieren")),
        ["AllowAutoRotate"] = (L.T("Scanmodus"), L.T("Automatisch drehen")),
        ["FaceUpFeeding"] = (L.T("Scanmodus"), L.T("Vorderseite oben")),
        ["FileFormat"] = (L.T("Dateiart"), L.T("Format")),
        ["PdfSplitMode"] = (L.T("Dateiart"), L.T("PDF aufteilen")),
        ["PdfSplitPages"] = (L.T("Dateiart"), L.T("Seiten pro PDF")),
        ["PdfUsePassword"] = (L.T("Dateiart"), "PDF-Passwort"),
        ["PdfPassword"] = (L.T("Dateiart"), L.T("Passwort")),
        ["OcrEnabled"] = (L.T("Dateiart"), L.T("Texterkennung")),
        ["OcrLanguage"] = (L.T("Dateiart"), L.T("Texterkennung-Sprache")),
        ["OcrTargetPages"] = (L.T("Dateiart"), L.T("Texterkennung-Seiten")),
        ["AddKeywordToPdf"] = (L.T("Dateiart"), L.T("Schlüsselwörter")),
        ["KeywordTarget"] = (L.T("Dateiart"), L.T("Schlüsselwort-Bereich")),
        ["EmailRecipient"] = (L.T("Anwendung"), L.T("E-Mail-Empfänger")),
        ["EmailSubjectTemplate"] = (L.T("Anwendung"), "E-Mail-Betreff"),
        ["PrinterName"] = (L.T("Anwendung"), L.T("Drucker")),
        ["ApplicationType"] = (L.T("Anwendung"), L.T("Anwendung")),
        ["CustomApplicationName"] = (L.T("Anwendung"), L.T("Eigene Anwendung")),
        ["CustomApplicationPath"] = (L.T("Anwendung"), L.T("Programmpfad")),
        ["CustomApplicationArguments"] = (L.T("Anwendung"), L.T("Programmparameter")),
        ["CarrierSheetEnabled"] = (L.T("Papier"), L.T("Trägerblatt")),
        ["CarrierSheetOutputSize"] = (L.T("Papier"), L.T("Trägerblatt-Ausgabe")),
        ["CarrierSheetCustomWidth"] = (L.T("Papier"), L.T("Trägerblatt-Breite")),
        ["CarrierSheetCustomHeight"] = (L.T("Papier"), L.T("Trägerblatt-Höhe")),
        ["PaperSize"] = (L.T("Papier"), L.T("Papiergröße")),
        ["SelectedCustomSizeIndex"] = (L.T("Papier"), L.T("Eigene Größe")),
        ["MultiFeedDetection"] = (L.T("Papier"), L.T("Mehrfacheinzug")),
        ["CustomPaperSizes"] = (L.T("Papier"), L.T("Eigene Papiergrößen")),
        ["CompressionRate"] = (L.T("Dateigröße"), L.T("Komprimierung")),
        ["FolderPath"] = (L.T("Speichern"), L.T("Ordner")),
        ["FileNameFormat"] = (L.T("Speichern"), L.T("Dateiname")),
        ["CustomFileName"] = (L.T("Speichern"), L.T("Eigener Dateiname")),
        ["CounterDigits"] = (L.T("Speichern"), L.T("Zählerstellen")),
        ["RenameAfterScan"] = (L.T("Speichern"), L.T("Nach Scan umbenennen")),
    };

    private static readonly Dictionary<string, string> EnumTexts = new()
    {
        ["ImageQuality.Automatic"] = L.T("Automatisch"), ["ImageQuality.Normal"] = L.T("Normal"), ["ImageQuality.Fine"] = L.T("Fein"),
        ["ImageQuality.Best"] = L.T("Beste"), ["ImageQuality.Excellent"] = L.T("Hervorragend"),
        ["ColorMode.Automatic"] = L.T("Automatisch"), ["ColorMode.Color"] = L.T("Farbe"), ["ColorMode.Grayscale"] = L.T("Graustufen"),
        ["ColorMode.BlackWhite"] = L.T("Schwarzweiß"),
        ["ScanSide.Automatic"] = L.T("Automatisch"), ["ScanSide.Simplex"] = L.T("Einseitig"), ["ScanSide.Duplex"] = L.T("Beidseitig"),
        ["ScanSide.Flatbed"] = L.T("Flachbett"),
        ["FileFormat.Pdf"] = "PDF", ["FileFormat.Jpeg"] = L.T("JPEG"), ["FileFormat.Png"] = "PNG",
        ["PdfSplitMode.MultiPage"] = L.T("Eine Datei"), ["PdfSplitMode.SplitByPageCount"] = L.T("Nach Seitenzahl"),
        ["OcrTargetPages.FirstPage"] = L.T("Erste Seite"), ["OcrTargetPages.AllPages"] = L.T("Alle Seiten"),
        ["KeywordTarget.FirstMarkedSection"] = L.T("Erster Bereich"), ["KeywordTarget.AllMarkedSections"] = L.T("Alle Bereiche"),
        ["MultiFeedDetection.Off"] = L.T("Aus"), ["MultiFeedDetection.OverlapUltrasound"] = L.T("Ultraschall"),
        ["MultiFeedDetection.Length"] = L.T("Länge"), ["MultiFeedDetection.Both"] = L.T("Ultraschall und Länge"),
        ["FormatMode.OsDefault"] = L.T("Windows-Standard"), ["FormatMode.Timestamp"] = L.T("Zeitstempel"), ["FormatMode.Custom"] = L.T("Eigener Name"),
        ["ApplicationType.ScanToFolder"] = L.T("Scan to Folder"), ["ApplicationType.ScanToEmail"] = L.T("Scan to E-Mail"),
        ["ApplicationType.ScanToPrint"] = L.T("Scan to Print"), ["ApplicationType.ScanToWord"] = L.T("Scan to Word"),
        ["ApplicationType.ScanToExcel"] = L.T("Scan to Excel"), ["ApplicationType.ScanToPowerPoint"] = L.T("Scan to PowerPoint"),
        ["ApplicationType.ScanPictureFolder"] = L.T("Bilderordner"), ["ApplicationType.EditWithPdf"] = L.T("PDF bearbeiten"),
        ["ApplicationType.Custom"] = L.T("Eigene Anwendung"),
        ["PaperSizeMode.Automatic"] = L.T("Automatisch"), ["CarrierSheetOutputSize.Automatic"] = L.T("Automatisch"),
        ["CarrierSheetOutputSize.Custom"] = L.T("Eigene Größe"),
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
                list.Add(new Change(area, label, L.F("{0} Einträge", saved.CustomPaperSizes.Count), L.F("{0} Einträge", edited.CustomPaperSizes.Count)));
                continue;
            }
            var prop = typeof(ScanSettings).GetProperty(name)!;
            if (name == "PdfPassword")
            {
                list.Add(new Change(area, label, "••••", L.T("geändert")));
                continue;
            }
            list.Add(new Change(area, label, Format(prop.GetValue(saved)), Format(prop.GetValue(edited))));
        }
        // Order like the pages in the window
        string[] order = { L.T("Allgemein"), L.T("Anwendung"), L.T("Speichern"), L.T("Scanmodus"), L.T("Dateiart"), L.T("Papier"), L.T("Dateigröße"), L.T("Einstellungen") };
        return list.OrderBy(c => Array.IndexOf(order, c.Area)).ThenBy(c => c.Name).ToList();
    }

    public static string OnOff(bool value) => value ? "An" : L.T("Aus");

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
