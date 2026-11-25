using System.Text;

namespace SpeedScanManager;

/// <summary>
/// One help topic: a group (with <see cref="Children"/>) or a page (with <see cref="Html"/>,
/// the body fragment below the title). Pages are rendered by <see cref="HelpRenderer"/>.
/// </summary>
internal class HelpTopic
{
    public string Id { get; }
    public string Title { get; private set; }
    /// <summary>One sentence: lead text below the title and description on cards / in lists.</summary>
    public string Summary { get; private set; }
    /// <summary>Body HTML of a page (empty for groups). Pictures as <c>help-img:NAME</c>.</summary>
    public string Html { get; private set; }
    public Glyph Glyph { get; init; } = Glyph.None;
    /// <summary>Topic ids shown as "Passende Themen" below the page.</summary>
    public string[] Related { get; init; } = Array.Empty<string>();
    public List<HelpTopic> Children { get; } = new();
    public HelpTopic? Parent { get; private set; }

    public HelpTopic Root => Parent?.Root ?? this;

    /// <summary>Page.</summary>
    public HelpTopic(string id, string title, string summary, string html)
    {
        Id = id;
        Title = title;
        Summary = summary;
        Html = html;
    }

    /// <summary>Group (no content of its own, only children).</summary>
    public HelpTopic(string id, string title, string summary, Glyph glyph, params HelpTopic[] children)
    {
        Id = id;
        Title = title;
        Summary = summary;
        Html = "";
        Glyph = glyph;
        foreach (var child in children)
        {
            child.Parent = this;
            Children.Add(child);
        }
    }

    /// <summary>Replaces the German texts with a translation (see <see cref="L.LoadHelp"/>).</summary>
    public void Localize(Dictionary<string, L.HelpText> texts)
    {
        if (texts.TryGetValue(Id, out var t))
        {
            if (t.Title.Length > 0) Title = t.Title;
            if (t.Summary.Length > 0) Summary = t.Summary;
            if (t.Html.Length > 0 && Html.Length > 0) Html = t.Html;
        }
        foreach (var child in Children)
            child.Localize(texts);
    }

    public HelpTopic? Find(string id)
    {
        if (Id == id) return this;
        foreach (var child in Children)
            if (child.Find(id) is { } found) return found;
        return null;
    }
}

/// <summary>
/// The help topics of SpeedScan Manager (German, "Sie" form, task oriented). The text describes
/// the actual behaviour of the program; pictures are generated screenshots (see
/// <see cref="HelpRenderer"/>). Topic ids are opened directly by the windows ("?" button) and must
/// stay stable: root, settings, app-tab, save-tab, scanmode-tab, filetype-tab, paper-tab,
/// filesize-tab, app-manage, custom-size-dialog, filename-format, multifeed-dialog,
/// postscan-media, postscan-save, scanmode-options, driver-info, version-info.
/// </summary>
internal static class HelpContent
{
    /// <summary>Questions on the start page (topic id, question).</summary>
    public static readonly (string Id, string Question)[] StartQuestions =
    {
        ("scan-quick", L.T("Wie scanne ich ein Dokument?")),
        ("save-tab", L.T("Wo werden meine Scans gespeichert?")),
        ("filename-format", L.T("Wie ändere ich den Dateinamen?")),
        ("filetype-tab", L.T("Wie erstelle ich eine durchsuchbare PDF?")),
        ("faq", L.T("Der Scanner wird nicht gefunden – was tun?")),
    };

    /// <summary>Builds the complete help topic tree (in the current language).</summary>
    public static HelpTopic BuildTree()
    {
        var root = BuildGerman();
        if (L.LoadHelp() is { } texts)
            root.Localize(texts);
        return root;
    }

    /// <summary>The help as written (German source).</summary>
    public static HelpTopic BuildGerman()
    {
        return new HelpTopic("root", "SpeedScan Manager Hilfe",
            "Scannen Sie Dokumente auf Knopfdruck und entscheiden Sie danach, wohin sie sollen – in einen Ordner, per E-Mail, auf den Drucker oder in ein Office-Dokument.",
            Glyph.Question,
            new HelpTopic("getting-started", "Erste Schritte",
                "Was das Programm kann, wo Sie es finden und wie Sie Ihren Scanner verbinden.", Glyph.Star,
                TopicOverview(),
                TopicStarting(),
                TopicScannerConnection(),
                TopicScannerSelection(),
                TopicSystemRequirements()
            ),
            new HelpTopic("scanning", "Scannen",
                "Dokumente scannen, Scan-Taste nutzen, lange Seiten und das Trägerblatt.", Glyph.Scanner,
                TopicScanQuickMenu(),
                TopicScanButton(),
                TopicScanWithoutQuickMenu(),
                TopicScanDisplay(),
                TopicScanContinue(),
                TopicScanLongPages(),
                TopicScanCarrierSheet()
            ),
            new HelpTopic("settings-group", "Einstellungen",
                "Das Einstellungsfenster und alle seine Seiten Schritt für Schritt erklärt.", Glyph.Gear,
                TopicSettingsDialog(),
                TopicApplicationTab(),
                TopicSaveTab(),
                TopicScanModeTab(),
                TopicFileTypeTab(),
                TopicPaperTab(),
                TopicFileSizeTab()
            ),
            new HelpTopic("dialogs", "Fenster und Dialoge",
                "Quick-Menü, Speichern-Fenster und alle weiteren Dialoge mit Bild.", Glyph.Apps,
                TopicPostScanMediaDialog(),
                TopicPostScanSaveDialog(),
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
                TopicScannerDriverInfoDialog()
            ),
            new HelpTopic("profiles", "Profile",
                "Feste Einstellungen für wiederkehrende Aufgaben speichern und auswählen.", Glyph.User,
                TopicProfileManagement(),
                TopicDefaultSettings()
            ),
            new HelpTopic("troubleshooting", "Probleme lösen",
                "Häufige Probleme, Mehrfacheinzug und alle Meldungen des Programms.", Glyph.Sliders,
                TopicFaq(),
                TopicMultiFeedDialog(),
                TopicMultiFeedMeasures(),
                TopicMessageList()
            ),
            new HelpTopic("reference", "Anhang",
                "Abkürzungen und Fachbegriffe kurz erklärt.", Glyph.FileType,
                TopicAbbreviations()
            )
        );
    }

    /// <summary>Flattens the topic tree into a dictionary for quick ID lookup.</summary>
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

    // ═══════════════════════════════════════════════════════════════════════
    // Building blocks
    // ═══════════════════════════════════════════════════════════════════════

    private static HelpTopic Page(string id, string title, string summary, string html, params string[] related)
        => new(id, title, summary, html) { Related = related };

    private static string Steps(params string[] steps)
    {
        var sb = new StringBuilder("<ol class='steps'>");
        foreach (var s in steps) sb.Append("<li>").Append(s).Append("</li>");
        return sb.Append("</ol>").ToString();
    }

    private static string Box(string kind, string title, string html) =>
        $"<div class='box {kind}'><div class='box-title'>{title}</div><p>{html}</p></div>";

    private static string Tip(string html) => Box("tip", "Tipp", html);
    private static string Note(string html) => Box("note", "Hinweis", html);
    private static string Warn(string html) => Box("warn", "Achtung", html);

    /// <summary>Screenshot (light/dark variant chosen at render time) with caption.</summary>
    private static string Shot(string image, string caption) =>
        $"<div class='shot'><img src='help-img:{image}' alt='{caption}'><div class='caption'>{caption}</div></div>";

    /// <summary>Explanation of the numbered markers in a screenshot.</summary>
    private static string Legend(params string[] items)
    {
        var sb = new StringBuilder("<table class='legend'>");
        for (int i = 0; i < items.Length; i++)
            sb.Append("<tr><td><span class='num'>").Append(i + 1).Append("</span></td><td>").Append(items[i]).Append("</td></tr>");
        return sb.Append("</table>").ToString();
    }

    /// <summary>Card with "element – explanation" rows.</summary>
    private static string Items(params (string Term, string Text)[] rows)
    {
        var sb = new StringBuilder("<table class='items'>");
        foreach (var (term, text) in rows)
            sb.Append("<tr><td class='term'>").Append(term).Append("</td><td>").Append(text).Append("</td></tr>");
        return sb.Append("</table>").ToString();
    }

    /// <summary>Table with a header row.</summary>
    private static string Grid(string[] head, params string[][] rows)
    {
        var sb = new StringBuilder("<table class='grid'><tr>");
        foreach (var h in head) sb.Append("<th>").Append(h).Append("</th>");
        sb.Append("</tr>");
        foreach (var row in rows)
        {
            sb.Append("<tr>");
            foreach (var cell in row) sb.Append("<td>").Append(cell).Append("</td>");
            sb.Append("</tr>");
        }
        return sb.Append("</table>").ToString();
    }

    private static string QA(string question, string answer) =>
        $"<div class='qa'><h3>{question}</h3>{answer}</div>";

    private static string Icon(string name, string alt) =>
        $"<img src='help-img:{name}' width='16' height='16' alt='{alt}' style='vertical-align:-3px'>";

    // ═══════════════════════════════════════════════════════════════════════
    // Erste Schritte
    // ═══════════════════════════════════════════════════════════════════════

    private static HelpTopic TopicOverview() => Page("overview", "Was ist SpeedScan Manager?",
        "SpeedScan Manager steuert Ihren TWAIN-Scanner und erledigt nach dem Scan die Arbeit für Sie.",
        @"
        <p>Das Programm läuft unauffällig im Infobereich der Taskleiste. Sie legen ein Dokument ein, drücken die
        Scan-Taste – und wählen danach, was mit dem Scan passieren soll.</p>

        <h2>Das können Sie damit machen</h2>
        " + Items(
            ("In Ordner speichern", "Als PDF, JPEG oder PNG in einem Ordner Ihrer Wahl ablegen."),
            ("Per E-Mail versenden", "Ihr E-Mail-Programm öffnet sich mit dem Scan als Anhang."),
            ("Drucken", "Die Seiten direkt auf einem Drucker ausgeben (Kopierer-Ersatz)."),
            ("Word, Excel, PowerPoint", "Die Seiten als Bilder in ein neues Office-Dokument einfügen."),
            ("Bilderordner", "Die Seiten als JPEG-Bilder in <code>Bilder\\SpeedScanManager</code> speichern."),
            ("Mit PDF-Programm öffnen", "Den Scan im Programm öffnen, das in Windows für die Datei eingestellt ist."),
            ("Eigene Programme", "Bis zu zehn eigene Programme mit dem Scan starten.")) + @"

        <h2>Das erledigt das Programm automatisch</h2>
        <ul>
            <li><b>Texterkennung (OCR)</b> – macht PDFs durchsuchbar.</li>
            <li><b>Leere Seiten entfernen</b>, <b>schiefe Seiten geraderücken</b> und <b>Seiten richtig drehen</b>.</li>
            <li><b>Farbe erkennen</b> – jede Seite wird als Farbe, Grau oder Schwarzweiß gespeichert.</li>
            <li><b>Trägerblatt</b> – zwei Hälften eines gefalteten A3-Blatts zu einem Bild zusammensetzen.</li>
        </ul>
        " + Tip("Am schnellsten starten Sie mit dem <a href='#scan-quick'>Quick-Menü</a>: Es ist bereits eingeschaltet und fragt nach jedem Scan, wohin er soll."),
        "scan-quick", "starting", "settings");

    private static HelpTopic TopicStarting() => Page("starting", "Tray-Symbol und Menü",
        "SpeedScan Manager hat kein großes Hauptfenster – alles erreichen Sie über das Symbol in der Taskleiste.",
        @"
        <p>Nach dem Start erscheint das Programmsymbol im Infobereich der Taskleiste (unten rechts, ggf. hinter dem
        Pfeil <b>˄</b>). Klicken Sie darauf, um das Menü zu öffnen.</p>
        " + Shot("tray_menu", "Das Tray-Menü. Oben sehen Sie, ob der Scanner bereit ist.") + @"

        <h2>Das Symbol zeigt den Status</h2>
        " + Items(
            (Icon("trayicon_connected", "Bereit") + "&nbsp; Normal", "Der Scanner ist verbunden und bereit."),
            (Icon("trayicon_scanning", "Scannt") + "&nbsp; Mit orangem Punkt", "Ein Scan läuft oder wird gerade verarbeitet."),
            (Icon("trayicon_disconnected", "Getrennt") + "&nbsp; Blass mit rotem Strich", "Kein Scanner verbunden oder der Scanner ist aus.")) + @"
        <p>Halten Sie die Maus über das Symbol, sehen Sie den Namen des Scanners – während eines Scans auch die Anzahl der
        bisher gescannten Seiten.</p>

        <h2>Klicken</h2>
        " + Items(
            ("Linksklick oder Rechtsklick", "Öffnet das Menü."),
            ("Doppelklick", "Öffnet die <a href='#settings'>Einstellungen</a>.")) + @"

        <h2>Die Befehle im Menü</h2>
        " + Items(
            ("Scan", "Startet einen Scan mit den aktuellen Einstellungen. Nicht verfügbar, solange der Scanner getrennt ist oder ein Scan läuft."),
            ("Scan abbrechen", "Nur sichtbar, während ein Scan läuft."),
            ("Öffnen", "Öffnet das <a href='#settings'>Einstellungsfenster</a>."),
            ("Scanner auswählen...", "Den Scanner wählen, wenn mehrere angeschlossen sind (<a href='#scanner-selection'>mehr</a>)."),
            ("Scan-Taste einrichten...", "Richtet die Taste am Scanner (erneut) ein (<a href='#scan-button'>mehr</a>)."),
            ("Profilverwaltung...", "Profile umbenennen, löschen und sortieren."),
            ("Scan-Ergebnis anzeigen", "Zeigt die zuletzt gespeicherte Datei im Explorer (erst nach einem Scan)."),
            ("Hilfe", "Diese Hilfe und die Versionsinformationen."),
            ("Beenden", "Beendet das Programm. Läuft gerade ein Scan, werden Sie vorher gefragt; wird er gerade gespeichert, beendet sich das Programm erst danach.")) + @"

        <h2>Mit Windows starten</h2>
        <p>Der Schalter <b>Mit Windows starten</b> auf der Seite <b>Allgemein</b> der Einstellungen legt fest,
        ob das Programm automatisch startet. Nach einer Neuinstallation ist er eingeschaltet; nach einem Update von Version 0.1.x
        bleibt er aus, bis Sie ihn einschalten.</p>
        " + Note("Das Programm läuft immer nur einmal. Starten Sie es ein zweites Mal, öffnen sich stattdessen die Einstellungen. " +
                 "Einstellungen und Profile liegen im Ordner <code>%AppData%\\SpeedScanManager</code>."),
        "scan-quick", "settings", "scan-button");

    private static HelpTopic TopicScannerConnection() => Page("scanner-connection", "Scanner verbinden",
        "USB- und Netzwerk-Scanner werden automatisch erkannt, sobald sie eingeschaltet sind.",
        Steps(
            "Installieren Sie den TWAIN-Treiber des Scanners (vom Hersteller).",
            "Schalten Sie den Scanner ein und verbinden Sie ihn per USB oder Netzwerk mit dem PC.",
            "Warten Sie einen Moment: Das Tray-Symbol wechselt von " + Icon("trayicon_disconnected", "getrennt") + " zu " +
            Icon("trayicon_connected", "bereit") + ", und oben im Menü erscheint der Name des Scanners mit grünem Punkt.") + @"

        <h2>Was im Hintergrund passiert</h2>
        <p>Wird ein Gerät angeschlossen oder getrennt, prüft das Programm den Scanner sofort. Nach dem Programmstart fragt es den
        Scanner nach wenigen Sekunden zum ersten Mal ab. Ist der Scanner getrennt, erscheint einmalig die Sprechblase
        „Der Scanner ist nicht angeschlossen oder ausgeschaltet.“</p>
        " + Tip("Schließen Sie möglichst nur einen Scanner an. Sind mehrere angeschlossen, legen Sie unter " +
                "<a href='#scanner-selection'>Scanner auswählen</a> fest, welcher verwendet wird.") +
        Warn("Wird der Scanner nicht erkannt, prüfen Sie Kabel bzw. Netzwerk, ob er eingeschaltet ist und ob der TWAIN-Treiber installiert ist. " +
             "Weitere Hilfe: <a href='#faq'>Häufige Probleme</a>."),
        "scanner-selection", "sysreq", "faq");

    private static HelpTopic TopicScannerSelection() => Page("scanner-selection", "Scanner auswählen",
        "Legen Sie fest, welcher Scanner verwendet wird, wenn mehrere angeschlossen sind.",
        Steps(
            "Klicken Sie auf das Tray-Symbol und wählen Sie <b>Scanner auswählen...</b> (nicht während eines Scans).",
            "Wählen Sie den Scanner in der Liste. <b>(USB)</b> bzw. <b>(Netzwerk)</b> zeigt die Verbindung; USB-Scanner stehen oben.",
            "Klicken Sie auf <b>OK</b>. Die Auswahl gilt auch nach einem Neustart.") +
        Shot("dlg_scanner", "Das Fenster „Scanner auswählen“") + @"
        <p>Der erste Eintrag <b>(Automatisch)</b> überlässt die Wahl dem Programm. <b>Abbrechen</b> oder das Schließen-Symbol
        schließen das Fenster ohne Änderung.</p>
        " + Note("Die Liste aktualisiert sich von selbst, wenn ein Scanner hinzukommt. Wird keiner gefunden, steht dort „Kein Scanner gefunden“ – " +
                 "<b>(Automatisch)</b> können Sie trotzdem bestätigen. Ist der gewählte Scanner nicht erreichbar, zeigt das Tray-Symbol „Auswahl: ... (offline)“."),
        "scanner-connection");

    private static HelpTopic TopicSystemRequirements() => Page("sysreq", "Systemanforderungen",
        "Was Ihr PC und Ihr Scanner mitbringen müssen.",
        Items(
            ("Betriebssystem", "Windows 10 oder Windows 11 (64-Bit)."),
            ("Scanner", "TWAIN-fähiger Scanner (USB oder Netzwerk) mit installiertem TWAIN-Treiber. Da SpeedScan Manager ein 32-Bit-Programm ist, muss der Treiber eine 32-Bit-TWAIN-Quelle mitbringen."),
            (".NET", "Beim Installationspaket ist keine separate .NET-Installation nötig."),
            ("Scan to E-Mail", "Ein E-Mail-Programm mit MAPI-Unterstützung (z. B. Outlook)."),
            ("Scan to Print", "Ein installierter Drucker."),
            ("Word, Excel, PowerPoint", "Zum Öffnen der erstellten Dateien – erstellt werden sie auch ohne Office.")) +
        Note("Mitgeliefert sind die Sprachdaten für die Texterkennung in Deutsch und Englisch sowie die Daten für die automatische Bilddrehung. " +
             "Weitere Sprachen: siehe <a href='#filetype-tab'>Seite „Dateiart“</a>."),
        "scanner-connection");

    // ═══════════════════════════════════════════════════════════════════════
    // Scannen
    // ═══════════════════════════════════════════════════════════════════════

    private static HelpTopic TopicScanQuickMenu() => Page("scan-quick", "So scannen Sie ein Dokument",
        "Mit dem Quick-Menü (Grundeinstellung) scannen Sie zuerst und entscheiden danach, wohin der Scan soll.",
        Steps(
            "Legen Sie die Seiten in den Scanner ein.",
            "Drücken Sie die <b>Scan-Taste am Scanner</b> – oder klicken Sie auf das Tray-Symbol und wählen Sie <b>Scan</b>.",
            "Warten Sie, bis alle Seiten eingezogen sind. Der Scan wird verarbeitet und im Speicherordner abgelegt.",
            "Das <b>Quick-Menü</b> erscheint. Klicken Sie auf das gewünschte Ziel und dann auf <b>Speichern</b> – oder doppelklicken Sie einfach auf das Ziel.") +
        Shot("quickmenu", "Das Quick-Menü nach dem Scan") +
        Legend(
            "Die Ziele: Ordner, E-Mail, Drucker, Word, Excel, PowerPoint, Bilderordner und PDF-Programm. Die Pfeiltasten bewegen die Auswahl.",
            "<b>Abbrechen</b>: Das Quick-Menü schließt sich, die bereits gespeicherte Datei bleibt erhalten.",
            "<b>Speichern</b>: Führt das gewählte Ziel aus.") +
        Tip("Wählen Sie <b>In Ordner speichern</b>, können Sie im nächsten Fenster noch <a href='#postscan-save'>Dateiname und Ordner</a> ändern.") + @"

        <h2>Qualität schnell wählen</h2>
        <p>Auf der Seite <b>Allgemein</b> der <a href='#settings'>Einstellungen</a> stellen Sie mit einem Klick ein, wie fein gescannt wird:</p>
        " + Grid(new[] { "Kachel", "Bildqualität", "Komprimierung", "Geeignet für" },
            new[] { "Empfohlen", "Automatisch", "3", "die meisten Dokumente" },
            new[] { "Kleine Datei", "Normal", "5", "E-Mail-Versand, Archiv" },
            new[] { "Hohe Bildqualität", "Fein", "1", "Fotos, feine Details" },
            new[] { "Benutzerdefiniert", "eigene Werte", "eigene Werte", "öffnet die Seite „Scanmodus“" }) +
        Note("Solange das Quick-Menü eingeschaltet ist, ist die Seite <b>Anwendung</b> in den Einstellungen ausgegraut – das Ziel wählen Sie ja nach dem Scan."),
        "postscan-media", "scan-button", "scan-no-quick", "scan-display");

    private static HelpTopic TopicScanButton() => Page("scan-button", "Scan-Taste am Scanner",
        "Ein Druck auf die Taste am Scanner startet einen Scan – auch wenn SpeedScan Manager noch nicht läuft.",
        @"
        <h2>Einmal einrichten</h2>
        <p>Damit Windows die Taste an SpeedScan Manager weitergibt, muss sie einmal eingerichtet werden. Das geschieht beim
        ersten Start automatisch:</p>
        " + Steps(
            "Windows fragt nach Administratorrechten (Benutzerkontensteuerung). Klicken Sie auf <b>Ja</b>.",
            "Fertig – ab jetzt startet die Taste einen Scan. Administratorrechte sind danach nicht mehr nötig.") +
        Tip("Haben Sie die Abfrage abgelehnt oder einen weiteren Scanner angeschlossen? Wählen Sie im Tray-Menü <b>Scan-Taste einrichten...</b> – " +
            "die Einrichtung startet erneut, danach meldet eine Sprechblase „Die Scan-Taste ist eingerichtet.“ Für einen neu angeschlossenen Scanner erscheint die Abfrage beim nächsten Programmstart auch von selbst.") + @"

        <h2>So funktioniert es</h2>
        <ul>
            <li>SpeedScan Manager trägt sich in Windows als Programm für die Scan-Taste ein und ersetzt dabei das Windows-Standardprogramm.
            Beim Deinstallieren wird der ursprüngliche Zustand wiederhergestellt.</li>
            <li>Läuft das Programm schon, scannt es sofort. Läuft es noch nicht, startet Windows es und der Scan beginnt, sobald es bereit ist.</li>
        </ul>
        " + Note("Während ein Scan läuft – auch solange danach noch ein Fenster wie das Quick-Menü offen ist – wird die Taste ignoriert. " +
                 "Zwei Drücke innerhalb von etwa 1,5 Sekunden zählen als einer. Kann das Programm den Scanner nicht verwenden (z. B. TWAIN nicht bereit), reagiert die Taste nicht.") +
        "<p>Lehnen Sie die Abfrage ab, wird sie nicht erneut angezeigt. Sie können dann weiterhin über das Tray-Menü scannen.</p>",
        "scan-quick", "faq", "starting");

    private static HelpTopic TopicScanWithoutQuickMenu() => Page("scan-no-quick", "Scannen ohne Quick-Menü",
        "Immer dasselbe Ziel? Dann schalten Sie das Quick-Menü aus und legen das Ziel vorher fest – gern auch als Profil.",
        Steps(
            "Öffnen Sie die <a href='#settings'>Einstellungen</a> (Doppelklick auf das Tray-Symbol).",
            "Schalten Sie auf der Seite <b>Allgemein</b> den Schalter <b>Quick-Menü verwenden</b> aus.",
            "Wählen Sie unter <b>Aktives Profil</b> ein Profil – oder stellen Sie das Ziel auf der Seite <a href='#app-tab'>Anwendung</a> selbst ein.",
            "Klicken Sie auf <b>OK</b>.",
            "Scannen Sie wie gewohnt mit der Scan-Taste oder über <b>Scan</b> im Tray-Menü.") + @"

        <h2>Was nach dem Scan passiert</h2>
        " + Items(
            ("Scan to Folder", "Das Fenster <a href='#postscan-save'>In Ordner speichern</a> erscheint."),
            ("Alle anderen Ziele", "Werden sofort ausgeführt. Ist auf der Seite <b>Speichern</b> der Schalter <b>Datei nach Scan umbenennen</b> an, erscheint vorher das Fenster „In Ordner speichern“.")),
        "profile-mgmt", "app-tab", "scan-quick");

    private static HelpTopic TopicScanDisplay() => Page("scan-display", "Was nach dem Scan passiert",
        "Jeder Scan wird zuerst als Datei gespeichert – erst dann wird das gewählte Ziel ausgeführt.",
        @"
        <h2>1. Die Seiten werden aufbereitet</h2>
        <p>Je nach Einstellung, in dieser Reihenfolge:</p>
        " + Steps(
            "<b>Trägerblatt</b>: Vorder- und Rückseite werden nebeneinander zusammengefügt.",
            "<b>Auf das Blatt zuschneiden</b> (nur bei Papiergröße „Automatische Erkennung“, ohne Trägerblatt).",
            "<b>Leere Seiten entfernen</b>. Sind alle Seiten leer, wird nichts gespeichert und eine Meldung erscheint.",
            "<b>Schräglauf korrigieren</b> (wenn eingeschaltet).",
            "<b>Automatische Bilddrehung</b> (wenn eingeschaltet).",
            "<b>Farberkennung</b> bei Farbmodus „Automatische Farberkennung“: Farbe, Grau oder Schwarzweiß pro Seite.",
            "<b>Texterkennung</b> und Speichern als PDF, JPEG oder PNG.") + @"

        <h2>2. Die Datei wird gespeichert</h2>
        <p>Im Speicherordner (Grundeinstellung <code>Dokumente\SpeedScanManager</code>). Vorhandene Dateien werden nie überschrieben.
        Ist der Ordner nicht beschreibbar, landet der Scan in <code>Dokumente\SpeedScanManager</code> und Sie erhalten eine Meldung.</p>

        <h2>3. Das Ziel wird ausgeführt</h2>
        " + Items(
            ("In Ordner speichern", "Öffnet das Fenster <a href='#postscan-save'>In Ordner speichern</a> für Dateiname und Ordner."),
            ("Per E-Mail versenden", "Öffnet Ihr E-Mail-Programm (MAPI) mit der Datei als Anhang."),
            ("Drucken", "Druckt die Seiten in Originalgröße."),
            ("Nach Word / Excel / PowerPoint", "Erstellt ein Office-Dokument mit den Seiten als Bilder, speichert es neben dem Scan und öffnet es."),
            ("In Bilderordner", "Speichert die Seiten als JPEG in <code>Bilder\\SpeedScanManager</code> und zeigt das erste Bild im Explorer."),
            ("Mit PDF Edit bearbeiten", "Öffnet die Datei(en) mit dem Programm, das in Windows dafür eingestellt ist."),
            ("Eigene Anwendung", "Startet Ihr Programm mit den gespeicherten Dateien (nur ohne Quick-Menü).")) +
        Tip("Datei nicht gefunden? Wählen Sie im Tray-Menü <b>Scan-Ergebnis anzeigen</b> – der Explorer zeigt die zuletzt gespeicherte Datei."),
        "scan-quick", "save-tab");

    private static HelpTopic TopicScanContinue() => Page("scan-continue", "Scan abbrechen / fortsetzen",
        "Einen laufenden Scan stoppen – oder mehrere Stapel zu einem Dokument zusammenfassen.",
        @"
        <h2>Scan abbrechen</h2>
        <p>Klicken Sie auf das Tray-Symbol und wählen Sie <b>Scan abbrechen</b>. Die bis dahin gescannten Seiten werden verworfen,
        eine Sprechblase meldet „Scan abgebrochen“. Das geht auch, während die Frage zum Mehrfacheinzug oder „Weitere Dokumente scannen?“ angezeigt wird.</p>
        " + Note("Wählen Sie während eines Scans <b>Beenden</b>, fragt das Programm zuerst nach. Wird der Scan gerade gespeichert, " +
                 "beendet es sich erst danach. Bereits gespeicherte Scans werden beim Beenden (auch beim Abmelden von Windows) nie gelöscht.") + @"

        <h2>Mehrere Stapel zu einem Scan zusammenfassen</h2>
        " + Steps(
            "Schalten Sie auf der Seite <a href='#scanmode-tab'>Scanmodus</a> den Schalter <b>Scanvorgang nach aktuellem Scan fortsetzen</b> ein.",
            "Scannen Sie den ersten Stapel.",
            "Es erscheint die Frage „Weitere Dokumente scannen?“ mit der bisherigen Seitenzahl. Legen Sie den nächsten Stapel ein und klicken Sie auf <b>Fortsetzen</b>.",
            "Sind alle Seiten gescannt, klicken Sie auf <b>Scan beenden</b>. Alle Seiten werden gemeinsam gespeichert.") +
        Shot("continue_dialog", "Die Frage nach jedem Durchgang") +
        Note("Wird in einem Durchgang ein Mehrfacheinzug mit <b>Wiederholen</b> verworfen, bleiben die früheren Durchgänge erhalten."),
        "scanmode-tab", "multifeed-dialog");

    private static HelpTopic TopicScanLongPages() => Page("scan-long", "Lange Seiten scannen",
        "Kassenbons und andere lange Dokumente scannen Sie mit der automatischen Papiergröße.",
        Steps(
            "Stellen Sie auf der Seite <a href='#paper-tab'>Papier</a> die <b>Papiergröße</b> auf <b>Automatische Erkennung</b>.",
            "Stellen Sie die <b>Mehrfacheinzugserkennung</b> nicht auf eine Variante mit <b>Länge</b> (also <b>Aus</b> oder <b>Ultraschall</b>).",
            "Scannen Sie wie gewohnt.") + @"
        <p>In diesem Modus erlaubt das Programm Seiten bis 8,5 × 125 Zoll (ca. 21,6 × 317 cm). Bei Scannern von Fujitsu, PFU und Ricoh
        (PaperStream IP) setzt es zusätzlich die herstellerspezifischen Einstellungen für lange Seiten. Ob das Bild auf die echte Länge
        zugeschnitten wird, entscheidet der Scannertreiber.</p>
        " + Warn("Seiten über 86 cm gehen nur bis 200 dpi (Bildqualität <b>Automatisch</b> oder <b>Fein</b>); bei höherer Auflösung ist bei 86 cm Schluss. " +
                 "Der ältere Treiber „FUJITSU fi-…“ schafft nur 35,6 cm; sind beide installiert, nimmt das Programm PaperStream IP. " +
                 "Die maximale Länge hängt vom Scannermodell ab. Lange Seiten ergeben größere Dateien."),
        "paper-tab");

    private static HelpTopic TopicScanCarrierSheet() => Page("scan-carrier", "Mit dem Trägerblatt scannen",
        "Mit einem Trägerblatt scannen Sie Dokumente, die größer als A4 sind (z. B. A3) – gefaltet in einem Durchgang.",
        Steps(
            "Öffnen Sie auf der Seite <a href='#paper-tab'>Papier</a> die <b>Trägerblatteinstellungen...</b>, schalten Sie den Trägerblatt-Modus ein und wählen Sie die Ausgabegröße.",
            "Falten Sie das Dokument in der Mitte und legen Sie es in das Trägerblatt ein.",
            "Führen Sie das Trägerblatt in den Scanner ein und scannen Sie <b>doppelseitig</b> (Duplex).",
            "Vorder- und Rückseite werden nebeneinander zu einem Bild zusammengesetzt.") +
        Warn("Nur Scanner, die für ein Trägerblatt gebaut sind, ziehen es ein. Zusammengesetzt wird nur bei Duplex-Scan – " +
             "beim einseitigen Scan wird jede Seite einzeln auf die Ausgabegröße gebracht."),
        "carrier-sheet-dialog", "paper-tab");

    // ═══════════════════════════════════════════════════════════════════════
    // Einstellungen
    // ═══════════════════════════════════════════════════════════════════════

    private static HelpTopic TopicSettingsDialog() => Page("settings", "Das Einstellungsfenster",
        "Hier legen Sie fest, wie gescannt und gespeichert wird. Sie öffnen es per Doppelklick auf das Tray-Symbol.",
        Shot("settings_window", "Das Einstellungsfenster mit der Seite „Allgemein“") +
        Legend(
            "<b>Seitenleiste</b>: Wählen Sie hier die Seite. Ist das Quick-Menü an, ist „Anwendung“ ausgegraut.",
            "<b>Die gewählte Seite</b> mit ihren Einstellungen in Gruppen. Längere Seiten lassen sich scrollen.",
            "<b>Hilfe</b>: öffnet die Hilfe genau zu dieser Seite.",
            "<b>OK</b> übernimmt und schließt, <b>Übernehmen</b> übernimmt ohne zu schließen, <b>Abbrechen</b> verwirft.") + @"
        <p>Änderungen gelten erst nach <b>OK</b> oder <b>Übernehmen</b>. Solange etwas nicht übernommen ist, steht unten links
        „Nicht übernommene Änderungen“. <b>Abbrechen</b>, <span class='kbd'>Esc</span> und das Schließen-Symbol verwerfen diese Änderungen.</p>

        <h2>Die Seiten</h2>
        " + Items(
            ("Allgemein", "Quick-Menü, Autostart, Scanqualität, Profil und Design (siehe unten)."),
            ("<a href='#app-tab'>Anwendung</a>", "Was nach dem Scan passiert (nur ohne Quick-Menü)."),
            ("<a href='#save-tab'>Speichern</a>", "Speicherordner und Dateiname."),
            ("<a href='#scanmode-tab'>Scanmodus</a>", "Auflösung, Farbe, ein- oder doppelseitig."),
            ("<a href='#filetype-tab'>Dateiart</a>", "PDF, JPEG oder PNG, Texterkennung."),
            ("<a href='#paper-tab'>Papier</a>", "Papiergröße, Trägerblatt, Mehrfacheinzug."),
            ("<a href='#filesize-tab'>Dateigröße</a>", "Komprimierung für kleinere Dateien.")) + @"

        <h2>Die Seite „Allgemein“</h2>
        " + Shot("page_general", "Seite „Allgemein“") +
        Items(
            ("Quick-Menü verwenden", "An: Nach jedem Scan wählen Sie das Ziel (<a href='#scan-quick'>mehr</a>). Dann sind die Seite „Anwendung“ und die Gruppe „Profil“ ausgegraut."),
            ("Mit Windows starten", "Startet das Programm mit Windows (Grundeinstellung: an)."),
            ("Scanqualität", "<b>Empfohlen</b>, <b>Kleine Datei</b> und <b>Hohe Bildqualität</b> setzen Bildqualität und Komprimierung. Die passende Kachel trägt ein Häkchen. " +
                             "<b>Benutzerdefiniert</b> ist markiert, wenn Ihre Werte zu keiner Kachel passen – ein Klick öffnet die Seite „Scanmodus“. " +
                             "Bedienbar auch mit <span class='kbd'>Tab</span> und <span class='kbd'>Leertaste</span>."),
            ("Profil", "Nur ohne Quick-Menü: <b>Aktives Profil</b> wählen, mit <b>Profil hinzufügen...</b> die aktuellen Einstellungen als <a href='#new-profile'>neues Profil</a> speichern, " +
                       "mit <b>Profilverwaltung...</b> Profile <a href='#profile-mgmt-dialog'>bearbeiten</a>."),
            ("Design", "<b>System</b> folgt Windows, <b>Hell</b> und <b>Dunkel</b> legen das Aussehen fest. Wirkt sofort, auch ohne OK.")) +
        Note("Ändern Sie die Einstellungen eines eigenen Profils, werden sie beim Übernehmen im Profil gespeichert. Die Standardprofile bleiben unverändert – " +
             "Sie werden gefragt, ob die Änderungen als neues Profil gespeichert oder nur als aktuelle Einstellungen übernommen werden sollen.") +
        Tip("Seiten wechseln per Tastatur: <span class='kbd'>Strg</span>+<span class='kbd'>Tab</span> bzw. <span class='kbd'>Strg</span>+<span class='kbd'>Umschalt</span>+<span class='kbd'>Tab</span> " +
            "(oder <span class='kbd'>Strg</span>+<span class='kbd'>Bild ab</span> / <span class='kbd'>Bild auf</span>), in der Seitenleiste auch mit den Pfeiltasten. " +
            "<span class='kbd'>Enter</span> entspricht <b>OK</b>. Mit dem Minimieren-Symbol legen Sie das Fenster in die Taskleiste."),
        "scan-quick", "profile-mgmt", "default-settings");

    private static HelpTopic TopicApplicationTab() => Page("app-tab", "Seite „Anwendung“",
        "Legt fest, was nach dem Scan automatisch passiert – nur wenn das Quick-Menü ausgeschaltet ist.",
        Shot("page_app", "Seite „Anwendung“") +
        Items(
            ("Anwendung", "Scan to Folder, Scan to E-Mail, Scan to Print, Scan to Word, Scan to Excel, Scan to PowerPoint, Scan Picture Folder, Edit with PDF – " +
                          "und darunter Ihre eigenen Programme."),
            ("E-Mail-Optionen...", "Erscheint bei Scan to E-Mail und öffnet die <a href='#email-options'>E-Mail-Optionen</a>."),
            ("Druck-Optionen...", "Erscheint bei Scan to Print und öffnet die <a href='#print-options'>Druck-Optionen</a>."),
            ("Installieren/Deinstallieren...", "Eigene Programme hinzufügen oder entfernen (<a href='#app-manage'>mehr</a>).")) +
        Note("Ist das Quick-Menü eingeschaltet, ist diese Seite ausgegraut – das Ziel wählen Sie dann nach dem Scan.") +
        Tip("E-Mail- und Druckeinstellungen erreichen Sie auch direkt im Tray-Menü – praktisch mit Quick-Menü. Dort geänderte Werte gelten sofort " +
            "und werden auch im aktiven eigenen Profil gespeichert (Standardprofile bleiben unverändert).") +
        "<p>Eine eigene Anwendung wird nach dem Scan mit ihren Argumenten gestartet; die gespeicherten Dateien werden als weitere Argumente angehängt.</p>",
        "app-manage", "scan-no-quick", "settings");

    private static HelpTopic TopicSaveTab() => Page("save-tab", "Seite „Speichern“",
        "Wohin die Scans gespeichert werden und wie die Dateien heißen.",
        Shot("page_save", "Seite „Speichern“") +
        Items(
            ("Speicherordner für Bilddaten", "Der Zielordner. Ändern Sie ihn mit <b>Durchsuchen...</b> (das Feld selbst ist nicht editierbar). Das Symbol daneben öffnet den Ordner im Explorer, ein Rechtsklick kopiert den Pfad. Grundeinstellung: <code>Dokumente\\SpeedScanManager</code>."),
            ("Dateinamenformat...", "Öffnet das Fenster <a href='#filename-format'>Dateinamenformat</a>. Daneben sehen Sie ein Beispiel für den nächsten Dateinamen."),
            ("Datei nach Scan umbenennen", "An: Nach dem Scan erscheint das Fenster <a href='#postscan-save'>In Ordner speichern</a>, in dem Sie Name und Ordner noch ändern können.")) +
        Note("Bei Scan to Folder und bei „In Ordner speichern“ im Quick-Menü erscheint dieses Fenster immer, unabhängig vom Schalter.") +
        Tip("Vorhandene Dateien werden nie überschrieben: Bei gleichem Namen wird <code>_1</code>, <code>_2</code> usw. angehängt. " +
            "Entstehen mehrere Dateien (z. B. ein Bild pro Seite), erhalten sie eine fortlaufende Nummer."),
        "filename-format", "postscan-save");

    private static HelpTopic TopicScanModeTab() => Page("scanmode-tab", "Seite „Scanmodus“",
        "Auflösung, Farbe und ob ein- oder doppelseitig gescannt wird.",
        Shot("page_scanmode", "Seite „Scanmodus“") + @"
        <h2>Bildqualität</h2>
        <p>Je höher die Auflösung, desto schärfer – aber der Scan dauert länger und die Datei wird größer.</p>
        " + Grid(new[] { "Bildqualität", "Farbe / Grau", "Schwarzweiß" },
            new[] { "Automatisch", "200 dpi", "400 dpi" },
            new[] { "Normal", "150 dpi", "300 dpi" },
            new[] { "Fein", "200 dpi", "400 dpi" },
            new[] { "Beste", "300 dpi", "600 dpi" },
            new[] { "Hervorragend", "600 dpi", "1200 dpi" }) + @"
        <p>Kann der Scanner eine Auflösung nicht, nimmt das Programm die nächstliegende und weist mit einer Sprechblase darauf hin.</p>

        <h2>Farbmodus</h2>
        " + Items(
            ("Automatische Farberkennung", "Scannt in Farbe und speichert jede Seite passend als Farbe, Grau oder Schwarzweiß."),
            ("Farbe", "Immer in Farbe (Grundeinstellung)."),
            ("Grau", "Immer in Graustufen."),
            ("Schwarzweiß", "Immer schwarzweiß – ideal für Text, sehr kleine Dateien.")) +
        Warn("Bei der automatischen Farberkennung können Seiten mit wenig Farbe als Grau oder Schwarzweiß erkannt werden.") + @"

        <h2>Scan-Seite</h2>
        " + Items(
            ("Automatisch", "Grundeinstellung: Liegt Papier im Einzug, wird doppelseitig gescannt und leere Rückseiten werden entfernt – sonst wird, falls vorhanden, das Flachbett verwendet."),
            ("Duplex-Scan (doppelseitig)", "Vorder- und Rückseite."),
            ("Simplex-Scan (einseitig)", "Nur die Vorderseite."),
            ("Flachbettscannen", "Über die Glasfläche. Die Papiergröße wird dabei auf A4 gesetzt (keine automatische Erkennung).")) + @"
        <p>Kann der Scanner kein Duplex bzw. hat er kein Flachbett, scannt das Programm einseitig bzw. über den Einzug und meldet das per Sprechblase.</p>

        <h2>Weitere Einstellungen</h2>
        " + Items(
            ("Scanvorgang nach aktuellem Scan fortsetzen", "Fragt nach jedem Durchgang „Weitere Dokumente scannen?“ (<a href='#scan-continue'>mehr</a>)."),
            ("Optionen...", "Helligkeit, leere Seiten, Schräglauf, Drehung, Reihenfolge – im Fenster <a href='#scanmode-options'>Scanmodus-Optionen</a>.")) +
        Tip("Die Seite öffnet sich auch über die Kachel <b>Benutzerdefiniert</b> auf der Seite <b>Allgemein</b>."),
        "scanmode-options", "paper-tab", "filesize-tab");

    private static HelpTopic TopicFileTypeTab() => Page("filetype-tab", "Seite „Dateiart“",
        "Dateiformat (PDF, JPEG, PNG) und Texterkennung für durchsuchbare PDFs.",
        Shot("page_filetype", "Seite „Dateiart“") + @"
        <h2>Dateiformat</h2>
        " + Items(
            ("PDF (*.pdf)", "Alle Seiten in einer PDF-Datei – oder aufgeteilt nach den <a href='#pdf-options'>PDF-Optionen</a> (Schaltfläche <b>Optionen...</b>)."),
            ("JPEG (*.jpg)", "Eine Bilddatei pro Seite. Nicht verfügbar bei Farbmodus „Schwarzweiß“."),
            ("PNG (*.png)", "Eine verlustfreie Bilddatei pro Seite.")) + @"

        <h2>So erstellen Sie eine durchsuchbare PDF</h2>
        " + Steps(
            "Wählen Sie als Dateiformat <b>PDF</b>.",
            "Schalten Sie <b>In durchsuchbare PDF konvertieren</b> ein.",
            "Wählen Sie unter <b>Sprache</b> die Sprache des Dokuments (oder <b>Automatische Erkennung</b>) und unter <b>Zielseiten</b> <b>Erste Seite</b> oder <b>Alle Seiten</b>.",
            "Klicken Sie auf <b>OK</b>. Der erkannte Text liegt danach unsichtbar über dem Bild – Sie können darin suchen und Text kopieren.") + @"

        <h2>Markierten Text als Schlüsselwort</h2>
        <p>Ist <b>Markierten Text als Schlüsselwort der PDF-Datei hinzufügen</b> an, werden mit Textmarker hervorgehobene Stellen als Schlüsselwörter
        in die PDF-Eigenschaften eingetragen. Unter <b>Zielmarkierung</b> wählen Sie die erste oder alle markierten Stellen.
        Das funktioniert nur zusammen mit der Texterkennung – sonst wird es übersprungen und eine Sprechblase erscheint.</p>

        <h2>Sprachen</h2>
        <p>Mitgeliefert sind <b>Deutsch</b> und <b>Englisch</b>. Weitere Sprachen (Japanisch, Französisch, Italienisch, Spanisch, Chinesisch
        vereinfacht/traditionell, Koreanisch, Russisch, Portugiesisch, Arabisch) erscheinen in der Liste, sobald Sie die passende
        <code>.traineddata</code>-Datei (z. B. <code>fra.traineddata</code>) in den Ordner <code>tessdata</code> neben <code>SpeedScanManager.exe</code> legen.</p>
        <p>Bei <b>Automatische Erkennung</b> wählt das Programm pro Seite anhand der Schrift eine installierte Sprache, sonst Deutsch und Englisch.</p>
        " + Note("Ist eine gespeicherte Sprache nicht mehr installiert, steht „(nicht installiert)“ dahinter. Beim Scannen erscheint dann eine Sprechblase " +
                 "und die vorhandenen Sprachen (bevorzugt Deutsch/Englisch) werden verwendet."),
        "pdf-options", "scanmode-tab");

    private static HelpTopic TopicPaperTab() => Page("paper-tab", "Seite „Papier“",
        "Papiergröße, Trägerblatt und die Erkennung von doppelt eingezogenen Blättern.",
        Shot("page_paper", "Seite „Papier“") + @"
        <h2>Papiergröße</h2>
        <p><b>Automatische Erkennung</b> (Grundeinstellung) schneidet jede Seite auf das Blatt zu: Der graue Scannerhintergrund
        und leere Flächen nach dem Blattende fallen weg. Sie ist auch für <a href='#scan-long'>lange Seiten</a> nötig. Außerdem wählbar:</p>
        " + Grid(new[] { "Größe", "Maße" },
            new[] { "A3 / A4 / A5 / A6", "297 × 420 / 210 × 297 / 148 × 210 / 105 × 148 mm" },
            new[] { "B4 / B5 / B6 (JIS)", "257 × 364 / 182 × 257 / 128 × 182 mm" },
            new[] { "Postkarte", "100 × 148 mm" },
            new[] { "Visitenkarte", "55 × 91 mm" },
            new[] { "Letter / Legal", "8,5 × 11 in. / 8,5 × 14 in." },
            new[] { "Double Letter", "11 × 17 in." },
            new[] { "Eigene Größen", "bis zu 10, über <b>Benutzerdefiniert...</b>" }) +
        Note("Welche Größen funktionieren, hängt vom Scanner ab – sonst erscheint eine Sprechblase. Beim Flachbett gibt es keine automatische Erkennung.") +
        Items(
            ("Trägerblatteinstellungen...", "Öffnet die <a href='#carrier-sheet-dialog'>Trägerblatteinstellungen</a>."),
            ("Benutzerdefiniert...", "Eigene Papiergrößen anlegen (<a href='#custom-size-dialog'>mehr</a>).")) + @"

        <h2>Mehrfacheinzugserkennung</h2>
        <p>Erkennt, wenn der Scanner zwei Blätter gleichzeitig einzieht.</p>
        " + Items(
            ("Aus", "Keine Prüfung (Grundeinstellung)."),
            ("Überprüfung von Überlappung [Ultraschall]", "Misst die Blattstärke."),
            ("Überprüfung der Länge", "Vergleicht die Blattlänge."),
            ("Überprüfung von Überlappung und Länge", "Beides. Kann der Scanner das nicht gleichzeitig, wird nur Ultraschall verwendet.")) +
        Note("Meldet der Scanner seine Fähigkeiten, sind nicht unterstützte Einträge mit „(nicht unterstützt)“ gekennzeichnet und nicht wählbar."),
        "multifeed-dialog", "custom-size-dialog", "carrier-sheet-dialog");

    private static HelpTopic TopicFileSizeTab() => Page("filesize-tab", "Seite „Dateigröße“",
        "Wie stark Farb- und Graubilder komprimiert werden – kleinere Dateien oder bessere Qualität.",
        Shot("page_filesize", "Seite „Dateigröße“") + @"
        <p>Der Schieber hat fünf Stufen (Grundeinstellung 3). Er bestimmt die JPEG-Qualität – auch in PDF-Dateien, Office-Dokumenten und im Bilderordner.</p>
        " + Grid(new[] { "Stufe", "JPEG-Qualität", "Ergebnis" },
            new[] { "1", "100", "beste Qualität, größte Datei" },
            new[] { "2", "85", "" },
            new[] { "3", "70", "ausgewogen" },
            new[] { "4", "50", "" },
            new[] { "5", "30", "kleinste Datei, sichtbare Verluste" }) +
        Note("Bei Farbmodus „Schwarzweiß“ und beim Format PNG ist der Schieber ausgegraut: Schwarzweißbilder und PNG werden verlustfrei gespeichert.") +
        Tip("Für kleine Dateien zum Versenden genügt meist die Kachel <b>Kleine Datei</b> auf der Seite <b>Allgemein</b>."),
        "scanmode-tab", "filetype-tab");

    // ═══════════════════════════════════════════════════════════════════════
    // Fenster und Dialoge
    // ═══════════════════════════════════════════════════════════════════════

    private static HelpTopic TopicPostScanMediaDialog() => Page("postscan-media", "Quick-Menü",
        "„Wohin möchtest du scannen?“ – erscheint nach jedem Scan, wenn das Quick-Menü eingeschaltet ist.",
        Shot("quickmenu", "Das Quick-Menü") +
        Legend(
            "Klicken Sie auf ein Ziel, um es auszuwählen (oder mit den Pfeiltasten). Ein <b>Doppelklick</b> startet es sofort.",
            "<b>Abbrechen</b> oder Schließen-Symbol: Fenster schließen. Die Datei bleibt gespeichert (Sprechblase „Scan gespeichert“).",
            "<b>Speichern</b>: das gewählte Ziel ausführen.") + @"
        <p>Oben steht, wie viele Seiten gescannt wurden. Die Datei liegt zu diesem Zeitpunkt schon im Speicherordner. Das Fenster bleibt im Vordergrund.</p>
        <h2>Die acht Ziele</h2>
        " + Items(
            ("In Ordner speichern", "Öffnet <a href='#postscan-save'>In Ordner speichern</a> für Titel und Zielordner."),
            ("Per E-Mail versenden", "E-Mail-Programm mit der Datei als Anhang. Das Zahnrad oben rechts öffnet die <a href='#email-options'>E-Mail-Optionen</a>."),
            ("Drucken", "Druckt die Seiten. Das Zahnrad oben rechts öffnet die <a href='#print-options'>Druck-Optionen</a>."),
            ("Nach Word", "Word-Dokument (.docx) mit den Seiten als Bilder."),
            ("Nach Excel", "Excel-Datei (.xlsx) mit den Seiten als Bilder."),
            ("Nach PowerPoint", "Präsentation (.pptx) mit den Seiten als Bilder."),
            ("In Bilderordner", "JPEG-Dateien in <code>Bilder\\SpeedScanManager</code>."),
            ("Mit PDF Edit bearbeiten", "Öffnet die Datei im dafür eingestellten Programm.")),
        "scan-quick", "postscan-save");

    private static HelpTopic TopicPostScanSaveDialog() => Page("postscan-save", "In Ordner speichern",
        "Prüfen Sie die Seiten und legen Sie Dateiname und Zielordner fest.",
        Shot("save_dialog", "Das Fenster „In Ordner speichern“") +
        Legend(
            "<b>Vorschau</b> der gescannten Seite.",
            "<b>Blättern</b> durch die Seiten („Seite 2 von 3“).",
            "<b>Titel festlegen</b>: der vorgeschlagene Dateiname – ändern Sie ihn nach Wunsch. Ungültige Zeichen werden ersetzt.",
            "<b>Zielordner festlegen</b>: Ordner mit <b>Durchsuchen...</b> wählen (das Feld ist nicht direkt editierbar).",
            "<b>Speichern</b>: Name und Ordner übernehmen.") + @"
        <p>Rechts unten fasst die Gruppe <b>Scaneinstellungen</b> zusammen, wie gescannt wurde (Voreinstellung, Auflösung, Farbmodus, Scanseite, Dateiformat, Kompression).</p>
        " + Note("Die Datei ist schon gespeichert – das Fenster benennt sie nur um bzw. verschiebt sie. Bei mehreren Dateien wird eine Nummer angehängt; " +
                 "vorhandene Dateien werden nie überschrieben (<code>_1</code> usw.).") +
        Warn("<b>Abbrechen</b>, <span class='kbd'>Esc</span> oder das Schließen-Symbol fragen „Scan verwerfen?“. Mit <b>Ja</b> werden die erstellten Dateien gelöscht, " +
             "mit <b>Nein</b> kehren Sie zum Fenster zurück."),
        "save-tab", "filename-format", "postscan-media");

    private static HelpTopic TopicScanModeOptionsDialog() => Page("scanmode-options", "Scanmodus-Optionen",
        "Helligkeit und automatische Bildverbesserungen. Öffnen über <b>Optionen...</b> auf der Seite „Scanmodus“.",
        Shot("dlg_scanmode_options", "Das Fenster „Scanmodus-Optionen“") + @"
        <h2>Helligkeit</h2>
        <p>Sieben Stufen von „Dunkler -3“ bis „Heller +3“ (Mitte = Normal). Der Regler wirkt nur beim Farbmodus <b>Schwarzweiß</b> –
        sonst ist er ausgegraut („Nur für Schwarzweiß-Scans“).</p>
        <h2>Bildverarbeitung</h2>
        " + Items(
            ("Einstellung nur für Textdokumente", "Verstärkt den Kontrast bei Schwarzweiß – gut für Text und Handschrift."),
            ("Automatisches Löschen leerer Seiten zulassen", "Entfernt leere Seiten (Grundeinstellung: an). Sind alle Seiten leer, wird keine Datei gespeichert."),
            ("Schräglauf automatisch korrigieren", "Rückt schief eingezogene Seiten gerade (Grundeinstellung: aus)."),
            ("Automatische Bilddrehung zulassen", "Dreht seitlich oder kopfüber gescannte Seiten richtig herum (Grundeinstellung: an). Dazu wird ein Stück Text probeweise gelesen. Ist zu wenig Text da, wird nicht gedreht."),
            ("Dokumente mit der Vorderseite nach oben einlegen", "An, wenn Sie den Stapel mit der Schrift nach oben einlegen: Die Reihenfolge wird umgedreht, damit die Seiten richtig sortiert sind.")) +
        Note("Bei Scan-Seite „Automatisch“ werden leere Rückseiten auch ohne die Option „leere Seiten“ entfernt.") +
        "<p><b>OK</b> übernimmt, <b>Abbrechen</b> verwirft die Änderungen.</p>",
        "scanmode-tab");

    private static HelpTopic TopicPdfOptionsDialog() => Page("pdf-options", "PDF-Optionen",
        "PDF aufteilen und mit Kennwort schützen. Öffnen über <b>Optionen...</b> auf der Seite „Dateiart“ (nur bei PDF).",
        Shot("dlg_pdf_options", "Das Fenster „PDF-Optionen“") + @"
        <h2>PDF-Seiten teilen</h2>
        " + Items(
            ("Mehrseitige PDF-Datei", "Alle Seiten in eine PDF-Datei (Grundeinstellung)."),
            ("Seitenzahl, für die jeweils eine neue PDF-Datei erstellt werden soll", "Nach jeweils so vielen Seiten (1–999) beginnt eine neue Datei, z. B. <code>Name_001.pdf</code>, <code>Name_002.pdf</code>.")) + @"
        <h2>Kennwort</h2>
        " + Steps(
            "Schalten Sie <b>Kennwort für PDF-Datei einstellen</b> ein.",
            "Geben Sie das Kennwort unter <b>Kennwort</b> ein und wiederholen Sie es unter <b>Bestätigen</b>.",
            "Klicken Sie auf <b>OK</b>. Fehlt das Kennwort oder stimmen beide Eingaben nicht überein, erscheint eine Meldung und das Fenster bleibt offen.") +
        Note("Das Kennwort wird verschlüsselt gespeichert und ist nur für Ihr Windows-Benutzerkonto lesbar. Kann es nicht gelesen werden (z. B. Einstellungen " +
             "von einem anderen PC), bleibt der Schutz an und das Programm fragt vor dem nächsten Scan nach – geben Sie das Kennwort dann hier neu ein."),
        "filetype-tab");

    private static HelpTopic TopicFileNameFormatDialog() => Page("filename-format", "Dateinamenformat",
        "Wie die gescannten Dateien automatisch benannt werden. Öffnen über <b>Dateinamenformat...</b> auf der Seite „Speichern“.",
        Shot("dlg_filename", "Das Fenster „Dateinamenformat“ mit benutzerdefiniertem Namen") + @"
        <p>Unten im Fenster zeigt <b>Beispiel</b>, wie der nächste Dateiname aussieht.</p>
        " + Items(
            ("Einstellung Ihres Betriebssystems", "Datum und Uhrzeit im Windows-Format. Unerlaubte Zeichen wie <code>:</code> und <code>/</code> werden zu <code>-</code>, z. B. <code>12.04.2026 15-30-45.pdf</code>."),
            ("JJJJMMTThhmmss", "Datum und Uhrzeit am Stück, z. B. <code>20260412153045.pdf</code> (Grundeinstellung)."),
            ("Benutzerdefinierter Dateiname", "Ihr eigener Name plus fortlaufende Nummer, z. B. <code>Rechnung_001.pdf</code>.")) + @"
        <h2>Eigenen Namen festlegen</h2>
        " + Steps(
            "Wählen Sie <b>Benutzerdefinierter Dateiname</b>.",
            "Geben Sie unter <b>Dateiname</b> den Anfang des Namens ein (bis 30 Zeichen).",
            "Wählen Sie unter <b>Zähler</b>, wie viele Stellen die Nummer hat (3, 4 oder 5 Ziffern).",
            "Klicken Sie auf <b>OK</b>.") +
        Note("Nicht erlaubt sind die Zeichen \\ / : * ? \" &lt; &gt; |, ein Punkt am Ende und reservierte Windows-Namen (z. B. CON, NUL, COM1). " +
             "Der Zähler beginnt bei 1 und macht nach der höchsten Nummer weiter, die schon im Ordner liegt. Gibt es einen Namen schon, wird <code>_1</code>, <code>_2</code> usw. angehängt.") +
        "<p><b>Übernehmen</b> (unten links) übernimmt, ohne zu schließen; <b>Abbrechen</b> verwirft die Änderungen.</p>",
        "save-tab", "postscan-save");

    private static HelpTopic TopicNewProfileDialog() => Page("new-profile", "Neues Profil hinzufügen",
        "Ein kleines Fenster für den Namen eines neuen Profils.",
        Shot("dlg_new_profile", "Namen für ein neues Profil eingeben") + @"
        <p>Geben Sie einen Namen ein (höchstens 64 Zeichen) und klicken Sie auf <b>OK</b>. Der Name muss eindeutig sein – Groß- und
        Kleinschreibung zählt dabei nicht. Ein ungültiger oder doppelter Name wird mit einer Meldung abgelehnt.</p>
        " + Items(
            ("Über „Profil hinzufügen...“", "Seite <b>Allgemein</b> der Einstellungen: Das neue Profil übernimmt die aktuell eingestellten Werte."),
            ("Über „Hinzufügen...“ in der Profilverwaltung", "Das neue Profil startet mit den <a href='#default-settings'>Grundeinstellungen</a>.")) +
        Note("Dasselbe Fenster erscheint auch beim Umbenennen („Profil umbenennen“). Insgesamt sind bis zu 20 Profile möglich (einschließlich der Standardprofile)."),
        "profile-mgmt", "profile-mgmt-dialog");

    private static HelpTopic TopicProfileManagementDialog() => Page("profile-mgmt-dialog", "Profilverwaltung",
        "Profile hinzufügen, umbenennen, löschen und sortieren.",
        Shot("dlg_profiles", "Das Fenster „Profilverwaltung“") +
        Items(
            ("Hinzufügen...", "Neues Profil mit den Grundeinstellungen (höchstens 20 Profile)."),
            ("Umbenennen...", "Neuen Namen für das gewählte Profil vergeben."),
            ("Löschen", "Löscht das gewählte Profil nach einer Rückfrage."),
            ("Nach oben / Nach unten", "Verschiebt das Profil in der Liste."),
            ("Schließen", "Schließt das Fenster.")) +
        Note("Die acht Standardprofile können nicht umbenannt oder gelöscht, aber verschoben werden. Eigene Profile können Sie frei bearbeiten."),
        "profile-mgmt", "default-settings");

    private static HelpTopic TopicAppManageDialog() => Page("app-manage", "Eigene Anwendungen",
        "Bis zu zehn eigene Programme hinzufügen, die nach dem Scan mit den Dateien gestartet werden.",
        Shot("dlg_apps", "Das Fenster „Installieren/Deinstallieren“") + @"
        <h2>Programm hinzufügen</h2>
        " + Steps(
            "Klicken Sie auf <b>Hinzufügen...</b>.",
            "Wählen Sie mit <b>Durchsuchen...</b> die Programmdatei (*.exe). Ist der Name noch leer, wird der Dateiname übernommen.",
            "Passen Sie den <b>Namen</b> an (eindeutig, bis 64 Zeichen) – so erscheint das Programm in der Liste „Anwendung“.",
            "Tragen Sie bei Bedarf <b>Argumente</b> ein. Die gescannten Dateien werden danach in Anführungszeichen angehängt.",
            "Klicken Sie auf <b>OK</b>.") +
        Shot("dlg_app_edit", "Ein Programm hinzufügen oder ändern") +
        Items(
            ("Ändern...", "Die Angaben des gewählten Programms ändern. Ein Doppelklick auf den Eintrag tut dasselbe."),
            ("Entfernen", "Das gewählte Programm aus der Liste entfernen."),
            ("Schließen", "Schließt das Fenster.")) +
        Note("Änderungen an dieser Liste werden sofort gespeichert – unabhängig von OK oder Abbrechen im Einstellungsfenster. " +
             "Findet sich ein Programm später nicht oder startet es nicht, erscheint eine Meldung; der Scan bleibt gespeichert."),
        "app-tab");

    private static HelpTopic TopicEmailOptionsDialog() => Page("email-options", "E-Mail-Optionen",
        "Empfänger und Betreff für Scan to E-Mail. Öffnen über die Seite „Anwendung“ oder <b>E-Mail-Einstellungen...</b> im Tray-Menü.",
        Shot("dlg_email", "Das Fenster „Scan to E-Mail“") +
        Items(
            ("Standard-Empfänger", "Wird in jede neue E-Mail eingetragen."),
            ("Betreff-Vorlage", "Betreff der E-Mail. Bleibt das Feld leer, lautet er „Gescanntes Dokument“.")) +
        Note("Verwendet wird das E-Mail-Programm, das in Windows als MAPI-Standard eingestellt ist. Lässt es sich nicht öffnen, erscheint eine Meldung " +
             "und der Ordner mit der Datei wird geöffnet – so können Sie sie von Hand anhängen."),
        "app-tab");

    private static HelpTopic TopicPrintOptionsDialog() => Page("print-options", "Druck-Optionen",
        "Der Drucker für Scan to Print. Öffnen über die Seite „Anwendung“ oder <b>Druckeinstellungen...</b> im Tray-Menü.",
        Shot("dlg_print", "Das Fenster „Scan to Print“") + @"
        <p>Wählen Sie unter <b>Zieldrucker</b> einen der installierten Drucker. <b>(Standarddrucker)</b> verwendet immer den aktuellen Windows-Standarddrucker.</p>
        " + Note("Gedruckt wird in Originalgröße; Querformat wird bei Bedarf automatisch gewählt. Gibt es den eingestellten Drucker nicht mehr, werden Sie gefragt, " +
                 "ob auf dem Standarddrucker gedruckt werden soll. Ist gar kein Drucker installiert, wird der Scan nur gespeichert."),
        "app-tab");

    private static HelpTopic TopicCarrierSheetDialog() => Page("carrier-sheet-dialog", "Trägerblatteinstellungen",
        "Trägerblatt-Modus einschalten und die Größe des fertigen Bildes wählen.",
        Shot("dlg_carrier", "Das Fenster „Trägerblatteinstellungen“ mit Anleitung") +
        Items(
            ("Trägerblatt-Modus aktivieren", "Schaltet das Zusammensetzen ein. Ist der Schalter aus, sind die anderen Felder gesperrt."),
            ("Modus", "<b>Zwei Seiten in einem Bild erstellen</b>: Vorder- und Rückseite (Duplex) werden nebeneinandergesetzt."),
            ("Ausgabebildgröße", "Automatische Erkennung, A3–A6, B4–B6 (JIS), Letter, Double Letter, Postkarte, Visitenkarte, Trägerblattgröße (220 × 297 mm) oder <b>Benutzerdefiniert...</b>"),
            ("Benutzerdefinierte Größe", "Erscheint bei „Benutzerdefiniert...“: Breite und Höhe in mm (50–2000).")) + @"
        <p>Die Gruppe <b>Anleitung</b> zeigt in drei Bildern, wie es geht: Dokument falten, in das Trägerblatt legen, in den Scanner führen.</p>
        " + Warn("Nur bestimmte Scannermodelle unterstützen das Trägerblatt."),
        "scan-carrier");

    private static HelpTopic TopicCustomSizeDialog() => Page("custom-size-dialog", "Eigene Papiergrößen",
        "Bis zu zehn eigene Papiergrößen anlegen – sie erscheinen danach in der Liste „Papiergröße“.",
        Shot("dlg_custom_size", "Das Fenster „Benutzerdefinierte Größen“") +
        Steps(
            "Klicken Sie auf <b>Hinzufügen...</b>.",
            "Geben Sie einen <b>Namen</b> (bis 62 Zeichen) sowie <b>Breite</b> und <b>Länge</b> in Millimetern (1–9999) ein.",
            "Klicken Sie auf <b>OK</b>.") +
        Items(
            ("Ändern...", "Name oder Maße der gewählten Größe ändern."),
            ("Entfernen", "Die gewählte Größe löschen."),
            ("Ausgewählte Größe", "Zeigt Breite × Länge der gewählten Größe."),
            ("Schließen", "Schließt das Fenster.")),
        "paper-tab");

    private static HelpTopic TopicVersionInfoDialog() => Page("version-info", "Versionsinformationen",
        "Version, Lizenz und Links zum Projekt. Öffnen über <b>Hilfe › Versionsinformationen</b> im Tray-Menü.",
        Shot("dlg_version", "Das Fenster „Versionsinformationen“") +
        Items(
            ("Logo und Version", "Die installierte Version von SpeedScan Manager."),
            ("Lizenz und Credits", "Hinweise zur GPL-3.0-Lizenz und zu den verwendeten Bibliotheken NTwain (TWAIN), Tesseract.NET (Texterkennung) und PdfSharpCore (PDF)."),
            ("GitHub / Discord", "Öffnen die Projektseite bzw. den Discord-Server im Browser."),
            ("Detail...", "Zeigt die <a href='#driver-info'>Scanner- und Treiberinformationen</a>."),
            ("OK", "Schließt das Fenster.")),
        "driver-info");

    private static HelpTopic TopicScannerDriverInfoDialog() => Page("driver-info", "Scanner- und Treiberinfos",
        "Technische Angaben zum Scanner und zu den Programmdateien – praktisch, wenn Sie Hilfe anfragen.",
        Shot("dlg_driver", "Das Fenster „Scanner- und Treiberinformationen“") +
        Items(
            ("Scanner", "Name, Hersteller, Produktfamilie und Treiberversion. Die Werte lassen sich markieren und kopieren. Ohne Scanner steht dort „Kein Scanner verbunden“."),
            ("Dateiversion", "Name, Version und Datum der Programmdatei und der Bibliotheken NTwain, Tesseract, PdfSharpCore und DocumentFormat.OpenXml."),
            ("OK", "Schließt das Fenster.")),
        "version-info");

    // ═══════════════════════════════════════════════════════════════════════
    // Profile
    // ═══════════════════════════════════════════════════════════════════════

    private static HelpTopic TopicProfileManagement() => Page("profile-mgmt", "Mit Profilen arbeiten",
        "Ein Profil speichert alle Scaneinstellungen – z. B. „Rechnungen“ als PDF in einen festen Ordner.",
        @"
        <p>Profile gelten nur, wenn das Quick-Menü <b>aus</b> ist. Ein Profil umfasst die Seiten Anwendung, Speichern, Scanmodus, Dateiart,
        Papier und Dateigröße. Bis zu 20 Profile sind möglich, einschließlich der acht Standardprofile.</p>

        <h2>Neues Profil anlegen</h2>
        " + Steps(
            "Öffnen Sie die <a href='#settings'>Einstellungen</a> und stellen Sie alles so ein, wie das Profil arbeiten soll.",
            "Klicken Sie auf der Seite <b>Allgemein</b> in der Gruppe <b>Profil</b> auf <b>Profil hinzufügen...</b>.",
            "Geben Sie einen <a href='#new-profile'>Namen</a> ein und klicken Sie auf <b>OK</b>.") + @"

        <h2>Profil verwenden</h2>
        <p>Wählen Sie das Profil unter <b>Aktives Profil</b> und klicken Sie auf <b>OK</b>. Ab jetzt scannt jeder Tastendruck mit diesem Profil.</p>

        <h2>Profil ändern</h2>
        <p>Wählen Sie das Profil, ändern Sie die Einstellungen und klicken Sie auf <b>OK</b> oder <b>Übernehmen</b> – die Änderungen landen im Profil.
        Standardprofile bleiben dabei unverändert; Sie werden gefragt, ob ein neues Profil angelegt werden soll.</p>

        <h2>Umbenennen, löschen, sortieren</h2>
        <p>In der <a href='#profile-mgmt-dialog'>Profilverwaltung</a> – zu öffnen mit <b>Profilverwaltung...</b> auf der Seite <b>Allgemein</b> oder im Tray-Menü.</p>
        " + Note("Ist das Quick-Menü eingeschaltet, ist die Gruppe „Profil“ ausgegraut."),
        "profile-mgmt-dialog", "default-settings", "scan-no-quick");

    private static HelpTopic TopicDefaultSettings() => Page("default-settings", "Grundeinstellungen",
        "Diese Werte gelten beim ersten Start, für alle Standardprofile und für neue Profile aus der Profilverwaltung.",
        Grid(new[] { "Einstellung", "Grundeinstellung" },
            new[] { "Quick-Menü verwenden", "An" },
            new[] { "Beim Anmelden starten", "An" },
            new[] { "Anwendung", "Scan to Folder" },
            new[] { "Speicherordner", "Dokumente\\SpeedScanManager" },
            new[] { "Dateinamenformat", "Datum und Uhrzeit (JJJJMMTThhmmss)" },
            new[] { "Benutzerdefinierter Dateiname", "unbenannt, 3 Ziffern" },
            new[] { "Datei nach Scan umbenennen", "Aus" },
            new[] { "Bildqualität", "Automatisch (200 dpi, Schwarzweiß 400 dpi)" },
            new[] { "Farbmodus", "Farbe" },
            new[] { "Scan-Seite", "Automatisch" },
            new[] { "Scanvorgang fortsetzen", "Aus" },
            new[] { "Helligkeit", "Normal" },
            new[] { "Leere Seiten löschen", "An" },
            new[] { "Schräglauf korrigieren", "Aus" },
            new[] { "Automatische Bilddrehung", "An" },
            new[] { "Dateiformat", "PDF, mehrseitig, ohne Kennwort" },
            new[] { "Texterkennung (OCR)", "Aus (Sprache: Automatische Erkennung, Zielseiten: Alle Seiten)" },
            new[] { "Papiergröße", "Automatische Erkennung" },
            new[] { "Mehrfacheinzugserkennung", "Aus" },
            new[] { "Trägerblatt-Modus", "Aus" },
            new[] { "Komprimierungsrate", "Stufe 3 von 5" },
            new[] { "E-Mail-Betreff", "Gescanntes Dokument" },
            new[] { "Zieldrucker", "(Standarddrucker)" }) + @"

        <h2>Die acht Standardprofile</h2>
        <p>Je eines pro Ziel. Sie haben die Grundeinstellungen und unterscheiden sich nur in der Anwendung:</p>
        " + Grid(new[] { "Profil", "Anwendung" },
            new[] { "In Ordner speichern", "Scan to Folder" },
            new[] { "Per E-Mail senden", "Scan to E-Mail" },
            new[] { "Drucken", "Scan to Print" },
            new[] { "In Word-Dokument umwandeln", "Scan to Word" },
            new[] { "In Excel-Tabelle umwandeln", "Scan to Excel" },
            new[] { "In PowerPoint-Präsentation umwandeln", "Scan to PowerPoint" },
            new[] { "In Bilderordner speichern", "Scan Picture Folder" },
            new[] { "Mit PDF-Programm bearbeiten", "Edit with PDF" }) +
        Note("Die Kacheln <b>Empfohlen</b>, <b>Kleine Datei</b> und <b>Hohe Bildqualität</b> sind keine Profile, sondern Voreinstellungen für Bildqualität und Komprimierung."),
        "profile-mgmt");

    // ═══════════════════════════════════════════════════════════════════════
    // Probleme lösen
    // ═══════════════════════════════════════════════════════════════════════

    private static HelpTopic TopicFaq() => Page("faq", "Häufige Probleme",
        "Die häufigsten Fragen – mit der schnellsten Lösung.",
        QA("Der Scanner wird nicht erkannt",
            "<ul><li>Ist der Scanner eingeschaltet und per USB oder Netzwerk verbunden?</li>" +
            "<li>Ist der TWAIN-Treiber des Herstellers installiert (32-Bit-TWAIN-Quelle)?</li>" +
            "<li>Sind mehrere Scanner angeschlossen? Wählen Sie den richtigen unter <a href='#scanner-selection'>Scanner auswählen</a>.</li>" +
            "<li>Warten Sie nach dem Programmstart einige Sekunden – erst dann wird der Scanner abgefragt. Ziehen Sie ggf. das USB-Kabel kurz ab und stecken Sie es wieder ein.</li></ul>") +
        QA("Die Taste am Scanner startet keinen Scan",
            "<p>Wählen Sie im Tray-Menü <b>Scan-Taste einrichten...</b> und bestätigen Sie die Windows-Abfrage mit <b>Ja</b>. " +
            "Läuft gerade ein Scan oder ist noch ein Fenster danach offen (z. B. das Quick-Menü), wird die Taste ignoriert. " +
            "<a href='#scan-button'>Mehr zur Scan-Taste</a></p>") +
        QA("Wo ist meine gescannte Datei?",
            "<p>Wählen Sie im Tray-Menü <b>Scan-Ergebnis anzeigen</b>. Standardmäßig liegen Scans in <code>Dokumente\\SpeedScanManager</code>; " +
            "den Ordner ändern Sie auf der Seite <a href='#save-tab'>Speichern</a>.</p>") +
        QA("Es wurde keine Datei gespeichert",
            "<p>Alle Seiten wurden als leer erkannt und entfernt – etwa wenn bei einseitigem Scan nur leere Rückseiten erfasst wurden. " +
            "Die Option dazu finden Sie in den <a href='#scanmode-options'>Scanmodus-Optionen</a>.</p>") +
        QA("Die Seite „Anwendung“ ist ausgegraut",
            "<p>Das Quick-Menü ist eingeschaltet – das Ziel wählen Sie dann nach jedem Scan. Möchten Sie ein festes Ziel, " +
            "schalten Sie <b>Quick-Menü verwenden</b> auf der Seite <b>Allgemein</b> aus.</p>") +
        QA("JPEG lässt sich nicht auswählen",
            "<p>Beim Farbmodus <b>Schwarzweiß</b> steht JPEG nicht zur Verfügung. Wählen Sie PDF oder PNG – oder einen anderen Farbmodus.</p>") +
        QA("Die Dateien sind zu groß",
            "<p>Wählen Sie auf der Seite <b>Allgemein</b> die Kachel <b>Kleine Datei</b>, oder erhöhen Sie die Stufe auf der Seite " +
            "<a href='#filesize-tab'>Dateigröße</a>. Für reinen Text spart <b>Schwarzweiß</b> am meisten.</p>") +
        QA("Seiten sind schief, stehen kopf oder sind falsch sortiert",
            "<p>In den <a href='#scanmode-options'>Scanmodus-Optionen</a>: <b>Schräglauf automatisch korrigieren</b>, " +
            "<b>Automatische Bilddrehung zulassen</b> und – wenn Sie mit der Schrift nach oben einlegen – <b>Dokumente mit der Vorderseite nach oben einlegen</b>.</p>") +
        QA("Die Texterkennung kennt meine Sprache nicht",
            "<p>Legen Sie die passende <code>.traineddata</code>-Datei in den Ordner <code>tessdata</code> neben <code>SpeedScanManager.exe</code> " +
            "(<a href='#filetype-tab'>mehr</a>).</p>") +
        QA("Der Scanner meldet „Mehrfacheinzug“",
            "<p>Lesen Sie <a href='#multifeed-measures'>Was tun bei Mehrfacheinzug?</a></p>") +
        Tip("Eine Liste aller Meldungen mit Erklärung finden Sie unter <a href='#message-list'>Liste der Meldungen</a>."),
        "message-list", "scanner-connection", "scan-button");

    private static HelpTopic TopicMultiFeedDialog() => Page("multifeed-dialog", "Mehrfacheinzug erkannt",
        "Der Scanner hat vermutlich zwei Blätter auf einmal eingezogen. Er wartet, bis Sie entscheiden.",
        Shot("multifeed_dialog", "Das Fenster „Mehrfacheinzug erkannt!“") + @"
        <p>Sie sehen die vorherige Seite und die betroffene Seite (rot markiert). Gibt es keine vorherige Seite, steht dort „Kein Vorschaubild verfügbar“.</p>
        " + Items(
            ("Wiederholen", "Verwirft die Seiten dieses Durchgangs und bricht den Scan ab („Scan verworfen“). Legen Sie die Seiten neu ein und scannen Sie erneut. " +
                            "Standardtaste (<span class='kbd'>Enter</span>); das Schließen-Symbol wirkt genauso."),
            ("So übernehmen", "Behält die Seite und scannt weiter."),
            ("Erkennung ausschalten", "Übernimmt die Seite und ignoriert weitere Meldungen für den Rest dieses Scans.")) +
        Warn("Umschläge oder Blätter mit aufgeklebten Zetteln, Fotos oder Briefmarken werden oft fälschlich als Mehrfacheinzug erkannt. " +
             "<span class='kbd'>Esc</span> schließt dieses Fenster nicht."),
        "multifeed-measures", "paper-tab");

    private static HelpTopic TopicMultiFeedMeasures() => Page("multifeed-measures", "Was tun bei Mehrfacheinzug?",
        "So entscheiden Sie richtig, wenn das Fenster „Mehrfacheinzug erkannt!“ erscheint.",
        @"
        <h2>Die Seiten sind vollständig und in der richtigen Reihenfolge</h2>
        <p>Klicken Sie auf <b>So übernehmen</b> – der Scan läuft weiter. Bei Umschlägen oder Blättern mit Klebezetteln, Fotos oder Briefmarken
        ebenso; folgen noch mehr solche Seiten, wählen Sie <b>Erkennung ausschalten</b>.</p>

        <h2>Es fehlt eine Seite oder die Reihenfolge stimmt nicht</h2>
        " + Steps(
            "Klicken Sie auf <b>Wiederholen</b>. Die Seiten dieses Durchgangs werden verworfen.",
            "Nehmen Sie alle Blätter aus dem Scanner – auch aus dem Ausgabefach – und richten Sie den Stapel bündig aus.",
            "Legen Sie den ganzen Stapel erneut ein und starten Sie einen neuen Scan.") +
        Tip("Kommt das häufig vor, fächern Sie den Stapel vor dem Einlegen kurz auf. Welche Erkennung verwendet wird, stellen Sie auf der Seite <a href='#paper-tab'>Papier</a> ein."),
        "multifeed-dialog");

    private static HelpTopic TopicMessageList() => Page("message-list", "Liste der Meldungen",
        "Alle Meldungen, die SpeedScan Manager anzeigen kann. „...“ steht für Namen, Pfade oder Fehlertexte.",
        GetMessageListHtml(),
        "faq");

    private static string GetMessageListHtml()
    {
        var windows = new[]
        {
            ("Ein Scanvorgang läuft bereits.", "Scan oder Scannerauswahl wurde während eines Scans gewählt."),
            ("TWAIN ist nicht initialisiert. Es ist kein Scanner verfügbar.", "Der TWAIN-Quellmanager konnte nicht gestartet werden – prüfen Sie die Treiberinstallation."),
            ("Der TWAIN-Quellmanager konnte nicht geöffnet werden.", ""),
            ("Es wurde kein Scanner gefunden.", ""),
            ("Der Scanner konnte nicht geöffnet werden. Ist er eingeschaltet und verbunden?", ""),
            ("Der Scanvorgang konnte nicht gestartet werden.", ""),
            ("Der Scanner hat nicht reagiert. Es wurden keine Seiten gescannt.", ""),
            ("Es wurden keine Seiten gescannt. Bitte legen Sie das Dokument in den Scanner ein und versuchen Sie es erneut.", ""),
            ("Alle gescannten Seiten wurden als leer erkannt und entfernt. Es wurde keine Datei gespeichert.", ""),
            ("Es wurde keine Datei erstellt.", ""),
            ("Der Scan konnte nicht gespeichert werden: ...", ""),
            ("Der Scan konnte nicht im Zielordner gespeichert werden ... Er wurde stattdessen hier gespeichert: Dokumente\\SpeedScanManager", ""),
            ("Der Zielordner konnte nicht erstellt werden ... Der Scan bleibt hier gespeichert: ...", ""),
            ("Folgende Dateien konnten nicht nach ... verschoben werden und bleiben in ...", ""),
            ("Scan verwerfen?", "Ja: Der Scan wird gelöscht. Nein: Zurück zum Fenster."),
            ("Das E-Mail-Programm konnte nicht mit der Datei als Anhang geöffnet werden.", "Der Ordner mit der Datei wird geöffnet, damit Sie sie von Hand anhängen können."),
            ("Es gibt keine Seiten zum Drucken.", ""),
            ("Es ist kein Drucker installiert. Der Scan wurde gespeichert, aber nicht gedruckt.", ""),
            ("Der Drucker \"...\" wurde nicht gefunden. Auf dem Standarddrucker drucken?", ""),
            ("Das Drucken ist fehlgeschlagen: ...", ""),
            ("Die Word-/Excel-/PowerPoint-Datei konnte nicht erstellt werden: ...", ""),
            ("Die Datei wurde erstellt, konnte aber nicht geöffnet werden (ist Word/Excel/PowerPoint installiert?)", ""),
            ("Die Bilder konnten nicht im Bilderordner gespeichert werden: ...", ""),
            ("Die Datei konnte nicht geöffnet werden: ...", ""),
            ("Das Programm \"...\" wurde nicht gefunden bzw. konnte nicht gestartet werden.", "Eigene Anwendung – der Scan bleibt gespeichert."),
            ("Der Ordner \"...\" existiert nicht.", "Bei „Scan-Ergebnis anzeigen“."),
            ("Es läuft gerade ein Scanvorgang. Scan abbrechen und SpeedScan Manager beenden?", ""),
            ("Der letzte Scan ist noch nicht abgeschlossen. SpeedScan Manager trotzdem beenden?", ""),
            ("Das PDF-Passwort konnte nicht gelesen werden, bitte neu eingeben.", "Einstellungen › Dateiart › Option › Kennwort."),
            ("Die Einrichtung der Scan-Taste wurde abgebrochen. / Die Scan-Taste konnte nicht eingerichtet werden.", ""),
            ("Es können höchstens 20 Profile angelegt werden.", ""),
            ("Das Standardprofil \"...\" kann nicht geändert werden. Möchten Sie die geänderten Einstellungen als neues Profil speichern?", ""),
            ("In SpeedScan Manager ist ein unerwarteter Fehler aufgetreten. Das Programm läuft weiter.", "Details stehen in %LocalAppData%\\SpeedScanManager\\crash.log."),
        };

        var balloons = new[]
        {
            ("Der Scanner ist nicht angeschlossen oder ausgeschaltet.", ""),
            ("TWAIN konnte nicht initialisiert werden. Es kann nicht gescannt werden.", ""),
            ("Scan abgebrochen", "Der Scanvorgang wurde abgebrochen."),
            ("Scan verworfen", "Mehrfacheinzug: Die Seiten wurden verworfen. Bitte legen Sie das Dokument erneut ein. – bzw. – Der Scan wurde gelöscht."),
            ("Scan gespeichert", "Dateiname und Ordner bzw. Anzahl der Dateien."),
            ("Drucken", "Das Dokument wurde an den Drucker gesendet."),
            ("Der Scan wird noch gespeichert. SpeedScan Manager wird danach beendet.", ""),
            ("Die Scan-Taste ist eingerichtet. Ein Druck auf die Taste startet einen Scan.", ""),
            ("Hinweis zum Scan: Der Scanner hat nicht mehr reagiert.", "Der Scanvorgang wurde abgebrochen."),
            ("Hinweis zum Scan: Eine oder mehrere Seiten konnten nicht übertragen werden.", ""),
            ("Hinweis zum Scan: Farbmodus / Flachbett / Duplex nicht unterstützt", "Es wird der Einzug verwendet bzw. einseitig gescannt."),
            ("Hinweis zum Scan: Die Auflösung wird nicht unterstützt", "Es wurde eine andere verwendet."),
            ("Hinweis zum Scan: Mehrfacheinzugserkennung", "Konnte nicht bzw. nur mit Ultraschall aktiviert werden."),
            ("Hinweis zum Scan: Papiergröße", "Die gewählte Größe wird nicht unterstützt bzw. keine gültige benutzerdefinierte Größe gewählt."),
            ("Hinweis: OCR-Sprachdaten fehlen", "Es wird eine andere Sprache verwendet bzw. die Texterkennung übersprungen."),
            ("Hinweis: Die Texterkennung ist nicht verfügbar", "tessdata fehlt oder die Erkennung ist fehlgeschlagen."),
            ("Hinweis: Die automatische Bilddrehung ist nicht verfügbar", "osd.traineddata fehlt."),
            ("Hinweis: Markierte Schlüsselwörter benötigen die Texterkennung", "Sie wurden übersprungen."),
            ("Hinweis: Zu wenig Arbeitsspeicher für die Schräglagenkorrektur", "Sie wurde übersprungen."),
        };

        static string Rows((string Text, string Info)[] list)
        {
            var sb = new StringBuilder("<table class='items'>");
            foreach (var (text, info) in list)
            {
                sb.Append("<tr><td>").Append("<b>").Append(System.Net.WebUtility.HtmlEncode(text)).Append("</b>");
                if (info.Length > 0)
                    sb.Append("<br><span style='font-size:9pt'>").Append(System.Net.WebUtility.HtmlEncode(info)).Append("</span>");
                sb.Append("</td></tr>");
            }
            return sb.Append("</table>").ToString();
        }

        return "<h2>Meldungsfenster</h2>" + Rows(windows) +
               "<h2>Sprechblasen am Tray-Symbol</h2>" + Rows(balloons);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Anhang
    // ═══════════════════════════════════════════════════════════════════════

    private static HelpTopic TopicAbbreviations() => Page("abbreviations", "Abkürzungen",
        "Fachbegriffe, die in dieser Hilfe vorkommen.",
        Grid(new[] { "Abkürzung", "Bedeutung" },
            new[] { "ADF", "Automatic Document Feeder – automatischer Dokumenteneinzug" },
            new[] { "dpi", "dots per inch – Bildpunkte pro Zoll (Auflösung)" },
            new[] { "Duplex / Simplex", "doppelseitig / einseitig scannen" },
            new[] { "JPEG", "verbreitetes Bildformat (verlustbehaftet)" },
            new[] { "MAPI", "Schnittstelle, über die Programme E-Mails erstellen" },
            new[] { "OCR", "Optical Character Recognition – Texterkennung" },
            new[] { "OSD", "Orientation and Script Detection – Erkennung von Ausrichtung und Schrift" },
            new[] { "PDF", "Portable Document Format" },
            new[] { "PNG", "Portable Network Graphics – verlustfreies Bildformat" },
            new[] { "S&amp;W", "Schwarzweiß" },
            new[] { "TWAIN", "Standard für die Verbindung zwischen Scanner und Software" },
            new[] { "UAC", "User Account Control – Windows-Benutzerkontensteuerung" },
            new[] { "USB", "Universal Serial Bus" },
            new[] { "WIA / STI", "Windows-Schnittstellen für Bildgeräte, u. a. für die Scan-Taste" }));
}
