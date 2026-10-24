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
/// Presence check: opening a TWAIN source is slow and can make drivers show
/// "communication failed" dialogs, so the full query (Open + capabilities) only runs
/// when the set of present imaging devices (SetupAPI, no driver involved) changed,
/// the preferred source changed, <see cref="NotifyDeviceChange"/> was called
/// (tray: on WM_DEVICECHANGE), or the cached result is older than <see cref="MaxCacheAge"/>.
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

    public ScannerStateService(TwainSession twain, WindowsFormsMessageLoopHook msgLoop, string? preferredSourceName = null)
    {
        _twain = twain;
        _msgLoop = msgLoop;
        PreferredSourceName = preferredSourceName;
    }

    /// <summary>Source name used by <see cref="QueryState()"/>; null/empty = automatic selection.</summary>
    public string? PreferredSourceName { get; set; }

    /// <summary>Maximum age of a cached "connected" result before a full query is forced (covers network scanners).</summary>
    public TimeSpan MaxCacheAge { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Maximum age of a cached "disconnected" result. Shorter, so scanners that don't appear
    /// in the imaging device class (e.g. network scanners) are still detected reasonably fast.
    /// </summary>
    public TimeSpan MaxDisconnectedCacheAge { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Last state returned by a query (null before the first query).</summary>
    public ScannerState? LastState
    {
        get { lock (_sync) return _cached; }
    }

    /// <summary>
    /// Marks the cached state as stale so the next query opens the source again.
    /// Call from WM_DEVICECHANGE handling, after the scanner selection changed, etc.
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
    /// <see cref="PreferredSourceName"/>). <paramref name="forceRefresh"/> bypasses the cache.
    /// </summary>
    public ScannerState QueryState(string? preferredSourceName, bool forceRefresh = false)
    {
        lock (_sync)
        {
            PreferredSourceName = preferredSourceName;
            var fingerprint = GetImagingDeviceFingerprint();

            bool cacheValid = !forceRefresh
                && !_dirty
                && _cached != null
                && fingerprint != null
                && fingerprint == _cachedFingerprint
                && string.Equals(_cachedForSource ?? "", preferredSourceName ?? "", StringComparison.Ordinal)
                && DateTime.UtcNow - _cachedAtUtc < (_cached.IsScannerConnected ? MaxCacheAge : MaxDisconnectedCacheAge);

            if (cacheValid)
                return _cached!;

            LogDiag($"QueryState: full query (force={forceRefresh}, dirty={_dirty}, fingerprintChanged={fingerprint != _cachedFingerprint}, preferred='{preferredSourceName ?? "(auto)"}')");
            var state = QueryStateFull(preferredSourceName);

            _cached = state;
            _cachedFingerprint = fingerprint;
            _cachedForSource = preferredSourceName;
            _cachedAtUtc = DateTime.UtcNow;
            _dirty = false;
            return state;
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
    /// Returns a string identifying the set of currently present imaging devices
    /// (scanners/cameras), or null if the enumeration failed. Does not touch any driver.
    /// </summary>
    internal static string? GetImagingDeviceFingerprint()
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
            return string.Join("|", ids);
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

    private static void LogDiag(string msg) => DiagLog.Write(msg);
}
