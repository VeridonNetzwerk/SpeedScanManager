using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace SpeedScanManager;

/// <summary>
/// Watches for scanner button presses using WIA.
/// Strategy:
/// 1. On first run: launch an elevated helper (/setup) that replaces the wiaacmgr
///    handler with our app in the registry and restarts the WIA service.
///    The registry entries persist — no admin needed on subsequent runs.
/// 2. Register an IWiaEventCallback via IWiaDevMgr2 for in-process event notifications.
/// 3. Run a named pipe server so that when WIA launches our exe with /scanbutton,
///    it signals the running main instance to start a scan.
/// </summary>
internal sealed class WiaEventWatcher : IDisposable
{
    private IWiaDevMgr2? _devMgr2;
    private WiaEventCallbackImpl? _callback;
    private GCHandle _callbackPin;
    private Thread? _pipeThread;
    private CancellationTokenSource? _cts;
    private bool _disposed;
    private readonly List<IntPtr> _eventObjects = new();

    private const string OurGuid = "{B7E3A1F0-5C2D-4E8A-9F1A-3D6B7C8E9F12}";
    private const string WiaacmgrClsid = "{D13E3F25-1688-45A0-9743-759EB35CDF9A}";
    private const string ScanButtonGlobalKey = @"SYSTEM\CurrentControlSet\Control\StillImage\Events\ScanButton";
    private const string DeviceClassKey = @"SYSTEM\CurrentControlSet\Control\Class\{6bdd1fc6-810f-11d0-bec7-08002be2092f}";

    public event Action? ScanButtonPressed;

    private static readonly Guid WIA_EVENT_SCAN_IMAGE = new("a6c5a715-8ce6-11d2-977a-0000f87a926f");

    private static void LogDiag(string msg) => DiagLog.WriteWia(msg);

    /// <summary>
    /// Checks if our handler is already registered (read-only, no admin needed).
    /// </summary>
    private static bool IsHandlerRegistered()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(ScanButtonGlobalKey, false);
            if (key == null) return false;
            using var ourKey = key.OpenSubKey(OurGuid, false);
            return ourKey != null;
        }
        catch { return false; }
    }

    /// <summary>
    /// Called from Program.cs when launched with /setup (elevated).
    /// Replaces wiaacmgr handler with our app and restarts WIA service.
    /// </summary>
    public static void DoRegistrySetup()
    {
        try
        {
            LogDiag("DoRegistrySetup: starting (elevated)");

            var srcExe = Environment.ProcessPath ?? "";

            // Create a VBS launcher script — wscript.exe has no console window
            var localDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SpeedScanManager");
            Directory.CreateDirectory(localDir);
            var vbsPath = Path.Combine(localDir, "scanbutton.vbs");
            var vbsContent = $@"Set WshShell = CreateObject(""WScript.Shell"")
WshShell.Run """"""{srcExe}"""""" & "" /scanbutton"" & BuildArgs(), 0, False

Function BuildArgs()
    Dim args, i
    args = """"
    For i = 0 To WScript.Arguments.Count - 1
        args = args & "" "" & WScript.Arguments(i)
    Next
    BuildArgs = args
End Function
";
            File.WriteAllText(vbsPath, vbsContent);
            LogDiag($"  Created VBS launcher at {vbsPath}");

            var cmdLine = $"wscript.exe \"{vbsPath}\" /StiDevice:%1 /StiEvent:%2";

            ReplaceHandlerInKey(ScanButtonGlobalKey, cmdLine);

            using var classKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(DeviceClassKey, false);
            if (classKey != null)
            {
                foreach (var subKeyName in classKey.GetSubKeyNames())
                {
                    var eventsPath = $@"{DeviceClassKey}\{subKeyName}\Events";
                    using var eventsKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(eventsPath, false);
                    if (eventsKey == null) continue;

                    foreach (var evtKeyName in eventsKey.GetSubKeyNames())
                    {
                        var evtPath = $@"{eventsPath}\{evtKeyName}";
                        using var evtKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(evtPath, true);
                        if (evtKey == null) continue;

                        var guid = evtKey.GetValue("GUID") as string;
                        if (guid == null) continue;

                        if (guid.Equals("{A6C5A715-8C6E-11D2-977A-0000F87A926F}", StringComparison.OrdinalIgnoreCase))
                        {
                            LogDiag($"  Found device-level ScanButton event at {evtPath}");
                            ReplaceHandlerInKey(evtPath, cmdLine);
                        }
                    }
                }
            }

            CleanupOldEntries();
            RestartWiaService();

            LogDiag("DoRegistrySetup: complete");
        }
        catch (Exception ex)
        {
            LogDiag($"DoRegistrySetup failed: {ex.Message}");
        }
    }

    private static void ReplaceHandlerInKey(string keyPath, string cmdLine)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath, true);
            if (key == null)
            {
                LogDiag($"  Key not found: {keyPath}");
                return;
            }

            var wiaacmgrSubKey = key.OpenSubKey(WiaacmgrClsid);
            if (wiaacmgrSubKey != null)
            {
                wiaacmgrSubKey.Close();
                key.DeleteSubKeyTree(WiaacmgrClsid, false);
                LogDiag($"  Removed wiaacmgr from {keyPath}");
            }

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                if (subKeyName == OurGuid) continue;
                using var subKey = key.OpenSubKey(subKeyName);
                if (subKey != null)
                {
                    var cmdline = subKey.GetValue("Cmdline") as string;
                    if (cmdline != null && cmdline.Contains("SpeedScanManager"))
                    {
                        subKey.Close();
                        key.DeleteSubKeyTree(subKeyName, false);
                        LogDiag($"  Removed old handler {subKeyName} from {keyPath}");
                    }
                }
            }

            key.DeleteSubKeyTree(OurGuid, false);

            using var ourKey = key.CreateSubKey(OurGuid);
            ourKey.SetValue("Name", "SpeedScan Manager");
            ourKey.SetValue("Desc", "Scan with SpeedScan Manager");
            ourKey.SetValue("Icon", "SpeedScanManager,0");
            ourKey.SetValue("Cmdline", cmdLine);
            LogDiag($"  Registered handler in {keyPath}");

            var la = key.GetValue("LaunchApplications") as string;
            if (string.IsNullOrEmpty(la) || la == "Not Used")
            {
                key.SetValue("LaunchApplications", "*");
                LogDiag($"  Set LaunchApplications='*' in {keyPath}");
            }
        }
        catch (Exception ex)
        {
            LogDiag($"  ReplaceHandlerInKey({keyPath}) failed: {ex.Message}");
        }
    }

    private static void CleanupOldEntries()
    {
        try
        {
            var oldGuid = "{33C60039-B6C1-4C33-9BB5-7B3B669D6631}";
            using var classKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(DeviceClassKey, true);
            if (classKey == null) return;

            foreach (var subKeyName in classKey.GetSubKeyNames())
            {
                var eventsPath = $@"{DeviceClassKey}\{subKeyName}\Events";
                using var eventsKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(eventsPath, true);
                if (eventsKey == null) continue;

                foreach (var evtKeyName in eventsKey.GetSubKeyNames())
                {
                    using var evtKey = eventsKey.OpenSubKey(evtKeyName, true);
                    if (evtKey == null) continue;

                    if (evtKey.GetSubKeyNames().Contains(oldGuid))
                    {
                        evtKey.DeleteSubKeyTree(oldGuid, false);
                        LogDiag($"  Cleaned up old entry {oldGuid} from {eventsPath}\\{evtKeyName}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogDiag($"  CleanupOldEntries failed: {ex.Message}");
        }
    }

    private static void RestartWiaService()
    {
        try
        {
            LogDiag("  Restarting WIA service (stisvc)...");
            var psi = new ProcessStartInfo
            {
                FileName = "net.exe",
                Arguments = "stop stisvc",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var stopProc = Process.Start(psi);
            if (stopProc != null)
            {
                stopProc.WaitForExit(10000);
                LogDiag($"  net stop stisvc: exit={stopProc.ExitCode}");
            }
            Thread.Sleep(1000);
            psi.Arguments = "start stisvc";
            using var startProc = Process.Start(psi);
            if (startProc != null)
            {
                startProc.WaitForExit(10000);
                LogDiag($"  net start stisvc: exit={startProc.ExitCode}");
            }
            Thread.Sleep(2000);
            LogDiag("  WIA service restarted");
        }
        catch (Exception ex)
        {
            LogDiag($"  RestartWiaService failed: {ex.Message}");
        }
    }

    public void Start()
    {
        try
        {
            var autoType = Type.GetTypeFromProgID("WIA.DeviceManager.1");
            var autoMgr = autoType != null ? Activator.CreateInstance(autoType) : null;
            if (autoMgr == null)
            {
                LogDiag("Start: could not create automation DeviceManager");
                return;
            }

            var deviceIds = new List<string>();
            dynamic mgr = autoMgr;
            foreach (var info in mgr.DeviceInfos)
            {
                if (info.Type == 1)
                {
                    var deviceId = (string)info.DeviceID;
                    LogDiag($"Start: found scanner id={deviceId}");
                    deviceIds.Add(deviceId);
                }
            }
            Marshal.ReleaseComObject(autoMgr);

            if (deviceIds.Count == 0)
            {
                LogDiag("Start: no scanners found");
                return;
            }

            _devMgr2 = ComEventHelper.CreateWiaDevMgr2();
            if (_devMgr2 != null)
            {
                LogDiag("Start: created IWiaDevMgr2 via CoCreateInstance");

                _callback = new WiaEventCallbackImpl();
                _callback.OnImageEvent += OnWiaImageEvent;
                _callbackPin = GCHandle.Alloc(_callback, GCHandleType.Normal);

                foreach (var deviceId in deviceIds)
                {
                    try
                    {
                        var g = WIA_EVENT_SCAN_IMAGE;
                        int hr = _devMgr2.RegisterEventCallbackInterface(0, deviceId, ref g, _callback, out var pEventObj);
                        if (hr == 0 && pEventObj != IntPtr.Zero)
                            _eventObjects.Add(pEventObj);
                        LogDiag($"  RegisterEventCallbackInterface for {deviceId}: hr=0x{hr:X8} pObj={pEventObj}");
                    }
                    catch (Exception ex)
                    {
                        LogDiag($"  RegisterEventCallbackInterface failed: {ex.Message}");
                    }
                }
                LogDiag("Start: in-process callback registration complete");
            }

            if (!IsHandlerRegistered())
            {
                LogDiag("Start: handler not registered, launching elevated setup...");
                try
                {
                    var exePath = Environment.ProcessPath ?? "";
                    var psi = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = "/setup",
                        Verb = "runas",
                        UseShellExecute = true
                    };
                    using var setupProc = Process.Start(psi);
                    if (setupProc != null)
                    {
                        setupProc.WaitForExit(30000);
                        LogDiag($"  Setup process exited: {setupProc.ExitCode}");
                    }
                }
                catch (Exception ex)
                {
                    LogDiag($"  Failed to launch setup: {ex.Message}");
                }
            }
            else
            {
                LogDiag("Start: handler already registered");
            }

            _cts = new CancellationTokenSource();
            _pipeThread = new Thread(() => PipeServerLoop(_cts.Token))
            {
                IsBackground = true,
                Name = "WiaPipeServer"
            };
            _pipeThread.Start();
            LogDiag("Start: named pipe server started");
        }
        catch (Exception ex)
        {
            LogDiag($"Start exception: {ex.Message}");
        }
    }

    private void OnWiaImageEvent(string eventGuid, string deviceId)
    {
        LogDiag($"OnWiaImageEvent: event={eventGuid} device={deviceId}");
        ScanButtonPressed?.Invoke();
    }

    private void PipeServerLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && !_disposed)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    Program.PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.None,
                    0, 0);

                server.WaitForConnectionAsync(ct).Wait(ct);

                using var reader = new StreamReader(server);
                var line = reader.ReadLine();
                LogDiag($"PipeServer: received '{line}'");

                if (line == "SCAN")
                {
                    ScanButtonPressed?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LogDiag($"PipeServer exception: {ex.Message}");
                Thread.Sleep(1000);
            }
        }
        LogDiag("PipeServer: stopped");
    }

    public void Stop()
    {
        _disposed = true;
        try
        {
            _cts?.Cancel();
            _cts = null;

            foreach (var ptr in _eventObjects)
            {
                if (ptr != IntPtr.Zero) Marshal.Release(ptr);
            }
            _eventObjects.Clear();

            if (_callbackPin.IsAllocated)
                _callbackPin.Free();
            _callback = null;

            if (_devMgr2 != null)
            {
                Marshal.ReleaseComObject(_devMgr2);
                _devMgr2 = null;
            }

            LogDiag("Stop: cleaned up");
        }
        catch (Exception ex)
        {
            LogDiag($"Stop exception: {ex.Message}");
        }
    }

    public void Dispose() => Stop();
}
