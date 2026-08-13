using NTwain;
using NTwain.Data;

namespace SpeedScanManager;

/// <summary>
/// Snapshot of the current scanner state and hardware capabilities.
/// All properties are read-only and populated by <see cref="ScannerStateService.QueryState"/>.
/// </summary>
internal sealed record ScannerState
{
    public bool IsScannerConnected { get; init; }
    public bool SupportsADF { get; init; }
    public bool SupportsDuplex { get; init; }
    public bool SupportsSimplex { get; init; }
    public bool SupportsFlatbed { get; init; }
    public bool SupportsLongPaper { get; init; }
    public bool SupportsUltrasonicDetection { get; init; }
    public bool SupportsLengthDetection { get; init; }
    public string SourceName { get; init; } = "";
    public string Manufacturer { get; init; } = "";
    public string ProductFamily { get; init; } = "";
    public string DriverVersion { get; init; } = "";
}

/// <summary>
/// Central service that queries TWAIN/NTwain for scanner connection state and hardware capabilities.
/// The UI reads exclusively from <see cref="ScannerState"/> snapshots returned by <see cref="QueryState"/>.
/// All hardware communication is encapsulated here.
/// </summary>
internal sealed class ScannerStateService
{
    private readonly TwainSession _twain;
    private readonly WindowsFormsMessageLoopHook _msgLoop;
    private readonly string? _preferredSourceName;

    public ScannerStateService(TwainSession twain, WindowsFormsMessageLoopHook msgLoop, string? preferredSourceName = null)
    {
        _twain = twain;
        _msgLoop = msgLoop;
        _preferredSourceName = preferredSourceName;
    }

    /// <summary>
    /// Queries the current scanner state including all hardware capabilities.
    /// Opens and closes the data source temporarily to read capabilities.
    /// </summary>
    public ScannerState QueryState()
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
            source = ScannerSelectionDialog.SelectBestSource(_twain, _preferredSourceName);
            if (source == null)
                return DisconnectedState();

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
                var online = source.Capabilities.CapDeviceOnline.GetCurrent();
                if (online == BoolType.False)
                    return DisconnectedState();
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
        SupportsADF = false,
        SupportsDuplex = false,
        SupportsSimplex = false,
        SupportsFlatbed = false,
        SupportsLongPaper = false,
        SupportsUltrasonicDetection = false,
        SupportsLengthDetection = false,
        SourceName = ""
    };

    private static ScannerState QueryCapabilities(DataSource source)
    {
        bool supportsDuplex = false;
        bool supportsADF = false;
        bool supportsFlatbed = false;
        bool supportsLongPaper = false;
        bool supportsUltrasonic = false;
        bool supportsLength = false;

        // Duplex support
        try
        {
            var duplexValues = source.Capabilities.CapDuplexEnabled.GetValues();
            supportsDuplex = duplexValues.Contains(BoolType.True);
        }
        catch { }

        // Feeder (ADF) and flatbed support
        try
        {
            var feederValues = source.Capabilities.CapFeederEnabled.GetValues();
            supportsADF = feederValues.Contains(BoolType.True);
            // If feeder can be disabled (False), flatbed is available as alternative
            supportsFlatbed = feederValues.Contains(BoolType.False);
        }
        catch
        {
            // If CAP_FEEDERENABLED is not supported, the scanner is likely flatbed-only
            supportsFlatbed = true;
        }

        // Simplex is always supported if any scanning is possible
        bool supportsSimplex = true;

        // Long paper mode: check if supported sizes include None (non-standard = long paper)
        try
        {
            var sizes = source.Capabilities.ICapSupportedSizes.GetValues();
            supportsLongPaper = sizes.Contains(SupportedSize.None);
        }
        catch { }

        // Multi-feed detection capabilities
        try
        {
            var detectionValues = source.Capabilities.CapDoubleFeedDetection.GetValues();
            if (detectionValues != null && detectionValues.Any())
            {
                supportsUltrasonic = detectionValues.Contains(DoubleFeedDetection.Ultrasonic);
                supportsLength = detectionValues.Contains(DoubleFeedDetection.ByLength);
                LogDiag($"QueryState: CapDoubleFeedDetection GetValues: ultrasonic={supportsUltrasonic}, length={supportsLength}, values=[{string.Join(",", detectionValues)}]");
            }
            else
            {
                // GetValues returned empty — driver doesn't report available modes
                // but the capability exists. Assume both are supported since
                // source.Open() succeeded and the scanner is connected.
                LogDiag("QueryState: CapDoubleFeedDetection GetValues returned empty, assuming both supported");
                supportsUltrasonic = true;
                supportsLength = true;
            }
        }
        catch (Exception ex)
        {
            LogDiag($"QueryState: CapDoubleFeedDetection GetValues failed: {ex.Message}");
            // Fallback: try GetCurrent to see what's currently set
            try
            {
                var current = source.Capabilities.CapDoubleFeedDetection.GetCurrent();
                LogDiag($"QueryState: CapDoubleFeedDetection GetCurrent={current}");
                // GetCurrent only tells us what's currently active, not what's available.
                // Since the capability exists and source is open, assume both supported.
                supportsUltrasonic = true;
                supportsLength = true;
            }
            catch (Exception ex2)
            {
                LogDiag($"QueryState: CapDoubleFeedDetection GetCurrent also failed: {ex2.Message}, assuming both supported");
                supportsUltrasonic = true;
                supportsLength = true;
            }
        }

        return new ScannerState
        {
            IsScannerConnected = true,
            SupportsADF = supportsADF,
            SupportsDuplex = supportsDuplex,
            SupportsSimplex = supportsSimplex,
            SupportsFlatbed = supportsFlatbed,
            SupportsLongPaper = supportsLongPaper,
            SupportsUltrasonicDetection = supportsUltrasonic,
            SupportsLengthDetection = supportsLength,
            SourceName = source.Name ?? "",
            Manufacturer = source.Manufacturer ?? "",
            ProductFamily = source.ProductFamily ?? "",
            DriverVersion = source.Version.Info ?? ""
        };
    }

    private static void LogDiag(string msg) => DiagLog.Write(msg);
}
