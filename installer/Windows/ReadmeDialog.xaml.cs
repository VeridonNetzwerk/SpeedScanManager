using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using Window = System.Windows.Window;
using RoutedEventArgs = System.Windows.RoutedEventArgs;

namespace SpeedScanInstaller;

public partial class ReadmeDialog : Window
{
    private const string ReadmeUrl = "https://raw.githubusercontent.com/VeridonNetzwerk/SpeedScanManager/refs/heads/main/README.md";

    public ReadmeDialog()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadReadme();
    }

    private void OnDragMove(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async Task LoadReadme()
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            var markdown = await client.GetStringAsync(ReadmeUrl);
            var html = MarkdownToHtml(markdown);
            var fullHtml = WrapHtml(html);
            Browser.NavigateToString(fullHtml);

            LoadingPanel.Visibility = Visibility.Collapsed;
            Browser.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            ErrorText.Text = $"README konnte nicht geladen werden:\n\n{ex.Message}\n\nURL: {ReadmeUrl}";
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private static string WrapHtml(string body)
    {
        return $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"">
<style>
  body {{
    font-family: 'Segoe UI', sans-serif;
    font-size: 14px;
    line-height: 1.6;
    color: #333;
    max-width: 680px;
    margin: 24px auto;
    padding: 0 20px;
  }}
  h1, h2, h3, h4, h5, h6 {{
    color: #1c2633;
    margin-top: 24px;
    margin-bottom: 12px;
  }}
  h1 {{ font-size: 28px; border-bottom: 2px solid #e0e4e8; padding-bottom: 8px; }}
  h2 {{ font-size: 22px; border-bottom: 1px solid #e0e4e8; padding-bottom: 6px; }}
  h3 {{ font-size: 18px; }}
  a {{ color: #2a6ad8; text-decoration: none; }}
  a:hover {{ text-decoration: underline; }}
  code {{
    font-family: 'Cascadia Code', 'Consolas', monospace;
    background: #f0f2f5;
    padding: 2px 6px;
    border-radius: 3px;
    font-size: 13px;
  }}
  pre {{
    background: #1c2633;
    color: #dfe6ef;
    padding: 16px;
    border-radius: 6px;
    overflow-x: auto;
  }}
  pre code {{
    background: transparent;
    color: inherit;
    padding: 0;
  }}
  ul, ol {{ padding-left: 24px; }}
  li {{ margin: 4px 0; }}
  img {{ max-width: 100%; height: auto; }}
  blockquote {{
    border-left: 4px solid #2a6ad8;
    margin: 12px 0;
    padding: 8px 16px;
    background: #f0f2f5;
    color: #555;
  }}
  table {{
    border-collapse: collapse;
    width: 100%;
    margin: 12px 0;
  }}
  th, td {{
    border: 1px solid #d0d4d8;
    padding: 8px 12px;
    text-align: left;
  }}
  th {{ background: #f0f2f5; font-weight: 600; }}
  hr {{ border: none; border-top: 1px solid #e0e4e8; margin: 24px 0; }}
  p {{ margin: 12px 0; }}
  div.align-center {{ text-align: center; }}
  sub {{ font-size: 11px; color: #888; }}
</style>
</head>
<body>
{body}
</body>
</html>";
    }

    private static string MarkdownToHtml(string md)
    {
        var html = md;

        // Escape HTML
        html = html.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        // Code blocks (```...```)
        html = Regex.Replace(html, @"```(\w*)\n([\s\S]*?)```", m =>
            $"<pre><code>{m.Groups[2].Value.TrimEnd()}</code></pre>");

        // Inline code (`...`)
        html = Regex.Replace(html, @"`([^`]+)`", "<code>$1</code>");

        // Headers
        html = Regex.Replace(html, @"^###### (.+)$", "<h6>$1</h6>", RegexOptions.Multiline);
        html = Regex.Replace(html, @"^##### (.+)$", "<h5>$1</h5>", RegexOptions.Multiline);
        html = Regex.Replace(html, @"^#### (.+)$", "<h4>$1</h4>", RegexOptions.Multiline);
        html = Regex.Replace(html, @"^### (.+)$", "<h3>$1</h3>", RegexOptions.Multiline);
        html = Regex.Replace(html, @"^## (.+)$", "<h2>$1</h2>", RegexOptions.Multiline);
        html = Regex.Replace(html, @"^# (.+)$", "<h1>$1</h1>", RegexOptions.Multiline);

        // Horizontal rule
        html = Regex.Replace(html, @"^---+$", "<hr/>", RegexOptions.Multiline);

        // Bold/italic
        html = Regex.Replace(html, @"\*\*\*(.+?)\*\*\*", "<strong><em>$1</em></strong>");
        html = Regex.Replace(html, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        html = Regex.Replace(html, @"\*(.+?)\*", "<em>$1</em>");

        // Links [text](url)
        html = Regex.Replace(html, @"\[([^\]]+)\]\(([^)]+)\)", "<a href=\"$2\">$1</a>");

        // Images ![alt](url)
        html = Regex.Replace(html, @"!\[([^\]]*)\]\(([^)]+)\)", "<img src=\"$2\" alt=\"$1\"/>");

        // Blockquote
        html = Regex.Replace(html, @"^&gt; (.+)$", "<blockquote>$1</blockquote>", RegexOptions.Multiline);

        // Unordered list
        html = Regex.Replace(html, @"^[\-\*] (.+)$", "<li>$1</li>", RegexOptions.Multiline);
        html = Regex.Replace(html, @"(<li>.*?</li>\n?)+", m => $"<ul>{m.Value}</ul>", RegexOptions.Singleline);

        // Ordered list
        html = Regex.Replace(html, @"^\d+\. (.+)$", "<li>$1</li>", RegexOptions.Multiline);

        // Tables (basic support)
        html = Regex.Replace(html, @"^\|(.+)\|$", m =>
        {
            var cells = m.Groups[1].Value.Split('|');
            var tds = string.Join("", cells.Select(c => $"<td>{c.Trim()}</td>"));
            return $"<tr>{tds}</tr>";
        }, RegexOptions.Multiline);
        html = Regex.Replace(html, @"(<tr>.*?</tr>\n?)+", m => $"<table>{m.Value}</table>", RegexOptions.Singleline);

        // Align center div
        html = Regex.Replace(html, @"<div align=""center"">", "<div class=\"align-center\">");

        // Sub
        html = Regex.Replace(html, @"<sub>", "<sub>").Replace(@"</sub>", "</sub>");

        // Paragraphs — wrap loose text lines
        var lines = html.Split('\n');
        var result = new StringBuilder();
        var inBlock = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (string.IsNullOrEmpty(trimmed))
            {
                if (inBlock)
                {
                    result.AppendLine("</p>");
                    inBlock = false;
                }
                result.AppendLine();
                continue;
            }

            // Skip lines that are already HTML block elements
            if (trimmed.StartsWith('<') && (trimmed.StartsWith("<h") || trimmed.StartsWith("<ul") ||
                trimmed.StartsWith("<ol") || trimmed.StartsWith("<pre") || trimmed.StartsWith("<blockquote") ||
                trimmed.StartsWith("<hr") || trimmed.StartsWith("<table") || trimmed.StartsWith("<div") ||
                trimmed.StartsWith("<img") || trimmed.StartsWith("<p>")))
            {
                if (inBlock)
                {
                    result.AppendLine("</p>");
                    inBlock = false;
                }
                result.AppendLine(line);
                continue;
            }

            if (!inBlock)
            {
                result.Append("<p>");
                inBlock = true;
            }
            else
            {
                result.Append(' ');
            }
            result.Append(trimmed);
        }

        if (inBlock)
            result.AppendLine("</p>");

        return result.ToString();
    }
}
