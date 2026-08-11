using System.Diagnostics;
using System.IO;
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
    private const string ProductName = "SpeedScan Manager";
    private const string Publisher = "VeridonNetzwerk";
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
            // Find install directory from registry
            string? targetDir = null;
            using (var key = Registry.LocalMachine.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall\SpeedScanManager"))
            {
                targetDir = key?.GetValue("InstallLocation") as string;
            }

            // If not found, use the directory the uninstaller is in
            targetDir ??= Path.GetDirectoryName(Environment.ProcessPath);

            // Kill running processes
            StatusText.Text = "Beende laufende Prozesse...";
            UninstallProgress.Value = 10;
            foreach (var proc in Process.GetProcessesByName("SpeedScanManager"))
            {
                try { proc.Kill(); proc.WaitForExit(3000); } catch { }
            }
            foreach (var proc in Process.GetProcessesByName("SpeedScanManagerTray"))
            {
                try { proc.Kill(); proc.WaitForExit(3000); } catch { }
            }
            await Task.Delay(500);

            // Remove shortcuts
            StatusText.Text = "Entferne Verknüpfungen...";
            UninstallProgress.Value = 25;
            var startMenuDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                ProductName);
            if (Directory.Exists(startMenuDir))
            {
                try { Directory.Delete(startMenuDir, true); } catch { }
            }

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
            try
            {
                Registry.LocalMachine.DeleteSubKeyTree(
                    @"Software\Microsoft\Windows\CurrentVersion\Uninstall\SpeedScanManager", false);
            } catch { }
            try
            {
                Registry.LocalMachine.DeleteSubKeyTree(
                    $@"Software\{Publisher}\SpeedScanManager", false);
            } catch { }
            await Task.Delay(200);

            // Remove settings if requested
            if (removeSettings)
            {
                StatusText.Text = "Entferne Einstellungen und Profildaten...";
                UninstallProgress.Value = 55;
                var appDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    ProductName);
                if (Directory.Exists(appDataDir))
                {
                    try { Directory.Delete(appDataDir, true); } catch { }
                }

                var localAppDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    ProductName);
                if (Directory.Exists(localAppDataDir))
                {
                    try { Directory.Delete(localAppDataDir, true); } catch { }
                }
                await Task.Delay(200);
            }

            // Remove program files
            StatusText.Text = "Entferne Programmdateien...";
            UninstallProgress.Value = 70;
            if (targetDir != null && Directory.Exists(targetDir))
            {
                // Delete everything except the uninstaller itself (we need it to finish)
                var uninstallerPath = Environment.ProcessPath;
                foreach (var file in Directory.GetFiles(targetDir, "*", SearchOption.AllDirectories))
                {
                    if (uninstallerPath != null &&
                        string.Equals(file, uninstallerPath, StringComparison.OrdinalIgnoreCase))
                        continue;
                    try { File.Delete(file); } catch { }
                }

                // Schedule self-deletion + folder cleanup
                var cleanupBat = Path.Combine(Path.GetTempPath(), "ssm_cleanup.bat");
                File.WriteAllText(cleanupBat, $@"@echo off
timeout /t 2 /nobreak >nul
del /q ""{uninstallerPath}""
rd /s /q ""{targetDir}""
del ""{cleanupBat}""
");
                Process.Start(new ProcessStartInfo(cleanupBat)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
            }
            await Task.Delay(300);

            UninstallProgress.Value = 100;
            StatusText.Text = "Deinstallation erfolgreich abgeschlossen!";

            await Task.Delay(800);

            MessageBox.Show(
                "SpeedScan Manager wurde erfolgreich deinstalliert.",
                "Deinstallation abgeschlossen",
                MessageBoxButton.OK, MessageBoxImage.Information);

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
}
