using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SpeedScanManager;

/// <summary>
/// Guesses the language of a page in Latin script from recognized text: OSD only knows the
/// script, and all Latin languages look alike to it. Counts frequent short words (accents
/// stripped, because the first recognition pass with deu+eng loses most of them), Turkish
/// suffixes and a few telltale letters. Measured on test pages of all supported Latin
/// languages: 18 of 18 right (normal and small/blurry print).
/// </summary>
internal static class LatinLanguageGuess
{
    private static readonly Dictionary<string, HashSet<string>> Words = new()
    {
        ["deu"] = Set("der die das und ist nicht ein eine zu den von mit sich des auf fur im dem auch es an als bei sie wir ihr oder wird sind vom zum zur uber"),
        ["eng"] = Set("the and of to in is that for it with as on be at by this are or from an was not you your we have will"),
        ["fra"] = Set("le la les des et est un une du en que qui pour dans sur au aux pas ce cette vous nous avec par ou sont il elle"),
        ["spa"] = Set("el la los las de y que en un una por para con del al es se su sus no lo como mas pero le nos les usted"),
        ["por"] = Set("o a os as de e que em um uma do da dos das para com nao no na por se ao sao ser mais foi pelo pela voce"),
        ["ita"] = Set("il la lo gli le di e che un una per con del della dei delle in non si al alla sono da nel nella ci vi"),
        ["nld"] = Set("de het een en van is dat op te in voor niet met zijn ook maar aan bij of er wordt uw u om dan deze"),
        ["pol"] = Set("i w z na sie nie do to jest ze o po za od jak tak dla czy przez ale lub oraz jego jej sa pan"),
        ["tur"] = Set("ve bir bu da de ile icin olan gibi daha cok ne var en mi o sizin size her ama veya kadar"),
    };

    /// <summary>Letters only some languages use (when the recognition keeps them).</summary>
    private static readonly Dictionary<string, string> Letters = new()
    {
        ["pol"] = "łśżźąęńć", ["tur"] = "şğı", ["por"] = "ãõ", ["spa"] = "ñ¿¡", ["fra"] = "œêèâû", ["ita"] = "ò", ["deu"] = "äöüß",
    };

    // Turkish attaches suffixes instead of using short words
    private static readonly Regex TurkishSuffix = new(
        "(iniz|unuz|siniz|acak|ecek|tir|dir|larin|lerin|sinda|sinde|inda|inde)$", RegexOptions.CultureInvariant);

    private static readonly Regex Token = new(@"[^\W\d_]+", RegexOptions.CultureInvariant);

    private static HashSet<string> Set(string words) => new(words.Split(' '), StringComparer.Ordinal);

    /// <summary>
    /// The language of <paramref name="text"/> if it is clearly NOT German/English:
    /// another Latin language with at least 4 points and twice the German and the English
    /// score. Null for German/English text, too little text or no clear winner.
    /// </summary>
    public static string? GuessOther(string text)
    {
        string low = text.ToLowerInvariant();
        var tokens = Token.Matches(low).Select(m => StripAccents(m.Value)).ToList();
        if (tokens.Count < 8) return null;

        var score = Words.ToDictionary(kv => kv.Key, kv => tokens.Count(kv.Value.Contains));
        score["tur"] += tokens.Count(t => t.Length >= 6 && TurkishSuffix.IsMatch(t));
        foreach (var (lang, letters) in Letters)
            score[lang] += 2 * low.Count(letters.Contains);

        var best = score.Where(kv => kv.Key is not ("deu" or "eng")).MaxBy(kv => kv.Value);
        int known = Math.Max(score["deu"], score["eng"]);
        int second = score.Where(kv => kv.Key != best.Key && kv.Key is not ("deu" or "eng")).Max(kv => kv.Value);
        return best.Value >= 4 && best.Value >= 2 * known && best.Value > second ? best.Key : null;
    }

    private static string StripAccents(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString();
    }
}
