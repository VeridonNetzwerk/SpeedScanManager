using System.Diagnostics;
using System.IO;
using System.Text;
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
            else if (InstallerCore.IsReparsePoint(targetDir))
                filesError = $"Der Installationsordner \"{targetDir}\" ist ein Link (Junction/Symlink) und wird aus Sicherheitsgründen nicht bearbeitet.";
            else if (!File.Exists(Path.Combine(targetDir, InstallerCore.AppExeName)))
                filesError = $"Im Ordner \"{targetDir}\" wurde keine {InstallerCore.AppExeName} gefunden.";
            var canRemoveFiles = filesError == null;

            // Kill running processes
            StatusText.Text = "Beende laufende Prozesse...";
            UninstallProgress.Value = 10;
            await Task.Run(InstallerCore.KillRunningApp);

            // Remove scanner button handler and restore the Windows default (needs admin; we are elevated).
            // Preferably via the app itself; if it is missing or fails, the uninstaller does it directly.
            StatusText.Text = "Entferne Scan-Tasten-Registrierung...";
            UninstallProgress.Value = 18;
            int? unregisterExit = null;
            if (canRemoveFiles)
            {
                unregisterExit = await InstallerCore.RunAppHelperAsync(targetDir!, "/unregister", TimeSpan.FromSeconds(30));
                Debug.WriteLine($"/unregister exit code: {unregisterExit?.ToString() ?? "n/a"}");
            }
            var scanButtonRestored = unregisterExit == 0 || await Task.Run(ScanButtonRegistry.Unregister);

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
            // Both registry views; the scanner-button backup stays if restoring it failed
            InstallerCore.DeleteProductRegistryKeys(keepScanButtonBackup: !scanButtonRestored);

            // Autostart entries of the elevated account and of all logged-on users
            InstallerCore.RemoveAutostartEntries();
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
            var allFilesKnown = true;
            if (canRemoveFiles)
            {
                allFilesKnown = await Task.Run(() => RemoveProgramFiles(targetDir!));
            }
            await Task.Delay(300);

            UninstallProgress.Value = 100;
            StatusText.Text = "Deinstallation erfolgreich abgeschlossen!";

            await Task.Delay(800);

            var warnings = new List<string>();
            if (!canRemoveFiles)
            {
                warnings.Add("Die Programmdateien konnten nicht automatisch gelöscht werden:\n" + filesError +
                    "\nBitte löschen Sie den Programmordner bei Bedarf manuell.");
            }
            else if (!allFilesKnown)
            {
                warnings.Add(InstallerCore.LegacyLeftoverMessage(targetDir!));
            }
            if (!scanButtonRestored)
            {
                warnings.Add("Die Scan-Tasten-Registrierung konnte nicht vollständig zurückgesetzt werden. " +
                    "Die Sicherung der ursprünglichen Windows-Einstellungen bleibt in der Registry erhalten.");
            }

            if (warnings.Count == 0)
            {
                MessageBox.Show(
                    "SpeedScan Manager wurde erfolgreich deinstalliert.",
                    "Deinstallation abgeschlossen",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    "SpeedScan Manager wurde aus Windows entfernt, aber:\n\n" + string.Join("\n\n", warnings),
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
    /// itself are removed by a cleanup script after this process has exited. Returns false if
    /// files of a legacy install may remain (see InstallerCore.GetLegacyFileList).
    /// </summary>
    private static bool RemoveProgramFiles(string targetDir)
    {
        var uninstallerPath = Environment.ProcessPath != null ? Path.GetFullPath(Environment.ProcessPath) : null;
        var allFilesKnown = InstallerCore.RemoveInstalledFiles(targetDir, uninstallerPath);

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
            return allFilesKnown;
        }

        ScheduleSelfDelete(uninstallerPath!, normalizedTarget);
        return allFilesKnown;
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
