using System.Diagnostics;
using System.Drawing;
using NTwain;
using NTwain.Data;

namespace SpeedScanManager;

/// <summary>
/// Result of one scan batch (<see cref="ScanPipeline.Scan"/>).
/// </summary>
internal sealed class ScanResult
{
    /// <summary>Acquired pages in reading order. Owned by the caller (dispose them).</summary>
    public List<Bitmap> Images { get; } = new();

    /// <summary>User cancelled (multi-feed "Wiederholen"/dialog closed, <see cref="ScanPipeline.RequestCancel"/>) or no source was selected. Images are discarded.</summary>
    public bool WasCancelled { get; internal set; }

    /// <summary>No TWAIN event arrived for the inactivity timeout; the scan was aborted. Images received so far are kept.</summary>
    public bool TimedOut { get; internal set; }

    /// <summary>Pages the driver announced but that could not be received/decoded (e.g. out of memory).</summary>
    public int FailedTransfers { get; internal set; }
    public List<string> FailedTransferMessages { get; } = new();

    /// <summary>A multi-feed (double feed) was reported by the scanner.</summary>
    public bool MultiFeedDetected { get; internal set; }

    /// <summary>Set when DSM/source could not be opened or enabled (German, user-facing).</summary>
    public string? ErrorMessage { get; internal set; }

    /// <summary>Non-fatal problems (e.g. a capability the scanner refused). German, user-facing.</summary>
    public List<string> Warnings { get; } = new();

    /// <summary>Pages were scanned duplex: Images[0,2,4..] are fronts, Images[1,3,5..] backs.</summary>
    public bool WasDuplex { get; internal set; }

    /// <summary>
    /// ScanSide "Automatisch" scanned duplex: blank back sides should be removed even if
    /// "Leere Seiten löschen" is off (pass on to <see cref="ScanOutputOptions"/>).
    /// </summary>
    public bool RemoveBlankBacks { get; internal set; }

    public float NegotiatedDpiX { get; internal set; }
    public float NegotiatedDpiY { get; internal set; }
    public string SourceName { get; internal set; } = "";

    /// <summary>
    /// "Scanvorgang fortsetzen": appends a following batch. Takes ownership of the
    /// other result's images (its list is emptied) and merges the counters.
    /// </summary>
    public void Append(ScanResult next)
    {
        Images.AddRange(next.Images);
        next.Images.Clear();
        FailedTransfers += next.FailedTransfers;
        FailedTransferMessages.AddRange(next.FailedTransferMessages);
        Warnings.AddRange(next.Warnings.Where(w => !Warnings.Contains(w)));
        MultiFeedDetected |= next.MultiFeedDetected;
        TimedOut |= next.TimedOut;
        // Front/back parity is only meaningful if every batch was duplex.
        WasDuplex &= next.WasDuplex;
        RemoveBlankBacks &= next.RemoveBlankBacks;
    }

    public void DisposeImages()
    {
        foreach (var img in Images) img.Dispose();
        Images.Clear();
    }
}

/// <summary>
/// Core scan pipeline: opens a TWAIN data source, configures capabilities,
/// acquires images, and returns them as Bitmap list.
///
/// THREADING: NTwain dispatches TWAIN messages through the message loop hook of the
/// hidden UI window, so all source events are raised on the UI thread. To follow the
/// TWAIN rule that a source is driven from one thread, every DSM/source call
/// (open, capability negotiation, enable, step-down, close) is marshalled to that
/// UI thread via <see cref="MessageLoopHook.Invoke"/>. <see cref="Scan"/> itself is meant
/// to run on a worker thread (the tray uses Task.Run) and only waits; if it is called on
/// the UI thread it pumps messages while waiting.
///
/// CONTINUE SCANNING: <see cref="Scan"/> can be called repeatedly on the same instance
/// (one batch per call). Collect the batches with <see cref="ScanResult.Append"/> and
/// process them once with ScanOutputProcessor.
/// </summary>
internal class ScanPipeline : IDisposable
{
    private static readonly TimeSpan InitialTimeout = TimeSpan.FromSeconds(120);   // until the first page arrives
    private static readonly TimeSpan InactivityTimeout = TimeSpan.FromSeconds(60); // between TWAIN events
    private static readonly TimeSpan CancelGracePeriod = TimeSpan.FromSeconds(5);

    private readonly TwainSession _twain;
    private readonly WindowsFormsMessageLoopHook _msgLoop;
    private readonly Form _hiddenWindow;
    private readonly ScanSettings _settingsSource;
    private readonly string? _preferredSourceName;
    private IntPtr _windowHandle;

    // Per-scan state (reset at the start of every Scan call)
    private ScanSettings _settings;
    private CustomPaperSize? _customPaperSize;
    private DataSource? _currentSource;
    private readonly List<Bitmap> _acquiredImages = new();
    private ScanResult _result = new();
    private readonly ManualResetEventSlim _scanComplete = new(false);
    private long _lastActivityTimestamp; // Stopwatch timestamp, written by the UI thread, read by the waiting thread
    private volatile bool _anyEventSeen;
    private volatile bool _cancelRequested;
    private volatile bool _cancelAllPending;
    private volatile bool _discardImages;
    private volatile bool _modalOpen;
    private bool _multiFeedDialogQueued;
    private MultiFeedWarningDialog? _multiFeedDialog; // UI thread only
    private bool _ignoreMultiFeed;
    private bool _subscribed;

    private static void LogDiag(string msg) => DiagLog.Write(msg);

    /// <summary>Images of the last batch (bitmaps belong to the caller of <see cref="Scan"/>).</summary>
    public IReadOnlyList<Bitmap> AcquiredImages => _acquiredImages;

    public bool WasCancelled => _result.WasCancelled;

    /// <summary>Result of the last <see cref="Scan"/> call.</summary>
    public ScanResult LastResult => _result;

    private bool IsLongPageMode => _settings.PaperSize == PaperSizeMode.Automatic
        && _settings.MultiFeedDetection != MultiFeedDetection.Length
        && _settings.MultiFeedDetection != MultiFeedDetection.Both;

    public ScanPipeline(TwainSession twain, WindowsFormsMessageLoopHook msgLoop, Form hiddenWindow, ScanSettings settings, string? preferredSourceName = null)
    {
        _twain = twain;
        _msgLoop = msgLoop;
        _hiddenWindow = hiddenWindow;
        _settingsSource = settings;
        _settings = settings;
        _preferredSourceName = preferredSourceName;
    }

    /// <summary>
    /// Same as the other constructor, with the window handle obtained on the UI thread
    /// beforehand (avoids touching the Form from the scan thread).
    /// </summary>
    public ScanPipeline(TwainSession twain, WindowsFormsMessageLoopHook msgLoop, Form hiddenWindow, IntPtr windowHandle, ScanSettings settings, string? preferredSourceName = null)
        : this(twain, msgLoop, hiddenWindow, settings, preferredSourceName)
    {
        _windowHandle = windowHandle;
    }

    /// <summary>
    /// Requests cancellation of the running scan: remaining pages are cancelled at the
    /// next transfer (CancelAll); if the driver doesn't respond, the source is forced back
    /// to state 4 after a grace period. Acquired images are discarded.
    /// </summary>
    public void RequestCancel()
    {
        LogDiag("RequestCancel: cancellation requested");
        _cancelRequested = true;

        // An open multi-feed dialog would otherwise keep the scan waiting: close it
        // (closing means "discard", the same as the cancel request).
        try
        {
            if (!_hiddenWindow.IsDisposed && _hiddenWindow.IsHandleCreated)
                _hiddenWindow.BeginInvoke(CloseMultiFeedDialog);
        }
        catch (Exception ex)
        {
            LogDiag($"RequestCancel: could not close the multi-feed dialog: {ex.Message}");
        }
    }

    private void CloseMultiFeedDialog()
    {
        var dlg = _multiFeedDialog;
        if (dlg == null || dlg.IsDisposed) return;
        LogDiag("RequestCancel: closing the multi-feed dialog");
        dlg.DialogResult = DialogResult.Cancel;
        dlg.Close();
    }

    /// <summary>
    /// Executes one scan batch. Never throws for TWAIN problems; see the result flags.
    /// The returned images are owned by the caller.
    /// </summary>
    public ScanResult Scan(ScanSide scanSideOverride)
    {
        // Snapshot the shared settings so UI changes during the scan can't interfere.
        _settings = _settingsSource.Clone();
        _customPaperSize = _settings.PaperSize == PaperSizeMode.Custom
            && _settings.SelectedCustomSizeIndex >= 0
            && _settings.SelectedCustomSizeIndex < _settings.CustomPaperSizes.Count
                ? _settings.CustomPaperSizes[_settings.SelectedCustomSizeIndex]
                : null;

        _result = new ScanResult();
        _acquiredImages.Clear();
        _scanComplete.Reset();
        _cancelRequested = false;
        _cancelAllPending = false;
        _discardImages = false;
        _modalOpen = false;
        _multiFeedDialogQueued = false;
        _ignoreMultiFeed = false;
        _anyEventSeen = false;
        var result = _result;

        bool onUiThread = Application.MessageLoop;
        bool enabled = false;

        // The DSM must be open before calls can be marshalled through the message loop hook.
        if (!_twain.IsDsmOpen)
        {
            var rc = _twain.Open(_msgLoop);
            if (rc != ReturnCode.Success)
            {
                LogDiag($"DSM open failed: {rc}");
                result.ErrorMessage = "Der TWAIN-Quellmanager konnte nicht geöffnet werden.";
                return result;
            }
        }

        try
        {
            RunOnTwainThread(() => enabled = OpenConfigureAndEnable(scanSideOverride));
            if (enabled)
                WaitForCompletion(onUiThread);
        }
        catch (Exception ex)
        {
            LogDiag($"Scan exception: {ex}");
            result.ErrorMessage ??= $"Scanfehler: {ex.Message}";
        }
        finally
        {
            try { RunOnTwainThread(Cleanup); }
            catch (Exception ex) { LogDiag($"Scan cleanup exception: {ex.Message}"); }
        }

        // A multi-feed dialog may still be open (scan ended right after the event): wait for the decision.
        WaitForModal(onUiThread);

        if (_cancelRequested || _discardImages)
        {
            result.WasCancelled = true;
            LogDiag($"Scan: cancelled, discarding {_acquiredImages.Count} image(s)");
            foreach (var img in _acquiredImages) img.Dispose();
            _acquiredImages.Clear();
        }
        else if (_settings.FaceUpFeeding && _acquiredImages.Count > 1)
        {
            // Face-up loading: the stack is fed last page first (and in duplex the first
            // side read is the back of the last page), so reversing the flat list restores
            // reading order and keeps front/back pairs together.
            _acquiredImages.Reverse();
            LogDiag("Scan: face-up feeding, page order reversed");
        }

        result.Images.AddRange(_acquiredImages);
        LogDiag($"Scan: finished, images={result.Images.Count}, cancelled={result.WasCancelled}, timedOut={result.TimedOut}, failed={result.FailedTransfers}, duplex={result.WasDuplex}");
        return result;
    }

    /// <summary>
    /// Runs a TWAIN call on the thread that owns the session's message loop (UI thread).
    /// </summary>
    private void RunOnTwainThread(Action action)
    {
        Exception? error = null;
        _msgLoop.Invoke(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        });
        if (error != null)
            throw new InvalidOperationException(error.Message, error);
    }

    private TimeSpan SinceLastActivity =>
        Stopwatch.GetElapsedTime(Interlocked.Read(ref _lastActivityTimestamp));

    private void WaitForCompletion(bool onUiThread)
    {
        Interlocked.Exchange(ref _lastActivityTimestamp, Stopwatch.GetTimestamp());
        Stopwatch? cancelWatch = null;

        while (true)
        {
            if (onUiThread)
            {
                Application.DoEvents();
                if (_scanComplete.Wait(50)) break;
            }
            else if (_scanComplete.Wait(250))
            {
                break;
            }

            // User cancel or multi-feed "Wiederholen": give the driver a moment to stop
            // at the next TransferReady (CancelAll), then force the step-down. Checked
            // before the modal state: a cancel request also applies while the multi-feed
            // dialog is open (RequestCancel closes it).
            if (_cancelRequested || _cancelAllPending)
            {
                cancelWatch ??= Stopwatch.StartNew();
                if (cancelWatch.Elapsed > CancelGracePeriod)
                {
                    LogDiag("Scan: driver did not stop after cancel request, forcing step-down");
                    break;
                }
                continue;
            }

            // A modal dialog (multi-feed) is waiting for the user: never time out meanwhile.
            if (_modalOpen)
            {
                Interlocked.Exchange(ref _lastActivityTimestamp, Stopwatch.GetTimestamp());
                continue;
            }

            var limit = _anyEventSeen ? InactivityTimeout : InitialTimeout;
            if (SinceLastActivity > limit)
            {
                LogDiag($"Scan timed out (no TWAIN event for {limit.TotalSeconds:0} s)");
                _result.TimedOut = true;
                _result.Warnings.Add("Der Scanner hat nicht mehr reagiert. Der Scanvorgang wurde abgebrochen.");
                break;
            }
        }
    }

    private void WaitForModal(bool onUiThread)
    {
        while (_modalOpen)
        {
            if (onUiThread) Application.DoEvents();
            Thread.Sleep(50);
        }
    }

    private void Touch()
    {
        _anyEventSeen = true;
        Interlocked.Exchange(ref _lastActivityTimestamp, Stopwatch.GetTimestamp());
    }

    /// <summary>Runs on the TWAIN (UI) thread. Returns true if the source was enabled.</summary>
    private bool OpenConfigureAndEnable(ScanSide scanSideOverride)
    {
        // Ensure DSM is open
        if (!_twain.IsDsmOpen)
        {
            var rc = _twain.Open(_msgLoop);
            if (rc != ReturnCode.Success)
            {
                LogDiag($"DSM open failed: {rc}");
                _result.ErrorMessage = "Der TWAIN-Quellmanager konnte nicht geöffnet werden.";
                return false;
            }
        }

        // Select source
        _currentSource = ScannerSelectionDialog.SelectBestSource(_twain, _preferredSourceName);
        if (_currentSource == null)
        {
            LogDiag("SelectSource: no source found");
            _result.WasCancelled = true;
            _result.ErrorMessage = "Es wurde kein Scanner gefunden.";
            return false;
        }
        LogDiag($"SelectSource: selected '{_currentSource.Name}' (preferred='{_preferredSourceName ?? "(auto)"}', mfr='{_currentSource.Manufacturer}', family='{_currentSource.ProductFamily}')");
        _result.SourceName = _currentSource.Name ?? "";

        // Open the data source (state 3 -> 4)
        var openRc = _currentSource.Open();
        if (openRc != ReturnCode.Success)
        {
            LogDiag($"Source open failed: {openRc}");
            _result.ErrorMessage = "Der Scanner konnte nicht geöffnet werden. Ist er eingeschaltet und verbunden?";
            return false;
        }

        // All capability negotiation happens here, in state 4, before Enable.
        ConfigureCapabilities(scanSideOverride);

        // Subscribe to transfer events
        _twain.DataTransferred += OnDataTransferred;
        _twain.SourceDisabled += OnSourceDisabled;
        _twain.DeviceEvent += OnDeviceEvent;
        _twain.TransferReady += OnTransferReady;
        _twain.TransferError += OnTransferError;
        _twain.TransferCanceled += OnTransferCanceled;
        _subscribed = true;

        if (_windowHandle == IntPtr.Zero)
            _windowHandle = _hiddenWindow.InvokeRequired
                ? (IntPtr)_hiddenWindow.Invoke(() => _hiddenWindow.Handle)
                : _hiddenWindow.Handle;

        // Enable the source (starts scanning, state 4 -> 5)
        var enableRc = _currentSource.Enable(SourceEnableMode.NoUI, true, _windowHandle);
        LogDiag($"Enable(NoUI) rc={enableRc}");
        if (enableRc != ReturnCode.Success)
        {
            LogStatus("Enable");
            _result.ErrorMessage = "Der Scanvorgang konnte nicht gestartet werden.";
            return false;
        }
        return true;
    }

    /// <summary>Runs on the TWAIN (UI) thread: unsubscribe, step down to state 4 and close the source.</summary>
    private void Cleanup()
    {
        if (_subscribed)
        {
            _twain.DataTransferred -= OnDataTransferred;
            _twain.SourceDisabled -= OnSourceDisabled;
            _twain.DeviceEvent -= OnDeviceEvent;
            _twain.TransferReady -= OnTransferReady;
            _twain.TransferError -= OnTransferError;
            _twain.TransferCanceled -= OnTransferCanceled;
            _subscribed = false;
        }

        try
        {
            if (_twain.State > 4)
            {
                LogDiag($"Cleanup: session in state {_twain.State}, forcing step-down to 4");
                _twain.ForceStepDown(4);
            }
        }
        catch (Exception ex)
        {
            LogDiag($"Cleanup: ForceStepDown failed: {ex.Message}");
        }

        if (_currentSource != null && _currentSource.IsOpen)
        {
            try
            {
                var rc = _currentSource.Close();
                LogDiag($"Cleanup: source close rc={rc}, session state={_twain.State}");
            }
            catch (Exception ex)
            {
                LogDiag($"Cleanup: source close failed: {ex.Message}");
            }
        }
    }

    // ───────────────────────── Capability negotiation (state 4) ─────────────────────────

    private void LogStatus(string what)
    {
        try
        {
            var status = _currentSource?.GetStatus();
            if (status != null)
                LogDiag($"  {what}: condition code={status.ConditionCode}");
        }
        catch { }
    }

    /// <summary>Sets a capability, logs "cap X = v -> rc" and returns the return code (never throws).</summary>
    private ReturnCode SetCap<T>(string name, ICapWrapper<T> cap, T value)
    {
        try
        {
            var rc = cap.SetValue(value);
            LogDiag($"cap {name} = {value} -> {rc}");
            if (rc != ReturnCode.Success) LogStatus(name);
            return rc;
        }
        catch (Exception ex)
        {
            LogDiag($"cap {name} = {value} -> exception {ex.Message}");
            return ReturnCode.Failure;
        }
    }

    private ReturnCode SetCapArray<T>(string name, ICapWrapper<T> cap, params ushort[] values)
    {
        try
        {
            var array = new TWArray
            {
                ItemType = ItemType.UInt16,
                ItemList = values.Select(v => (object)v).ToArray()
            };
            var rc = cap.SetValue(array);
            LogDiag($"cap {name} = [{string.Join(",", values)}] -> {rc}");
            if (rc != ReturnCode.Success) LogStatus(name);
            return rc;
        }
        catch (Exception ex)
        {
            LogDiag($"cap {name} = [{string.Join(",", values)}] -> exception {ex.Message}");
            return ReturnCode.Failure;
        }
    }

    private T? GetCurrent<T>(string name, IReadOnlyCapWrapper<T> cap) where T : struct
    {
        try
        {
            var value = cap.GetCurrent();
            LogDiag($"cap {name} current = {value}");
            return value;
        }
        catch (Exception ex)
        {
            LogDiag($"cap {name} GetCurrent failed: {ex.Message}");
            return null;
        }
    }

    private bool IsFujitsuFamily(DataSource source)
    {
        static bool Match(string? s) => !string.IsNullOrEmpty(s)
            && (s.Contains("fujitsu", StringComparison.OrdinalIgnoreCase)
                || s.Contains("pfu", StringComparison.OrdinalIgnoreCase)
                || s.Contains("ricoh", StringComparison.OrdinalIgnoreCase));
        return Match(source.Manufacturer) || Match(source.ProductFamily);
    }

    private void SetCustomCapFix32(ushort capId, float value)
    {
        try
        {
            // TW_FIX32 is a struct: { short Whole (16-bit), ushort Frac (16-bit) }
            // In memory layout (little-endian): Whole in low 16 bits, Frac in high 16 bits
            short whole = (short)Math.Truncate(value);
            ushort frac = (ushort)((value - whole) * 65536);
            // Pack as: Whole in low word, Frac in high word
            uint fix32Value = (uint)((frac << 16) | (ushort)whole);

            var cap = (CapabilityId)capId;
            var rc = _currentSource!.DGControl.Capability.Set(
                new TWCapability(cap, new TWOneValue
                {
                    Item = fix32Value,
                    ItemType = ItemType.Fix32
                }));
            LogDiag($"Custom cap {capId} SET to {value} (whole={whole} frac={frac} raw=0x{fix32Value:X8}) rc={rc}");
        }
        catch (Exception ex)
        {
            LogDiag($"Custom cap {capId} SET failed: {ex.Message}");
        }
    }

    private void SetCustomCapUInt16(ushort capId, ushort value)
    {
        try
        {
            var cap = (CapabilityId)capId;
            var rc = _currentSource!.DGControl.Capability.Set(
                new TWCapability(cap, new TWOneValue
                {
                    Item = value,
                    ItemType = ItemType.UInt16
                }));
            LogDiag($"Custom cap {capId} SET to {value} rc={rc}");
        }
        catch (Exception ex)
        {
            LogDiag($"Custom cap {capId} SET failed: {ex.Message}");
        }
    }

    /// <summary>
    /// dpi per image quality. B/W uses the doubled values promised in the UI
    /// ("Normal (Farbe/Grau: 150 dpi, S&amp;W: 300 dpi)" etc.). Automatic = Fein.
    /// </summary>
    internal static int GetTargetDpi(ImageQuality quality, bool blackWhite) => (quality, blackWhite) switch
    {
        (ImageQuality.Normal, false) => 150,
        (ImageQuality.Fine, false) => 200,
        (ImageQuality.Best, false) => 300,
        (ImageQuality.Excellent, false) => 600,
        (ImageQuality.Normal, true) => 300,
        (ImageQuality.Fine, true) => 400,
        (ImageQuality.Best, true) => 600,
        (ImageQuality.Excellent, true) => 1200,
        (_, true) => 400,
        _ => 200
    };

    private void ConfigureCapabilities(ScanSide scanSideOverride)
    {
        var src = _currentSource;
        if (src == null) return;
        var caps = src.Capabilities;

        LogDiag($"ConfigureCapabilities: colorMode={_settings.ColorMode}, quality={_settings.ImageQuality}, side={_settings.ScanSide}/override={scanSideOverride}, paper={_settings.PaperSize}, multiFeed={_settings.MultiFeedDetection}, longPage={IsLongPageMode}");

        // Units first: resolution and frames are interpreted in ICAP_UNITS.
        SetCap("ICapUnits", caps.ICapUnits, Unit.Inches);

        // ── Feeder / flatbed and duplex ──
        ScanSide effectiveSide = scanSideOverride != ScanSide.Automatic ? scanSideOverride : _settings.ScanSide;
        ConfigureFeederAndSide(effectiveSide);

        // ── Pixel type ──
        var pixelType = _settings.ColorMode switch
        {
            ColorMode.Grayscale => PixelType.Gray,
            ColorMode.BlackWhite => PixelType.BlackWhite,
            _ => PixelType.RGB // Color and Automatic (classified per page afterwards)
        };
        if (SetCap("ICapPixelType", caps.ICapPixelType, pixelType) != ReturnCode.Success)
            _result.Warnings.Add($"Der Scanner unterstützt den Farbmodus '{pixelType}' nicht.");
        GetCurrent("ICapPixelType", caps.ICapPixelType);

        // ── Resolution ──
        bool blackWhite = _settings.ColorMode == ColorMode.BlackWhite;
        int dpi = GetTargetDpi(_settings.ImageQuality, blackWhite);
        ConfigureResolution(dpi);

        // ── Brightness / contrast (B/W only, as labelled in the UI) ──
        if (blackWhite && _settings.Brightness != 0)
        {
            // TWAIN range -1000..+1000; UI -3..+3
            SetCap("ICapBrightness", caps.ICapBrightness, (TWFix32)(_settings.Brightness * 333f));
        }
        if (blackWhite && _settings.TextOnlySettings)
        {
            // "Einstellung nur für Textdokumente": emphasize contrast so faint text/handwriting is kept.
            SetCap("ICapContrast", caps.ICapContrast, (TWFix32)300f);
        }

        // ── Multi-feed detection ──
        ConfigureMultiFeed();

        // ── Auto scan (optional throughput improvement) ──
        try
        {
            if (caps.CapAutoScan.IsSupported && caps.CapAutoScan.CanSet)
                SetCap("CapAutoScan", caps.CapAutoScan, BoolType.True);
        }
        catch (Exception ex)
        {
            LogDiag($"CapAutoScan check failed: {ex.Message}");
        }

        // ── Paper size / frame: always last, other caps may reset it ──
        ConfigurePaperSize();
    }

    private void ConfigureFeederAndSide(ScanSide effectiveSide)
    {
        var caps = _currentSource!.Capabilities;

        bool flatbedAvailable = false;
        try
        {
            var feederValues = caps.CapFeederEnabled.GetValues()?.ToList();
            flatbedAvailable = feederValues != null && feederValues.Contains(BoolType.False);
            LogDiag($"CapFeederEnabled values=[{string.Join(",", feederValues ?? new List<BoolType>())}] flatbedAvailable={flatbedAvailable}");
        }
        catch (Exception ex)
        {
            LogDiag($"CapFeederEnabled GetValues failed: {ex.Message}");
        }

        bool autoSide = effectiveSide == ScanSide.Automatic;
        if (autoSide)
        {
            // "Automatisch – ADF oder Flachbett je nach Dokumenteinlage"
            SetCap("CapFeederEnabled", caps.CapFeederEnabled, BoolType.True);
            BoolType? loaded = null;
            try
            {
                if (caps.CapFeederLoaded.IsSupported)
                    loaded = caps.CapFeederLoaded.GetCurrent();
            }
            catch (Exception ex)
            {
                LogDiag($"CapFeederLoaded failed: {ex.Message}");
            }
            LogDiag($"Automatic side: feederLoaded={loaded?.ToString() ?? "unknown"}, flatbedAvailable={flatbedAvailable}");
            effectiveSide = loaded == BoolType.False && flatbedAvailable ? ScanSide.Flatbed : ScanSide.Duplex;
        }

        if (effectiveSide == ScanSide.Flatbed)
        {
            if (SetCap("CapFeederEnabled", caps.CapFeederEnabled, BoolType.False) != ReturnCode.Success)
                _result.Warnings.Add("Der Scanner unterstützt kein Flachbett. Es wird der Einzug verwendet.");
            else
                SetCap("CapDuplexEnabled", caps.CapDuplexEnabled, BoolType.False);
        }
        else
        {
            if (!autoSide)
                SetCap("CapFeederEnabled", caps.CapFeederEnabled, BoolType.True);

            if (effectiveSide == ScanSide.Duplex)
            {
                if (SetCap("CapDuplexEnabled", caps.CapDuplexEnabled, BoolType.True) != ReturnCode.Success)
                {
                    LogDiag("Duplex refused, falling back to Simplex");
                    SetCap("CapDuplexEnabled", caps.CapDuplexEnabled, BoolType.False);
                    if (!autoSide)
                        _result.Warnings.Add("Der Scanner unterstützt keinen Duplex-Scan. Es wurde einseitig gescannt.");
                }
            }
            else
            {
                SetCap("CapDuplexEnabled", caps.CapDuplexEnabled, BoolType.False);
            }
        }

        var duplexNow = GetCurrent("CapDuplexEnabled", caps.CapDuplexEnabled);
        var feederNow = GetCurrent("CapFeederEnabled", caps.CapFeederEnabled);
        _result.WasDuplex = duplexNow == BoolType.True && feederNow != BoolType.False;
        _result.RemoveBlankBacks = autoSide && _result.WasDuplex;
    }

    private void ConfigureResolution(int dpi)
    {
        var caps = _currentSource!.Capabilities;
        var rcX = SetCap("ICapXResolution", caps.ICapXResolution, (TWFix32)(float)dpi);
        var rcY = SetCap("ICapYResolution", caps.ICapYResolution, (TWFix32)(float)dpi);

        if (rcX != ReturnCode.Success || rcY != ReturnCode.Success)
        {
            // Pick the closest resolution the scanner offers.
            try
            {
                var values = caps.ICapXResolution.GetValues()?.Select(v => (float)v).Where(v => v > 0).ToList();
                if (values != null && values.Count > 0)
                {
                    var best = values.OrderBy(v => Math.Abs(v - dpi)).First();
                    LogDiag($"Resolution {dpi} refused, supported=[{string.Join(",", values)}], using {best}");
                    SetCap("ICapXResolution", caps.ICapXResolution, (TWFix32)best);
                    SetCap("ICapYResolution", caps.ICapYResolution, (TWFix32)best);
                    _result.Warnings.Add($"Die Auflösung {dpi} dpi wird nicht unterstützt, es wurden {best:0} dpi verwendet.");
                }
            }
            catch (Exception ex)
            {
                LogDiag($"ICapXResolution GetValues failed: {ex.Message}");
            }
        }

        var curX = GetCurrent("ICapXResolution", caps.ICapXResolution);
        var curY = GetCurrent("ICapYResolution", caps.ICapYResolution);
        _result.NegotiatedDpiX = curX.HasValue ? (float)curX.Value : dpi;
        _result.NegotiatedDpiY = curY.HasValue ? (float)curY.Value : _result.NegotiatedDpiX;
    }

    private void ConfigureMultiFeed()
    {
        if (_settings.MultiFeedDetection == MultiFeedDetection.Off)
            return;

        var caps = _currentSource!.Capabilities;
        ReturnCode rc;
        switch (_settings.MultiFeedDetection)
        {
            case MultiFeedDetection.Length:
                rc = SetCap("CapDoubleFeedDetection", caps.CapDoubleFeedDetection, DoubleFeedDetection.ByLength);
                break;
            case MultiFeedDetection.Both:
                // CAP_DOUBLEFEEDDETECTION is an array capability: request both methods.
                rc = SetCapArray("CapDoubleFeedDetection", caps.CapDoubleFeedDetection,
                    (ushort)DoubleFeedDetection.Ultrasonic, (ushort)DoubleFeedDetection.ByLength);
                if (rc != ReturnCode.Success)
                {
                    rc = SetCap("CapDoubleFeedDetection", caps.CapDoubleFeedDetection, DoubleFeedDetection.Ultrasonic);
                    if (rc == ReturnCode.Success)
                        _result.Warnings.Add("Der Scanner unterstützt nicht beide Mehrfacheinzugsprüfungen gleichzeitig; es wird nur Ultraschall verwendet.");
                }
                break;
            default:
                rc = SetCap("CapDoubleFeedDetection", caps.CapDoubleFeedDetection, DoubleFeedDetection.Ultrasonic);
                break;
        }

        if (rc != ReturnCode.Success)
        {
            _result.Warnings.Add("Die Mehrfacheinzugserkennung konnte nicht aktiviert werden.");
            return;
        }

        // Stop the feeder on a multi-feed so the user can react.
        SetCap("CapDoubleFeedDetectionResponse", caps.CapDoubleFeedDetectionResponse, DoubleFeedDetectionResponse.StopAndWait);

        // The source only sends MSG_DEVICEEVENT for events the application asked for.
        if (SetCapArray("CapDeviceEvent", caps.CapDeviceEvent, (ushort)DeviceEvent.PaperDoubleFeed) != ReturnCode.Success)
            SetCap("CapDeviceEvent", caps.CapDeviceEvent, DeviceEvent.PaperDoubleFeed);
    }

    /// <summary>Paper sizes in mm (portrait). Sizes without a usable TWAIN constant use frames.</summary>
    private static (SupportedSize? size, double widthMm, double heightMm)? GetPaperSpec(PaperSizeMode mode) => mode switch
    {
        PaperSizeMode.A3 => (SupportedSize.A3, 297, 420),
        PaperSizeMode.A4 => (SupportedSize.A4, 210, 297),
        PaperSizeMode.A5 => (SupportedSize.A5, 148, 210),
        PaperSizeMode.A6 => (SupportedSize.A6, 105, 148),
        PaperSizeMode.B4Jis => (SupportedSize.JisB4, 257, 364),
        PaperSizeMode.B5Jis => (SupportedSize.JisB5, 182, 257),
        PaperSizeMode.B6Jis => (SupportedSize.JisB6, 128, 182),
        PaperSizeMode.Postcard => (null, 100, 148),
        PaperSizeMode.BusinessCard => (SupportedSize.BusinessCard, 55, 91),
        PaperSizeMode.Letter => (SupportedSize.USLetter, 215.9, 279.4),
        PaperSizeMode.DoubleLetter => (SupportedSize.USLedger, 279.4, 431.8),
        PaperSizeMode.Legal => (SupportedSize.USLegal, 215.9, 355.6),
        _ => null
    };

    private void ConfigurePaperSize()
    {
        var src = _currentSource!;
        var caps = src.Capabilities;

        if (IsLongPageMode)
        {
            // Long page: custom frame of 8.5 x 125 inches (Automatic paper size, no length detection).
            // Fujitsu scanners allow pages longer than 34 inches (864 mm) only up to 200 dpi.
            float maxLength = _result.NegotiatedDpiY is > 0 and <= 200 ? 125f : 34f;
            var longFrame = new TWFrame { Left = 0, Top = 0, Right = 8.5f, Bottom = maxLength };
            SetSupportedSizeNone();
            SetCap("ICapFrames", caps.ICapFrames, longFrame);
            try
            {
                if (src.DGImage.ImageLayout.Get(out var layout) == ReturnCode.Success)
                {
                    layout.Frame = longFrame;
                    var rc = src.DGImage.ImageLayout.Set(layout);
                    LogDiag($"Long page mode: ImageLayout set rc={rc}");
                }
            }
            catch (Exception ex)
            {
                LogDiag($"Long page mode: ImageLayout failed: {ex.Message}");
            }

            // PaperStream IP (Fujitsu/PFU/Ricoh) vendor caps, only for those drivers:
            // 40983 = paper width (TW_FIX32), 40984 = paper length (TW_FIX32),
            // 41095 = cropping mode (0=Fixed, 1=DetectLength, 2=Automatic, 3=LongPage)
            if (IsFujitsuFamily(src))
            {
                SetCustomCapFix32(40983, 8.5f);
                SetCustomCapFix32(40984, maxLength);
                SetCustomCapUInt16(41095, 3);
            }
            else
            {
                LogDiag("Long page mode: not a Fujitsu/PFU/Ricoh source, vendor caps skipped");
            }

            TryLogFrame();
            return;
        }

        if (_settings.PaperSize == PaperSizeMode.Automatic)
        {
            LogDiag("Paper size automatic: leaving driver default (edge detection)");
            return;
        }

        double widthMm, heightMm;
        if (_settings.PaperSize == PaperSizeMode.Custom)
        {
            if (_customPaperSize == null || _customPaperSize.WidthMm <= 0 || _customPaperSize.LengthMm <= 0)
            {
                LogDiag($"Custom paper size: no valid selection (index={_settings.SelectedCustomSizeIndex}), leaving driver default");
                _result.Warnings.Add("Keine gültige benutzerdefinierte Papiergröße ausgewählt.");
                return;
            }
            widthMm = _customPaperSize.WidthMm;
            heightMm = _customPaperSize.LengthMm;
            LogDiag($"Custom paper size '{_customPaperSize.Name}': {widthMm} x {heightMm} mm");
        }
        else
        {
            var spec = GetPaperSpec(_settings.PaperSize);
            if (spec == null) return;
            widthMm = spec.Value.widthMm;
            heightMm = spec.Value.heightMm;

            if (spec.Value.size is SupportedSize size)
            {
                bool listed = false;
                try
                {
                    var supported = caps.ICapSupportedSizes.GetValues()?.ToList();
                    listed = supported != null && supported.Contains(size);
                    LogDiag($"ICapSupportedSizes values=[{string.Join(",", supported ?? new List<SupportedSize>())}]");
                }
                catch (Exception ex)
                {
                    LogDiag($"ICapSupportedSizes GetValues failed: {ex.Message}");
                }

                if (listed && SetCap("ICapSupportedSizes", caps.ICapSupportedSizes, size) == ReturnCode.Success)
                {
                    TryLogFrame();
                    return;
                }
                LogDiag($"Paper size {size} not available as constant, using a frame");
            }
        }

        // Frame in inches (ICapUnits was set to inches at the start).
        var frame = new TWFrame
        {
            Left = 0,
            Top = 0,
            Right = (float)(widthMm / 25.4),
            Bottom = (float)(heightMm / 25.4)
        };
        SetSupportedSizeNone();
        if (SetCap("ICapFrames", caps.ICapFrames, frame) != ReturnCode.Success)
            _result.Warnings.Add("Die gewählte Papiergröße wird vom Scanner nicht unterstützt.");
        TryLogFrame();
    }

    private void SetSupportedSizeNone()
    {
        try
        {
            var caps = _currentSource!.Capabilities;
            var supported = caps.ICapSupportedSizes.GetValues()?.ToList();
            if (supported != null && supported.Contains(SupportedSize.None))
                SetCap("ICapSupportedSizes", caps.ICapSupportedSizes, SupportedSize.None);
        }
        catch (Exception ex)
        {
            LogDiag($"ICapSupportedSizes None failed: {ex.Message}");
        }
    }

    private void TryLogFrame()
    {
        try
        {
            var frame = _currentSource!.Capabilities.ICapFrames.GetCurrent();
            LogDiag($"cap ICapFrames current = {frame}");
        }
        catch (Exception ex)
        {
            LogDiag($"cap ICapFrames GetCurrent failed: {ex.Message}");
        }
    }

    // ───────────────────────── TWAIN events (UI thread) ─────────────────────────

    private void OnTransferReady(object? sender, TransferReadyEventArgs e)
    {
        Touch();
        LogDiag($"TransferReady: pending={e.PendingTransferCount}, endOfJob={e.EndOfJob}");

        if (_cancelRequested || _cancelAllPending)
        {
            LogDiag("TransferReady: cancelling all remaining transfers");
            e.CancelAll = true;
        }
    }

    private void OnDataTransferred(object? sender, DataTransferredEventArgs e)
    {
        Touch();
        Bitmap? bmp = null;
        try
        {
            // Native transfer: decode the DIB stream, then copy into a standalone bitmap so
            // the stream can be released right away (GDI+ keeps stream-backed bitmaps alive).
            using (var stream = e.GetNativeImageStream())
            {
                if (stream != null)
                {
                    stream.Position = 0;
                    using var tmp = new Bitmap(stream);
                    bmp = ImageProcessing.DeepCopy(tmp);
                }
            }

            // Fallback: file transfer
            if (bmp == null && !string.IsNullOrEmpty(e.FileDataPath) && File.Exists(e.FileDataPath))
            {
                using var tmp = new Bitmap(e.FileDataPath);
                bmp = ImageProcessing.DeepCopy(tmp);
            }

            // Fallback: memory data
            if (bmp == null && e.MemoryData != null && e.MemoryData.Length > 0)
            {
                using var ms = new MemoryStream(e.MemoryData);
                using var tmp = new Bitmap(ms);
                bmp = ImageProcessing.DeepCopy(tmp);
            }

            if (bmp == null)
            {
                RecordFailedTransfer("Keine Bilddaten vom Scanner erhalten.");
                return;
            }

            ApplyResolution(bmp, e);
            _acquiredImages.Add(bmp);
            LogDiag($"DataTransferred: page {_acquiredImages.Count}: {bmp.Width}x{bmp.Height} {bmp.PixelFormat} @ {bmp.HorizontalResolution:0}x{bmp.VerticalResolution:0} dpi");
        }
        catch (OutOfMemoryException ex)
        {
            bmp?.Dispose();
            RecordFailedTransfer("Zu wenig Arbeitsspeicher für eine Seite.");
            LogDiag($"Image transfer failed (out of memory): {ex.Message}");
        }
        catch (Exception ex)
        {
            bmp?.Dispose();
            RecordFailedTransfer($"Seite konnte nicht übertragen werden: {ex.Message}");
            LogDiag($"Image transfer failed: {ex}");
        }
    }

    /// <summary>
    /// Drivers that leave biXPelsPerMeter at 0 produce 96-dpi bitmaps in GDI+; fix the dpi
    /// from the transfer's image info or the negotiated resolution so page sizes are right.
    /// </summary>
    private void ApplyResolution(Bitmap bmp, DataTransferredEventArgs e)
    {
        float dpiX = 0, dpiY = 0;
        try
        {
            var info = e.ImageInfo;
            if (info != null)
            {
                dpiX = info.XResolution;
                dpiY = info.YResolution;
            }
        }
        catch { }
        if (dpiX <= 1) dpiX = _result.NegotiatedDpiX;
        if (dpiY <= 1) dpiY = _result.NegotiatedDpiY > 1 ? _result.NegotiatedDpiY : dpiX;

        bool bitmapDpiUnreliable = bmp.HorizontalResolution <= 1 || Math.Abs(bmp.HorizontalResolution - 96f) < 0.5f;
        if (bitmapDpiUnreliable && dpiX > 1 && Math.Abs(dpiX - 96f) >= 0.5f)
        {
            bmp.SetResolution(dpiX, dpiY);
            LogDiag($"DataTransferred: bitmap dpi corrected to {dpiX:0}x{dpiY:0}");
        }
    }

    private void RecordFailedTransfer(string message)
    {
        _result.FailedTransfers++;
        _result.FailedTransferMessages.Add(message);
    }

    private void OnTransferError(object? sender, TransferErrorEventArgs e)
    {
        Touch();
        var msg = e.Exception?.Message ?? $"rc={e.ReturnCode}, cc={e.SourceStatus?.ConditionCode}";
        LogDiag($"TransferError: {msg}");
        RecordFailedTransfer($"Übertragungsfehler: {msg}");
    }

    private void OnTransferCanceled(object? sender, TransferCanceledEventArgs e)
    {
        Touch();
        LogDiag("TransferCanceled");
    }

    private void OnSourceDisabled(object? sender, EventArgs e)
    {
        LogDiag("SourceDisabled");
        _scanComplete.Set();
    }

    private void OnDeviceEvent(object? sender, DeviceEventArgs e)
    {
        Touch();
        var evt = e.DeviceEvent.Event;
        LogDiag($"DeviceEvent: {evt} (device='{e.DeviceEvent.DeviceName}')");

        if (evt != DeviceEvent.PaperDoubleFeed)
            return;

        _result.MultiFeedDetected = true;
        if (_ignoreMultiFeed || _multiFeedDialogQueued || _cancelAllPending)
        {
            LogDiag("Multi-feed: ignored (detection disabled by user or decision pending)");
            return;
        }

        // Don't show a modal dialog inside the TWAIN event dispatch: post it to the UI
        // message queue. The decision is applied at the next TransferReady (CancelAll) or
        // after the batch ended. The inactivity timeout pauses while the dialog is open.
        _multiFeedDialogQueued = true;
        _modalOpen = true;

        Bitmap? currentPage = _acquiredImages.Count > 0 ? _acquiredImages[^1] : null;
        Bitmap? previousPage = _acquiredImages.Count > 1 ? _acquiredImages[^2] : null;

        try
        {
            _hiddenWindow.BeginInvoke(() => HandleMultiFeedDialog(currentPage, previousPage));
        }
        catch (Exception ex)
        {
            LogDiag($"Multi-feed: could not post dialog: {ex.Message}");
            _modalOpen = false;
            _multiFeedDialogQueued = false;
        }
    }

    private void HandleMultiFeedDialog(Bitmap? currentPage, Bitmap? previousPage)
    {
        var action = MultiFeedWarningDialog.MultiFeedAction.Rescan;
        if (_cancelRequested)
        {
            // Cancelled before the posted dialog came up: don't show it at all.
            _multiFeedDialogQueued = false;
            _modalOpen = false;
            Touch();
            return;
        }
        try
        {
            using var dlg = new MultiFeedWarningDialog(currentPage, previousPage);
            _multiFeedDialog = dlg;
            action = dlg.ShowDialog(_hiddenWindow) == DialogResult.OK ? dlg.Action : MultiFeedWarningDialog.MultiFeedAction.Rescan;
        }
        catch (Exception ex)
        {
            LogDiag($"Multi-feed dialog failed: {ex.Message}");
        }
        finally
        {
            _multiFeedDialog = null;
        }

        LogDiag($"Multi-feed: user chose {action}");
        switch (action)
        {
            case MultiFeedWarningDialog.MultiFeedAction.Rescan:
                // Cancel the remaining transfers and discard this batch.
                _cancelAllPending = true;
                _discardImages = true;
                break;

            case MultiFeedWarningDialog.MultiFeedAction.KeepAsIs:
                break;

            case MultiFeedWarningDialog.MultiFeedAction.DisableDetection:
                // Capabilities can't be changed while the source is enabled (state 5-7);
                // ignore further multi-feed events of this batch instead.
                _ignoreMultiFeed = true;
                break;
        }

        _multiFeedDialogQueued = false;
        _modalOpen = false;
        Touch();
    }

    public void Dispose()
    {
        _scanComplete.Dispose();
    }
}
