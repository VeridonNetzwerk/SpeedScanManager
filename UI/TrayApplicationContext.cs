using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using NTwain;
using NTwain.Data;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace SpeedScanManager;

internal enum ScannerStatus { Unknown, Connected, Disconnected, Scanning }

/// <summary>
/// ApplicationContext that runs the app as a tray-only application.
/// Polls TWAIN every 3 seconds and updates the tray icon accordingly.
/// </summary>
internal class TrayApplicationContext : ApplicationContext, IMessageFilter
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
    private readonly AppSettings _appSettings;
    private ScanPipeline? _scanPipeline;
    private bool _isScanning;
    private ApplicationType _currentApplicationType = ApplicationType.ScanToFolder;
    private List<string> _lastScanFiles = new();
    private ScannerState? _lastScannerState;
    private DataSource? _persistentSource;
    private bool _deviceEventSubscribed;
    private WiaEventWatcher? _wiaWatcher;
    private bool _quickMenuScanTriggered;

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
        // Load persisted app settings
        _appSettings = AppSettings.Load();
        _quickMenuScanTriggered = _appSettings.QuickMenuEnabled;
        _currentApplicationType = _appSettings.CurrentApplicationType;

        // Apply saved profile to settings if it exists
        var savedProfile = _profileManager.GetByName(_appSettings.SelectedProfileName);
        if (savedProfile != null)
        {
            savedProfile.ApplyTo(_settings);
        }

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
        var miVersionInfo = new ToolStripMenuItem("SpeedScan Manager – Versionsinformationen");
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

        // Close persistent source and stop WIA watcher before scanning —
        // ScanPipeline manages its own source lifecycle, and WIA must release the device
        ClosePersistentSource();
        StopWiaWatcher();

        _isScanning = true;
        _scannerStatus = ScannerStatus.Scanning;
        _notifyIcon.Text = "SpeedScan Manager\nScanne...";

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

                    // Determine if we need to show the PostScanSaveDialog (Scan to Folder)
                    bool isScanToFolder = _quickMenuScanTriggered
                        ? false  // Quick-Menü: PostScanMediaDialog handles folder selection
                        : _currentApplicationType == ApplicationType.ScanToFolder;

                    // For non-QuickMenu Scan to Folder: keep images for the verify dialog
                    List<Bitmap>? imagesForDialog = null;
                    List<string> fileNames;

                    if (isScanToFolder)
                    {
                        // Keep cloned images for the dialog, dispose originals
                        imagesForDialog = images.Select(b => (Bitmap)b.Clone()).ToList();
                        foreach (var img in images)
                            img.Dispose();

                        // Process on UI thread after dialog
                        fileNames = new List<string>();
                    }
                    else
                    {
                        // Normal flow: save immediately
                        Directory.CreateDirectory(folder);
                        var processor = new ScanOutputProcessor(_settings);
                        fileNames = processor.ProcessAndSave(images, folder, _settings, formatMode, customName, digits);

                        // If Scan to Print, keep copies of images for printing before disposal
                        List<Bitmap>? imagesToPrint = null;
                        if (_currentApplicationType == ApplicationType.ScanToPrint || _quickMenuScanTriggered)
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
                                Debug.WriteLine($"[Tray] Scan abgeschlossen: {fileNames.Count} Datei(en) erstellt.");

                                if (_quickMenuScanTriggered)
                                {
                                    using var dlg = new PostScanMediaDialog(fileNames, imagesToPrint);
                                    dlg.StartPosition = FormStartPosition.CenterParent;

                                    if (dlg.ShowDialog(_hiddenWindow) == DialogResult.OK)
                                    {
                                        switch (dlg.SelectedMediaAction)
                                        {
                                            case PostScanMediaDialog.MediaAction.ScanToFolder:
                                                OpenScanResultFolder();
                                                break;

                                            case PostScanMediaDialog.MediaAction.ScanToEmail:
                                                OpenMailClient(fileNames);
                                                break;

                                            case PostScanMediaDialog.MediaAction.ScanToPrint:
                                                if (imagesToPrint != null && imagesToPrint.Count > 0)
                                                    PrintScannedImages(imagesToPrint);
                                                break;

                                            case PostScanMediaDialog.MediaAction.ScanToWord:
                                                CreateWordDocument(fileNames);
                                                break;

                                            case PostScanMediaDialog.MediaAction.ScanToExcel:
                                                CreateExcelDocument(fileNames);
                                                break;

                                            case PostScanMediaDialog.MediaAction.ScanToPowerPoint:
                                                CreatePowerPointPresentation(fileNames);
                                                break;

                                            case PostScanMediaDialog.MediaAction.ScanPictureFolder:
                                                SaveToPictureFolder(fileNames);
                                                break;

                                            case PostScanMediaDialog.MediaAction.EditWithPdf:
                                                OpenPdfEditor(fileNames);
                                                break;
                                        }
                                    }
                                }
                                else
                                {
                                    // No Quick-Menü → use configured application type directly
                                    if (_currentApplicationType == ApplicationType.ScanToEmail)
                                        OpenMailClient(fileNames);

                                    if (_currentApplicationType == ApplicationType.ScanToPrint && imagesToPrint != null)
                                        PrintScannedImages(imagesToPrint);
                                }
                            }
                            else
                            {
                                Debug.WriteLine("[Tray] Scan abgeschlossen, aber keine Dateien erstellt.");
                            }
                        });
                    }

                    // Scan to Folder without Quick-Menü: show verify dialog, then save
                    if (isScanToFolder && imagesForDialog != null)
                    {
                        _hiddenWindow.BeginInvoke(() =>
                        {
                            using var saveDlg = new PostScanSaveDialog(imagesForDialog, _settings);
                            saveDlg.StartPosition = FormStartPosition.CenterParent;

                            if (saveDlg.ShowDialog(_hiddenWindow) == DialogResult.OK)
                            {
                                string finalFolder = saveDlg.SelectedFolderPath;
                                string finalTitle = saveDlg.SelectedTitle;
                                Directory.CreateDirectory(finalFolder);

                                var processor = new ScanOutputProcessor(_settings);
                                fileNames = processor.ProcessAndSave(
                                    imagesForDialog,
                                    finalFolder,
                                    _settings,
                                    formatMode,
                                    finalTitle,
                                    digits);

                                _lastScanFiles = fileNames;
                                Debug.WriteLine($"[Tray] Scan gespeichert in: {finalFolder} ({fileNames.Count} Datei(en))");
                                OpenScanResultFolder();
                            }
                            else
                            {
                                Debug.WriteLine("[Tray] Scan abgebrochen durch Verify-Dialog.");
                            }

                            // Dispose dialog images
                            foreach (var img in imagesForDialog)
                                img.Dispose();
                        });
                    }
                }
                else if (!_scanPipeline.WasCancelled)
                {
                    _hiddenWindow.BeginInvoke(() =>
                    {
                        Debug.WriteLine("[Tray] Keine Bilder vom Scanner empfangen.");
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Scan failed: {ex}");
                _hiddenWindow.BeginInvoke(() =>
                {
                    Debug.WriteLine($"[Tray] Scan-Fehler: {ex.Message}");
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
                        // No images doesn't necessarily mean disconnected — could be
                        // user cancelled or no pages in ADF. Keep current status and
                        // try a lightweight reconnection check.
                        _scannerStatus = ScannerStatus.Connected;
                    }
                    // Always restart WIA watcher to detect future button presses
                    StartWiaWatcher();
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
            _quickMenuScanTriggered = _mainForm.QuickMenuEnabled;

            // Persist settings
            _appSettings.QuickMenuEnabled = _quickMenuScanTriggered;
            _appSettings.CurrentApplicationType = _currentApplicationType;
            if (_mainForm.SaveTab != null)
            {
                var (folder, formatMode, customName, digits) = _mainForm.SaveTab.GetSaveConfig();
                _appSettings.FolderPath = folder;
                _appSettings.FileNameFormat = formatMode;
                _appSettings.CustomFileName = customName;
                _appSettings.CounterDigits = digits;
            }
            _appSettings.Save();
        }
    }

    private (string folder, FileNameFormatDialog.FormatMode mode, string customName, int digits) GetSaveConfig()
    {
        if (_mainForm != null && !_mainForm.IsDisposed && _mainForm.SaveTab != null)
        {
            return _mainForm.SaveTab.GetSaveConfig();
        }

        // Use persisted settings when MainForm isn't open
        string folder = !string.IsNullOrEmpty(_appSettings.FolderPath)
            ? _appSettings.FolderPath
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SpeedScanManager");
        return (folder, _appSettings.FileNameFormat, _appSettings.CustomFileName, _appSettings.CounterDigits);
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
                Debug.WriteLine("[Tray] E-Mail-Client konnte nicht geöffnet werden. Dateien wurden im Zielordner gespeichert.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Mail client failed: {ex.Message}");
            Debug.WriteLine($"[Tray] E-Mail-Fehler: {ex.Message}");
        }
    }

    private void PrintScannedImages(List<Bitmap> images)
    {
        try
        {
            bool success = PrintHelper.PrintImages(images, _settings.PrinterName);

            if (success)
            {
                Debug.WriteLine("[Tray] Dokument wurde zum Drucker gesendet.");
            }
            else
            {
                Debug.WriteLine("[Tray] Druck fehlgeschlagen. Dateien wurden im Zielordner gespeichert.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Print failed: {ex.Message}");
            Debug.WriteLine($"[Tray] Druck-Fehler: {ex.Message}");
        }
        finally
        {
            foreach (var img in images)
                img.Dispose();
        }
    }

    private void CreateWordDocument(List<string> filePaths)
    {
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"SpeedScanManager_Word_{DateTime.Now:yyyyMMddHHmmss}");
            Directory.CreateDirectory(tempDir);
            var docxPath = Path.Combine(tempDir, "ScanResult.docx");

            using var doc = WordprocessingDocument.Create(docxPath, WordprocessingDocumentType.Document);

            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = new Body();

            foreach (var imgFile in filePaths)
            {
                if (!File.Exists(imgFile)) continue;

                var imagePart = mainPart.AddImagePart(GetImagePartType(imgFile));
                using (var fs = File.OpenRead(imgFile))
                {
                    imagePart.FeedData(fs);
                }

                var relId = mainPart.GetIdOfPart(imagePart);
                var drawing = new DocumentFormat.OpenXml.Wordprocessing.Drawing(CreateInlineDrawing(relId));
                var paragraph = new Paragraph(new DocumentFormat.OpenXml.Wordprocessing.Run(drawing));
                body.Append(paragraph);
            }

            mainPart.Document.Append(body);
            mainPart.Document.Save();

            Process.Start(new ProcessStartInfo { FileName = docxPath, UseShellExecute = true });
            Debug.WriteLine("[Tray] Word-Dokument erstellt und geöffnet.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CreateWordDocument failed: {ex.Message}");
            Debug.WriteLine($"[Tray] Word-Erstellung fehlgeschlagen: {ex.Message}");
        }
    }

    private void CreateExcelDocument(List<string> filePaths)
    {
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"SpeedScanManager_Excel_{DateTime.Now:yyyyMMddHHmmss}");
            Directory.CreateDirectory(tempDir);
            var xlsxPath = Path.Combine(tempDir, "ScanResult.xlsx");

            using var doc = SpreadsheetDocument.Create(xlsxPath, SpreadsheetDocumentType.Workbook);

            var wbPart = doc.AddWorkbookPart();
            wbPart.Workbook = new Workbook();
            var wsPart = wbPart.AddNewPart<WorksheetPart>();
            wsPart.Worksheet = new Worksheet(new SheetData());

            var sheets = wbPart.Workbook.AppendChild(new Sheets());
            sheets.AppendChild(new Sheet
            {
                Id = wbPart.GetIdOfPart(wsPart),
                SheetId = 1,
                Name = "Scan"
            });

            var drawingsPart = wsPart.AddNewPart<DrawingsPart>();
            wsPart.Worksheet.AppendChild(new DocumentFormat.OpenXml.Spreadsheet.Drawing
            {
                Id = wsPart.GetIdOfPart(drawingsPart)
            });

            var drawing = new Xdr.WorksheetDrawing();
            uint drawingId = 1;
            int rowOffset = 0;

            foreach (var imgFile in filePaths)
            {
                if (!File.Exists(imgFile)) continue;

                var imagePart = drawingsPart.AddImagePart(GetImagePartType(imgFile));
                using (var fs = File.OpenRead(imgFile))
                {
                    imagePart.FeedData(fs);
                }
                var relId = drawingsPart.GetIdOfPart(imagePart);

                var anchor = new Xdr.AbsoluteAnchor
                {
                    Position = new Xdr.Position { X = 0, Y = rowOffset },
                    Extent = new Xdr.Extent { Cx = 6000000, Cy = 8000000 }
                };
                anchor.AppendChild(new A.Blip { Embed = relId });
                drawing.AppendChild(anchor);
                drawingId++;
                rowOffset += 8200000;
            }

            drawingsPart.WorksheetDrawing = drawing;
            wbPart.Workbook.Save();

            Process.Start(new ProcessStartInfo { FileName = xlsxPath, UseShellExecute = true });
            Debug.WriteLine("[Tray] Excel-Dokument erstellt und geöffnet.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CreateExcelDocument failed: {ex.Message}");
            Debug.WriteLine($"[Tray] Excel-Erstellung fehlgeschlagen: {ex.Message}");
        }
    }

    private void CreatePowerPointPresentation(List<string> filePaths)
    {
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"SpeedScanManager_Pptx_{DateTime.Now:yyyyMMddHHmmss}");
            Directory.CreateDirectory(tempDir);
            var pptxPath = Path.Combine(tempDir, "ScanResult.pptx");

            using var doc = PresentationDocument.Create(pptxPath, PresentationDocumentType.Presentation);

            var presPart = doc.AddPresentationPart();
            presPart.Presentation = new Presentation();
            var slideIdList = new SlideIdList();
            presPart.Presentation.Append(slideIdList);

            var slideMasterPart = presPart.AddNewPart<SlideMasterPart>();
            slideMasterPart.SlideMaster = new SlideMaster(
                new CommonSlideData(),
                new ColorMap(),
                new SlideLayoutIdList(),
                new TextStyles());
            slideMasterPart.SlideMaster.Save();

            var slideLayoutPart = slideMasterPart.AddNewPart<SlideLayoutPart>();
            slideLayoutPart.SlideLayout = new SlideLayout(
                new CommonSlideData(),
                new ColorMapOverride());
            slideLayoutPart.SlideLayout.Save();

            presPart.Presentation.SlideMasterIdList = new SlideMasterIdList(
                new SlideMasterId { Id = 2147483648U, RelationshipId = presPart.GetIdOfPart(slideMasterPart) });

            presPart.Presentation.SlideSize = new SlideSize { Cx = 9144000, Cy = 6858000 };
            presPart.Presentation.NotesSize = new NotesSize { Cx = 6858000, Cy = 9144000 };

            uint slideIdx = 1;
            foreach (var imgFile in filePaths)
            {
                if (!File.Exists(imgFile)) continue;

                var slidePart = presPart.AddNewPart<SlidePart>();
                slidePart.Slide = new Slide(
                    new CommonSlideData(
                        new ShapeTree(
                            new NonVisualGroupShapeProperties(
                                new NonVisualDrawingProperties { Id = 1U, Name = "Group 1" },
                                new NonVisualGroupShapeProperties()),
                            new GroupShapeProperties(new A.TransformGroup()))));

                var imagePart = slidePart.AddImagePart(GetImagePartType(imgFile));
                using (var fs = File.OpenRead(imgFile))
                {
                    imagePart.FeedData(fs);
                }
                var relId = slidePart.GetIdOfPart(imagePart);

                var shapeTree = slidePart.Slide.CommonSlideData!.ShapeTree!;
                shapeTree.AppendChild(new DocumentFormat.OpenXml.Presentation.Picture(
                    new NonVisualPictureProperties(
                        new NonVisualDrawingProperties { Id = 2U, Name = $"Image {slideIdx}" },
                        new NonVisualPictureDrawingProperties()),
                    new BlipFill(
                        new A.Blip { Embed = relId },
                        new A.Stretch(new A.FillRectangle())),
                    new ShapeProperties(
                        new A.Transform2D(
                            new A.Offset { X = 457200, Y = 342900 },
                            new A.Extents { Cx = 8229600, Cy = 6172200 }))));

                slideIdList.AppendChild(new SlideId
                {
                    Id = 256U + slideIdx,
                    RelationshipId = presPart.GetIdOfPart(slidePart)
                });
                slideIdx++;
            }

            presPart.Presentation.Save();

            Process.Start(new ProcessStartInfo { FileName = pptxPath, UseShellExecute = true });
            Debug.WriteLine("[Tray] PowerPoint-Präsentation erstellt und geöffnet.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CreatePowerPointPresentation failed: {ex.Message}");
            Debug.WriteLine($"[Tray] PowerPoint-Erstellung fehlgeschlagen: {ex.Message}");
        }
    }

    private void SaveToPictureFolder(List<string> filePaths)
    {
        try
        {
            var picsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "SpeedScanManager",
                $"Scan_{DateTime.Now:yyyyMMdd_HHmmss}");

            Directory.CreateDirectory(picsPath);

            foreach (var imgFile in filePaths)
            {
                if (!File.Exists(imgFile)) continue;
                var dest = Path.Combine(picsPath, Path.GetFileName(imgFile));
                File.Copy(imgFile, dest, true);
            }

            Process.Start(new ProcessStartInfo { FileName = picsPath, UseShellExecute = true });
            Debug.WriteLine($"[Tray] Bilder gespeichert in: {picsPath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SaveToPictureFolder failed: {ex.Message}");
            Debug.WriteLine($"[Tray] Bilder-Ordner fehlgeschlagen: {ex.Message}");
        }
    }

    private void OpenPdfEditor(List<string> filePaths)
    {
        try
        {
            foreach (var f in filePaths.Where(File.Exists))
            {
                Process.Start(new ProcessStartInfo { FileName = f, UseShellExecute = true });
            }
            Debug.WriteLine("[Tray] PDF-Editor geöffnet.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OpenPdfEditor failed: {ex.Message}");
            Debug.WriteLine($"[Tray] PDF-Editor fehlgeschlagen: {ex.Message}");
        }
    }

    private static PartTypeInfo GetImagePartType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".jpeg" or ".jpg" => ImagePartType.Jpeg,
            ".png" => ImagePartType.Png,
            ".bmp" => ImagePartType.Bmp,
            ".gif" => ImagePartType.Gif,
            ".tiff" or ".tif" => ImagePartType.Tiff,
            _ => ImagePartType.Jpeg
        };
    }

    private static DW.Inline CreateInlineDrawing(string relId)
    {
        return new DW.Inline
        {
            Extent = new DW.Extent { Cx = 6000000, Cy = 8000000 },
            DocProperties = new DW.DocProperties { Id = 1, Name = "Scan Image" },
            Graphic = new A.Graphic(
                new A.GraphicData(
                    new PIC.Picture(
                        new PIC.NonVisualPictureProperties(
                            new PIC.NonVisualDrawingProperties { Id = 0, Name = "Scan" },
                            new PIC.NonVisualPictureDrawingProperties()),
                        new PIC.BlipFill(
                            new A.Blip { Embed = relId },
                            new A.Stretch(new A.FillRectangle())),
                        new PIC.ShapeProperties(
                            new A.Transform2D(
                                new A.Offset { X = 0, Y = 0 },
                                new A.Extents { Cx = 6000000, Cy = 8000000 }))))
                { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })
        };
    }

    private void ShowMainForm()
    {
        if (_mainForm == null || _mainForm.IsDisposed)
        {
            _mainForm = new MainForm(_settings);
            _mainForm.FormClosed += (s, e) =>
            {
                SyncSettingsFromMainForm();
                _mainForm = null;
            };
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
        ClosePersistentSource();
        StopWiaWatcher();
        WiaEventWatcher.Shutdown();
        _notifyIcon.Visible = false;
        Application.Exit();
    }

    private void InitializeTwain()
    {
        try
        {
            _hiddenWindow.Show();
            _hiddenWindow.Hide();

            var appId = TWIdentity.Create(
                DataGroups.Control | DataGroups.Image,
                new Version(1, 0, 0, 0),
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
        // Query scanner state once after TWAIN init to detect connected scanners.
        // If the scanner is disconnected, source.Open() may show a driver dialog,
        // but that's acceptable — the user needs to know the real status.
        if (_twainInitialized)
        {
            _scannerStatus = ScannerStatus.Unknown;
            UpdateConnectionState();
        }
        else
        {
            _scannerStatus = ScannerStatus.Disconnected;
        }
        UpdateTrayVisuals();
        UpdateMenuItems();
    }

    private void OnPollTick(object? sender, EventArgs e)
    {
        try
        {
            // When already disconnected, skip background polling entirely.
            if (_scannerStatus == ScannerStatus.Disconnected)
                return;

            // When WIA watcher is active (connected, waiting for button events),
            // skip polling — QueryState would open/close the TWAIN source and
            // interfere with WIA event delivery.
            if (_wiaWatcher != null)
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
                // Start WIA event watcher for scanner button presses
                StartWiaWatcher();
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
                ClosePersistentSource();
                StopWiaWatcher();
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
                    ClosePersistentSource();
                    StopWiaWatcher();
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
            ? $"SpeedScan Manager\n{_currentScannerName}"
            : "SpeedScan Manager\nKein Scanner";
    }

    /// <summary>
    /// Opens the default TWAIN source and keeps it open to receive device events
    /// (scanner button presses). This prevents Windows from showing its own
    /// scanner button event dialog.
    /// </summary>
    private void OpenPersistentSource()
    {
        if (_twain == null || !_twain.IsDsmOpen) return;
        if (_persistentSource != null && _persistentSource.IsOpen) return;

        try
        {
            var source = _twain.DefaultSource;
            if (source == null)
            {
                LogDiag("OpenPersistentSource: no default source");
                return;
            }

            var rc = source.Open();
            if (rc != ReturnCode.Success)
            {
                LogDiag($"OpenPersistentSource: source.Open rc={rc}");
                return;
            }

            _persistentSource = source;
            LogDiag($"OpenPersistentSource: opened '{source.Name}'");

            // Log supported device events (CAP_DEVICEEVENT is typically read-only —
            // the driver reports which events it can send; the app processes them
            // via the message loop when the source is open)
            try
            {
                var supportedEvents = source.Capabilities.CapDeviceEvent.GetValues();
                LogDiag($"OpenPersistentSource: supported device events: [{string.Join(", ", supportedEvents)}]");
            }
            catch (Exception ex)
            {
                LogDiag($"OpenPersistentSource: CapDeviceEvent GetValues failed: {ex.Message}");
            }

            if (!_deviceEventSubscribed)
            {
                _twain.DeviceEvent += OnScannerDeviceEvent;
                _deviceEventSubscribed = true;
                Application.AddMessageFilter(this);
                LogDiag("OpenPersistentSource: subscribed to DeviceEvent + message filter");
            }
        }
        catch (Exception ex)
        {
            LogDiag($"OpenPersistentSource exception: {ex.Message}");
        }
    }

    private void ClosePersistentSource()
    {
        if (_twain != null && _deviceEventSubscribed)
        {
            _twain.DeviceEvent -= OnScannerDeviceEvent;
            _deviceEventSubscribed = false;
            Application.RemoveMessageFilter(this);
            LogDiag("ClosePersistentSource: unsubscribed from DeviceEvent + message filter");
        }

        if (_persistentSource != null && _persistentSource.IsOpen)
        {
            try { _persistentSource.Close(); LogDiag("ClosePersistentSource: closed"); }
            catch (Exception ex) { LogDiag($"ClosePersistentSource close failed: {ex.Message}"); }
        }
        _persistentSource = null;
    }

    private void OnScannerDeviceEvent(object? sender, DeviceEventArgs e)
    {
        var eventType = e.DeviceEvent.Event;
        LogDiag($"OnScannerDeviceEvent: {eventType}, device='{e.DeviceEvent.DeviceName}'");

        // Trigger scan on any device event when connected and not already scanning.
        // Fujitsu scanners may use different event types for button presses.
        if (!_isScanning && _scannerStatus == ScannerStatus.Connected)
        {
            LogDiag("OnScannerDeviceEvent: triggering scan from button press");
            _hiddenWindow.BeginInvoke(() => StartScan(ScanSide.Automatic));
        }
    }

    private void StartWiaWatcher()
    {
        if (_wiaWatcher != null) return;

        try
        {
            // Close persistent TWAIN source so WIA can access the scanner device
            ClosePersistentSource();

            _wiaWatcher = new WiaEventWatcher();
            _wiaWatcher.ScanButtonPressed += OnWiaScanButtonPressed;
            _wiaWatcher.Start();
            LogDiag("StartWiaWatcher: started");
        }
        catch (Exception ex)
        {
            LogDiag($"StartWiaWatcher exception: {ex.Message}");
        }
    }

    private void StopWiaWatcher()
    {
        if (_wiaWatcher == null) return;

        try
        {
            _wiaWatcher.ScanButtonPressed -= OnWiaScanButtonPressed;
            _wiaWatcher.Dispose();
            _wiaWatcher = null;
            LogDiag("StopWiaWatcher: stopped");
        }
        catch (Exception ex)
        {
            LogDiag($"StopWiaWatcher exception: {ex.Message}");
        }
    }

    private void OnWiaScanButtonPressed()
    {
        LogDiag($"OnWiaScanButtonPressed: isScanning={_isScanning}, status={_scannerStatus}");

        if (!_isScanning && _scannerStatus == ScannerStatus.Connected)
        {
            LogDiag("OnWiaScanButtonPressed: triggering scan");
            _hiddenWindow.BeginInvoke(() => StartScan(ScanSide.Automatic));
        }
    }

    /// <summary>
    /// IMessageFilter implementation — intercepts Windows messages and forwards
    /// them to TWAIN for device event processing when the source is open but not enabled.
    /// </summary>
    public bool PreFilterMessage(ref System.Windows.Forms.Message m)
    {
        if (_twain != null && _persistentSource != null && _persistentSource.IsOpen && !_isScanning)
        {
            try
            {
                _twain.IsTwainMessage(m.HWnd, m.Msg, m.WParam, m.LParam);
            }
            catch { }
        }
        return false;
    }

    private void ShowScannerDisconnectedBalloon()
    {
        Debug.WriteLine("[Tray] Der Scanner ist nicht angeschlossen oder ausgeschaltet.");
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
            _scanPipeline?.Dispose();

            // Persist settings on exit
            _appSettings.QuickMenuEnabled = _quickMenuScanTriggered;
            _appSettings.CurrentApplicationType = _currentApplicationType;
            _appSettings.Save();
        }

        base.Dispose(disposing);
    }
}
