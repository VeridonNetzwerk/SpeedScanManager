using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Thickness = System.Windows.Thickness;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;
using Visibility = System.Windows.Visibility;
using Window = System.Windows.Window;
using RoutedEventArgs = System.Windows.RoutedEventArgs;
using FontWeights = System.Windows.FontWeights;

namespace SpeedScanInstaller;

public partial class MainWindow : Window
{
    private const string ProductName = "SpeedScan Manager";
    private const string Publisher = "VeridonNetzwerk";
    private const string ProductUrl = "https://github.com/VeridonNetzwerk/SpeedScanManager";
    private const string DiscordUrl = "https://discord.gg/P2RQNYjWbp";
    private const string DiscordFaqUrl = "https://discord.gg/eJ7tJqVAZA";
    private const string DiscordFeedbackUrl = "https://discord.gg/VHh3u746fA";
    private const string ReadmeUrl = "https://raw.githubusercontent.com/VeridonNetzwerk/SpeedScanManager/refs/heads/main/README.md";

    private static readonly string DistDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "dist"));

    private bool _readmeLoaded = false;
    private bool _webViewReady = false;

    public MainWindow()
    {
        InitializeComponent();
        TxtInstallPath.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "SpeedScanManager");
        UpdateActiveTab("install");
        _ = InitWebViewAsync();
    }

    private void UpdateActiveTab(string tab)
    {
        LblInstall.FontWeight = tab == "install" ? FontWeights.SemiBold : FontWeights.Normal;
        LblReadme.FontWeight = tab == "readme" ? FontWeights.SemiBold : FontWeights.Normal;
        LblSupport.FontWeight = tab == "support" ? FontWeights.SemiBold : FontWeights.Normal;
    }

    private async Task InitWebViewAsync()
    {
        try
        {
            ReadmeBrowser.DefaultBackgroundColor = System.Drawing.Color.White;
            await ReadmeBrowser.EnsureCoreWebView2Async();
            ReadmeBrowser.NavigationStarting += OnWebViewNavigationStarting;
            _webViewReady = true;
        }
        catch
        {
            // WebView2 runtime not installed — will show error when readme is clicked
        }
    }

    private void OnWebViewNavigationStarting(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs e)
    {
        if (e.Uri != null && e.Uri.StartsWith("http"))
        {
            Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute = true });
            e.Cancel = true;
        }
    }

    // ===== Admin check =====
    private static bool IsUserAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    // ===== Window drag =====
    private void OnDragMove(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }

    // ===== Minimize =====
    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = System.Windows.WindowState.Minimized;
    }

    // ===== Sidebar / content button handlers =====
    private void OnInstallClick(object sender, RoutedEventArgs e)
    {
        // Switch to welcome view
        WelcomeView.Visibility = Visibility.Visible;
        ReadmeView.Visibility = Visibility.Collapsed;
        SupportView.Visibility = Visibility.Collapsed;
        UpdateActiveTab("install");
    }

    private void OnStartInstallClick(object sender, RoutedEventArgs e)
    {
        if (!IsUserAdmin())
        {
            var result = MessageBox.Show(
                "Für die Installation werden Administrator-Rechte benötigt.\n\nMöchten Sie das Setup als Administrator neu starten?",
                "Administrator-Rechte erforderlich",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
                RestartAsAdmin();
            return;
        }

        _ = DoInstallAsync();
    }

    private async void OnReadmeClick(object sender, RoutedEventArgs e)
    {
        // Switch to readme view
        WelcomeView.Visibility = Visibility.Collapsed;
        ReadmeView.Visibility = Visibility.Visible;
        SupportView.Visibility = Visibility.Collapsed;
        UpdateActiveTab("readme");

        if (_readmeLoaded)
        {
            ReadmeLoadingPanel.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            if (!_webViewReady)
            {
                await InitWebViewAsync();
            }

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            var markdown = await client.GetStringAsync(ReadmeUrl);
            var html = MarkdownToHtml(markdown);
            var fullHtml = WrapHtml(html);

            ReadmeBrowser.NavigateToString(fullHtml);

            _readmeLoaded = true;
            ReadmeLoadingPanel.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ReadmeLoadingPanel.Visibility = Visibility.Collapsed;
            ReadmeErrorText.Text = $"README konnte nicht geladen werden:\n\n{ex.Message}";
            ReadmeErrorText.Visibility = Visibility.Visible;
        }
    }

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Zielordner wählen (SpeedScanManager wird automatisch als Unterordner erstellt)"
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            TxtInstallPath.Text = Path.Combine(dialog.SelectedPath, "SpeedScanManager");
    }

    private void OnSupportClick(object sender, RoutedEventArgs e)
    {
        WelcomeView.Visibility = Visibility.Collapsed;
        ReadmeView.Visibility = Visibility.Collapsed;
        SupportView.Visibility = Visibility.Visible;
        UpdateActiveTab("support");
    }

    private void OnGitHubIssueClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://github.com/VeridonNetzwerk/SpeedScanManager/issues/new") { UseShellExecute = true });
    }

    private void OnDiscordFaqClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        Process.Start(new ProcessStartInfo(DiscordFaqUrl) { UseShellExecute = true });
    }

    private void OnDiscordFeedbackClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        Process.Start(new ProcessStartInfo(DiscordFeedbackUrl) { UseShellExecute = true });
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // ===== Footer links =====
    private void OnDiscordLinkClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        Process.Start(new ProcessStartInfo(DiscordUrl) { UseShellExecute = true });
    }

    private void OnGitHubLinkClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        Process.Start(new ProcessStartInfo(ProductUrl) { UseShellExecute = true });
    }

    private void OnLicenseLinkClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://github.com/VeridonNetzwerk/SpeedScanManager/blob/main/LICENSE") { UseShellExecute = true });
    }

    // ===== Installation logic =====
    private async Task DoInstallAsync()
    {
        var targetDir = TxtInstallPath.Text;

        // Validate path
        if (string.IsNullOrWhiteSpace(targetDir))
        {
            MessageBox.Show("Bitte geben Sie einen gültigen Installationspfad an.", "Fehler",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Check dist directory exists
        if (!Directory.Exists(DistDir))
        {
            MessageBox.Show($"Der dist-Ordner wurde nicht gefunden:\n{DistDir}\n\nBitte führen Sie zuerst 'dotnet publish' aus.",
                "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Disable buttons
        BtnInstall.IsEnabled = false;
        BtnStartInstall.IsEnabled = false;
        BtnBrowse.IsEnabled = false;

        // Show progress
        ProgressPanel.Visibility = Visibility.Visible;
        InstallProgress.Value = 0;
        StatusText.Text = "Bereite Installation vor...";

        try
        {
            // Create target directory
            StatusText.Text = "Erstelle Installationsverzeichnis...";
            InstallProgress.Value = 5;
            Directory.CreateDirectory(targetDir);
            await Task.Delay(100);

            // Collect files to copy
            var filesToCopy = new List<string>();
            var dirsToCreate = new List<(string relDir, string absTarget)>();

            // Core files
            foreach (var file in Directory.GetFiles(DistDir, "*.*", SearchOption.TopDirectoryOnly))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext is ".exe" or ".dll" or ".pdb")
                    filesToCopy.Add(file);
            }

            // x86 native libs
            var x86Dir = Path.Combine(DistDir, "x86");
            if (Directory.Exists(x86Dir))
                dirsToCreate.Add(("x86", Path.Combine(targetDir, "x86")));

            // x64 native libs
            var x64Dir = Path.Combine(DistDir, "x64");
            if (Directory.Exists(x64Dir))
                dirsToCreate.Add(("x64", Path.Combine(targetDir, "x64")));

            // Create subdirectories
            foreach (var (relDir, absTarget) in dirsToCreate)
            {
                StatusText.Text = $"Erstelle Ordner: {relDir}\\...";
                Directory.CreateDirectory(absTarget);
                foreach (var file in Directory.GetFiles(Path.Combine(DistDir, relDir)))
                    filesToCopy.Add(file);
            }
            InstallProgress.Value = 10;
            await Task.Delay(100);

            // Copy files
            var totalFiles = filesToCopy.Count;
            for (int i = 0; i < totalFiles; i++)
            {
                var srcFile = filesToCopy[i];
                var relPath = Path.GetRelativePath(DistDir, srcFile);
                var dstFile = Path.Combine(targetDir, relPath);

                StatusText.Text = $"Kopiere: {Path.GetFileName(srcFile)}  ({i + 1}/{totalFiles})";
                Directory.CreateDirectory(Path.GetDirectoryName(dstFile)!);
                File.Copy(srcFile, dstFile, overwrite: true);

                InstallProgress.Value = 10 + (int)((double)(i + 1) / totalFiles * 70);
                await Task.Delay(10);
            }

            // Registry entries
            StatusText.Text = "Schreibe Registry-Einträge...";
            InstallProgress.Value = 85;
            WriteRegistryEntries(targetDir);
            await Task.Delay(200);

            // Scan button setup (WIA handler registration)
            StatusText.Text = "Registriere Scan-Taste...";
            InstallProgress.Value = 88;
            try
            {
                var setupPsi = new ProcessStartInfo
                {
                    FileName = Path.Combine(targetDir, "SpeedScanManager.exe"),
                    Arguments = "/setup",
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                var setupProc = Process.Start(setupPsi);
                if (setupProc != null)
                {
                    await setupProc.WaitForExitAsync();
                    Debug.WriteLine($"Scan button setup exit code: {setupProc.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Scan button setup failed: {ex.Message}");
            }
            await Task.Delay(200);

            // Shortcuts
            StatusText.Text = "Erstelle Verknüpfungen...";
            InstallProgress.Value = 90;
            CreateShortcuts(targetDir);
            await Task.Delay(200);

            // Uninstaller
            StatusText.Text = "Erstelle Deinstallations-Eintrag...";
            InstallProgress.Value = 95;
            CreateUninstaller(targetDir);
            await Task.Delay(200);

            // Done
            InstallProgress.Value = 100;
            StatusText.Text = "Installation erfolgreich abgeschlossen!";

            await Task.Delay(500);

            var launch = MessageBox.Show(
                "SpeedScan Manager wurde erfolgreich installiert!\n\nMöchten Sie SpeedScan Manager jetzt starten?",
                "Installation erfolgreich",
                MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (launch == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo(Path.Combine(targetDir, "SpeedScanManager.exe"))
                {
                    UseShellExecute = true
                });
            }

            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Fehler: {ex.Message}";
            MessageBox.Show($"Während der Installation ist ein Fehler aufgetreten:\n\n{ex.Message}",
                "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);

            // Re-enable buttons
            BtnInstall.IsEnabled = true;
            BtnStartInstall.IsEnabled = true;
            BtnBrowse.IsEnabled = true;
        }
    }

    private static void WriteRegistryEntries(string targetDir)
    {
        var uninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\SpeedScanManager";
        using var key = Registry.LocalMachine.CreateSubKey(uninstallKey);
        if (key == null) return;

        key.SetValue("DisplayName", ProductName);
        key.SetValue("UninstallString", Path.Combine(targetDir, "uninstall.exe"));
        key.SetValue("DisplayIcon", Path.Combine(targetDir, "SpeedScanManager.exe"));
        key.SetValue("Publisher", Publisher);
        key.SetValue("DisplayVersion", "0.1.0");
        key.SetValue("URLInfoAbout", ProductUrl);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("InstallLocation", targetDir);

        // Also store install dir for app lookup
        using var appKey = Registry.LocalMachine.CreateSubKey($@"Software\{Publisher}\SpeedScanManager");
        appKey?.SetValue("InstallDir", targetDir);
    }

    private static void CreateShortcuts(string targetDir)
    {
        var exePath = Path.Combine(targetDir, "SpeedScanManager.exe");
        var startMenuDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            ProductName);
        Directory.CreateDirectory(startMenuDir);

        // Start Menu shortcut
        var shortcutPath = Path.Combine(startMenuDir, $"{ProductName}.lnk");
        CreateShortcut(shortcutPath, exePath, targetDir);

        // Uninstall shortcut
        var uninstallPath = Path.Combine(startMenuDir, "Deinstallieren.lnk");
        CreateShortcut(uninstallPath, Path.Combine(targetDir, "uninstall.exe"), targetDir, "--uninstall");

        // Desktop shortcut
        var desktopPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            $"{ProductName}.lnk");
        CreateShortcut(desktopPath, exePath, targetDir);
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string? arguments = null)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")!;
        dynamic shell = Activator.CreateInstance(shellType)!;
        var shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetPath;
        shortcut.WorkingDirectory = workingDir;
        shortcut.Description = ProductName;
        if (arguments != null)
            shortcut.Arguments = arguments;
        shortcut.Save();
    }

    private static void CreateUninstaller(string targetDir)
    {
        // Copy the installer exe as uninstall.exe — it launches in uninstall mode with --uninstall arg
        var installerPath = Environment.ProcessPath;
        if (installerPath == null) return;

        var uninstallPath = Path.Combine(targetDir, "uninstall.exe");
        File.Copy(installerPath, uninstallPath, overwrite: true);

        // Update registry to call uninstall.exe with --uninstall arg
        using var key = Registry.LocalMachine.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\SpeedScanManager", writable: true);
        key?.SetValue("UninstallString", $"\"{uninstallPath}\" --uninstall");
    }

    // ===== Restart as admin =====
    private static void RestartAsAdmin()
    {
        var exePath = Environment.ProcessPath;
        if (exePath == null) return;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas"
            };
            Process.Start(startInfo);
            Environment.Exit(0);
        }
        catch
        {
            // User declined UAC
        }
    }

    // ===== Markdown to HTML =====
    private static string WrapHtml(string body)
    {
        return $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"">
<style>
  body {{
    font-family: 'Segoe UI', 'Segoe UI Emoji', sans-serif;
    font-size: 14px;
    line-height: 1.6;
    color: #333;
    background: #ffffff;
    color-scheme: light;
    max-width: 680px;
    margin: 24px auto;
    padding: 0 20px;
  }}
  h1, h2, h3, h4, h5, h6 {{ color: #1c2633; margin-top: 24px; margin-bottom: 12px; }}
  h1 {{ font-size: 28px; border-bottom: 2px solid #e0e4e8; padding-bottom: 8px; }}
  h2 {{ font-size: 22px; border-bottom: 1px solid #e0e4e8; padding-bottom: 6px; }}
  h3 {{ font-size: 18px; }}
  a {{ color: #2a6ad8; text-decoration: none; }}
  a:hover {{ text-decoration: underline; }}
  code {{ font-family: 'Cascadia Code', 'Consolas', monospace; background: #f0f2f5; padding: 2px 6px; border-radius: 3px; font-size: 13px; }}
  pre {{ background: #1c2633; color: #dfe6ef; padding: 16px; border-radius: 6px; overflow-x: auto; }}
  pre code {{ background: transparent; color: inherit; padding: 0; }}
  ul, ol {{ padding-left: 24px; }}
  li {{ margin: 4px 0; }}
  img {{ max-width: 100%; height: auto; }}
  p img {{ display: inline-block; margin: 0 4px; vertical-align: middle; }}
  a img {{ border: none; }}
  blockquote {{ border-left: 4px solid #2a6ad8; margin: 12px 0; padding: 8px 16px; background: #f0f2f5; color: #555; }}
  table {{ border-collapse: collapse; width: 100%; margin: 12px 0; }}
  th, td {{ border: 1px solid #d0d4d8; padding: 8px 12px; text-align: left; }}
  th {{ background: #f0f2f5; font-weight: 600; }}
  hr {{ border: none; border-top: 1px solid #e0e4e8; margin: 24px 0; }}
  p {{ margin: 12px 0; }}
  div.align-center {{ text-align: center; }}
  sub {{ font-size: 11px; color: #888; }}
  ::-webkit-scrollbar {{ width: 12px; }}
  ::-webkit-scrollbar-track {{ background: #ffffff; }}
  ::-webkit-scrollbar-thumb {{ background: #c0c8d0; border-radius: 6px; border: 3px solid #ffffff; }}
  ::-webkit-scrollbar-thumb:hover {{ background: #a0a8b0; }}
</style>
</head>
<body>
{body}
</body>
</html>";
    }

    private static string MarkdownToHtml(string md)
    {
        var lines = md.Replace("\r\n", "\n").Split('\n');
        var sb = new StringBuilder();
        int i = 0;

        while (i < lines.Length)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            // Empty line
            if (string.IsNullOrEmpty(trimmed))
            {
                i++;
                continue;
            }

            // Raw HTML pass-through: <div, </div, <p, </p, <sub, </sub, <a , </a, <img, <hr
            if (trimmed.StartsWith("<div") || trimmed.StartsWith("</div>") ||
                trimmed.StartsWith("<p>") || trimmed.StartsWith("</p>") ||
                trimmed.StartsWith("<sub>") || trimmed.StartsWith("</sub>") ||
                trimmed.StartsWith("<a ") || trimmed.StartsWith("</a>") ||
                trimmed.StartsWith("<hr"))
            {
                if (trimmed.StartsWith("<div"))
                {
                    var alignMatch = Regex.Match(trimmed, @"align\s*=\s*""?center""?", RegexOptions.IgnoreCase);
                    sb.AppendLine(alignMatch.Success ? "<div style=\"text-align:center\">" : "<div>");
                }
                else
                {
                    sb.AppendLine(trimmed);
                }
                i++;
                continue;
            }

            // Standalone <img> tag
            if (trimmed.StartsWith("<img"))
            {
                sb.AppendLine(FixImgTag(trimmed));
                i++;
                continue;
            }

            // Multi-line <p> block with badges: collect until </p>
            if (trimmed.StartsWith("<p>") && !trimmed.Contains("</p>"))
            {
                sb.AppendLine("<p>");
                i++;
                while (i < lines.Length && !lines[i].Trim().StartsWith("</p>"))
                {
                    var pLine = lines[i].Trim();
                    if (!string.IsNullOrEmpty(pLine))
                    {
                        // Fix img src URLs in the line, keep everything else as-is
                        pLine = Regex.Replace(pLine, @"src=""([^""]+)""", m =>
                            $"src=\"{FixImageUrl(m.Groups[1].Value)}\"");
                        sb.AppendLine(pLine);
                    }
                    i++;
                }
                if (i < lines.Length) i++; // skip </p>
                sb.AppendLine("</p>");
                continue;
            }

            // Code block ```
            if (trimmed.StartsWith("```"))
            {
                var code = new StringBuilder();
                i++;
                while (i < lines.Length && !lines[i].Trim().StartsWith("```"))
                {
                    code.AppendLine(lines[i]);
                    i++;
                }
                if (i < lines.Length) i++; // skip closing ```
                sb.AppendLine($"<pre><code>{code.ToString().TrimEnd()}</code></pre>");
                continue;
            }

            // Headers
            var headerMatch = Regex.Match(trimmed, @"^(#{1,6})\s+(.+)$");
            if (headerMatch.Success)
            {
                var level = headerMatch.Groups[1].Value.Length;
                var text = ProcessInline(headerMatch.Groups[2].Value);
                sb.AppendLine($"<h{level}>{text}</h{level}>");
                i++;
                continue;
            }

            // Horizontal rule
            if (Regex.IsMatch(trimmed, @"^---+\s*$"))
            {
                sb.AppendLine("<hr/>");
                i++;
                continue;
            }

            // Blockquote
            if (trimmed.StartsWith(">"))
            {
                var quoteText = trimmed[1..].Trim();
                sb.AppendLine($"<blockquote>{ProcessInline(quoteText)}</blockquote>");
                i++;
                continue;
            }

            // Table
            if (trimmed.StartsWith("|") && trimmed.EndsWith("|"))
            {
                var tableSb = new StringBuilder();
                tableSb.AppendLine("<table>");
                bool firstRow = true;
                while (i < lines.Length && lines[i].Trim().StartsWith("|"))
                {
                    var row = lines[i].Trim();
                    // Skip separator row |---|---|
                    if (Regex.IsMatch(row, @"^\|[\s\-:]+\|"))
                    {
                        i++;
                        continue;
                    }
                    var cells = row.Trim('|').Split('|');
                    var tag = firstRow ? "th" : "td";
                    tableSb.Append("<tr>");
                    foreach (var cell in cells)
                        tableSb.Append($"<{tag}>{ProcessInline(cell.Trim())}</{tag}>");
                    tableSb.AppendLine("</tr>");
                    firstRow = false;
                    i++;
                }
                tableSb.AppendLine("</table>");
                sb.AppendLine(tableSb.ToString());
                continue;
            }

            // Unordered list
            if (Regex.IsMatch(trimmed, @"^[\-\*]\s+"))
            {
                sb.AppendLine("<ul>");
                while (i < lines.Length && Regex.IsMatch(lines[i].Trim(), @"^[\-\*]\s+"))
                {
                    var itemText = Regex.Replace(lines[i].Trim(), @"^[\-\*]\s+", "");
                    sb.AppendLine($"<li>{ProcessInline(itemText)}</li>");
                    i++;
                }
                sb.AppendLine("</ul>");
                continue;
            }

            // Ordered list
            if (Regex.IsMatch(trimmed, @"^\d+\.\s+"))
            {
                sb.AppendLine("<ol>");
                while (i < lines.Length && Regex.IsMatch(lines[i].Trim(), @"^\d+\.\s+"))
                {
                    var itemText = Regex.Replace(lines[i].Trim(), @"^\d+\.\s+", "");
                    sb.AppendLine($"<li>{ProcessInline(itemText)}</li>");
                    i++;
                }
                sb.AppendLine("</ol>");
                continue;
            }

            // Normal paragraph (collect consecutive non-empty, non-special lines)
            var paraSb = new StringBuilder();
            while (i < lines.Length)
            {
                var t = lines[i].Trim();
                if (string.IsNullOrEmpty(t)) break;
                if (t.StartsWith("#") || t.StartsWith("```") || t.StartsWith(">") ||
                    t.StartsWith("|") || t.StartsWith("<div") || t.StartsWith("</div") ||
                    t.StartsWith("<p>") || t.StartsWith("</p>") || t.StartsWith("<sub") ||
                    t.StartsWith("<img") || t.StartsWith("<a ") || t.StartsWith("<hr") ||
                    Regex.IsMatch(t, @"^[\-\*]\s+") || Regex.IsMatch(t, @"^\d+\.\s+") ||
                    Regex.IsMatch(t, @"^---+\s*$"))
                    break;
                if (paraSb.Length > 0) paraSb.Append(' ');
                paraSb.Append(t);
                i++;
            }
            sb.AppendLine($"<p>{ProcessInline(paraSb.ToString())}</p>");
        }

        return sb.ToString();
    }

    private static string ProcessInline(string text)
    {
        // Images ![alt](url)
        text = Regex.Replace(text, @"!\[([^\]]*)\]\(([^)]+)\)", m =>
            $"<img src=\"{FixImageUrl(m.Groups[2].Value)}\" alt=\"{m.Groups[1].Value}\"/>");

        // Links [text](url)
        text = Regex.Replace(text, @"\[([^\]]+)\]\(([^)]+)\)", "<a href=\"$2\">$1</a>");

        // Bold + italic ***text***
        text = Regex.Replace(text, @"\*\*\*(.+?)\*\*\*", "<strong><em>$1</em></strong>");

        // Bold **text**
        text = Regex.Replace(text, @"\*\*(.+?)\*\*", "<strong>$1</strong>");

        // Italic *text*
        text = Regex.Replace(text, @"\*(.+?)\*", "<em>$1</em>");

        // Inline code `code`
        text = Regex.Replace(text, @"`([^`]+)`", "<code>$1</code>");

        return text;
    }

    private static string FixImgTag(string tag)
    {
        var srcMatch = Regex.Match(tag, @"src=""([^""]+)""");
        if (!srcMatch.Success) return tag;
        var src = FixImageUrl(srcMatch.Groups[1].Value);
        var altMatch = Regex.Match(tag, @"alt=""([^""]*)""");
        var heightMatch = Regex.Match(tag, @"height=""(\d+)""");
        var alt = altMatch.Success ? altMatch.Groups[1].Value : "";
        var heightAttr = heightMatch.Success ? $" height=\"{heightMatch.Groups[1].Value}\"" : "";
        return $"<img src=\"{src}\" alt=\"{alt}\"{heightAttr}/>";
    }

    private static string FixImageUrl(string src)
    {
        if (src.StartsWith("assets/"))
            return $"https://raw.githubusercontent.com/VeridonNetzwerk/SpeedScanManager/refs/heads/main/{src}";
        if (src.StartsWith("/VeridonNetzwerk/SpeedScanManager/"))
            return $"https://raw.githubusercontent.com/VeridonNetzwerk/SpeedScanManager/refs/heads/main/{src.Substring("/VeridonNetzwerk/SpeedScanManager/".Length)}";
        return src;
    }
}
