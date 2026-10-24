using System.Diagnostics;
using Microsoft.Win32;

namespace SpeedScanManager;

/// <summary>
/// Scanner button support via the Windows Still Image (STI/WIA) event handler registry.
/// Strategy:
/// 1. Once per machine (elevated "/setup"): register "SpeedScanManager.exe /scanbutton ..."
///    as the handler for the ScanButton event (global and per device), remove the
///    Windows default handler (wiaacmgr) and remember what was removed so that
///    "/unregister" can restore it exactly. The registry entries persist — no admin
///    rights are needed on subsequent runs.
/// 2. When the button is pressed, STI launches "SpeedScanManager.exe /scanbutton", which
///    forwards "SCAN" to the running instance through <see cref="ScanButtonPipeServer"/>.
///
/// The in-process WIA COM callback (IWiaDevMgr2::RegisterEventCallbackInterface) was
/// removed: it never worked (wrong event GUID and wrong callback signature) and the
/// registry handler covers the use case.
/// </summary>
internal static class WiaEventWatcher
{
    private const string OurGuid = "{B7E3A1F0-5C2D-4E8A-9F1A-3D6B7C8E9F12}";
    private const string OldOurGuid = "{33C60039-B6C1-4C33-9BB5-7B3B669D6631}";
    private const string WiaacmgrClsid = "{D13E3F25-1688-45A0-9743-759EB35CDF9A}";
    private const string ScanButtonEventGuid = "{A6C5A715-8C6E-11D2-977A-0000F87A926F}";
    private const string ScanButtonGlobalKey = @"SYSTEM\CurrentControlSet\Control\StillImage\Events\ScanButton";
    private const string DeviceClassKey = @"SYSTEM\CurrentControlSet\Control\Class\{6bdd1fc6-810f-11d0-bec7-08002be2092f}";

    // HKLM: backup of everything setup removed or changed (restored by /unregister).
    private const string AppRegKey = @"SOFTWARE\VeridonNetzwerk\SpeedScanManager";
    private const string BackupKey = AppRegKey + @"\RemovedHandlers";
    private const string BackupHandlersKey = BackupKey + @"\Handlers";
    private const string BackupLaunchAppsKey = BackupKey + @"\LaunchApplications";

    // HKCU: the user declined the UAC prompt for the setup helper.
    private const string DeclinedValueName = "ScanButtonSetupDeclined";

    private static int _elevationAttempted;

    private static void LogDiag(string msg) => DiagLog.WriteWia(msg);

    // ───────────────────────── Handler command line ─────────────────────────

    private static string ExePath => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "SpeedScanManager.exe");

    /// <summary>
    /// The command line STI runs when the scanner button is pressed (%1 = device, %2 = event).
    /// </summary>
    internal static string ExpectedCmdLine => $"\"{ExePath}\" /scanbutton /StiDevice:%1 /StiEvent:%2";

    /// <summary>
    /// True for handler command lines that belong to this app (current or older versions,
    /// including the former wscript/scanbutton.vbs launcher).
    /// </summary>
    private static bool IsOwnCmdLine(string? cmdLine)
    {
        if (string.IsNullOrEmpty(cmdLine)) return false;
        return cmdLine.Contains("scanbutton.vbs", StringComparison.OrdinalIgnoreCase)
            || cmdLine.Contains("SpeedScanManager", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Cheap check (one registry read, no elevation): is our handler registered and does
    /// its command line point to the current exe?
    /// </summary>
    internal static bool IsHandlerRegistered()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(ScanButtonGlobalKey, false);
            if (key == null) return false;
            using var ourKey = key.OpenSubKey(OurGuid, false);
            if (ourKey == null) return false;

            var cmdLine = ourKey.GetValue("Cmdline") as string ?? "";
            var expected = ExpectedCmdLine;
            if (!string.Equals(cmdLine.Trim(), expected, StringComparison.OrdinalIgnoreCase))
            {
                LogDiag($"IsHandlerRegistered: Cmdline mismatch. Registered: {cmdLine} | Expected: {expected}");
                return false;
            }

            return true;
        }
        catch { return false; }
    }

    // ───────────────────────── Elevation (called by the tray) ─────────────────────────

    /// <summary>
    /// Ensures the scan button handler is registered. Call once at startup.
    /// Never blocks the calling thread: the registry check is cheap and the elevated
    /// "/setup" helper is launched on a worker thread. Elevation is attempted at most
    /// once per session, and not at all if the user declined the UAC prompt before
    /// (persisted in HKCU, see <see cref="ResetSetupDeclined"/>).
    /// Returns true if the handler is registered afterwards.
    /// </summary>
    public static Task<bool> EnsureScanButtonHandlerAsync()
    {
        if (IsHandlerRegistered())
        {
            LogDiag("EnsureScanButtonHandler: already registered");
            return Task.FromResult(true);
        }

        if (IsSetupDeclined())
        {
            LogDiag("EnsureScanButtonHandler: user declined elevation earlier, skipping");
            return Task.FromResult(false);
        }

        if (Interlocked.Exchange(ref _elevationAttempted, 1) == 1)
        {
            LogDiag("EnsureScanButtonHandler: elevation already attempted in this session");
            return Task.FromResult(false);
        }

        return Task.Run(() =>
        {
            try
            {
                LogDiag("EnsureScanButtonHandler: launching elevated /setup");
                var psi = new ProcessStartInfo
                {
                    FileName = ExePath,
                    Arguments = "/setup",
                    Verb = "runas",
                    UseShellExecute = true
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    if (proc.WaitForExit(60000))
                        LogDiag($"EnsureScanButtonHandler: setup exited with {proc.ExitCode}");
                    else
                        LogDiag("EnsureScanButtonHandler: setup still running after 60 s");
                }
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // ERROR_CANCELLED: the user declined the UAC prompt — don't ask again.
                LogDiag("EnsureScanButtonHandler: UAC declined, persisting decision");
                SetSetupDeclined(true);
                return false;
            }
            catch (Exception ex)
            {
                LogDiag($"EnsureScanButtonHandler: failed to launch setup: {ex.Message}");
                return false;
            }

            var ok = IsHandlerRegistered();
            LogDiag($"EnsureScanButtonHandler: registered={ok}");
            return ok;
        });
    }

    /// <summary>True if the user declined the UAC prompt for the scan button setup.</summary>
    public static bool IsSetupDeclined()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AppRegKey, false);
            return key?.GetValue(DeclinedValueName) is int v && v != 0;
        }
        catch { return false; }
    }

    /// <summary>Clears the persisted "declined" flag (e.g. from a settings button "Scan-Taste einrichten").</summary>
    public static void ResetSetupDeclined()
    {
        SetSetupDeclined(false);
        Interlocked.Exchange(ref _elevationAttempted, 0);
    }

    private static void SetSetupDeclined(bool declined)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(AppRegKey, true);
            if (declined)
                key.SetValue(DeclinedValueName, 1, RegistryValueKind.DWord);
            else
                key.DeleteValue(DeclinedValueName, false);
        }
        catch (Exception ex)
        {
            LogDiag($"SetSetupDeclined({declined}) failed: {ex.Message}");
        }
    }

    // ───────────────────────── /setup (elevated) ─────────────────────────

    /// <summary>
    /// Called from Program.cs when launched with /setup (elevated).
    /// Registers our exe as ScanButton handler, removes (and backs up) the Windows default
    /// handler and restarts the WIA service. Returns true on success.
    /// </summary>
    public static bool DoRegistrySetup()
    {
        try
        {
            LogDiag("DoRegistrySetup: starting (elevated)");
            var cmdLine = ExpectedCmdLine;
            LogDiag($"  Cmdline: {cmdLine}");

            ReplaceHandlerInKey(ScanButtonGlobalKey, cmdLine);

            foreach (var evtPath in EnumerateDeviceEventKeys(scanButtonOnly: true))
            {
                LogDiag($"  Found device-level ScanButton event at {evtPath}");
                ReplaceHandlerInKey(evtPath, cmdLine);
            }

            CleanupOldEntries();
            DeleteLegacyVbsLauncher();
            RestartWiaService();

            var ok = IsHandlerRegistered();
            LogDiag($"DoRegistrySetup: complete, registered={ok}");
            return ok;
        }
        catch (Exception ex)
        {
            LogDiag($"DoRegistrySetup failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Enumerates "...\Class\{imaging}\NNNN\Events\XYZ" key paths. With scanButtonOnly,
    /// only events whose GUID is the WIA ScanButton event are returned.
    /// </summary>
    private static List<string> EnumerateDeviceEventKeys(bool scanButtonOnly)
    {
        var result = new List<string>();
        try
        {
            using var classKey = Registry.LocalMachine.OpenSubKey(DeviceClassKey, false);
            if (classKey == null) return result;

            foreach (var subKeyName in classKey.GetSubKeyNames())
            {
                var eventsPath = $@"{DeviceClassKey}\{subKeyName}\Events";
                using var eventsKey = Registry.LocalMachine.OpenSubKey(eventsPath, false);
                if (eventsKey == null) continue;

                foreach (var evtKeyName in eventsKey.GetSubKeyNames())
                {
                    var evtPath = $@"{eventsPath}\{evtKeyName}";
                    if (scanButtonOnly)
                    {
                        using var evtKey = Registry.LocalMachine.OpenSubKey(evtPath, false);
                        var guid = evtKey?.GetValue("GUID") as string;
                        if (guid == null || !guid.Equals(ScanButtonEventGuid, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }
                    result.Add(evtPath);
                }
            }
        }
        catch (Exception ex)
        {
            LogDiag($"  EnumerateDeviceEventKeys failed: {ex.Message}");
        }
        return result;
    }

    private static void ReplaceHandlerInKey(string keyPath, string cmdLine)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath, true);
            if (key == null)
            {
                LogDiag($"  Key not found: {keyPath}");
                return;
            }

            // Remove the Windows default handler, but remember it for /unregister.
            using (var wiaacmgrSubKey = key.OpenSubKey(WiaacmgrClsid, false))
            {
                if (wiaacmgrSubKey != null)
                    BackupRemovedHandler(keyPath, WiaacmgrClsid, wiaacmgrSubKey);
            }
            if (key.GetSubKeyNames().Any(n => n.Equals(WiaacmgrClsid, StringComparison.OrdinalIgnoreCase)))
            {
                key.DeleteSubKeyTree(WiaacmgrClsid, false);
                LogDiag($"  Removed wiaacmgr from {keyPath}");
            }

            // Remove stale handlers of older versions (e.g. wscript scanbutton.vbs launchers).
            foreach (var subKeyName in key.GetSubKeyNames())
            {
                if (subKeyName.Equals(OurGuid, StringComparison.OrdinalIgnoreCase)) continue;
                string? existing;
                using (var subKey = key.OpenSubKey(subKeyName, false))
                    existing = subKey?.GetValue("Cmdline") as string;
                if (IsOwnCmdLine(existing))
                {
                    key.DeleteSubKeyTree(subKeyName, false);
                    LogDiag($"  Removed old handler {subKeyName} ({existing}) from {keyPath}");
                }
            }

            // (Re)create our handler so the Cmdline always matches the current exe location.
            key.DeleteSubKeyTree(OurGuid, false);
            using (var ourKey = key.CreateSubKey(OurGuid))
            {
                ourKey.SetValue("Name", "SpeedScan Manager");
                ourKey.SetValue("Desc", "Scan with SpeedScan Manager");
                ourKey.SetValue("Icon", $"{ExePath},0");
                ourKey.SetValue("Cmdline", cmdLine);
            }
            LogDiag($"  Registered handler in {keyPath}");

            var la = key.GetValue("LaunchApplications") as string;
            if (string.IsNullOrEmpty(la) || la == "Not Used")
            {
                BackupLaunchApplications(keyPath, la);
                key.SetValue("LaunchApplications", "*");
                LogDiag($"  Set LaunchApplications='*' in {keyPath} (was '{la ?? "(none)"}')");
            }
        }
        catch (Exception ex)
        {
            LogDiag($"  ReplaceHandlerInKey({keyPath}) failed: {ex.Message}");
        }
    }

    private static string EncodeBackupName(string keyPath, string? subKeyName = null)
    {
        var name = keyPath.Replace('\\', '|');
        return subKeyName == null ? name : $"{name}|{subKeyName}";
    }

    private static void BackupRemovedHandler(string keyPath, string subKeyName, RegistryKey source)
    {
        try
        {
            using var handlers = Registry.LocalMachine.CreateSubKey(BackupHandlersKey, true);
            var recordName = EncodeBackupName(keyPath, subKeyName);
            using (var existing = handlers.OpenSubKey(recordName, false))
            {
                if (existing != null) return; // keep the first (original) backup
            }

            using var record = handlers.CreateSubKey(recordName, true);
            record.SetValue("KeyPath", keyPath);
            record.SetValue("SubKeyName", subKeyName);
            using var data = record.CreateSubKey("Data", true);
            CopyKey(source, data);
            LogDiag($"  Backed up {keyPath}\\{subKeyName}");
        }
        catch (Exception ex)
        {
            LogDiag($"  BackupRemovedHandler({keyPath}\\{subKeyName}) failed: {ex.Message}");
        }
    }

    private static void BackupLaunchApplications(string keyPath, string? oldValue)
    {
        try
        {
            using var launch = Registry.LocalMachine.CreateSubKey(BackupLaunchAppsKey, true);
            var recordName = EncodeBackupName(keyPath);
            using (var existing = launch.OpenSubKey(recordName, false))
            {
                if (existing != null) return;
            }

            using var record = launch.CreateSubKey(recordName, true);
            record.SetValue("KeyPath", keyPath);
            record.SetValue("HadValue", oldValue != null ? 1 : 0, RegistryValueKind.DWord);
            record.SetValue("Value", oldValue ?? "");
        }
        catch (Exception ex)
        {
            LogDiag($"  BackupLaunchApplications({keyPath}) failed: {ex.Message}");
        }
    }

    private static void CopyKey(RegistryKey source, RegistryKey target)
    {
        foreach (var valueName in source.GetValueNames())
        {
            var kind = source.GetValueKind(valueName);
            var value = source.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (value != null)
                target.SetValue(valueName, value, kind);
        }
        foreach (var subName in source.GetSubKeyNames())
        {
            using var srcSub = source.OpenSubKey(subName, false);
            if (srcSub == null) continue;
            using var dstSub = target.CreateSubKey(subName, true);
            CopyKey(srcSub, dstSub);
        }
    }

    private static void CleanupOldEntries()
    {
        try
        {
            foreach (var evtPath in EnumerateDeviceEventKeys(scanButtonOnly: false))
            {
                using var evtKey = Registry.LocalMachine.OpenSubKey(evtPath, true);
                if (evtKey == null) continue;
                if (evtKey.GetSubKeyNames().Any(n => n.Equals(OldOurGuid, StringComparison.OrdinalIgnoreCase)))
                {
                    evtKey.DeleteSubKeyTree(OldOurGuid, false);
                    LogDiag($"  Cleaned up old entry {OldOurGuid} from {evtPath}");
                }
            }
        }
        catch (Exception ex)
        {
            LogDiag($"  CleanupOldEntries failed: {ex.Message}");
        }
    }

    private static void DeleteLegacyVbsLauncher()
    {
        try
        {
            var vbs = Path.Combine(Path.GetDirectoryName(ExePath) ?? AppContext.BaseDirectory, "scanbutton.vbs");
            if (File.Exists(vbs))
            {
                File.Delete(vbs);
                LogDiag($"  Deleted legacy launcher {vbs}");
            }
        }
        catch (Exception ex)
        {
            LogDiag($"  Deleting legacy scanbutton.vbs failed: {ex.Message}");
        }
    }

    // ───────────────────────── /unregister (elevated) ─────────────────────────

    /// <summary>
    /// Called from Program.cs when launched with /unregister (elevated, e.g. by the uninstaller).
    /// Removes our handler from the global and all per-device event keys, restores the
    /// handlers and LaunchApplications values that setup removed/changed, and restarts
    /// the WIA service. Returns true on success.
    /// </summary>
    public static bool DoRegistryUnregister()
    {
        bool ok = true;
        try
        {
            LogDiag("DoRegistryUnregister: starting (elevated)");

            ok &= RemoveOwnHandlers(ScanButtonGlobalKey);
            foreach (var evtPath in EnumerateDeviceEventKeys(scanButtonOnly: false))
                ok &= RemoveOwnHandlers(evtPath);

            ok &= RestoreBackups();

            RestartWiaService();
            LogDiag($"DoRegistryUnregister: complete, ok={ok}");
        }
        catch (Exception ex)
        {
            LogDiag($"DoRegistryUnregister failed: {ex.Message}");
            ok = false;
        }
        return ok;
    }

    private static bool RemoveOwnHandlers(string keyPath)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath, true);
            if (key == null) return true;

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                bool remove = subKeyName.Equals(OurGuid, StringComparison.OrdinalIgnoreCase)
                    || subKeyName.Equals(OldOurGuid, StringComparison.OrdinalIgnoreCase);
                if (!remove)
                {
                    using var subKey = key.OpenSubKey(subKeyName, false);
                    remove = IsOwnCmdLine(subKey?.GetValue("Cmdline") as string);
                }
                if (remove)
                {
                    key.DeleteSubKeyTree(subKeyName, false);
                    LogDiag($"  Removed handler {subKeyName} from {keyPath}");
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            LogDiag($"  RemoveOwnHandlers({keyPath}) failed: {ex.Message}");
            return false;
        }
    }

    private static bool RestoreBackups()
    {
        bool ok = true;
        try
        {
            using (var handlers = Registry.LocalMachine.OpenSubKey(BackupHandlersKey, false))
            {
                if (handlers != null)
                {
                    foreach (var recordName in handlers.GetSubKeyNames())
                    {
                        try
                        {
                            using var record = handlers.OpenSubKey(recordName, false);
                            var keyPath = record?.GetValue("KeyPath") as string;
                            var subKeyName = record?.GetValue("SubKeyName") as string;
                            using var data = record?.OpenSubKey("Data", false);
                            if (keyPath == null || subKeyName == null || data == null) continue;

                            using var target = Registry.LocalMachine.OpenSubKey(keyPath, true);
                            if (target == null)
                            {
                                LogDiag($"  Restore: {keyPath} no longer exists, skipping {subKeyName}");
                                continue;
                            }
                            using var restored = target.CreateSubKey(subKeyName, true);
                            CopyKey(data, restored);
                            LogDiag($"  Restored {keyPath}\\{subKeyName}");
                        }
                        catch (Exception ex)
                        {
                            LogDiag($"  Restore handler {recordName} failed: {ex.Message}");
                            ok = false;
                        }
                    }
                }
            }

            using (var launch = Registry.LocalMachine.OpenSubKey(BackupLaunchAppsKey, false))
            {
                if (launch != null)
                {
                    foreach (var recordName in launch.GetSubKeyNames())
                    {
                        try
                        {
                            using var record = launch.OpenSubKey(recordName, false);
                            var keyPath = record?.GetValue("KeyPath") as string;
                            if (keyPath == null) continue;
                            bool hadValue = record!.GetValue("HadValue") is int h && h != 0;
                            var oldValue = record.GetValue("Value") as string ?? "";

                            using var target = Registry.LocalMachine.OpenSubKey(keyPath, true);
                            if (target == null) continue;
                            // Only undo our own change; leave values someone else set since.
                            if ((target.GetValue("LaunchApplications") as string) != "*") continue;

                            if (hadValue)
                                target.SetValue("LaunchApplications", oldValue);
                            else
                                target.DeleteValue("LaunchApplications", false);
                            LogDiag($"  Restored LaunchApplications in {keyPath} to '{(hadValue ? oldValue : "(none)")}'");
                        }
                        catch (Exception ex)
                        {
                            LogDiag($"  Restore LaunchApplications {recordName} failed: {ex.Message}");
                            ok = false;
                        }
                    }
                }
            }

            if (ok)
                Registry.LocalMachine.DeleteSubKeyTree(BackupKey, false);
        }
        catch (Exception ex)
        {
            LogDiag($"  RestoreBackups failed: {ex.Message}");
            ok = false;
        }
        return ok;
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
}
