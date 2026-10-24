using System.Diagnostics;
using System.ServiceProcess;
using Microsoft.Win32;

namespace SpeedScanInstaller;

/// <summary>
/// Fallback for "SpeedScanManager.exe /unregister" (Core/Interop/WiaEventWatcher.cs in the app),
/// used by the uninstaller when the exe is missing or the helper fails: removes the app's
/// ScanButton handlers from the global and per-device STI event keys, restores the Windows
/// handlers and LaunchApplications values the app backed up, and restarts the WIA service.
/// Keep in sync with WiaEventWatcher.DoRegistryUnregister.
/// </summary>
internal static class ScanButtonRegistry
{
    private const string OurGuid = "{B7E3A1F0-5C2D-4E8A-9F1A-3D6B7C8E9F12}";
    private const string OldOurGuid = "{33C60039-B6C1-4C33-9BB5-7B3B669D6631}";
    private const string ScanButtonGlobalKey = @"SYSTEM\CurrentControlSet\Control\StillImage\Events\ScanButton";
    private const string DeviceClassKey = @"SYSTEM\CurrentControlSet\Control\Class\{6bdd1fc6-810f-11d0-bec7-08002be2092f}";

    // Written by the x86 app, so physically under WOW6432Node (RegistryView.Registry32).
    private const string BackupKey = @"SOFTWARE\VeridonNetzwerk\SpeedScanManager\RemovedHandlers";
    private const string BackupHandlersKey = BackupKey + @"\Handlers";
    private const string BackupLaunchAppsKey = BackupKey + @"\LaunchApplications";

    private static readonly TimeSpan ServiceTimeout = TimeSpan.FromSeconds(20);

    /// <summary>Performs the unregistration. Returns true if everything could be undone.</summary>
    public static bool Unregister()
    {
        bool ok = true;
        try
        {
            // SYSTEM is not redirected; use the native view explicitly (on 32-bit Windows
            // Registry64 falls back to the only view there is).
            using var system = InstallerCore.OpenHklm(RegistryView.Registry64);
            ok &= RemoveOwnHandlers(system, ScanButtonGlobalKey);
            foreach (var evtPath in EnumerateDeviceEventKeys(system))
                ok &= RemoveOwnHandlers(system, evtPath);

            // The backup lives in the app's (32-bit) view; also check the 64-bit view in case
            // a 64-bit build of the app ever wrote it.
            foreach (var view in InstallerCore.AllRegistryViews)
                ok &= RestoreBackups(system, view);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ScanButtonRegistry.Unregister failed: {ex.Message}");
            ok = false;
        }
        finally
        {
            RestartWiaService();
        }
        return ok;
    }

    private static List<string> EnumerateDeviceEventKeys(RegistryKey hklm)
    {
        var result = new List<string>();
        try
        {
            using var classKey = hklm.OpenSubKey(DeviceClassKey, false);
            if (classKey == null) return result;
            foreach (var subKeyName in classKey.GetSubKeyNames())
            {
                var eventsPath = $@"{DeviceClassKey}\{subKeyName}\Events";
                using var eventsKey = hklm.OpenSubKey(eventsPath, false);
                if (eventsKey == null) continue;
                foreach (var evtKeyName in eventsKey.GetSubKeyNames())
                    result.Add($@"{eventsPath}\{evtKeyName}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"EnumerateDeviceEventKeys failed: {ex.Message}");
        }
        return result;
    }

    /// <summary>Same matching as WiaEventWatcher.IsOwnCmdLine.</summary>
    private static bool IsOwnCmdLine(string? cmdLine)
    {
        if (string.IsNullOrEmpty(cmdLine)) return false;
        return cmdLine.Contains("scanbutton.vbs", StringComparison.OrdinalIgnoreCase)
            || cmdLine.Contains("SpeedScanManager", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RemoveOwnHandlers(RegistryKey hklm, string keyPath)
    {
        try
        {
            using var key = hklm.OpenSubKey(keyPath, true);
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
                    key.DeleteSubKeyTree(subKeyName, false);
            }
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"RemoveOwnHandlers({keyPath}) failed: {ex.Message}");
            return false;
        }
    }

    private static bool RestoreBackups(RegistryKey system, RegistryView backupView)
    {
        bool ok = true;
        try
        {
            using var software = InstallerCore.OpenHklm(backupView);
            using (var backup = software.OpenSubKey(BackupKey, false))
            {
                if (backup == null) return true;
            }

            using (var handlers = software.OpenSubKey(BackupHandlersKey, false))
            {
                foreach (var recordName in handlers?.GetSubKeyNames() ?? Array.Empty<string>())
                {
                    try
                    {
                        using var record = handlers!.OpenSubKey(recordName, false);
                        var keyPath = record?.GetValue("KeyPath") as string;
                        var subKeyName = record?.GetValue("SubKeyName") as string;
                        using var data = record?.OpenSubKey("Data", false);
                        if (keyPath == null || subKeyName == null || data == null) continue;

                        using var target = system.OpenSubKey(keyPath, true);
                        if (target == null) continue; // device removed meanwhile
                        using var restored = target.CreateSubKey(subKeyName, true);
                        CopyKey(data, restored);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Restore handler {recordName} failed: {ex.Message}");
                        ok = false;
                    }
                }
            }

            using (var launch = software.OpenSubKey(BackupLaunchAppsKey, false))
            {
                foreach (var recordName in launch?.GetSubKeyNames() ?? Array.Empty<string>())
                {
                    try
                    {
                        using var record = launch!.OpenSubKey(recordName, false);
                        var keyPath = record?.GetValue("KeyPath") as string;
                        if (record == null || keyPath == null) continue;
                        bool hadValue = record.GetValue("HadValue") is int h && h != 0;
                        var oldValue = record.GetValue("Value") as string ?? "";

                        using var target = system.OpenSubKey(keyPath, true);
                        if (target == null) continue;
                        // Only undo the app's own change; leave values someone else set since.
                        if ((target.GetValue("LaunchApplications") as string) != "*") continue;

                        if (hadValue)
                            target.SetValue("LaunchApplications", oldValue);
                        else
                            target.DeleteValue("LaunchApplications", false);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Restore LaunchApplications {recordName} failed: {ex.Message}");
                        ok = false;
                    }
                }
            }

            if (ok)
                software.DeleteSubKeyTree(BackupKey, false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"RestoreBackups({backupView}) failed: {ex.Message}");
            ok = false;
        }
        return ok;
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

    /// <summary>Restarts stisvc so it rereads the handlers; the start is attempted even if stopping failed.</summary>
    private static void RestartWiaService()
    {
        ServiceController? service = null;
        try
        {
            service = new ServiceController("stisvc");
            service.Refresh();
            if (service.Status is not (ServiceControllerStatus.Stopped or ServiceControllerStatus.StopPending))
                service.Stop();
            service.WaitForStatus(ServiceControllerStatus.Stopped, ServiceTimeout);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Stopping stisvc failed: {ex.Message}");
        }
        finally
        {
            try
            {
                if (service != null)
                {
                    service.Refresh();
                    if (service.Status is ServiceControllerStatus.Stopped)
                        service.Start();
                    service.WaitForStatus(ServiceControllerStatus.Running, ServiceTimeout);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Starting stisvc failed: {ex.Message}");
            }
            service?.Dispose();
        }
    }
}
