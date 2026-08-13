using System.Drawing;
using System.Windows.Forms;
using NTwain;
using NTwain.Data;

namespace SpeedScanManager;

/// <summary>
/// Dialog that lets the user pick a TWAIN data source (scanner).
/// USB-connected scanners are sorted to the top of the list.
/// </summary>
internal class ScannerSelectionDialog : Form
{
    private readonly ComboBox _combo;
    private readonly TwainSession _twain;
    private readonly string? _currentSelection;
    private readonly Button _btnOk;
    private readonly Button _btnCancel;
    private readonly Label _lblHint;
    private readonly System.Windows.Forms.Timer _pollTimer;
    private List<DataSource> _sorted = new();
    private bool _isAutoSelected = true;
    private int _lastSourceCount = -1;

    /// <summary>
    /// Returns the selected source name, or null if the user cancelled or chose automatic.
    /// </summary>
    public string? SelectedSourceName { get; private set; }

    public ScannerSelectionDialog(TwainSession twain, string? currentSelection)
    {
        _twain = twain;
        _currentSelection = currentSelection;

        Text = "SpeedScan Manager – Scanner auswählen";
        Icon = TrayIcons.GetAppIcon();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(420, 290);
        Font = new Font("Microsoft Sans Serif", 8.25f);

        var lbl = new Label
        {
            Text = "Bitte wählen Sie einen Scanner:",
            Location = new Point(16, 86),
            AutoSize = true,
            Font = Font
        };

        _combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(16, 110),
            Size = new Size(388, 24),
            Font = Font
        };
        _combo.SelectedIndexChanged += (s, e) =>
        {
            _isAutoSelected = _combo.SelectedIndex == 0;
        };

        _lblHint = new Label
        {
            Text = "USB-verbundene Scanner werden bevorzugt oben angezeigt.",
            Location = new Point(16, 146),
            AutoSize = true,
            ForeColor = Color.Gray,
            Font = Font
        };

        _btnOk = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(230, 240),
            Size = new Size(80, 28),
            Font = Font
        };
        _btnOk.Click += (s, e) =>
        {
            // Index 0 = "Automatisch" -> null (auto selection)
            if (_combo.SelectedIndex == 0)
                SelectedSourceName = null;
            else
            {
                var idx = _combo.SelectedIndex - 1; // offset for auto entry
                if (idx >= 0 && idx < _sorted.Count)
                    SelectedSourceName = _sorted[idx].Name;
            }
            DialogResult = DialogResult.OK;
            Close();
        };

        _btnCancel = new Button
        {
            Text = "Abbrechen",
            DialogResult = DialogResult.Cancel,
            Location = new Point(324, 240),
            Size = new Size(80, 28),
            Font = Font
        };

        Controls.AddRange(new Control[] { lbl, _combo, _lblHint, _btnOk, _btnCancel });
        AcceptButton = _btnOk;
        CancelButton = _btnCancel;

        // Auto-poll timer: refresh scanner list every 3 seconds
        _pollTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        _pollTimer.Tick += (s, e) => RefreshSources(autoRefresh: true);
        _pollTimer.Start();

        RefreshSources();
    }

    private void RefreshSources(bool autoRefresh = false)
    {
        List<DataSource> sources;
        try
        {
            sources = _twain.GetSources().ToList();
            sources = sources
                .OrderBy(s => GetConnectionType(s))
                .ThenBy(s => s.Name)
                .ToList();
        }
        catch
        {
            sources = new List<DataSource>();
        }

        // Skip rebuild if nothing changed during auto-refresh
        if (autoRefresh && sources.Count == _lastSourceCount &&
            sources.Select(s => s.Name).SequenceEqual(_sorted.Select(s => s.Name)))
            return;

        _sorted = sources;
        _lastSourceCount = sources.Count;

        // Preserve current selection state
        bool wasAuto = _isAutoSelected;
        string? prevSelectedName = null;
        if (!wasAuto && _combo.SelectedIndex > 0)
        {
            var prevIdx = _combo.SelectedIndex - 1;
            if (prevIdx >= 0 && prevIdx < _sorted.Count)
                prevSelectedName = _sorted[prevIdx].Name;
        }

        _combo.Items.Clear();

        // First entry is always "Automatisch"
        _combo.Items.Add("(Automatisch)");

        foreach (var s in _sorted)
        {
            var label = s.Name ?? "(unbenannt)";
            var connType = GetConnectionType(s);
            if (connType == ConnectionType.Usb)
                label += "  (USB)";
            else if (connType == ConnectionType.Network)
                label += "  (Netzwerk)";
            _combo.Items.Add(label);
        }

        if (_sorted.Count == 0)
        {
            _combo.Items.Add("(kein Scanner gefunden)");
            _combo.SelectedIndex = 0;
            _combo.Enabled = false;
            _btnOk.Enabled = true; // OK still works for "Automatisch"
            _lblHint.Text = "Kein Scanner gefunden. Die Liste wird automatisch aktualisiert, sobald ein Scanner erkannt wird.";
            _lblHint.ForeColor = Color.Firebrick;
            _isAutoSelected = true;
        }
        else
        {
            _combo.Enabled = true;
            _btnOk.Enabled = true;
            _lblHint.Text = "USB-verbundene Scanner werden bevorzugt oben angezeigt.";
            _lblHint.ForeColor = Color.Gray;

            // Restore selection
            if (wasAuto)
            {
                _combo.SelectedIndex = 0;
                _isAutoSelected = true;
            }
            else if (!string.IsNullOrEmpty(prevSelectedName))
            {
                // Find previously selected scanner in new list
                int foundIdx = -1;
                for (int i = 0; i < _sorted.Count; i++)
                {
                    if ((_sorted[i].Name ?? "") == prevSelectedName)
                    {
                        foundIdx = i + 1; // +1 for auto entry
                        break;
                    }
                }
                _combo.SelectedIndex = foundIdx >= 0 ? foundIdx : 0;
                _isAutoSelected = _combo.SelectedIndex == 0;
            }
            else if (!string.IsNullOrEmpty(_currentSelection))
            {
                // Select current saved source if it exists
                int selIdx = 0;
                for (int i = 0; i < _sorted.Count; i++)
                {
                    if ((_sorted[i].Name ?? "") == _currentSelection)
                    {
                        selIdx = i + 1; // +1 for auto entry
                        break;
                    }
                }
                _combo.SelectedIndex = selIdx;
                _isAutoSelected = selIdx == 0;
            }
            else
            {
                _combo.SelectedIndex = 0;
                _isAutoSelected = true;
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pollTimer?.Stop();
            _pollTimer?.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Three-way classification of scanner connection type.
    /// </summary>
    private enum ConnectionType { Usb = 0, Unknown = 1, Network = 2 }

    /// <summary>
    /// Classifies a TWAIN data source as USB, Network, or Unknown.
    /// Only labels as USB when WIA is in the name (WIA is the Windows USB scanning layer).
    /// Only labels as Network when IP address or network keywords are present.
    /// Everything else is Unknown — we don't guess.
    /// </summary>
    private static ConnectionType GetConnectionType(DataSource s)
    {
        var name = (s.Name ?? "").ToLowerInvariant();
        var mfr = (s.Manufacturer ?? "").ToLowerInvariant();
        var family = (s.ProductFamily ?? "").ToLowerInvariant();
        var combined = $"{name} {mfr} {family}";

        // Network indicators: IP address pattern, or explicit network keywords
        if (System.Text.RegularExpressions.Regex.IsMatch(combined, @"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b"))
            return ConnectionType.Network;
        if (combined.Contains("network") || combined.Contains("net scan") ||
            combined.Contains("wifi") || combined.Contains("wlan") ||
            combined.Contains("ethernet") || combined.Contains("lan") ||
            combined.Contains("tcp/ip") || combined.Contains("http://") ||
            combined.Contains("https://"))
            return ConnectionType.Network;

        // USB indicators: WIA is the Windows USB scanning bridge — definitively USB
        if (name.Contains("wia"))
            return ConnectionType.Usb;

        // Everything else: Unknown — don't claim USB when we don't know
        return ConnectionType.Unknown;
    }

    /// <summary>
    /// Selects the best TWAIN data source based on saved preference and USB preference.
    /// Called by ScanPipeline, ScannerStateService, and TrayApplicationContext.
    /// </summary>
    public static DataSource? SelectBestSource(TwainSession twain, string? savedSourceName)
    {
        // If we have a saved selection, try to find it
        if (!string.IsNullOrEmpty(savedSourceName))
        {
            var saved = twain.GetSources()
                .FirstOrDefault(s => (s.Name ?? "") == savedSourceName);
            if (saved != null)
                return saved;
        }

        // No saved selection (or saved selection no longer available):
        // Prefer USB-connected scanners, then Unknown, then Network
        var sources = twain.GetSources().ToList();
        if (sources.Count == 0)
            return null;

        // Sort by connection type: USB first, then Unknown, then Network
        var sorted = sources
            .OrderBy(s => GetConnectionType(s))
            .ThenBy(s => s.Name)
            .ToList();

        // Prefer the first USB source, then first Unknown, then first Network
        var usb = sorted.FirstOrDefault(s => GetConnectionType(s) == ConnectionType.Usb);
        if (usb != null)
            return usb;

        var unknown = sorted.FirstOrDefault(s => GetConnectionType(s) == ConnectionType.Unknown);
        if (unknown != null)
            return unknown;

        // Fall back to system default or first available (Network)
        return twain.DefaultSource ?? sorted[0];
    }
}
