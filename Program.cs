using System.Diagnostics;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Entry point.
///
/// Command line:
///   (none)        Start the tray app. If an instance is already running in this session,
///                 send "SHOW" to it over the pipe and exit.
///   /scanbutton   Launched by the WIA/STI ScanButton handler. Sends "SCAN" to the running
///                 instance. If no instance is running (or none answers within ~3 s and the
///                 instance mutex becomes free), the app starts normally with
///                 <see cref="ScanOnStartRequested"/> = true.
///   /setup        Elevated helper: registers the scan button handler. Exit code 0/1.
///   /unregister   Elevated helper (uninstaller): removes the handler and restores the
///                 Windows default handler. Exit code 0/1.
///
/// Single instance: named mutex <see cref="MutexName"/> (per logon session).
/// Pipe protocol: see <see cref="ScanButtonPipeServer"/>.
/// </summary>
internal static class Program
{
    internal const string PipeName = "SpeedScanManager_ScanButton";
    internal const string MutexName = @"Local\SpeedScanManager_SingleInstance";

    /// <summary>
    /// True when the app was started by "/scanbutton" without a running instance.
    /// The tray should start one scan once it is initialised and then reset the flag.
    /// </summary>
    internal static bool ScanOnStartRequested { get; set; }

    [STAThread]
    static int Main(string[] args)
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

            // Elevated helpers: do registry work and exit
            if (HasArg(args, "/setup"))
                return WiaEventWatcher.DoRegistrySetup() ? 0 : 1;

            if (HasArg(args, "/unregister"))
                return WiaEventWatcher.DoRegistryUnregister() ? 0 : 1;

            bool scanButton = HasArg(args, "/scanbutton");
            if (scanButton)
                DiagLog.WriteWia($"Program: launched by scan button ({string.Join(" ", args)})");

            using var mutex = new Mutex(false, MutexName);
            bool ownsMutex = TryAcquire(mutex);

            if (!ownsMutex)
            {
                // Another instance is running: hand the request over and exit.
                var command = scanButton ? ScanButtonPipeServer.CommandScan : ScanButtonPipeServer.CommandShow;
                var sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < 3000)
                {
                    if (ScanButtonPipeServer.TrySend(command, 1000))
                        return 0;

                    // The other instance may just be exiting — take over if the mutex is free now.
                    if (TryAcquire(mutex))
                    {
                        ownsMutex = true;
                        break;
                    }
                }

                if (!ownsMutex)
                {
                    DiagLog.WriteWia($"Program: running instance did not answer '{command}', exiting");
                    return 1;
                }
            }

            if (scanButton)
                ScanOnStartRequested = true;

            // App-lifetime pipe server (independent of scanner/WIA state).
            ScanButtonPipeServer.Shared.Start();
            try
            {
                ApplicationConfiguration.Initialize();
                Application.Run(new TrayApplicationContext());
            }
            finally
            {
                ScanButtonPipeServer.Shared.Stop();
                try { mutex.ReleaseMutex(); } catch { }
            }
            return 0;
        }
        catch (Exception ex)
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SpeedScanManager", "crash.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.WriteAllText(logPath, $"{DateTime.Now}: {ex}\n\nInner: {ex.InnerException}");
            MessageBox.Show(ex.ToString(), "SpeedScan Manager – Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static bool HasArg(string[] args, string name) =>
        args.Contains(name, StringComparer.OrdinalIgnoreCase);

    private static bool TryAcquire(Mutex mutex)
    {
        try
        {
            return mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            // Previous owner crashed — we own the mutex now.
            return true;
        }
    }
}
