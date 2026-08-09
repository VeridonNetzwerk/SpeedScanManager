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
/// adapted from the ScanSnap Manager for fi Series help documentation.
/// All references to "ScanSnap Manager for fi Series" have been changed to
/// "SpeedScan Manager". Features not present in the actual program have been
/// removed. Features present in the program but not in the original help
/// (e.g. PNG format) have been added.
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
                TopicScannerConnection()
            ),
            new HelpTopic("scanning", "Scannen von Dokumenten",
                TopicScanQuickMenu(),
                TopicScanWithoutQuickMenu(),
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
                TopicAppAddRemoveDialog(),
                TopicAppSettingsDialog(),
                TopicEmailOptionsDialog(),
                TopicEmailDialog(),
                TopicPrintOptionsDialog(),
                TopicPrintDialog(),
                TopicScanToFolderDialog(),
                TopicCarrierSheetDialog(),
                TopicCustomSizeDialog(),
                TopicSerializeDialog(),
                TopicPasswordDialog()
            ),
            new HelpTopic("profiles", "Profile",
                TopicProfileManagement(),
                TopicDefaultSettings()
            ),
            new HelpTopic("troubleshooting", "Fehlerbehebung",
                TopicScanErrorDialog(),
                TopicMultiFeedDialog(),
                TopicMultiFeedMeasures(),
                TopicMessageList()
            ),
            new HelpTopic("reference", "Anhang",
                TopicAbbreviations(),
                TopicKeywordMarking()
            )
        );
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Erste Schritte
    // ═══════════════════════════════════════════════════════════════════════

    private static HelpTopic TopicOverview() => new("overview", "Überblick",
        Wrap("Überblick", @"
        <p>SpeedScan Manager ist eine Software für den Betrieb von Fujitsu fi-Scanner-Serien.
        Mit SpeedScan Manager können Sie Dokumente einfach und schnell scannen und die gescannten
        Bilder in verschiedenen Formaten speichern oder weiterverarbeiten.</p>

        <h2>Hauptfunktionen</h2>
        <ul>
            <li><b>Quick-Menü</b> – Einfaches Scannen mit voreingestellten Profilen (Empfohlen, Kleine Datei, Hohe Bildqualität, Benutzerdefiniert)</li>
            <li><b>Profile</b> – Bis zu 20 Profile mit individuellen Einstellungen können verwaltet werden</li>
            <li><b>Scan to Folder</b> – Gescannte Bilder in einem Ordner speichern</li>
            <li><b>Scan to E-Mail</b> – Gescannte Bilder als Anhang einer E-Mail versenden</li>
            <li><b>Scan to Print</b> – Gescannte Bilder direkt drucken</li>
            <li><b>OCR-Texterkennung</b> – Durchsuchbare PDF-Dateien erstellen</li>
            <li><b>Trägerblatt-Unterstützung</b> – Scannen von großen oder empfindlichen Dokumenten</li>
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
            <li>USB-Anschluss (USB 2.0 oder höher) für den Scanner</li>
        </ul>

        <h2>Software</h2>
        <ul>
            <li>.NET 8.0 Desktop Runtime</li>
            <li>E-Mail-Programm (für Scan to E-Mail)</li>
            <li>Drucker-Treiber (für Scan to Print)</li>
        </ul>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Die OCR-Texterkennung unterstützt die Sprachen Deutsch und Englisch.</p>
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
            <li><b>Duplex-Scan</b> – Startet einen beidseitigen Scan</li>
            <li><b>Simplex-Scan</b> – Startet einen einseitigen Scan</li>
            <li><b>Flachbettscannen</b> – Startet einen Scan über das Flachbett</li>
            <li><b>Einstellungen...</b> – Öffnet das Einstellungsdialogfeld</li>
            <li><b>Scan-Ergebnis anzeigen</b> – Zeigt die zuletzt gescannten Dateien an</li>
            <li><b>Hilfe</b> – Öffnet diese Hilfe</li>
            <li><b>Beenden</b> – Beendet SpeedScan Manager</li>
        </ul>

        <h2>Doppelklick</h2>
        <p>Ein Doppelklick auf das Tray-Symbol öffnet das Einstellungsdialogfeld.</p>
    "));

    private static HelpTopic TopicScannerConnection() => new("scanner-connection", "Scanner verbinden",
        Wrap("Scanner verbinden", @"
        <p>Verbinden Sie den Scanner über ein USB-Kabel mit dem Computer.</p>

        <h2>Schritte zum Verbinden</h2>
        <ol>
            <li>Schalten Sie den Scanner ein.</li>
            <li>Verbinden Sie den Scanner über das USB-Kabel mit dem Computer.</li>
            <li>SpeedScan Manager erkennt den Scanner automatisch und aktualisiert das Tray-Symbol.</li>
        </ol>

        <div class='warning'>
            <div class='warning-title'>ACHTUNG</div>
            <ul>
                <li>Schließen Sie nur einen Scanner gleichzeitig an den Computer an.</li>
                <li>Wenn der Scanner nicht erkannt wird, prüfen Sie die USB-Verbindung und stellen Sie sicher, dass der Scanner eingeschaltet ist.</li>
            </ul>
        </div>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>SpeedScan Manager kommuniziert mit dem Scanner über die TWAIN-Schnittstelle.
            Stellen Sie sicher, dass der TWAIN-Treiber des Scanners installiert ist.</p>
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
            <li>Wählen Sie im Quick-Menü die gewünschte Aktion (z. B. Scan to Folder, Scan to E-Mail, Scan to Print).</li>
        </ol>

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
            </ul>
        </div>
    "));

    private static HelpTopic TopicScanWithoutQuickMenu() => new("scan-no-quick", "Scannen ohne Quick-Menü",
        Wrap("Scannen ohne Quick-Menü", @"
        <p>Wenn das Quick-Menü deaktiviert ist, können Sie detaillierte Scaneinstellungen
        über das Einstellungsdialogfeld vornehmen und Profile verwenden.</p>

        <h2>Schritte</h2>
        <ol>
            <li>Wählen Sie ein Profil aus der Profil-Dropdown-Liste im Einstellungsdialogfeld.</li>
            <li>Passen Sie die Einstellungen auf den Registerkarten an (Anwendung, Speichern, Scanmodus, Dateiart, Papier, Dateigröße).</li>
            <li>Klicken Sie auf <span class='kbd'>OK</span>, um die Einstellungen zu übernehmen.</li>
            <li>Legen Sie die Dokumente in den Scanner ein und starten Sie den Scan.</li>
        </ol>

        <div class='see-also'>Siehe auch:
            <ul>
                <li><a href='#settings'>Einstellungsdialogfeld</a></li>
                <li><a href='#profiles'>Profile verwalten</a></li>
            </ul>
        </div>
    "));

    private static HelpTopic TopicScanLongPages() => new("scan-long", "Scannen langer Seiten",
        Wrap("Scannen langer Seiten", @"
        <p>Lange Dokumente (z. B. Quittungen oder Endlosdokumente) können gescannt werden,
        wenn für die Papiergröße <b>Automatische Erkennung</b> ausgewählt ist.</p>

        <p>Beim Scannen langer Seiten gelten folgende Einschränkungen:</p>
        <ul>
            <li>Die <b>Papiergröße</b>-Einstellung wird ignoriert; es wird immer <b>Automatische Erkennung</b> verwendet.</li>
            <li>Die <b>Mehrfacheinzugserkennung</b> wird deaktiviert (auf <b>Keine</b> gesetzt).</li>
        </ul>

        <div class='warning'>
            <div class='warning-title'>ACHTUNG</div>
            <ul>
                <li>Die maximale Länge hängt vom angeschlossenen Scannermodell ab.</li>
                <li>Lange Dokumente können zu einer größeren Dateigröße führen.</li>
            </ul>
        </div>
    "));

    private static HelpTopic TopicScanDisplay() => new("scan-display", "Anzeige während des Scannens",
        Wrap("Anzeige während des Scannens", @"
        <p>Während des Scanvorgangs zeigt das Tray-Symbol den Scan-Status an. Eine
        Statusanzeige informiert über den Fortschritt.</p>

        <p>Nach Abschluss des Scans wird die gewählte Anwendung gestartet:</p>
        <ul>
            <li><b>Scan to Folder</b> – Das <a href='#scan-folder-dialog'>[Scan to Folder] Dialogfeld</a> erscheint</li>
            <li><b>Scan to E-Mail</b> – Das <a href='#email-dialog'>[Scan to E-Mail] Dialogfeld</a> erscheint (falls Vorschau aktiviert)</li>
            <li><b>Scan to Print</b> – Das <a href='#print-dialog'>[Scan to Print] Dialogfeld</a> erscheint (falls Druckdialog aktiviert)</li>
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
        für das Scannen von Dokumenten konfigurieren.</p>

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
            Wählen Sie die zu startende Anwendung aus dem Quick-Menü.</p>
        </div>

        <h2>Elemente</h2>
        <p><b>[Anwendung] Auswahlliste</b><br>
        Zeigt eine Liste der mit SpeedScan Manager interagierenden Anwendungen an:</p>
        <ul>
            <li>Scan to Folder</li>
            <li>Scan to E-Mail</li>
            <li>Scan to Print</li>
        </ul>
        <p>Weitere Anwendungen können über das [Anwendung hinzufügen/entfernen] Dialogfeld hinzugefügt werden.</p>

        <p><b>[E-Mail-Optionen...] Taste</b><br>
        Erscheint, wenn Scan to E-Mail ausgewählt wurde. Öffnet das
        <a href='#email-options'>[Scan to E-Mail-Optionen] Dialogfeld</a>.</p>

        <p><b>[Druck-Optionen...] Taste</b><br>
        Erscheint, wenn Scan to Print ausgewählt wurde. Öffnet das
        <a href='#print-options'>[Scan to Print-Optionen] Dialogfeld</a>.</p>

        <p><b>[Installieren/Deinstallieren...] Taste</b><br>
        Öffnet das <a href='#app-add-remove'>[Anwendung hinzufügen/entfernen] Dialogfeld</a>.</p>

        <div class='see-also'>Siehe auch:
            <ul>
                <li><a href='#settings'>Einstellungsdialogfeld</a></li>
                <li><a href='#app-add-remove'>[Anwendung hinzufügen/entfernen] Dialogfeld</a></li>
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
        Zeigt das [Ordner suchen] Dialogfeld an, in dem Sie einen Ordner für das Speichern
        der Bilder auswählen können.</p>

        <p><b>[Dateinameformat] Taste</b><br>
        Zeigt das <a href='#filename-format'>[Dateinamenformat] Dialogfeld</a> an, in dem das
        Dateinamenformat bestimmt werden kann.</p>

        <p><b>[Datei nach Scan umbenennen] Kontrollkästchen</b><br>
        Wenn markiert, erscheint nach dem Scannen das [Speichern Sie das gescannte Bild als]
        Dialogfeld, in dem Ziel oder Dateiname geändert werden können.</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Der Standard-Speicherordner ist <code>Dokumente\SpeedScanManager</code> im
            Benutzerprofil. Wenn mehrere Dateien erstellt werden, wird dem Dateinamen eine
            Seriennummer angefügt.</p>
        </div>

        <div class='see-also'>Siehe auch:
            <ul>
                <li><a href='#filename-format'>[Dateinamenformat] Dialogfeld</a></li>
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
            <li><b>Grau (umgekehrt)</b> – Dokumente werden immer als Graubilder gespeichert</li>
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
        Fortsetzen des Scans. Die maximale Seitenzahl für eine PDF-Datei beträgt 1.000 Seiten.</p>

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

        <h2>Texterkennung wählen</h2>
        <p><b>[Markierten Text als Schlüsselwort der PDF-Datei hinzufügen] Kontrollkästchen</b><br>
        Führt die Texterkennung für den markierten Bereich aus und erstellt eine PDF-Datei
        mit erkannten Zeichen, die als Schlüsselwörter verwendet werden können.
        Nur verfügbar, wenn PDF als Dateiformat gewählt wurde.</p>

        <p><b>Zielmarkierung</b><br>
        <ul>
            <li><b>Erste markierte Sektion</b> – Texterkennung nur in der ersten markierten Sektion</li>
            <li><b>Alle markierten Sektionen</b> – Text aller markierten Sektionen als Schlüsselwörter</li>
        </ul></p>

        <p><b>[In durchsuchbare PDF konvertieren] Kontrollkästchen</b><br>
        Führt OCR während des Scannens durch und erstellt eine durchsuchbare PDF-Datei.</p>

        <h2>Texterkennungsoptionen</h2>
        <p><b>[Sprache] Auswahlliste</b><br>
        Wählen Sie eine der folgenden Sprachen: Deutsch oder Englisch.</p>

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
                <li><a href='#keyword-marking'>Markieren von Textstellen für PDF-Schlüsselwörter</a></li>
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
        </ul>

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
        Intensität für das Scannen von Schwarzweißbildern, 11 Stufen einstellbar.
        Standard: Normal (Mitte). Nur verfügbar, wenn [Schwarzweiß] als Farbmodus gewählt wurde.</p>

        <p><b>[Einstellung nur für Textdokumente] Kontrollkästchen</b><br>
        Betont den Kontrast der gescannten Bilder. Wählen Sie dieses für zweiseitige
        Schwarzweißdokumente oder Dokumente mit handschriftlichen Texten.</p>

        <p><b>[Automatisches Löschen leerer Seiten zulassen] Kontrollkästchen</b><br>
        Leere Seiten werden automatisch erkannt und aus dem Ausgabebild gelöscht.</p>

        <p><b>[Automatische Korrektur schiefer Zeichen zulassen] Kontrollkästchen</b><br>
        Korrigiert schiefe Zeichen auf einem Dokument (Fehlwinkel bis ±5 Grad).</p>

        <p><b>[Automatische Bilddrehung zulassen] Kontrollkästchen</b><br>
        Dokumente, die seitlich oder kopfüber gescannt wurden, werden in der korrekten
        Richtung ausgegeben.</p>

        <p><b>[Dokumente mit der Vorderseite nach oben einlegen] Kontrollkästchen</b><br>
        Die erste nach oben zeigende Seite wird als erste Seite gescannt.</p>

        <div class='warning'>
            <div class='warning-title'>ACHTUNG</div>
            <p>Wenn [Scan to Print] in der [Anwendung] Auswahlliste gewählt wurde, stehen
            [Automatisches Löschen leerer Seiten] und [Automatische Bilddrehung zulassen]
            nicht zur Verfügung.</p>
        </div>

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
        Nach dem Scannen erscheint ein Dialogfeld zur Kennwortvergabe. Bis zu 16 Zeichen.
        Das Kennwort wird als ""Kennwort zum Öffnen des Dokuments"" verwendet.</p>

        <p><b>[Festgelegtes Kennwort verwenden] Kontrollkästchen</b><br>
        Ein festgelegtes Kennwort wird automatisch für die PDF-Dateien vergeben, ohne
        dass ein Dialogfeld angezeigt wird.</p>

        <div class='see-also'>Siehe auch:
            <ul>
                <li><a href='#filetype-tab'>[Dateiart] Registerkarte</a></li>
                <li><a href='#password-dialog'>[Kennwort vergeben] Dialogfeld</a></li>
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
        [Standard] kann nicht umbenannt werden.</p>

        <p><b>[Löschen] Taste</b> – Löscht das gewählte Profil.
        [Standard] kann nicht gelöscht werden.</p>

        <p><b>[Oben] Taste</b> – Verschiebt das Profil um eine Position nach oben.</p>
        <p><b>[Unten] Taste</b> – Verschiebt das Profil um eine Position nach unten.</p>
        <p><b>[Schließen] Taste</b> – Schließt das Dialogfeld.</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Die Anzeigeposition von [Standard] kann nicht geändert werden.</p>
        </div>
    "));

    private static HelpTopic TopicAppAddRemoveDialog() => new("app-add-remove", "[Anwendung hinzufügen/entfernen] Dialogfeld",
        Wrap("[Anwendung hinzufügen/entfernen] Dialogfeld", @"
        <p>In diesem Dialogfeld können Sie Anwendungen hinzufügen, entfernen oder ändern.
        Bis zu zehn Anwendungen können hinzugefügt werden.</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Die hier getroffenen Änderungen werden unabhängig von der [OK] oder [Abbrechen]
            Taste im Einstellungsdialogfeld übernommen.</p>
        </div>

        <h2>Elemente</h2>
        <p><b>[Hinzugefügt]</b> – Liste der Anwendungen, die der [Anwendung] Auswahlliste hinzugefügt werden können.</p>
        <p><b>[Hinzufügen] Taste</b> – Öffnet das <a href='#app-settings'>[Anwendungseinstellung] Dialogfeld</a>.</p>
        <p><b>[Entfernen] Taste</b> – Entfernt die ausgewählte Anwendung.</p>
        <p><b>[Ändern] Taste</b> – Ändert die Einstellungen einer Anwendung.</p>
        <p><b>[Schließen] Taste</b> – Schließt das Dialogfeld.</p>
    "));

    private static HelpTopic TopicAppSettingsDialog() => new("app-settings", "[Anwendungseinstellung] Dialogfeld",
        Wrap("[Anwendungseinstellung] Dialogfeld", @"
        <p>In diesem Dialogfeld kann das Verzeichnis einer Anwendung und deren Bezeichnung
        festgelegt werden.</p>

        <h2>Elemente</h2>
        <p><b>[Anwendungsverzeichnis]</b> – Zeigt den Speicherort der Anwendung an.</p>
        <p><b>[Durchsuchen] Taste</b> – Öffnet einen Datei-Öffnen-Dialog für die Auswahl
        einer Ausführungsdatei (.exe) oder Verknüpfungsdatei (.lnk).</p>
        <p><b>[Name der Anwendung]</b> – Name, der in der Liste angezeigt wird. Bis zu 62 Zeichen.</p>
        <p><b>[OK] Taste</b> – Einstellungen übernehmen.</p>
        <p><b>[Abbrechen] Taste</b> – Einstellungen verwerfen.</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Alle Anwendungen, die PDF- oder JPEG-Dateien unterstützen, können mit
            SpeedScan Manager verwendet werden.</p>
        </div>
    "));

    private static HelpTopic TopicEmailOptionsDialog() => new("email-options", "[Scan to E-Mail-Optionen] Dialogfeld",
        Wrap("[Scan to E-Mail-Optionen] Dialogfeld", @"
        <p>In diesem Dialogfeld können Sie Einstellungen für die Verwendung von Scan to E-Mail
        konfigurieren.</p>

        <h2>Elemente</h2>
        <p><b>[Vorschau anzeigen] Kontrollkästchen</b><br>
        Markieren, um nach dem Scannen das <a href='#email-dialog'>[Scan to E-Mail] Dialogfeld</a>
        anzuzeigen. Entfernen der Markierung startet direkt ein E-Mail-Programm.</p>

        <p><b>[""Kennwort zum Öffnen des Dokuments"" für die PDF-Dateien einstellen] Kontrollkästchen</b><br>
        Öffnet nach dem Scannen das <a href='#password-dialog'>[Kennwort vergeben] Dialogfeld</a>.</p>

        <p><b>[Gescannte Bilder als Datei speichern] Kontrollkästchen</b><br>
        Speichert gescannte Daten im unter der [Speichern] Registerkarte bestimmten Ordner.</p>

        <p><b>Größe der angefügten Datei</b><br>
        Maximale Dateigröße für Anlagen (1 bis 10 MB).</p>
    "));

    private static HelpTopic TopicEmailDialog() => new("email-dialog", "[Scan to E-Mail] Dialogfeld",
        Wrap("[Scan to E-Mail] Dialogfeld", @"
        <p>In diesem Dialogfeld können Sie einen Dateinamen zum Anfügen an eine E-Mail bestimmen
        und das Bild einer Datei prüfen.</p>

        <h2>Elemente</h2>
        <p><b>Vorschau</b> – Zeigt das gescannte Bild.</p>
        <p><b>Dateiname</b> – Zeigt den Dateinamen an. Kann direkt eingegeben und geändert werden
        (bis zu 100 Zeichen).</p>
        <p><b>[Verlauf] Taste</b> – Zeigt die zehn zuletzt verwendeten Dateinamen an.</p>
        <p><b>[Serialisieren] Taste</b> – Erscheint bei mehreren Dateien. Öffnet das
        <a href='#serialize-dialog'>[Serialisieren] Dialogfeld</a>.</p>
        <p><b>[""Kennwort zum Öffnen des Dokuments"" für die PDF-Dateien einstellen] Kontrollkästchen</b><br>
        Erscheint vor dem Versenden von PDF-Dateien das <a href='#password-dialog'>[Kennwort vergeben] Dialogfeld</a>.</p>
        <p><b>[Gescannte Bilder als Datei speichern] Kontrollkästchen</b></p>
        <p><b>[Diesen Dialog nicht wieder anzeigen] Kontrollkästchen</b></p>
        <p><b>[Anfügen] Taste</b> – Startet das E-Mail-Programm mit den gescannten Dokumenten als Anlage.</p>
        <p><b>[Abbrechen] Taste</b> – Bricht die Einstellungen ab.</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Das als Standardanwendung festgelegte E-Mail-Programm wird verwendet.
            Bis zu 10 Dateien können auf einmal an eine E-Mail angefügt werden.</p>
        </div>
    "));

    private static HelpTopic TopicPrintOptionsDialog() => new("print-options", "[Scan to Print-Optionen] Dialogfeld",
        Wrap("[Scan to Print-Optionen] Dialogfeld", @"
        <p>In diesem Dialogfeld können die Optionen der Funktion Scan to Print eingestellt werden.</p>

        <h2>Elemente</h2>
        <p><b>[Für das Drucken verwendete Daten als Datei sichern] Kontrollkästchen</b><br>
        Speichert gescannte Bilder im unter der [Speichern] Registerkarte bestimmten Ordner.</p>

        <p><b>[""Drucken"" Dialogfeld anzeigen] Kontrollkästchen</b><br>
        Markieren, um nach dem Scannen das <a href='#print-dialog'>[Scan to Print] Dialogfeld</a>
        anzuzeigen. Entfernen der Markierung verwendet den Standarddrucker direkt.</p>
    "));

    private static HelpTopic TopicPrintDialog() => new("print-dialog", "[Scan to Print] Dialogfeld",
        Wrap("[Scan to Print] Dialogfeld", @"
        <p>In diesem Dialogfeld können Sie einen Drucker und die Anzahl der zu druckenden Kopien
        bestimmen.</p>

        <h2>Elemente</h2>
        <p><b>Vorschau</b> – Das Bild der aktuell ausgewählten Datei wird angezeigt.</p>
        <p><b>[Name] Auswahlliste</b> – Zeigt die im Betriebssystem erkannten Drucker.</p>
        <p><b>[Eigenschaften] Taste</b> – Zeigt die Eigenschaften des ausgewählten Druckers an.</p>

        <h3>Druckeinstellungen</h3>
        <p><b>[Kopien]</b> – Anzahl der Kopien (1 bis 99).</p>
        <p><b>[Gleiche Größe/Verkleinern] Auswahlliste</b><br>
        <ul>
            <li><b>Auf Papiergröße verkleinern</b> – Bild wird verkleinert, um vollständig auf das Blatt zu passen</li>
            <li><b>Gleiche Größe</b> – Bilder in Originalgröße (größere Bilder werden abgeschnitten)</li>
        </ul></p>
        <p><b>[Druckqualität] Auswahlliste</b> – Normal (150 dpi) oder Hoch (Scan-Auflösung).</p>
        <p><b>[Automatische Bilddrehung zulassen] Kontrollkästchen</b></p>
        <p><b>[Bild zentrieren und drucken] Kontrollkästchen</b></p>
        <p><b>[Für das Drucken verwendete Daten als Datei sichern] Kontrollkästchen</b></p>
        <p><b>[Diesen Dialog nicht wieder anzeigen] Kontrollkästchen</b></p>
        <p><b>[Drucken] Taste</b> – Druckt das Bild gemäß den Einstellungen.</p>
        <p><b>[Abbrechen] Taste</b> – Bricht ab.</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Bis zu 100 Dateien können auf einmal gedruckt werden.</p>
        </div>
    "));

    private static HelpTopic TopicScanToFolderDialog() => new("scan-folder-dialog", "[Scan to Folder] Dialogfeld",
        Wrap("[Scan to Folder] Dialogfeld", @"
        <p>In diesem Dialogfeld können die Bilder der gescannten Dokumente eingesehen und für
        das Speichern Dateinamen und Zielordner bestimmt werden.</p>

        <h2>Elemente</h2>
        <p><b>Vorschau</b> – Zeigt das gescannte Dokumentenbild.</p>
        <p><b>Dateiname</b> – Zeigt den Dateinamen an. Kann geändert werden (bis zu 100 Zeichen).</p>
        <p><b>[Verlauf] Taste</b> – Zeigt die zehn zuletzt verwendeten Dateinamen an.</p>
        <p><b>[Serialisieren] Taste</b> – Bei mehreren Dateien. Öffnet das
        <a href='#serialize-dialog'>[Serialisieren] Dialogfeld</a>.</p>
        <p><b>Speichern in</b> – Zeigt den Zielordner an. Kann direkt eingegeben oder über
        [Durchsuchen] geändert werden.</p>
        <p><b>[Verlauf] Taste (Ordner)</b> – Zeigt die zehn zuletzt verwendeten Ordnerpfade an.</p>
        <p><b>[Durchsuchen] Taste</b> – Öffnet einen Dialog zur Ordnerauswahl.</p>
        <p><b>[Speichern] Taste</b> – Speichert das gescannte Bild.</p>
        <p><b>[Abbrechen] Taste</b> – Bricht ab.</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Bis zu 100 Dateien können auf einmal gespeichert werden.</p>
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

    private static HelpTopic TopicSerializeDialog() => new("serialize-dialog", "[Serialisieren] Dialogfeld",
        Wrap("[Serialisieren] Dialogfeld", @"
        <p>Wenn Sie mehrere Dateien erstellen, können Sie in diesem Dialogfeld das Format
        bestimmen, in dem Seriennummern an das Ende von Dateinamen angefügt werden sollen.</p>

        <h2>Elemente</h2>
        <p><b>Dateiname</b> – Zeigt den festgelegten Dateinamen an. Kann direkt eingegeben werden.</p>
        <p><b>[Seriennummer] Auswahlliste</b> – Stellenanzahl der Seriennummer (bis zu 6 Stellen).</p>
        <p>Beispiel: <code>Dateiname_Seriennummer</code> (z. B. Scan_001, Scan_002, ...)</p>
        <p><b>[OK] Taste</b> – Einstellungen übernehmen.</p>
        <p><b>[Abbrechen] Taste</b> – Einstellungen verwerfen.</p>
    "));

    private static HelpTopic TopicPasswordDialog() => new("password-dialog", "[Kennwort vergeben] Dialogfeld",
        Wrap("[Kennwort vergeben] Dialogfeld", @"
        <p>In diesem Dialogfeld können Sie ein Kennwort für die PDF-Datei bestimmen.</p>

        <h2>Elemente</h2>
        <p><b>Kennwort</b> – Geben Sie ein ""Kennwort zum Öffnen des Dokuments"" ein (bis zu 16 Zeichen).</p>
        <p><b>Kennwort bestätigen</b> – Geben Sie das Kennwort erneut ein.</p>
        <p><b>[OK] Taste</b> – Einstellungen übernehmen. Das E-Mail-Programm wird gestartet.</p>
        <p><b>[Abbrechen] Taste</b> – Bricht ab.</p>

        <h2>Zulässige Zeichen</h2>
        <ul>
            <li>Alphanumerische Zeichen: A-Z, a-z, 0-9</li>
            <li>Symbole: ! "" # $ % & ' ( ) * + , - . / : ; &lt; = &gt; ? @ [ \ ] ^ _ ` { | } ~</li>
        </ul>
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

        <h2>Grundeinstellungen von [Standard]</h2>
        <table>
            <tr><th>Einstellung</th><th>Grundeinstellung</th></tr>
            <tr><td>Anwendung</td><td>Scan to Folder</td></tr>
            <tr><td>Bildqualität</td><td>Automatisch</td></tr>
            <tr><td>Farbmodus</td><td>Automatische Farberkennung</td></tr>
            <tr><td>Scan-Seite</td><td>Automatisch</td></tr>
            <tr><td>Dateiformat</td><td>PDF (*.pdf)</td></tr>
            <tr><td>Papiergröße</td><td>Automatische Erkennung</td></tr>
            <tr><td>Komprimierungsrate</td><td>Stufe 3 von 5</td></tr>
        </table>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Wenn der angeschlossene Scanner über kein Flachbett verfügt, wird für [Scan-Seite]
            [Duplex Scan] übernommen. Steht Duplex Scan nicht zur Verfügung, wird [Simplex Scan] verwendet.</p>
        </div>
    "));

    // ═══════════════════════════════════════════════════════════════════════
    // Fehlerbehebung
    // ═══════════════════════════════════════════════════════════════════════

    private static HelpTopic TopicScanErrorDialog() => new("scan-error", "Scanfehlerdialogfeld",
        Wrap("Scanfehlerdialogfeld", @"
        <p>Tritt beim Scannen ein Fehler auf (z. B. Papierstau), erscheint eine Fehlermeldung.</p>

        <h2>Elemente</h2>
        <p><b>[Scan fortsetzen] Taste</b><br>
        Setzt das Scannen fort. Vergewissern Sie sich, dass kein Dokument mehr im ADF vorhanden ist.
        Legen Sie die noch zu scannenden Dokumente ein und klicken Sie auf [Scan fortsetzen]
        oder drücken Sie die Scan-Taste des Scanners.</p>

        <p>Verfügbar für folgende Fehler:</p>
        <ul>
            <li>Papierstau</li>
            <li>Kein Papier im Scanner</li>
            <li>Inkorrekte Papiergröße</li>
            <li>ADF-Abdeckung geöffnet</li>
        </ul>

        <p><b>[Beenden] Taste</b><br>
        Beendet das Scannen. Klicken Sie [Ja], um die gescannten Bilder zu löschen, oder
        [Nein], um die erstellten Bilder zu speichern und den Scanvorgang zu beenden.</p>
    "));

    private static HelpTopic TopicMultiFeedDialog() => new("multifeed-dialog", "Mehrfacheinzugserkennungsdialogfeld",
        Wrap("Mehrfacheinzugserkennungsdialogfeld", @"
        <p>Wenn während des Scannens ein Mehrfacheinzug erkannt wurde, wird der Scanvorgang
        unterbrochen und dieses Dialogfeld angezeigt.</p>

        <h2>Elemente</h2>
        <p><b>[Vorderseite] / [Rückseite] Taste</b> – Wechselt die Seitenansicht der Miniaturansicht.</p>
        <p><b>Miniaturansicht Liste</b> – Zeigt die gescannten Bilder in der Reihenfolge des Scannens.
        Miniaturansichten mit Mehrfacheinzug haben einen rosa Hintergrund.</p>

        <div class='note'>
            <div class='note-title'>HINWEIS</div>
            <p>Maximal 11 Miniaturansichten werden angezeigt. Für die angezeigten Miniaturansichten
            sind die Scaneinstellungen noch nicht übernommen.</p>
        </div>

        <p><b>[Behalten] Taste</b> – Behält die Bilder mit Mehrfacheinzug. Scannen kann fortgesetzt
        oder abgebrochen werden.</p>
        <p><b>[Aussondern] Taste</b> – Löscht die Bilder mit Mehrfacheinzug.</p>
        <p><b>[Scan fortsetzen] Taste</b> – Startet das Scannen erneut.</p>
        <p><b>[Beenden] Taste</b> – Beendet das Scannen.</p>

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
            <li>Wählen Sie [Behalten] und klicken Sie auf [Scan fortsetzen].</li>
        </ol>
        <p>Für Umschläge oder Dokumente mit Briefmarken/Klebenotizen/Fotos: Wählen Sie [Behalten]
        und klicken Sie auf [Scan fortsetzen] oder drücken Sie die Scan-Taste.</p>

        <h2>Dokumente wurden nicht in der Reihenfolge des Originaldokuments gescannt</h2>
        <ol>
            <li>Öffnen Sie den ADF, entnehmen Sie die Dokumente und richten Sie die Blattkanten erneut aus.</li>
            <li>Entnehmen Sie die erneut zu scannenden Dokumente aus dem Ausgabefach und legen Sie diese erneut ein.</li>
            <li>Platzieren Sie die in Schritt 1 entfernten Dokumente über den verbliebenen Dokumenten.</li>
            <li>Wählen Sie [Aussondern] im Mehrfacheinzugserkennungsdialogfeld.</li>
            <li>Klicken Sie auf [Scan fortsetzen] oder drücken Sie die Scan-Taste.</li>
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
            "<Systemfehlerdetails> (0xXXXXXXXX)",
            "Acrobat(R) startet nicht, wenn JPEG als Dateiformat eingestellt ist.",
            "Alle Seiten wurden als leer erkannt. Überprüfen Sie, ob der Scan korrekt ausgeführt wurde.",
            "Bildinformationen von xxx konnten nicht erhalten werden.",
            "Das Kennwort ist inkorrekt. Geben Sie das Kennwort bitte korrekt ein.",
            "Das Versenden der E-Mail ist fehlgeschlagen oder wurde unterbrochen. (n)",
            "Datei konnte nicht geöffnet werden.",
            "Der ausgewählte Zielordner ist ungültig. Überprüfen Sie den Zielordner.",
            "Der Dateiname ist ungültig, da folgende Zeichen enthalten sind: \\ / : * ? \" < > |",
            "Der Dateiname kann nicht mit folgenden Zeichen beginnen: \\ / : , ; * ? \" < > |",
            "Der eingegebene Dateiname ist ungültig. Bitte überprüfen Sie den Dateinamen.",
            "Der gleiche Dateiname (xxx) wurde ausgewählt. Überprüfen Sie den Dateinamen auf dessen Korrektheit.",
            "Der Name der serialisierten Datei enthält mehr als n Zeichen. Bestimmen Sie einen kürzeren Namen.",
            "Der Scanner ist ausgeschaltet.",
            "Der Scanner kann nicht verwendet werden. Prüfen Sie, ob der Scanner von anderen Anwendungen verwendet wird.",
            "Der von Ihnen angegebene Dateiname (xxx) existiert bereits.",
            "Die Anzahl der verarbeitbaren Seiten überschreitet den Schwellwert (n Seiten). Die Verarbeitung wurde gestoppt.",
            "Die Dateien wurden erfolgreich gespeichert.",
            "Die Gesamtgröße der angefügten Dateien hat den Schwellenwert überschritten. Möchten Sie den Vorgang fortsetzen?",
            "Die Scannerabdeckung ist offen.",
            "Die Sensoren sind verschmutzt.",
            "Druck fehlgeschlagen.",
            "Ein überlappendes Dokument wurde erkannt.",
            "Ein interner Fehler ist aufgetreten. (n)",
            "Ein nicht der angegebenen Größe entsprechendes Dokument wurde gescannt oder mehrere Blätter wurden gleichzeitig eingezogen.",
            "Eine Verarbeitung ist nicht möglich, da der Ordnername folgende Zeichen enthält: * ? \" < > |",
            "Es ist kein Papier im Scanner eingelegt.",
            "Es sind mehrere Scanner-Geräte mit dem PC verbunden.",
            "Fehler bei Datenübertragung aufgetreten.",
            "Fehler beim Start der gewählten Anwendung.",
            "Fehler im optischen System.",
            "Fehler während Komprimierung aufgetreten.",
            "Hardwarefehler aufgetreten.",
            "Initialisierung des Passwortmoduls fehlgeschlagen.",
            "Kein Drucker verfügbar.",
            "Keine Verbindung zum Scanner. (n) Prüfen Sie, ob der Scanner eingeschaltet oder mit dem Netzwerk verbunden ist.",
            "Kommunikation mit dem Scanner fehlgeschlagen (empfange) / Kommunikation mit dem Scanner fehlgeschlagen (sende)",
            "Möchten Sie das \"Speichern\" abbrechen?",
            "Möchten Sie diese Operation ohne die Vergabe eines \"Kennwort zum Öffnen des Dokuments\" für die PDF-Dateien fortsetzen?",
            "Nicht genügend Speicher vorhanden.",
            "Papierstau.",
            "PDF-Datei hat die maximale Größe erreicht (1.000 Seiten).",
            "PDF-Datei konnte nicht erstellt werden.",
            "Problem im Transportmechanismus des Scanners.",
            "Scan fehlgeschlagen. Der für diesen Vorgang erforderliche Speicher oder benötigte Ressourcen sind eventuell unzureichend.",
            "Scanner wird von einem anderen Benutzer bzw. Programm verwendet.",
            "Scanvorgang durch Benutzer abgebrochen.",
            "Speichern der Bilder fehlgeschlagen.",
            "Texterkennung fehlgeschlagen.",
            "Über n Zeichen. Verarbeitung nicht möglich.",
            "Unerwarteter Fehler aufgetreten. (n)",
            "Ungültiger Ordner zum Speichern ausgewählt.",
            "Ungenügender Systemspeicher. Verarbeitung nicht möglich.",
            "Verarbeitung wegen unzureichendem Festplattenspeicher nicht möglich.",
            "Während der Verarbeitung des Trägerblatts ist ein Fehler aufgetreten.",
            "Wechsel des Einzugsmodus erkannt.",
            "Zeitüberschreitung während Kommunikation.",
            "Zugriff auf xxx nicht möglich. Verarbeitung nicht möglich.",
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
            <tr><td>PDF</td><td>Portable Document Format</td></tr>
            <tr><td>JPEG</td><td>Joint Photographic Experts Group (Bildformat)</td></tr>
            <tr><td>PNG</td><td>Portable Network Graphics (Bildformat)</td></tr>
            <tr><td> dpi</td><td>dots per inch (Punkte pro Zoll)</td></tr>
            <tr><td>USB</td><td>Universal Serial Bus</td></tr>
            <tr><td>TWAIN</td><td>Standard-Protokoll für die Kommunikation zwischen Scanner und Software</td></tr>
            <tr><td>S&W</td><td>Schwarz und Weiß</td></tr>
        </table>
    "));

    private static HelpTopic TopicKeywordMarking() => new("keyword-marking", "Markieren von Textstellen für PDF-Schlüsselwörter",
        Wrap("Markieren von Textstellen für PDF-Schlüsselwörter", @"
        <p>Zeichenfolgen wie Titel von Schwarzweißdokumenten können als Schlüsselwörter bestimmt
        und für die Suche nach PDF-Dateien verwendet werden.</p>

        <p>Markieren Sie eine Zeichenfolge, die als Schlüsselwort bestimmt werden soll, mit einem
        wasserlöslichen Textmarker, so dass diese Zeichenfolge vollständig bedeckt ist.</p>

        <h2>Markierungsrichtlinien</h2>
        <ul>
            <li>Alle herkömmlichen Textmarker können verwendet werden (empfohlen: Rosa, Gelb, Blau, Grün)</li>
            <li>Markieren Sie gerade</li>
            <li>Markierte Bereiche sollten innerhalb folgender Abmessungen liegen:
                <ul>
                    <li>Minimum: 3 mm (kurze Seite) × 10 mm (lange Seite)</li>
                    <li>Maximum: 20 mm (kurze Seite) × 150 mm (lange Seite)</li>
                </ul>
            </li>
            <li>Verwenden Sie pro Seite nur eine Farbe</li>
            <li>Markieren Sie einen Textabschnitt so, dass dieser vollständig hervorgehoben ist</li>
            <li>Bis zu zehn Textstellen können pro Seite markiert werden</li>
        </ul>

        <div class='warning'>
            <div class='warning-title'>ACHTUNG</div>
            <ul>
                <li>Für Farbdokumente (Kataloge, Broschüren) können markierte Sektionen nicht erkannt werden</li>
                <li>Dokumente mit mehreren Farben können nicht erkannt werden</li>
                <li>Zwischen zwei markierten Textstellen sollte ein Leerraum von mindestens 5 mm verbleiben</li>
            </ul>
        </div>

        <div class='see-also'>Siehe auch:
            <ul>
                <li><a href='#filetype-tab'>[Dateiart] Registerkarte</a></li>
            </ul>
        </div>
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
