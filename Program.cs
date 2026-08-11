using System.Diagnostics;
using System.IO.Pipes;
using System.Windows.Forms;

namespace SpeedScanManager;

internal static class Program
{
    internal const string PipeName = "SpeedScanManager_ScanButton";

    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            // Early diagnostic: verify the app starts at all
            var debugFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SpeedScanManager", "startup.log");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(debugFile)!);
                File.WriteAllText(debugFile, $"{DateTime.Now}: App started. Args: {string.Join(",", args)}\nExe: {Environment.ProcessPath ?? "?"}\nRuntime: {Environment.Version}\nOS: {Environment.OSVersion}");
            }
            catch { }

            // Elevated helper: do registry setup and exit
            if (args.Contains("/setup", StringComparer.OrdinalIgnoreCase))
            {
                WiaEventWatcher.DoRegistrySetup();
                return;
            }

            // If launched with /scanbutton, signal the running instance via named pipe
            if (args.Contains("/scanbutton", StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                    client.Connect(3000);
                    using var writer = new StreamWriter(client);
                    writer.WriteLine("SCAN");
                    writer.Flush();
                }
                catch { }
                return;
            }

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
