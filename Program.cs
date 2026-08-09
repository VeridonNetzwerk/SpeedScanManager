using System.Windows.Forms;

namespace SpeedScanManager;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApplicationContext());
        }
        catch (Exception ex)
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SpeedScanManager", "crash.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.WriteAllText(logPath, $"{DateTime.Now}: {ex}\n\nInner: {ex.InnerException}");
            MessageBox.Show(ex.ToString(), "SpeedScan Manager – Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
