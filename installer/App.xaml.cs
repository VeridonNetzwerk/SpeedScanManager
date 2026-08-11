using System.Threading;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace SpeedScanInstaller;

public partial class App : Application
{
    private static Mutex? _mutex;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        _mutex = new Mutex(true, "SpeedScanInstaller_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("Das Setup wird bereits ausgeführt.", "SpeedScan Manager Setup", MessageBoxButton.OK, MessageBoxImage.Information);
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
            var window = new MainWindow();
            window.Show();
        }
        base.OnStartup(e);
    }
}
