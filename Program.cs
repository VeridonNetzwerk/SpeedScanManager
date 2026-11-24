using System.Diagnostics;
using System.Security.Principal;
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
/// Pipe protocol: see <see cref="ScanButtonPipeServer"/>; the pipe name contains the session
/// id and the user SID (<see cref="PipeName"/>), so instances of other users or sessions
/// (fast user switching, RDP) never receive each other's commands.
/// </summary>
internal static class Program
{
    /// <summary>
    /// "SpeedScanManager_ScanButton_{session id}_{user SID}". The STI scan button handler
    /// launches "/scanbutton" in the interactive user's session as that user, so the client
    /// computes the same name as the tray instance it must reach.
    /// </summary>
    internal static string PipeName { get; } = BuildPipeName();
    internal const string MutexName = @"Local\SpeedScanManager_SingleInstance";

    /// <summary>
    /// True when the app was started by "/scanbutton" without a running instance.
    /// The tray should start one scan once it is initialised and then reset the flag.
    /// </summary>
    internal static bool ScanOnStartRequested { get; set; }

    /// <summary>Restarted after a language change from the settings window: open it again.</summary>
    internal static bool ShowSettingsOnStart { get; set; }

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
                // Appended (a second instance that only forwards "SHOW" must not hide the
                // entry of the running one); trimmed when it grows.
                if (File.Exists(debugFile) && new FileInfo(debugFile).Length > 64 * 1024)
                    File.Delete(debugFile);
                File.AppendAllText(debugFile, $"{DateTime.Now}: App started (pid {Environment.ProcessId}). Args: {string.Join(",", args)}\nExe: {Environment.ProcessPath ?? "?"}\nRuntime: {Environment.Version}\nOS: {Environment.OSVersion}\n\n");
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

            // Restart (language change): the old instance is still exiting, wait for it
            if (!ownsMutex && HasArg(args, "/restart"))
            {
                var wait = Stopwatch.StartNew();
                while (!ownsMutex && wait.ElapsedMilliseconds < 15000)
                {
                    Thread.Sleep(100);
                    ownsMutex = TryAcquire(mutex);
                }
            }
            ShowSettingsOnStart = HasArg(args, "/settings");

            if (!ownsMutex)
            {
                // Another instance is running: hand the request over and exit.
                var command = scanButton ? ScanButtonPipeServer.CommandScan : ScanButtonPipeServer.CommandShow;
                // Let the running instance bring its dialogs/settings window to the front.
                WindowActivation.AllowAnyProcessToActivate();
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

            InstallExceptionHandlers();

            // Language first: every window and static text below uses it
            L.Init(AppSettings.PeekLanguage());

            // App-lifetime pipe server (independent of scanner/WIA state).
            ScanButtonPipeServer.Shared.Start();
            try
            {
                ApplicationConfiguration.Initialize();
                using var context = new TrayApplicationContext();
                Application.Run(context);
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
            ReportCrash("Main", ex, fatal: true);
            return 1;
        }
    }

    private static int _handlersInstalled;

    /// <summary>
    /// Last-resort handlers: log to DiagLog and crash.log (%LocalAppData%\SpeedScanManager)
    /// and tell the user. Exceptions on the UI thread don't terminate the app.
    /// </summary>
    private static void InstallExceptionHandlers()
    {
        if (Interlocked.Exchange(ref _handlersInstalled, 1) == 1) return;

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ReportCrash("UI-Thread", e.Exception, fatal: false);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            ReportCrash("AppDomain", e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString()), e.IsTerminating);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            // Errors of background tasks: log only, they must not take the app down.
            e.SetObserved();
            WriteCrashLog("Task", e.Exception);
        };
    }

    private static int _crashDialogOpen;

    private static void ReportCrash(string source, Exception ex, bool fatal)
    {
        WriteCrashLog(source, ex);

        // Avoid a cascade of dialogs if the error repeats while the first one is open.
        if (Interlocked.Exchange(ref _crashDialogOpen, 1) == 1) return;
        try
        {
            var text = fatal
                ? "SpeedScan Manager wurde wegen eines unerwarteten Fehlers beendet."
                : "In SpeedScan Manager ist ein unerwarteter Fehler aufgetreten. Das Programm läuft weiter.";
            TrayMessageBox.Error(null,
                $"{text}\n\n{ex.GetType().Name}: {ex.Message}\n\nDetails stehen in:\n{CrashLogPath}");
        }
        catch
        {
            // nothing left to do
        }
        finally
        {
            Interlocked.Exchange(ref _crashDialogOpen, 0);
        }
    }

    private static string CrashLogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SpeedScanManager", "crash.log");

    private static void WriteCrashLog(string source, Exception ex)
    {
        try { DiagLog.Write($"[Crash] {source}: {ex}"); } catch { }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);
            File.AppendAllText(CrashLogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{source}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch { }
    }

    private static string BuildPipeName()
    {
        int sessionId = 0;
        string user = Environment.UserName;
        try
        {
            using var process = Process.GetCurrentProcess();
            sessionId = process.SessionId;
        }
        catch { }
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            user = identity.User?.Value ?? user;
        }
        catch { }
        return $"SpeedScanManager_ScanButton_{sessionId}_{user}";
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
