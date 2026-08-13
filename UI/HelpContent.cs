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
                TopicScanDisplay(),
                TopicScanCarrierSheet()
            ),
            new HelpTopic("settings", "Einstellungsdialogfeld",
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
            <li><b>Quick-Menü</b> – Einfaches Scannen mit voreingestellten Profilen (Empfohlen, Kleine Datei, Hohe Bildqualität, Benutzerdefiniert)</li>
            <li><b>Profile</b> – Bis zu 20 Profile mit individuellen Einstellungen können verwaltet werden</li>
            <li><b>Scan-Taste am Scanner</b> – Scannen durch Drücken der Hardware-Taste am Scanner</li>
            <li><b>Scan to Folder</b> – Gescannte Bilder in einem Ordner speichern</li>
            <li><b>Scan to E-Mail</b> – Gescannte Bilder als Anhang einer E-Mail versenden</li>
            <li><b>Scan to Print</b> – Gescannte Bilder direkt drucken</li>
            <li><b>Scan to Word / Excel / PowerPoint</b> – Gescannte Bilder in Office-Dokumente einfügen</li>
            <li><b>Scan Picture Folder</b> – Bilder im Bildordner speichern</li>
            <li><b>Edit with PDF</b> – PDF-Datei zur Bearbeitung öffnen</li>
            <li><b>OCR-Texterkennung</b> – Durchsuchbare PDF-Dateien erstellen</li>
            <li><b>Trägerblatt-Unterstützung</b> – Scannen von großen oder empfindlichen Dokumenten</li>
            <li><b>Automatische Bilddrehung</b> – Korrektur der Ausrichtung mittels Tesseract OSD</li>
            <li><b>Leere Seitenerkennung</b> – Automatisches Entfernen leerer Seiten</li>
        </ul>

        <h2>Unterstützte Dateiformate</h2>
        <ul>
            <li>PDF (*.pdf) – mit Kennwortschutz und OCR-Optionen</li>
            <li>JPEG (*.jpg) – nur bei Farbe- oder Grau-Modus</li>
            <li>PNG (*.png) – nur bei Farbe- oder Grau-Modus</li>
        </ul>
    "));

    private static HelpTopic TopicSystemRequirements() => new("sysreq", "Systemanforderungen",
        Wrap("Systemanforderungen", @"
        <h2>Betriebssystem</h2>
        <ul>
            <li>Windows 10 (64-bit)</li>
            <li>Windows 11 (64-bit)</li>
        </ul>

        <h2>Hardware</h2>
        <ul>
            <li>Prozessor: 1 GHz oder schneller</li>
            <li>Arbeitsspeicher: mindestens 2 GB (4 GB empfohlen)</li>
            <li>Festplattenspeicher: mindestens 500 MB freier Speicherplatz</li>
            <li>USB-Anschluss (USB 2.0 oder höher) oder Netzwerkverbindung für den Scanner</li>
        </ul>

        <h2>Software</h2>
        <ul>
            <li>.NET 8.0 Desktop Runtime</li>
            <li>E-Mail-Programm (MAPI-kompatibel, für Scan to E-Mail)</li>
            <li>Drucker-Treiber (für Scan to Print)</li>
            <li>TWAIN-Treiber des Scanners</li>
        </ul>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Die OCR-Texterkennung unterstützt zahlreiche Sprachen, darunter Deutsch, Englisch,
            Französisch, Italienisch, Spanisch, Japanisch, Chinesisch, Koreanisch, Russisch,
            Portugiesisch und Arabisch.</p>
        </div>
    "));

    private static HelpTopic TopicStarting() => new("starting", "Starten von SpeedScan Manager",
        Wrap("Starten von SpeedScan Manager", @"
        <p>SpeedScan Manager wird als Tray-Anwendung ausgeführt. Nach dem Start erscheint
        ein Symbol im Benachrichtigungsbereich der Taskleiste.</p>

        <h2>Tray-Symbol</h2>
        <p>Das Tray-Symbol zeigt den aktuellen Status des Scanners an:</p>
        <ul>
            <li><b>Farbig</b> – Scanner ist verbunden und einsatzbereit</li>
            <li><b>Ausgegraut</b> – Scanner ist nicht verbunden oder ausgeschaltet</li>
        </ul>

        <h2>Rechtsklick-Menü</h2>
        <p>Klicken Sie mit der rechten Maustaste auf das Tray-Symbol, um das Kontextmenü zu öffnen:</p>
        <ul>
            <li><b>Scan</b> – Startet einen Scan mit den aktuell konfigurierten Einstellungen</li>
            <li><b>Scanner auswählen...</b> – Öffnet den Dialog zur Scanner-Auswahl</li>
            <li><b>Einstellungen der SCAN Taste...</b> – Öffnet das Einstellungsdialogfeld</li>
            <li><b>Profilverwaltung...</b> – Verwaltung der Scan-Profile</li>
            <li><b>Scan-Ergebnis anzeigen</b> – Zeigt die zuletzt gescannten Dateien an</li>
            <li><b>Hilfe</b> – Öffnet diese Hilfe oder Versionsinformationen</li>
            <li><b>Beenden</b> – Beendet SpeedScan Manager</li>
        </ul>

        <h2>Doppelklick</h2>
        <p>Ein Doppelklick auf das Tray-Symbol öffnet das Einstellungsdialogfeld.</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Beim ersten Start prüft SpeedScan Manager automatisch, ob der WIA-Handler für die
            Scan-Taste registriert ist. Falls nicht, wird eine elevated Installation durchgeführt
            (UAC-Abfrage). Dies ist für die Funktion der Scan-Taste am Scanner erforderlich.</p>
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

        <p>Die Verbindung wird regelmäßig überprüft (alle 3 Sekunden wenn verbunden,
        alle 30 Sekunden wenn getrennt). Beim Trennen wird nach einer Bestätigung
        (2 aufeinanderfolgende fehlgeschlagene Prüfungen) eine Benachrichtigung angezeigt.</p>

        <div class='warning'>
            <div class='warning-title'>ACHTUNG</div>
            <ul>
                <li>Schließen Sie nur einen Scanner gleichzeitig an den Computer an.</li>
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
        verwendet werden soll. Der Dialog ist über das Tray-Kontextmenü unter
        <b>Scanner auswählen...</b> erreichbar.</p>

        <h2>Elemente</h2>
        <p><b>Dropdown-Liste</b> – Zeigt alle verfügbaren TWAIN-Quellen an.
        USB-verbundene Scanner werden oben in der Liste sortiert.
        Der Eintrag <b>Automatisch</b> wählt automatisch den besten verfügbaren Scanner.</p>

        <p><b>Automatische Erkennung</b> – Wenn <b>Automatisch</b> ausgewählt ist, wählt
        SpeedScan Manager den bevorzugten Scanner (gespeicherte Auswahl oder USB-Gerät).</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Der Dialog aktualisiert sich automatisch, wenn neue Scanner erkannt werden.
            Es ist kein manueller Aktualisieren-Knopf erforderlich.</p>
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
        <p>Wenn das Quick-Menü aktiviert ist, können Sie Dokumente mit voreingestellten
        Profilen scannen, ohne detaillierte Einstellungen vornehmen zu müssen.</p>

        <h2>Schritte</h2>
        <ol>
            <li>Legen Sie die Dokumente in den Scanner ein.</li>
            <li>Drücken Sie die Scan-Taste am Scanner oder starten Sie den Scan über das Tray-Kontextmenü.</li>
            <li>Wählen Sie im Quick-Menü die gewünschte Aktion:</li>
        </ol>
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

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Wenn das Quick-Menü aktiviert ist, ist die [Anwendung] Registerkarte im
            Einstellungsdialogfeld deaktiviert. Die Anwendung wird stattdessen nach dem Scannen
            über das Quick-Menü ausgewählt.</p>
        </div>

        <h2>Voreingestellte Profile</h2>
        <ul>
            <li><b>Empfohlen</b> – Automatische Bildqualität, geeignet für Standarddokumente</li>
            <li><b>Kleine Datei</b> – Normale Bildqualität, reduzierte Dateigröße</li>
            <li><b>Hohe Bildqualität</b> – Beste Bildqualität für hochwertige Scans</li>
            <li><b>Benutzerdefiniert</b> – Frei konfigurierbare Einstellungen</li>
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
        ausgeführte Aktion wird durch die <b>[Anwendung]</b> Registerkarte bestimmt.</p>

        <h2>Schritte</h2>
        <ol>
            <li>Wählen Sie ein Profil aus der Profil-Dropdown-Liste im Einstellungsdialogfeld.</li>
            <li>Passen Sie die Einstellungen auf den Registerkarten an (Anwendung, Speichern, Scanmodus, Dateiart, Papier, Dateigröße).</li>
            <li>Schließen Sie das Einstellungsdialogfeld mit <span class='kbd'>OK</span>.</li>
            <li>Legen Sie die Dokumente in den Scanner ein und starten Sie den Scan über das Tray-Menü oder die Scan-Taste.</li>
        </ol>

        <div class='see-also'>Siehe auch:
            <ul>
                <li><a href='#settings'>Einstellungsdialogfeld</a></li>
                <li><a href='#profiles'>Profile verwalten</a></li>
                <li><a href='#app-tab'>[Anwendung] Registerkarte</a></li>
            </ul>
        </div>
    "));

    private static HelpTopic TopicScanButton() => new("scan-button", "Scan-Taste am Scanner",
        Wrap("Scan-Taste am Scanner", @"
        <p>SpeedScan Manager unterstützt die Hardware-Scan-Taste am Scanner. Wenn der Scanner
        verbunden ist und die Scan-Taste gedrückt wird, startet automatisch ein Scanvorgang.</p>

        <h2>Funktionsweise</h2>
        <p>Die Scan-Taste wird über zwei Mechanismen überwacht:</p>
        <ul>
            <li><b>WIA-Ereignisüberwachung</b> – Windows Image Acquisition (WIA) erkennt
            Hardware-Tastendrücke und löst den Scan aus.</li>
            <li><b>TWAIN-Geräteereignisse</b> – Bei unterstützten Scannern werden
            Geräteereignisse direkt über den TWAIN-Treiber verarbeitet.</li>
        </ul>

        <h2>Setup</h2>
        <p>Beim ersten Start prüft SpeedScan Manager automatisch, ob der WIA-Handler registriert ist.
        Falls nicht, wird eine elevated Installation durchgeführt. Dies ist einmalig erforderlich.</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Die Scan-Taste funktioniert nur, wenn der Scanner als verbunden erkannt wurde
            und kein Scanvorgang läuft. Während des Scannens wird die Taste ignoriert.</p>
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
        Mehrfacheinzugserkennung nicht auf <b>Längenprüfung</b> oder <b>Beide</b> steht.</p>

        <p>In diesem Modus (Long Page Mode) gelten folgende Besonderheiten:</p>
        <ul>
            <li>Es wird ein benutzerdefinierter Rahmen von 8,5 × 125 Zoll gesetzt.</li>
            <li>Spezielle PaperStream-IP-Treiberfunktionen werden konfiguriert (Cap 40983/40984/41095).</li>
            <li>Die Mehrfacheinzugserkennung wird deaktiviert.</li>
            <li>Alle TWAIN-Kapabilitäten werden vor dem Scan gesetzt, um den Treiber nicht zu stören.</li>
        </ul>

        <div class='warning'>
            <div class='warning-title'>ACHTUNG</div>
            <ul>
                <li>Die maximale Länge hängt vom angeschlossenen Scannermodell ab.</li>
                <li>Lange Dokumente können zu einer größeren Dateigröße führen.</li>
            </ul>
        </div>
    "));

    private static HelpTopic TopicScanDisplay() => new("scan-display", "Aktionen nach dem Scannen",
        Wrap("Aktionen nach dem Scannen", @"
        <p>Nach Abschluss des Scans wird die gewählte Anwendung gestartet.</p>

        <h2>Bei aktivem Quick-Menü</h2>
        <p>Das <a href='#postscan-media'>[Quick-Menü] Dialogfeld</a> erscheint und bietet folgende Aktionen:</p>
        <ul>
            <li><b>Scan to Folder</b> – Öffnet das <a href='#postscan-save'>[In Ordner speichern] Dialogfeld</a></li>
            <li><b>Scan to E-Mail</b> – Startet das E-Mail-Programm mit den Scans als Anhang</li>
            <li><b>Scan to Print</b> – Druckt die gescannten Bilder</li>
            <li><b>Scan to Word / Excel / PowerPoint</b> – Erstellt ein Office-Dokument mit den gescannten Bildern</li>
            <li><b>Scan Picture Folder</b> – Speichert Bilder im Bildordner</li>
            <li><b>Edit with PDF</b> – Öffnet die PDF-Datei zur Bearbeitung</li>
        </ul>

        <h2>Bei deaktiviertem Quick-Menü</h2>
        <p>Die in der <a href='#app-tab'>[Anwendung] Registerkarte</a> ausgewählte Aktion wird
        direkt ausgeführt, ohne ein Auswahldialogfeld anzuzeigen.</p>

        <h2>Nachverarbeitung</h2>
        <p>Vor dem Speichern werden folgende Nachverarbeitungsschritte angewendet (je nach Einstellungen):</p>
        <ul>
            <li><b>Trägerblatt-Zusammenführung</b> – Paare von Seiten werden nebeneinander zusammengefügt</li>
            <li><b>Deskew</b> – Korrektur schiefer Zeichen (falls aktiviert)</li>
            <li><b>Auto-Rotate</b> – Automatische Ausrichtung mittels Tesseract OSD</li>
            <li><b>Leere Seitenerkennung</b> – Leere Seiten werden entfernt (falls aktiviert)</li>
        </ul>
    "));

    private static HelpTopic TopicScanCarrierSheet() => new("scan-carrier", "Scannen mit dem Trägerblatt",
        Wrap("Scannen mit dem Trägerblatt", @"
        <p>Mit dem Trägerblatt können Sie Dokumente scannen, die sonst schwer direkt
        in den Scanner einzulegen sind, wie z. B. Fotos, Zeitungsausschnitte oder
        zerrissene Dokumente.</p>

        <h2>Modi</h2>
        <ul>
            <li><b>Zwei Seiten in einem Bild erstellen</b> – Ein Dokument, das größer als A4 ist
            (z. B. A3), wird gefaltet und beide Seiten werden gescannt. Vorder- und Rückseite
            werden nebeneinander in einem Bild zusammengefasst.</li>
            <li><b>Vorder- und Rückseitenbild separat erstellen</b> – Für empfindliche Dokumente
            wie Fotos. Das gescannte Bild wird in einer vorbestimmten Größe ausgegeben.</li>
        </ul>

        <div class='warning'>
            <div class='warning-title'>ACHTUNG</div>
            <ul>
                <li>Nur bestimmte Scannermodelle unterstützen das Scannen mit dem Trägerblatt.</li>
                <li>Ein Mehrfacheinzug kann beim Scannen mit dem Trägerblatt nicht erkannt werden.</li>
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
        Tray-Symbol oder über <b>Einstellungen der SCAN Taste...</b> im Kontextmenü geöffnet.</p>

        <h2>Elemente</h2>
        <p><b>[Quick-Menü verwenden] Kontrollkästchen</b><br>
        Markieren Sie dieses Kontrollkästchen, um das Quick-Menü zu verwenden. Entfernen Sie
        die Markierung, falls Sie das Quick-Menü nicht verwenden möchten.</p>

        <p><b>[Empfohlen] Taste</b><br>
        Erscheint, wenn Quick-Menü verwenden ausgewählt wurde. Einstellungen: Automatisch für
        Bildqualität, Grundeinstellungen für andere Funktionen. Geeignet für Standarddokumente.</p>

        <p><b>[Kleine Datei] Taste</b><br>
        Einstellungen: Normal für Bildqualität. Geeignet zur Reduzierung der Dateigröße.</p>

        <p><b>[Hohe Bildqualität] Taste</b><br>
        Einstellungen: Beste für Bildqualität. Geeignet für hochwertige Scans.</p>

        <p><b>[Benutzerdefiniert] Taste</b><br>
        In der Grundeinstellung entsprechen diese Einstellungen denen von [Empfohlen].
        Diese können jedoch beliebig geändert werden.</p>

        <p><b>[Profil] Auswahlliste</b><br>
        Erscheint, wenn Quick-Menü verwenden nicht gewählt wurde. Sie können Profile wechseln,
        hinzufügen oder verwalten.</p>

        <p><b>[Detail] / [Ausblenden] Taste</b><br>
        Zeigt oder blendet die Detaileinstellungsregisterkarten aus.</p>

        <p><b>Detaileinstellungsregisterkarten</b><br>
        <ul>
            <li>Mit Quick-Menü: Speichern, Scanmodus, Dateiart, Papier, Dateigröße</li>
            <li>Ohne Quick-Menü: Anwendung, Speichern, Scanmodus, Dateiart, Papier, Dateigröße</li>
        </ul></p>

        <p><b>[OK] Taste</b> – Einstellungen übernehmen und Dialogfeld schließen.<br>
        <b>[Abbrechen] Taste</b> – Einstellungen verwerfen und Dialogfeld schließen.<br>
        <b>[Übernehmen] Taste</b> – Änderungen übernehmen, ohne das Dialogfeld zu schließen.</p>

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
            Wählen Sie die zu startende Anwendung aus dem Quick-Menü nach dem Scannen.</p>
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
        <p>Weitere Anwendungen können über das [Installieren/Deinstallieren] Dialogfeld hinzugefügt werden.</p>

        <p><b>[E-Mail-Optionen...] Taste</b><br>
        Erscheint, wenn Scan to E-Mail ausgewählt wurde. Öffnet das
        <a href='#email-options'>[Scan to E-Mail-Optionen] Dialogfeld</a>.</p>

        <p><b>[Druck-Optionen...] Taste</b><br>
        Erscheint, wenn Scan to Print ausgewählt wurde. Öffnet das
        <a href='#print-options'>[Scan to Print-Optionen] Dialogfeld</a>.</p>

        <p><b>[Installieren/Deinstallieren...] Taste</b><br>
        Öffnet das <a href='#app-manage'>[Installieren/Deinstallieren] Dialogfeld</a>.</p>

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

        <p><b>[Dateinameformat] Taste</b><br>
        Zeigt das <a href='#filename-format'>[Dateinamenformat] Dialogfeld</a> an, in dem das
        Dateinamenformat bestimmt werden kann.</p>

        <p><b>[Datei nach Scan umbenennen] Kontrollkästchen</b><br>
        Wenn markiert, erscheint nach dem Scannen das <a href='#postscan-save'>[In Ordner speichern]
        Dialogfeld</a>, in dem Ziel oder Dateiname geändert werden können.</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Der Standard-Speicherordner ist <code>Dokumente\SpeedScanManager</code> im
            Benutzerprofil. Wenn mehrere Dateien erstellt werden, wird dem Dateinamen eine
            Seriennummer angefügt.</p>
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
        <ul>
            <li><b>Automatisch</b> – Standarddokumente in hoher Qualität (Fein), Visitenkarten in besserer Qualität (Beste)</li>
            <li><b>Normal</b> (Farbe/Grau: 150 dpi, S&W: 300 dpi) – Schnelles Scannen</li>
            <li><b>Fein</b> (Farbe/Grau: 200 dpi, S&W: 400 dpi) – Hohe Qualität</li>
            <li><b>Beste</b> (Farbe/Grau: 300 dpi, S&W: 600 dpi) – Höhere Qualität</li>
            <li><b>Hervorragend</b> (Farbe/Grau: 600 dpi, S&W: 1200 dpi) – Höchste Qualität</li>
        </ul>
        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Abhängig von der Systemumgebung kann das Scannen im [Hervorragend] Modus die
            Scangeschwindigkeit herabsetzen. Mit höherer Auflösung nimmt die benötigte Zeit
            zum Scannen zu und die Dateigröße wird größer.</p>
        </div>

        <h2>[Farbmodus] Auswahlliste</h2>
        <ul>
            <li><b>Automatische Farberkennung</b> – Automatische Erkennung von Farbe, Grau oder Schwarzweiß</li>
            <li><b>Farbe</b> – Dokumente werden immer als Farbbilder gespeichert</li>
            <li><b>Grau</b> – Dokumente werden immer als Graubilder gespeichert</li>
            <li><b>Schwarzweiß</b> – Dokumente werden immer als Schwarzweiß-Bilder gespeichert</li>
        </ul>
        <div class='warning'>
            <div class='warning-title'>ACHTUNG</div>
            <p>Bei der [Automatische Farberkennung] ist das Scannen etwas langsamer. Schwach
            getöntes Papier oder Dokumente mit wenig Farbanteil können als Schwarzweiß erkannt werden.</p>
        </div>

        <h2>[Scan-Seite] Auswahlliste</h2>
        <ul>
            <li><b>Duplex-Scan (doppelseitig)</b> – Scannt beide Seiten des Dokuments</li>
            <li><b>Simplex-Scan (einseitig)</b> – Scannt nur eine Seite des Dokuments</li>
            <li><b>Flachbettscannen</b> – Scannen über das Flachbett</li>
            <li><b>Automatisch</b> – ADF oder Flachbett je nach Dokumenteinlage</li>
        </ul>

        <p><b>[Scanvorgang nach aktuellem Scan fortsetzen] Kontrollkästchen</b><br>
        Ist dieses Kontrollkästchen markiert, erscheint nach dem Scannen eine Abfrage zum
        Fortsetzen des Scans.</p>

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
            <li><b>PDF (*.pdf)</b> – Alle Bilder werden in einer PDF-Datei zusammengefasst</li>
            <li><b>JPEG (*.jpg)</b> – Für jede gescannte Seite wird eine JPEG-Datei erstellt</li>
            <li><b>PNG (*.png)</b> – Für jede gescannte Seite wird eine PNG-Datei erstellt</li>
        </ul>
        <div class='warning'>
            <div class='warning-title'>ACHTUNG</div>
            <p>JPEG und PNG stehen nur zur Verfügung, wenn [Farbe] oder [Grau] unter [Farbmodus]
            in der [Scanmodus] Registerkarte gewählt wurde.</p>
        </div>

        <h2>Texterkennung (OCR)</h2>
        <p><b>[In durchsuchbare PDF konvertieren] Kontrollkästchen</b><br>
        Führt OCR während des Scannens durch und erstellt eine durchsuchbare PDF-Datei.
        Der erkannte Text wird als unsichtbare Schicht über das Bild gelegt.</p>

        <p><b>[Sprache] Auswahlliste</b><br>
        Wählen Sie die Sprache für die Texterkennung. Verfügbar sind:
        Automatisch, Deutsch, Englisch, Französisch, Italienisch, Spanisch, Japanisch,
        Chinesisch (vereinfacht/traditionell), Koreanisch, Russisch, Portugiesisch und Arabisch.</p>

        <p><b>Zielseiten</b><br>
        <ul>
            <li><b>Erste Seite</b> – Texterkennung nur für die erste Seite</li>
            <li><b>Alle Seiten</b> – Texterkennung für alle Seiten</li>
        </ul></p>

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
            <li>Letter (8,5 × 11 in. / 216 × 279,4 mm)</li>
            <li>Double Letter (11 × 17 in. / 279,4 × 431,8 mm)</li>
            <li>Legal (8,5 × 14 in. / 216 × 355,6 mm)</li>
            <li>A3 (297 × 420 mm)</li>
            <li>A4 (210 × 297 mm)</li>
            <li>A5 (148 × 210 mm)</li>
            <li>A6 (105 × 148 mm)</li>
            <li>B4 (257 × 364 mm)</li>
            <li>B5 (182 × 257 mm)</li>
            <li>B6 (128 × 182 mm)</li>
            <li>Postkarte (100 × 148 mm)</li>
            <li>Visitenkarte (55 × 91 mm)</li>
            <li>Benutzerdefiniert (bis zu 10 Größen können hinzugefügt werden)</li>
        </ul>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Die verfügbaren Papiergrößen hängen vom verwendeten Scannermodell ab.
            Bei [Automatische Erkennung] wird die Papiergröße anhand der Papierränder erkannt.</p>
        </div>

        <p><b>[Trägerblatteinstellungen] Taste</b><br>
        Öffnet das <a href='#carrier-sheet-dialog'>[Trägerblatteinstellungen] Dialogfeld</a>.</p>

        <p><b>[Benutzerdefiniert] Taste</b><br>
        Zeigt das <a href='#custom-size-dialog'>[Hinzufügen oder Entfernen von benutzerdefinierten Größen] Dialogfeld</a> an.</p>

        <h2>[Mehrfacheinzugserkennung] Auswahlliste</h2>
        <ul>
            <li><b>Keine</b> – Mehrfacheinzugserkennung wird nicht ausgeführt</li>
            <li><b>Überprüfung der Länge</b> – Überwacht die Blattlängen der eingezogenen Dokumente</li>
            <li><b>Überprüfung von Überlappung (Ultraschall)</b> – Überwacht die Blattstärken der eingezogenen Dokumente</li>
            <li><b>Beide</b> – Längen- und Ultraschallprüfung kombiniert</li>
        </ul>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Die verfügbaren Mehrfacheinzugserkennungs-Modi hängen vom Scannermodell ab.
            Die Fähigkeiten des Scanners werden beim Verbinden automatisch abgefragt.</p>
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
            <p>Die [Dateigröße] Registerkarte wird deaktiviert, wenn [Schwarzweiß] in der
            [Farbmodus] Auswahlliste unter der [Scanmodus] Registerkarte ausgewählt wurde.
            Die Komprimierungsrate für Schwarzweißbilder wird übernommen.</p>
        </div>

        <h2>[Komprimierungsrate] Schieber</h2>
        <p>Die Komprimierungsrate kann fünfstufig eingestellt werden. Der ausgewählte Wert
        wird rechts neben dem Regler angezeigt.</p>
        <ul>
            <li>Je größer der Wert, desto höher die Komprimierungsrate und desto kleiner die Dateigröße</li>
            <li>Eine höhere Komprimierungsrate führt zu einer Verschlechterung der Bildqualität</li>
            <li>Diese Einstellung ist nur beim Scannen von Farb- und Graubildern effektiv</li>
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
        Intensität für das Scannen von Schwarzweißbildern, einstellbar.
        Standard: Normal (Mitte). Nur verfügbar, wenn [Schwarzweiß] als Farbmodus gewählt wurde.</p>

        <p><b>[Einstellung nur für Textdokumente] Kontrollkästchen</b><br>
        Betont den Kontrast der gescannten Bilder. Wählen Sie dieses für zweiseitige
        Schwarzweißdokumente oder Dokumente mit handschriftlichen Texten.</p>

        <p><b>[Automatisches Löschen leerer Seiten zulassen] Kontrollkästchen</b><br>
        Leere Seiten werden automatisch erkannt und aus dem Ausgabebild gelöscht.
        Die Erkennung erfolgt über schnelle Pixel-Sampling mit LockBits.</p>

        <p><b>[Automatische Korrektur schiefer Zeichen zulassen] Kontrollkästchen</b><br>
        Korrigiert schiefe Zeichen auf einem Dokument.</p>

        <p><b>[Automatische Bilddrehung zulassen] Kontrollkästchen</b><br>
        Dokumente, die seitlich oder kopfüber gescannt wurden, werden in der korrekten
        Richtung ausgegeben. Die Ausrichtung wird mittels Tesseract OSD erkannt.</p>

        <p><b>[Dokumente mit der Vorderseite nach oben einlegen] Kontrollkästchen</b><br>
        Die erste nach oben zeigende Seite wird als erste Seite gescannt.</p>

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
        Erstellt nach dem Erreichen der festgelegten Seitenzahl jeweils eine neue PDF-Datei.</p>

        <h2>Kennwort</h2>
        <p><b>[Kennwort für PDF-Datei einstellen] Kontrollkästchen</b><br>
        Aktiviert den Kennwortschutz für die erstellten PDF-Dateien.</p>

        <p><b>Kennwort / Kennwort bestätigen</b><br>
        Geben Sie das gewünschte Kennwort ein und bestätigen Sie es.
        Das Kennwort wird als ""Kennwort zum Öffnen des Dokuments"" verwendet.</p>

        <div class='see-also'>Siehe auch:
            <ul>
                <li><a href='#filetype-tab'>[Dateiart] Registerkarte</a></li>
            </ul>
        </div>
    "));

    private static HelpTopic TopicFileNameFormatDialog() => new("filename-format", "[Dateinamenformat] Dialogfeld",
        Wrap("[Dateinamenformat] Dialogfeld", @"
        <p>In diesem Dialogfeld kann das Dateinamenformat bestimmt werden, in dem die Dateien
        automatisch gespeichert werden.</p>

        <h2>Datum und Uhrzeit</h2>
        <p><b>[Benutzen Sie die Einstellung Ihres Betriebssystems]</b><br>
        Verwendet die Datum-/Uhrzeit-Einstellungen des Betriebssystems.</p>

        <p><b>[jjjjMMddHHmmss]</b><br>
        Verwendet das Format ""jjjjMMddHHmmss"" für den Dateinamen.</p>

        <h2>Benutzerdefinierter Dateiname</h2>
        <p><b>Dateiname</b> – Bis zu 30 Zeichen für den Anfang des Dateinamens.</p>
        <p><b>[Zähler] Auswahlliste</b> – Stellenanzahl für die Seriennummer (0 bis 6 Ziffern).</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Bei mehreren Dateien wird eine Seriennummer angefügt. Beispiel mit Dateiname ""Scan""
            und 3 Ziffern: Scan001.pdf, Scan002.pdf, Scan003.pdf ...</p>
        </div>
    "));

    private static HelpTopic TopicNewProfileDialog() => new("new-profile", "[Neues Profil hinzufügen] Dialogfeld",
        Wrap("[Neues Profil hinzufügen] Dialogfeld", @"
        <p>In diesem Dialogfeld können Namen für neue Profile vergeben werden.
        Bis zu 20 Profile können hinzugefügt werden.</p>

        <h2>Elemente</h2>
        <p><b>Neuer Profilname</b> – Geben Sie den Namen für das neue Profil ein.</p>
        <p><b>[OK] Taste</b> – Einstellungen übernehmen und Dialogfeld schließen.</p>
        <p><b>[Abbrechen] Taste</b> – Einstellungen verwerfen und Dialogfeld schließen.</p>
    "));

    private static HelpTopic TopicProfileManagementDialog() => new("profile-mgmt-dialog", "[Profilverwaltung] Dialogfeld",
        Wrap("[Profilverwaltung] Dialogfeld", @"
        <p>In diesem Dialogfeld können Sie Profilnamen ändern oder löschen, sowie die
        Reihenfolge der Auflistung ändern.</p>

        <h2>Elemente</h2>
        <p><b>[Umbenennen] Taste</b> – Ändert die ausgewählte Profilbezeichnung.
        Alle Profile können umbenannt werden.</p>

        <p><b>[Löschen] Taste</b> – Löscht das gewählte Profil.
        Alle Profile können gelöscht werden.</p>

        <p><b>[Oben] Taste</b> – Verschiebt das Profil um eine Position nach oben.</p>
        <p><b>[Unten] Taste</b> – Verschiebt das Profil um eine Position nach unten.</p>
        <p><b>[Schließen] Taste</b> – Schließt das Dialogfeld.</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Alle Profile können frei umbenannt, gelöscht und neu angeordnet werden.</p>
        </div>
    "));

    private static HelpTopic TopicAppManageDialog() => new("app-manage", "[Installieren/Deinstallieren] Dialogfeld",
        Wrap("[Installieren/Deinstallieren] Dialogfeld", @"
        <p>In diesem Dialogfeld können Sie benutzerdefinierte Anwendungen hinzufügen, entfernen
        oder ändern. Bis zu zehn Anwendungen können hinzugefügt werden.</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Die hier getroffenen Änderungen werden unabhängig von der [OK] oder [Abbrechen]
            Taste im Einstellungsdialogfeld übernommen.</p>
        </div>

        <h2>Elemente</h2>
        <p><b>Anwendungsliste</b> – Liste der hinzugefügten Anwendungen.</p>
        <p><b>[Hinzufügen] Taste</b> – Fügt eine neue Anwendung hinzu.</p>
        <p><b>[Entfernen] Taste</b> – Entfernt die ausgewählte Anwendung.</p>
        <p><b>[Ändern] Taste</b> – Ändert die Einstellungen einer Anwendung.</p>
        <p><b>[Schließen] Taste</b> – Schließt das Dialogfeld.</p>
    "));

    private static HelpTopic TopicEmailOptionsDialog() => new("email-options", "[Scan to E-Mail-Optionen] Dialogfeld",
        Wrap("[Scan to E-Mail-Optionen] Dialogfeld", @"
        <p>In diesem Dialogfeld können Sie Einstellungen für die Verwendung von Scan to E-Mail
        konfigurieren.</p>

        <h2>Elemente</h2>
        <p><b>Standard-Empfänger</b> – E-Mail-Adresse des Standardempfängers.
        Wird beim Erstellen einer E-Mail automatisch eingetragen.</p>

        <p><b>Betreff-Vorlage</b> – Vorlage für den Betreff der E-Mail.
        Standard: ""Gescanntes Dokument"".</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Das als Standardanwendung festgelegte E-Mail-Programm (MAPI) wird verwendet.
            Die gescannten Dateien werden als Anhang versendet.</p>
        </div>
    "));

    private static HelpTopic TopicPrintOptionsDialog() => new("print-options", "[Scan to Print-Optionen] Dialogfeld",
        Wrap("[Scan to Print-Optionen] Dialogfeld", @"
        <p>In diesem Dialogfeld können die Optionen der Funktion Scan to Print eingestellt werden.</p>

        <h2>Elemente</h2>
        <p><b>Zieldrucker</b> – Auswahlliste aller im Betriebssystem erkannten Drucker.
        Der Standarddrucker wird automatisch vorausgewählt.</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Beim Drucken werden die gescannten Bilder an den ausgewählten Drucker gesendet.
            Die Druckqualität entspricht der Scan-Auflösung.</p>
        </div>
    "));

    private static HelpTopic TopicCarrierSheetDialog() => new("carrier-sheet-dialog", "[Trägerblatteinstellungen] Dialogfeld",
        Wrap("[Trägerblatteinstellungen] Dialogfeld", @"
        <p>In diesem Dialogfeld können Sie Einstellungen für das Scannen mit dem Trägerblatt
        vornehmen.</p>

        <h2>[Modus] Auswahlliste</h2>
        <ul>
            <li><b>Zwei Seiten in einem Bild erstellen</b> – Für Dokumente größer als A4 (z. B. A3, B4).
            Vorder- und Rückseite werden nebeneinander in einem Bild zusammengefasst.</li>
            <li><b>Vorder- und Rückseitenbild separat erstellen</b> – Für empfindliche Dokumente
            wie Fotos. Das gescannte Bild wird in einer bestimmten Größe ausgegeben.</li>
        </ul>

        <h2>[Ausgabebildgröße] Auswahlliste</h2>
        <p>Folgende Größen sind verfügbar (je nach Modus):</p>
        <ul>
            <li>Automatische Erkennung</li>
            <li>A3, A4, A5, A6, B4, B5, B6</li>
            <li>Letter, Double Letter</li>
            <li>Postkarte, Visitenkarte</li>
            <li>Trägerblattgröße (220 × 297 mm)</li>
            <li>Benutzerdefiniert (bis zu 5 Größen)</li>
        </ul>

        <p><b>[Benutzerdefiniert] Taste</b> – Fügt eine benutzerdefinierte Größe hinzu.
        Nur verfügbar im Modus ""Vorder- und Rückseitenbild separat erstellen"".</p>

        <div class='warning'>
            <div class='warning-title'>ACHTUNG</div>
            <p>Nur bestimmte Scannermodelle unterstützen das Scannen mit dem Trägerblatt.</p>
        </div>
    "));

    private static HelpTopic TopicCustomSizeDialog() => new("custom-size-dialog", "[Hinzufügen oder Entfernen von benutzerdefinierten Größen] Dialogfeld",
        Wrap("[Hinzufügen oder Entfernen von benutzerdefinierten Größen] Dialogfeld", @"
        <p>In diesem Dialogfeld können Einstellungen von benutzerdefinierten Dokumentengrößen
        hinzugefügt, gelöscht oder geändert werden. Bis zu zehn benutzerdefinierte
        Dokumentengrößen können hinzugefügt werden.</p>

        <h2>Elemente</h2>
        <p><b>Benutzerdefinierte Größen</b> – Liste der benutzerdefinierten Größen.</p>
        <p><b>[Hinzufügen] Taste</b> – Öffnet das Dialogfeld für zusätzliche benutzerdefinierte Größen.</p>
        <p><b>[Entfernen] Taste</b> – Entfernt die gewählte Größe.</p>
        <p><b>[Ändern] Taste</b> – Ändert die Einstellungen der gewählten Größe.</p>
        <p><b>[Schließen] Taste</b> – Schließt das Dialogfeld.</p>

        <h2>Einstellungen für zusätzliche benutzerdefinierte Größen</h2>
        <p><b>Dokumentengröße</b> – Geben Sie Breite und Länge in mm oder inches ein.</p>
        <p><b>[Automatische Länge] Kontrollkästchen</b> – Automatische Erkennung der Länge.</p>
        <p><b>Bezeichnung</b> – Name für die Papiergröße (bis zu 62 Zeichen).</p>
    "));

    private static HelpTopic TopicVersionInfoDialog() => new("version-info", "[Versionsinformationen] Dialogfeld",
        Wrap("[Versionsinformationen] Dialogfeld", @"
        <p>In diesem Dialogfeld werden Versionsinformationen zu SpeedScan Manager und den
        verwendeten Komponenten angezeigt.</p>

        <h2>Elemente</h2>
        <p><b>Logo und Versionsnummer</b> – Zeigt das SpeedScan Manager Logo und die aktuelle
        Version an.</p>
        <p><b>Lizenzinformationen</b> – Zeigt Informationen zur GPL-3.0-Lizenz und die
        Copyright-Hinweise an.</p>
        <p><b>Verwendete Komponenten</b> – Listet die verwendeten Bibliotheken auf:</p>
        <ul>
            <li>NTwain (TWAIN-Unterstützung)</li>
            <li>Tesseract.NET (OCR-Engine)</li>
            <li>PdfSharpCore (PDF-Verarbeitung)</li>
        </ul>
        <p><b>[Detail...] Taste</b> – Öffnet das Dialogfeld für <a href='#driver-info'>Scanner- und Treiberinformationen</a>.</p>
        <p><b>[OK] Taste</b> – Schließt das Dialogfeld.</p>
        <p><b>[Hilfe] Taste</b> – Öffnet diese Hilfe.</p>
    "));

    private static HelpTopic TopicPostScanMediaDialog() => new("postscan-media", "[Quick-Menü] Dialogfeld",
        Wrap("[Quick-Menü] Dialogfeld", @"
        <p>Nach dem Scannen wird dieses Dialogfeld angezeigt, wenn das Quick-Menü aktiviert ist.
        Wählen Sie eine Aktion für die gescannten Dokumente aus.</p>

        <h2>Verfügbare Aktionen</h2>
        <ul>
            <li><b>Scan to Folder</b> – Speichert die gescannten Bilder in einem Ordner.</li>
            <li><b>Scan to E-Mail</b> – Versendet die gescannten Bilder als E-Mail-Anhang.</li>
            <li><b>Scan to Print</b> – Druckt die gescannten Bilder direkt.</li>
            <li><b>Scan to Word</b> – Erstellt ein Word-Dokument mit den gescannten Bildern.</li>
            <li><b>Scan to Excel</b> – Erstellt eine Excel-Datei mit den gescannten Bildern.</li>
            <li><b>Scan to PowerPoint</b> – Erstellt eine PowerPoint-Präsentation mit den gescannten Bildern.</li>
            <li><b>Scan Picture Folder</b> – Speichert die Bilder als Bilddateien in einem Ordner.</li>
            <li><b>Edit with PDF</b> – Öffnet die gescannte PDF-Datei zur Bearbeitung.</li>
        </ul>

        <h2>Bedienung</h2>
        <p>Klicken Sie auf eine Aktion, um sie auszuwählen. Doppelklicken Sie, um die Aktion
        direkt auszuführen. Alternativ wählen Sie eine Aktion aus und klicken Sie auf
        <b>[Speichern]</b>.</p>

        <p><b>[Speichern] Taste</b> – Führt die ausgewählte Aktion aus.</p>
        <p><b>[Abbrechen] Taste</b> – Bricht ab und schließt das Dialogfeld.</p>

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
        das Speichern Dateiname und Zielordner bestimmt werden.</p>

        <h2>Elemente</h2>
        <p><b>Vorschau</b> – Zeigt das gescannte Dokumentenbild an. Bei mehreren Seiten kann
        durch die Seiten navigiert werden.</p>
        <p><b>Seitennavigation</b> – Zeigt die aktuelle Seitenzahl und ermöglicht das
        Durchblättern der gescannten Seiten.</p>
        <p><b>Titel</b> – Zeigt den Dateinamen an. Kann geändert werden.</p>
        <p><b>Speichern in</b> – Zeigt den Zielordner an. Kann direkt eingegeben oder über
        [Durchsuchen] geändert werden.</p>
        <p><b>[Durchsuchen] Taste</b> – Öffnet einen Dialog zur Ordnerauswahl.</p>
        <p><b>[Speichern] Taste</b> – Speichert das gescannte Bild.</p>
        <p><b>[Abbrechen] Taste</b> – Bricht ab. Bereits erstellte Dateien werden gelöscht.</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Bei mehreren Dateien wird eine Seriennummer angefügt.</p>
        </div>
    "));

    private static HelpTopic TopicScannerDriverInfoDialog() => new("driver-info", "[Scanner- und Treiberinformationen] Dialogfeld",
        Wrap("[Scanner- und Treiberinformationen] Dialogfeld", @"
        <p>In diesem Dialogfeld werden detaillierte Informationen zum angeschlossenen Scanner
        und den installierten Treiberkomponenten angezeigt.</p>

        <h2>Elemente</h2>
        <p><b>Scanner-Informationen</b> – Zeigt Modellname, Hersteller, Produktfamilie und
        Treiberversion des angeschlossenen Scanners an.</p>
        <p><b>Treiber-Versionen</b> – Listet die Versionen der verwendeten Bibliotheken auf:</p>
        <ul>
            <li>NTwain (TWAIN-Treiber-Bibliothek)</li>
            <li>Tesseract (OCR-Engine)</li>
            <li>PdfSharpCore (PDF-Verarbeitung)</li>
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
        [Profil] Auswahlliste im Einstellungsdialogfeld auswählen.</p>

        <p>Bis zu 20 Profile, einschließlich der bereits in der [Profil] Auswahlliste
        enthaltenen, können hinzugefügt werden.</p>

        <h2>Neues Profil hinzufügen</h2>
        <ol>
            <li>Wählen Sie [Profil hinzufügen] in der [Profil] Auswahlliste.</li>
            <li>Vergeben Sie im <a href='#new-profile'>[Neues Profil hinzufügen] Dialogfeld</a> einen Namen.</li>
        </ol>

        <h2>Profile umbenennen, löschen oder sortieren</h2>
        <p>Öffnen Sie das <a href='#profile-mgmt-dialog'>[Profilverwaltung] Dialogfeld</a> über:</p>
        <ul>
            <li>[Profilverwaltung] in der [Profil] Auswahlliste des Einstellungsdialogfelds</li>
            <li>[Profilverwaltung] im Rechtsklick-Menü des Tray-Symbols</li>
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
        <h2>Grundeinstellungen für das Quick-Menü</h2>
        <table>
            <tr><th>Einstellung</th><th>Grundeinstellung</th></tr>
            <tr><td>Profil-Taste</td><td>[Empfohlen]</td></tr>
            <tr><td>Speicherordner</td><td>Dokumente\SpeedScanManager</td></tr>
            <tr><td>Dateinamenformat</td><td>jjjjMMddHHmmss</td></tr>
            <tr><td>Bildqualität</td><td>Automatisch</td></tr>
            <tr><td>Farbmodus</td><td>Automatische Farberkennung</td></tr>
            <tr><td>Scan-Seite</td><td>Automatisch</td></tr>
            <tr><td>Dateiformat</td><td>PDF (*.pdf)</td></tr>
            <tr><td>Papiergröße</td><td>Automatische Erkennung</td></tr>
            <tr><td>Mehrfacheinzugserkennung</td><td>Überprüfung von Überlappung (Ultraschall)</td></tr>
            <tr><td>Komprimierungsrate</td><td>Stufe 3 von 5</td></tr>
            <tr><td>Automatische Bilddrehung</td><td>Aktiviert</td></tr>
            <tr><td>Automatisches Löschen leerer Seiten</td><td>Aktiviert</td></tr>
        </table>

        <h2>Standardprofile</h2>
        <p>Beim ersten Start werden folgende Standardprofile erstellt:</p>
        <table>
            <tr><th>Profil</th><th>Anwendung</th><th>Bildqualität</th><th>Komprimierungsrate</th></tr>
            <tr><td>Scan to Folder</td><td>Scan to Folder</td><td>Automatisch</td><td>Stufe 3</td></tr>
            <tr><td>Empfohlen</td><td>Scan to Folder</td><td>Automatisch</td><td>Stufe 3</td></tr>
            <tr><td>Kleine Datei</td><td>Scan to Folder</td><td>Normal</td><td>Stufe 5</td></tr>
            <tr><td>Hohe Bildqualität</td><td>Scan to Folder</td><td>Fein</td><td>Stufe 1</td></tr>
        </table>
        <p>Alle Standardprofile können umbenannt, gelöscht und neu angeordnet werden.</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Wenn der angeschlossene Scanner über kein Flachbett verfügt, wird für [Scan-Seite]
            [Duplex Scan] übernommen. Steht Duplex Scan nicht zur Verfügung, wird [Simplex Scan] verwendet.</p>
        </div>
    "));

    // ═══════════════════════════════════════════════════════════════════════
    // Fehlerbehebung
    // ═══════════════════════════════════════════════════════════════════════

    private static HelpTopic TopicMultiFeedDialog() => new("multifeed-dialog", "Mehrfacheinzugserkennungsdialogfeld",
        Wrap("Mehrfacheinzugserkennungsdialogfeld", @"
        <p>Wenn während des Scannens ein Mehrfacheinzug erkannt wurde, wird der Scanvorgang
        unterbrochen und dieses Dialogfeld angezeigt.</p>

        <h2>Elemente</h2>
        <p><b>Vorschaubilder</b> – Zeigt die aktuelle Seite (mit Mehrfacheinzug) und die
        vorherige Seite zur Beurteilung.</p>

        <p><b>[Wiederholen] Taste</b> – Bricht den aktuellen Scan ab und startet neu.
        Die bereits gescannten Bilder werden verworfen.</p>

        <p><b>[So übernehmen] Taste</b> – Behält die Bilder mit Mehrfacheinzug.
        Der Scanvorgang wird fortgesetzt.</p>

        <p><b>[Erkennung ausschalten] Taste</b> – Deaktiviert die Mehrfacheinzugserkennung
        für den restlichen Scan und übernimmt die aktuelle Seite.</p>

        <div class='warning'>
            <div class='warning-title'>ACHTUNG</div>
            <ul>
                <li>Ein Mehrfacheinzug kann beim Scannen mit dem Trägerblatt oder beim Scannen
                langer Seiten nicht erkannt werden.</li>
                <li>Umschläge oder Dokumente mit aufgeklebten Zetteln/Fotos/Briefmarken werden
                als Mehrfacheinzug erkannt.</li>
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
        um die Seite zu behalten und den Scan fortzusetzen.</p>

        <h2>Dokumente wurden nicht in der Reihenfolge des Originaldokuments gescannt</h2>
        <ol>
            <li>Öffnen Sie den ADF, entnehmen Sie die Dokumente und richten Sie die Blattkanten erneut aus.</li>
            <li>Entnehmen Sie die erneut zu scannenden Dokumente aus dem Ausgabefach und legen Sie diese erneut ein.</li>
            <li>Platzieren Sie die in Schritt 1 entfernten Dokumente über den verbliebenen Dokumenten.</li>
            <li>Wählen Sie [Wiederholen] im Mehrfacheinzugserkennungsdialogfeld.</li>
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
        var messages = new[]
        {
            "Der Scanner ist nicht angeschlossen oder ausgeschaltet.",
            "Der Scanner kann nicht verwendet werden. Prüfen Sie, ob der Scanner von anderen Anwendungen verwendet wird.",
            "Keine Verbindung zum Scanner. Prüfen Sie, ob der Scanner eingeschaltet oder verbunden ist.",
            "Kommunikation mit dem Scanner fehlgeschlagen.",
            "Scanner wird von einem anderen Benutzer bzw. Programm verwendet.",
            "Es ist kein Papier im Scanner eingelegt.",
            "Papierstau.",
            "Die Scannerabdeckung ist offen.",
            "Die Sensoren sind verschmutzt.",
            "Fehler im optischen System.",
            "Hardwarefehler aufgetreten.",
            "Problem im Transportmechanismus des Scanners.",
            "Wechsel des Einzugsmodus erkannt.",
            "Ein überlappendes Dokument wurde erkannt (Mehrfacheinzug).",
            "Ein nicht der angegebenen Größe entsprechendes Dokument wurde gescannt.",
            "Fehler bei Datenübertragung aufgetreten.",
            "Zeitüberschreitung während Kommunikation.",
            "Scanvorgang durch Benutzer abgebrochen.",
            "Scan fehlgeschlagen. Der erforderliche Speicher oder die benötigten Ressourcen sind eventuell unzureichend.",
            "Alle Seiten wurden als leer erkannt. Überprüfen Sie, ob der Scan korrekt ausgeführt wurde.",
            "Texterkennung fehlgeschlagen.",
            "PDF-Datei konnte nicht erstellt werden.",
            "Speichern der Bilder fehlgeschlagen.",
            "Druck fehlgeschlagen.",
            "Fehler beim Start der gewählten Anwendung.",
            "E-Mail konnte nicht gesendet werden.",
            "Der ausgewählte Zielordner ist ungültig.",
            "Der Dateiname ist ungültig. Folgende Zeichen sind nicht erlaubt: \\ / : * ? \" < > |",
            "Eine Datei mit diesem Namen existiert bereits.",
            "Nicht genügend Speicher vorhanden.",
            "Verarbeitung wegen unzureichendem Festplattenspeicher nicht möglich.",
            "Ein interner Fehler ist aufgetreten.",
            "Unerwarteter Fehler aufgetreten.",
        };

        var sb = new StringBuilder();
        sb.AppendLine("<p>Folgende Meldungen können während der Verwendung von SpeedScan Manager erscheinen:</p>");
        sb.AppendLine("<ul>");
        foreach (var msg in messages)
            sb.AppendLine($"  <li>{msg}</li>");
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
            <tr><td>WIA</td><td>Windows Image Acquisition (Windows-Bilderfassung)</td></tr>
            <tr><td>TWAIN</td><td>Standard-Protokoll für die Kommunikation zwischen Scanner und Software</td></tr>
            <tr><td>MAPI</td><td>Messaging Application Programming Interface (E-Mail-Schnittstelle)</td></tr>
            <tr><td>S&W</td><td>Schwarz und Weiß</td></tr>
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
