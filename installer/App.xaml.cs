using Application = System.Windows.Application;

namespace SpeedScanInstaller;

public partial class App : Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
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
