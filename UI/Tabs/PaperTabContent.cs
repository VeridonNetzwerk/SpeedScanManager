using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Content panel for the "Papier" tab.
/// Contains paper size dropdown, custom size management, carrier sheet button (disabled),
/// and multi-feed detection dropdown.
/// </summary>
internal sealed class PaperSizeItem
{
    public string DisplayText { get; }
    public PaperSizeMode Value { get; }
    /// <summary>Index into ScanSettings.CustomPaperSizes for Value == Custom, otherwise -1.</summary>
    public int CustomIndex { get; }

    public PaperSizeItem(string displayText, PaperSizeMode value, int customIndex = -1)
    {
        DisplayText = displayText;
        Value = value;
        CustomIndex = customIndex;
    }

    public override string ToString() => DisplayText;
}

/// <summary>
/// Content of the "Papier" settings page: paper size (with the flatbed hint), custom sizes,
/// carrier sheet settings and multi-feed detection, grouped in settings cards.
/// </summary>
internal class PaperTabContent : ModernStack
{
    private readonly ComboBox _cbPaperSize;
    private readonly Button _btnCustomSize;
    private readonly Button _btnCarrierSheet;
    private readonly ComboBox _cbMultiFeed;
    private readonly ScanSettings _settings;
    private readonly SettingRow _rowPaperSize;
    private bool _ultrasoundSupported = true;
    private bool _lengthSupported = true;
    private bool _loading;

    private static readonly PaperSizeItem[] FixedSizes =
    {
        new("Automatische Erkennung", PaperSizeMode.Automatic),
        new("A3 (297 x 420 mm)", PaperSizeMode.A3),
        new("A4 (210 x 297 mm)", PaperSizeMode.A4),
        new("A5 (148 x 210 mm)", PaperSizeMode.A5),
        new("A6 (105 x 148 mm)", PaperSizeMode.A6),
        new("B4(JIS) (257 x 364 mm)", PaperSizeMode.B4Jis),
        new("B5(JIS) (182 x 257 mm)", PaperSizeMode.B5Jis),
        new("B6(JIS) (128 x 182 mm)", PaperSizeMode.B6Jis),
        new("Postkarte (100 x 148 mm)", PaperSizeMode.Postcard),
        new("Visitenkarte (55 x 91 mm)", PaperSizeMode.BusinessCard),
        new("Letter (8,5 x 11 in. (216 x 279,4 mm))", PaperSizeMode.Letter),
        new("Double Letter (11 x 17 in. (279,4 x 431,8 mm))", PaperSizeMode.DoubleLetter),
        new("Legal (8,5 x 14 in. (216 x 355,6 mm))", PaperSizeMode.Legal)
    };

    public PaperTabContent(ScanSettings settings)
    {
        _settings = settings;

        _cbPaperSize = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Size = new Size(300, 24),
            DropDownWidth = 360,
            MaxDropDownItems = 16,
            IntegralHeight = false
        };
        _cbPaperSize.SelectedIndexChanged += (s, e) => OnPaperSizeChanged();

        _btnCarrierSheet = new Button
        {
            Text = "Trägerblatteinstellungen...",
            Size = new Size(220, 32)
        };
        ThemeManager.SetGlyph(_btnCarrierSheet, Glyph.Carrier);
        _btnCarrierSheet.Click += (s, e) => OpenCarrierSheetDialog();

        _btnCustomSize = new Button
        {
            Text = "Benutzerdefiniert...",
            Size = new Size(180, 32),
            Enabled = true
        };
        ThemeManager.SetGlyph(_btnCustomSize, Glyph.Ruler);
        _btnCustomSize.Click += (s, e) => OpenCustomSizeDialog();

        _cbMultiFeed = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode = DrawMode.OwnerDrawFixed,
            Size = new Size(300, 24),
            DropDownWidth = 380
        };
        _cbMultiFeed.DrawItem += (s, e) => DrawMultiFeedItem(e);
        _cbMultiFeed.Items.AddRange(new object[]
        {
            "Aus",
            "Überprüfung von Überlappung [Ultraschall]",
            "Überprüfung der Länge",
            "Überprüfung von Überlappung und Länge"
        });
        _cbMultiFeed.SelectedIndexChanged += (s, e) =>
        {
            if (_loading || _cbMultiFeed.SelectedIndex < 0) return;
            var selected = (MultiFeedDetection)_cbMultiFeed.SelectedIndex;
            if (!IsMultiFeedModeSupported(selected))
            {
                _cbMultiFeed.SelectedIndex = (int)MultiFeedDetection.Off;
                _settings.MultiFeedDetection = MultiFeedDetection.Off;
                return;
            }
            _settings.MultiFeedDetection = selected;
        };

        var cardPaper = new SettingsCard();
        _rowPaperSize = cardPaper.AddRow("Papiergröße",
            "Die automatische Papiergrößenerkennung steht für das Flachbettscannen nicht zur Verfügung.",
            _cbPaperSize, 300);
        cardPaper.AddRow("Benutzerdefinierte Papiergrößen", "Eigene Papiergrößen anlegen, ändern oder löschen.", _btnCustomSize);
        cardPaper.AddRow("Trägerblatt", "Trägerblatt-Modus und Ausgabebildgröße.", _btnCarrierSheet);

        var cardFeed = new SettingsCard("Einzug");
        cardFeed.AddRow("Mehrfacheinzugserkennung", "Erkennt, wenn mehrere Blätter gleichzeitig eingezogen werden.", _cbMultiFeed, 300);

        Controls.Add(cardPaper);
        Controls.Add(cardFeed);
        LoadFromSettings();
    }

    /// <summary>
    /// Refreshes all controls from the settings without raising change notifications.
    /// </summary>
    public void LoadFromSettings()
    {
        _loading = true;
        try
        {
            RefreshPaperSizeList();
            SelectCurrentPaperSize();
            _cbMultiFeed.SelectedIndex = Math.Clamp((int)_settings.MultiFeedDetection, 0, _cbMultiFeed.Items.Count - 1);
        }
        finally
        {
            _loading = false;
        }
        UpdateFlatbedHint();
    }

    /// <summary>
    /// Fixed sizes followed by the user-defined sizes (name + mm).
    /// </summary>
    private void RefreshPaperSizeList()
    {
        _cbPaperSize.BeginUpdate();
        _cbPaperSize.Items.Clear();
        _cbPaperSize.Items.AddRange(FixedSizes);
        for (int i = 0; i < _settings.CustomPaperSizes.Count; i++)
        {
            _cbPaperSize.Items.Add(new PaperSizeItem(
                _settings.CustomPaperSizes[i].DisplayText, PaperSizeMode.Custom, i));
        }
        _cbPaperSize.EndUpdate();
    }

    private void SelectCurrentPaperSize()
    {
        for (int i = 0; i < _cbPaperSize.Items.Count; i++)
        {
            var item = (PaperSizeItem)_cbPaperSize.Items[i]!;
            if (item.Value == _settings.PaperSize
                && (item.Value != PaperSizeMode.Custom || item.CustomIndex == _settings.SelectedCustomSizeIndex))
            {
                _cbPaperSize.SelectedIndex = i;
                return;
            }
        }
        _cbPaperSize.SelectedIndex = 0;
    }

    private void OnPaperSizeChanged()
    {
        if (_loading) return;
        if (_cbPaperSize.SelectedItem is PaperSizeItem item)
        {
            if (item.Value == PaperSizeMode.Automatic && _settings.ScanSide == ScanSide.Flatbed)
            {
                // Automatic detection is not available for flatbed scanning
                _loading = true;
                try { SelectCurrentPaperSize(); }
                finally { _loading = false; }
                UpdateFlatbedHint();
                return;
            }
            _settings.PaperSize = item.Value;
            _settings.SelectedCustomSizeIndex = item.CustomIndex;
        }
    }

    /// <summary>
    /// Called by MainForm when the scan side changes. Flatbed cannot use automatic
    /// paper size detection, so a fixed size (A4) is forced.
    /// </summary>
    public void OnScanSideChanged(ScanSide side)
    {
        if (side == ScanSide.Flatbed && _settings.PaperSize == PaperSizeMode.Automatic)
        {
            _settings.PaperSize = PaperSizeMode.A4;
            _settings.SelectedCustomSizeIndex = -1;
            _loading = true;
            try { SelectCurrentPaperSize(); }
            finally { _loading = false; }
        }
        UpdateFlatbedHint();
    }

    private void UpdateFlatbedHint()
    {
        bool flatbed = _settings.ScanSide == ScanSide.Flatbed;
        _rowPaperSize.DescriptionRole = flatbed ? ThemeRole.Danger : ThemeRole.Muted;
    }

    private void OpenCustomSizeDialog()
    {
        using var dialog = new CustomSizeDialog(_settings.CustomPaperSizes);

        if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
        {
            // Keep the selected custom size if it still exists (matched by value)
            CustomPaperSize? selected = _settings.PaperSize == PaperSizeMode.Custom
                && _settings.SelectedCustomSizeIndex >= 0
                && _settings.SelectedCustomSizeIndex < _settings.CustomPaperSizes.Count
                    ? _settings.CustomPaperSizes[_settings.SelectedCustomSizeIndex]
                    : null;

            _settings.CustomPaperSizes.Clear();
            _settings.CustomPaperSizes.AddRange(dialog.CustomSizes.Take(ScanSettings.MaxCustomPaperSizes));

            if (_settings.PaperSize == PaperSizeMode.Custom)
            {
                int idx = selected == null ? -1 : _settings.CustomPaperSizes.IndexOf(selected);
                if (idx < 0 && selected != null)
                    idx = _settings.CustomPaperSizes.FindIndex(c => c.Name == selected.Name);
                if (idx >= 0)
                {
                    _settings.SelectedCustomSizeIndex = idx;
                }
                else
                {
                    _settings.PaperSize = _settings.ScanSide == ScanSide.Flatbed ? PaperSizeMode.A4 : PaperSizeMode.Automatic;
                    _settings.SelectedCustomSizeIndex = -1;
                }
            }

            LoadFromSettings();
        }
    }

    /// <summary>
    /// Updates the multi-feed dropdown based on pre-queried scanner capabilities.
    /// </summary>
    public void UpdateMultiFeedCapabilities(bool supportsUltrasonic, bool supportsLength)
    {
        _ultrasoundSupported = supportsUltrasonic;
        _lengthSupported = supportsLength;
        EnsureValidSelection();
        _cbMultiFeed.Invalidate();
    }

    private void EnsureValidSelection()
    {
        // If current selection is not supported, revert to Off
        var current = (MultiFeedDetection)_cbMultiFeed.SelectedIndex;
        if (current == MultiFeedDetection.OverlapUltrasound && !_ultrasoundSupported)
        {
            _cbMultiFeed.SelectedIndex = (int)MultiFeedDetection.Off;
            _settings.MultiFeedDetection = MultiFeedDetection.Off;
        }
        else if (current == MultiFeedDetection.Length && !_lengthSupported)
        {
            _cbMultiFeed.SelectedIndex = (int)MultiFeedDetection.Off;
            _settings.MultiFeedDetection = MultiFeedDetection.Off;
        }
        else if (current == MultiFeedDetection.Both && (!_ultrasoundSupported || !_lengthSupported))
        {
            _cbMultiFeed.SelectedIndex = (int)MultiFeedDetection.Off;
            _settings.MultiFeedDetection = MultiFeedDetection.Off;
        }
    }

    /// <summary>
    /// Returns whether a specific multi-feed detection mode is supported by the current scanner.
    /// </summary>
    public bool IsMultiFeedModeSupported(MultiFeedDetection mode) => mode switch
    {
        MultiFeedDetection.Off => true,
        MultiFeedDetection.OverlapUltrasound => _ultrasoundSupported,
        MultiFeedDetection.Length => _lengthSupported,
        MultiFeedDetection.Both => _ultrasoundSupported && _lengthSupported,
        _ => false
    };

    private void DrawMultiFeedItem(DrawItemEventArgs e)
    {
        if (e.Index < 0)
        {
            ThemeManager.DrawItemBackground(_cbMultiFeed, e);
            return;
        }

        var mode = (MultiFeedDetection)e.Index;
        bool supported = IsMultiFeedModeSupported(mode);
        var text = _cbMultiFeed.Items[e.Index]?.ToString() ?? "";

        var textColor = ThemeManager.DrawItemBackground(_cbMultiFeed, e, itemEnabled: supported);
        var bounds = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 4), e.Bounds.Height);

        var flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
        TextRenderer.DrawText(e.Graphics, text, _cbMultiFeed.Font, bounds, textColor, flags);

        if (!supported && (e.State & DrawItemState.Selected) != DrawItemState.Selected)
        {
            var suffix = " (nicht unterstützt)";
            var textSize = TextRenderer.MeasureText(e.Graphics, text + "  ", _cbMultiFeed.Font, bounds.Size, flags);
            var suffixRect = new Rectangle(bounds.X + textSize.Width, bounds.Y, bounds.Width - textSize.Width, bounds.Height);
            TextRenderer.DrawText(e.Graphics, suffix, _cbMultiFeed.Font, suffixRect, ThemeManager.Current.TextMuted, flags);
        }
    }

    private void OpenCarrierSheetDialog()
    {
        using var dlg = new CarrierSheetDialog
        {
            CarrierSheetEnabled = _settings.CarrierSheetEnabled,
            OutputSize = _settings.CarrierSheetOutputSize,
            CustomWidth = _settings.CarrierSheetCustomWidth,
            CustomHeight = _settings.CarrierSheetCustomHeight
        };

        if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
        {
            _settings.CarrierSheetEnabled = dlg.CarrierSheetEnabled;
            _settings.CarrierSheetOutputSize = dlg.OutputSize;
            _settings.CarrierSheetCustomWidth = dlg.CustomWidth;
            _settings.CarrierSheetCustomHeight = dlg.CustomHeight;
        }
    }
}
