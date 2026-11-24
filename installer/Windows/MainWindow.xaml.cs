using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
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

namespace SpeedScanInstaller;

public partial class MainWindow : Window
{
    private const string ProductName = InstallerCore.ProductName;
    private const string Publisher = InstallerCore.Publisher;
    private const string ProductUrl = "https://github.com/VeridonNetzwerk/SpeedScanManager";
    private const string DiscordUrl = "https://discord.gg/P2RQNYjWbp";
    private const string DiscordFaqUrl = "https://discord.gg/eJ7tJqVAZA";
    private const string DiscordFeedbackUrl = "https://discord.gg/VHh3u746fA";
    private const string ReadmeUrl = "https://raw.githubusercontent.com/VeridonNetzwerk/SpeedScanManager/refs/heads/main/README.md";

    private bool _readmeLoaded = false;
    private bool _webViewReady = false;
    private bool _allowNextNavigation = false;
    private string? _readmeBody;

    public MainWindow()
    {
        InitializeComponent();
        // Default to an existing installation (upgrade), otherwise Program Files (x86)
        var existing = InstallerCore.GetExistingInstallLocation();
        if (existing != null && InstallerCore.IsSafeAppDirectory(existing, out _) &&
            File.Exists(Path.Combine(existing, InstallerCore.AppExeName)))
        {
            TxtInstallPath.Text = existing;
            BtnStartInstall.Content = "Aktualisieren";
        }
        else
        {
            TxtInstallPath.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), InstallerCore.AppFolderName);
        }
        var version = typeof(MainWindow).Assembly.GetName().Version;
        LblVersion.Text = version != null ? $"Version {version.Major}.{version.Minor}.{version.Build}" : "";
        UpdateActiveTab("install");
        BtnTheme.IsChecked = Theme.IsDark;
        SourceInitialized += (_, _) => Theme.RoundCorners(this);
        Theme.Changed += OnThemeChanged;
        Closed += (_, _) => Theme.Changed -= OnThemeChanged;
        _ = InitWebViewAsync();
    }

    private void UpdateActiveTab(string tab)
    {
        BtnInstall.IsChecked = tab == "install";
        BtnReadme.IsChecked = tab == "readme";
        BtnSupport.IsChecked = tab == "support";
    }

    // ===== Light / dark =====
    private void OnThemeClick(object sender, RoutedEventArgs e) => Theme.Toggle();

    private void OnThemeChanged()
    {
        BtnTheme.IsChecked = Theme.IsDark;
        if (!_webViewReady) return;
        ReadmeBrowser.DefaultBackgroundColor = ToDrawingColor(Theme.Css("ContentBrush"));
        if (_readmeBody != null)
        {
            _allowNextNavigation = true;
            ReadmeBrowser.NavigateToString(WrapHtml(_readmeBody));
        }
    }

    private static System.Drawing.Color ToDrawingColor(string css) =>
        System.Drawing.ColorTranslator.FromHtml(css);

    private async Task InitWebViewAsync()
    {
        try
        {
            ReadmeBrowser.DefaultBackgroundColor = ToDrawingColor(Theme.Css("ContentBrush"));
            await ReadmeBrowser.EnsureCoreWebView2Async();
            var settings = ReadmeBrowser.CoreWebView2.Settings;
            settings.IsScriptEnabled = false;
            settings.IsWebMessageEnabled = false;
            settings.AreDevToolsEnabled = false;
            settings.AreHostObjectsAllowed = false;
            settings.IsStatusBarEnabled = false;
            ReadmeBrowser.NavigationStarting += OnWebViewNavigationStarting;
            ReadmeBrowser.CoreWebView2.NewWindowRequested += OnWebViewNewWindowRequested;
            _webViewReady = true;
        }
        catch
        {
            // WebView2 runtime not installed — will show error when readme is clicked
        }
    }

    private void OnWebViewNavigationStarting(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs e)
    {
        // Only the README rendered via NavigateToString may load inside the WebView;
        // links open in the default browser, everything else is blocked.
        if (_allowNextNavigation)
        {
            _allowNextNavigation = false;
            return;
        }
        e.Cancel = true;
        OpenExternalLink(e.Uri);
    }

    private void OnWebViewNewWindowRequested(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        OpenExternalLink(e.Uri);
    }

    private static void OpenExternalLink(string? uri)
    {
        if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed) &&
            (parsed.Scheme == Uri.UriSchemeHttps || parsed.Scheme == Uri.UriSchemeHttp))
        {
            try { Process.Start(new ProcessStartInfo(parsed.AbsoluteUri) { UseShellExecute = true }); } catch { }
        }
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
        // app.manifest requires administrator rights, so the setup is always elevated here
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
            _readmeBody = html;
            var fullHtml = WrapHtml(html);

            _allowNextNavigation = true;
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
        {
            var selected = dialog.SelectedPath.TrimEnd(Path.DirectorySeparatorChar);
            TxtInstallPath.Text = string.Equals(Path.GetFileName(selected), InstallerCore.AppFolderName, StringComparison.OrdinalIgnoreCase)
                ? selected
                : Path.Combine(selected, InstallerCore.AppFolderName);
        }
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
        var targetDir = InstallerCore.ValidateInstallPath(TxtInstallPath.Text, out var pathError);
        if (targetDir == null)
        {
            MessageBox.Show(pathError, "Ungültiger Installationspfad",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        TxtInstallPath.Text = targetDir;
        if (InstallerCore.IsReparsePoint(targetDir))
        {
            MessageBox.Show($"Der Ordner \"{targetDir}\" ist ein Link (Junction/Symlink). Bitte wählen Sie einen normalen Ordner.",
                "Ungültiger Installationspfad", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // An existing installation in a different folder: offer to remove it, otherwise the
        // old folder would stay behind while the Windows entry points to the new one.
        string? oldInstallDir = null;
        var existingDir = InstallerCore.GetExistingInstallLocation();
        if (existingDir != null &&
            !string.Equals(Path.GetFullPath(existingDir).TrimEnd('\\'), targetDir, StringComparison.OrdinalIgnoreCase) &&
            File.Exists(Path.Combine(existingDir, InstallerCore.AppExeName)))
        {
            var answer = MessageBox.Show(
                $"SpeedScan Manager ist bereits in einem anderen Ordner installiert:\n{existingDir}\n\n" +
                $"Vorhandene Installation in {existingDir} entfernen?\n\n" +
                "Ja: alte Programmdateien löschen und neu installieren\n" +
                "Nein: alte Dateien behalten (die Windows-Einträge zeigen danach auf den neuen Ordner)",
                "Vorhandene Installation",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Cancel)
                return;
            if (answer == MessageBoxResult.Yes)
            {
                if (!InstallerCore.IsSafeAppDirectory(existingDir, out var oldDirError) || InstallerCore.IsReparsePoint(existingDir))
                {
                    MessageBox.Show("Die vorhandene Installation kann nicht automatisch entfernt werden:\n\n" +
                        (oldDirError.Length > 0 ? oldDirError : $"\"{existingDir}\" ist ein Link (Junction/Symlink).") +
                        "\n\nBitte löschen Sie den alten Ordner bei Bedarf manuell.",
                        "Vorhandene Installation", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    oldInstallDir = existingDir;
                }
            }
        }

        InstallerCore.Payload payload;
        try
        {
            payload = InstallerCore.Payload.Open();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        using var payloadScope = payload;

        if (!payload.Files.Any(f => string.Equals(f.RelativePath, InstallerCore.AppExeName, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show($"Das Installationspaket ({payload.Source}) enthält keine {InstallerCore.AppExeName}.",
                "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Running instance must be closed before files can be replaced
        if (InstallerCore.GetRunningAppProcesses().Length > 0)
        {
            var answer = MessageBox.Show(
                "SpeedScan Manager läuft gerade und wird beendet.\n\nFortfahren?",
                "SpeedScan Manager läuft",
                MessageBoxButton.OKCancel, MessageBoxImage.Information);
            if (answer != MessageBoxResult.OK)
                return;
            if (!InstallerCore.KillRunningApp())
            {
                MessageBox.Show("SpeedScan Manager konnte nicht beendet werden. Bitte beenden Sie das Programm manuell und versuchen Sie es erneut.",
                    "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        // Disable buttons
        BtnInstall.IsEnabled = false;
        BtnClose.IsEnabled = false;
        BtnStartInstall.IsEnabled = false;
        BtnBrowse.IsEnabled = false;
        TxtInstallPath.IsEnabled = false;
        SetOptionsEnabled(false);
        bool desktop = ChkDesktop.IsChecked == true, startMenu = ChkStartMenu.IsChecked == true,
            launch = ChkLaunch.IsChecked == true;

        // Show progress
        ProgressPanel.Visibility = Visibility.Visible;
        InstallProgress.Value = 0;
        StatusText.Text = "Bereite Installation vor...";

        try
        {
            // Remove the installation in the other folder first (manifest-based)
            if (oldInstallDir != null)
            {
                StatusText.Text = "Entferne vorhandene Installation...";
                InstallProgress.Value = 2;
                var oldDir = oldInstallDir;
                var allOldFilesKnown = await Task.Run(() =>
                {
                    var self = Environment.ProcessPath != null ? Path.GetFullPath(Environment.ProcessPath) : null;
                    var known = InstallerCore.RemoveInstalledFiles(oldDir, self);
                    try
                    {
                        if (Directory.Exists(oldDir) && !Directory.EnumerateFileSystemEntries(oldDir).Any())
                            Directory.Delete(oldDir, false);
                    }
                    catch { }
                    return known;
                });
                if (!allOldFilesKnown)
                {
                    MessageBox.Show(InstallerCore.LegacyLeftoverMessage(oldDir), "Vorhandene Installation",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            // Create target directory
            StatusText.Text = "Erstelle Installationsverzeichnis...";
            InstallProgress.Value = 5;
            Directory.CreateDirectory(targetDir);
            if (InstallerCore.IsReparsePoint(targetDir))
                throw new IOException($"Der Installationsordner ist ein Link (Junction/Symlink): {targetDir}");
            try
            {
                // Outside Program Files: keep standard users from modifying the install folder
                InstallerCore.SecureInstallDirectory(targetDir);
            }
            catch (Exception aclEx)
            {
                MessageBox.Show(
                    "Die Zugriffsrechte des Installationsordners konnten nicht eingeschränkt werden:\n\n" + aclEx.Message +
                    "\n\nDie Installation wird fortgesetzt. Ohne eingeschränkte Rechte können auch Standardbenutzer " +
                    "die Programmdateien verändern.",
                    "Warnung", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            await Task.Delay(100);

            // Files of a previous installation in this folder (for upgrade cleanup)
            var oldFiles = InstallerCore.ReadManifest(targetDir)
                ?? (File.Exists(Path.Combine(targetDir, InstallerCore.AppExeName))
                    ? InstallerCore.GetLegacyFileList(targetDir, out _)
                    : new List<string>());

            // Copy files
            var installed = new List<string>();
            var totalFiles = payload.Files.Count;
            for (int i = 0; i < totalFiles; i++)
            {
                var file = payload.Files[i];

                StatusText.Text = $"Kopiere: {Path.GetFileName(file.RelativePath)}  ({i + 1}/{totalFiles})";
                await Task.Run(() =>
                {
                    // Refuses paths through links and replaces (never follows) existing links
                    using var dst = InstallerCore.CreateFileInside(targetDir, file.RelativePath);
                    using var src = file.Open();
                    src.CopyTo(dst);
                });
                installed.Add(file.RelativePath);

                InstallProgress.Value = 10 + (int)((double)(i + 1) / totalFiles * 70);
            }

            // Uninstaller (payload-less build of this setup, started with --uninstall)
            StatusText.Text = "Erstelle Deinstallationsprogramm...";
            InstallProgress.Value = 82;
            await Task.Run(() => InstallerCore.WriteUninstaller(targetDir));
            installed.Add(InstallerCore.UninstallerExeName);

            // Remove files of the previous version that are no longer part of the payload
            StatusText.Text = "Entferne veraltete Dateien...";
            InstallProgress.Value = 84;
            var keep = new HashSet<string>(installed, StringComparer.OrdinalIgnoreCase);
            foreach (var rel in oldFiles.Append("scanbutton.vbs"))
            {
                if (keep.Contains(rel)) continue;
                try { InstallerCore.DeleteFileInside(targetDir, rel); }
                catch { }
            }
            InstallerCore.RemoveEmptySubdirectories(targetDir);
            InstallerCore.WriteManifest(targetDir, installed);

            // Registry entries
            StatusText.Text = "Schreibe Registry-Einträge...";
            InstallProgress.Value = 86;
            WriteRegistryEntries(targetDir);
            await Task.Delay(100);

            // Scan button setup (WIA handler registration) — the setup already runs elevated
            StatusText.Text = "Registriere Scan-Taste...";
            InstallProgress.Value = 88;
            var setupExit = await InstallerCore.RunAppHelperAsync(targetDir, "/setup", TimeSpan.FromSeconds(30));
            Debug.WriteLine($"Scan button setup exit code: {setupExit?.ToString() ?? "n/a"}");

            // Shortcuts
            StatusText.Text = "Erstelle Verknüpfungen...";
            InstallProgress.Value = 94;
            CreateShortcuts(targetDir, desktop, startMenu);
            await Task.Delay(100);

            // Done
            InstallProgress.Value = 100;
            StatusText.Text = "Installation erfolgreich abgeschlossen!";

            await Task.Delay(1200);

            if (launch)
            {
                // The setup runs elevated; start the app with normal user rights
                InstallerCore.StartDeElevated(Path.Combine(targetDir, InstallerCore.AppExeName));
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
            BtnClose.IsEnabled = true;
            BtnStartInstall.IsEnabled = true;
            BtnBrowse.IsEnabled = true;
            TxtInstallPath.IsEnabled = true;
            SetOptionsEnabled(true);
        }
    }

    private static void WriteRegistryEntries(string targetDir)
    {
        var exePath = Path.Combine(targetDir, InstallerCore.AppExeName);
        var uninstallPath = Path.Combine(targetDir, InstallerCore.UninstallerExeName);

        // Explicit 32-bit view (matches the x86 app and earlier release installs); a stale entry
        // of an AnyCPU dev build in the other view would show up twice in "Apps".
        foreach (var view in InstallerCore.AllRegistryViews.Where(v => v != InstallerCore.AppRegistryView))
        {
            try
            {
                using var otherHklm = InstallerCore.OpenHklm(view);
                using var probe = otherHklm.OpenSubKey(InstallerCore.UninstallKeyPath);
                if (probe != null && Environment.Is64BitOperatingSystem)
                    otherHklm.DeleteSubKeyTree(InstallerCore.UninstallKeyPath, false);
            }
            catch { }
        }

        using var hklm = InstallerCore.OpenHklm(InstallerCore.AppRegistryView);
        using (var key = hklm.CreateSubKey(InstallerCore.UninstallKeyPath))
        {
            key.SetValue("DisplayName", ProductName);
            key.SetValue("DisplayVersion", InstallerCore.GetAppVersion(targetDir));
            key.SetValue("Publisher", Publisher);
            key.SetValue("DisplayIcon", $"\"{exePath}\",0");
            key.SetValue("UninstallString", $"\"{uninstallPath}\" --uninstall");
            key.SetValue("QuietUninstallString", $"\"{uninstallPath}\" --uninstall");
            key.SetValue("InstallLocation", targetDir);
            key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
            key.SetValue("EstimatedSize", (int)Math.Min(int.MaxValue, InstallerCore.GetDirectorySizeKb(targetDir)), RegistryValueKind.DWord);
            key.SetValue("URLInfoAbout", ProductUrl);
            key.SetValue("HelpLink", ProductUrl + "/issues");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }

        // Also store install dir for app lookup
        using var appKey = hklm.CreateSubKey(InstallerCore.AppKeyPath);
        appKey.SetValue("InstallDir", targetDir);
    }

    private void SetOptionsEnabled(bool enabled)
    {
        ChkDesktop.IsEnabled = enabled;
        ChkStartMenu.IsEnabled = enabled;
        ChkLaunch.IsEnabled = enabled;
    }

    /// <summary>
    /// Creates the selected shortcuts. Unselected ones are removed, so an update with a
    /// changed choice doesn't leave an old shortcut behind.
    /// </summary>
    private static void CreateShortcuts(string targetDir, bool desktop, bool startMenu)
    {
        var exePath = Path.Combine(targetDir, InstallerCore.AppExeName);
        var startMenuDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            ProductName);
        var shortcutPath = Path.Combine(startMenuDir, $"{ProductName}.lnk");
        var uninstallPath = Path.Combine(startMenuDir, "Deinstallieren.lnk");
        if (startMenu)
        {
            Directory.CreateDirectory(startMenuDir);
            CreateShortcut(shortcutPath, exePath, targetDir);
            CreateShortcut(uninstallPath, Path.Combine(targetDir, InstallerCore.UninstallerExeName), targetDir, "--uninstall");
        }
        else
        {
            try { File.Delete(shortcutPath); } catch { }
            try { File.Delete(uninstallPath); } catch { }
            try
            {
                if (Directory.Exists(startMenuDir) && !Directory.EnumerateFileSystemEntries(startMenuDir).Any())
                    Directory.Delete(startMenuDir);
            }
            catch { }
        }

        var desktopPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            $"{ProductName}.lnk");
        if (desktop)
            CreateShortcut(desktopPath, exePath, targetDir);
        else
            try { File.Delete(desktopPath); } catch { }
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

    // ===== Markdown to HTML =====
    private static string WrapHtml(string body)
    {
        string bg = Theme.Css("ContentBrush"), text = Theme.Css("TextBrush"), heading = Theme.Css("TextBrush"),
            muted = Theme.Css("TextMutedBrush"), border = Theme.Css("BorderBrush"), card = Theme.Css("CardBrush"),
            link = Theme.Css("LinkBrush"), accent = Theme.Css("AccentBrush"), thumb = Theme.Css("ButtonSecondaryHoverBrush"),
            scheme = Theme.IsDark ? "dark" : "light";
        return $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"">
<style>
  body {{
    font-family: 'Segoe UI', 'Segoe UI Emoji', sans-serif;
    font-size: 14px;
    line-height: 1.6;
    color: {text};
    background: {bg};
    color-scheme: {scheme};
    max-width: 680px;
    margin: 24px auto;
    padding: 0 24px;
  }}
  h1, h2, h3, h4, h5, h6 {{ color: {heading}; margin-top: 24px; margin-bottom: 12px; }}
  h1 {{ font-size: 26px; border-bottom: 1px solid {border}; padding-bottom: 8px; }}
  h2 {{ font-size: 20px; border-bottom: 1px solid {border}; padding-bottom: 6px; }}
  h3 {{ font-size: 17px; }}
  a {{ color: {link}; text-decoration: none; }}
  a:hover {{ text-decoration: underline; }}
  code {{ font-family: 'Cascadia Code', 'Consolas', monospace; background: {card}; border: 1px solid {border}; padding: 1px 6px; border-radius: 4px; font-size: 13px; }}
  pre {{ background: {card}; border: 1px solid {border}; padding: 14px 16px; border-radius: 8px; overflow-x: auto; }}
  pre code {{ background: transparent; border: none; color: inherit; padding: 0; }}
  ul, ol {{ padding-left: 24px; }}
  li {{ margin: 4px 0; }}
  img {{ max-width: 100%; height: auto; }}
  p img {{ display: inline-block; margin: 0 4px; vertical-align: middle; }}
  a img {{ border: none; }}
  blockquote {{ border-left: 4px solid {accent}; margin: 12px 0; padding: 8px 16px; background: {card}; color: {muted}; border-radius: 0 8px 8px 0; }}
  table {{ border-collapse: collapse; width: 100%; margin: 12px 0; }}
  th, td {{ border: 1px solid {border}; padding: 8px 12px; text-align: left; }}
  th {{ background: {card}; font-weight: 600; }}
  hr {{ border: none; border-top: 1px solid {border}; margin: 24px 0; }}
  p {{ margin: 12px 0; }}
  div.align-center {{ text-align: center; }}
  sub {{ font-size: 11px; color: {muted}; }}
  ::-webkit-scrollbar {{ width: 12px; }}
  ::-webkit-scrollbar-track {{ background: {bg}; }}
  ::-webkit-scrollbar-thumb {{ background: {thumb}; border-radius: 6px; border: 3px solid {bg}; }}
  ::-webkit-scrollbar-thumb:hover {{ background: {muted}; }}
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
                sb.AppendLine($"<pre><code>{WebUtility.HtmlEncode(code.ToString().TrimEnd())}</code></pre>");
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
        // Encode first so README text can't inject markup; the replacements below only
        // add the tags they generate themselves.
        text = WebUtility.HtmlEncode(text);

        // Images ![alt](url)
        text = Regex.Replace(text, @"!\[([^\]]*)\]\(([^)]+)\)", m =>
            $"<img src=\"{SafeUrl(FixImageUrl(WebUtility.HtmlDecode(m.Groups[2].Value)))}\" alt=\"{m.Groups[1].Value}\"/>");

        // Links [text](url)
        text = Regex.Replace(text, @"\[([^\]]+)\]\(([^)]+)\)", m =>
            $"<a href=\"{SafeUrl(WebUtility.HtmlDecode(m.Groups[2].Value))}\">{m.Groups[1].Value}</a>");

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

    /// <summary>Allows only http(s), mailto and in-page anchors; returns an attribute-safe value.</summary>
    private static string SafeUrl(string url)
    {
        url = url.Trim();
        var ok = url.StartsWith('#') ||
                 url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                 url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                 url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);
        return ok ? WebUtility.HtmlEncode(url) : "#";
    }

    private static string FixImgTag(string tag)
    {
        var srcMatch = Regex.Match(tag, @"src=""([^""]+)""");
        if (!srcMatch.Success) return tag;
        var src = SafeUrl(FixImageUrl(srcMatch.Groups[1].Value));
        var altMatch = Regex.Match(tag, @"alt=""([^""]*)""");
        var heightMatch = Regex.Match(tag, @"height=""(\d+)""");
        var alt = altMatch.Success ? WebUtility.HtmlEncode(altMatch.Groups[1].Value) : "";
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
