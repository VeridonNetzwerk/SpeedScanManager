using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using NTwain;
using NTwain.Data;

namespace SpeedScanManager;

/// <summary>
/// Core scan pipeline: opens a TWAIN data source, configures capabilities,
/// acquires images, and returns them as Bitmap list.
/// </summary>
internal class ScanPipeline : IDisposable
{
    private readonly TwainSession _twain;
    private readonly WindowsFormsMessageLoopHook _msgLoop;
    private readonly Form _hiddenWindow;
    private readonly ScanSettings _settings;
    private DataSource? _currentSource;
    private readonly List<Bitmap> _acquiredImages = new();

    private static void LogDiag(string msg)
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SpeedScanManager", "twain_diag.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, $"{DateTime.Now:HH:mm:ss.fff} {msg}\n");
        }
        catch { }
    }
    private readonly ManualResetEventSlim _scanComplete = new(false);
    private bool _scanCancelled;
    private bool _multiFeedDetected;
    private bool _userCancelledMultiFeed;
    private bool _disableMultiFeedDetection;

    public IReadOnlyList<Bitmap> AcquiredImages => _acquiredImages;
    public bool WasCancelled => _scanCancelled;
    public bool MultiFeedDetected => _multiFeedDetected;
    public bool UserCancelledMultiFeed => _userCancelledMultiFeed;
    public bool DisableMultiFeedDetection => _disableMultiFeedDetection;

    public ScanPipeline(TwainSession twain, WindowsFormsMessageLoopHook msgLoop, Form hiddenWindow, ScanSettings settings)
    {
        _twain = twain;
        _msgLoop = msgLoop;
        _hiddenWindow = hiddenWindow;
        _settings = settings;
    }

    /// <summary>
    /// Selects a TWAIN data source. If multiple sources are available, shows a selection dialog.
    /// </summary>
    private DataSource? SelectSource()
    {
        // Prefer the system default source
        var defaultSrc = _twain.DefaultSource;
        if (defaultSrc != null)
            return defaultSrc;

        var sources = _twain.GetSources().ToList();
        if (sources.Count == 0)
            return null;
        if (sources.Count == 1)
            return sources[0];

        // Multiple sources: show selection dialog
        string[] names = sources.Select(s => s.Name).ToArray();
        int selectedIdx = 0;

        // Use a simple dialog on the UI thread
        if (_hiddenWindow.InvokeRequired)
        {
            _hiddenWindow.Invoke(() =>
            {
                selectedIdx = ShowSourceSelection(names);
            });
        }
        else
        {
            selectedIdx = ShowSourceSelection(names);
        }

        return selectedIdx >= 0 ? sources[selectedIdx] : null;
    }

    private int ShowSourceSelection(string[] names)
    {
        using var dlg = new Form
        {
            Text = "SpeedScan Manager – Quelle wählen",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterScreen,
            Width = 360,
            Height = 200,
            ShowInTaskbar = false
        };

        var lbl = new Label
        {
            Text = "Bitte wählen Sie eine TWAIN-Quelle:",
            Location = new Point(12, 12),
            AutoSize = true
        };

        var cb = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(12, 36),
            Size = new Size(320, 24)
        };
        cb.Items.AddRange(names);
        cb.SelectedIndex = 0;

        var btnOk = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(170, 120),
            Size = new Size(75, 28)
        };

        var btnCancel = new Button
        {
            Text = "Abbrechen",
            DialogResult = DialogResult.Cancel,
            Location = new Point(257, 120),
            Size = new Size(75, 28)
        };

        dlg.Controls.AddRange(new Control[] { lbl, cb, btnOk, btnCancel });
        dlg.AcceptButton = btnOk;
        dlg.CancelButton = btnCancel;

        return dlg.ShowDialog() == DialogResult.OK ? cb.SelectedIndex : -1;
    }

    /// <summary>
    /// Executes a full scan cycle with the given scan side override.
    /// Returns acquired images as Bitmap list.
    /// </summary>
    public List<Bitmap> ExecuteScan(ScanSide scanSideOverride)
    {
        _acquiredImages.Clear();
        _scanComplete.Reset();
        _scanCancelled = false;
        _multiFeedDetected = false;
        _userCancelledMultiFeed = false;

        // Ensure DSM is open
        if (!_twain.IsDsmOpen)
        {
            var rc = _twain.Open(_msgLoop);
            if (rc != ReturnCode.Success)
            {
                LogDiag($"DSM open failed: {rc}");
                return new List<Bitmap>();
            }
        }

        // Select source
        _currentSource = SelectSource();
        if (_currentSource == null)
        {
            _scanCancelled = true;
            return new List<Bitmap>();
        }

        // Open the data source
        var openRc = _currentSource.Open();
        if (openRc != ReturnCode.Success)
        {
            LogDiag($"Source open failed: {openRc}");
            return new List<Bitmap>();
        }

        // Determine if we're in long page mode
        bool isLongPageMode = _settings.PaperSize == PaperSizeMode.Automatic
            && _settings.MultiFeedDetection != MultiFeedDetection.Length
            && _settings.MultiFeedDetection != MultiFeedDetection.Both;

        // Configure capabilities (sets custom PaperStream IP caps for long page mode)
        ConfigureCapabilities(scanSideOverride);

        // Subscribe to transfer events
        _twain.DataTransferred += OnDataTransferred;
        _twain.SourceDisabled += OnSourceDisabled;
        _twain.DeviceEvent += OnDeviceEvent;
        _twain.TransferReady += OnTransferReady;

        // Enable the source (starts scanning)
        var enableRc = _currentSource.Enable(SourceEnableMode.NoUI, true, _hiddenWindow.Handle);
        if (enableRc != ReturnCode.Success)
        {
            LogDiag($"Enable failed: {enableRc}");
            _twain.DataTransferred -= OnDataTransferred;
            _twain.SourceDisabled -= OnSourceDisabled;
            _twain.DeviceEvent -= OnDeviceEvent;
            _twain.TransferReady -= OnTransferReady;
            _currentSource.Close();
            return new List<Bitmap>();
        }

        // Wait for scan to complete (the message loop processes TWAIN events)
        // Use a timeout to avoid hanging forever
        if (!_scanComplete.Wait(120000)) // 2 minute timeout
        {
            LogDiag("Scan timed out");
            _scanCancelled = true;
        }

        // Cleanup
        _twain.DataTransferred -= OnDataTransferred;
        _twain.SourceDisabled -= OnSourceDisabled;
        _twain.DeviceEvent -= OnDeviceEvent;
        _twain.TransferReady -= OnTransferReady;

        if (_currentSource.IsOpen)
        {
            try { _currentSource.Close(); } catch { }
        }

        return new List<Bitmap>(_acquiredImages);
    }

    private void OnTransferReady(object? sender, TransferReadyEventArgs e)
    {
        // In long page mode, try to set the frame to 120 inches right before the scan starts.
        // The TransferReady event fires after the driver dialog (if any) but before data transfer.
        // This is our last chance to override the frame.
        bool isLongPageMode = _settings.PaperSize == PaperSizeMode.Automatic
            && _settings.MultiFeedDetection != MultiFeedDetection.Length
            && _settings.MultiFeedDetection != MultiFeedDetection.Both;

        if (!isLongPageMode || _currentSource == null)
            return;

        LogDiag("TransferReady: attempting to set long page frame (125 inches)");

        try
        {
            // Set units to inches
            _currentSource.Capabilities.ICapUnits.SetValue(Unit.Inches);

            // Try ICapFrames
            var longFrame = new TWFrame
            {
                Left = 0,
                Right = 8.5f,
                Top = 0,
                Bottom = 125f
            };
            _currentSource.Capabilities.ICapFrames.SetValue(longFrame);
            LogDiag("TransferReady: ICapFrames set to 8.5x125");

            // Try ImageLayout
            if (_currentSource.DGImage.ImageLayout.Get(out var layout) == ReturnCode.Success)
            {
                layout.Frame = longFrame;
                var rc = _currentSource.DGImage.ImageLayout.Set(layout);
                LogDiag($"TransferReady: ImageLayout set rc={rc}");
            }
        }
        catch (Exception ex)
        {
            LogDiag($"TransferReady: frame set failed: {ex.Message}");
        }
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

            var cap = (NTwain.Data.CapabilityId)capId;
            var rc = _currentSource!.DGControl.Capability.Set(
                new NTwain.Data.TWCapability(cap, new NTwain.Data.TWOneValue
                {
                    Item = fix32Value,
                    ItemType = NTwain.Data.ItemType.Fix32
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
            var cap = (NTwain.Data.CapabilityId)capId;
            var rc = _currentSource!.DGControl.Capability.Set(
                new NTwain.Data.TWCapability(cap, new NTwain.Data.TWOneValue
                {
                    Item = value,
                    ItemType = NTwain.Data.ItemType.UInt16
                }));
            LogDiag($"Custom cap {capId} SET to {value} rc={rc}");
        }
        catch (Exception ex)
        {
            LogDiag($"Custom cap {capId} SET failed: {ex.Message}");
        }
    }

    private void ConfigureCapabilities(ScanSide scanSideOverride)
    {
        if (_currentSource == null) return;

        // Determine effective scan side
        ScanSide effectiveSide = scanSideOverride != ScanSide.Automatic
            ? scanSideOverride
            : _settings.ScanSide;

        // In long page mode (Automatic paper size + no length detection), skip ALL capability
        // configuration. The PaperStream IP driver resets paper size to 14 inches (Legal) whenever
        // ANY capability is set. With ShowUI mode, the driver keeps its previous settings
        // (e.g. Long Page + 120 inch custom length) only if we don't touch any capabilities.
        bool isLongPageMode = _settings.PaperSize == PaperSizeMode.Automatic
            && _settings.MultiFeedDetection != MultiFeedDetection.Length
            && _settings.MultiFeedDetection != MultiFeedDetection.Both;

        if (isLongPageMode)
        {
            // Try to set the frame to 120 inches BEFORE the driver UI appears.
            try
            {
                _currentSource.Capabilities.ICapUnits.SetValue(Unit.Inches);

                // Set SupportedSize to None (custom frame)
                var supportedSizes = _currentSource.Capabilities.ICapSupportedSizes.GetValues();
                if (supportedSizes.Contains(SupportedSize.None))
                {
                    _currentSource.Capabilities.ICapSupportedSizes.SetValue(SupportedSize.None);
                    LogDiag("Long page mode: ICapSupportedSizes set to None (custom frame)");
                }

                // Set ICapFrames to 125 inches
                var longFrame = new TWFrame
                {
                    Left = 0,
                    Right = 8.5f,
                    Top = 0,
                    Bottom = 125f
                };
                _currentSource.Capabilities.ICapFrames.SetValue(longFrame);
                LogDiag("Long page mode: ICapFrames set to 8.5x125");

                // Also try ImageLayout
                if (_currentSource.DGImage.ImageLayout.Get(out var layout) == ReturnCode.Success)
                {
                    layout.Frame = longFrame;
                    var rc = _currentSource.DGImage.ImageLayout.Set(layout);
                    LogDiag($"Long page mode: ImageLayout set rc={rc}");
                }

                // Try setting PaperStream IP custom caps directly:
                // Cap 40983 = paper width (TW_FIX32), Cap 40984 = paper length (TW_FIX32)
                // Cap 41095 = cropping mode (0=Fixed, 1=DetectLength, 2=Automatic, 3=LongPage)
                // TW_FIX32: 16-bit whole + 16-bit fraction (frac/65536)
                SetCustomCapFix32(40983, 8.5f);
                SetCustomCapFix32(40984, 125f);
                SetCustomCapUInt16(41095, 3); // Long Page mode
            }
            catch (Exception ex)
            {
                LogDiag($"Long page mode: frame pre-set failed: {ex.Message}");
            }
            return;
        }

        // Flatbed mode: disable feeder to use flatbed
        if (effectiveSide == ScanSide.Flatbed)
        {
            try
            {
                _currentSource.Capabilities.CapFeederEnabled.SetValue(BoolType.False);
            }
            catch (Exception ex)
            {
                LogDiag($"Feeder disable (flatbed) failed: {ex.Message}");
            }
        }
        else
        {
            // Ensure feeder is enabled for ADF scanning
            try
            {
                _currentSource.Capabilities.CapFeederEnabled.SetValue(BoolType.True);
            }
            catch (Exception ex)
            {
                LogDiag($"Feeder enable failed: {ex.Message}");
            }

            // Duplex / Simplex
            try
            {
                if (effectiveSide == ScanSide.Duplex)
                {
                    _currentSource.Capabilities.CapDuplexEnabled.SetValue(BoolType.True);
                }
                else if (effectiveSide == ScanSide.Simplex)
                {
                    _currentSource.Capabilities.CapDuplexEnabled.SetValue(BoolType.False);
                }
            }
            catch (Exception ex)
            {
                LogDiag($"Duplex cap failed: {ex.Message}");
            }
        }

        // Color mode
        try
        {
            var pixelType = _settings.ColorMode switch
            {
                ColorMode.Color => PixelType.RGB,
                ColorMode.Grayscale => PixelType.Gray,
                ColorMode.BlackWhite => PixelType.BlackWhite,
                _ => PixelType.RGB // Automatic
            };
            _currentSource.Capabilities.ICapPixelType.SetValue(pixelType);
        }
        catch (Exception ex)
        {
            LogDiag($"PixelType cap failed: {ex.Message}");
        }

        // Resolution based on image quality
        try
        {
            var dpi = _settings.ImageQuality switch
            {
                ImageQuality.Normal => 150,
                ImageQuality.Fine => 200,
                ImageQuality.Best => 300,
                ImageQuality.Excellent => 600,
                _ => 200 // Automatic
            };

            _currentSource.Capabilities.ICapXResolution.SetValue(dpi);
            _currentSource.Capabilities.ICapYResolution.SetValue(dpi);
            LogDiag($"Resolution set to {dpi} DPI");
        }
        catch (Exception ex)
        {
            LogDiag($"Resolution cap failed: {ex.Message}");
        }

        // Paper size
        try
        {
            if (_settings.PaperSize == PaperSizeMode.Automatic)
            {
                // When paper size is automatic and length detection is off,
                // enable long paper / max size scanning
                var lengthDetectionOff = _settings.MultiFeedDetection != MultiFeedDetection.Length
                    && _settings.MultiFeedDetection != MultiFeedDetection.Both;

                if (lengthDetectionOff)
                {
                    // Don't set ICapSupportedSizes here — setting MaxSize resets the driver's
                    // frame to 14 inches (Legal), overwriting any long page setting the user
                    // configured in the driver UI. With ShowUI mode, the driver keeps its
                    // previous settings (e.g. Long Page + 120 inch custom length).
                    LogDiag("Long page mode: skipping ICapSupportedSizes set to preserve driver UI settings");
                }
            }
            else if (_settings.PaperSize != PaperSizeMode.Automatic)
            {
                var supportedSizes = _currentSource.Capabilities.ICapSupportedSizes.GetValues();
                var targetSize = _settings.PaperSize switch
                {
                    PaperSizeMode.A3 => SupportedSize.A3,
                    PaperSizeMode.A4 => SupportedSize.A4,
                    PaperSizeMode.A5 => SupportedSize.A5,
                    PaperSizeMode.A6 => SupportedSize.A6,
                    PaperSizeMode.B4Jis => SupportedSize.JisB4,
                    PaperSizeMode.B5Jis => SupportedSize.JisB5,
                    PaperSizeMode.B6Jis => SupportedSize.JisB6,
                    PaperSizeMode.Postcard => SupportedSize.A6,
                    PaperSizeMode.BusinessCard => SupportedSize.BusinessCard,
                    PaperSizeMode.Letter => SupportedSize.USLetter,
                    PaperSizeMode.DoubleLetter => SupportedSize.USLedger,
                    PaperSizeMode.Legal => SupportedSize.USLegal,
                    // Automatic and Custom are handled separately; fallback to A4
                    _ => SupportedSize.A4
                };
                if (supportedSizes.Contains(targetSize))
                {
                    _currentSource.Capabilities.ICapSupportedSizes.SetValue(targetSize);
                }
            }
        }
        catch (Exception ex)
        {
            LogDiag($"Paper size cap failed: {ex.Message}");
        }

        // Brightness (only for B/W)
        if (_settings.ColorMode == ColorMode.BlackWhite && _settings.Brightness != 0)
        {
            try
            {
                // Brightness range is typically -1000 to +1000 in TWAIN
                int brightness = _settings.Brightness * 333; // -3..3 -> -999..999
                _currentSource.Capabilities.ICapBrightness.SetValue(brightness);
            }
            catch (Exception ex)
            {
                LogDiag($"Brightness cap failed: {ex.Message}");
            }
        }

        // Multi-feed detection (Phase 15)
        if (_settings.MultiFeedDetection != MultiFeedDetection.Off)
        {
            try
            {
                var detectionMode = _settings.MultiFeedDetection switch
                {
                    MultiFeedDetection.OverlapUltrasound => DoubleFeedDetection.Ultrasonic,
                    MultiFeedDetection.Length => DoubleFeedDetection.ByLength,
                    MultiFeedDetection.Both => DoubleFeedDetection.Ultrasonic,
                    _ => DoubleFeedDetection.Ultrasonic
                };
                _currentSource.Capabilities.CapDoubleFeedDetection.SetValue(detectionMode);

                // Set response to StopAndWait so scanner pauses on multi-feed
                _currentSource.Capabilities.CapDoubleFeedDetectionResponse.SetValue(
                    DoubleFeedDetectionResponse.StopAndWait);

                LogDiag($"Multi-feed detection set: {detectionMode}");
            }
            catch (Exception ex)
            {
                LogDiag($"Multi-feed detection cap failed: {ex.Message}");
            }
        }
    }

    private void OnDataTransferred(object? sender, DataTransferredEventArgs e)
    {
        try
        {
            // Get the image from the transfer
            Bitmap? bmp = null;

            // Try native transfer via stream
            var stream = e.GetNativeImageStream();
            if (stream != null)
            {
                stream.Position = 0;
                bmp = new Bitmap(stream);
            }

            // Fallback: check if file path was used (file transfer)
            if (bmp == null && !string.IsNullOrEmpty(e.FileDataPath) && File.Exists(e.FileDataPath))
            {
                bmp = new Bitmap(e.FileDataPath);
            }

            // Fallback: memory data
            if (bmp == null && e.MemoryData != null && e.MemoryData.Length > 0)
            {
                using var ms = new MemoryStream(e.MemoryData);
                bmp = new Bitmap(ms);
            }

            if (bmp != null)
            {
                _acquiredImages.Add(bmp);
            }
        }
        catch (Exception ex)
        {
            LogDiag($"Image transfer failed: {ex.Message}");
        }
    }

    private void OnSourceDisabled(object? sender, EventArgs e)
    {
        _scanComplete.Set();
    }

    private void OnDeviceEvent(object? sender, DeviceEventArgs e)
    {
        if (e.DeviceEvent.Event == DeviceEvent.PaperDoubleFeed)
        {
            LogDiag("Multi-feed detected by scanner!");
            _multiFeedDetected = true;

            // Get preview images: current page (last acquired) and previous page
            Bitmap? currentPage = _acquiredImages.Count > 0 ? _acquiredImages[^1] : null;
            Bitmap? previousPage = _acquiredImages.Count > 1 ? _acquiredImages[^2] : null;

            // Show warning dialog on UI thread
            MultiFeedWarningDialog.MultiFeedAction action = MultiFeedWarningDialog.MultiFeedAction.Rescan;
            try
            {
                if (_hiddenWindow.InvokeRequired)
                {
                    _hiddenWindow.Invoke(() =>
                    {
                        action = ShowMultiFeedWarning(currentPage, previousPage);
                    });
                }
                else
                {
                    action = ShowMultiFeedWarning(currentPage, previousPage);
                }
            }
            catch (Exception ex)
            {
                LogDiag($"Multi-feed dialog failed: {ex.Message}");
            }

            switch (action)
            {
                case MultiFeedWarningDialog.MultiFeedAction.Rescan:
                    _userCancelledMultiFeed = true;
                    _scanCancelled = true;
                    _scanComplete.Set();
                    break;

                case MultiFeedWarningDialog.MultiFeedAction.KeepAsIs:
                    // Continue scanning, keep current page
                    LogDiag("User chose to keep page despite multi-feed");
                    break;

                case MultiFeedWarningDialog.MultiFeedAction.DisableDetection:
                    // Disable multi-feed detection for the rest of this scan
                    _disableMultiFeedDetection = true;
                    try
                    {
                        _currentSource?.Capabilities.CapDoubleFeedDetection.Reset();
                        LogDiag("Multi-feed detection disabled by user");
                    }
                    catch (Exception ex)
                    {
                        LogDiag($"Failed to disable multi-feed: {ex.Message}");
                    }
                    break;
            }
        }
    }

    private MultiFeedWarningDialog.MultiFeedAction ShowMultiFeedWarning(Bitmap? currentPage, Bitmap? previousPage)
    {
        using var dlg = new MultiFeedWarningDialog(currentPage, previousPage);
        return dlg.ShowDialog() == DialogResult.OK ? dlg.Action : MultiFeedWarningDialog.MultiFeedAction.Rescan;
    }

    public void Dispose()
    {
        _scanComplete.Dispose();
    }
}
