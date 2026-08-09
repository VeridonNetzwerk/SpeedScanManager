using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;
using NTwain;
using NTwain.Data;

namespace SpeedScanManager;

internal enum ScannerStatus { Unknown, Connected, Disconnected, Scanning }

/// <summary>
/// ApplicationContext that runs the app as a tray-only application.
/// Polls TWAIN every 3 seconds and updates the tray icon accordingly.
/// </summary>
internal class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private ContextMenuStrip _contextMenu = null!;
    private ToolStripMenuItem _miDuplexScan = null!;
    private ToolStripMenuItem _miSimplexScan = null!;
    private ToolStripMenuItem _miFlatbedScan = null!;
    private ToolStripMenuItem _miScanKeySettings = null!;
    private ToolStripMenuItem _miProfileManagement = null!;
    private ToolStripMenuItem _miScanResult = null!;
    private ToolStripMenuItem _miHelp = null!;
    private ToolStripMenuItem _miExit = null!;
    private readonly System.Windows.Forms.Timer _pollTimer;
    private readonly Form _hiddenWindow;
    private TwainSession? _twain;
    private WindowsFormsMessageLoopHook? _msgLoop;
    private ScannerStateService? _scannerStateService;
    private bool _twainInitialized;
    private MainForm? _mainForm;
    private readonly ScanSettings _settings = new();
    private readonly ProfileManager _profileManager = new();
    private ScanPipeline? _scanPipeline;
    private bool _isScanning;
    private ApplicationType _currentApplicationType = ApplicationType.ScanToFolder;
    private List<string> _lastScanFiles = new();
    private ScannerState? _lastScannerState;

    // State machine for scanner connection
    private ScannerStatus _scannerStatus = ScannerStatus.Unknown;
    private string _currentScannerName = "";
    private int _pendingDisconnectCount;
    private const int DisconnectConfirmThreshold = 2;

    // Cached icons (avoid recreating GDI handles on every poll)
    private Icon? _cachedConnectedIcon;
    private Icon? _cachedDisconnectedIcon;

    private const int PollIntervalMs = 3000;
    private const int SlowPollIntervalMs = 30000;
    private const string TrayNotificationTitle = "[SpeedScan Manager]";

    public TrayApplicationContext()
    {
        // Hidden window provides the HWND that TWAIN DSM requires.
        _hiddenWindow = new Form
        {
            ShowInTaskbar = false,
            WindowState = FormWindowState.Minimized,
            Opacity = 0
        };

        _notifyIcon = new NotifyIcon
        {
            Icon = GetDisconnectedIcon(),
            Visible = true,
            Text = "SpeedScan Manager"
        };

        BuildContextMenu();
        _notifyIcon.ContextMenuStrip = _contextMenu;
        _notifyIcon.DoubleClick += OnTrayDoubleClick;

        // Defer TWAIN init until the message loop is running (WindowsFormsMessageLoopHook requires UI thread)
        Application.Idle += OnFirstIdle;

        _pollTimer = new System.Windows.Forms.Timer
        {
            Interval = PollIntervalMs
        };
        _pollTimer.Tick += OnPollTick;
        _pollTimer.Start();
    }

    private void BuildContextMenu()
    {
        _contextMenu = new ContextMenuStrip
        {
            RenderMode = ToolStripRenderMode.System,
            Font = SystemFonts.MenuFont,
            ShowImageMargin = false
        };
        _contextMenu.Opening += OnContextMenuOpening;
        _contextMenu.Closed += OnContextMenuClosed;

        _miDuplexScan = new ToolStripMenuItem("Duplex-Scan");
        _miSimplexScan = new ToolStripMenuItem("Simplex-Scan");
        _miFlatbedScan = new ToolStripMenuItem("Flachbettscannen");
        _miScanKeySettings = new ToolStripMenuItem("Einstellungen der SCAN Taste...");
        _miProfileManagement = new ToolStripMenuItem("Profilverwaltung...");
        _miScanResult = new ToolStripMenuItem("Scan-Ergebnis anzeigen");
        _miHelp = new ToolStripMenuItem("Hilfe");
        _miExit = new ToolStripMenuItem("Beenden");

        // Help submenu items
        var miHelpTopics = new ToolStripMenuItem("Hilfethemen");
        var miVersionInfo = new ToolStripMenuItem("SpeedScan Manager for fi Series – Versionsinformationen");
        var miPreferences = new ToolStripMenuItem("Präferenzen...");

        _miDuplexScan.Click += (s, e) => StartScan(ScanSide.Duplex);
        _miSimplexScan.Click += (s, e) => StartScan(ScanSide.Simplex);
        _miFlatbedScan.Click += (s, e) => StartScan(ScanSide.Flatbed);
        _miScanKeySettings.Click += (s, e) => ShowMainForm();
        _miProfileManagement.Click += (s, e) => OpenProfileManagement();
        _miScanResult.Click += (s, e) => OpenScanResultFolder();
        miHelpTopics.Click += (s, e) => ShowHelpDialog();
        miVersionInfo.Click += (s, e) => ShowVersionInfo();
        miPreferences.Click += (s, e) => ShowPreferencesDialog();
        _miExit.Click += (s, e) => ExitApplication();

        _miHelp.DropDownItems.AddRange(new ToolStripItem[]
        {
            miHelpTopics,
            miVersionInfo,
            new ToolStripSeparator(),
            miPreferences
        });

        _contextMenu.Items.AddRange(new ToolStripItem[]
        {
            _miDuplexScan,
            _miSimplexScan,
            _miFlatbedScan,
            new ToolStripSeparator(),
            _miScanKeySettings,
            _miProfileManagement,
            new ToolStripSeparator(),
            _miScanResult,
            new ToolStripSeparator(),
            _miHelp,
            _miExit
        });
    }

    private void OnContextMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            // Pause polling while the context menu is open to avoid
            // concurrent TWAIN access from the poll timer.
            _pollTimer.Stop();
            // Only query TWAIN if we think the scanner might be connected.
            // When already Disconnected, skip the query to avoid triggering
            // the TWAIN driver's "Kommunikation fehlgeschlagen" dialog.
            if (_scannerStatus != ScannerStatus.Disconnected)
                UpdateConnectionState();
            // UpdateMenuItems uses the cached _lastScannerState from UpdateConnectionState
            // to avoid opening the TWAIN source a second time.
            UpdateMenuItems();
        }
        catch (Exception ex)
        {
            LogDiag($"OnContextMenuOpening exception: {ex.Message}");
        }
    }

    private void OnContextMenuClosed(object? sender, EventArgs e)
    {
        // Resume polling after the context menu closes.
        _pollTimer.Start();
    }

    /// <summary>
    /// Updates all menu items based on current scanner state and last scan files.
    /// Called before each menu open via the Opening event.
    /// </summary>
    private void UpdateMenuItems()
    {
        bool connected = _scannerStatus == ScannerStatus.Connected;
        bool supportsDuplex = false, supportsSimplex = false, supportsFlatbed = false;
        bool supportsUltrasonic = false, supportsLength = false;

        // Use cached scanner state from UpdateConnectionState to avoid
        // opening the TWAIN source a second time (causes native crash).
        if (connected && !_isScanning && _lastScannerState != null)
        {
            supportsDuplex = _lastScannerState.SupportsDuplex;
            supportsSimplex = _lastScannerState.SupportsSimplex;
            supportsFlatbed = _lastScannerState.SupportsFlatbed;
            supportsUltrasonic = _lastScannerState.SupportsUltrasonicDetection;
            supportsLength = _lastScannerState.SupportsLengthDetection;

            // Update multi-feed capabilities with cached values
            if (_mainForm != null && !_mainForm.IsDisposed && _mainForm.PaperTab != null)
            {
                _mainForm.PaperTab.UpdateMultiFeedCapabilities(
                    supportsUltrasonic,
                    supportsLength);
            }
        }
        else if (_mainForm != null && !_mainForm.IsDisposed && _mainForm.PaperTab != null)
        {
            _mainForm.PaperTab.UpdateMultiFeedCapabilities(false, false);
        }

        _miDuplexScan.Enabled = connected && supportsDuplex;
        _miSimplexScan.Enabled = connected && supportsSimplex;
        _miFlatbedScan.Enabled = connected && supportsFlatbed;
        _miScanKeySettings.Enabled = true;
        _miProfileManagement.Enabled = true;
        _miScanResult.Enabled = _lastScanFiles.Count > 0;
        _miHelp.Enabled = true;
        _miExit.Enabled = true;
    }

    private HelpForm? _helpForm;

    private void ShowHelpDialog()
    {
        if (_helpForm != null && !_helpForm.IsDisposed)
        {
            _helpForm.Activate();
            return;
        }

        _helpForm = new HelpForm();
        _helpForm.FormClosed += (s, e) => _helpForm = null;
        _helpForm.Show(_hiddenWindow);
    }

    private void ShowVersionInfo()
    {
        using var dlg = new VersionInfoDialog();
        dlg.ShowDialog(_hiddenWindow);
    }

    private void ShowPreferencesDialog()
    {
        using var dlg = new PreferencesDialog(_settings);
        dlg.ShowDialog(_mainForm ?? _hiddenWindow);
    }

    private void OpenProfileManagement()
    {
        using var dlg = new ProfileManagementDialog(_profileManager);
        dlg.ShowDialog(_mainForm ?? _hiddenWindow);
    }

    private void StartScan(ScanSide scanSideOverride)
    {
        if (_isScanning)
        {
            MessageBox.Show("Ein Scanvorgang läuft bereits.", "SpeedScan Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!_twainInitialized || _twain == null || _msgLoop == null)
        {
            MessageBox.Show("TWAIN ist nicht initialisiert. Kein Scanner verfügbar.", "SpeedScan Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Sync settings from MainForm if it's open
        SyncSettingsFromMainForm();

        _isScanning = true;
        _scannerStatus = ScannerStatus.Scanning;
        _notifyIcon.Text = "SpeedScan Manager – Scanne...";

        // Run scan on a background thread to avoid blocking the UI message loop
        Task.Run(() =>
        {
            try
            {
                _scanPipeline = new ScanPipeline(_twain, _msgLoop, _hiddenWindow, _settings);
                var images = _scanPipeline.ExecuteScan(scanSideOverride);

                if (images.Count > 0)
                {
                    // Get save config
                    var (folder, formatMode, customName, digits) = GetSaveConfig();
                    Directory.CreateDirectory(folder);

                    var processor = new ScanOutputProcessor(_settings);
                    var fileNames = processor.ProcessAndSave(images, folder, _settings, formatMode, customName, digits);

                    // If Scan to Print, keep copies of images for printing before disposal
                    List<Bitmap>? imagesToPrint = null;
                    if (_currentApplicationType == ApplicationType.ScanToPrint)
                    {
                        imagesToPrint = images.Select(b => (Bitmap)b.Clone()).ToList();
                    }

                    // Dispose images
                    foreach (var img in images)
                        img.Dispose();

                    _hiddenWindow.BeginInvoke(() =>
                    {
                        if (fileNames.Count > 0)
                        {
                            _lastScanFiles = fileNames;
                            _notifyIcon.ShowBalloonTip(3000, TrayNotificationTitle,
                                $"Scan abgeschlossen: {fileNames.Count} Datei(en) erstellt.",
                                ToolTipIcon.Info);

                            // Open mail client if Scan to E-Mail
                            if (_currentApplicationType == ApplicationType.ScanToEmail)
                            {
                                OpenMailClient(fileNames);
                            }

                            // Print if Scan to Print
                            if (_currentApplicationType == ApplicationType.ScanToPrint && imagesToPrint != null)
                            {
                                PrintScannedImages(imagesToPrint);
                            }
                        }
                        else
                        {
                            _notifyIcon.ShowBalloonTip(3000, TrayNotificationTitle,
                                "Scan abgeschlossen, aber keine Dateien erstellt.",
                                ToolTipIcon.Warning);
                        }
                    });
                }
                else if (!_scanPipeline.WasCancelled)
                {
                    _hiddenWindow.BeginInvoke(() =>
                    {
                        _notifyIcon.ShowBalloonTip(3000, TrayNotificationTitle,
                            "Keine Bilder vom Scanner empfangen.",
                            ToolTipIcon.Warning);
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Scan failed: {ex}");
                _hiddenWindow.BeginInvoke(() =>
                {
                    _notifyIcon.ShowBalloonTip(3000, TrayNotificationTitle,
                        $"Scan-Fehler: {ex.Message}",
                        ToolTipIcon.Error);
                });
            }
            finally
            {
                _hiddenWindow.BeginInvoke(() =>
                {
                    _isScanning = false;
                    // Infer connection state from scan result — don't call QueryState()
                    // which would trigger the driver's "Kommunikation fehlgeschlagen" dialog
                    // if the scanner is off.
                    if (_scanPipeline != null && _scanPipeline.AcquiredImages.Count > 0)
                    {
                        _scannerStatus = ScannerStatus.Connected;
                    }
                    else
                    {
                        _scannerStatus = ScannerStatus.Disconnected;
                        _currentScannerName = "";
                        _lastScannerState = null;
                    }
                    UpdateTrayVisuals();
                });
            }
        });
    }

    private void SyncSettingsFromMainForm()
    {
        if (_mainForm != null && !_mainForm.IsDisposed)
        {
            // MainForm shares the same ScanSettings instance, so values are already synced
            // But we need to ensure the save tab config is current
            if (_mainForm.ApplicationTab != null)
            {
                _currentApplicationType = _mainForm.ApplicationTab.SelectedApplicationType;
            }
        }
    }

    private (string folder, FileNameFormatDialog.FormatMode mode, string customName, int digits) GetSaveConfig()
    {
        if (_mainForm != null && !_mainForm.IsDisposed && _mainForm.SaveTab != null)
        {
            return _mainForm.SaveTab.GetSaveConfig();
        }

        // Default config when MainForm isn't open
        string defaultFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SpeedScanManager");
        return (defaultFolder, FileNameFormatDialog.FormatMode.Timestamp, "unbenannt", 3);
    }

    private void OpenScanResultFolder()
    {
        if (_lastScanFiles.Count == 0)
            return;

        string? firstFile = _lastScanFiles.FirstOrDefault(f => File.Exists(f));
        if (firstFile != null)
        {
            string? folder = Path.GetDirectoryName(firstFile);
            if (folder != null && Directory.Exists(folder))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", folder)
                {
                    UseShellExecute = true
                });
            }
        }
        else
        {
            // All files deleted, fall back to configured folder
            var (folder, _, _, _) = GetSaveConfig();
            if (Directory.Exists(folder))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", folder)
                {
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show($"Der Ordner \"{folder}\" existiert nicht.", "SpeedScan Manager",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }

    private void OpenMailClient(List<string> filePaths)
    {
        try
        {
            bool success = MailHelper.OpenMailWithAttachment(
                _settings.EmailSubjectTemplate,
                _settings.EmailRecipient,
                filePaths);

            if (!success)
            {
                _notifyIcon.ShowBalloonTip(3000, TrayNotificationTitle,
                    "E-Mail-Client konnte nicht geöffnet werden. Dateien wurden im Zielordner gespeichert.",
                    ToolTipIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Mail client failed: {ex.Message}");
            _notifyIcon.ShowBalloonTip(3000, TrayNotificationTitle,
                $"E-Mail-Fehler: {ex.Message}",
                ToolTipIcon.Error);
        }
    }

    private void PrintScannedImages(List<Bitmap> images)
    {
        try
        {
            bool success = PrintHelper.PrintImages(images, _settings.PrinterName);

            if (success)
            {
                _notifyIcon.ShowBalloonTip(3000, TrayNotificationTitle,
                    "Dokument wurde zum Drucker gesendet.",
                    ToolTipIcon.Info);
            }
            else
            {
                _notifyIcon.ShowBalloonTip(3000, TrayNotificationTitle,
                    "Druck fehlgeschlagen. Dateien wurden im Zielordner gespeichert.",
                    ToolTipIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Print failed: {ex.Message}");
            _notifyIcon.ShowBalloonTip(3000, TrayNotificationTitle,
                $"Druck-Fehler: {ex.Message}",
                ToolTipIcon.Error);
        }
        finally
        {
            foreach (var img in images)
                img.Dispose();
        }
    }

    private void ShowMainForm()
    {
        if (_mainForm == null || _mainForm.IsDisposed)
        {
            _mainForm = new MainForm(_settings);
            _mainForm.FormClosed += (s, e) => _mainForm = null;
        }
        if (!_mainForm.Visible)
        {
            // Pause polling while opening the form to avoid concurrent TWAIN access
            _pollTimer.Stop();
            // Refresh connection state only if scanner might be connected.
            // Skip when already Disconnected to avoid TWAIN driver dialog.
            if (_scannerStatus != ScannerStatus.Disconnected)
                UpdateConnectionState();
            _mainForm.Show();
            // Use cached scanner state to avoid opening TWAIN source again
            if (_scannerStatus == ScannerStatus.Connected)
            {
                bool ultrasonic = _lastScannerState?.SupportsUltrasonicDetection ?? true;
                bool length = _lastScannerState?.SupportsLengthDetection ?? true;
                _mainForm.PaperTab?.UpdateMultiFeedCapabilities(ultrasonic, length);
            }
            else
            {
                _mainForm.PaperTab?.UpdateMultiFeedCapabilities(false, false);
            }
            _pollTimer.Start();
        }
        _mainForm.BringToFront();
        _mainForm.Activate();
    }

    private void OnTrayDoubleClick(object? sender, EventArgs e)
    {
        ShowMainForm();
    }

    private void ExitApplication()
    {
        _notifyIcon.Visible = false;
        Application.Exit();
    }

    private void InitializeTwain()
    {
        try
        {
            _hiddenWindow.Show();
            _hiddenWindow.Hide();

            var appId = TWIdentity.CreateFromAssembly(
                DataGroups.Control | DataGroups.Image,
                Assembly.GetExecutingAssembly());

            _twain = new TwainSession(appId);
            _msgLoop = new WindowsFormsMessageLoopHook(_hiddenWindow.Handle);
            var rc = _twain.Open(_msgLoop);
            _twainInitialized = rc == ReturnCode.Success;

            LogDiag($"TWAIN init: DSM open rc={rc}, initialized={_twainInitialized}");

            if (_twainInitialized)
            {
                _scannerStateService = new ScannerStateService(_twain, _msgLoop);
                try
                {
                    var sources = _twain.GetSources().ToList();
                    LogDiag($"TWAIN sources count={sources.Count}");
                    foreach (var s in sources)
                        LogDiag($"  Source: {s.Name} (mfr={s.Manufacturer}, family={s.ProductFamily})");
                    var def = _twain.DefaultSource;
                    LogDiag($"  DefaultSource: {def?.Name ?? "(null)"}");
                }
                catch (Exception ex) { LogDiag($"Source enum failed: {ex.Message}"); }
            }
        }
        catch (Exception ex)
        {
            LogDiag($"TWAIN init exception: {ex}");
            _twainInitialized = false;
        }
    }

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

    private void OnFirstIdle(object? sender, EventArgs e)
    {
        Application.Idle -= OnFirstIdle;
        InitializeTwain();
        // Don't query TWAIN at startup — source.Open() on a disconnected scanner
        // triggers the driver's native "Kommunikation fehlgeschlagen" dialog (DS42019).
        // Default to Disconnected; the user knows from the tray icon.
        // When the user tries to scan, StartScan will detect the scanner.
        _scannerStatus = ScannerStatus.Disconnected;
        UpdateTrayVisuals();
        UpdateMenuItems();
    }

    private void OnPollTick(object? sender, EventArgs e)
    {
        try
        {
            // When already disconnected, skip background polling entirely.
            // The TWAIN driver shows a "Kommunikation fehlgeschlagen" dialog
            // every time source.Open() is attempted on a disconnected scanner.
            // Reconnection is detected when the user opens the context menu
            // (OnContextMenuOpening) or tries to scan — both call UpdateConnectionState().
            if (_scannerStatus == ScannerStatus.Disconnected)
                return;
            UpdateConnectionState();
        }
        catch (Exception ex)
        {
            LogDiag($"OnPollTick exception: {ex.Message}");
        }
    }

    private bool _isUpdatingConnection;

    private void UpdateConnectionState()
    {
        // Don't poll during scanning — the scanner is busy
        if (_isScanning) return;

        // Prevent re-entrancy: if the poll timer fires while the context menu
        // is already calling this method, skip the second call.
        if (_isUpdatingConnection) return;
        _isUpdatingConnection = true;

        try
        {

        // TWAIN not yet initialized — stay in Unknown, no balloon
        if (!_twainInitialized)
        {
            LogDiag("UpdateConnectionState: TWAIN not initialized, status=Unknown");
            return;
        }

        // Use QueryState as the single TWAIN source access — it checks
        // connection (source.Open + CapDeviceOnline) AND queries capabilities
        // in one pass, avoiding double source.Open that causes native crashes.
        ScannerState? state = null;
        bool connected;
        string scannerName = "";

        if (_scannerStateService != null)
        {
            try
            {
                state = _scannerStateService.QueryState();
                connected = state.IsScannerConnected;
                scannerName = state.SourceName ?? "";
                _lastScannerState = state;
                LogDiag($"UpdateConnectionState: QueryState connected={connected}, ultrasonic={state.SupportsUltrasonicDetection}, length={state.SupportsLengthDetection}");
            }
            catch (Exception ex)
            {
                LogDiag($"UpdateConnectionState: QueryState failed: {ex.Message}");
                connected = false;
                _lastScannerState = null;
            }
        }
        else
        {
            connected = CheckScannerConnected(out scannerName);
            _lastScannerState = null;
        }

        if (connected)
        {
            _pendingDisconnectCount = 0;

            if (_scannerStatus != ScannerStatus.Connected || scannerName != _currentScannerName)
            {
                LogDiag($"UpdateConnectionState: ->Connected, scanner='{scannerName}', prev={_scannerStatus}");
                _scannerStatus = ScannerStatus.Connected;
                _currentScannerName = scannerName;
                UpdateTrayVisuals();
                // No balloon on connect (matching original ScanSnap Manager behavior)
            }
        }
        else
        {
            if (_scannerStatus == ScannerStatus.Unknown)
            {
                // First valid check after TWAIN init — take as initial state, no debounce
                LogDiag("UpdateConnectionState: ->Disconnected (initial), showing balloon");
                _scannerStatus = ScannerStatus.Disconnected;
                _currentScannerName = "";
                UpdateTrayVisuals();
                ShowScannerDisconnectedBalloon();
            }
            else if (_scannerStatus == ScannerStatus.Connected)
            {
                _pendingDisconnectCount++;
                LogDiag($"UpdateConnectionState: pending disconnect {_pendingDisconnectCount}/{DisconnectConfirmThreshold}");
                if (_pendingDisconnectCount >= DisconnectConfirmThreshold)
                {
                    LogDiag("UpdateConnectionState: ->Disconnected (confirmed), showing balloon");
                    _scannerStatus = ScannerStatus.Disconnected;
                    _currentScannerName = "";
                    UpdateTrayVisuals();
                    ShowScannerDisconnectedBalloon();
                }
            }
            // If already Disconnected, do nothing — no repeated balloon
        }
        }
        catch (Exception ex)
        {
            LogDiag($"UpdateConnectionState exception: {ex.Message}");
        }
        finally
        {
            _isUpdatingConnection = false;
        }
    }

    private void UpdateTrayVisuals()
    {
        bool connected = _scannerStatus == ScannerStatus.Connected;
        _notifyIcon.Icon = connected ? GetConnectedIcon() : GetDisconnectedIcon();
        _notifyIcon.Text = connected
            ? $"SpeedScan Manager – {_currentScannerName}"
            : "SpeedScan Manager – Kein Scanner";
    }

    private void ShowScannerDisconnectedBalloon()
    {
        _notifyIcon.ShowBalloonTip(5000, TrayNotificationTitle,
            "Der Scanner ist nicht angeschlossen oder ausgeschaltet.",
            ToolTipIcon.Warning);
    }

    private Icon GetConnectedIcon()
    {
        if (_cachedConnectedIcon == null)
            _cachedConnectedIcon = TrayIcons.CreateConnectedIcon();
        return _cachedConnectedIcon;
    }

    private Icon GetDisconnectedIcon()
    {
        if (_cachedDisconnectedIcon == null)
            _cachedDisconnectedIcon = TrayIcons.CreateDisconnectedIcon();
        return _cachedDisconnectedIcon;
    }

    /// <summary>
    /// Checks whether a TWAIN scanner is actually connected and online.
    /// Opens the default source and queries CapDeviceOnline to verify
    /// the hardware is powered and connected (not just a registered driver).
    /// </summary>
    private bool CheckScannerConnected(out string scannerName)
    {
        scannerName = "";
        if (!_twainInitialized || _twain == null)
        {
            LogDiag("CheckScannerConnected: TWAIN not initialized");
            return false;
        }

        DataSource? source = null;
        try
        {
            if (!_twain.IsDsmOpen)
            {
                var rc = _twain.Open(_msgLoop);
                LogDiag($"CheckScannerConnected: DSM reopen rc={rc}");
                if (rc != ReturnCode.Success)
                    return false;
            }

            source = _twain.DefaultSource;
            if (source == null)
            {
                var sources = _twain.GetSources().ToList();
                LogDiag($"CheckScannerConnected: DefaultSource null, sources={sources.Count}");
                if (sources.Count == 0)
                    return false;
                source = sources[0];
            }

            LogDiag($"CheckScannerConnected: opening source '{source.Name}'");
            scannerName = source.Name;
            var openRc = source.Open();
            LogDiag($"CheckScannerConnected: source.Open rc={openRc}");
            if (openRc != ReturnCode.Success)
                return false;

            try
            {
                var online = source.Capabilities.CapDeviceOnline.GetCurrent();
                LogDiag($"CheckScannerConnected: CapDeviceOnline.GetCurrent={online}");
                if (online == BoolType.False)
                    return false;
            }
            catch (Exception ex)
            {
                LogDiag($"CheckScannerConnected: CapDeviceOnline not supported ({ex.Message}), assuming connected");
            }

            LogDiag("CheckScannerConnected: CONNECTED");
            return true;
        }
        catch (Exception ex)
        {
            LogDiag($"CheckScannerConnected: exception {ex.Message}");
            return false;
        }
        finally
        {
            if (source != null && source.IsOpen)
            {
                try { source.Close(); } catch { }
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pollTimer.Stop();
            _pollTimer.Dispose();

            try
            {
                if (_twain != null)
                {
                    if (_twain.IsDsmOpen)
                        _twain.Close();
                }
            }
            catch { }

            _mainForm?.Close();
            _contextMenu.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _hiddenWindow.Dispose();

            _cachedConnectedIcon?.Dispose();
            _cachedDisconnectedIcon?.Dispose();
        }

        base.Dispose(disposing);
    }
}
