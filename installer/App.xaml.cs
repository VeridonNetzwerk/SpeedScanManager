using System.Threading;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = SpeedScanInstaller.SetupDialog;

namespace SpeedScanInstaller;

public partial class App : Application
{
    private static Mutex? _mutex;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        // Language: the app's setting, the existing installation, or Windows
        I18n.Set(I18n.Initial(InstallerCore.GetExistingInstallLocation()));

        Theme.ApplySystem();

        _mutex = new Mutex(true, "SpeedScanInstaller_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(I18n.T("Das Setup wird bereits ausgeführt."), I18n.T("SpeedScan Manager Setup"), MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        if (e.Args.Length > 0 && e.Args[0] == "--uninstall")
        {
            var window = new UninstallerWindow();
            window.Show();
        }
        else
        {
            // "--update": started by the app's updater (install right away, start the app again)
            var window = new MainWindow(update: e.Args.Contains("--update"));
            window.Show();
        }
        base.OnStartup(e);
    }
}
