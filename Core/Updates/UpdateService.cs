using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace SpeedScanManager;

/// <summary>A newer release on GitHub.</summary>
internal sealed record UpdateInfo(Version Version, string Title, string Notes, string PageUrl,
    string SetupUrl, string SetupName, long SetupSize, string? Sha256);

/// <summary>
/// Looks for a newer release of SpeedScan Manager on GitHub and downloads its setup
/// (like the Modrinth App: checked in the background, installed only when the user says so).
/// Only the public release list is requested; nothing about the user is sent.
/// </summary>
internal static class UpdateService
{
    private const string Repo = "VeridonNetzwerk/SpeedScanManager";
    private const string LatestUrl = "https://api.github.com/repos/" + Repo + "/releases/latest";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SpeedScanManager", CurrentVersionText));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return http;
    }

    /// <summary>Installed version (Major.Minor.Build).</summary>
    public static Version CurrentVersion
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        }
    }

    public static string CurrentVersionText => Format(CurrentVersion);

    public static string Format(Version v) => $"{v.Major}.{v.Minor}.{Math.Max(0, v.Build)}";

    /// <summary>The latest release if it is newer than this version, otherwise null.</summary>
    public static async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        using var response = await Http.GetAsync(LatestUrl, ct).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null; // no release yet
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        var root = doc.RootElement;

        if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean()) return null;
        if (root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()) return null;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!TryParseVersion(tag, out var version) || version <= CurrentVersion)
            return null;

        // The setup asset ("SpeedScanManager-x.y.z-Setup-win-x86.exe")
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            // "SpeedScanManager-x.y.z-Setup-win-x86.exe" (older releases: "SpeedScanInstaller.exe")
            if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                || (name.IndexOf("setup", StringComparison.OrdinalIgnoreCase) < 0
                    && name.IndexOf("installer", StringComparison.OrdinalIgnoreCase) < 0)) continue;
            var url = asset.GetProperty("browser_download_url").GetString() ?? "";
            if (!IsGitHubUrl(url)) continue;
            string? sha = null;
            if (asset.TryGetProperty("digest", out var digest) && digest.ValueKind == JsonValueKind.String
                && digest.GetString() is { } d && d.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                sha = d[7..].ToLowerInvariant();
            return new UpdateInfo(
                version,
                root.TryGetProperty("name", out var n) && n.GetString() is { Length: > 0 } title ? title : $"SpeedScan Manager {Format(version)}",
                root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "",
                root.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : $"https://github.com/{Repo}/releases",
                url, name, asset.GetProperty("size").GetInt64(), sha);
        }
        return null; // a release without setup can't be installed from here
    }

    /// <summary>
    /// Downloads the setup into the temp folder and checks size and checksum. Returns its path.
    /// </summary>
    public static async Task<string> DownloadAsync(UpdateInfo info, IProgress<double>? progress, CancellationToken ct)
    {
        var dir = Path.Combine(Path.GetTempPath(), "SpeedScanManager", L.T("Update"));
        Directory.CreateDirectory(dir);
        var target = Path.Combine(dir, Path.GetFileName(info.SetupName));
        var partial = target + ".part";

        using (var response = await Http.GetAsync(info.SetupUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();
            var final = response.RequestMessage?.RequestUri?.AbsoluteUri ?? info.SetupUrl;
            if (!IsGitHubUrl(final))
                throw new InvalidOperationException(L.T("Der Download wurde auf eine unbekannte Adresse umgeleitet."));
            long total = response.Content.Headers.ContentLength ?? info.SetupSize;
            await using var input = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                done += read;
                if (total > 0) progress?.Report(Math.Min(1.0, done / (double)total));
            }
        }

        var length = new FileInfo(partial).Length;
        if (info.SetupSize > 0 && length != info.SetupSize)
        {
            File.Delete(partial);
            throw new InvalidOperationException(L.T("Der Download ist unvollständig. Bitte erneut versuchen."));
        }
        if (info.Sha256 != null)
        {
            string actual;
            await using (var fs = File.OpenRead(partial))
                actual = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct).ConfigureAwait(false)).ToLowerInvariant();
            if (actual != info.Sha256)
            {
                File.Delete(partial);
                throw new InvalidOperationException(L.T("Die heruntergeladene Datei ist beschädigt (Prüfsumme stimmt nicht)."));
            }
        }
        File.Move(partial, target, overwrite: true);
        return target;
    }

    /// <summary>
    /// Release notes as plain text (Markdown marks removed; the release title and a
    /// "Downloads" section are left out, the window shows the version itself).
    /// </summary>
    public static string PlainNotes(string markdown)
    {
        var lines = new List<string>();
        bool skipSection = false;
        foreach (var raw in markdown.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            bool isList = line.StartsWith("- ") || line.StartsWith("* ");
            if (line.Length > 0 && !isList && (line.StartsWith('#') || lines.Count == 0 || lines[^1].Length == 0))
            {
                var heading = line.TrimStart('#').Trim();
                if (lines.Count == 0 && heading.StartsWith("SpeedScan", StringComparison.OrdinalIgnoreCase))
                    continue; // title
                skipSection = heading.Equals("Downloads", StringComparison.OrdinalIgnoreCase)
                              || heading.Equals("Download", StringComparison.OrdinalIgnoreCase);
            }
            if (skipSection) continue;
            if (line.Length == 0) { if (lines.Count > 0 && lines[^1].Length > 0) lines.Add(""); continue; }
            line = line.TrimStart('#').Trim();
            if (line.StartsWith("- ") || line.StartsWith("* ")) line = "•  " + line[2..];
            line = System.Text.RegularExpressions.Regex.Replace(line, @"\*\*(.+?)\*\*|__(.+?)__", "$1$2");
            line = System.Text.RegularExpressions.Regex.Replace(line, @"`([^`]+)`", "$1");
            line = System.Text.RegularExpressions.Regex.Replace(line, @"\[([^\]]+)\]\([^)]+\)", "$1");
            lines.Add(line);
        }
        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        return string.Join("\n", lines);
    }

    private static bool TryParseVersion(string tag, out Version version)
    {
        var text = tag.Trim().TrimStart('v', 'V');
        int dash = text.IndexOfAny(new[] { '-', '+' });
        if (dash >= 0) text = text[..dash];
        if (Version.TryParse(text, out var v))
        {
            version = new Version(v.Major, v.Minor, Math.Max(0, v.Build));
            return true;
        }
        version = new Version(0, 0, 0);
        return false;
    }

    private static bool IsGitHubUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps &&
        (u.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
         || u.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase));
}
