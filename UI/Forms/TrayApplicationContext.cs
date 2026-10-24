using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;
using NTwain;
using NTwain.Data;

namespace SpeedScanManager;

internal enum ScannerStatus { Unknown, Connected, Disconnected }

/// <summary>Where a scan request came from.</summary>
internal enum ScanTrigger
{
    /// <summary>Tray menu: the user gets feedback such as "Scan läuft bereits".</summary>
    Menu,
    /// <summary>Scanner button (pipe "SCAN" / start with /scanbutton): ignored silently while busy, debounced.</summary>
    Button
}

/// <summary>
/// Invisible top-level window of the tray app. Provides the HWND required by the TWAIN DSM,
/// owns the post-scan dialogs and reports WM_DEVICECHANGE (sent to all top-level windows).
/// </summary>
internal sealed class TrayHiddenWindow : Form
{
    private const int WM_DEVICECHANGE = 0x0219;

    /// <summary>A device was added/removed (raised on the UI thread, may fire in bursts).</summary>
    public event EventHandler? DeviceChanged;

    public TrayHiddenWindow()
    {
        Text = "SpeedScan Manager";
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-32000, -32000);
        Size = new Size(1, 1);
        WindowState = FormWindowState.Minimized;
        Opacity = 0;
    }

    protected override void WndProc(ref System.Windows.Forms.Message m)
    {
        if (m.Msg == WM_DEVICECHANGE)
        {
            try { DeviceChanged?.Invoke(this, EventArgs.Empty); }
            catch (Exception ex) { DiagLog.Write($"DeviceChanged handler failed: {ex.Message}"); }
        }
        base.WndProc(ref m);
    }
}

/// <summary>
/// ApplicationContext that runs the app as a tray-only application.
///
/// Scanner state: <see cref="ScannerStateService"/> caches the state and only opens the
/// TWAIN source when the set of imaging devices changed. The tray asks it every
/// <see cref="PollIntervalMs"/> (cheap) and after WM_DEVICECHANGE (debounced); the
/// context menu only reads <see cref="ScannerStateService.LastState"/>.
///
/// Scan flow (one at a time, <see cref="_busy"/> covers the whole flow including all
/// post-scan dialogs): acquire on a worker thread (<see cref="ScanPipeline.Scan"/>) →
/// optional "Weitere Dokumente scannen?" → process and save on a worker thread
/// (<see cref="ScanOutputProcessor.Process"/>) → Quick-Menü or the configured application.
/// Dialogs run on the UI thread; problems are shown as balloon (info/warnings) or
/// message box (errors).
/// </summary>
internal class TrayApplicationContext : ApplicationContext
{
    private const int PollIntervalMs = 10000;
    private const int DeviceChangeDebounceMs = 1500;
    private const int DisconnectConfirmThreshold = 2;
    private const int ExitCancelTimeoutMs = 8000;
    private static readonly TimeSpan ButtonDebounce = TimeSpan.FromSeconds(1.5);
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "SpeedScanManager";
    private const string AppTitle = "SpeedScan Manager";

    private readonly NotifyIcon _notifyIcon;
    private ContextMenuStrip _contextMenu = null!;
    private ToolStripMenuItem _miScan = null!;
    private ToolStripMenuItem _miCancelScan = null!;
    private ToolStripMenuItem _miSelectScanner = null!;
    private ToolStripMenuItem _miScanKeySettings = null!;
    private ToolStripMenuItem _miProfileManagement = null!;
    private ToolStripMenuItem _miEmailSettings = null!;
    private ToolStripMenuItem _miPrintSettings = null!;
    private ToolStripMenuItem _miScanResult = null!;
    private ToolStripMenuItem _miHelp = null!;
    private ToolStripMenuItem _miExit = null!;

    private readonly System.Windows.Forms.Timer _pollTimer;
    private readonly System.Windows.Forms.Timer _deviceChangeTimer;
    private readonly System.Windows.Forms.Timer _progressTimer;
    private readonly System.Windows.Forms.Timer _leftClickTimer;
    private readonly TrayHiddenWindow _hiddenWindow;

    private TwainSession? _twain;
    private WindowsFormsMessageLoopHook? _msgLoop;
    private ScannerStateService? _scannerStateService;
    private bool _twainInitialized;
    private bool _initialized;
    private bool _disposed;

    private MainForm? _mainForm;
    private HelpForm? _helpForm;
    private readonly ScanSettings _settings;
    private readonly ProfileManager _profileManager = new();
    private readonly AppSettings _appSettings;

    // Scan flow state (UI thread only)
    private bool _busy;
    private ScanPipeline? _activePipeline;
    private bool _batchRunning;
    private int _pagesInEarlierBatches;
    private bool _cancelRequestedByUser;
    private bool _exitRequested;
    private bool _pendingButtonScan;
    private DateTime _lastButtonScanUtc = DateTime.MinValue;
    private List<string> _lastScanFiles = new();

    // Scanner connection state machine
    private ScannerStatus _scannerStatus = ScannerStatus.Unknown;
    private string _currentScannerName = "";
    private int _pendingDisconnectCount;
    private bool _disconnectBalloonShown;
    private bool _isUpdatingConnection;

    // Cached icons (avoid recreating GDI handles)
    private Icon? _cachedConnectedIcon;
    private Icon? _cachedDisconnectedIcon;
    private Icon? _cachedScanningIcon;

    private static readonly MethodInfo? ShowContextMenuMethod =
        typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);

    public TrayApplicationContext()
    {
        // AppSettings.ScanSettings is the single source of truth for the current scan
        // settings (old files are migrated on first load).
        _appSettings = AppSettings.Load();
        _settings = _appSettings.EnsureScanSettings(_profileManager);

        _hiddenWindow = new TrayHiddenWindow();
        _hiddenWindow.DeviceChanged += OnDeviceChanged;
        // Create the handle now so commands from the pipe server can be marshalled
        // (BeginInvoke) even before the TWAIN initialisation ran.
        _ = _hiddenWindow.Handle;

        _notifyIcon = new NotifyIcon
        {
            Icon = GetDisconnectedIcon(),
            Visible = true
        };
        SetTrayText(AppTitle);

        BuildContextMenu();
        _notifyIcon.ContextMenuStrip = _contextMenu;
        _notifyIcon.MouseUp += OnTrayMouseUp;
        _notifyIcon.DoubleClick += OnTrayDoubleClick;

        _pollTimer = new System.Windows.Forms.Timer { Interval = PollIntervalMs };
        _pollTimer.Tick += OnPollTick;

        _deviceChangeTimer = new System.Windows.Forms.Timer { Interval = DeviceChangeDebounceMs };
        _deviceChangeTimer.Tick += OnDeviceChangeTimerTick;

        _progressTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _progressTimer.Tick += (_, _) => UpdateBusyText();

        _leftClickTimer = new System.Windows.Forms.Timer { Interval = Math.Max(200, SystemInformation.DoubleClickTime) };
        _leftClickTimer.Tick += (_, _) =>
        {
            _leftClickTimer.Stop();
            ShowTrayContextMenu();
        };

        // Scan button / second instance commands (raised on a thread-pool thread).
        ScanButtonPipeServer.Shared.CommandReceived += OnPipeCommand;

        ApplyAutoStart();

        // Defer TWAIN init until the message loop is running (WindowsFormsMessageLoopHook requires it).
        Application.Idle += OnFirstIdle;
    }

    // ───────────────────────── Menu ─────────────────────────

    private void BuildContextMenu()
    {
        _contextMenu = new ContextMenuStrip
        {
            RenderMode = ToolStripRenderMode.System,
            Font = SystemFonts.MenuFont,
            ShowImageMargin = false
        };
        _contextMenu.Opening += OnContextMenuOpening;

        _miScan = new ToolStripMenuItem("Scan");
        _miCancelScan = new ToolStripMenuItem("Scan abbrechen") { Visible = false };
        _miSelectScanner = new ToolStripMenuItem("Scanner auswählen...");
        _miScanKeySettings = new ToolStripMenuItem("Einstellungen der SCAN Taste...");
        _miProfileManagement = new ToolStripMenuItem("Profilverwaltung...");
        _miEmailSettings = new ToolStripMenuItem("E-Mail-Einstellungen...");
        _miPrintSettings = new ToolStripMenuItem("Druckeinstellungen...");
        _miScanResult = new ToolStripMenuItem("Scan-Ergebnis anzeigen");
        _miHelp = new ToolStripMenuItem("Hilfe");
        _miExit = new ToolStripMenuItem("Beenden");

        var miHelpTopics = new ToolStripMenuItem("Hilfethemen");
        var miVersionInfo = new ToolStripMenuItem("Versionsinformationen");

        _miScan.Click += (_, _) => StartScan(ScanTrigger.Menu);
        _miCancelScan.Click += (_, _) => CancelRunningScan();
        _miSelectScanner.Click += (_, _) => ShowScannerSelectionDialog();
        _miScanKeySettings.Click += (_, _) => ShowMainForm();
        _miProfileManagement.Click += (_, _) => OpenProfileManagement();
        _miEmailSettings.Click += (_, _) => ShowEmailSettings();
        _miPrintSettings.Click += (_, _) => ShowPrintSettings();
        _miScanResult.Click += (_, _) => OpenScanResult();
        miHelpTopics.Click += (_, _) => ShowHelpDialog();
        miVersionInfo.Click += (_, _) => ShowVersionInfo();
        _miExit.Click += (_, _) => ExitApplication();

        _miHelp.DropDownItems.AddRange(new ToolStripItem[] { miHelpTopics, miVersionInfo });

        _contextMenu.Items.AddRange(new ToolStripItem[]
        {
            _miScan,
            _miCancelScan,
            new ToolStripSeparator(),
            _miSelectScanner,
            _miScanKeySettings,
            _miProfileManagement,
            _miEmailSettings,
            _miPrintSettings,
            new ToolStripSeparator(),
            _miScanResult,
            new ToolStripSeparator(),
            _miHelp,
            _miExit
        });
    }

    private void OnContextMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Never open the TWAIN source here: only the cached state is used.
        try { UpdateMenuItems(); }
        catch (Exception ex) { LogDiag($"OnContextMenuOpening exception: {ex.Message}"); }
    }

    private void UpdateMenuItems()
    {
        bool scannerUsable = _twainInitialized && _scannerStatus != ScannerStatus.Disconnected;
        _miScan.Enabled = !_busy && scannerUsable;
        _miCancelScan.Visible = _busy && _activePipeline != null;
        _miSelectScanner.Enabled = !_busy && _twainInitialized;
        _miScanResult.Enabled = _lastScanFiles.Count > 0; // no File.Exists here: a lost network share would block the menu
    }

    private void OnTrayMouseUp(object? sender, MouseEventArgs e)
    {
        // Left click opens the menu (like ScanSnap); wait for a possible double click first.
        if (e.Button == MouseButtons.Left)
        {
            _leftClickTimer.Stop();
            _leftClickTimer.Start();
        }
    }

    private void OnTrayDoubleClick(object? sender, EventArgs e)
    {
        _leftClickTimer.Stop();
        ShowMainForm();
    }

    private void ShowTrayContextMenu()
    {
        try
        {
            // NotifyIcon's own (non-public) routine positions the menu at the tray and
            // handles the foreground window correctly.
            if (ShowContextMenuMethod != null)
                ShowContextMenuMethod.Invoke(_notifyIcon, null);
            else
                _contextMenu.Show(Cursor.Position);
        }
        catch (Exception ex)
        {
            LogDiag($"ShowTrayContextMenu failed: {ex.Message}");
            _contextMenu.Show(Cursor.Position);
        }
    }

    // ───────────────────────── Commands from the scan button / second instance ─────────────────────────

    private void OnPipeCommand(string command)
    {
        // Thread-pool thread → UI thread
        try
        {
            if (_disposed || _hiddenWindow.IsDisposed) return;
            _hiddenWindow.BeginInvoke(() => HandleCommand(command));
        }
        catch (Exception ex)
        {
            LogDiag($"OnPipeCommand '{command}' could not be dispatched: {ex.Message}");
        }
    }

    private void HandleCommand(string command)
    {
        LogDiag($"Pipe command: {command}");
        if (command == ScanButtonPipeServer.CommandScan)
        {
            if (!_initialized)
            {
                _pendingButtonScan = true;
                return;
            }
            StartScan(ScanTrigger.Button);
        }
        else if (command == ScanButtonPipeServer.CommandShow)
        {
            ShowMainForm();
        }
    }

    // ───────────────────────── Scan flow ─────────────────────────

    private void CancelRunningScan()
    {
        if (_activePipeline == null) return;
        LogDiag("Tray: user requested scan cancel");
        _cancelRequestedByUser = true;
        _activePipeline.RequestCancel();
    }

    private async void StartScan(ScanTrigger trigger)
    {
        if (_busy)
        {
            if (trigger == ScanTrigger.Button)
                LogDiag("StartScan: scan button ignored, a scan is already running");
            else
                TrayMessageBox.Info(_hiddenWindow, "Ein Scanvorgang läuft bereits.");
            return;
        }

        if (trigger == ScanTrigger.Button)
        {
            var now = DateTime.UtcNow;
            if (now - _lastButtonScanUtc < ButtonDebounce)
            {
                LogDiag("StartScan: scan button ignored (debounce)");
                return;
            }
            _lastButtonScanUtc = now;
        }

        if (!_twainInitialized || _twain == null || _msgLoop == null)
        {
            TrayMessageBox.Error(_hiddenWindow, "TWAIN ist nicht initialisiert. Es ist kein Scanner verfügbar.");
            return;
        }

        _busy = true;
        _cancelRequestedByUser = false;
        UpdateTrayVisuals();
        ScanResult? scan = null;
        try
        {
            // Snapshot for the whole flow: settings applied meanwhile don't change this scan.
            var settings = _settings.Clone();
            bool quickMenu = _appSettings.QuickMenuEnabled;

            scan = await AcquireAsync(settings);
            if (scan != null && !_exitRequested)
                await PostScanAsync(scan, settings, quickMenu);
        }
        catch (Exception ex)
        {
            LogDiag($"Scan flow failed: {ex}");
            TrayMessageBox.Error(_hiddenWindow, $"Beim Scannen ist ein Fehler aufgetreten:\n\n{ex.Message}");
        }
        finally
        {
            scan?.DisposeImages();
            _activePipeline = null;
            _busy = false;
            if (_exitRequested)
                DoExit();
            else
                UpdateTrayVisuals();
        }
    }

    /// <summary>
    /// Acquires one or more batches ("Weitere Dokumente scannen?"). Returns null when
    /// nothing is to be processed; all problems are already reported to the user.
    /// </summary>
    private async Task<ScanResult?> AcquireAsync(ScanSettings settings)
    {
        IntPtr handle = _hiddenWindow.Handle; // read on the UI thread
        ScanResult? total = null;
        bool errorShown = false;

        using (var pipeline = new ScanPipeline(_twain!, _msgLoop!, _hiddenWindow, handle, settings, _appSettings.SelectedSourceName))
        {
            _activePipeline = pipeline;
            _pagesInEarlierBatches = 0;
            _progressTimer.Start();
            try
            {
                while (true)
                {
                    _batchRunning = true;
                    UpdateBusyText();
                    ScanResult batch;
                    try
                    {
                        batch = await Task.Run(() => pipeline.Scan(ScanSide.Automatic));
                    }
                    finally
                    {
                        _batchRunning = false;
                    }

                    if (batch.ErrorMessage != null)
                    {
                        batch.DisposeImages();
                        TrayMessageBox.Error(_hiddenWindow, batch.ErrorMessage);
                        errorShown = true;
                        break; // earlier batches (if any) are still processed
                    }

                    if (batch.WasCancelled)
                    {
                        batch.DisposeImages();
                        if (_cancelRequestedByUser || _exitRequested)
                        {
                            total?.DisposeImages();
                            ShowBalloon("Scan abgebrochen", "Der Scanvorgang wurde abgebrochen.", ToolTipIcon.Info);
                            return null;
                        }
                        ShowBalloon("Scan verworfen", batch.MultiFeedDetected
                            ? "Mehrfacheinzug: Die Seiten wurden verworfen. Bitte legen Sie das Dokument erneut ein."
                            : "Der Scanvorgang wurde abgebrochen.", ToolTipIcon.Info);
                        if (total == null)
                            return null;
                    }
                    else
                    {
                        ReportScanProblems(batch);
                        if (total == null) total = batch;
                        else total.Append(batch);
                        _pagesInEarlierBatches = total.Images.Count;
                    }

                    if (!settings.ContinueScanning || _exitRequested)
                        break;
                    if (!AskContinueScanning(total?.Images.Count ?? 0))
                        break;
                }
            }
            catch
            {
                total?.DisposeImages();
                throw;
            }
            finally
            {
                _progressTimer.Stop();
                _activePipeline = null;
                UpdateTrayVisuals();
            }
        }

        if (total == null || total.Images.Count == 0)
        {
            if (!errorShown && !_exitRequested)
            {
                TrayMessageBox.Warning(_hiddenWindow, total?.TimedOut == true
                    ? "Der Scanner hat nicht reagiert. Es wurden keine Seiten gescannt."
                    : "Es wurden keine Seiten gescannt.\n\nBitte legen Sie das Dokument in den Scanner ein und versuchen Sie es erneut.");
            }
            total?.DisposeImages();
            return null;
        }

        // The scanner answered: it is connected.
        if (_scannerStatus != ScannerStatus.Connected)
        {
            _scannerStatus = ScannerStatus.Connected;
            _pendingDisconnectCount = 0;
            _disconnectBalloonShown = false;
            if (!string.IsNullOrEmpty(total.SourceName))
                _currentScannerName = total.SourceName;
        }
        return total;
    }

    private bool AskContinueScanning(int pagesSoFar)
    {
        using var dlg = new ContinueScanDialog(pagesSoFar);
        return dlg.ShowDialog(_hiddenWindow) == DialogResult.OK;
    }

    private void ReportScanProblems(ScanResult batch)
    {
        var messages = new List<string>(batch.Warnings);
        if (batch.FailedTransfers > 0)
        {
            messages.Add(batch.FailedTransfers == 1
                ? "1 Seite konnte nicht übertragen werden."
                : $"{batch.FailedTransfers} Seiten konnten nicht übertragen werden.");
            messages.AddRange(batch.FailedTransferMessages.Distinct().Take(2));
        }
        foreach (var m in messages) LogDiag($"Scan warning: {m}");
        if (messages.Count > 0)
            ShowBalloon("Hinweis zum Scan", string.Join("\n", messages.Distinct()), ToolTipIcon.Warning);
    }

    private sealed record ProcessOutcome(ScanOutputResult Output, string? FallbackMessage);

    private async Task PostScanAsync(ScanResult scan, ScanSettings settings, bool quickMenu)
    {
        var appType = settings.ApplicationType;
        bool needPages = quickMenu
            || settings.RenameAfterScan
            || appType is not (ApplicationType.ScanToEmail or ApplicationType.EditWithPdf or ApplicationType.Custom);
        string folder = string.IsNullOrWhiteSpace(settings.FolderPath) ? ScanSettings.DefaultFolderPath : settings.FolderPath;

        var options = ScanOutputOptions.FromScanResult(scan, settings);
        options.ProduceProcessedPages = needPages;

        SetTrayText($"{AppTitle}\nVerarbeitet {scan.Images.Count} Seite(n)…");
        ProcessOutcome outcome;
        try
        {
            outcome = await Task.Run(() => ProcessWithFallback(scan.Images, folder, settings, options));
        }
        catch (Exception ex)
        {
            LogDiag($"Process failed: {ex}");
            TrayMessageBox.Error(_hiddenWindow, $"Der Scan konnte nicht gespeichert werden:\n\n{ex.Message}");
            return;
        }
        finally
        {
            // The inputs are no longer needed — free the memory before the dialogs.
            scan.DisposeImages();
            UpdateBusyText();
        }

        using var output = outcome.Output;
        if (outcome.FallbackMessage != null)
            TrayMessageBox.Warning(_hiddenWindow, outcome.FallbackMessage);

        foreach (var w in output.Warnings) LogDiag($"Output warning: {w}");
        if (output.Warnings.Count > 0)
            ShowBalloon("Hinweis", string.Join("\n", output.Warnings.Distinct()), ToolTipIcon.Warning);

        if (output.AllPagesBlank)
        {
            TrayMessageBox.Info(_hiddenWindow, "Alle gescannten Seiten wurden als leer erkannt und entfernt. Es wurde keine Datei gespeichert.");
            return;
        }
        if (output.Files.Count == 0)
        {
            TrayMessageBox.Error(_hiddenWindow, "Es wurde keine Datei erstellt.");
            return;
        }

        var files = new List<string>(output.Files);
        _lastScanFiles = files;
        LogDiag($"Scan saved: {files.Count} file(s), {output.PageCount} page(s), {output.BlankPagesRemoved} blank removed");

        if (quickMenu)
        {
            await QuickMenuAsync(output, files, settings);
            return;
        }

        if (appType == ApplicationType.ScanToFolder)
        {
            var saved = ShowSaveDialog(output, files, settings);
            if (saved != null)
                ShowSavedBalloon(saved);
            return;
        }

        if (settings.RenameAfterScan)
        {
            var renamed = ShowSaveDialog(output, files, settings);
            if (renamed == null)
                return;
            files = renamed;
        }

        await RunApplicationAsync(appType, output, files, settings);
    }

    /// <summary>Worker thread: Process() into the folder, or into Documents\SpeedScanManager if that fails.</summary>
    private static ProcessOutcome ProcessWithFallback(IReadOnlyList<Bitmap> images, string folder, ScanSettings settings, ScanOutputOptions options)
    {
        var processor = new ScanOutputProcessor(settings);
        try
        {
            return new ProcessOutcome(processor.Process(images, folder, options), null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException
                                       or NotSupportedException or ArgumentException)
        {
            var fallback = ScanSettings.DefaultFolderPath;
            if (SamePath(folder, fallback))
                throw;

            DiagLog.Write($"Process: saving to '{folder}' failed ({ex.Message}), falling back to '{fallback}'");
            var output = processor.Process(images, fallback, options);
            return new ProcessOutcome(output,
                $"Der Scan konnte nicht im Zielordner gespeichert werden:\n{folder}\n\n{ex.Message}\n\n" +
                $"Er wurde stattdessen hier gespeichert:\n{fallback}");
        }
    }

    private static bool SamePath(string a, string b)
    {
        try
        {
            return string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Quick-Menü. Closing it (Abbrechen / X) keeps the already saved file in the target
    /// folder (like ScanSnap); nothing is deleted without an explicit "Scan verwerfen?".
    /// </summary>
    private async Task QuickMenuAsync(ScanOutputResult output, List<string> files, ScanSettings settings)
    {
        PostScanMediaDialog.MediaAction action;
        using (var dlg = new PostScanMediaDialog(output.PageCount))
        {
            if (dlg.ShowDialog(_hiddenWindow) != DialogResult.OK)
            {
                ShowSavedBalloon(files);
                return;
            }
            action = dlg.SelectedMediaAction;
        }

        var appType = MapMediaAction(action);
        if (appType == ApplicationType.ScanToFolder)
        {
            var saved = ShowSaveDialog(output, files, settings);
            if (saved != null)
                ShowSavedBalloon(saved);
            return;
        }

        await RunApplicationAsync(appType, output, files, settings);
    }

    private static ApplicationType MapMediaAction(PostScanMediaDialog.MediaAction action) => action switch
    {
        PostScanMediaDialog.MediaAction.ScanToEmail => ApplicationType.ScanToEmail,
        PostScanMediaDialog.MediaAction.ScanToPrint => ApplicationType.ScanToPrint,
        PostScanMediaDialog.MediaAction.ScanToWord => ApplicationType.ScanToWord,
        PostScanMediaDialog.MediaAction.ScanToExcel => ApplicationType.ScanToExcel,
        PostScanMediaDialog.MediaAction.ScanToPowerPoint => ApplicationType.ScanToPowerPoint,
        PostScanMediaDialog.MediaAction.ScanPictureFolder => ApplicationType.ScanPictureFolder,
        PostScanMediaDialog.MediaAction.EditWithPdf => ApplicationType.EditWithPdf,
        _ => ApplicationType.ScanToFolder
    };

    /// <summary>
    /// "In Ordner speichern" / rename dialog for files that are already saved. OK moves or
    /// renames them (never overwriting); cancel asks "Scan verwerfen?" and deletes them on
    /// "Ja". Returns the final file list, or null if the scan was discarded.
    /// </summary>
    private List<string>? ShowSaveDialog(ScanOutputResult output, List<string> files, ScanSettings settings)
    {
        string currentFolder = Path.GetDirectoryName(files[0]) ?? ScanSettings.DefaultFolderPath;
        string generatedName = Path.GetFileNameWithoutExtension(files[0]);

        string targetFolder, title;
        using (var dlg = new PostScanSaveDialog(output.ProcessedPages, settings, generatedName, currentFolder))
        {
            if (dlg.ShowDialog(_hiddenWindow) != DialogResult.OK)
            {
                DeleteFiles(files);
                _lastScanFiles = _lastScanFiles.Except(files, StringComparer.OrdinalIgnoreCase).ToList();
                ShowBalloon("Scan verworfen", "Der Scan wurde gelöscht.", ToolTipIcon.Info);
                return null;
            }
            targetFolder = dlg.SelectedFolderPath;
            title = dlg.SelectedTitle;
        }

        bool sameName = string.Equals(title, generatedName, StringComparison.OrdinalIgnoreCase);
        if (sameName && SamePath(targetFolder, currentFolder))
            return files;

        var result = new List<string>();
        try
        {
            Directory.CreateDirectory(targetFolder);
        }
        catch (Exception ex)
        {
            TrayMessageBox.Error(_hiddenWindow,
                $"Der Zielordner konnte nicht erstellt werden:\n{targetFolder}\n\n{ex.Message}\n\nDer Scan bleibt hier gespeichert:\n{currentFolder}");
            return files;
        }

        var failures = new List<string>();
        for (int i = 0; i < files.Count; i++)
        {
            var src = files[i];
            if (!File.Exists(src)) continue;

            var ext = Path.GetExtension(src);
            string newName = sameName
                ? Path.GetFileName(src)
                : files.Count == 1 ? title + ext : $"{title}_{i + 1:D3}{ext}";
            try
            {
                var dest = ScanOutputProcessor.MakeUniquePath(targetFolder, newName);
                File.Move(src, dest);
                result.Add(dest);
            }
            catch (Exception ex)
            {
                LogDiag($"Move '{src}' failed: {ex.Message}");
                failures.Add($"{Path.GetFileName(src)}: {ex.Message}");
                result.Add(src);
            }
        }

        if (failures.Count > 0)
        {
            TrayMessageBox.Error(_hiddenWindow,
                $"Folgende Dateien konnten nicht nach \"{targetFolder}\" verschoben werden und bleiben in \"{currentFolder}\":\n\n" +
                string.Join("\n", failures.Take(5)));
        }

        _lastScanFiles = result;
        return result;
    }

    private static void DeleteFiles(IEnumerable<string> files)
    {
        foreach (var f in files)
        {
            try { if (File.Exists(f)) File.Delete(f); }
            catch (Exception ex) { DiagLog.Write($"Delete '{f}' failed: {ex.Message}"); }
        }
    }

    private void ShowSavedBalloon(List<string> files)
    {
        var folder = files.Count > 0 ? Path.GetDirectoryName(files[0]) : null;
        ShowBalloon("Scan gespeichert",
            files.Count == 1
                ? $"{Path.GetFileName(files[0])}\nin {folder}"
                : $"{files.Count} Dateien in {folder}",
            ToolTipIcon.Info);
    }

    // ───────────────────────── Applications ─────────────────────────

    private async Task RunApplicationAsync(ApplicationType appType, ScanOutputResult output, List<string> files, ScanSettings settings)
    {
        int jpegQuality = ScanOutputProcessor.JpegQualityFor(settings.CompressionRate);
        string targetFolder = Path.GetDirectoryName(files[0]) ?? ScanSettings.DefaultFolderPath;
        string baseName = Path.GetFileNameWithoutExtension(files[0]);

        switch (appType)
        {
            case ApplicationType.ScanToFolder:
                ShowSavedBalloon(files);
                break;

            case ApplicationType.ScanToEmail:
                // Not awaited: the compose window may stay open for a long time; the scan
                // flow (busy state) ends now, errors are reported when MAPI returns.
                SendMail(files, settings);
                break;

            case ApplicationType.ScanToPrint:
                await PrintAsync(output.ProcessedPages, settings.PrinterName);
                break;

            case ApplicationType.ScanToWord:
                await CreateOfficeFileAsync("Word", ".docx", targetFolder, baseName,
                    path => OfficeExportHelper.CreateWord(output.ProcessedPages, path, jpegQuality));
                break;

            case ApplicationType.ScanToExcel:
                await CreateOfficeFileAsync("Excel", ".xlsx", targetFolder, baseName,
                    path => OfficeExportHelper.CreateExcel(output.ProcessedPages, path, jpegQuality));
                break;

            case ApplicationType.ScanToPowerPoint:
                await CreateOfficeFileAsync("PowerPoint", ".pptx", targetFolder, baseName,
                    path => OfficeExportHelper.CreatePowerPoint(output.ProcessedPages, path, jpegQuality));
                break;

            case ApplicationType.ScanPictureFolder:
                await SaveToPictureFolderAsync(output.ProcessedPages, baseName, jpegQuality);
                break;

            case ApplicationType.EditWithPdf:
                OpenFiles(files);
                break;

            case ApplicationType.Custom:
                LaunchCustomApplication(settings, files);
                break;
        }
    }

    private async void SendMail(List<string> files, ScanSettings settings)
    {
        try
        {
            // MAPI runs on its own STA thread; no owner window is passed because a parent
            // window owned by another thread would attach the input queues of both threads.
            var result = await MailHelper.SendMailAsync(IntPtr.Zero, settings.EmailSubjectTemplate, settings.EmailRecipient, files);
            if (result.Success || result.UserAborted)
                return;

            TrayMessageBox.Error(_hiddenWindow,
                "Das E-Mail-Programm konnte nicht mit der Datei als Anhang geöffnet werden.\n\n" +
                $"{result.ErrorMessage}\n\nDer Ordner mit der gespeicherten Datei wird geöffnet, damit Sie sie manuell anhängen können.");
            RevealInExplorer(files[0]);
        }
        catch (Exception ex)
        {
            LogDiag($"SendMail failed: {ex}");
            TrayMessageBox.Error(_hiddenWindow, $"E-Mail-Fehler: {ex.Message}");
        }
    }

    private async Task PrintAsync(IReadOnlyList<Bitmap> pages, string configuredPrinter)
    {
        if (pages.Count == 0)
        {
            TrayMessageBox.Error(_hiddenWindow, "Es gibt keine Seiten zum Drucken.");
            return;
        }

        string? printer;
        bool missing;
        try
        {
            printer = PrintHelper.ResolvePrinter(configuredPrinter, out missing);
        }
        catch (Exception ex)
        {
            TrayMessageBox.Error(_hiddenWindow, $"Die Drucker konnten nicht abgefragt werden:\n{ex.Message}");
            return;
        }

        if (printer == null)
        {
            TrayMessageBox.Error(_hiddenWindow, "Es ist kein Drucker installiert. Der Scan wurde gespeichert, aber nicht gedruckt.");
            return;
        }
        if (missing && !TrayMessageBox.AskYesNo(_hiddenWindow,
                $"Der Drucker \"{configuredPrinter}\" wurde nicht gefunden.\n\nAuf dem Standarddrucker drucken?"))
            return;

        try
        {
            SetTrayText($"{AppTitle}\nDruckt…");
            await Task.Run(() => PrintHelper.PrintPages(pages, printer, "SpeedScan Manager – Gescanntes Dokument"));
            ShowBalloon("Drucken", "Das Dokument wurde an den Drucker gesendet.", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            LogDiag($"Print failed: {ex}");
            TrayMessageBox.Error(_hiddenWindow, $"Das Drucken ist fehlgeschlagen:\n\n{ex.Message}");
        }
    }

    private async Task CreateOfficeFileAsync(string appName, string extension, string folder, string baseName, Action<string> create)
    {
        if (string.IsNullOrEmpty(baseName)) baseName = "Scan";
        string path = ScanOutputProcessor.MakeUniquePath(folder, baseName + extension);
        try
        {
            SetTrayText($"{AppTitle}\nErstellt {appName}-Datei…");
            await Task.Run(() => create(path));
        }
        catch (Exception ex)
        {
            LogDiag($"Create {appName} file failed: {ex}");
            try { if (File.Exists(path)) File.Delete(path); } catch { }
            TrayMessageBox.Error(_hiddenWindow, $"Die {appName}-Datei konnte nicht erstellt werden:\n\n{ex.Message}");
            return;
        }

        _lastScanFiles = _lastScanFiles.Append(path).ToList();
        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LogDiag($"Open {path} failed: {ex.Message}");
            TrayMessageBox.Error(_hiddenWindow,
                $"Die Datei wurde erstellt, konnte aber nicht geöffnet werden (ist {appName} installiert?):\n{path}\n\n{ex.Message}");
            RevealInExplorer(path);
        }
    }

    private async Task SaveToPictureFolderAsync(IReadOnlyList<Bitmap> pages, string baseName, int jpegQuality)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "SpeedScanManager");
        List<string> images;
        try
        {
            images = await Task.Run(() => OfficeExportHelper.SaveImages(pages, folder, string.IsNullOrEmpty(baseName) ? "Scan" : baseName, jpegQuality));
        }
        catch (Exception ex)
        {
            LogDiag($"SaveToPictureFolder failed: {ex}");
            TrayMessageBox.Error(_hiddenWindow, $"Die Bilder konnten nicht im Bilderordner gespeichert werden:\n{folder}\n\n{ex.Message}");
            return;
        }

        if (images.Count > 0)
        {
            _lastScanFiles = _lastScanFiles.Concat(images).ToList();
            RevealInExplorer(images[0]);
        }
    }

    private void OpenFiles(List<string> files)
    {
        foreach (var f in files.Where(File.Exists))
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = f, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                LogDiag($"Open {f} failed: {ex.Message}");
                TrayMessageBox.Error(_hiddenWindow, $"Die Datei konnte nicht geöffnet werden:\n{f}\n\n{ex.Message}");
                RevealInExplorer(f);
                return;
            }
        }
    }

    private void LaunchCustomApplication(ScanSettings settings, List<string> files)
    {
        var exe = settings.CustomApplicationPath?.Trim().Trim('"') ?? "";
        var name = string.IsNullOrWhiteSpace(settings.CustomApplicationName) ? Path.GetFileName(exe) : settings.CustomApplicationName;
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
        {
            TrayMessageBox.Error(_hiddenWindow,
                $"Das Programm \"{name}\" wurde nicht gefunden:\n{exe}\n\nBitte prüfen Sie die Anwendung in den Einstellungen. Der Scan wurde gespeichert.");
            RevealInExplorer(files[0]);
            return;
        }

        var args = string.Join(" ", new[] { settings.CustomApplicationArguments?.Trim() ?? "" }
            .Concat(files.Select(f => $"\"{f}\""))
            .Where(a => a.Length > 0));
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? ""
            });
        }
        catch (Exception ex)
        {
            LogDiag($"Custom application failed: {ex}");
            TrayMessageBox.Error(_hiddenWindow, $"Das Programm \"{name}\" konnte nicht gestartet werden:\n\n{ex.Message}");
        }
    }

    private static void RevealInExplorer(string file)
    {
        try
        {
            if (File.Exists(file))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file}\"") { UseShellExecute = true });
                return;
            }
            var folder = Path.GetDirectoryName(file);
            if (folder != null && Directory.Exists(folder))
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            DiagLog.Write($"RevealInExplorer failed: {ex.Message}");
        }
    }

    private void OpenScanResult()
    {
        var file = _lastScanFiles.FirstOrDefault(File.Exists);
        if (file != null)
        {
            RevealInExplorer(file);
            return;
        }

        var folder = string.IsNullOrWhiteSpace(_settings.FolderPath) ? ScanSettings.DefaultFolderPath : _settings.FolderPath;
        if (Directory.Exists(folder))
        {
            try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true }); }
            catch (Exception ex) { LogDiag($"Open folder failed: {ex.Message}"); }
        }
        else
            TrayMessageBox.Info(_hiddenWindow, $"Der Ordner \"{folder}\" existiert nicht.");
    }

    // ───────────────────────── Settings windows ─────────────────────────

    private void ShowHelpDialog()
    {
        if (_helpForm != null && !_helpForm.IsDisposed)
        {
            WindowActivation.BringToFront(_helpForm);
            return;
        }

        _helpForm = new HelpForm("root");
        _helpForm.FormClosed += (_, _) => _helpForm = null;
        _helpForm.Show(_hiddenWindow);
        WindowActivation.BringToFront(_helpForm);
    }

    private void ShowVersionInfo()
    {
        using var dlg = new VersionInfoDialog(_scannerStateService?.LastState);
        dlg.StartPosition = FormStartPosition.CenterScreen;
        dlg.TopMost = true;
        dlg.ShowDialog(_hiddenWindow);
    }

    private void OpenProfileManagement()
    {
        using var dlg = new ProfileManagementDialog(_profileManager);
        if (_mainForm == null || !_mainForm.Visible)
        {
            dlg.StartPosition = FormStartPosition.CenterScreen;
            dlg.TopMost = true;
        }
        dlg.ShowDialog(_mainForm != null && _mainForm.Visible ? _mainForm : _hiddenWindow);
        _mainForm?.RefreshProfileDropdown();
    }

    /// <summary>
    /// E-mail options are also reachable here because the Anwendung tab is disabled while the
    /// Quick-Menü is on. Changes go to the applied settings and to an open settings window's
    /// working copy, so applying that window later doesn't revert them.
    /// </summary>
    private void ShowEmailSettings()
    {
        using var dlg = new ScanToEmailOptionsDialog
        {
            Recipient = _settings.EmailRecipient,
            SubjectTemplate = _settings.EmailSubjectTemplate,
            StartPosition = FormStartPosition.CenterScreen,
            TopMost = true
        };
        if (dlg.ShowDialog(_hiddenWindow) != DialogResult.OK)
            return;

        foreach (var s in SettingsTargets())
        {
            s.EmailRecipient = dlg.Recipient;
            s.EmailSubjectTemplate = dlg.SubjectTemplate;
        }
        _appSettings.Save();
    }

    private void ShowPrintSettings()
    {
        using var dlg = new ScanToPrintOptionsDialog
        {
            SelectedPrinterName = _settings.PrinterName,
            StartPosition = FormStartPosition.CenterScreen,
            TopMost = true
        };
        if (dlg.ShowDialog(_hiddenWindow) != DialogResult.OK)
            return;

        foreach (var s in SettingsTargets())
            s.PrinterName = dlg.SelectedPrinterName;
        _appSettings.Save();
    }

    private IEnumerable<ScanSettings> SettingsTargets()
    {
        yield return _settings;
        if (_mainForm != null && !_mainForm.IsDisposed && !ReferenceEquals(_mainForm.Settings, _settings))
            yield return _mainForm.Settings;
    }

    private void ShowMainForm()
    {
        if (_mainForm == null || _mainForm.IsDisposed)
        {
            _mainForm = new MainForm(_settings, _profileManager, _appSettings);
            _mainForm.SettingsApplied += (_, _) => OnSettingsApplied();
            _mainForm.CustomApplicationsChanged += (_, _) => _appSettings.Save();
            _mainForm.FormClosed += (_, _) => _mainForm = null;
        }
        if (!_mainForm.Visible)
        {
            _mainForm.Show();
            UpdateMainFormCapabilities();
        }
        WindowActivation.BringToFront(_mainForm);
    }

    /// <summary>OK/Übernehmen in the settings window: _settings already holds the applied values.</summary>
    private void OnSettingsApplied()
    {
        _appSettings.Save();
        ApplyAutoStart();
    }

    private void UpdateMainFormCapabilities()
    {
        if (_mainForm == null || _mainForm.IsDisposed || _mainForm.PaperTab == null)
            return;
        // Only restrict the multi-feed options when the connected scanner reported its modes.
        // Unknown (offline scanner, driver without the capability) keeps all options: the
        // paper tab resets unsupported selections, which would silently change the setting.
        var state = _scannerStateService?.LastState;
        if (_scannerStatus == ScannerStatus.Connected && state != null && state.IsScannerConnected && state.MultiFeedCapabilityKnown)
            _mainForm.PaperTab.UpdateMultiFeedCapabilities(state.SupportsUltrasonicDetection, state.SupportsLengthDetection);
        else
            _mainForm.PaperTab.UpdateMultiFeedCapabilities(true, true);
    }

    /// <summary>Writes or removes HKCU\...\Run "SpeedScanManager" according to <see cref="AppSettings.AutoStart"/>.</summary>
    private void ApplyAutoStart()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key == null) return;

            if (_appSettings.AutoStart)
            {
                var exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe) || !File.Exists(exe)
                    || !exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    return;
                var value = $"\"{exe}\"";
                if (!string.Equals(key.GetValue(RunValueName) as string, value, StringComparison.OrdinalIgnoreCase))
                {
                    key.SetValue(RunValueName, value, RegistryValueKind.String);
                    LogDiag($"AutoStart: registered {value}");
                }
            }
            else if (key.GetValue(RunValueName) != null)
            {
                key.DeleteValue(RunValueName, false);
                LogDiag("AutoStart: removed");
            }
        }
        catch (Exception ex)
        {
            LogDiag($"ApplyAutoStart failed: {ex.Message}");
        }
    }

    private void ShowScannerSelectionDialog()
    {
        if (_busy)
        {
            TrayMessageBox.Info(_hiddenWindow, "Ein Scanvorgang läuft bereits.");
            return;
        }
        if (_twain == null || !_twain.IsDsmOpen)
        {
            TrayMessageBox.Warning(_hiddenWindow, "TWAIN ist nicht initialisiert. Die Scannerauswahl ist nicht verfügbar.");
            return;
        }

        string? selected;
        using (var dlg = new ScannerSelectionDialog(_twain, _appSettings.SelectedSourceName))
        {
            dlg.TopMost = true;
            if (dlg.ShowDialog(_hiddenWindow) != DialogResult.OK)
                return;
            selected = dlg.SelectedSourceName;
        }

        _appSettings.SelectedSourceName = selected;
        _appSettings.Save();
        LogDiag($"Scanner selection changed to: {selected ?? "(auto)"}");

        if (_scannerStateService != null)
        {
            _scannerStateService.PreferredSourceName = selected;
            _scannerStateService.NotifyDeviceChange();
        }
        _scannerStatus = ScannerStatus.Unknown;
        _pendingDisconnectCount = 0;
        _disconnectBalloonShown = false;
        UpdateConnectionState(forceRefresh: true);
    }

    // ───────────────────────── Exit ─────────────────────────

    private void ExitApplication()
    {
        if (_busy)
        {
            if (_activePipeline != null)
            {
                if (!TrayMessageBox.AskYesNo(_hiddenWindow,
                        "Es läuft gerade ein Scanvorgang.\n\nScan abbrechen und SpeedScan Manager beenden?", defaultNo: true))
                    return;

                // The scan runs through the UI message loop: cancel, let the flow finish
                // (it calls DoExit), and force the exit if the driver doesn't respond.
                _exitRequested = true;
                _cancelRequestedByUser = true;
                _activePipeline.RequestCancel();
                var exitTimer = new System.Windows.Forms.Timer { Interval = ExitCancelTimeoutMs };
                exitTimer.Tick += (_, _) =>
                {
                    exitTimer.Stop();
                    exitTimer.Dispose();
                    LogDiag("Exit: scan did not stop in time, exiting anyway");
                    DoExit();
                };
                exitTimer.Start();
                return;
            }

            if (!TrayMessageBox.AskYesNo(_hiddenWindow,
                    "Der letzte Scan ist noch nicht abgeschlossen.\n\nSpeedScan Manager trotzdem beenden?", defaultNo: true))
                return;
        }

        DoExit();
    }

    private bool _exiting;

    private void DoExit()
    {
        if (_exiting) return;
        _exiting = true;
        _pollTimer.Stop();
        _notifyIcon.Visible = false;
        Application.Exit();
    }

    // ───────────────────────── TWAIN / scanner state ─────────────────────────

    private void InitializeTwain()
    {
        try
        {
            _hiddenWindow.Show();
            _hiddenWindow.Hide();

            var asmVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 1, 2, 0);
            var appId = TWIdentity.Create(
                DataGroups.Control | DataGroups.Image,
                asmVersion,
                "VeridonNetzwerk",
                "SpeedScan Manager",
                "SpeedScan Manager",
                "SpeedScan Manager TWAIN");

            _twain = new TwainSession(appId);
            _msgLoop = new WindowsFormsMessageLoopHook(_hiddenWindow.Handle);
            var rc = _twain.Open(_msgLoop);
            _twainInitialized = rc == ReturnCode.Success;

            LogDiag($"TWAIN init: DSM open rc={rc}, initialized={_twainInitialized}");

            if (_twainInitialized)
            {
                _scannerStateService = new ScannerStateService(_twain, _msgLoop, _appSettings.SelectedSourceName);
                try
                {
                    var sources = _twain.GetSources().ToList();
                    LogDiag($"TWAIN sources count={sources.Count}");
                    foreach (var s in sources)
                        LogDiag($"  Source: {s.Name} (mfr={s.Manufacturer}, family={s.ProductFamily})");
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

    private static void LogDiag(string msg) => DiagLog.Write($"[Tray] {msg}");

    private void OnFirstIdle(object? sender, EventArgs e)
    {
        Application.Idle -= OnFirstIdle;

        // Scan button registration: cheap registry check, elevation at most once (off the UI thread).
        WiaEventWatcher.EnsureScanButtonHandlerAsync().ContinueWith(
            t => LogDiag(t.IsFaulted
                ? $"EnsureScanButtonHandler failed: {t.Exception?.GetBaseException().Message}"
                : $"EnsureScanButtonHandler: registered={t.Result}"),
            TaskScheduler.Default);

        InitializeTwain();
        if (_twainInitialized)
        {
            _scannerStatus = ScannerStatus.Unknown;
            UpdateConnectionState();
        }
        else
        {
            _scannerStatus = ScannerStatus.Disconnected;
            ShowBalloon(AppTitle, "TWAIN konnte nicht initialisiert werden. Es kann nicht gescannt werden.", ToolTipIcon.Error);
        }
        UpdateTrayVisuals();
        _initialized = true;
        _pollTimer.Start();

        if (Program.ScanOnStartRequested || _pendingButtonScan)
        {
            Program.ScanOnStartRequested = false;
            _pendingButtonScan = false;
            LogDiag("Starting scan requested by the scan button at startup");
            StartScan(ScanTrigger.Button);
        }
    }

    private void OnPollTick(object? sender, EventArgs e)
    {
        try { UpdateConnectionState(); }
        catch (Exception ex) { LogDiag($"OnPollTick exception: {ex.Message}"); }
    }

    private void OnDeviceChanged(object? sender, EventArgs e)
    {
        _scannerStateService?.NotifyDeviceChange();
        // Device notifications come in bursts: requery once things settled.
        _deviceChangeTimer.Stop();
        _deviceChangeTimer.Start();
    }

    private void OnDeviceChangeTimerTick(object? sender, EventArgs e)
    {
        _deviceChangeTimer.Stop();
        LogDiag("WM_DEVICECHANGE: requerying scanner state");
        UpdateConnectionState();
    }

    private void UpdateConnectionState(bool forceRefresh = false)
    {
        // Never touch TWAIN while a scan uses the source (dialogs after the scan are fine).
        if (_activePipeline != null)
            return;
        if (_isUpdatingConnection) return;
        _isUpdatingConnection = true;

        try
        {
            if (!_twainInitialized || _scannerStateService == null)
                return;

            bool connected;
            string scannerName = "";
            try
            {
                var state = _scannerStateService.QueryState(_appSettings.SelectedSourceName, forceRefresh);
                connected = state.IsScannerConnected;
                scannerName = state.SourceName ?? "";
            }
            catch (Exception ex)
            {
                LogDiag($"UpdateConnectionState: QueryState failed: {ex.Message}");
                connected = false;
            }

            if (connected)
            {
                _pendingDisconnectCount = 0;
                _disconnectBalloonShown = false;
                if (_scannerStatus != ScannerStatus.Connected || scannerName != _currentScannerName)
                {
                    LogDiag($"UpdateConnectionState: ->Connected, scanner='{scannerName}', prev={_scannerStatus}");
                    _scannerStatus = ScannerStatus.Connected;
                    _currentScannerName = scannerName;
                    UpdateTrayVisuals();
                    UpdateMainFormCapabilities();
                }
            }
            else if (_scannerStatus == ScannerStatus.Unknown
                     || (_scannerStatus == ScannerStatus.Connected && ++_pendingDisconnectCount >= DisconnectConfirmThreshold))
            {
                LogDiag($"UpdateConnectionState: ->Disconnected (prev={_scannerStatus})");
                _scannerStatus = ScannerStatus.Disconnected;
                _currentScannerName = "";
                UpdateTrayVisuals();
                UpdateMainFormCapabilities();
                ShowScannerDisconnectedBalloon();
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

    private void ShowScannerDisconnectedBalloon()
    {
        if (_disconnectBalloonShown) return;
        _disconnectBalloonShown = true;
        ShowBalloon(AppTitle, "Der Scanner ist nicht angeschlossen oder ausgeschaltet.", ToolTipIcon.Warning);
    }

    // ───────────────────────── Tray visuals ─────────────────────────

    private void UpdateTrayVisuals()
    {
        if (_disposed) return;

        if (_busy)
        {
            _notifyIcon.Icon = GetScanningIcon();
            UpdateBusyText();
            return;
        }

        bool connected = _scannerStatus == ScannerStatus.Connected;
        _notifyIcon.Icon = connected ? GetConnectedIcon() : GetDisconnectedIcon();

        if (connected)
            SetTrayText($"{AppTitle}\n{_currentScannerName}");
        else if (!string.IsNullOrEmpty(_appSettings.SelectedSourceName))
            SetTrayText($"{AppTitle}\nAuswahl: {_appSettings.SelectedSourceName} (offline)");
        else if (_scannerStatus == ScannerStatus.Disconnected)
            SetTrayText($"{AppTitle}\nKein Scanner verbunden");
        else
            SetTrayText(AppTitle);
    }

    private void UpdateBusyText()
    {
        if (!_busy || _disposed) return;
        if (_activePipeline != null)
        {
            int pages = _pagesInEarlierBatches + (_batchRunning ? _activePipeline.AcquiredImages.Count : 0);
            SetTrayText(pages > 0
                ? $"{AppTitle}\nScannt… ({pages} {(pages == 1 ? "Seite" : "Seiten")})"
                : $"{AppTitle}\nScannt…");
        }
        else
        {
            SetTrayText($"{AppTitle}\nScan wird bearbeitet…");
        }
    }

    /// <summary>NotifyIcon.Text throws for more than 127 characters.</summary>
    private void SetTrayText(string text)
    {
        if (_disposed) return;
        if (text.Length > 127)
            text = text.Substring(0, 126) + "…";
        try { _notifyIcon.Text = text; }
        catch (Exception ex) { LogDiag($"SetTrayText failed: {ex.Message}"); }
    }

    private void ShowBalloon(string title, string text, ToolTipIcon icon)
    {
        if (_disposed) return;
        try
        {
            if (text.Length > 250) text = text.Substring(0, 249) + "…";
            if (title.Length > 60) title = title.Substring(0, 60);
            _notifyIcon.ShowBalloonTip(6000, title, text, icon);
        }
        catch (Exception ex)
        {
            LogDiag($"ShowBalloon failed: {ex.Message}");
        }
    }

    private Icon GetConnectedIcon() => _cachedConnectedIcon ??= TrayIcons.CreateConnectedIcon();
    private Icon GetDisconnectedIcon() => _cachedDisconnectedIcon ??= TrayIcons.CreateDisconnectedIcon();
    private Icon GetScanningIcon() => _cachedScanningIcon ??= TrayIcons.CreateScanningIcon();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            ScanButtonPipeServer.Shared.CommandReceived -= OnPipeCommand;
            Application.Idle -= OnFirstIdle;

            _pollTimer.Stop();
            _pollTimer.Dispose();
            _deviceChangeTimer.Stop();
            _deviceChangeTimer.Dispose();
            _progressTimer.Stop();
            _progressTimer.Dispose();
            _leftClickTimer.Stop();
            _leftClickTimer.Dispose();

            try
            {
                if (_twain != null && _twain.IsDsmOpen)
                    _twain.Close();
            }
            catch { }

            try { _mainForm?.Close(); } catch { }
            _contextMenu.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _hiddenWindow.Dispose();

            _cachedConnectedIcon?.Dispose();
            _cachedDisconnectedIcon?.Dispose();
            _cachedScanningIcon?.Dispose();

            // Persist settings on exit
            _appSettings.Save();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// "Scanvorgang fortsetzen": asks whether more documents should be appended to this scan.
    /// </summary>
    private sealed class ContinueScanDialog : Form
    {
        public ContinueScanDialog(int pagesSoFar)
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = AppTitle;
            Icon = TrayIcons.GetAppIcon();
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            ShowInTaskbar = true;
            ClientSize = new Size(360, 120);
            Font = UiFonts.Default;

            var lbl = new Label
            {
                Text = (pagesSoFar == 1 ? "Bisher wurde 1 Seite gescannt." : $"Bisher wurden {pagesSoFar} Seiten gescannt.")
                       + "\n\nWeitere Dokumente scannen? Legen Sie dazu die nächsten Seiten in den Scanner ein.",
                Location = new Point(14, 12),
                Size = new Size(332, 60),
                Font = UiFonts.Default
            };

            var btnContinue = new Button
            {
                Text = "Fortsetzen",
                DialogResult = DialogResult.OK,
                Location = new Point(150, 80),
                Size = new Size(95, 28),
                Font = UiFonts.Default
            };
            var btnFinish = new Button
            {
                Text = "Scan beenden",
                DialogResult = DialogResult.Cancel,
                Location = new Point(251, 80),
                Size = new Size(95, 28),
                Font = UiFonts.Default
            };

            Controls.AddRange(new Control[] { lbl, btnContinue, btnFinish });
            AcceptButton = btnContinue;
            CancelButton = btnFinish;
            ResumeLayout(false);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            WindowActivation.BringToFront(this);
        }
    }
}
