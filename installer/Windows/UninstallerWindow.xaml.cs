using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace SpeedScanInstaller;

public partial class UninstallerWindow : Window
{
    private const string ProductName = InstallerCore.ProductName;
    private const string ProductUrl = "https://github.com/VeridonNetzwerk/SpeedScanManager";
    private const string DiscordFeedbackUrl = "https://discord.gg/VHh3u746fA";
    private const string DiscordUrl = "https://discord.gg/P2RQNYjWbp";

    public UninstallerWindow()
    {
        InitializeComponent();
    }

    private void OnDragMove(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnLicenseLinkClick(object sender, MouseButtonEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://github.com/VeridonNetzwerk/SpeedScanManager/blob/main/LICENSE") { UseShellExecute = true });
    }

    private void OnGitHubLinkClick(object sender, MouseButtonEventArgs e)
    {
        Process.Start(new ProcessStartInfo(ProductUrl) { UseShellExecute = true });
    }

    private void OnDiscordLinkClick(object sender, MouseButtonEventArgs e)
    {
        Process.Start(new ProcessStartInfo(DiscordUrl) { UseShellExecute = true });
    }

    private void OnDiscordFeedbackClick(object sender, MouseButtonEventArgs e)
    {
        Process.Start(new ProcessStartInfo(DiscordFeedbackUrl) { UseShellExecute = true });
    }

    private void OnUninstallClick(object sender, RoutedEventArgs e)
    {
        ConfirmOverlay.Visibility = Visibility.Visible;
    }

    private void OnConfirmCancelClick(object sender, RoutedEventArgs e)
    {
        ConfirmOverlay.Visibility = Visibility.Collapsed;
    }

    private async void OnConfirmUninstallClick(object sender, RoutedEventArgs e)
    {
        ConfirmOverlay.Visibility = Visibility.Collapsed;

        BtnUninstall.IsEnabled = false;
        BtnCancel.IsEnabled = false;
        ChkRemoveSettings.IsEnabled = false;
        ProgressPanel.Visibility = Visibility.Visible;
        UninstallProgress.Value = 0;
        StatusText.Text = "Bereite Deinstallation vor...";

        var removeSettings = ChkRemoveSettings.IsChecked == true;

        try
        {
            // Install directory comes only from the registry — never from the uninstaller's own location
            var targetDir = InstallerCore.GetExistingInstallLocation();
            string? filesError = null;
            if (targetDir == null)
                filesError = "Der Installationsordner ist nicht in der Registry eingetragen.";
            else if (!InstallerCore.IsSafeAppDirectory(targetDir, out var dirError))
                filesError = dirError;
            else if (!File.Exists(Path.Combine(targetDir, InstallerCore.AppExeName)))
                filesError = $"Im Ordner \"{targetDir}\" wurde keine {InstallerCore.AppExeName} gefunden.";
            var canRemoveFiles = filesError == null;

            // Kill running processes
            StatusText.Text = "Beende laufende Prozesse...";
            UninstallProgress.Value = 10;
            await Task.Run(InstallerCore.KillRunningApp);

            // Remove scanner button handler and restore the Windows default (needs admin; we are elevated)
            if (canRemoveFiles)
            {
                StatusText.Text = "Entferne Scan-Tasten-Registrierung...";
                UninstallProgress.Value = 18;
                var exit = await InstallerCore.RunAppHelperAsync(targetDir!, "/unregister", TimeSpan.FromSeconds(30));
                Debug.WriteLine($"/unregister exit code: {exit?.ToString() ?? "n/a"}");
            }

            // Remove shortcuts
            StatusText.Text = "Entferne Verknüpfungen...";
            UninstallProgress.Value = 25;
            var startMenuDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                ProductName);
            foreach (var lnk in new[] { $"{ProductName}.lnk", "Deinstallieren.lnk" })
            {
                try { File.Delete(Path.Combine(startMenuDir, lnk)); } catch { }
            }
            try
            {
                if (Directory.Exists(startMenuDir) && !Directory.EnumerateFileSystemEntries(startMenuDir).Any())
                    Directory.Delete(startMenuDir);
            }
            catch { }

            var desktopPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                $"{ProductName}.lnk");
            if (File.Exists(desktopPath))
            {
                try { File.Delete(desktopPath); } catch { }
            }
            await Task.Delay(200);

            // Remove registry entries
            StatusText.Text = "Entferne Registry-Einträge...";
            UninstallProgress.Value = 40;
            try { Registry.LocalMachine.DeleteSubKeyTree(InstallerCore.UninstallKeyPath, false); } catch { }
            try { Registry.LocalMachine.DeleteSubKeyTree(InstallerCore.AppKeyPath, false); } catch { }
            try
            {
                bool publisherKeyEmpty;
                using (var pubKey = Registry.LocalMachine.OpenSubKey(InstallerCore.PublisherKeyPath))
                    publisherKeyEmpty = pubKey != null && pubKey.SubKeyCount == 0 && pubKey.ValueCount == 0;
                if (publisherKeyEmpty)
                    Registry.LocalMachine.DeleteSubKey(InstallerCore.PublisherKeyPath, false);
            }
            catch { }

            // Autostart entry. Note: this is the HKCU of the account the (elevated) uninstaller
            // runs as — if elevation used a different admin account, that user's entry stays.
            try
            {
                using var runKey = Registry.CurrentUser.OpenSubKey(InstallerCore.RunKeyPath, writable: true);
                runKey?.DeleteValue(InstallerCore.RunValueName, false);
            }
            catch { }
            await Task.Delay(200);

            // Remove settings if requested
            if (removeSettings)
            {
                StatusText.Text = "Entferne Einstellungen und Profildaten...";
                UninstallProgress.Value = 55;
                foreach (var root in new[] { Environment.SpecialFolder.ApplicationData, Environment.SpecialFolder.LocalApplicationData })
                {
                    var dataDir = Path.Combine(Environment.GetFolderPath(root), InstallerCore.AppFolderName);
                    if (Directory.Exists(dataDir))
                    {
                        try { Directory.Delete(dataDir, true); } catch { }
                    }
                }
                await Task.Delay(200);
            }

            // Remove program files (only those listed in the install manifest)
            StatusText.Text = "Entferne Programmdateien...";
            UninstallProgress.Value = 70;
            if (canRemoveFiles)
            {
                await Task.Run(() => RemoveProgramFiles(targetDir!));
            }
            await Task.Delay(300);

            UninstallProgress.Value = 100;
            StatusText.Text = "Deinstallation erfolgreich abgeschlossen!";

            await Task.Delay(800);

            if (canRemoveFiles)
            {
                MessageBox.Show(
                    "SpeedScan Manager wurde erfolgreich deinstalliert.",
                    "Deinstallation abgeschlossen",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    "SpeedScan Manager wurde aus Windows entfernt, die Programmdateien konnten aber nicht automatisch gelöscht werden:\n\n" +
                    filesError + "\n\nBitte löschen Sie den Programmordner bei Bedarf manuell.",
                    "Deinstallation abgeschlossen",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Fehler: {ex.Message}";
            MessageBox.Show($"Während der Deinstallation ist ein Fehler aufgetreten:\n\n{ex.Message}",
                "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);

            BtnUninstall.IsEnabled = true;
            BtnCancel.IsEnabled = true;
            ChkRemoveSettings.IsEnabled = true;
        }
    }

    /// <summary>
    /// Deletes the files listed in the install manifest (or the legacy file set of installs
    /// without manifest), then empty folders. The running uninstaller and the install folder
    /// itself are removed by a cleanup script after this process has exited.
    /// </summary>
    private static void RemoveProgramFiles(string targetDir)
    {
        var uninstallerPath = Environment.ProcessPath != null ? Path.GetFullPath(Environment.ProcessPath) : null;
        var files = InstallerCore.ReadManifest(targetDir) ?? InstallerCore.GetLegacyFileList(targetDir);
        files.Add(InstallerCore.ManifestFileName);
        files.Add("scanbutton.vbs");

        foreach (var rel in files)
        {
            try
            {
                var path = InstallerCore.ResolveInside(targetDir, rel);
                if (uninstallerPath != null && string.Equals(path, uninstallerPath, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }
        InstallerCore.RemoveEmptySubdirectories(targetDir);

        var normalizedTarget = Path.GetFullPath(targetDir).TrimEnd(Path.DirectorySeparatorChar);
        var uninstallerInTarget = uninstallerPath != null &&
            string.Equals(Path.GetDirectoryName(uninstallerPath), normalizedTarget, StringComparison.OrdinalIgnoreCase);
        if (!uninstallerInTarget)
        {
            try
            {
                if (Directory.Exists(normalizedTarget) && !Directory.EnumerateFileSystemEntries(normalizedTarget).Any())
                    Directory.Delete(normalizedTarget);
            }
            catch { }
            return;
        }

        ScheduleSelfDelete(uninstallerPath!, normalizedTarget);
    }

    /// <summary>
    /// Starts a hidden batch file that waits until this uninstaller has exited, deletes it and
    /// removes the install folder if it is empty. Uses ping as delay because "timeout" fails
    /// without a console.
    /// </summary>
    private static void ScheduleSelfDelete(string uninstallerPath, string targetDir)
    {
        static string Esc(string p) => p.Replace("%", "%%");
        var exe = Esc(uninstallerPath);
        var dir = Esc(targetDir);

        var cleanupBat = Path.Combine(Path.GetTempPath(), $"ssm_cleanup_{Environment.ProcessId}.bat");
        var script = string.Join("\r\n",
            "@echo off",
            "chcp 65001 >nul",
            "set /a tries=0",
            ":retry",
            $"del /f /q \"{exe}\" >nul 2>&1",
            $"if not exist \"{exe}\" goto cleanup",
            "set /a tries+=1",
            "if %tries% geq 30 goto cleanup",
            "ping -n 3 127.0.0.1 >nul",
            "goto retry",
            ":cleanup",
            $"rd \"{dir}\" >nul 2>&1",
            "(goto) 2>nul & del \"%~f0\"",
            "");
        File.WriteAllText(cleanupBat, script, new UTF8Encoding(false));

        Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            Arguments = $"/d /c \"\"{cleanupBat}\"\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden,
        });
    }
}
