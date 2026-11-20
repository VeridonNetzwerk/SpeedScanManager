using System.Runtime.InteropServices;
using System.Text;
using NTwain;
using NTwain.Data;

namespace SpeedScanManager;

/// <summary>
/// Snapshot of the current scanner state and hardware capabilities.
/// All properties are read-only and populated by <see cref="ScannerStateService.QueryState()"/>.
/// </summary>
internal sealed record ScannerState
{
    public bool IsScannerConnected { get; init; }
    public bool SupportsUltrasonicDetection { get; init; }
    public bool SupportsLengthDetection { get; init; }
    /// <summary>
    /// False if the driver did not report its multi-feed modes; the two Supports* flags
    /// are then false although the scanner may support detection ("unknown").
    /// </summary>
    public bool MultiFeedCapabilityKnown { get; init; }
    public bool SupportsDuplex { get; init; }
    public string SourceName { get; init; } = "";
    public string Manufacturer { get; init; } = "";
    public string ProductFamily { get; init; } = "";
    public string DriverVersion { get; init; } = "";
}

/// <summary>
/// Central service that queries TWAIN/NTwain for scanner connection state and hardware capabilities.
/// The UI reads exclusively from <see cref="ScannerState"/> snapshots returned by <see cref="QueryState()"/>.
/// All hardware communication is encapsulated here.
///
/// Opening a TWAIN source loads the vendor driver and talks to the device. Drivers (e.g.
/// Fujitsu) show their own error dialogs when that happens at a bad moment: device still
/// initialising after plug-in, device unplugged, another application holds it, or the
/// process is slowed down (antivirus scan right after start). So the source is opened only
/// when it is needed:
/// <list type="bullet">
/// <item>Presence is decided by a SetupAPI check of the imaging device class (no driver
/// involved). Once the scan source was found connected, the hardware keys (VID/PID) of its
/// device are remembered (also across restarts) and decide connected/disconnected alone.</item>
/// <item>The source is opened (capabilities) when the scanner's device appears, a new
/// imaging device was added, the selected source changed, the caller forces a refresh
/// (WM_DEVICECHANGE retries, user action) while the device is present, or the scanner's
/// hardware is not known (network scanners, see <see cref="MaxCacheAge"/>).</item>
/// <item>Never while the scanner's known device is absent. Failed probes back off
/// (<see cref="MinRetryDelay"/> doubling up to <see cref="MaxRetryDelay"/>).</item>
/// <item>At startup the last known state (scanner_state.json) is reused when the same device,
/// source and driver version are present, so the source is not opened at all.</item>
/// </list>
/// Must be called on the thread that owns the TWAIN session (UI thread).
/// </summary>
internal sealed class ScannerStateService
{
    private readonly TwainSession _twain;
    private readonly WindowsFormsMessageLoopHook _msgLoop;
    private readonly object _sync = new();

    private ScannerState? _cached;
    private string? _cachedFingerprint;
    private string? _cachedForSource;
    private DateTime _cachedAtUtc;
    private bool _dirty = true;

    /// <summary>Hardware keys (VID/PID) of the scanner's device; null = unknown (network scanner, not found yet).</summary>
    private HashSet<string>? _scannerHwKeys;
    /// <summary>Imaging device keys present at the previous query (detects newly added devices).</summary>
    private HashSet<string>? _lastSeenKeys;
    /// <summary>Imaging device keys present at the last full query that found no scanner.</summary>
    private HashSet<string>? _lastDisconnectedKeys;
    private int _failureStreak;
    private DateTime _nextProbeUtc;
    private bool _persistedChecked;

    public ScannerStateService(TwainSession twain, WindowsFormsMessageLoopHook msgLoop, string? preferredSourceName = null)
    {
        _twain = twain;
        _msgLoop = msgLoop;
        PreferredSourceName = preferredSourceName;
    }

    /// <summary>Source name used by <see cref="QueryState()"/>; null/empty = automatic selection.</summary>
    public string? PreferredSourceName { get; set; }

    /// <summary>
    /// Maximum age of a cached "connected" result before a full query is forced. Only used
    /// when the scanner's hardware is unknown (network scanners); a USB scanner is tracked
    /// by its device presence instead.
    /// </summary>
    public TimeSpan MaxCacheAge { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>First delay before a failed background probe is repeated (doubles per failure).</summary>
    public TimeSpan MinRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Upper bound of the delay between failed background probes.</summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Last state returned by a query (null before the first query).</summary>
    public ScannerState? LastState
    {
        get { lock (_sync) return _cached; }
    }

    /// <summary>
    /// Marks the cached state as stale (WM_DEVICECHANGE, scanner selection changed, ...).
    /// Leads to opening the source only if the imaging device list changed or can't be read.
    /// </summary>
    public void NotifyDeviceChange()
    {
        lock (_sync) _dirty = true;
    }

    /// <summary>
    /// Queries the current scanner state for <see cref="PreferredSourceName"/>.
    /// Cheap when nothing changed (cached), otherwise opens and closes the data source
    /// temporarily to read capabilities.
    /// </summary>
    public ScannerState QueryState() => QueryState(PreferredSourceName);

    /// <summary>
    /// Queries the current scanner state for the given source (also stored as
    /// <see cref="PreferredSourceName"/>). <paramref name="forceRefresh"/> bypasses the cache,
    /// but the source is still never opened while the scanner's known device is absent.
    /// </summary>
    public ScannerState QueryState(string? preferredSourceName, bool forceRefresh = false)
    {
        lock (_sync)
        {
            PreferredSourceName = preferredSourceName;
            var ids = GetPresentImagingDeviceIds();
            var fingerprint = ids == null ? null : string.Join("|", ids);
            var keys = ids?.Select(HardwareKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var now = DateTime.UtcNow;

            bool sourceChanged = _cached != null && !SameSource(_cachedForSource, preferredSourceName);
            if (sourceChanged)
            {
                // The remembered hardware belongs to the previously selected scanner.
                _scannerHwKeys = null;
                _lastDisconnectedKeys = null;
                _failureStreak = 0;
            }

            // Imaging devices added since the previous query (a different scanner may have appeared).
            bool deviceAdded = keys != null && _lastSeenKeys != null && keys.Except(_lastSeenKeys).Any();
            if (keys != null)
                _lastSeenKeys = keys;

            // First query of this session: reuse the last known state if the same device is present.
            if (!_persistedChecked)
            {
                _persistedChecked = true;
                if (!forceRefresh)
                {
                    var persisted = TryUsePersistedState(preferredSourceName, keys);
                    if (persisted != null)
                        return Remember(persisted, fingerprint, preferredSourceName, now);
                }
            }

            bool? present = _scannerHwKeys is { Count: > 0 } && keys != null
                ? _scannerHwKeys.Overlaps(keys)
                : null;

            if (present == false && !deviceAdded && !sourceChanged)
            {
                // The scanner's device is not there: opening the source could only fail and
                // may make the driver show "scanner not connected" dialogs.
                if (_cached == null || _cached.IsScannerConnected)
                    LogDiag("QueryState: scanner device not present -> disconnected (source not opened)");
                _failureStreak = 0;
                return Remember(DisconnectedState(), fingerprint, preferredSourceName, now);
            }

            if (!forceRefresh && !sourceChanged && _cached != null)
            {
                bool changed = fingerprint == null ? _dirty : fingerprint != _cachedFingerprint;
                if (_cached.IsScannerConnected)
                {
                    // Decisive presence: the single known device of the scanner is there.
                    if (present == true && _scannerHwKeys!.Count == 1)
                        return _cached;
                    bool hwUnknown = _scannerHwKeys is not { Count: > 0 };
                    if (!changed && (!hwUnknown || now - _cachedAtUtc < MaxCacheAge))
                        return _cached;
                }
                else if (!changed && !deviceAdded && now < _nextProbeUtc)
                {
                    return _cached;
                }
            }

            LogDiag($"QueryState: full query (force={forceRefresh}, dirty={_dirty}, fingerprintChanged={fingerprint != _cachedFingerprint}, deviceAdded={deviceAdded}, present={present?.ToString() ?? "unknown"}, preferred='{preferredSourceName ?? "(auto)"}')");
            var state = QueryStateFull(preferredSourceName);

            if (state.IsScannerConnected)
            {
                if (_failureStreak > 1)
                    LogDiag($"QueryState: scanner reachable again after {_failureStreak} failed probes");
                _failureStreak = 0;
                LearnScannerHardware(keys);
                SavePersistedState(preferredSourceName, state);
            }
            else
            {
                _failureStreak++;
                var delay = TimeSpan.FromTicks(Math.Min(
                    MaxRetryDelay.Ticks,
                    MinRetryDelay.Ticks * (1L << Math.Min(_failureStreak - 1, 10))));
                _nextProbeUtc = now + delay;
                _lastDisconnectedKeys = keys;
                if (_failureStreak == 1 || _failureStreak % 10 == 0)
                    LogDiag($"QueryState: no scanner (failed probes in a row: {_failureStreak}), next background probe in {delay.TotalSeconds:0} s");
            }

            return Remember(state, fingerprint, preferredSourceName, now);
        }
    }

    private ScannerState Remember(ScannerState state, string? fingerprint, string? preferredSourceName, DateTime now)
    {
        _cached = state;
        _cachedFingerprint = fingerprint;
        _cachedForSource = preferredSourceName;
        _cachedAtUtc = now;
        _dirty = false;
        return state;
    }

    private static bool SameSource(string? a, string? b) =>
        string.Equals(a ?? "", b ?? "", StringComparison.Ordinal);

    /// <summary>
    /// Remembers which imaging device belongs to the connected scanner: the devices added
    /// since the last "no scanner" result, else the previously known ones that are still
    /// present, else all present devices.
    /// </summary>
    private void LearnScannerHardware(HashSet<string>? keys)
    {
        if (keys == null || keys.Count == 0)
        {
            // Not a PnP imaging device (network scanner?) or the enumeration failed.
            _scannerHwKeys = null;
            return;
        }

        HashSet<string>? learned = null;
        if (_lastDisconnectedKeys != null)
        {
            var added = keys.Except(_lastDisconnectedKeys, StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (added.Count > 0)
                learned = added;
        }
        if (learned == null && _scannerHwKeys is { Count: > 0 })
        {
            var still = _scannerHwKeys.Intersect(keys, StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (still.Count > 0)
                learned = still;
        }
        learned ??= new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);

        if (_scannerHwKeys == null || !_scannerHwKeys.SetEquals(learned))
            LogDiag($"QueryState: scanner hardware = {string.Join(", ", learned)}");
        _scannerHwKeys = learned;
        _lastDisconnectedKeys = null;
    }

    // ───────────────────────── Last known state (across restarts) ─────────────────────────

    private sealed class PersistedState
    {
        public string? PreferredSource { get; set; }
        public List<string>? HardwareKeys { get; set; }
        public ScannerState? State { get; set; }
    }

    private static string StateFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SpeedScanManager", "scanner_state.json");

    /// <summary>
    /// Startup: if the scanner that was connected last time is present again (same hardware,
    /// TWAIN source and driver version), returns its state without opening the source. If its
    /// hardware is known but absent, returns "disconnected" (also without opening the source).
    /// Otherwise null (a full query is needed).
    /// </summary>
    private ScannerState? TryUsePersistedState(string? preferredSourceName, HashSet<string>? keys)
    {
        PersistedState? p;
        try
        {
            if (!File.Exists(StateFilePath)) return null;
            p = System.Text.Json.JsonSerializer.Deserialize<PersistedState>(File.ReadAllText(StateFilePath));
        }
        catch (Exception ex)
        {
            LogDiag($"QueryState: last known state unreadable: {ex.Message}");
            return null;
        }

        if (p?.State == null || p.HardwareKeys is not { Count: > 0 } || keys == null
            || !SameSource(p.PreferredSource, preferredSourceName))
            return null;

        _scannerHwKeys = p.HardwareKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!_scannerHwKeys.Overlaps(keys))
        {
            LogDiag("QueryState: known scanner device not present at startup -> disconnected (source not opened)");
            return DisconnectedState();
        }

        try
        {
            if (!_twain.IsDsmOpen && _twain.Open(_msgLoop) != ReturnCode.Success)
                return null;
            // Identity only (from the DSM's source list): the source is not opened.
            var source = ScannerSelectionDialog.SelectBestSource(_twain, preferredSourceName);
            if (source == null
                || !string.Equals(source.Name ?? "", p.State.SourceName, StringComparison.Ordinal)
                || !string.Equals(source.Version.Info ?? "", p.State.DriverVersion, StringComparison.Ordinal))
                return null;
        }
        catch
        {
            return null;
        }

        LogDiag($"QueryState: scanner device present, using last known capabilities of '{p.State.SourceName}' (source not opened)");
        return p.State with { IsScannerConnected = true };
    }

    private void SavePersistedState(string? preferredSourceName, ScannerState state)
    {
        if (_scannerHwKeys is not { Count: > 0 }) return;
        try
        {
            var data = new PersistedState
            {
                PreferredSource = preferredSourceName,
                HardwareKeys = _scannerHwKeys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList(),
                State = state
            };
            Directory.CreateDirectory(Path.GetDirectoryName(StateFilePath)!);
            File.WriteAllText(StateFilePath, System.Text.Json.JsonSerializer.Serialize(data));
        }
        catch (Exception ex)
        {
            LogDiag($"QueryState: could not save last known state: {ex.Message}");
        }
    }

    private ScannerState QueryStateFull(string? preferredSourceName)
    {
        if (!_twain.IsDsmOpen)
        {
            try
            {
                var rc = _twain.Open(_msgLoop);
                if (rc != ReturnCode.Success)
                    return DisconnectedState();
            }
            catch
            {
                return DisconnectedState();
            }
        }

        DataSource? source = null;
        bool sourceWasAlreadyOpen = false;
        try
        {
            // Use the centralized selection logic (saved preference + USB preference)
            source = ScannerSelectionDialog.SelectBestSource(_twain, preferredSourceName);
            if (source == null)
            {
                // No sources found — the scanner may have been connected after
                // the DSM was opened. Close and reopen the DSM to force TWAIN
                // to re-enumerate available sources.
                LogDiag("QueryState: no sources found, reopening DSM to re-enumerate");
                try
                {
                    _twain.Close();
                    var reopenRc = _twain.Open(_msgLoop);
                    LogDiag($"QueryState: DSM reopen rc={reopenRc}");
                    if (reopenRc == ReturnCode.Success)
                    {
                        source = ScannerSelectionDialog.SelectBestSource(_twain, preferredSourceName);
                    }
                }
                catch (Exception ex)
                {
                    LogDiag($"QueryState: DSM reopen failed: {ex.Message}");
                }
                if (source == null)
                    return DisconnectedState();
            }

            // If the source is already open (e.g. persistent source for device events),
            // don't close it when we're done — just use it as-is.
            sourceWasAlreadyOpen = source.IsOpen;
            if (!sourceWasAlreadyOpen)
            {
                var openRc = source.Open();
                if (openRc != ReturnCode.Success)
                {
                    LogDiag($"QueryState: source.Open rc={openRc}, returning disconnected");
                    return DisconnectedState();
                }
            }

            // Check if the device is actually online (hardware powered & connected)
            try
            {
                if (source.Capabilities.CapDeviceOnline.IsSupported)
                {
                    var online = source.Capabilities.CapDeviceOnline.GetCurrent();
                    LogDiag($"QueryState: CapDeviceOnline={online}");
                    if (online == BoolType.False)
                        return DisconnectedState();
                }
            }
            catch
            {
                // CapDeviceOnline not supported by this driver — assume connected
                // since source.Open() succeeded
            }

            return QueryCapabilities(source);
        }
        catch
        {
            return DisconnectedState();
        }
        finally
        {
            // Only close the source if we opened it (don't close persistent source)
            if (source != null && source.IsOpen && !sourceWasAlreadyOpen)
            {
                try { source.Close(); } catch { }
            }
        }
    }

    private static ScannerState DisconnectedState() => new()
    {
        IsScannerConnected = false,
        SupportsUltrasonicDetection = false,
        SupportsLengthDetection = false,
        SourceName = ""
    };

    private static ScannerState QueryCapabilities(DataSource source)
    {
        bool supportsUltrasonic = false;
        bool supportsLength = false;
        bool known = false;

        // Multi-feed detection capabilities. If the driver doesn't report the modes,
        // report "not supported / unknown" instead of guessing.
        try
        {
            var detectionValues = source.Capabilities.CapDoubleFeedDetection.GetValues()?.ToList();
            if (detectionValues != null && detectionValues.Count > 0)
            {
                supportsUltrasonic = detectionValues.Contains(DoubleFeedDetection.Ultrasonic);
                supportsLength = detectionValues.Contains(DoubleFeedDetection.ByLength);
                known = true;
                LogDiag($"QueryState: CapDoubleFeedDetection values=[{string.Join(",", detectionValues)}] ultrasonic={supportsUltrasonic}, length={supportsLength}");
            }
            else
            {
                LogDiag("QueryState: CapDoubleFeedDetection reported no values -> unknown (reported as unsupported)");
            }
        }
        catch (Exception ex)
        {
            LogDiag($"QueryState: CapDoubleFeedDetection GetValues failed: {ex.Message} -> unknown (reported as unsupported)");
        }

        bool supportsDuplex = false;
        try
        {
            var duplex = source.Capabilities.CapDuplex.GetCurrent();
            supportsDuplex = duplex == Duplex.OnePass || duplex == Duplex.TwoPass;
            LogDiag($"QueryState: CapDuplex={duplex}");
        }
        catch (Exception ex)
        {
            LogDiag($"QueryState: CapDuplex failed: {ex.Message}");
        }

        return new ScannerState
        {
            IsScannerConnected = true,
            SupportsUltrasonicDetection = supportsUltrasonic,
            SupportsLengthDetection = supportsLength,
            MultiFeedCapabilityKnown = known,
            SupportsDuplex = supportsDuplex,
            SourceName = source.Name ?? "",
            Manufacturer = source.Manufacturer ?? "",
            ProductFamily = source.ProductFamily ?? "",
            DriverVersion = source.Version.Info ?? ""
        };
    }

    // ───────────────────────── Cheap presence check (SetupAPI) ─────────────────────────

    private static readonly Guid ImagingDeviceClass = new("6bdd1fc6-810f-11d0-bec7-08002be2092f");
    private const uint DIGCF_PRESENT = 0x2;

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevsW(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, uint memberIndex, ref SP_DEVINFO_DATA deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInstanceIdW(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData,
        StringBuilder deviceInstanceId, int deviceInstanceIdSize, out int requiredSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    /// <summary>
    /// Returns the sorted device instance ids of the currently present imaging devices
    /// (scanners/cameras), or null if the enumeration failed. Does not touch any driver.
    /// </summary>
    internal static List<string>? GetPresentImagingDeviceIds()
    {
        var classGuid = ImagingDeviceClass;
        var set = SetupDiGetClassDevsW(ref classGuid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT);
        if (set == IntPtr.Zero || set == new IntPtr(-1))
            return null;

        try
        {
            var ids = new List<string>();
            var info = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
            var sb = new StringBuilder(512);
            for (uint i = 0; SetupDiEnumDeviceInfo(set, i, ref info); i++)
            {
                sb.Clear();
                if (SetupDiGetDeviceInstanceIdW(set, ref info, sb, sb.Capacity, out _))
                    ids.Add(sb.ToString());
            }
            ids.Sort(StringComparer.OrdinalIgnoreCase);
            return ids;
        }
        catch
        {
            return null;
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(set);
        }
    }

    /// <summary>
    /// Port-independent key of a device instance id: "VID_04C5&amp;PID_114D" for USB devices
    /// (stays the same when the scanner is plugged into another port), else the full id.
    /// </summary>
    internal static string HardwareKey(string instanceId)
    {
        var upper = instanceId.ToUpperInvariant();
        int vid = upper.IndexOf("VID_", StringComparison.Ordinal);
        int pid = upper.IndexOf("PID_", StringComparison.Ordinal);
        if (vid >= 0 && pid > vid && upper.Length >= vid + 8 && upper.Length >= pid + 8)
            return $"{upper.Substring(vid, 8)}&{upper.Substring(pid, 8)}";
        return upper;
    }

    private static void LogDiag(string msg) => DiagLog.Write(msg);
}
