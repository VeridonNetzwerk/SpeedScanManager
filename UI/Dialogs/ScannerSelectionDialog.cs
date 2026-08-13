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
    private readonly Button _btnAuto;
    private readonly Button _btnRefresh;
    private readonly Label _lblHint;
    private List<DataSource> _sorted = new();

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
        ClientSize = new Size(420, 220);
        Font = new Font("Microsoft Sans Serif", 8.25f);

        var lbl = new Label
        {
            Text = "Bitte wählen Sie einen Scanner:",
            Location = new Point(16, 16),
            AutoSize = true,
            Font = Font
        };

        _combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(16, 40),
            Size = new Size(340, 24),
            Font = Font
        };

        _btnRefresh = new Button
        {
            Text = "Aktualisieren",
            Location = new Point(362, 39),
            Size = new Size(42, 24),
            Font = Font
        };
        _btnRefresh.Click += (s, e) => RefreshSources();

        _lblHint = new Label
        {
            Text = "USB-verbundene Scanner werden bevorzugt oben angezeigt.",
            Location = new Point(16, 76),
            AutoSize = true,
            ForeColor = Color.Gray,
            Font = Font
        };

        _btnAuto = new Button
        {
            Text = "Automatisch",
            Location = new Point(16, 170),
            Size = new Size(90, 28),
            Font = Font
        };
        _btnAuto.Click += (s, e) =>
        {
            SelectedSourceName = null;
            DialogResult = DialogResult.OK;
            Close();
        };

        _btnOk = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(230, 170),
            Size = new Size(80, 28),
            Font = Font
        };
        _btnOk.Click += (s, e) =>
        {
            var idx = _combo.SelectedIndex;
            if (idx >= 0 && idx < _sorted.Count)
                SelectedSourceName = _sorted[idx].Name;
            DialogResult = DialogResult.OK;
            Close();
        };

        _btnCancel = new Button
        {
            Text = "Abbrechen",
            DialogResult = DialogResult.Cancel,
            Location = new Point(324, 170),
            Size = new Size(80, 28),
            Font = Font
        };

        Controls.AddRange(new Control[] { lbl, _combo, _btnRefresh, _lblHint, _btnAuto, _btnOk, _btnCancel });
        AcceptButton = _btnOk;
        CancelButton = _btnCancel;

        RefreshSources();
    }

    private void RefreshSources()
    {
        _combo.Items.Clear();
        _sorted.Clear();

        try
        {
            var sources = _twain.GetSources().ToList();
            _sorted = sources
                .OrderByDescending(s => IsUsbLikely(s))
                .ThenBy(s => s.Name)
                .ToList();
        }
        catch
        {
            _sorted = new List<DataSource>();
        }

        foreach (var s in _sorted)
        {
            var label = s.Name ?? "(unbenannt)";
            if (IsUsbLikely(s))
                label += "  (USB)";
            _combo.Items.Add(label);
        }

        if (_sorted.Count == 0)
        {
            _combo.Items.Add("(kein Scanner gefunden)");
            _combo.SelectedIndex = 0;
            _combo.Enabled = false;
            _btnOk.Enabled = false;
            _lblHint.Text = "Kein Scanner gefunden. Schließen Sie einen Scanner an und klicken Sie auf 'Aktualisieren'.";
            _lblHint.ForeColor = Color.Firebrick;
        }
        else
        {
            _combo.Enabled = true;
            _btnOk.Enabled = true;
            _lblHint.Text = "USB-verbundene Scanner werden bevorzugt oben angezeigt.";
            _lblHint.ForeColor = Color.Gray;

            // Select current saved source if it exists
            int selIdx = 0;
            if (!string.IsNullOrEmpty(_currentSelection))
            {
                for (int i = 0; i < _sorted.Count; i++)
                {
                    if ((_sorted[i].Name ?? "") == _currentSelection)
                    {
                        selIdx = i;
                        break;
                    }
                }
            }
            _combo.SelectedIndex = selIdx;
        }
    }

    /// <summary>
    /// Heuristic: TWAIN sources whose name or manufacturer contains "WIA" are
    /// typically WIA-based (which is the Windows USB scanning layer).
    /// Sources without "WIA" in the name are likely direct TWAIN drivers
    /// for USB-connected scanners. We prefer non-WIA TWAIN sources first,
    /// then WIA sources as fallback.
    /// </summary>
    private static bool IsUsbLikely(DataSource s)
    {
        var name = (s.Name ?? "").ToLowerInvariant();
        var mfr = (s.Manufacturer ?? "").ToLowerInvariant();

        // WIA-based TWAIN sources are USB scanners accessed through the WIA bridge
        // Direct TWAIN drivers are also typically USB — both are USB-connected.
        // The only non-USB scenario would be network scanners, which often have
        // "network", "net", "ip", or "wifi" in their name.
        if (name.Contains("network") || name.Contains("net ") || name.Contains("wifi") || name.Contains("wlan"))
            return false;

        // Everything else is likely USB-connected
        return true;
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
        // Prefer USB-connected scanners, then fall back to default source
        var sources = twain.GetSources().ToList();
        if (sources.Count == 0)
            return null;

        // Sort by USB likelihood
        var usbSources = sources
            .Where(s => IsUsbLikely(s))
            .ToList();

        if (usbSources.Count > 0)
            return usbSources[0];

        // Fall back to system default or first available
        return twain.DefaultSource ?? sources[0];
    }
}
