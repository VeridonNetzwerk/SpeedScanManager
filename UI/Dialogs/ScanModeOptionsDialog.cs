using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Sub-dialog for scan mode options: brightness slider (B/W only),
/// blank page deletion, deskew, auto-rotate, face-up feeding.
/// Modern layout (<see cref="ModernWindow"/>): a brightness card and a card with switches.
/// </summary>
internal class ScanModeOptionsDialog : ModernWindow
{
    private readonly ModernSlider _brightnessSlider;
    private readonly Label _lblBrightnessValue;
    private readonly CheckBox _chkTextOnly;
    private readonly CheckBox _chkDeleteBlankPages;
    private readonly CheckBox _chkDeskew;
    private readonly CheckBox _chkAutoRotate;
    private readonly CheckBox _chkFaceUp;

    private readonly ColorMode _colorMode;

    public int Brightness { get; set; }
    public bool TextOnlySettings { get; set; }
    public bool AllowDeleteBlankPages { get; set; } = true;
    public bool AllowDeskew { get; set; }
    public bool AllowAutoRotate { get; set; } = true;
    public bool FaceUpFeeding { get; set; }

    /// <param name="colorMode">Current color mode – brightness slider only enabled for BlackWhite.</param>
    public ScanModeOptionsDialog(ColorMode colorMode)
    {
        _colorMode = colorMode;

        SuspendLayout();
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        Text = "Scanmodus-Optionen";
        Title = "Scanmodus-Optionen";
        Subtitle = "Helligkeit und Bildverarbeitung";
        HeaderButtons = ModernHeaderButtons.Help | ModernHeaderButtons.Close;
        HelpTopic = "scanmode-options";
        HeaderIcon = ColorIcon.Tile(Glyph.Sliders, IconColors.Blue);
        AutoSizeToContent = true;
        MinimumContentWidth = 480;

        bool brightnessEnabled = colorMode == ColorMode.BlackWhite;

        // === Brightness card ===
        var picLeftIcon = new ColorIconBox(ColorIcon.Tile(Glyph.Moon, IconColors.Graphite)) { Faded = !brightnessEnabled };

        // Slider: negative values (left) = darker, positive (right) = lighter (TWAIN brightness)
        var lblDark = new Label { Text = "Dunkel", AutoSize = true };
        var lblBright = new Label { Text = "Hell", AutoSize = true };
        ThemeManager.SetRole(lblDark, ThemeRole.Muted);
        ThemeManager.SetRole(lblBright, ThemeRole.Muted);

        var picRightIcon = new ColorIconBox(ColorIcon.Tile(Glyph.Sun, IconColors.Amber)) { Faded = !brightnessEnabled };

        // Themed slider (same as the Dateigröße page): -3 .. +3, keyboard and mouse as before
        _brightnessSlider = new ModernSlider
        {
            Size = new Size(250, 46),
            Minimum = -3,
            Maximum = 3,
            Value = 0,
            TickLabels = new[] { "-3", "-2", "-1", "0", "+1", "+2", "+3" },
            Enabled = brightnessEnabled,
            AccessibleName = "Helligkeit"
        };
        _brightnessSlider.ValueChanged += (s, e) => UpdateBrightnessLabel();

        _lblBrightnessValue = new Label
        {
            Text = "Normal",
            AutoSize = false,
            Size = new Size(250, 20),
            Font = ThemeFonts.Semibold,
            TextAlign = ContentAlignment.MiddleCenter
        };
        ThemeManager.SetRole(_lblBrightnessValue, ThemeRole.Accent);

        var strip = new BrightnessStrip(picLeftIcon, lblDark, _brightnessSlider, lblBright, picRightIcon, _lblBrightnessValue);

        var cardBrightness = new SettingsCard("Helligkeit", "Nur für Schwarzweiß-Scans (Farbmodus \"Schwarzweiß\").")
        {
            Dense = true,
            Icon = ColorIcon.Tile(Glyph.Sun, IconColors.Amber)
        };
        cardBrightness.Add(strip);

        // === Options card (switches) ===
        _chkTextOnly = new ToggleSwitch();
        _chkDeleteBlankPages = new ToggleSwitch { Checked = true };
        _chkDeskew = new ToggleSwitch();
        _chkAutoRotate = new ToggleSwitch { Checked = true };
        _chkFaceUp = new ToggleSwitch();

        var cardOptions = new SettingsCard("Bildverarbeitung") { Dense = true, Icon = ColorIcon.Tile(Glyph.Image, IconColors.Teal) };
        cardOptions.AddRow("Einstellung nur für Textdokumente", null, _chkTextOnly).Icon = ColorIcon.Tile(Glyph.FileType, IconColors.Blue);
        cardOptions.AddRow("Automatisches Löschen leerer Seiten zulassen", null, _chkDeleteBlankPages).Icon = ColorIcon.Tile(Glyph.PageBlank, IconColors.Red);
        cardOptions.AddRow("Schräglauf automatisch korrigieren", null, _chkDeskew).Icon = ColorIcon.Tile(Glyph.Skew, IconColors.Teal);
        cardOptions.AddRow("Automatische Bilddrehung zulassen", null, _chkAutoRotate).Icon = ColorIcon.Tile(Glyph.Rotate, IconColors.Purple);
        cardOptions.AddRow("Dokumente mit der Vorderseite nach oben einlegen", null, _chkFaceUp).Icon = ColorIcon.Tile(Glyph.PageSingle, IconColors.Orange);

        var stack = new ModernStack { Dock = DockStyle.Fill, Padding = new Padding(16, 16, 16, 16), Spacing = 12 };
        stack.Controls.Add(cardBrightness);
        stack.Controls.Add(cardOptions);
        Body.Controls.Add(stack);

        // === Footer ===
        var btnCancel = AddFooterButton("Abbrechen", ThemeRole.Secondary, Glyph.Cross, DialogResult.Cancel);
        var btnOk = AddFooterButton("OK", ThemeRole.Primary, Glyph.Check, DialogResult.OK);

        AcceptButton = btnOk;
        CancelButton = btnCancel;
        ResumeLayout(false);

        ThemeManager.Apply(this);
    }

    private void UpdateBrightnessLabel()
    {
        int val = _brightnessSlider.Value;
        _lblBrightnessValue.Text = val switch
        {
            0 => "Normal",
            _ when val < 0 => $"Dunkler {val}",
            _ => $"Heller +{val}"
        };
    }

    protected override void OnShown(EventArgs e)
    {
        // Restore settings
        _brightnessSlider.Value = Math.Clamp(Brightness, -3, 3);
        _chkTextOnly.Checked = TextOnlySettings;
        _chkDeleteBlankPages.Checked = AllowDeleteBlankPages;
        _chkDeskew.Checked = AllowDeskew;
        _chkAutoRotate.Checked = AllowAutoRotate;
        _chkFaceUp.Checked = FaceUpFeeding;

        UpdateBrightnessLabel();
        base.OnShown(e);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK)
        {
            Brightness = _brightnessSlider.Value;
            TextOnlySettings = _chkTextOnly.Checked;
            AllowDeleteBlankPages = _chkDeleteBlankPages.Checked;
            AllowDeskew = _chkDeskew.Checked;
            AllowAutoRotate = _chkAutoRotate.Checked;
            FaceUpFeeding = _chkFaceUp.Checked;
        }
        base.OnFormClosing(e);
    }

    /// <summary>
    /// Brightness row: icon, "Dunkel", slider, "Hell", icon in one line, the value label
    /// centered under the slider.
    /// </summary>
    private sealed class BrightnessStrip : Panel
    {
        private readonly Control _leftIcon, _lblDark, _slider, _lblBright, _rightIcon, _value;

        public BrightnessStrip(Control leftIcon, Control lblDark, Control slider, Control lblBright, Control rightIcon, Control value)
        {
            _leftIcon = leftIcon;
            _lblDark = lblDark;
            _slider = slider;
            _lblBright = lblBright;
            _rightIcon = rightIcon;
            _value = value;
            Height = 82;
            Margin = Padding.Empty;
            Controls.AddRange(new[] { leftIcon, lblDark, slider, lblBright, rightIcon, value });
        }

        private int S(float v) => ModernLayout.Scale(this, v);

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            int sliderH = _slider.Height;
            int top = S(10);
            int lineY = top + S(12); // vertical center of the slider track
            int x = 0;
            _leftIcon.Location = new Point(x, lineY - _leftIcon.Height / 2);
            x = _leftIcon.Right + S(6);
            _lblDark.Location = new Point(x, lineY - _lblDark.Height / 2);
            int left = _lblDark.Right + S(8);

            int r = Width;
            _rightIcon.Location = new Point(r - _rightIcon.Width, lineY - _rightIcon.Height / 2);
            r = _rightIcon.Left - S(6);
            _lblBright.Location = new Point(r - _lblBright.Width, lineY - _lblBright.Height / 2);
            int right = _lblBright.Left - S(8);

            _slider.SetBounds(left, top, Math.Max(S(60), right - left), sliderH);
            _value.SetBounds(_slider.Left, _slider.Bottom + S(2), _slider.Width, _value.Height);
        }
    }
}
