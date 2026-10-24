using System.Drawing;
using System.Text;

namespace SpeedScanManager;

/// <summary>
/// Represents a single help topic in the navigation tree.
/// </summary>
internal class HelpTopic
{
    public string Id { get; }
    public string Title { get; }
    public string Html { get; }
    public List<HelpTopic> Children { get; } = new();

    /// <summary>Leaf topic with content.</summary>
    public HelpTopic(string id, string title, string html)
    {
        Id = id;
        Title = title;
        Html = html;
    }

    /// <summary>Branch topic (no content, only children).</summary>
    public HelpTopic(string id, string title, params HelpTopic[] children)
    {
        Id = id;
        Title = title;
        Html = "";
        Children.AddRange(children);
    }
}

/// <summary>
/// Static class that builds the help topic tree and provides HTML content
/// for the SpeedScan Manager help system.
/// All content reflects the actual implemented features of the application.
/// </summary>
internal static class HelpContent
{
    public const string Css = @"
        body { font-family: 'Microsoft Sans Serif', Verdana, sans-serif; font-size: 9pt; color: #000; margin: 8px 16px 16px 16px; }
        h1 { color: #2B579A; font-size: 14pt; margin: 0 0 12px 0; }
        h2 { color: #2B579A; font-size: 11pt; margin: 16px 0 6px 0; }
        h3 { color: #333; font-size: 10pt; margin: 12px 0 4px 0; }
        p { margin: 4px 0 6px 0; line-height: 1.4; }
        ul, ol { margin: 4px 0 8px 0; padding-left: 24px; }
        li { margin: 2px 0; line-height: 1.4; }
        a { color: #0000CC; text-decoration: underline; }
        a:visited { color: #660066; }
        .note { background: #FFFCE6; border: 1px solid #E0D890; padding: 6px 10px; margin: 8px 0; }
        .note-title { font-weight: bold; color: #8B7500; }
        .warning { background: #FFF0F0; border: 1px solid #E09090; padding: 6px 10px; margin: 8px 0; }
        .warning-title { font-weight: bold; color: #CC0000; }
        .see-also { margin: 12px 0 4px 0; font-style: italic; color: #555; }
        .see-also ul { font-style: normal; color: #000; padding-left: 16px; margin-top: 4px; }
        table { border-collapse: collapse; margin: 8px 0; }
        td, th { border: 1px solid #999; padding: 4px 8px; font-size: 9pt; }
        th { background: #E8E8E8; }
        .kbd { background: #F0F0F0; border: 1px solid #CCC; padding: 1px 4px; font-family: monospace; }
        code { font-family: 'Consolas', monospace; font-size: 9pt; background-color: #F6F6F6; padding: 1px 4px; }
        kbd { background: #E8E8E8; border: 1px solid #CCC; padding: 1px 6px; font-family: 'Microsoft Sans Serif', monospace; font-size: 9pt; white-space: nowrap; }
    ";

    /// <summary>
    /// Style sheet for <paramref name="p"/> (light or dark). The WebBrowser control is Internet
    /// Explorer, which has no CSS custom properties, so the theme colors are written into the
    /// rules directly.
    /// </summary>
    public static string ThemedCss(ThemePalette p)
    {
        static string H(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        bool dark = p.IsDark;
        var link = dark ? Color.FromArgb(0x00, 0xA8, 0xFC) : Color.FromArgb(0x00, 0x5F, 0xCC);
        var heading2 = dark ? ThemePalette.Blend(p.Accent, Color.White, 0.3f) : p.AccentHover;
        var noteText = dark ? p.Warning : Color.FromArgb(0x8A, 0x5A, 0x00);
        var dangerText = dark ? Color.FromArgb(0xF2, 0x6B, 0x6E) : Color.FromArgb(0xC0, 0x24, 0x29);
        var noteBg = ThemePalette.Blend(p.Background, p.Warning, dark ? 0.10f : 0.09f);
        var warnBg = ThemePalette.Blend(p.Background, p.Danger, dark ? 0.10f : 0.07f);
        var codeBg = dark ? p.Tertiary : p.Secondary;
        var thumb = dark ? Color.FromArgb(0x1A, 0x1B, 0x1E) : Color.FromArgb(0xC4, 0xC9, 0xCE);
        var track = dark ? p.Secondary : p.Secondary;
        var sb = new StringBuilder();
        sb.Append($@"
        html, body {{ scrollbar-face-color: {H(thumb)}; scrollbar-track-color: {H(track)}; scrollbar-arrow-color: {H(p.TextMuted)};
            scrollbar-shadow-color: {H(thumb)}; scrollbar-highlight-color: {H(thumb)}; scrollbar-3dlight-color: {H(track)};
            scrollbar-darkshadow-color: {H(track)}; scrollbar-base-color: {H(track)};
            -ms-scrollbar-face-color: {H(thumb)}; -ms-scrollbar-track-color: {H(track)}; -ms-scrollbar-arrow-color: {H(p.TextMuted)};
            -ms-scrollbar-shadow-color: {H(thumb)}; -ms-scrollbar-highlight-color: {H(thumb)}; -ms-scrollbar-3dlight-color: {H(track)};
            -ms-scrollbar-darkshadow-color: {H(track)}; -ms-scrollbar-base-color: {H(track)}; background: {H(p.Background)}; }}
        body {{ font-family: 'Segoe UI', Tahoma, Verdana, sans-serif; font-size: 9.5pt; color: {H(p.Text)}; background: {H(p.Background)};
            margin: 14px 22px 22px 22px; line-height: 1.45; }}
        h1 {{ font-family: 'Segoe UI Semibold', 'Segoe UI', sans-serif; font-weight: normal; color: {H(p.Text)}; font-size: 15pt;
            margin: 0 0 12px 0; padding-bottom: 8px; border-bottom: 1px solid {H(p.Border)}; }}
        h2 {{ font-family: 'Segoe UI Semibold', 'Segoe UI', sans-serif; font-weight: normal; color: {H(heading2)}; font-size: 11.5pt; margin: 18px 0 6px 0; }}
        h3 {{ font-family: 'Segoe UI Semibold', 'Segoe UI', sans-serif; font-weight: normal; color: {H(p.TextSecondary)}; font-size: 10pt; margin: 12px 0 4px 0; }}
        p {{ margin: 4px 0 8px 0; line-height: 1.45; }}
        b, strong {{ font-family: 'Segoe UI Semibold', 'Segoe UI', sans-serif; font-weight: normal; color: {H(p.Text)}; }}
        ul, ol {{ margin: 4px 0 10px 0; padding-left: 24px; }}
        li {{ margin: 3px 0; line-height: 1.45; }}
        a {{ color: {H(link)}; text-decoration: none; }}
        a:visited {{ color: {H(link)}; }}
        a:hover {{ text-decoration: underline; }}
        .note {{ background: {H(noteBg)}; border: 1px solid {H(ThemePalette.Blend(noteBg, p.Warning, 0.35f))}; border-left: 4px solid {H(p.Warning)};
            border-radius: 4px; padding: 8px 12px; margin: 10px 0; }}
        .note-title {{ font-family: 'Segoe UI Semibold', 'Segoe UI', sans-serif; font-weight: normal; color: {H(noteText)}; }}
        .warning {{ background: {H(warnBg)}; border: 1px solid {H(ThemePalette.Blend(warnBg, p.Danger, 0.35f))}; border-left: 4px solid {H(p.Danger)};
            border-radius: 4px; padding: 8px 12px; margin: 10px 0; }}
        .warning-title {{ font-family: 'Segoe UI Semibold', 'Segoe UI', sans-serif; font-weight: normal; color: {H(dangerText)}; }}
        .see-also {{ margin: 16px 0 4px 0; padding-top: 8px; border-top: 1px solid {H(p.Border)}; color: {H(p.TextMuted)}; }}
        .see-also ul {{ color: {H(p.Text)}; padding-left: 18px; margin-top: 4px; }}
        table {{ border-collapse: collapse; margin: 10px 0; }}
        td, th {{ border: 1px solid {H(p.Border)}; padding: 5px 10px; font-size: 9pt; text-align: left; }}
        th {{ background: {H(p.Secondary)}; color: {H(p.Text)}; font-family: 'Segoe UI Semibold', 'Segoe UI', sans-serif; font-weight: normal; }}
        .kbd, kbd {{ background: {H(codeBg)}; color: {H(p.Text)}; border: 1px solid {H(p.Border)}; border-bottom-width: 2px; border-radius: 3px;
            padding: 0 5px; font-family: 'Segoe UI', sans-serif; font-size: 8.5pt; white-space: nowrap; }}
        code {{ font-family: 'Consolas', monospace; font-size: 9pt; color: {H(p.Text)}; background: {H(codeBg)}; border-radius: 3px; padding: 1px 4px; }}
        ::selection {{ background: {H(ThemePalette.Blend(p.Background, p.Accent, 0.45f))}; }}
        ");
        return sb.ToString();
    }

    /// <summary>
    /// Returns <paramref name="html"/> (a page built with <see cref="Wrap"/>) styled for
    /// <paramref name="p"/>: the static style sheet is replaced by <see cref="ThemedCss"/> and the
    /// page is rendered in the current IE document mode (rounded boxes, colors).
    /// </summary>
    public static string ApplyTheme(string html, ThemePalette p)
    {
        const string ieMode = "<meta http-equiv='X-UA-Compatible' content='IE=edge'>";
        string themed = html.Replace("<style>" + Css + "</style>", "<style>" + ThemedCss(p) + "</style>");
        if (!themed.Contains("X-UA-Compatible", StringComparison.Ordinal))
            themed = themed.Replace("<head>", "<head>" + ieMode);
        return themed;
    }

    /// <summary>Wraps body content in a full HTML document with CSS.</summary>
    private static string Wrap(string title, string body) =>
        $"<html><head><meta charset='utf-8'><style>{Css}</style></head><body>" +
        $"<h1>{title}</h1>{body}</body></html>";

    /// <summary>Builds the complete help topic tree.</summary>
    public static HelpTopic BuildTree()
    {
        return new HelpTopic("root", "SpeedScan Manager Hilfe",
            new HelpTopic("getting-started", "Erste Schritte",
                TopicOverview(),
                TopicSystemRequirements(),
                TopicStarting(),
                TopicScannerConnection(),
                TopicScannerSelection()
            ),
            new HelpTopic("scanning", "Scannen von Dokumenten",
                TopicScanQuickMenu(),
                TopicScanWithoutQuickMenu(),
                TopicScanButton(),
                TopicScanLongPages(),
                TopicScanContinue(),
                TopicScanDisplay(),
                TopicScanCarrierSheet()
            ),
            new HelpTopic("settings-group", "Einstellungsdialogfeld",
                TopicSettingsDialog(),
                TopicApplicationTab(),
                TopicSaveTab(),
                TopicScanModeTab(),
                TopicFileTypeTab(),
                TopicPaperTab(),
                TopicFileSizeTab()
            ),
            new HelpTopic("dialogs", "Dialogfelder",
                TopicScanModeOptionsDialog(),
                TopicPdfOptionsDialog(),
                TopicFileNameFormatDialog(),
                TopicNewProfileDialog(),
                TopicProfileManagementDialog(),
                TopicAppManageDialog(),
                TopicEmailOptionsDialog(),
                TopicPrintOptionsDialog(),
                TopicCarrierSheetDialog(),
                TopicCustomSizeDialog(),
                TopicVersionInfoDialog(),
                TopicPostScanMediaDialog(),
                TopicPostScanSaveDialog(),
                TopicScannerDriverInfoDialog()
            ),
            new HelpTopic("profiles", "Profile",
                TopicProfileManagement(),
                TopicDefaultSettings()
            ),
            new HelpTopic("troubleshooting", "Fehlerbehebung",
                TopicMultiFeedDialog(),
                TopicMultiFeedMeasures(),
                TopicMessageList()
            ),
            new HelpTopic("reference", "Anhang",
                TopicAbbreviations()
            )
        );
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Erste Schritte
    // ═══════════════════════════════════════════════════════════════════════

    private static HelpTopic TopicOverview() => new("overview", "Überblick",
        Wrap("Überblick", @"
        <p>SpeedScan Manager ist eine Software für den Betrieb von TWAIN-kompatiblen Scannern.
        Mit SpeedScan Manager können Sie Dokumente einfach und schnell scannen und die gescannten
        Bilder in verschiedenen Formaten speichern oder weiterverarbeiten.</p>

        <h2>Hauptfunktionen</h2>
        <ul>
            <li><b>Quick-Menü</b> – Nach dem Scannen wählen Sie, was mit dem Scan geschehen soll. Die Voreinstellungen
            [Empfohlen], [Kleine Datei], [Hohe Bildqualität] und [Benutzerdefiniert] legen Bildqualität und Komprimierungsrate fest.</li>
            <li><b>Profile</b> – Bis zu 20 Profile mit individuellen Einstellungen können verwaltet werden</li>
            <li><b>Scan-Taste am Scanner</b> – Scannen durch Drücken der Hardware-Taste am Scanner</li>
            <li><b>Scan to Folder</b> – Gescannte Bilder in einem Ordner speichern</li>
            <li><b>Scan to E-Mail</b> – Gescannte Dateien als Anhang einer E-Mail versenden</li>
            <li><b>Scan to Print</b> – Gescannte Bilder direkt drucken</li>
            <li><b>Scan to Word / Excel / PowerPoint</b> – Gescannte Bilder in ein Office-Dokument einfügen (die Datei wird neben dem Scan gespeichert)</li>
            <li><b>Scan Picture Folder</b> – Bilder (JPEG) im Bilderordner speichern</li>
            <li><b>Edit with PDF</b> – Gescannte Datei mit dem dafür eingestellten Windows-Programm öffnen</li>
            <li><b>Eigene Anwendungen</b> – Bis zu zehn eigene Programme (mit optionalen Argumenten) nach dem Scan starten</li>
            <li><b>OCR-Texterkennung</b> – Durchsuchbare PDF-Dateien erstellen, optional mit Schlüsselwörtern aus markiertem Text</li>
            <li><b>Trägerblatt-Unterstützung</b> – Zwei Seiten eines gefalteten Dokuments in einem Bild zusammenfügen</li>
            <li><b>Automatische Bilddrehung</b> – Korrektur der Ausrichtung mittels Tesseract OSD</li>
            <li><b>Leere Seiten entfernen</b> und <b>Schräglauf korrigieren</b></li>
            <li><b>Scanvorgang fortsetzen</b> – Weitere Dokumente an denselben Scan anhängen</li>
        </ul>

        <h2>Unterstützte Dateiformate</h2>
        <ul>
            <li>PDF (*.pdf) – mit Kennwortschutz und OCR-Optionen</li>
            <li>JPEG (*.jpg) – nicht bei Farbmodus Schwarzweiß</li>
            <li>PNG (*.png) – verlustfrei</li>
        </ul>
    "));

    private static HelpTopic TopicSystemRequirements() => new("sysreq", "Systemanforderungen",
        Wrap("Systemanforderungen", @"
        <h2>Betriebssystem</h2>
        <ul>
            <li>Windows 10 (64-bit)</li>
            <li>Windows 11 (64-bit)</li>
        </ul>

        <h2>Scanner</h2>
        <ul>
            <li>TWAIN-kompatibler Scanner (USB oder Netzwerk) mit installiertem TWAIN-Treiber</li>
            <li>SpeedScan Manager läuft als 32-Bit-Anwendung und benötigt daher einen
            Scannertreiber, der eine 32-Bit-TWAIN-Quelle bereitstellt.</li>
        </ul>

        <h2>Software</h2>
        <ul>
            <li>Eine separate Installation der .NET-Laufzeit ist beim Installationspaket nicht erforderlich.</li>
            <li>E-Mail-Programm mit MAPI-Unterstützung (für Scan to E-Mail)</li>
            <li>Drucker-Treiber (für Scan to Print)</li>
            <li>Word, Excel bzw. PowerPoint (oder ein anderes Programm für diese Dateitypen), um die erstellten
            Office-Dateien zu öffnen. Die Dateien selbst werden ohne Office erstellt.</li>
        </ul>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Mitgeliefert sind die Texterkennungs-Sprachdaten für Deutsch und Englisch sowie die Daten
            für die automatische Bilddrehung. Weitere Sprachen siehe
            <a href='#filetype-tab'>[Dateiart] Registerkarte</a>.</p>
        </div>
    "));

    private static HelpTopic TopicStarting() => new("starting", "Starten von SpeedScan Manager",
        Wrap("Starten von SpeedScan Manager", @"
        <p>SpeedScan Manager wird als Tray-Anwendung ausgeführt. Nach dem Start erscheint
        ein Symbol im Benachrichtigungsbereich der Taskleiste. Das Programm kann nur einmal
        gleichzeitig laufen: Wenn Sie es ein zweites Mal starten, wird stattdessen das
        Einstellungsdialogfeld der laufenden Instanz geöffnet.</p>

        <h2>Tray-Symbol</h2>
        <p>Das Tray-Symbol zeigt den aktuellen Status an:</p>
        <ul>
            <li><b>Normales Symbol</b> – Scanner ist verbunden und einsatzbereit</li>
            <li><b>Abgeblendetes Symbol mit rotem Strich</b> – Scanner ist nicht verbunden oder ausgeschaltet</li>
            <li><b>Symbol mit orangem Punkt</b> – Ein Scan läuft bzw. wird gerade verarbeitet</li>
        </ul>
        <p>Der Hinweistext des Symbols (Maus darüberhalten) zeigt den Namen des Scanners, bei einem
        laufenden Scan die Anzahl der bisher gescannten Seiten.</p>

        <h2>Klick auf das Tray-Symbol</h2>
        <ul>
            <li><b>Linksklick</b> – Öffnet das Menü (wie bei ScanSnap Manager)</li>
            <li><b>Rechtsklick</b> – Öffnet ebenfalls das Menü</li>
            <li><b>Doppelklick</b> – Öffnet das Einstellungsdialogfeld</li>
        </ul>

        <h2>Menü</h2>
        <ul>
            <li><b>Scan</b> – Startet einen Scan mit den aktuell übernommenen Einstellungen
            (nur verfügbar, wenn der Scanner nicht als getrennt erkannt wurde und kein Scan läuft)</li>
            <li><b>Scan abbrechen</b> – Nur sichtbar, solange ein Scan läuft</li>
            <li><b>Scanner auswählen...</b> – Öffnet den Dialog zur Scanner-Auswahl</li>
            <li><b>Einstellungen der SCAN Taste...</b> – Öffnet das Einstellungsdialogfeld</li>
            <li><b>Scan-Taste einrichten...</b> – Richtet die Scan-Taste am Scanner (erneut) ein, z. B. nach dem
            Anschließen eines weiteren Scanners oder wenn die Administrator-Abfrage abgelehnt wurde
            (siehe <a href='#scan-button'>Scan-Taste am Scanner</a>)</li>
            <li><b>Profilverwaltung...</b> – Verwaltung der Scan-Profile</li>
            <li><b>E-Mail-Einstellungen...</b> – Öffnet die <a href='#email-options'>Scan to E-Mail-Optionen</a>
            (auch bei aktivem Quick-Menü erreichbar)</li>
            <li><b>Druckeinstellungen...</b> – Öffnet die <a href='#print-options'>Scan to Print-Optionen</a></li>
            <li><b>Scan-Ergebnis anzeigen</b> – Zeigt die zuletzt gespeicherte Datei im Explorer an
            (erst nach einem Scan verfügbar)</li>
            <li><b>Hilfe</b> – Untermenü mit <b>Hilfethemen</b> und <b>Versionsinformationen</b></li>
            <li><b>Beenden</b> – Beendet SpeedScan Manager (bei laufendem Scan nach Rückfrage; wird der Scan
            gerade gespeichert, beendet sich das Programm erst danach)</li>
        </ul>

        <h2>Autostart</h2>
        <p>Im Einstellungsdialogfeld legt das Kontrollkästchen <b>SpeedScan Manager beim Anmelden starten</b>
        fest, ob das Programm mit Windows startet. Bei einer Neuinstallation ist die Option aktiviert;
        nach einem Update von Version 0.1.x bleibt sie ausgeschaltet, bis Sie sie aktivieren.</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Beim Start prüft SpeedScan Manager, ob die Scan-Taste in Windows eingerichtet ist. Falls nicht,
            erscheint einmalig eine Administrator-Abfrage (UAC). Details siehe
            <a href='#scan-button'>Scan-Taste am Scanner</a>.</p>
            <p>Einstellungen und Profile werden im Ordner <code>%AppData%\SpeedScanManager</code> gespeichert.</p>
        </div>
    "));

    private static HelpTopic TopicScannerConnection() => new("scanner-connection", "Scanner verbinden",
        Wrap("Scanner verbinden", @"
        <p>SpeedScan Manager unterstützt sowohl USB- als auch Netzwerk-Scanner. Der Scanner wird
        automatisch erkannt, sobald er eingeschaltet und mit dem Computer verbunden ist.</p>

        <h2>Schritte zum Verbinden</h2>
        <ol>
            <li>Schalten Sie den Scanner ein.</li>
            <li>Verbinden Sie den Scanner über USB oder Netzwerk mit dem Computer.</li>
            <li>SpeedScan Manager erkennt den Scanner automatisch und aktualisiert das Tray-Symbol.</li>
        </ol>

        <p>Beim Anschließen oder Trennen eines Geräts wird der Scannerstatus sofort neu geprüft.
        Zusätzlich erfolgt etwa alle 10 Sekunden eine einfache Prüfung im Hintergrund. Ein Trennen wird erst
        nach zwei aufeinanderfolgenden fehlgeschlagenen Prüfungen gemeldet; die Meldung
        „Der Scanner ist nicht angeschlossen oder ausgeschaltet.“ erscheint dabei nur einmal als Sprechblase.</p>

        <div class='warning'>
            <div class='warning-title'>ACHTUNG</div>
            <ul>
                <li>Schließen Sie nach Möglichkeit nur einen Scanner gleichzeitig an den Computer an
                oder wählen Sie den gewünschten Scanner im Dialog <b>Scanner auswählen</b>.</li>
                <li>Wenn der Scanner nicht erkannt wird, prüfen Sie die Verbindung und stellen Sie
                sicher, dass der Scanner eingeschaltet ist.</li>
                <li>Stellen Sie sicher, dass der TWAIN-Treiber des Scanners installiert ist.</li>
            </ul>
        </div>

        <div class='see-also'>Siehe auch:
            <ul>
                <li><a href='#scanner-selection'>Scanner auswählen</a></li>
            </ul>
        </div>
    "));

    private static HelpTopic TopicScannerSelection() => new("scanner-selection", "Scanner auswählen",
        Wrap("Scanner auswählen", @"
        <p>Im Dialog <b>Scanner auswählen</b> können Sie festlegen, welcher TWAIN-Scanner
        verwendet werden soll. Der Dialog ist über das Tray-Menü unter
        <b>Scanner auswählen...</b> erreichbar (nicht während eines Scans).</p>

        <h2>Elemente</h2>
        <p><b>Auswahlliste</b> – Zeigt alle verfügbaren TWAIN-Quellen an. Die Einträge sind mit
        <b>(USB)</b> bzw. <b>(Netzwerk)</b> gekennzeichnet, USB-Scanner stehen bevorzugt oben.
        Der erste Eintrag <b>(Automatisch)</b> überlässt die Wahl dem Programm.</p>

        <p><b>[OK] Taste</b> – Übernimmt die Auswahl. Sie wird gespeichert und beim nächsten Start wieder verwendet.
        Ist der gewählte Scanner nicht erreichbar, zeigt der Hinweistext des Tray-Symbols
        „Auswahl: ... (offline)“ an.</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Der Dialog aktualisiert sich automatisch, wenn neue Scanner erkannt werden.
            Es ist kein manueller Aktualisieren-Knopf erforderlich. Wird kein Scanner gefunden, kann trotzdem
            <b>(Automatisch)</b> bestätigt werden.</p>
        </div>

        <div class='see-also'>Siehe auch:
            <ul>
                <li><a href='#scanner-connection'>Scanner verbinden</a></li>
            </ul>
        </div>
    "));

    // ═══════════════════════════════════════════════════════════════════════
    // Scannen von Dokumenten
    // ═══════════════════════════════════════════════════════════════════════

    private static HelpTopic TopicScanQuickMenu() => new("scan-quick", "Scannen mit dem Quick-Menü",
        Wrap("Scannen mit dem Quick-Menü", @"
        <p>Wenn das Quick-Menü aktiviert ist (Grundeinstellung), scannen Sie mit den Einstellungen der
        Registerkarten und wählen erst nach dem Scan, was mit den Seiten geschehen soll.</p>

        <h2>Schritte</h2>
        <ol>
            <li>Legen Sie die Dokumente in den Scanner ein.</li>
            <li>Drücken Sie die Scan-Taste am Scanner oder wählen Sie <b>Scan</b> im Tray-Menü.</li>
            <li>Die Seiten werden verarbeitet und in den Speicherordner gespeichert.</li>
            <li>Das <a href='#postscan-media'>[Quick-Menü] Dialogfeld</a> erscheint. Wählen Sie die gewünschte Aktion:</li>
        </ol>
        <ul>
            <li>Scan to Folder (im Dialogfeld „In Ordner speichern“)</li>
            <li>Scan to E-Mail (im Dialogfeld „Per E-Mail versenden“)</li>
            <li>Scan to Print („Drucken“)</li>
            <li>Scan to Word („Nach Word“)</li>
            <li>Scan to Excel („Nach Excel“)</li>
            <li>Scan to PowerPoint („Nach PowerPoint“)</li>
            <li>Scan Picture Folder („In Bilderordner“)</li>
            <li>Edit with PDF („Mit PDF Edit bearbeiten“)</li>
        </ul>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Wenn das Quick-Menü aktiviert ist, ist die [Anwendung] Registerkarte im
            Einstellungsdialogfeld deaktiviert. Die Aktion wird stattdessen nach dem Scannen
            im Quick-Menü ausgewählt. Wird das Quick-Menü über [Abbrechen] oder das Schließen-Symbol beendet,
            bleibt die bereits gespeicherte Datei erhalten.</p>
        </div>

        <h2>Voreinstellungen</h2>
        <p>Die Tasten im Einstellungsdialogfeld setzen Bildqualität und Komprimierungsrate:</p>
        <ul>
            <li><b>Empfohlen</b> – Bildqualität Automatisch, Komprimierungsrate 3</li>
            <li><b>Kleine Datei</b> – Bildqualität Normal, Komprimierungsrate 5</li>
            <li><b>Hohe Bildqualität</b> – Bildqualität Fein, Komprimierungsrate 1</li>
            <li><b>Benutzerdefiniert</b> – Öffnet die Detaileinstellungen; aktiv, sobald die Werte keiner der
            drei Voreinstellungen entsprechen</li>
        </ul>

        <div class='see-also'>Siehe auch:
            <ul>
                <li><a href='#settings'>Einstellungsdialogfeld</a></li>
                <li><a href='#scan-no-quick'>Scannen ohne Quick-Menü</a></li>
                <li><a href='#postscan-media'>[Quick-Menü] Dialogfeld</a></li>
            </ul>
        </div>
    "));

    private static HelpTopic TopicScanWithoutQuickMenu() => new("scan-no-quick", "Scannen ohne Quick-Menü",
        Wrap("Scannen ohne Quick-Menü", @"
        <p>Wenn das Quick-Menü deaktiviert ist, können Sie detaillierte Scaneinstellungen
        über das Einstellungsdialogfeld vornehmen und Profile verwenden. Die nach dem Scan
        ausgeführte Aktion wird durch die <b>[Anwendung]</b> Registerkarte bestimmt (bzw. durch das gewählte Profil).</p>

        <h2>Schritte</h2>
        <ol>
            <li>Wählen Sie ein Profil aus der [Profil] Auswahlliste im Einstellungsdialogfeld.</li>
            <li>Passen Sie die Einstellungen auf den Registerkarten an (Anwendung, Speichern, Scanmodus, Dateiart, Papier, Dateigröße).</li>
            <li>Schließen Sie das Einstellungsdialogfeld mit <span class='kbd'>OK</span>.</li>
            <li>Legen Sie die Dokumente in den Scanner ein und starten Sie den Scan über das Tray-Menü oder die Scan-Taste.</li>
        </ol>

        <h2>Was nach dem Scan geschieht</h2>
        <ul>
            <li><b>Scan to Folder</b> – Es erscheint das <a href='#postscan-save'>[In Ordner speichern] Dialogfeld</a>.</li>
            <li><b>Andere Anwendungen</b> – Die Aktion wird sofort ausgeführt. Ist [Datei nach Scan umbenennen]
            markiert, erscheint vorher das [In Ordner speichern] Dialogfeld.</li>
        </ul>

        <div class='see-also'>Siehe auch:
            <ul>
                <li><a href='#settings'>Einstellungsdialogfeld</a></li>
                <li><a href='#profile-mgmt'>Profile verwalten</a></li>
                <li><a href='#app-tab'>[Anwendung] Registerkarte</a></li>
            </ul>
        </div>
    "));

    private static HelpTopic TopicScanButton() => new("scan-button", "Scan-Taste am Scanner",
        Wrap("Scan-Taste am Scanner", @"
        <p>SpeedScan Manager unterstützt die Hardware-Scan-Taste am Scanner. Wird die Taste gedrückt,
        startet ein Scanvorgang mit den aktuell übernommenen Einstellungen.</p>

        <h2>Funktionsweise</h2>
        <ul>
            <li>SpeedScan Manager trägt sich in Windows als Programm für das Ereignis „Scan-Taste“ des
            Scanners ein (Windows-Schnittstelle für Still-Image-/WIA-Geräte). Dabei wird der
            Standardhandler von Windows ersetzt.</li>
            <li>Beim Tastendruck startet Windows <code>SpeedScanManager.exe</code> direkt mit einem
            Scan-Befehl. Läuft SpeedScan Manager bereits, wird der Befehl an die laufende Instanz weitergegeben;
            andernfalls startet das Programm und scannt sofort, sobald es bereit ist.</li>
            <li>Beim Deinstallieren wird der ursprüngliche Windows-Handler wiederhergestellt.</li>
        </ul>

        <h2>Einrichtung</h2>
        <p>Die Einrichtung erfordert Administratorrechte und erfolgt beim Start einmalig mit einer
        UAC-Abfrage. Danach sind keine Administratorrechte mehr nötig. Wird später ein weiterer Scanner
        angeschlossen, erscheint die Abfrage beim nächsten Start noch einmal, damit auch seine Taste eingerichtet wird.
        Wenn Sie die Abfrage ablehnen, wird die Scan-Taste nicht eingerichtet und die Abfrage nicht erneut angezeigt;
        Sie können weiterhin über das Tray-Menü scannen. Mit <b>Scan-Taste einrichten...</b> im Tray-Menü
        starten Sie die Einrichtung jederzeit erneut (eine UAC-Abfrage).</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Während ein Scan läuft (einschließlich aller Dialogfelder nach dem Scan), wird die Taste ignoriert.
            Zwei Tastendrücke innerhalb von etwa 1,5 Sekunden zählen als ein Druck. Die Taste funktioniert
            nicht, wenn das Programm den Scanner nicht verwenden kann (z. B. TWAIN nicht initialisiert).</p>
        </div>

        <div class='see-also'>Siehe auch:
            <ul>
                <li><a href='#scanner-connection'>Scanner verbinden</a></li>
                <li><a href='#starting'>Starten von SpeedScan Manager</a></li>
            </ul>
        </div>
    "));

    private static HelpTopic TopicScanLongPages() => new("scan-long", "Scannen langer Seiten",
        Wrap("Scannen langer Seiten", @"
        <p>Lange Dokumente (z. B. Quittungen oder Endlosdokumente) können gescannt werden,
        wenn für die Papiergröße <b>Automatische Erkennung</b> ausgewählt ist und die
        Mehrfacheinzugserkennung nicht auf <b>Überprüfung der Länge</b> oder
        <b>Überprüfung von Überlappung und Länge</b> steht.</p>

        <p>In diesem Modus (Langseitenmodus) gilt Folgendes:</p>
        <ul>
            <li>Der Scanbereich wird auf bis zu 8,5 × 125 Zoll gesetzt.</li>
            <li>Bei Scannern der Hersteller Fujitsu, PFU und Ricoh (PaperStream IP) werden zusätzlich
            herstellerspezifische Einstellungen für lange Seiten gesetzt.</li>
            <li>Ob das Ergebnis auf die tatsächliche Papiergröße zugeschnitten wird, hängt vom Scannertreiber ab.</li>
        </ul>

        <div class='warning'>
            <div class='warning-title'>ACHTUNG</div>
            <ul>
                <li>Die maximale Länge hängt vom angeschlossenen Scannermodell ab.</li>
                <li>Lange Dokumente können zu einer größeren Dateigröße führen.</li>
            </ul>
        </div>
    "));

    private static HelpTopic TopicScanContinue() => new("scan-continue", "Scan abbrechen und fortsetzen",
        Wrap("Scan abbrechen und fortsetzen", @"
        <h2>Scan abbrechen</h2>
        <p>Solange ein Scan läuft, steht im Tray-Menü der Eintrag <b>Scan abbrechen</b> zur Verfügung.
        Die bis dahin gescannten Seiten werden verworfen; es erscheint die Sprechblase „Scan abgebrochen“.
        Das gilt auch, während die Meldung zum Mehrfacheinzug oder die Frage „Weitere Dokumente scannen?“ angezeigt wird.
        Wenn Sie <b>Beenden</b> wählen, während ein Scan läuft, werden Sie gefragt, ob der Scan abgebrochen
        und das Programm beendet werden soll. Wird der Scan bereits verarbeitet und gespeichert, beendet sich
        SpeedScan Manager erst, wenn die Datei vollständig geschrieben ist; bereits gespeicherte Scans werden
        beim Beenden (auch beim Abmelden von Windows) nie gelöscht.</p>

        <h2>Scanvorgang fortsetzen</h2>
        <p>Ist auf der [Scanmodus] Registerkarte das Kontrollkästchen
        <b>Scanvorgang nach aktuellem Scan fortsetzen</b> markiert, fragt SpeedScan Manager nach jedem
        Durchgang „Weitere Dokumente scannen?“ und nennt die bisherige Seitenzahl.</p>
        <ul>
            <li><b>Fortsetzen</b> – Legen Sie die nächsten Seiten ein; sie werden an die bisherigen Seiten angehängt.</li>
            <li><b>Scan beenden</b> – Alle Seiten werden gemeinsam verarbeitet und gespeichert.</li>
        </ul>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Wird bei einem Durchgang ein Mehrfacheinzug mit [Wiederholen] verworfen, bleiben bereits
            gescannte frühere Durchgänge erhalten.</p>
        </div>
    "));

    private static HelpTopic TopicScanDisplay() => new("scan-display", "Aktionen nach dem Scannen",
        Wrap("Aktionen nach dem Scannen", @"
        <p>Nach Abschluss des Scans werden die Seiten verarbeitet und als Datei(en) im Speicherordner abgelegt.
        Ist der Zielordner nicht beschreibbar, wird stattdessen in <code>Dokumente\SpeedScanManager</code> gespeichert
        und eine Meldung angezeigt. Vorhandene Dateien werden nie überschrieben.
        Danach wird die gewählte Aktion ausgeführt.</p>

        <h2>Bei aktivem Quick-Menü</h2>
        <p>Das <a href='#postscan-media'>[Quick-Menü] Dialogfeld</a> erscheint und bietet folgende Aktionen:</p>
        <ul>
            <li><b>Scan to Folder</b> – Öffnet das <a href='#postscan-save'>[In Ordner speichern] Dialogfeld</a></li>
            <li><b>Scan to E-Mail</b> – Öffnet das E-Mail-Programm (MAPI) mit der Datei als Anhang</li>
            <li><b>Scan to Print</b> – Druckt die gescannten Seiten in Originalgröße</li>
            <li><b>Scan to Word / Excel / PowerPoint</b> – Erstellt ein Office-Dokument aus den Seiten
            (JPEG-Bilder in Originalgröße), speichert es neben dem Scan und öffnet es</li>
            <li><b>Scan Picture Folder</b> – Speichert die Seiten als JPEG-Dateien in
            <code>Bilder\SpeedScanManager</code> und zeigt die erste im Explorer an</li>
            <li><b>Edit with PDF</b> – Öffnet die gescannte(n) Datei(en) mit dem in Windows zugeordneten Programm</li>
        </ul>

        <h2>Bei deaktiviertem Quick-Menü</h2>
        <p>Die in der <a href='#app-tab'>[Anwendung] Registerkarte</a> ausgewählte Aktion (einschließlich
        eigener Anwendungen) wird direkt ausgeführt. Bei Scan to Folder erscheint das
        [In Ordner speichern] Dialogfeld; bei anderen Anwendungen nur, wenn [Datei nach Scan umbenennen] markiert ist.</p>

        <h2>Nachverarbeitung</h2>
        <p>Vor dem Speichern werden folgende Schritte angewendet (je nach Einstellungen):</p>
        <ul>
            <li><b>Trägerblatt</b> – Vorder- und Rückseite werden nebeneinander zusammengefügt, Ausgabegröße angewendet</li>
            <li><b>Leere Seiten entfernen</b> – Wenn alle Seiten leer sind, wird nichts gespeichert und eine Meldung angezeigt</li>
            <li><b>Schräglauf korrigieren</b> (falls aktiviert)</li>
            <li><b>Automatische Bilddrehung</b> mittels Tesseract OSD (falls aktiviert)</li>
            <li><b>Automatische Farberkennung</b> – Seiten werden in Farbe, Grau oder Schwarzweiß umgewandelt (bei Farbmodus „Automatische Farberkennung“)</li>
            <li><b>Texterkennung (OCR)</b> und PDF-Erstellung bzw. Speichern als JPEG/PNG</li>
        </ul>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Mit <b>Scan-Ergebnis anzeigen</b> im Tray-Menü zeigen Sie die zuletzt gespeicherte Datei im Explorer an.</p>
        </div>
    "));

    private static HelpTopic TopicScanCarrierSheet() => new("scan-carrier", "Scannen mit dem Trägerblatt",
        Wrap("Scannen mit dem Trägerblatt", @"
        <p>Mit dem Trägerblatt können Sie Dokumente scannen, die größer als das normale Scanformat sind
        (z. B. A3). Das Dokument wird in der Mitte gefaltet und in das Trägerblatt eingelegt.
        Beide Seiten werden gescannt und im Ausgabebild nebeneinander zusammengefügt.</p>

        <h2>Ablauf</h2>
        <ol>
            <li>Aktivieren Sie in der [Papier] Registerkarte unter [Trägerblatteinstellungen] den Trägerblatt-Modus
            und wählen Sie die Ausgabebildgröße.</li>
            <li>Falten Sie das Dokument entlang der Mittelachse und legen Sie es in das Trägerblatt ein.</li>
            <li>Führen Sie das Trägerblatt in den Scanner ein. Verwenden Sie dazu einen Duplex-Scan, damit
            Vorder- und Rückseite des Trägerblatts zu einem Bild zusammengefügt werden.</li>
        </ol>

        <div class='warning'>
            <div class='warning-title'>ACHTUNG</div>
            <ul>
                <li>Nur Scanner, die für das Trägerblatt ausgelegt sind, ziehen es ein.</li>
                <li>Das Zusammenfügen von Vorder- und Rückseite erfolgt nur bei Duplex-Scan.
                Bei einseitigem Scan wird jede Seite einzeln auf die Ausgabegröße gebracht.</li>
            </ul>
        </div>

        <div class='see-also'>Siehe auch:
            <ul>
                <li><a href='#carrier-sheet-dialog'>[Trägerblatteinstellungen] Dialogfeld</a></li>
            </ul>
        </div>
    "));

    // ═══════════════════════════════════════════════════════════════════════
    // Einstellungsdialogfeld
    // ═══════════════════════════════════════════════════════════════════════

    private static HelpTopic TopicSettingsDialog() => new("settings", "SpeedScan Manager Einstellungsdialogfeld",
        Wrap("SpeedScan Manager Einstellungsdialogfeld", @"
        <p>Im SpeedScan Manager Einstellungsdialogfeld können Sie verschiedene Einstellungen
        für das Scannen von Dokumenten konfigurieren. Es wird durch Doppelklick auf das
        Tray-Symbol oder über <b>Einstellungen der SCAN Taste...</b> im Tray-Menü geöffnet.
        Die Änderungen werden erst mit [OK] oder [Übernehmen] wirksam; [Abbrechen], die Taste
        <span class='kbd'>Esc</span> und das Schließen-Symbol verwerfen nicht übernommene Änderungen.
        Mit dem Hilfe-Symbol (?) in der Titelleiste öffnen Sie diese Hilfe.</p>

        <h2>Elemente</h2>
        <p><b>[Quick-Menü verwenden] Kontrollkästchen</b><br>
        Markieren Sie dieses Kontrollkästchen, um das Quick-Menü zu verwenden. Entfernen Sie
        die Markierung, falls Sie das Quick-Menü nicht verwenden möchten.</p>

        <p><b>[SpeedScan Manager beim Anmelden starten] Kontrollkästchen</b><br>
        Startet das Programm automatisch bei der Windows-Anmeldung (Grundeinstellung: markiert).</p>

        <p><b>[Empfohlen] / [Kleine Datei] / [Hohe Bildqualität] Tasten</b><br>
        Setzen Bildqualität und Komprimierungsrate (siehe
        <a href='#scan-quick'>Scannen mit dem Quick-Menü</a>). Die Taste, die den aktuellen Werten
        entspricht, ist hervorgehoben. Die Tasten sind auch ohne Quick-Menü sichtbar.</p>

        <p><b>[Benutzerdefiniert] Taste</b><br>
        Nur bei aktivem Quick-Menü sichtbar. Blendet die Detaileinstellungen ein; die Taste ist hervorgehoben,
        wenn die Werte keiner Voreinstellung entsprechen.</p>

        <p><b>[Profil] Auswahlliste</b><br>
        Erscheint, wenn das Quick-Menü nicht verwendet wird. Sie können Profile wechseln,
        mit <b>Profil hinzufügen...</b> ein neues Profil aus den aktuellen Einstellungen anlegen oder mit
        <b>Profilverwaltung...</b> Profile verwalten. Änderungen an einem eigenen Profil werden beim Übernehmen
        in diesem Profil gespeichert. Die Standardprofile können nicht geändert werden: Es wird gefragt, ob die
        Einstellungen als neues Profil gespeichert oder nur als aktuelle Einstellungen übernommen werden sollen.</p>

        <p><b>[Detail] / [Ausblenden] Taste</b><br>
        Zeigt oder blendet die Detaileinstellungsregisterkarten aus.</p>

        <p><b>Detaileinstellungsregisterkarten</b></p>
        <ul>
            <li>Mit Quick-Menü: Speichern, Scanmodus, Dateiart, Papier, Dateigröße (die Registerkarte Anwendung ist deaktiviert)</li>
            <li>Ohne Quick-Menü: Anwendung, Speichern, Scanmodus, Dateiart, Papier, Dateigröße</li>
        </ul>

        <p><b>[OK] Taste</b> – Einstellungen übernehmen und Dialogfeld schließen.<br>
        <b>[Abbrechen] Taste</b> – Änderungen verwerfen und Dialogfeld schließen.<br>
        <b>[Übernehmen] Taste</b> – Änderungen übernehmen, ohne das Dialogfeld zu schließen.
        Die Taste ist nur aktiv, wenn es nicht übernommene Änderungen gibt.</p>

        <div class='see-also'>Siehe auch:
            <ul>
                <li><a href='#app-tab'>[Anwendung] Registerkarte</a></li>
                <li><a href='#save-tab'>[Speichern] Registerkarte</a></li>
                <li><a href='#scanmode-tab'>[Scanmodus] Registerkarte</a></li>
                <li><a href='#filetype-tab'>[Dateiart] Registerkarte</a></li>
                <li><a href='#paper-tab'>[Papier] Registerkarte</a></li>
                <li><a href='#filesize-tab'>[Dateigröße] Registerkarte</a></li>
            </ul>
        </div>
    "));

    private static HelpTopic TopicApplicationTab() => new("app-tab", "[Anwendung] Registerkarte",
        Wrap("[Anwendung] Registerkarte", @"
        <p>In der [Anwendung] Registerkarte können Sie eine Anwendung wählen, die nach dem
        Scannen zur Weiterverarbeitung der Bilddaten verwendet werden soll.</p>

        <div class='warning'>
            <div class='warning-title'>ACHTUNG</div>
            <p>Die [Anwendung] Registerkarte ist deaktiviert, wenn das Quick-Menü verwendet wird.
            Wählen Sie die auszuführende Aktion dann im Quick-Menü nach dem Scannen.</p>
        </div>

        <h2>Elemente</h2>
        <p><b>[Anwendung] Auswahlliste</b><br>
        Zeigt eine Liste der verfügbaren Anwendungen:</p>
        <ul>
            <li>Scan to Folder</li>
            <li>Scan to E-Mail</li>
            <li>Scan to Print</li>
            <li>Scan to Word</li>
            <li>Scan to Excel</li>
            <li>Scan to PowerPoint</li>
            <li>Scan Picture Folder</li>
            <li>Edit with PDF</li>
        </ul>
        <p>Darunter erscheinen die eigenen Anwendungen, die über das [Installieren/Deinstallieren]
        Dialogfeld hinzugefügt wurden. Nach dem Scan wird eine eigene Anwendung mit ihren Argumenten und
        den gespeicherten Dateien als weiteren Argumenten gestartet.</p>

        <p><b>[E-Mail-Optionen...] Taste</b><br>
        Erscheint, wenn Scan to E-Mail ausgewählt wurde. Öffnet das
        <a href='#email-options'>[Scan to E-Mail-Optionen] Dialogfeld</a>.</p>

        <p><b>[Druck-Optionen...] Taste</b><br>
        Erscheint, wenn Scan to Print ausgewählt wurde. Öffnet das
        <a href='#print-options'>[Scan to Print-Optionen] Dialogfeld</a>.</p>

        <p><b>[Installieren/Deinstallieren...] Taste</b><br>
        Öffnet das <a href='#app-manage'>[Installieren/Deinstallieren] Dialogfeld</a>.</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Die E-Mail- und Druckeinstellungen sind auch im Tray-Menü erreichbar
            (<b>E-Mail-Einstellungen...</b>, <b>Druckeinstellungen...</b>), z. B. bei aktivem Quick-Menü.
            Dort geänderte Werte gelten sofort und werden auch im aktiven eigenen Profil gespeichert
            (Standardprofile bleiben unverändert).</p>
        </div>

        <div class='see-also'>Siehe auch:
            <ul>
                <li><a href='#settings'>Einstellungsdialogfeld</a></li>
                <li><a href='#app-manage'>[Installieren/Deinstallieren] Dialogfeld</a></li>
            </ul>
        </div>
    "));

    private static HelpTopic TopicSaveTab() => new("save-tab", "[Speichern] Registerkarte",
        Wrap("[Speichern] Registerkarte", @"
        <p>Unter der [Speichern] Registerkarte können Sie den Zielordner für die gescannten
        Bilder bestimmen.</p>

        <h2>Elemente</h2>
        <p><b>Speicherordner für Bilddaten</b><br>
        Zeigt den aktuellen Zielordner an. Das Verzeichnis kann nicht direkt bearbeitet werden.
        Verwenden Sie die [Durchsuchen] Taste, um einen Ordner zu wählen.</p>

        <p><b>[Durchsuchen] Taste</b><br>
        Zeigt einen Ordner-Dialog, in dem Sie einen Ordner für das Speichern der Bilder auswählen können.</p>

        <p><b>[Dateinamenformat] Taste</b><br>
        Zeigt das <a href='#filename-format'>[Dateinamenformat] Dialogfeld</a> an, in dem das
        Dateinamenformat bestimmt werden kann. Darunter wird ein Beispiel für den nächsten Dateinamen angezeigt.</p>

        <p><b>[Datei nach Scan umbenennen] Kontrollkästchen</b><br>
        Wenn markiert, erscheint nach dem Scannen das <a href='#postscan-save'>[In Ordner speichern]
        Dialogfeld</a>, in dem Ziel oder Dateiname geändert werden können. Bei Scan to Folder und bei
        aktivem Quick-Menü wird dieses Dialogfeld unabhängig von der Markierung angezeigt, wenn „In Ordner speichern“
        gewählt wird.</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Der Standard-Speicherordner ist <code>Dokumente\SpeedScanManager</code> im
            Benutzerprofil. Wenn mehrere Dateien erstellt werden (z. B. ein Bild pro Seite oder eine
            geteilte PDF-Datei), wird dem Dateinamen eine Seriennummer angefügt. Bestehende Dateien werden
            nie überschrieben; bei gleichem Namen wird <code>_1</code>, <code>_2</code> usw. angehängt.</p>
        </div>

        <div class='see-also'>Siehe auch:
            <ul>
                <li><a href='#filename-format'>[Dateinamenformat] Dialogfeld</a></li>
                <li><a href='#postscan-save'>[In Ordner speichern] Dialogfeld</a></li>
                <li><a href='#settings'>Einstellungsdialogfeld</a></li>
            </ul>
        </div>
    "));

    private static HelpTopic TopicScanModeTab() => new("scanmode-tab", "[Scanmodus] Registerkarte",
        Wrap("[Scanmodus] Registerkarte", @"
        <p>In der [Scanmodus] Registerkarte können Sie die Scanmodi für den Scanner festlegen.</p>

        <h2>[Bildqualität] Auswahlliste</h2>
        <table>
            <tr><th>Bildqualität</th><th>Farbe / Grau</th><th>Schwarzweiß</th></tr>
            <tr><td>Automatisch</td><td>200 dpi</td><td>400 dpi</td></tr>
            <tr><td>Normal</td><td>150 dpi</td><td>300 dpi</td></tr>
            <tr><td>Fein</td><td>200 dpi</td><td>400 dpi</td></tr>
            <tr><td>Beste</td><td>300 dpi</td><td>600 dpi</td></tr>
            <tr><td>Hervorragend</td><td>600 dpi</td><td>1200 dpi</td></tr>
        </table>
        <p>„Automatisch“ verwendet dieselbe Auflösung wie „Fein“. Unterstützt der Scanner die gewünschte
        Auflösung nicht, wird die nächstliegende verwendet und eine Sprechblase weist darauf hin.</p>
        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Mit höherer Auflösung nimmt die benötigte Zeit zum Scannen zu und die Dateigröße wird größer.</p>
        </div>

        <h2>[Farbmodus] Auswahlliste</h2>
        <ul>
            <li><b>Automatische Farberkennung</b> – Die Seiten werden in Farbe gescannt und anschließend einzeln als Farbe, Grau oder Schwarzweiß erkannt und umgewandelt</li>
            <li><b>Farbe</b> – Dokumente werden immer als Farbbilder gespeichert</li>
            <li><b>Grau</b> – Dokumente werden immer als Graubilder gespeichert</li>
            <li><b>Schwarzweiß</b> – Dokumente werden immer als Schwarzweiß-Bilder gespeichert</li>
        </ul>
        <div class='warning'>
            <div class='warning-title'>ACHTUNG</div>
            <p>Bei der [Automatische Farberkennung] können Dokumente mit wenig Farbanteil als Grau oder
            Schwarzweiß erkannt werden. Die Grundeinstellung ist [Farbe].</p>
        </div>

        <h2>[Scan-Seite] Auswahlliste</h2>
        <ul>
            <li><b>Duplex-Scan (doppelseitig)</b> – Scannt beide Seiten des Dokuments</li>
            <li><b>Simplex-Scan (einseitig)</b> – Scannt nur eine Seite des Dokuments</li>
            <li><b>Flachbettscannen</b> – Scannen über das Flachbett</li>
            <li><b>Automatisch</b> (Grundeinstellung) – Ist Papier im Einzug eingelegt, wird im Duplex-Modus
            eingezogen und leere Rückseiten werden entfernt; sonst wird, falls vorhanden, das Flachbett verwendet</li>
        </ul>
        <p>Unterstützt der Scanner Duplex bzw. Flachbett nicht, wird einseitig gescannt bzw. der Einzug verwendet
        und eine Sprechblase weist darauf hin. Für das Flachbettscannen steht die automatische
        Papiergrößenerkennung nicht zur Verfügung; die Papiergröße wird dann auf A4 gesetzt.</p>

        <p><b>[Scanvorgang nach aktuellem Scan fortsetzen] Kontrollkästchen</b><br>
        Ist dieses Kontrollkästchen markiert, erscheint nach dem Scannen die Abfrage „Weitere Dokumente scannen?“
        (siehe <a href='#scan-continue'>Scan abbrechen und fortsetzen</a>).</p>

        <p><b>[Option] Taste</b><br>
        Zeigt das <a href='#scanmode-options'>[Scanmodus Option] Dialogfeld</a> an.</p>

        <div class='see-also'>Siehe auch:
            <ul>
                <li><a href='#scanmode-options'>[Scanmodus Option] Dialogfeld</a></li>
                <li><a href='#paper-tab'>[Papier] Registerkarte</a></li>
            </ul>
        </div>
    "));

    private static HelpTopic TopicFileTypeTab() => new("filetype-tab", "[Dateiart] Registerkarte",
        Wrap("[Dateiart] Registerkarte", @"
        <p>Unter der [Dateiart] Registerkarte können Sie das Dateiformat für die gescannten
        Bilder festlegen.</p>

        <h2>[Dateiformat] Auswahlliste</h2>
        <ul>
            <li><b>PDF (*.pdf)</b> – Alle Bilder werden in einer PDF-Datei zusammengefasst (oder gemäß den PDF-Optionen aufgeteilt)</li>
            <li><b>JPEG (*.jpg)</b> – Für jede gescannte Seite wird eine JPEG-Datei erstellt</li>
            <li><b>PNG (*.png)</b> – Für jede gescannte Seite wird eine PNG-Datei erstellt</li>
        </ul>
        <div class='warning'>
            <div class='warning-title'>ACHTUNG</div>
            <p>JPEG steht nicht zur Verfügung, wenn [Schwarzweiß] unter [Farbmodus]
            in der [Scanmodus] Registerkarte gewählt wurde. Die Optionen zur Texterkennung und zu
            Schlüsselwörtern sind nur bei PDF verfügbar.</p>
        </div>

        <h2>Texterkennung (OCR)</h2>
        <p><b>[In durchsuchbare PDF konvertieren] Kontrollkästchen</b><br>
        Führt OCR durch und erstellt eine durchsuchbare PDF-Datei.
        Der erkannte Text wird als unsichtbare Schicht über das Bild gelegt.</p>

        <p><b>[Markierten Text als Schlüsselwort der PDF-Datei hinzufügen] Kontrollkästchen</b><br>
        Mit Textmarker hervorgehobene Textstellen werden als Schlüsselwörter in die PDF-Eigenschaften eingetragen.
        Dazu ist die Texterkennung nötig; ohne sie wird die Option übersprungen und eine Sprechblase angezeigt.
        Unter <b>Zielmarkierung</b> wählen Sie, ob nur die erste oder alle markierten Stellen verwendet werden.</p>

        <p><b>[Sprache] Auswahlliste</b><br>
        Die Liste enthält <b>Automatische Erkennung</b> und die installierten Sprachen.
        Mitgeliefert sind die Sprachdaten für <b>Deutsch</b> und <b>Englisch</b>.
        Weitere Sprachen (Japanisch, Französisch, Italienisch, Spanisch, Chinesisch vereinfacht/traditionell,
        Koreanisch, Russisch, Portugiesisch, Arabisch) erscheinen in der Liste, sobald Sie die passende
        <code>.traineddata</code>-Datei (z. B. <code>fra.traineddata</code>, <code>jpn.traineddata</code>,
        <code>chi_sim.traineddata</code>) in den Ordner <code>tessdata</code> neben <code>SpeedScanManager.exe</code> legen.
        Ist eine gespeicherte Sprache nicht mehr installiert, wird sie mit dem Zusatz „(nicht installiert)“ angezeigt;
        beim Scannen erscheint dann eine Sprechblase und es werden die vorhandenen Sprachen (bevorzugt Deutsch/Englisch) verwendet.
        Bei <b>Automatische Erkennung</b> wird anhand der erkannten Schrift pro Seite die passende installierte Sprache
        gewählt, sonst Deutsch und Englisch.</p>

        <p><b>Zielseiten</b></p>
        <ul>
            <li><b>Erste Seite</b> – Texterkennung nur für die erste Seite</li>
            <li><b>Alle Seiten</b> – Texterkennung für alle Seiten</li>
        </ul>

        <p><b>[Option] Taste</b><br>
        Öffnet das <a href='#pdf-options'>[PDF-Dateiformat Option] Dialogfeld</a>.
        Nur verfügbar, wenn PDF als Dateiformat gewählt wurde.</p>

        <div class='see-also'>Siehe auch:
            <ul>
                <li><a href='#pdf-options'>[PDF-Dateiformat Option] Dialogfeld</a></li>
                <li><a href='#scanmode-tab'>[Scanmodus] Registerkarte</a></li>
            </ul>
        </div>
    "));

    private static HelpTopic TopicPaperTab() => new("paper-tab", "[Papier] Registerkarte",
        Wrap("[Papier] Registerkarte", @"
        <p>In der [Papier] Registerkarte können Sie die Papiergröße für das zu scannende
        Dokument einstellen.</p>

        <h2>[Papiergröße] Auswahlliste</h2>
        <ul>
            <li>Automatische Erkennung</li>
            <li>A3 (297 × 420 mm)</li>
            <li>A4 (210 × 297 mm)</li>
            <li>A5 (148 × 210 mm)</li>
            <li>A6 (105 × 148 mm)</li>
            <li>B4(JIS) (257 × 364 mm)</li>
            <li>B5(JIS) (182 × 257 mm)</li>
            <li>B6(JIS) (128 × 182 mm)</li>
            <li>Postkarte (100 × 148 mm)</li>
            <li>Visitenkarte (55 × 91 mm)</li>
            <li>Letter (8,5 × 11 in. / 216 × 279,4 mm)</li>
            <li>Double Letter (11 × 17 in. / 279,4 × 431,8 mm)</li>
            <li>Legal (8,5 × 14 in. / 216 × 355,6 mm)</li>
            <li>Danach folgen Ihre benutzerdefinierten Größen (bis zu 10)</li>
        </ul>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Die verfügbaren Papiergrößen hängen vom verwendeten Scannermodell ab. Wird eine Größe nicht
            unterstützt, erscheint eine Sprechblase. Bei [Automatische Erkennung] bestimmt der Scannertreiber den
            Scanbereich (siehe <a href='#scan-long'>Scannen langer Seiten</a>). Für das Flachbettscannen ist
            die automatische Erkennung nicht verfügbar.</p>
        </div>

        <p><b>[Trägerblatteinstellungen] Taste</b><br>
        Öffnet das <a href='#carrier-sheet-dialog'>[Trägerblatteinstellungen] Dialogfeld</a>.</p>

        <p><b>[Benutzerdefiniert] Taste</b><br>
        Zeigt das <a href='#custom-size-dialog'>[Hinzufügen oder Entfernen von benutzerdefinierten Größen] Dialogfeld</a> an.</p>

        <h2>[Mehrfacheinzugserkennung] Auswahlliste</h2>
        <ul>
            <li><b>Aus</b> – Mehrfacheinzugserkennung wird nicht ausgeführt (Grundeinstellung)</li>
            <li><b>Überprüfung von Überlappung [Ultraschall]</b> – Überwacht die Blattstärken der eingezogenen Dokumente</li>
            <li><b>Überprüfung der Länge</b> – Überwacht die Blattlängen der eingezogenen Dokumente</li>
            <li><b>Überprüfung von Überlappung und Länge</b> – Beide Prüfungen kombiniert; unterstützt der Scanner nicht beide gleichzeitig, wird nur Ultraschall verwendet</li>
        </ul>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Die verfügbaren Mehrfacheinzugserkennungs-Modi hängen vom Scannermodell ab.
            Meldet der verbundene Scanner seine Fähigkeiten, werden nicht unterstützte Einträge
            mit „(nicht unterstützt)“ gekennzeichnet und können nicht gewählt werden.</p>
        </div>

        <div class='see-also'>Siehe auch:
            <ul>
                <li><a href='#carrier-sheet-dialog'>[Trägerblatteinstellungen] Dialogfeld</a></li>
                <li><a href='#custom-size-dialog'>[Hinzufügen oder Entfernen von benutzerdefinierten Größen] Dialogfeld</a></li>
            </ul>
        </div>
    "));

    private static HelpTopic TopicFileSizeTab() => new("filesize-tab", "[Dateigröße] Registerkarte",
        Wrap("[Dateigröße] Registerkarte", @"
        <p>Unter der [Dateigröße] Registerkarte können Sie die Komprimierungsrate für die
        gescannten Bilddaten bestimmen.</p>

        <div class='warning'>
            <div class='warning-title'>ACHTUNG</div>
            <p>Die Einstellung ist deaktiviert (mit Hinweis), wenn [Schwarzweiß] in der
            [Farbmodus] Auswahlliste unter der [Scanmodus] Registerkarte ausgewählt wurde, sowie bei PNG als Dateiformat.
            Schwarzweißbilder werden verlustfrei komprimiert, PNG-Dateien verlustfrei gespeichert.</p>
        </div>

        <h2>[Komprimierungsrate] Schieber</h2>
        <p>Die Komprimierungsrate kann fünfstufig eingestellt werden (Grundeinstellung: 3). Sie bestimmt
        die JPEG-Qualität der Farb- und Graubilder (auch in PDF-Dateien, Office-Dokumenten und im Bilderordner):</p>
        <table>
            <tr><th>Stufe</th><th>JPEG-Qualität</th></tr>
            <tr><td>1 (Niedrig / Dateigröße groß)</td><td>100</td></tr>
            <tr><td>2</td><td>85</td></tr>
            <tr><td>3 (Normal)</td><td>70</td></tr>
            <tr><td>4</td><td>50</td></tr>
            <tr><td>5 (Hoch / Dateigröße klein)</td><td>30</td></tr>
        </table>
        <ul>
            <li>Je größer der Wert, desto höher die Komprimierungsrate und desto kleiner die Dateigröße</li>
            <li>Eine höhere Komprimierungsrate führt zu einer Verschlechterung der Bildqualität</li>
        </ul>

        <div class='see-also'>Siehe auch:
            <ul>
                <li><a href='#scanmode-tab'>[Scanmodus] Registerkarte</a></li>
            </ul>
        </div>
    "));

    // ═══════════════════════════════════════════════════════════════════════
    // Dialogfelder
    // ═══════════════════════════════════════════════════════════════════════

    private static HelpTopic TopicScanModeOptionsDialog() => new("scanmode-options", "[Scanmodus Option] Dialogfeld",
        Wrap("[Scanmodus Option] Dialogfeld", @"
        <p>In diesem Dialogfeld können Sie die Einstellungen der Scanmodus-Optionen festlegen.</p>

        <h2>Elemente</h2>
        <p><b>[Helligkeit (nur Schwarzweiß-Scan)] Regler</b><br>
        Sieben Stufen von „Dunkler -3“ bis „Heller +3“, Mitte = Normal (Grundeinstellung).
        Der Regler ist nur verfügbar, wenn [Schwarzweiß] als Farbmodus gewählt wurde.</p>

        <p><b>[Einstellung nur für Textdokumente] Kontrollkästchen</b><br>
        Betont den Kontrast der gescannten Bilder (bei Schwarzweiß-Scans und bei der Umwandlung in
        Schwarzweiß). Wählen Sie dieses für Dokumente mit Text oder handschriftlichen Texten.</p>

        <p><b>[Automatisches Löschen leerer Seiten zulassen] Kontrollkästchen</b><br>
        Leere Seiten werden automatisch erkannt und aus dem Ausgabebild gelöscht (Grundeinstellung: markiert).
        Sind alle Seiten leer, wird keine Datei gespeichert. Bei Scan-Seite „Automatisch“ im Duplex-Modus werden
        leere Rückseiten auch ohne diese Option entfernt.</p>

        <p><b>[Schräglauf automatisch korrigieren] Kontrollkästchen</b><br>
        Richtet schief eingezogene Seiten gerade aus (Grundeinstellung: nicht markiert).</p>

        <p><b>[Automatische Bilddrehung zulassen] Kontrollkästchen</b><br>
        Dokumente, die seitlich oder kopfüber gescannt wurden, werden in der korrekten
        Richtung ausgegeben (Grundeinstellung: markiert). Die Ausrichtung wird mittels Tesseract OSD erkannt;
        bei zu unsicherer Erkennung wird nicht gedreht.</p>

        <p><b>[Dokumente mit der Vorderseite nach oben einlegen] Kontrollkästchen</b><br>
        Wählen Sie dies, wenn der Stapel mit der Vorderseite nach oben eingelegt wird. Die Seitenreihenfolge
        wird dann umgekehrt, sodass die Seiten in Lesereihenfolge gespeichert werden.</p>

        <div class='see-also'>Siehe auch:
            <ul>
                <li><a href='#scanmode-tab'>[Scanmodus] Registerkarte</a></li>
            </ul>
        </div>
    "));

    private static HelpTopic TopicPdfOptionsDialog() => new("pdf-options", "[PDF-Dateiformat Option] Dialogfeld",
        Wrap("[PDF-Dateiformat Option] Dialogfeld", @"
        <p>In diesem Dialogfeld können Sie die PDF-Dateiformate bestimmen.</p>

        <h2>PDF-Seiten teilen</h2>
        <p><b>[Mehrseitige PDF-Datei (gesamter Stapel in einer PDF)]</b><br>
        Alle gescannten Bilder werden in einer PDF-Datei zusammengefasst.</p>

        <p><b>[Seitenzahl, für die jeweils eine neue PDF-Datei erstellt werden soll]</b><br>
        Erstellt nach dem Erreichen der festgelegten Seitenzahl (1 bis 999) jeweils eine neue PDF-Datei.
        Die Dateien erhalten eine laufende Nummer (z. B. <code>Name_001.pdf</code>, <code>Name_002.pdf</code>).</p>

        <h2>Kennwort</h2>
        <p><b>[Kennwort für PDF-Datei einstellen] Kontrollkästchen</b><br>
        Aktiviert den Kennwortschutz für die erstellten PDF-Dateien.</p>

        <p><b>Kennwort / Bestätigen</b><br>
        Geben Sie das gewünschte Kennwort ein und bestätigen Sie es. Es wird für das Öffnen
        des Dokuments verwendet. Das Kennwort wird verschlüsselt (nur für Ihr Windows-Benutzerkonto lesbar)
        in den Einstellungen gespeichert. Kann es nicht gelesen werden (z. B. Einstellungen von einem anderen
        Benutzer oder PC), bleibt der Kennwortschutz eingeschaltet und SpeedScan Manager fragt vor dem nächsten
        Scan nach; geben Sie das Kennwort dann hier neu ein.</p>

        <div class='see-also'>Siehe auch:
            <ul>
                <li><a href='#filetype-tab'>[Dateiart] Registerkarte</a></li>
            </ul>
        </div>
    "));

    private static HelpTopic TopicFileNameFormatDialog() => new("filename-format", "[Dateinamenformat] Dialogfeld",
        Wrap("[Dateinamenformat] Dialogfeld", @"
        <p>In diesem Dialogfeld kann das Dateinamenformat bestimmt werden, in dem die Dateien
        automatisch gespeichert werden. Unten wird ein Beispiel für den Dateinamen angezeigt.</p>

        <h2>Datum und Uhrzeit</h2>
        <p><b>[Benutzen Sie die Einstellung Ihres Betriebssystems]</b><br>
        Der Dateiname besteht aus Datum und Uhrzeit im kurzen Datums- und im Zeitformat von Windows.
        Zeichen, die in Dateinamen nicht erlaubt sind (z. B. <code>:</code> und <code>/</code>), werden durch
        <code>-</code> ersetzt, z. B. <code>12.04.2026 15-30-45.pdf</code> bei deutschem Windows.</p>
        <p><b>[JJJJMMTThhmmss]</b><br>
        Der Dateiname besteht aus Datum und Uhrzeit im Format <code>JahrMonatTagStundeMinuteSekunde</code>,
        z. B. <code>20260412153045.pdf</code> (Grundeinstellung).</p>
        <p>Bei einem bereits vorhandenen Namen wird <code>_1</code>, <code>_2</code> usw. angehängt.</p>

        <h2>Benutzerdefinierter Dateiname</h2>
        <p><b>Dateiname</b> – Bis zu 30 Zeichen für den Anfang des Dateinamens (Grundeinstellung „unbenannt“).
        Nicht erlaubt sind die Zeichen \ / : * ? "" &lt; &gt; |, ein Punkt am Ende sowie reservierte Windows-Namen (z. B. CON, NUL, COM1).</p>
        <p><b>[Zähler] Auswahlliste</b> – Stellenanzahl für die Seriennummer: 3, 4 oder 5 Ziffern (Grundeinstellung 3).</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Der benutzerdefinierte Name wird immer mit Unterstrich und Seriennummer gespeichert. Beispiel mit
            Dateiname „Scan“ und 3 Ziffern: <code>Scan_001.pdf</code>, <code>Scan_002.pdf</code> ... Der Zähler beginnt bei 1
            und setzt sich nach der höchsten bereits vorhandenen Nummer im Ordner fort. Bei mehreren Dateien
            in einem Scan (z. B. ein Bild pro Seite) werden die Dateien fortlaufend nummeriert.</p>
        </div>

        <p><b>[OK]</b> übernimmt die Einstellung und schließt das Dialogfeld, <b>[Übernehmen]</b> übernimmt sie, ohne
        zu schließen, <b>[Abbrechen]</b> verwirft die Änderungen. Ein ungültiger Dateiname wird mit einer Meldung abgewiesen.</p>
    "));

    private static HelpTopic TopicNewProfileDialog() => new("new-profile", "[Neues Profil hinzufügen] Dialogfeld",
        Wrap("[Neues Profil hinzufügen] Dialogfeld", @"
        <p>In diesem Dialogfeld können Namen für neue Profile vergeben werden.
        Insgesamt können bis zu 20 Profile (einschließlich der Standardprofile) vorhanden sein.</p>

        <h2>Elemente</h2>
        <p><b>Profilname</b> – Geben Sie den Namen für das neue Profil ein (höchstens 64 Zeichen).
        Der Name muss eindeutig sein; die Groß-/Kleinschreibung wird nicht unterschieden.</p>
        <p><b>[OK] Taste</b> – Profil anlegen und Dialogfeld schließen.</p>
        <p><b>[Abbrechen] Taste</b> – Dialogfeld schließen, ohne ein Profil anzulegen.</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Über <b>Profil hinzufügen...</b> im Einstellungsdialogfeld enthält das neue Profil die aktuell
            eingestellten Werte. Über <b>Hinzufügen...</b> in der Profilverwaltung erhält es die Grundeinstellungen.</p>
        </div>
    "));

    private static HelpTopic TopicProfileManagementDialog() => new("profile-mgmt-dialog", "[Profilverwaltung] Dialogfeld",
        Wrap("[Profilverwaltung] Dialogfeld", @"
        <p>In diesem Dialogfeld können Sie Profile hinzufügen, umbenennen, löschen sowie die
        Reihenfolge der Auflistung ändern.</p>

        <h2>Elemente</h2>
        <p><b>[Hinzufügen...] Taste</b> – Legt ein neues Profil mit den Grundeinstellungen an (höchstens 20 Profile).</p>

        <p><b>[Umbenennen...] Taste</b> – Ändert die ausgewählte Profilbezeichnung.
        Nicht verfügbar für Standardprofile.</p>

        <p><b>[Löschen] Taste</b> – Löscht das gewählte Profil nach Rückfrage.
        Nicht verfügbar für Standardprofile.</p>

        <p><b>[Nach oben] Taste</b> – Verschiebt das Profil um eine Position nach oben.</p>
        <p><b>[Nach unten] Taste</b> – Verschiebt das Profil um eine Position nach unten.</p>
        <p><b>[Schließen] Taste</b> – Schließt das Dialogfeld.</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Die acht Standardprofile (siehe <a href='#default-settings'>Grundeinstellungen der Profile</a>)
            können nicht umbenannt oder gelöscht, aber verschoben werden. Eigene Profile können frei umbenannt,
            gelöscht und neu angeordnet werden.</p>
        </div>
    "));

    private static HelpTopic TopicAppManageDialog() => new("app-manage", "[Installieren/Deinstallieren] Dialogfeld",
        Wrap("[Installieren/Deinstallieren] Dialogfeld", @"
        <p>In diesem Dialogfeld können Sie eigene Anwendungen hinzufügen, entfernen
        oder ändern, die nach dem Scan gestartet werden. Bis zu zehn Anwendungen können hinzugefügt werden.</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Die hier getroffenen Änderungen an der Anwendungsliste werden sofort gespeichert, unabhängig von
            der [OK] oder [Abbrechen] Taste im Einstellungsdialogfeld.</p>
        </div>

        <h2>Elemente</h2>
        <p><b>Hinzugefügt</b> – Liste der hinzugefügten Anwendungen (Name und Programmpfad).</p>
        <p><b>[Hinzufügen...] Taste</b> – Fügt eine neue Anwendung hinzu. Angegeben werden:</p>
        <ul>
            <li><b>Name</b> – Anzeigename in der [Anwendung] Auswahlliste (eindeutig, bis 64 Zeichen)</li>
            <li><b>Programm</b> – Programmdatei (*.exe), über [Durchsuchen...] auswählbar</li>
            <li><b>Argumente</b> – Optional. Die gescannten Dateien werden danach (in Anführungszeichen) angehängt.</li>
        </ul>
        <p><b>[Entfernen] Taste</b> – Entfernt die ausgewählte Anwendung.</p>
        <p><b>[Ändern...] Taste</b> – Ändert die Einstellungen einer Anwendung.</p>
        <p><b>[Hilfe] Taste</b> – Öffnet diese Hilfe.</p>
        <p><b>[Schließen] Taste</b> – Schließt das Dialogfeld.</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Wird das Programm später nicht gefunden oder lässt es sich nicht starten, erscheint eine Meldung;
            der Scan bleibt gespeichert.</p>
        </div>
    "));

    private static HelpTopic TopicEmailOptionsDialog() => new("email-options", "[Scan to E-Mail-Optionen] Dialogfeld",
        Wrap("[Scan to E-Mail-Optionen] Dialogfeld", @"
        <p>In diesem Dialogfeld können Sie Einstellungen für die Verwendung von Scan to E-Mail
        konfigurieren. Es ist über die [Anwendung] Registerkarte oder das Tray-Menü
        (<b>E-Mail-Einstellungen...</b>) erreichbar.</p>

        <h2>Elemente</h2>
        <p><b>Standard-Empfänger</b> – E-Mail-Adresse des Standardempfängers.
        Wird beim Erstellen einer E-Mail automatisch eingetragen.</p>

        <p><b>Betreff-Vorlage</b> – Betreff der E-Mail.
        Standard: „Gescanntes Dokument“.</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Es wird das E-Mail-Programm verwendet, das in Windows als MAPI-Standard festgelegt ist.
            Die gespeicherten Dateien werden als Anhang eingefügt. Kann das E-Mail-Programm nicht geöffnet werden,
            erscheint eine Meldung und der Ordner mit der gespeicherten Datei wird geöffnet.</p>
        </div>
    "));

    private static HelpTopic TopicPrintOptionsDialog() => new("print-options", "[Scan to Print-Optionen] Dialogfeld",
        Wrap("[Scan to Print-Optionen] Dialogfeld", @"
        <p>In diesem Dialogfeld können die Optionen der Funktion Scan to Print eingestellt werden.
        Es ist über die [Anwendung] Registerkarte oder das Tray-Menü
        (<b>Druckeinstellungen...</b>) erreichbar.</p>

        <h2>Elemente</h2>
        <p><b>Zieldrucker</b> – Auswahlliste aller im Betriebssystem erkannten Drucker.
        Der Eintrag <b>(Standarddrucker)</b> verwendet jeweils den aktuellen Windows-Standarddrucker.</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Die Seiten werden in Originalgröße gedruckt; Querformat wird bei Bedarf automatisch gewählt.
            Ist der eingestellte Drucker nicht mehr vorhanden, werden Sie gefragt, ob auf dem Standarddrucker
            gedruckt werden soll. Ist kein Drucker installiert, wird der Scan nur gespeichert.</p>
        </div>
    "));

    private static HelpTopic TopicCarrierSheetDialog() => new("carrier-sheet-dialog", "[Trägerblatteinstellungen] Dialogfeld",
        Wrap("[Trägerblatteinstellungen] Dialogfeld", @"
        <p>In diesem Dialogfeld können Sie Einstellungen für das Scannen mit dem Trägerblatt
        vornehmen. Das Dialogfeld enthält außerdem eine kurze Anleitung (Dokument falten,
        in das Trägerblatt einlegen, in den Scanner führen).</p>

        <h2>Elemente</h2>
        <p><b>[Trägerblatt-Modus aktivieren] Kontrollkästchen</b><br>
        Schaltet die Trägerblatt-Verarbeitung ein. Ohne Markierung sind die übrigen Elemente deaktiviert.</p>

        <p><b>[Modus] Auswahlliste</b><br>
        Es gibt den Modus <b>Zwei Seiten in einem Bild erstellen</b>: Vorder- und Rückseite (Duplex-Scan)
        werden nebeneinander in einem Bild zusammengefasst. Weitere Modi stehen nicht zur Verfügung.</p>

        <p><b>[Ausgabebildgröße] Auswahlliste</b></p>
        <ul>
            <li>Automatische Erkennung</li>
            <li>A3, A4, A5, A6</li>
            <li>B4(JIS), B5(JIS), B6(JIS)</li>
            <li>Letter, Double Letter</li>
            <li>Postkarte, Visitenkarte</li>
            <li>Trägerblattgröße (220 × 297 mm)</li>
            <li>Benutzerdefiniert...</li>
        </ul>
        <p>Bei <b>Benutzerdefiniert...</b> geben Sie Breite und Höhe in mm ein (50 bis 2000 mm).
        Es kann eine benutzerdefinierte Ausgabegröße gespeichert werden.</p>

        <div class='warning'>
            <div class='warning-title'>ACHTUNG</div>
            <p>Nur bestimmte Scannermodelle unterstützen das Scannen mit dem Trägerblatt.</p>
        </div>
    "));

    private static HelpTopic TopicCustomSizeDialog() => new("custom-size-dialog", "[Hinzufügen oder Entfernen von benutzerdefinierten Größen] Dialogfeld",
        Wrap("[Hinzufügen oder Entfernen von benutzerdefinierten Größen] Dialogfeld", @"
        <p>In diesem Dialogfeld können Einstellungen von benutzerdefinierten Dokumentengrößen
        hinzugefügt, gelöscht oder geändert werden. Bis zu zehn benutzerdefinierte
        Dokumentengrößen können hinzugefügt werden. Sie erscheinen in der [Papiergröße] Auswahlliste.</p>

        <h2>Elemente</h2>
        <p><b>Benutzerdefinierte Größen</b> – Liste der benutzerdefinierten Größen; darunter werden Breite und Länge der gewählten Größe angezeigt.</p>
        <p><b>[Hinzufügen...] Taste</b> – Öffnet das Dialogfeld für eine zusätzliche benutzerdefinierte Größe.</p>
        <p><b>[Entfernen] Taste</b> – Entfernt die gewählte Größe.</p>
        <p><b>[Ändern...] Taste</b> – Ändert die Einstellungen der gewählten Größe.</p>
        <p><b>[Hilfe] Taste</b> – Öffnet diese Hilfe.</p>
        <p><b>[Schließen] Taste</b> – Schließt das Dialogfeld.</p>

        <h2>Benutzerdefinierte Größe</h2>
        <p><b>Name</b> – Name für die Papiergröße (bis zu 62 Zeichen).</p>
        <p><b>Breite (mm) / Länge (mm)</b> – Abmessungen in Millimetern (1 bis 9999).</p>
    "));

    private static HelpTopic TopicVersionInfoDialog() => new("version-info", "[Versionsinformationen] Dialogfeld",
        Wrap("[Versionsinformationen] Dialogfeld", @"
        <p>In diesem Dialogfeld werden Versionsinformationen zu SpeedScan Manager und den
        verwendeten Komponenten angezeigt. Es wird über <b>Hilfe &gt; Versionsinformationen</b> im Tray-Menü geöffnet.</p>

        <h2>Elemente</h2>
        <p><b>Logo und Versionsnummer</b> – Zeigt das SpeedScan Manager Logo und die aktuelle
        Version an.</p>
        <p><b>Lizenzinformationen</b> – Zeigt Informationen zur GPL-3.0-Lizenz und die
        Copyright-Hinweise an.</p>
        <p><b>Verwendete Komponenten</b> – Nennt die verwendeten Bibliotheken:</p>
        <ul>
            <li>NTwain (TWAIN-Unterstützung)</li>
            <li>Tesseract.NET (OCR-Engine)</li>
            <li>PdfSharpCore (PDF-Verarbeitung)</li>
        </ul>
        <p><b>GitHub- und Discord-Symbole</b> – Öffnen die Projektseite bzw. den Discord-Server im Browser.</p>
        <p><b>[Detail...] Taste</b> – Öffnet das Dialogfeld für <a href='#driver-info'>Scanner- und Treiberinformationen</a>.</p>
        <p><b>[OK] Taste</b> – Schließt das Dialogfeld.</p>
        <p><b>[Hilfe] Taste</b> – Öffnet diese Hilfe.</p>
    "));

    private static HelpTopic TopicPostScanMediaDialog() => new("postscan-media", "[Quick-Menü] Dialogfeld",
        Wrap("[Quick-Menü] Dialogfeld", @"
        <p>Nach dem Scannen wird dieses Dialogfeld angezeigt, wenn das Quick-Menü aktiviert ist.
        Es nennt die Seitenzahl und fragt, wohin gescannt werden soll. Die Datei ist zu diesem Zeitpunkt
        bereits im Speicherordner abgelegt.</p>

        <h2>Verfügbare Aktionen</h2>
        <ul>
            <li><b>In Ordner speichern</b> (Scan to Folder) – Öffnet das Dialogfeld zum Festlegen von Titel und Zielordner.</li>
            <li><b>Per E-Mail versenden</b> (Scan to E-Mail) – Öffnet das E-Mail-Programm mit der Datei als Anhang.</li>
            <li><b>Drucken</b> (Scan to Print) – Druckt die gescannten Seiten.</li>
            <li><b>Nach Word</b> (Scan to Word) – Erstellt ein Word-Dokument (.docx) mit den gescannten Bildern.</li>
            <li><b>Nach Excel</b> (Scan to Excel) – Erstellt eine Excel-Datei (.xlsx) mit den gescannten Bildern.</li>
            <li><b>Nach PowerPoint</b> (Scan to PowerPoint) – Erstellt eine PowerPoint-Präsentation (.pptx) mit den gescannten Bildern.</li>
            <li><b>In Bilderordner</b> (Scan Picture Folder) – Speichert die Seiten als JPEG-Dateien in <code>Bilder\SpeedScanManager</code>.</li>
            <li><b>Mit PDF Edit bearbeiten</b> (Edit with PDF) – Öffnet die gescannte Datei mit dem zugeordneten Programm.</li>
        </ul>

        <h2>Bedienung</h2>
        <p>Klicken Sie auf eine Aktion, um sie auszuwählen. Doppelklicken Sie, um die Aktion
        direkt auszuführen. Alternativ wählen Sie eine Aktion aus und klicken Sie auf
        <b>[Speichern]</b>.</p>

        <p><b>[Speichern] Taste</b> – Führt die ausgewählte Aktion aus.</p>
        <p><b>[Abbrechen] Taste</b> und <b>Schließen-Symbol</b> – Schließen das Dialogfeld. Die bereits gespeicherte
        Datei bleibt erhalten (Sprechblase „Scan gespeichert“).</p>
        <p><b>Hilfe-Symbol</b> – Öffnet diese Hilfe.</p>

        <div class='see-also'>Siehe auch:
            <ul>
                <li><a href='#scan-quick'>Scannen mit dem Quick-Menü</a></li>
                <li><a href='#postscan-save'>[In Ordner speichern] Dialogfeld</a></li>
            </ul>
        </div>
    "));

    private static HelpTopic TopicPostScanSaveDialog() => new("postscan-save", "[In Ordner speichern] Dialogfeld",
        Wrap("[In Ordner speichern] Dialogfeld", @"
        <p>In diesem Dialogfeld können die Bilder der gescannten Dokumente eingesehen und für
        das Speichern Dateiname und Zielordner bestimmt werden. Die Datei wurde zuvor bereits im Speicherordner
        abgelegt; das Dialogfeld benennt sie um bzw. verschiebt sie.</p>

        <h2>Elemente</h2>
        <p><b>Vorschau</b> – Zeigt das gescannte Dokumentenbild an. Bei mehreren Seiten kann
        durch die Seiten navigiert werden.</p>
        <p><b>Seitennavigation</b> – Zeigt die aktuelle Seitenzahl und ermöglicht das
        Durchblättern der gescannten Seiten mit den Pfeilschaltflächen.</p>
        <p><b>Titel festlegen</b> – Zeigt den vorgeschlagenen Dateinamen an. Kann geändert werden.
        Ungültige Zeichen werden ersetzt.</p>
        <p><b>Zielordner festlegen</b> – Zeigt den Zielordner an. Er wird über
        [Durchsuchen...] geändert.</p>
        <p><b>Scaneinstellungen</b> – Zeigt eine Übersicht der verwendeten Einstellungen (Auflösung, Farbmodus,
        Scan-Seite, Dateiformat, Kompression).</p>
        <p><b>[Speichern] Taste</b> – Übernimmt Titel und Zielordner. Bei mehreren Dateien wird eine Seriennummer
        angefügt. Bestehende Dateien werden nie überschrieben (es wird <code>_1</code> usw. angehängt).</p>
        <p><b>[Abbrechen] Taste</b> – Fragt „Scan verwerfen?“. Bei <b>Ja</b> werden die bereits erstellten Dateien gelöscht,
        bei <b>Nein</b> kehren Sie zum Dialogfeld zurück.</p>
    "));

    private static HelpTopic TopicScannerDriverInfoDialog() => new("driver-info", "[Scanner- und Treiberinformationen] Dialogfeld",
        Wrap("[Scanner- und Treiberinformationen] Dialogfeld", @"
        <p>In diesem Dialogfeld werden detaillierte Informationen zum angeschlossenen Scanner
        und den verwendeten Programmkomponenten angezeigt.</p>

        <h2>Elemente</h2>
        <p><b>Scanner-Informationen</b> – Zeigt Scannername, Hersteller, Produktfamilie und
        Treiberversion des angeschlossenen Scanners an. Ist kein Scanner verbunden, wird „Kein Scanner verbunden“ angezeigt.</p>
        <p><b>Dateiversion</b> – Listet Version und Datum der Programmdatei sowie der Bibliotheken auf:</p>
        <ul>
            <li>NTwain (TWAIN-Bibliothek)</li>
            <li>Tesseract (OCR-Engine)</li>
            <li>PdfSharpCore (PDF-Verarbeitung)</li>
            <li>DocumentFormat.OpenXml (Word-, Excel- und PowerPoint-Dateien)</li>
        </ul>
        <p><b>[OK] Taste</b> – Schließt das Dialogfeld.</p>
        <p><b>[Hilfe] Taste</b> – Öffnet diese Hilfe.</p>
    "));

    // ═══════════════════════════════════════════════════════════════════════
    // Profile
    // ═══════════════════════════════════════════════════════════════════════

    private static HelpTopic TopicProfileManagement() => new("profile-mgmt", "Profile verwalten",
        Wrap("Profile verwalten (Hinzufügen/Umbenennen/Löschen)", @"
        <p>Um Einstellungen ohne das Quick-Menü festzulegen, können Sie ein Profil aus der
        [Profil] Auswahlliste im Einstellungsdialogfeld auswählen. Ein Profil speichert alle Einstellungen
        aller Registerkarten (Anwendung, Speichern, Scanmodus, Dateiart, Papier, Dateigröße).</p>

        <p>Bis zu 20 Profile, einschließlich der bereits in der [Profil] Auswahlliste
        enthaltenen, können hinzugefügt werden.</p>

        <h2>Neues Profil hinzufügen</h2>
        <ol>
            <li>Stellen Sie die gewünschten Werte ein.</li>
            <li>Wählen Sie [Profil hinzufügen...] in der [Profil] Auswahlliste.</li>
            <li>Vergeben Sie im <a href='#new-profile'>[Neues Profil hinzufügen] Dialogfeld</a> einen Namen.</li>
        </ol>

        <h2>Profil ändern</h2>
        <p>Ändern Sie die Einstellungen bei ausgewähltem eigenem Profil und übernehmen Sie sie mit [OK] oder
        [Übernehmen]; sie werden im Profil gespeichert. Standardprofile bleiben unverändert (es wird nachgefragt,
        ob ein neues Profil angelegt werden soll).</p>

        <h2>Profile umbenennen, löschen oder sortieren</h2>
        <p>Öffnen Sie das <a href='#profile-mgmt-dialog'>[Profilverwaltung] Dialogfeld</a> über:</p>
        <ul>
            <li>[Profilverwaltung...] in der [Profil] Auswahlliste des Einstellungsdialogfelds</li>
            <li>[Profilverwaltung...] im Tray-Menü</li>
        </ul>

        <div class='see-also'>Siehe auch:
            <ul>
                <li><a href='#new-profile'>[Neues Profil hinzufügen] Dialogfeld</a></li>
                <li><a href='#profile-mgmt-dialog'>[Profilverwaltung] Dialogfeld</a></li>
            </ul>
        </div>
    "));

    private static HelpTopic TopicDefaultSettings() => new("default-settings", "Grundeinstellungen der Profile",
        Wrap("Grundeinstellungen der Profile", @"
        <h2>Grundeinstellungen</h2>
        <p>Diese Werte gelten beim ersten Start, für alle Standardprofile und für neu über die Profilverwaltung angelegte Profile:</p>
        <table>
            <tr><th>Einstellung</th><th>Grundeinstellung</th></tr>
            <tr><td>Quick-Menü verwenden</td><td>Aktiviert</td></tr>
            <tr><td>Beim Anmelden starten</td><td>Aktiviert</td></tr>
            <tr><td>Anwendung</td><td>Scan to Folder</td></tr>
            <tr><td>Speicherordner</td><td>Dokumente\SpeedScanManager</td></tr>
            <tr><td>Dateinamenformat</td><td>Datum und Uhrzeit (JJJJMMTThhmmss)</td></tr>
            <tr><td>Benutzerdefinierter Dateiname</td><td>unbenannt, 3 Ziffern</td></tr>
            <tr><td>Datei nach Scan umbenennen</td><td>Nicht markiert</td></tr>
            <tr><td>Bildqualität</td><td>Automatisch (200 dpi, Schwarzweiß 400 dpi)</td></tr>
            <tr><td>Farbmodus</td><td>Farbe</td></tr>
            <tr><td>Scan-Seite</td><td>Automatisch</td></tr>
            <tr><td>Scanvorgang fortsetzen</td><td>Nicht markiert</td></tr>
            <tr><td>Helligkeit</td><td>Normal</td></tr>
            <tr><td>Automatisches Löschen leerer Seiten</td><td>Aktiviert</td></tr>
            <tr><td>Schräglauf automatisch korrigieren</td><td>Deaktiviert</td></tr>
            <tr><td>Automatische Bilddrehung</td><td>Aktiviert</td></tr>
            <tr><td>Dateiformat</td><td>PDF (*.pdf), mehrseitig, ohne Kennwort</td></tr>
            <tr><td>Texterkennung (OCR)</td><td>Deaktiviert (Sprache: Automatische Erkennung, Zielseiten: Alle Seiten)</td></tr>
            <tr><td>Papiergröße</td><td>Automatische Erkennung</td></tr>
            <tr><td>Mehrfacheinzugserkennung</td><td>Aus</td></tr>
            <tr><td>Trägerblatt-Modus</td><td>Deaktiviert</td></tr>
            <tr><td>Komprimierungsrate</td><td>Stufe 3 von 5</td></tr>
            <tr><td>E-Mail-Betreff</td><td>Gescanntes Dokument</td></tr>
            <tr><td>Zieldrucker</td><td>(Standarddrucker)</td></tr>
        </table>

        <h2>Standardprofile</h2>
        <p>Beim ersten Start werden acht Standardprofile erstellt, je eines pro Anwendung. Sie enthalten die
        oben genannten Grundeinstellungen und unterscheiden sich nur in der Anwendung:</p>
        <table>
            <tr><th>Profilname</th><th>Anwendung</th></tr>
            <tr><td>In Ordner speichern</td><td>Scan to Folder</td></tr>
            <tr><td>Per E-Mail senden</td><td>Scan to E-Mail</td></tr>
            <tr><td>Drucken</td><td>Scan to Print</td></tr>
            <tr><td>In Word-Dokument umwandeln</td><td>Scan to Word</td></tr>
            <tr><td>In Excel-Tabelle umwandeln</td><td>Scan to Excel</td></tr>
            <tr><td>In PowerPoint-Präsentation umwandeln</td><td>Scan to PowerPoint</td></tr>
            <tr><td>In Bilderordner speichern</td><td>Scan Picture Folder</td></tr>
            <tr><td>Mit PDF-Programm bearbeiten</td><td>Edit with PDF</td></tr>
        </table>
        <p>Standardprofile können nicht umbenannt oder gelöscht werden, lassen sich aber in der Reihenfolge
        verschieben. Insgesamt sind höchstens 20 Profile möglich.</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Die Bildqualität [Hohe Bildqualität], [Kleine Datei] und [Empfohlen] sind keine Profile, sondern
            Voreinstellungen für Bildqualität und Komprimierungsrate im Einstellungsdialogfeld.</p>
        </div>
    "));

    // ═══════════════════════════════════════════════════════════════════════
    // Fehlerbehebung
    // ═══════════════════════════════════════════════════════════════════════

    private static HelpTopic TopicMultiFeedDialog() => new("multifeed-dialog", "Mehrfacheinzugserkennungsdialogfeld",
        Wrap("Mehrfacheinzugserkennungsdialogfeld", @"
        <p>Wenn während des Scannens ein Mehrfacheinzug erkannt wurde, erscheint dieses Dialogfeld
        („Mehrfacheinzug erkannt!“). Der Scanner hält an, bis Sie entschieden haben.</p>

        <h2>Elemente</h2>
        <p><b>Vorschaubilder</b> – Zeigt die vorherige Seite und die aktuelle Seite (mit Mehrfacheinzug)
        zur Beurteilung.</p>

        <p><b>[Wiederholen] Taste</b> – Verwirft die in diesem Durchgang gescannten Bilder und bricht den Scan ab
        (Sprechblase „Scan verworfen“). Legen Sie das Dokument erneut ein und starten Sie einen neuen Scan.
        Dieselbe Wirkung hat das Schließen des Dialogfelds.</p>

        <p><b>[So übernehmen] Taste</b> – Behält die Bilder mit Mehrfacheinzug.
        Der Scanvorgang wird fortgesetzt.</p>

        <p><b>[Erkennung ausschalten] Taste</b> – Ignoriert weitere Mehrfacheinzug-Meldungen
        für den restlichen Scan und übernimmt die aktuelle Seite.</p>

        <div class='warning'>
            <div class='warning-title'>ACHTUNG</div>
            <ul>
                <li>Umschläge oder Dokumente mit aufgeklebten Zetteln/Fotos/Briefmarken können vom Scanner
                als Mehrfacheinzug erkannt werden.</li>
                <li>Welche Erkennungsarten möglich sind, hängt vom Scannermodell ab.</li>
            </ul>
        </div>

        <div class='see-also'>Siehe auch:
            <ul>
                <li><a href='#multifeed-measures'>Maßnahmen beim Auftreten eines Mehrfacheinzugs</a></li>
            </ul>
        </div>
    "));

    private static HelpTopic TopicMultiFeedMeasures() => new("multifeed-measures", "Maßnahmen beim Auftreten eines Mehrfacheinzugs",
        Wrap("Maßnahmen beim Auftreten eines Mehrfacheinzugs", @"
        <p>Wenn der Scanner einen Mehrfacheinzug erkannt hat, wird der Scanvorgang unterbrochen.</p>

        <h2>Dokumente wurden in der Reihenfolge des Originaldokuments gescannt</h2>
        <ol>
            <li>Wählen Sie [So übernehmen] und der Scan wird fortgesetzt.</li>
        </ol>
        <p>Für Umschläge oder Dokumente mit Briefmarken/Klebenotizen/Fotos: Wählen Sie [So übernehmen]
        um die Seite zu behalten und den Scan fortzusetzen, oder [Erkennung ausschalten], wenn weitere solche
        Seiten folgen.</p>

        <h2>Dokumente wurden nicht in der Reihenfolge des Originaldokuments gescannt</h2>
        <ol>
            <li>Wählen Sie [Wiederholen]. Die Seiten dieses Durchgangs werden verworfen.</li>
            <li>Öffnen Sie den ADF, entnehmen Sie alle Dokumente (auch aus dem Ausgabefach) und richten Sie die Blattkanten neu aus.</li>
            <li>Legen Sie den gesamten Stapel erneut ein und starten Sie einen neuen Scan.</li>
        </ol>

        <div class='see-also'>Siehe auch:
            <ul>
                <li><a href='#multifeed-dialog'>Mehrfacheinzugserkennungsdialogfeld</a></li>
            </ul>
        </div>
    "));

    private static HelpTopic TopicMessageList() => new("message-list", "Liste der Meldungen",
        Wrap("Liste der Meldungen", GetMessageListHtml()));

    private static string GetMessageListHtml()
    {
        var windows = new[]
        {
            "Ein Scanvorgang läuft bereits. (Scan oder Scannerauswahl wurde während eines Scans gewählt.)",
            "TWAIN ist nicht initialisiert. Es ist kein Scanner verfügbar. (Der TWAIN-Quellmanager konnte nicht gestartet werden; prüfen Sie die Treiberinstallation.)",
            "Der TWAIN-Quellmanager konnte nicht geöffnet werden.",
            "Es wurde kein Scanner gefunden.",
            "Der Scanner konnte nicht geöffnet werden. Ist er eingeschaltet und verbunden?",
            "Der Scanvorgang konnte nicht gestartet werden.",
            "Der Scanner hat nicht reagiert. Es wurden keine Seiten gescannt.",
            "Es wurden keine Seiten gescannt. Bitte legen Sie das Dokument in den Scanner ein und versuchen Sie es erneut.",
            "Alle gescannten Seiten wurden als leer erkannt und entfernt. Es wurde keine Datei gespeichert.",
            "Es wurde keine Datei erstellt.",
            "Der Scan konnte nicht gespeichert werden: ...",
            "Der Scan konnte nicht im Zielordner gespeichert werden ... Er wurde stattdessen hier gespeichert: Dokumente\\SpeedScanManager",
            "Der Zielordner konnte nicht erstellt werden ... Der Scan bleibt hier gespeichert: ...",
            "Folgende Dateien konnten nicht nach ... verschoben werden und bleiben in ...",
            "Scan verwerfen? Ja: Der Scan wird gelöscht. Nein: Zurück zum Dialog.",
            "Das E-Mail-Programm konnte nicht mit der Datei als Anhang geöffnet werden. (Der Ordner mit der Datei wird geöffnet, damit Sie sie manuell anhängen können.)",
            "Es gibt keine Seiten zum Drucken.",
            "Es ist kein Drucker installiert. Der Scan wurde gespeichert, aber nicht gedruckt.",
            "Der Drucker \"...\" wurde nicht gefunden. Auf dem Standarddrucker drucken?",
            "Das Drucken ist fehlgeschlagen: ...",
            "Die Word-/Excel-/PowerPoint-Datei konnte nicht erstellt werden: ...",
            "Die Datei wurde erstellt, konnte aber nicht geöffnet werden (ist Word/Excel/PowerPoint installiert?)",
            "Die Bilder konnten nicht im Bilderordner gespeichert werden: ...",
            "Die Datei konnte nicht geöffnet werden: ...",
            "Das Programm \"...\" wurde nicht gefunden bzw. konnte nicht gestartet werden. (Eigene Anwendung; der Scan bleibt gespeichert.)",
            "Der Ordner \"...\" existiert nicht. (Scan-Ergebnis anzeigen)",
            "Es läuft gerade ein Scanvorgang. Scan abbrechen und SpeedScan Manager beenden?",
            "Der letzte Scan ist noch nicht abgeschlossen. SpeedScan Manager trotzdem beenden?",
            "Das PDF-Passwort konnte nicht gelesen werden, bitte neu eingeben (Einstellungen > Dateiart > Option > Kennwort).",
            "Die Einrichtung der Scan-Taste wurde abgebrochen. / Die Scan-Taste konnte nicht eingerichtet werden.",
            "Es können höchstens 20 Profile angelegt werden.",
            "Das Standardprofil \"...\" kann nicht geändert werden. Möchten Sie die geänderten Einstellungen als neues Profil speichern?",
            "In SpeedScan Manager ist ein unerwarteter Fehler aufgetreten. Das Programm läuft weiter. (Details stehen in %LocalAppData%\\SpeedScanManager\\crash.log.)",
        };

        var balloons = new[]
        {
            "Der Scanner ist nicht angeschlossen oder ausgeschaltet.",
            "TWAIN konnte nicht initialisiert werden. Es kann nicht gescannt werden.",
            "Scan abgebrochen: Der Scanvorgang wurde abgebrochen.",
            "Scan verworfen: Mehrfacheinzug: Die Seiten wurden verworfen. Bitte legen Sie das Dokument erneut ein.",
            "Scan verworfen: Der Scan wurde gelöscht.",
            "Scan gespeichert: Dateiname und Ordner bzw. Anzahl der Dateien.",
            "Drucken: Das Dokument wurde an den Drucker gesendet.",
            "Der Scan wird noch gespeichert. SpeedScan Manager wird danach beendet.",
            "Die Scan-Taste ist eingerichtet. Ein Druck auf die Taste startet einen Scan.",
            "Hinweis zum Scan: Der Scanner hat nicht mehr reagiert. Der Scanvorgang wurde abgebrochen.",
            "Hinweis zum Scan: Eine oder mehrere Seiten konnten nicht übertragen werden.",
            "Hinweis zum Scan: Der Scanner unterstützt den Farbmodus nicht / kein Flachbett (der Einzug wird verwendet) / keinen Duplex-Scan (es wurde einseitig gescannt).",
            "Hinweis zum Scan: Die Auflösung wird nicht unterstützt, es wurde eine andere verwendet.",
            "Hinweis zum Scan: Die Mehrfacheinzugserkennung konnte nicht (bzw. nur mit Ultraschall) aktiviert werden.",
            "Hinweis zum Scan: Die gewählte Papiergröße wird vom Scanner nicht unterstützt / keine gültige benutzerdefinierte Papiergröße ausgewählt.",
            "Hinweis: OCR-Sprachdaten fehlen; es wird eine andere Sprache verwendet bzw. die Texterkennung wurde übersprungen.",
            "Hinweis: Die Texterkennung ist nicht verfügbar (tessdata fehlt) oder fehlgeschlagen.",
            "Hinweis: Die automatische Bilddrehung ist nicht verfügbar (osd.traineddata fehlt).",
            "Hinweis: Markierte Schlüsselwörter benötigen die Texterkennung (durchsuchbare PDF); sie wurden übersprungen.",
            "Hinweis: Zu wenig Arbeitsspeicher für die Schräglagenkorrektur; sie wurde übersprungen.",
        };

        var sb = new StringBuilder();
        sb.AppendLine("<p>Folgende Meldungen können während der Verwendung von SpeedScan Manager erscheinen. Auslassungspunkte (...) stehen für Namen, Pfade oder Fehlertexte.</p>");

        sb.AppendLine("<h2>Meldungsfenster</h2>");
        sb.AppendLine("<ul>");
        foreach (var msg in windows)
            sb.AppendLine($"  <li>{System.Net.WebUtility.HtmlEncode(msg)}</li>");
        sb.AppendLine("</ul>");

        sb.AppendLine("<h2>Sprechblasen am Tray-Symbol</h2>");
        sb.AppendLine("<ul>");
        foreach (var msg in balloons)
            sb.AppendLine($"  <li>{System.Net.WebUtility.HtmlEncode(msg)}</li>");
        sb.AppendLine("</ul>");
        return sb.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Anhang
    // ═══════════════════════════════════════════════════════════════════════

    private static HelpTopic TopicAbbreviations() => new("abbreviations", "In dieser Hilfe verwendete Abkürzungen",
        Wrap("In dieser Hilfe verwendete Abkürzungen", @"
        <table>
            <tr><th>Abkürzung</th><th>Bedeutung</th></tr>
            <tr><td>ADF</td><td>Automatic Document Feeder (Automatischer Dokumenteneinzug)</td></tr>
            <tr><td>OCR</td><td>Optical Character Recognition (Optische Zeichenerkennung)</td></tr>
            <tr><td>OSD</td><td>Orientation and Script Detection (Ausrichtungs- und Schrifterkennung)</td></tr>
            <tr><td>PDF</td><td>Portable Document Format</td></tr>
            <tr><td>JPEG</td><td>Joint Photographic Experts Group (Bildformat)</td></tr>
            <tr><td>PNG</td><td>Portable Network Graphics (Bildformat)</td></tr>
            <tr><td>dpi</td><td>dots per inch (Punkte pro Zoll)</td></tr>
            <tr><td>USB</td><td>Universal Serial Bus</td></tr>
            <tr><td>WIA / STI</td><td>Windows Image Acquisition / Still Image (Windows-Schnittstellen für Bilderfassungsgeräte, u. a. für die Scan-Taste)</td></tr>
            <tr><td>TWAIN</td><td>Standard-Protokoll für die Kommunikation zwischen Scanner und Software</td></tr>
            <tr><td>MAPI</td><td>Messaging Application Programming Interface (E-Mail-Schnittstelle)</td></tr>
            <tr><td>UAC</td><td>User Account Control (Windows-Benutzerkontensteuerung)</td></tr>
            <tr><td>S&amp;W</td><td>Schwarz und Weiß</td></tr>
        </table>
    "));

    /// <summary>
    /// Flattens the topic tree into a dictionary for quick ID lookup.
    /// </summary>
    public static Dictionary<string, HelpTopic> BuildTopicMap(HelpTopic root)
    {
        var map = new Dictionary<string, HelpTopic>();
        void Walk(HelpTopic t)
        {
            map[t.Id] = t;
            foreach (var c in t.Children)
                Walk(c);
        }
        Walk(root);
        return map;
    }
}
