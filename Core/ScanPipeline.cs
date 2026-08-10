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
                System.Diagnostics.Debug.WriteLine($"DSM open failed: {rc}");
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
            System.Diagnostics.Debug.WriteLine($"Source open failed: {openRc}");
            return new List<Bitmap>();
        }

        // Configure capabilities
        ConfigureCapabilities(scanSideOverride);

        // Subscribe to transfer events
        _twain.DataTransferred += OnDataTransferred;
        _twain.SourceDisabled += OnSourceDisabled;
        _twain.DeviceEvent += OnDeviceEvent;

        // Enable the source (starts scanning)
        var enableRc = _currentSource.Enable(SourceEnableMode.NoUI, true, _hiddenWindow.Handle);
        if (enableRc != ReturnCode.Success)
        {
            System.Diagnostics.Debug.WriteLine($"Enable failed: {enableRc}");
            _twain.DataTransferred -= OnDataTransferred;
            _twain.SourceDisabled -= OnSourceDisabled;
            _twain.DeviceEvent -= OnDeviceEvent;
            _currentSource.Close();
            return new List<Bitmap>();
        }

        // Wait for scan to complete (the message loop processes TWAIN events)
        // Use a timeout to avoid hanging forever
        if (!_scanComplete.Wait(120000)) // 2 minute timeout
        {
            System.Diagnostics.Debug.WriteLine("Scan timed out");
            _scanCancelled = true;
        }

        // Cleanup
        _twain.DataTransferred -= OnDataTransferred;
        _twain.SourceDisabled -= OnSourceDisabled;
        _twain.DeviceEvent -= OnDeviceEvent;

        if (_currentSource.IsOpen)
        {
            try { _currentSource.Close(); } catch { }
        }

        return new List<Bitmap>(_acquiredImages);
    }

    private void ConfigureCapabilities(ScanSide scanSideOverride)
    {
        if (_currentSource == null) return;

        // Determine effective scan side
        ScanSide effectiveSide = scanSideOverride != ScanSide.Automatic
            ? scanSideOverride
            : _settings.ScanSide;

        // Flatbed mode: disable feeder to use flatbed
        if (effectiveSide == ScanSide.Flatbed)
        {
            try
            {
                _currentSource.Capabilities.CapFeederEnabled.SetValue(BoolType.False);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Feeder disable (flatbed) failed: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Feeder enable failed: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Duplex cap failed: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"PixelType cap failed: {ex.Message}");
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
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Resolution cap failed: {ex.Message}");
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
                    var supportedSizes = _currentSource.Capabilities.ICapSupportedSizes.GetValues();
                    if (supportedSizes.Contains(SupportedSize.MaxSize))
                    {
                        _currentSource.Capabilities.ICapSupportedSizes.SetValue(SupportedSize.MaxSize);
                        System.Diagnostics.Debug.WriteLine("Paper size set to MaxSize (unlimited length)");
                    }
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
            System.Diagnostics.Debug.WriteLine($"Paper size cap failed: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Brightness cap failed: {ex.Message}");
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

                System.Diagnostics.Debug.WriteLine($"Multi-feed detection set: {detectionMode}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Multi-feed detection cap failed: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"Image transfer failed: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine("Multi-feed detected by scanner!");
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
                System.Diagnostics.Debug.WriteLine($"Multi-feed dialog failed: {ex.Message}");
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
                    System.Diagnostics.Debug.WriteLine("User chose to keep page despite multi-feed");
                    break;

                case MultiFeedWarningDialog.MultiFeedAction.DisableDetection:
                    // Disable multi-feed detection for the rest of this scan
                    _disableMultiFeedDetection = true;
                    try
                    {
                        _currentSource?.Capabilities.CapDoubleFeedDetection.Reset();
                        System.Diagnostics.Debug.WriteLine("Multi-feed detection disabled by user");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to disable multi-feed: {ex.Message}");
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
