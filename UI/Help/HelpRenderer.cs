using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace SpeedScanManager;

/// <summary>
/// Turns <see cref="HelpTopic"/>s into complete HTML pages for the help window (Internet Explorer
/// WebBrowser control, IE11 document mode): themed style sheet, start page, group pages,
/// breadcrumb and "related topics" chips.
///
/// Pictures: topic HTML references them as <c>help-img:NAME</c>. They are embedded resources
/// (<c>Resources\Help\NAME_light.png</c> / <c>NAME_dark.png</c>, or <c>NAME.png</c> for both themes,
/// logical name <c>Help.FILE</c>) and are inserted as data URIs for the current theme
/// (IE11 mode has no practical size limit for data URIs). Icons are drawn with
/// <see cref="ThemeGlyphs"/> at run time.
/// </summary>
internal static class HelpRenderer
{
    private const string ResourcePrefix = "Help.";
    private const string EmptyGif = "data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7";
    private static readonly Dictionary<string, string> ImageCache = new();
    private static readonly Regex ImageRef = new(@"help-img:([A-Za-z0-9_]+)", RegexOptions.Compiled);

    // ───────────────────────── pages ─────────────────────────

    /// <summary>Complete HTML document for <paramref name="topic"/> in the colors of <paramref name="p"/>.</summary>
    public static string RenderPage(HelpTopic topic, ThemePalette p)
    {
        var body = new StringBuilder();
        if (topic.Parent == null)
            AppendStartPage(body, topic, p);
        else if (topic.Children.Count > 0)
            AppendGroupPage(body, topic, p);
        else
            AppendTopicPage(body, topic, p);

        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html><head><meta http-equiv='X-UA-Compatible' content='IE=edge'><meta charset='utf-8'>");
        html.Append("<style>").Append(Css(p)).Append("</style></head><body>");
        html.Append(ImageRef.Replace(body.ToString(), m => ImageUri(m.Groups[1].Value, p.IsDark)));
        html.Append("</body></html>");
        return html.ToString();
    }

    private static void AppendTopicPage(StringBuilder sb, HelpTopic topic, ThemePalette p)
    {
        AppendCrumb(sb, topic);
        sb.Append("<h1>").Append(topic.Title).Append("</h1>");
        if (topic.Summary.Length > 0)
            sb.Append("<p class='lead'>").Append(topic.Summary).Append("</p>");
        sb.Append(topic.Html);
        if (topic.Related.Length > 0)
        {
            sb.Append("<div class='related'><div class='related-title'>Passende Themen</div>");
            foreach (var id in topic.Related)
            {
                var target = topic.Root.Find(id);
                if (target == null) continue;
                sb.Append("<a class='chip' href='#").Append(target.Id).Append("'>").Append(target.Title).Append("</a> ");
            }
            sb.Append("</div>");
        }
    }

    private static void AppendGroupPage(StringBuilder sb, HelpTopic topic, ThemePalette p)
    {
        AppendCrumb(sb, topic);
        sb.Append("<table class='group-head'><tr><td class='gh-icon'>").Append(GlyphTile(topic.Glyph, p, 44, 24)).Append("</td><td>");
        sb.Append("<h1>").Append(topic.Title).Append("</h1>");
        if (topic.Summary.Length > 0)
            sb.Append("<p class='lead'>").Append(topic.Summary).Append("</p>");
        sb.Append("</td></tr></table>");
        sb.Append("<div class='list'>");
        foreach (var child in topic.Children)
        {
            sb.Append("<a class='row' href='#").Append(child.Id).Append("'><table><tr>");
            sb.Append("<td class='row-icon'>").Append(GlyphImg(child.Glyph != Glyph.None ? child.Glyph : Glyph.FileType, p.TextMuted, 18)).Append("</td>");
            sb.Append("<td><span class='row-title'>").Append(child.Title).Append("</span>");
            if (child.Summary.Length > 0)
                sb.Append("<span class='row-text'>").Append(child.Summary).Append("</span>");
            sb.Append("</td><td class='row-chev'>").Append(GlyphImg(Glyph.ChevronRight, p.TextMuted, 14)).Append("</td></tr></table></a>");
        }
        sb.Append("</div>");
    }

    private static void AppendStartPage(StringBuilder sb, HelpTopic root, ThemePalette p)
    {
        sb.Append("<table class='hero'><tr><td class='hero-logo'><img src='").Append(LogoUri()).Append("' width='56' height='56' alt=''></td><td>");
        sb.Append("<h1>Willkommen bei SpeedScan Manager</h1>");
        sb.Append("<p class='lead'>").Append(root.Summary).Append("</p></td></tr></table>");

        // Three-step flow
        sb.Append("<h2>So einfach geht’s</h2><table class='flow'><tr>");
        var steps = new (Glyph Glyph, string Title, string Text)[]
        {
            (Glyph.Paper, "Dokument einlegen", "Legen Sie die Seiten in den Einzug des Scanners."),
            (Glyph.Scanner, "Scan starten", "Drücken Sie die Scan-Taste am Scanner oder wählen Sie <b>Scan</b> im Tray-Menü."),
            (Glyph.Folder, "Ziel wählen", "Im <b>Quick-Menü</b> entscheiden Sie, was mit dem Scan passiert."),
        };
        for (int i = 0; i < steps.Length; i++)
        {
            if (i > 0) sb.Append("<td class='flow-arrow'>").Append(GlyphImg(Glyph.ChevronRight, p.TextMuted, 16)).Append("</td>");
            sb.Append("<td class='flow-step'><div class='flow-top'>").Append(GlyphTile(steps[i].Glyph, p, 36, 20))
              .Append("<span class='flow-num'>Schritt ").Append(i + 1).Append("</span></div>");
            sb.Append("<div class='flow-title'>").Append(steps[i].Title).Append("</div><div class='flow-text'>").Append(steps[i].Text).Append("</div></td>");
        }
        sb.Append("</tr></table>");
        sb.Append("<p class='more'><a href='#scan-quick'>Schritt für Schritt mit Bildern ansehen</a></p>");

        // Topic cards (2 columns)
        sb.Append("<h2>Themen</h2><table class='cards'>");
        for (int i = 0; i < root.Children.Count; i += 2)
        {
            sb.Append("<tr>");
            for (int j = i; j < i + 2; j++)
            {
                if (j >= root.Children.Count) { sb.Append("<td class='card-empty'></td>"); continue; }
                var g = root.Children[j];
                sb.Append("<td class='card'><a href='#").Append(g.Id).Append("'><table><tr><td class='card-icon'>")
                  .Append(GlyphTile(g.Glyph, p, 36, 20)).Append("</td><td><span class='card-title'>").Append(g.Title)
                  .Append("</span><span class='card-text'>").Append(g.Summary).Append("</span></td></tr></table></a></td>");
            }
            sb.Append("</tr>");
        }
        sb.Append("</table>");

        // Frequent questions
        sb.Append("<h2>Häufige Fragen</h2><div class='list'>");
        foreach (var (id, question) in HelpContent.StartQuestions)
        {
            sb.Append("<a class='row' href='#").Append(id).Append("'><table><tr><td class='row-icon'>")
              .Append(GlyphImg(Glyph.Question, p.Accent, 18)).Append("</td><td><span class='row-title'>").Append(question)
              .Append("</span></td><td class='row-chev'>").Append(GlyphImg(Glyph.ChevronRight, p.TextMuted, 14)).Append("</td></tr></table></a>");
        }
        sb.Append("</div>");
        sb.Append("<p class='foot'>Tipp: Über das <b>?</b> oben rechts in jedem Fenster öffnen Sie direkt die passende Hilfeseite.</p>");
    }

    private static void AppendCrumb(StringBuilder sb, HelpTopic topic)
    {
        var chain = new List<HelpTopic>();
        for (var t = topic.Parent; t != null; t = t.Parent)
            chain.Insert(0, t);
        sb.Append("<div class='crumb'>");
        for (int i = 0; i < chain.Count; i++)
        {
            if (i > 0) sb.Append("<span class='sep'>›</span>");
            sb.Append("<a href='#").Append(chain[i].Id).Append("'>").Append(i == 0 ? "Hilfe" : chain[i].Title).Append("</a>");
        }
        sb.Append("</div>");
    }

    // ───────────────────────── images ─────────────────────────

    /// <summary>Data URI of the help picture <paramref name="name"/> for the light or dark theme.</summary>
    public static string ImageUri(string name, bool dark)
    {
        string key = name + (dark ? "|d" : "|l");
        lock (ImageCache)
        {
            if (ImageCache.TryGetValue(key, out var cached))
                return cached;
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream($"{ResourcePrefix}{name}_{(dark ? "dark" : "light")}.png")
                               ?? asm.GetManifestResourceStream($"{ResourcePrefix}{name}.png");
            string uri = EmptyGif;
            if (stream != null)
            {
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                uri = "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
            }
            ImageCache[key] = uri;
            return uri;
        }
    }

    private static string LogoUri()
    {
        lock (ImageCache)
        {
            if (ImageCache.TryGetValue("logo", out var cached))
                return cached;
            string uri = EmptyGif;
            try
            {
                using var bmp = new Bitmap(112, 112, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(AppResources.Logo, 0, 0, 112, 112);
                }
                uri = PngUri(bmp);
            }
            catch (Exception)
            {
                // logo missing: page without logo
            }
            ImageCache["logo"] = uri;
            return uri;
        }
    }

    /// <summary>A line icon as &lt;img&gt; (drawn at twice the size for high DPI).</summary>
    public static string GlyphImg(Glyph glyph, Color color, int size)
    {
        string key = $"g|{glyph}|{color.ToArgb()}|{size}";
        string uri;
        lock (ImageCache)
        {
            if (!ImageCache.TryGetValue(key, out uri!))
            {
                using var bmp = new Bitmap(size * 2, size * 2, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    ThemeGlyphs.Draw(g, glyph, new RectangleF(0, 0, size * 2, size * 2), color);
                }
                uri = PngUri(bmp);
                ImageCache[key] = uri;
            }
        }
        return $"<img class='glyph' src='{uri}' width='{size}' height='{size}' alt=''>";
    }

    /// <summary>Rounded tile in a light accent tint with an accent icon.</summary>
    private static string GlyphTile(Glyph glyph, ThemePalette p, int tile, int icon)
        => $"<span class='gtile' style='width:{tile}px;height:{tile}px;line-height:{tile}px'>{GlyphImg(glyph, p.Accent, icon)}</span>";

    private static string PngUri(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
    }

    // ───────────────────────── style sheet ─────────────────────────

    private static string H(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    /// <summary>
    /// Style sheet for <paramref name="p"/>. IE has no CSS custom properties, so the theme colors
    /// are written into the rules. Layout uses tables (equal-height cards in IE11).
    /// </summary>
    public static string Css(ThemePalette p)
    {
        bool dark = p.IsDark;
        var bg = p.Background;
        var card = p.Card;
        var cardBorder = p.CardBorder;
        var divider = p.CardDivider;
        var link = dark ? Color.FromArgb(0x00, 0xA8, 0xFC) : Color.FromArgb(0x00, 0x5F, 0xCC);
        var accentTint = ThemePalette.Blend(card, p.Accent, dark ? 0.20f : 0.11f);
        var tipBg = ThemePalette.Blend(card, p.Accent, dark ? 0.10f : 0.06f);
        var noteBg = ThemePalette.Blend(card, p.Warning, dark ? 0.09f : 0.08f);
        var warnBg = ThemePalette.Blend(card, p.Danger, dark ? 0.09f : 0.06f);
        var tipText = dark ? ThemePalette.Blend(p.Accent, Color.White, 0.35f) : p.AccentPressed;
        var noteText = dark ? p.Warning : Color.FromArgb(0x8A, 0x5A, 0x00);
        var warnText = dark ? Color.FromArgb(0xF2, 0x6B, 0x6E) : Color.FromArgb(0xC0, 0x24, 0x29);
        var hover = dark ? ThemePalette.Blend(card, Color.White, 0.045f) : ThemePalette.Blend(card, p.Accent, 0.04f);
        var hoverBorder = ThemePalette.Blend(cardBorder, p.Accent, dark ? 0.45f : 0.40f);
        var codeBg = p.Tertiary;
        var thumb = dark ? Color.FromArgb(0x1A, 0x1B, 0x1E) : Color.FromArgb(0xC4, 0xC9, 0xCE);
        string shadow = dark ? "0 2px 10px rgba(0,0,0,0.35)" : "0 2px 10px rgba(30,40,60,0.10)";
        string semi = "font-family: 'Segoe UI Semibold', 'Segoe UI', sans-serif; font-weight: normal;";

        return $@"
html, body {{ scrollbar-face-color: {H(thumb)}; scrollbar-track-color: {H(bg)}; scrollbar-arrow-color: {H(p.TextMuted)};
    scrollbar-shadow-color: {H(thumb)}; scrollbar-highlight-color: {H(thumb)}; scrollbar-3dlight-color: {H(bg)};
    scrollbar-darkshadow-color: {H(bg)}; scrollbar-base-color: {H(bg)}; background: {H(bg)}; }}
body {{ font-family: 'Segoe UI', Tahoma, sans-serif; font-size: 10pt; color: {H(p.TextSecondary)}; background: {H(bg)};
    margin: 22px 30px 36px 30px; line-height: 1.55; max-width: 780px; }}
h1 {{ {semi} color: {H(p.Text)}; font-size: 17pt; line-height: 1.25; margin: 0 0 6px 0; }}
h2 {{ {semi} color: {H(p.Text)}; font-size: 12pt; margin: 28px 0 10px 0; }}
h3 {{ {semi} color: {H(p.Text)}; font-size: 10.5pt; margin: 18px 0 4px 0; }}
p {{ margin: 0 0 10px 0; }}
b, strong {{ {semi} color: {H(p.Text)}; }}
a {{ color: {H(link)}; text-decoration: none; }}
a:hover {{ text-decoration: underline; }}
ul {{ margin: 4px 0 12px 0; padding-left: 20px; }}
li {{ margin: 3px 0; }}
img.glyph {{ border: 0; vertical-align: middle; -ms-interpolation-mode: bicubic; }}
.lead {{ color: {H(p.TextMuted)}; font-size: 10.5pt; margin: 0 0 18px 0; }}
.crumb {{ font-size: 8.5pt; color: {H(p.TextMuted)}; margin: 0 0 6px 0; }}
.crumb a {{ color: {H(p.TextMuted)}; }}
.crumb a:hover {{ color: {H(link)}; }}
.crumb .sep {{ margin: 0 6px; }}
.gtile {{ display: inline-block; text-align: center; vertical-align: middle; border-radius: 10px; background: {H(accentTint)}; }}
.gtile img {{ vertical-align: middle; }}

/* numbered steps */
ol.steps {{ list-style: none; margin: 8px 0 16px 0; padding: 0; counter-reset: step; }}
ol.steps > li {{ position: relative; padding: 2px 0 8px 40px; margin: 0; counter-increment: step; min-height: 26px; }}
ol.steps > li:before {{ content: counter(step); position: absolute; left: 0; top: 0; width: 26px; height: 26px; line-height: 26px;
    border-radius: 13px; background: {H(p.Accent)}; color: #FFFFFF; text-align: center; {semi} font-size: 9.5pt; }}

/* pictures */
.shot {{ margin: 12px 0 20px 0; }}
.shot img {{ max-width: 100%; height: auto; border: 1px solid {H(cardBorder)}; border-radius: 8px; box-shadow: {shadow}; -ms-interpolation-mode: bicubic; }}
.shot.flat img {{ box-shadow: none; }}
.caption {{ color: {H(p.TextMuted)}; font-size: 8.5pt; margin-top: 6px; }}
table.legend {{ border-collapse: collapse; margin: 0 0 16px 0; }}
table.legend td {{ padding: 4px 10px 4px 0; vertical-align: top; }}
.num {{ display: inline-block; width: 22px; height: 22px; line-height: 22px; border-radius: 11px; background: {H(p.Accent)}; color: #FFFFFF;
    text-align: center; {semi} font-size: 9pt; }}

/* tip / note / warning */
.box {{ border: 1px solid {H(cardBorder)}; border-left-width: 4px; border-radius: 8px; padding: 10px 16px; margin: 14px 0 16px 0; }}
.box p, .box ul {{ margin: 2px 0 2px 0; }}
.box-title {{ {semi} font-size: 9pt; margin-bottom: 2px; }}
.tip {{ background: {H(tipBg)}; border-color: {H(ThemePalette.Blend(cardBorder, p.Accent, 0.35f))}; border-left-color: {H(p.Accent)}; }}
.tip .box-title {{ color: {H(tipText)}; }}
.note {{ background: {H(noteBg)}; border-color: {H(ThemePalette.Blend(cardBorder, p.Warning, 0.35f))}; border-left-color: {H(p.Warning)}; }}
.note .box-title {{ color: {H(noteText)}; }}
.warn {{ background: {H(warnBg)}; border-color: {H(ThemePalette.Blend(cardBorder, p.Danger, 0.35f))}; border-left-color: {H(p.Danger)}; }}
.warn .box-title {{ color: {H(warnText)}; }}

/* element lists and tables */
table.items, table.grid {{ width: 100%; border-collapse: separate; border-spacing: 0; margin: 8px 0 16px 0;
    background: {H(card)}; border: 1px solid {H(cardBorder)}; border-radius: 8px; }}
table.items td {{ padding: 9px 14px; border-top: 1px solid {H(divider)}; vertical-align: top; }}
table.items tr:first-child td {{ border-top: 0; }}
table.items td.term {{ width: 34%; {semi} color: {H(p.Text)}; }}
table.grid th, table.grid td {{ padding: 7px 14px; text-align: left; border-top: 1px solid {H(divider)}; font-size: 9.5pt; }}
table.grid th {{ {semi} color: {H(p.Text)}; border-top: 0; background: {H(dark ? p.Tertiary : p.Secondary)}; }}
table.grid tr:first-child th:first-child {{ border-top-left-radius: 7px; }}
table.grid tr:first-child th:last-child {{ border-top-right-radius: 7px; }}
.kbd, kbd {{ background: {H(codeBg)}; color: {H(p.Text)}; border: 1px solid {H(dark ? p.Border : p.InputBorder)}; border-bottom-width: 2px;
    border-radius: 4px; padding: 0 5px; font-family: 'Segoe UI', sans-serif; font-size: 8.5pt; white-space: nowrap; }}
code {{ font-family: Consolas, monospace; font-size: 9pt; color: {H(p.Text)}; background: {H(codeBg)}; border-radius: 4px; padding: 1px 5px; }}
.status {{ display: inline-block; padding: 1px 8px; border-radius: 10px; font-size: 8.5pt; {semi} }}

/* questions (FAQ) */
.qa {{ background: {H(card)}; border: 1px solid {H(cardBorder)}; border-radius: 8px; padding: 12px 16px; margin: 0 0 10px 0; }}
.qa h3 {{ margin: 0 0 4px 0; }}
.qa p, .qa ul, .qa ol {{ margin: 2px 0 4px 0; }}

/* group page / start page lists */
table.group-head {{ border-collapse: collapse; margin: 0 0 6px 0; }}
table.group-head td {{ vertical-align: top; padding: 0; }}
td.gh-icon {{ width: 60px; padding-top: 2px !important; }}
.list {{ background: {H(card)}; border: 1px solid {H(cardBorder)}; border-radius: 8px; margin: 6px 0 18px 0; overflow: hidden; }}
a.row {{ display: block; border-top: 1px solid {H(divider)}; color: {H(p.TextSecondary)}; text-decoration: none; }}
a.row:first-child {{ border-top: 0; }}
a.row:hover {{ background: {H(hover)}; text-decoration: none; }}
a.row table {{ width: 100%; border-collapse: collapse; }}
a.row td {{ padding: 10px 14px 10px 0; vertical-align: middle; }}
td.row-icon {{ width: 22px; padding-left: 14px !important; }}
td.row-chev {{ width: 14px; }}
.row-title {{ display: block; {semi} color: {H(p.Text)}; }}
.row-text {{ display: block; color: {H(p.TextMuted)}; font-size: 9pt; margin-top: 1px; }}
.related {{ margin: 30px 0 0 0; padding-top: 14px; border-top: 1px solid {H(divider)}; }}
.related-title {{ {semi} font-size: 8pt; letter-spacing: 0.05em; text-transform: uppercase; color: {H(p.TextMuted)}; margin-bottom: 8px; }}
a.chip {{ display: inline-block; margin: 0 6px 8px 0; padding: 4px 12px; border-radius: 14px; background: {H(card)};
    border: 1px solid {H(cardBorder)}; color: {H(p.Text)}; font-size: 9pt; }}
a.chip:hover {{ border-color: {H(hoverBorder)}; background: {H(hover)}; text-decoration: none; }}

/* start page */
table.hero {{ border-collapse: collapse; margin: 0 0 4px 0; }}
table.hero td {{ vertical-align: middle; padding: 0; }}
td.hero-logo {{ width: 72px; }}
td.hero-logo img {{ display: block; -ms-interpolation-mode: bicubic; }}
table.hero .lead {{ margin: 0; }}
table.flow {{ width: 100%; border-collapse: collapse; margin: 4px 0 6px 0; }}
td.flow-step {{ width: 31%; vertical-align: top; background: {H(card)}; border: 1px solid {H(cardBorder)}; border-radius: 8px; padding: 14px 16px; }}
td.flow-arrow {{ width: 3%; text-align: center; vertical-align: middle; }}
.flow-top {{ margin-bottom: 8px; }}
.flow-num {{ {semi} color: {H(p.TextMuted)}; font-size: 8pt; letter-spacing: 0.05em; text-transform: uppercase; margin-left: 10px; vertical-align: middle; }}
.flow-title {{ {semi} color: {H(p.Text)}; }}
.flow-text {{ font-size: 9pt; color: {H(p.TextMuted)}; margin-top: 2px; }}
.flow-text b {{ color: {H(p.TextSecondary)}; }}
p.more {{ margin: 8px 0 0 0; font-size: 9.5pt; }}
table.cards {{ width: calc(100% + 20px); border-collapse: separate; border-spacing: 10px; margin: -10px -10px 0 -10px; }}
td.card {{ width: 50%; padding: 0; vertical-align: top; background: {H(card)}; border: 1px solid {H(cardBorder)}; border-radius: 8px; }}
td.card:hover {{ background: {H(hover)}; border-color: {H(hoverBorder)}; }}
td.card-empty {{ width: 50%; }}
td.card a {{ display: block; color: {H(p.TextSecondary)}; text-decoration: none; }}
td.card a:hover {{ text-decoration: none; }}
td.card table {{ border-collapse: collapse; }}
td.card td {{ padding: 12px 14px 12px 0; vertical-align: top; }}
td.card-icon {{ width: 36px; padding-left: 14px !important; }}
.card-title {{ display: block; {semi} color: {H(p.Text)}; }}
.card-text {{ display: block; font-size: 9pt; color: {H(p.TextMuted)}; margin-top: 1px; }}
p.foot {{ color: {H(p.TextMuted)}; font-size: 9pt; margin-top: 18px; }}
::selection {{ background: {H(ThemePalette.Blend(bg, p.Accent, 0.45f))}; }}
";
    }

    // ───────────────────────── text helpers ─────────────────────────

    /// <summary>Visible text of an HTML fragment (markup removed, entities decoded) for the search.</summary>
    public static string PlainText(string html)
    {
        var text = Regex.Replace(html, @"<(style|script)\b[^>]*>.*?</\1\s*>", " ", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<[^>]*>", " ");
        return Regex.Replace(WebUtility.HtmlDecode(text), @"\s+", " ").Trim();
    }
}
