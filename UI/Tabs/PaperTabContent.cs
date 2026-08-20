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

    public PaperSizeItem(string displayText, PaperSizeMode value)
    {
        DisplayText = displayText;
        Value = value;
    }

    public override string ToString() => DisplayText;
}

internal class PaperTabContent : Panel
{
    private readonly ComboBox _cbPaperSize;
    private readonly Button _btnCustomSize;
    private readonly Button _btnCarrierSheet;
    private readonly ComboBox _cbMultiFeed;
    private readonly ScanSettings _settings;
    private bool _ultrasoundSupported = true;
    private bool _lengthSupported = true;

    public PaperTabContent(ScanSettings settings)
    {
        _settings = settings;

        var font = new Font("Microsoft Sans Serif", 8.25f);
        Dock = DockStyle.Fill;
        BackColor = Color.White;

        // === Info bar ===
        var infoBar = new GradientInfoBar("Wählen Sie ein Papier.");

        // === Paper size row ===
        // Container with Padding to create left/right offsets (Margin is ignored on Dock=Fill)
        var lblPaperSize = new Label
        {
            Text = "Papiergröße:",
            AutoSize = true,
            Font = font,
            Anchor = AnchorStyles.Left,
            Margin = Padding.Empty
        };

        _cbPaperSize = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = font,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Margin = Padding.Empty,
            DropDownWidth = 450,
            MaxDropDownItems = 13,
            IntegralHeight = false
        };
        _cbPaperSize.Items.AddRange(new object[]
        {
            new PaperSizeItem("Automatische Erkennung", PaperSizeMode.Automatic),
            new PaperSizeItem("A3 (297 x 420 mm)", PaperSizeMode.A3),
            new PaperSizeItem("A4 (210 x 297 mm)", PaperSizeMode.A4),
            new PaperSizeItem("A5 (148 x 210 mm)", PaperSizeMode.A5),
            new PaperSizeItem("A6 (105 x 148 mm)", PaperSizeMode.A6),
            new PaperSizeItem("B4(JIS) (257 x 364 mm)", PaperSizeMode.B4Jis),
            new PaperSizeItem("B5(JIS) (182 x 257 mm)", PaperSizeMode.B5Jis),
            new PaperSizeItem("B6(JIS) (128 x 182 mm)", PaperSizeMode.B6Jis),
            new PaperSizeItem("Postkarte (100 x 148 mm)", PaperSizeMode.Postcard),
            new PaperSizeItem("Visitenkarte (55 x 91 mm)", PaperSizeMode.BusinessCard),
            new PaperSizeItem("Letter (8,5 x 11 in. (216 x 279,4 mm))", PaperSizeMode.Letter),
            new PaperSizeItem("Double Letter (11 x 17 in. (279,4 x 431,8 mm))", PaperSizeMode.DoubleLetter),
            new PaperSizeItem("Legal (8,5 x 14 in. (216 x 355,6 mm))", PaperSizeMode.Legal)
        });
        _cbPaperSize.SelectedIndex = 0;
        _cbPaperSize.SelectedIndexChanged += (s, e) => OnPaperSizeChanged();

        var paperSizeContainer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(92, 6, 24, 0),
            BackColor = Color.White
        };
        var paperSizeTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            ColumnStyles =
            {
                new ColumnStyle(SizeType.Absolute, 84f),
                new ColumnStyle(SizeType.Percent, 100f)
            },
            RowCount = 1,
            RowStyles = { new RowStyle(SizeType.Percent, 100f) },
            Margin = Padding.Empty,
            BackColor = Color.White
        };
        paperSizeTable.Controls.Add(lblPaperSize, 0, 0);
        paperSizeTable.Controls.Add(_cbPaperSize, 1, 0);
        paperSizeContainer.Controls.Add(paperSizeTable);

        // === Button row ===
        // 3-column TableLayoutPanel: left padding | carrier button | flexible | custom button | right padding
        _btnCarrierSheet = new Button
        {
            Text = "Trägerblatteinstellungen...",
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            Size = new Size(200, 25),
            Font = font,
            Image = TabIcons.CreateCarrierIcon(),
            ImageAlign = ContentAlignment.MiddleLeft,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(2, 0, 4, 0),
            Anchor = AnchorStyles.Left,
            Margin = Padding.Empty
        };
        _btnCarrierSheet.Click += (s, e) => OpenCarrierSheetDialog();

        _btnCustomSize = new Button
        {
            Text = "Benutzerdefiniert...",
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            Size = new Size(160, 25),
            Font = font,
            Enabled = true,
            Image = TabIcons.CreateRulerIcon(),
            ImageAlign = ContentAlignment.MiddleLeft,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(2, 0, 4, 0),
            Anchor = AnchorStyles.Right,
            Margin = Padding.Empty,
            AutoSize = false
        };
        _btnCustomSize.Click += (s, e) => OpenCustomSizeDialog();

        var buttonTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            ColumnStyles =
            {
                new ColumnStyle(SizeType.Absolute, 176f),  // left padding
                new ColumnStyle(SizeType.Absolute, 200f),  // carrier button
                new ColumnStyle(SizeType.Percent, 100f),   // flexible gap
                new ColumnStyle(SizeType.Absolute, 160f),  // custom button
                new ColumnStyle(SizeType.Absolute, 19f)    // right padding
            },
            RowCount = 1,
            RowStyles = { new RowStyle(SizeType.Percent, 100f) },
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Color.White
        };
        buttonTable.Controls.Add(new Panel { BackColor = Color.White, Dock = DockStyle.Fill }, 0, 0);
        buttonTable.Controls.Add(_btnCarrierSheet, 1, 0);
        buttonTable.Controls.Add(new Panel { BackColor = Color.White, Dock = DockStyle.Fill }, 2, 0);
        buttonTable.Controls.Add(_btnCustomSize, 3, 0);
        buttonTable.Controls.Add(new Panel { BackColor = Color.White, Dock = DockStyle.Fill }, 4, 0);

        // === Multi-feed row ===
        var lblMultiFeed = new Label
        {
            Text = "Mehrfacheinzugserkennung:",
            AutoSize = true,
            Font = font,
            Anchor = AnchorStyles.Left,
            Margin = Padding.Empty
        };

        _cbMultiFeed = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = font,
            DrawMode = DrawMode.OwnerDrawFixed,
            Size = new Size(230, 24),
            Anchor = AnchorStyles.Left,
            Margin = Padding.Empty,
            DropDownWidth = 230
        };
        _cbMultiFeed.DrawItem += (s, e) => DrawMultiFeedItem(e);
        _cbMultiFeed.Items.AddRange(new object[]
        {
            "Aus",
            "Überprüfung von Überlappung [Ultraschall]",
            "Überprüfung der Länge",
            "Überprüfung von Überlappung und Länge"
        });
        _cbMultiFeed.SelectedIndex = (int)MultiFeedDetection.Off;
        _cbMultiFeed.SelectedIndexChanged += (s, e) =>
        {
            if (_cbMultiFeed.SelectedIndex < 0) return;
            var selected = (MultiFeedDetection)_cbMultiFeed.SelectedIndex;
            if (!IsMultiFeedModeSupported(selected))
            {
                _cbMultiFeed.SelectedIndex = (int)MultiFeedDetection.Off;
                _settings.MultiFeedDetection = MultiFeedDetection.Off;
                return;
            }
            _settings.MultiFeedDetection = selected;
        };

        var multiFeedContainer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(92, 4, 24, 0),
            BackColor = Color.White
        };
        var multiFeedTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            ColumnStyles =
            {
                new ColumnStyle(SizeType.Absolute, 175f),
                new ColumnStyle(SizeType.Absolute, 190f)
            },
            RowCount = 1,
            RowStyles = { new RowStyle(SizeType.Percent, 100f) },
            Margin = Padding.Empty,
            BackColor = Color.White
        };
        multiFeedTable.Controls.Add(lblMultiFeed, 0, 0);
        multiFeedTable.Controls.Add(_cbMultiFeed, 1, 0);
        multiFeedContainer.Controls.Add(multiFeedTable);

        // === Main layout: TableLayoutPanel with 6 rows ===
        // Fixed rows for controls, flexible space only at the bottom.
        var mainTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            ColumnStyles = { new ColumnStyle(SizeType.Percent, 100f) },
            RowCount = 6,
            RowStyles =
            {
                new RowStyle(SizeType.Absolute, 20f),   // Row 0: Info bar
                new RowStyle(SizeType.Absolute, 36f),   // Row 1: Paper size
                new RowStyle(SizeType.Absolute, 30f),   // Row 2: Hint text
                new RowStyle(SizeType.Absolute, 35f),   // Row 3: Button row
                new RowStyle(SizeType.Absolute, 38f),   // Row 4: Multi-feed row
                new RowStyle(SizeType.Percent, 100f)    // Row 5: Flexible remainder at bottom
            },
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Color.White
        };

        var lblHint = new Label
        {
            Text = "Die automatische Papiergrößenerkennung steht für das Flachbettscannen nicht zur Verfügung.",
            Font = font,
            ForeColor = Color.FromArgb(90, 90, 90),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(176, 0, 0, 0),
            BackColor = Color.White
        };

        mainTable.Controls.Add(infoBar, 0, 0);
        mainTable.Controls.Add(paperSizeContainer, 0, 1);
        mainTable.Controls.Add(lblHint, 0, 2);
        mainTable.Controls.Add(buttonTable, 0, 3);
        mainTable.Controls.Add(multiFeedContainer, 0, 4);
        mainTable.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Color.White }, 0, 5);

        Controls.Add(mainTable);
    }

    private void OnPaperSizeChanged()
    {
        if (_cbPaperSize.SelectedItem is PaperSizeItem item)
        {
            _settings.PaperSize = item.Value;
        }
    }

    private void OpenCustomSizeDialog()
    {
        using var dialog = new CustomSizeDialog(_settings.CustomPaperSizes);

        if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
        {
            _settings.CustomPaperSizes.Clear();
            _settings.CustomPaperSizes.AddRange(dialog.CustomSizes);

            if (_settings.CustomPaperSizes.Count > 0)
            {
                _settings.PaperSize = PaperSizeMode.Custom;
            }
            else
            {
                _settings.PaperSize = PaperSizeMode.Automatic;
                _cbPaperSize.SelectedIndex = 0;
            }
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
        if (e.Index < 0) return;
        e.DrawBackground();

        var mode = (MultiFeedDetection)e.Index;
        bool supported = IsMultiFeedModeSupported(mode);
        var text = _cbMultiFeed.Items[e.Index]?.ToString() ?? "";

        var textColor = supported
            ? (e.State & DrawItemState.Selected) == DrawItemState.Selected
                ? SystemColors.HighlightText
                : SystemColors.ControlText
            : SystemColors.GrayText;

        var flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
        TextRenderer.DrawText(e.Graphics, text, _cbMultiFeed.Font, e.Bounds, textColor, flags);

        if (!supported && (e.State & DrawItemState.Selected) != DrawItemState.Selected)
        {
            var suffix = " (nicht unterstützt)";
            var textSize = TextRenderer.MeasureText(e.Graphics, text + "  ", _cbMultiFeed.Font, e.Bounds.Size, flags);
            var suffixRect = new Rectangle(e.Bounds.X + textSize.Width, e.Bounds.Y, e.Bounds.Width - textSize.Width, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, suffix, _cbMultiFeed.Font, suffixRect, Color.Gray, flags);
        }

        e.DrawFocusRectangle();
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
