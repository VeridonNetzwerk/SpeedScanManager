using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Content panel for the "Dateigröße" tab.
/// Compression rate slider (1-5) with wedge-shaped gradient indicators,
/// matching the ScanSnap Manager reference layout.
/// </summary>
internal class FileSizeTabContent : Panel
{
    private readonly TrackBar _slider;
    private readonly NumericUpDown _numValue;
    private readonly ScanSettings _settings;

    private readonly Panel _centerPanel;
    private readonly Panel _topWedge;
    private readonly Panel _bottomWedge;
    private readonly Panel _numBox;
    private readonly Panel _separatorLine;
    private readonly Label _numDisplay;

    // Layout constants — exact pixel positions per ScanSnap reference
    private const int GroupWidth = 530;
    private const int GroupHeight = 150;

    private const int HeaderX = 0;
    private const int HeaderY = 0;

    private const int CompressionLabelX = 24;
    private const int CompressionLabelY = 26;

    private const int FileSizeLabelX = 29;
    private const int FileSizeLabelY = 74;

    private const int TopLowX = 133;
    private const int TopLabelY = 18;

    private const int WedgeX = 192;
    private const int TopWedgeY = 9;
    private const int BottomWedgeY = 89;

    private const int TopWedgeWidth = 182;
    private const int BottomWedgeWidth = 185;
    private const int WedgeHeight = 18;

    private const int HighLabelX = 402;
    private const int BottomSideLabelY = 94;

    private const int SliderX = 152;
    private const int SliderY = 36;
    private const int SliderWidth = 270;
    private const int SliderHeight = 45;

    private const int NumberX = 439;
    private const int NumberY = 36;
    private const int NumberWidth = 40;
    private const int NumberHeight = 40;

    private const int NormalY = 111;

    public void ApplyPreset(int compression)
    {
        _settings.CompressionRate = compression;
        _slider.Value = compression;
        _numValue.Value = compression;
    }

    public FileSizeTabContent(ScanSettings settings)
    {
        _settings = settings;

        var font = new Font("Microsoft Sans Serif", 8.25f);
        var smallFont = new Font("Microsoft Sans Serif", 8.25f);
        var boldFont = new Font("Microsoft Sans Serif", 8.25f, FontStyle.Bold);
        Dock = DockStyle.Fill;
        BackColor = Color.White;

        // === Info bar ===
        var infoBar = new GradientInfoBar("Geben Sie die Komprimierungsrate an (nur für Farb- und Graustufenbilder).");

        // === Centered container ===
        _centerPanel = new Panel
        {
            BackColor = Color.White,
            Size = new Size(GroupWidth, GroupHeight),
            Anchor = AnchorStyles.None
        };

        // === Row 1: Header ===
        var lblHeader = new Label
        {
            Text = "Komprimierungsrate:",
            Location = new Point(HeaderX, HeaderY),
            AutoSize = true,
            Font = boldFont,
            BackColor = Color.Transparent
        };
        _centerPanel.Controls.Add(lblHeader);

        // === Row 2: (Niedrig) + top wedge + (Hoch) ===
        var lblLow = new Label
        {
            Text = "(Niedrig)",
            Location = new Point(TopLowX, TopLabelY),
            AutoSize = true,
            Font = smallFont,
            BackColor = Color.Transparent
        };
        _centerPanel.Controls.Add(lblLow);

        _topWedge = new Panel
        {
            Location = new Point(WedgeX, TopWedgeY),
            Size = new Size(TopWedgeWidth, WedgeHeight),
            BackColor = Color.White,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _topWedge.Paint += (sender, e) => PaintTopWedge(e.Graphics, _topWedge.ClientRectangle);
        _centerPanel.Controls.Add(_topWedge);

        var lblHigh = new Label
        {
            Text = "(Hoch)",
            Location = new Point(HighLabelX, TopLabelY),
            AutoSize = true,
            Font = smallFont,
            BackColor = Color.Transparent
        };
        _centerPanel.Controls.Add(lblHigh);

        // === Row 3: Komprimierung + slider + number box ===
        var lblCompression = new Label
        {
            Text = "Komprimierung",
            Location = new Point(CompressionLabelX, CompressionLabelY),
            AutoSize = true,
            Font = smallFont,
            ForeColor = Color.FromArgb(80, 80, 80),
            BackColor = Color.Transparent
        };
        _centerPanel.Controls.Add(lblCompression);

        // 3D frame around slider — like selected category tab
        var sliderFrame = new Panel
        {
            Location = new Point(SliderX - 2, SliderY - 2),
            Size = new Size(SliderWidth + 4, SliderHeight + 4),
            BackColor = Color.White,
            BorderStyle = BorderStyle.Fixed3D,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        sliderFrame.Paint += (sender, e) =>
        {
            // Blue accent line on top — like selected category tab
            using var accentPen = new Pen(Color.FromArgb(45, 90, 170), 2f);
            e.Graphics.DrawLine(accentPen, 0, 0, sliderFrame.ClientSize.Width - 1, 0);
        };
        _centerPanel.Controls.Add(sliderFrame);

        _slider = new TrackBar
        {
            Location = new Point(SliderX, SliderY),
            Size = new Size(SliderWidth, SliderHeight),
            Minimum = 1,
            Maximum = 5,
            Value = Math.Clamp(_settings.CompressionRate, 1, 5),
            TickFrequency = 1,
            SmallChange = 1,
            LargeChange = 1,
            TickStyle = TickStyle.BottomRight,
            BackColor = Color.White,
            Margin = Padding.Empty
        };
        _centerPanel.Controls.Add(_slider);

        _numBox = new Panel
        {
            Location = new Point(NumberX, NumberY),
            Size = new Size(NumberWidth, NumberHeight),
            BackColor = Color.White,
            BorderStyle = BorderStyle.Fixed3D,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _numBox.Paint += (sender, e) =>
        {
            // Blue accent line on top — like selected category tab
            using var accentPen = new Pen(Color.FromArgb(45, 90, 170), 2f);
            e.Graphics.DrawLine(accentPen, 0, 0, _numBox.ClientSize.Width - 1, 0);
        };
        _centerPanel.Controls.Add(_numBox);

        // === Separator line between Komprimierung and Dateigröße ===
        _separatorLine = new Panel
        {
            Location = new Point(24, 57),
            Size = new Size(86, 1),
            BackColor = Color.FromArgb(145, 145, 145),
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _centerPanel.Controls.Add(_separatorLine);

        _numDisplay = new Label
        {
            Dock = DockStyle.Fill,
            Text = _settings.CompressionRate.ToString(),
            Font = new Font("Microsoft Sans Serif", 14f, FontStyle.Regular),
            ForeColor = Color.FromArgb(35, 35, 35),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.White,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _numBox.Controls.Add(_numDisplay);

        // Hidden NumericUpDown for data binding
        _numValue = new NumericUpDown
        {
            Location = new Point(NumberX, NumberY),
            Size = new Size(NumberWidth, NumberHeight),
            Font = font,
            Minimum = 1,
            Maximum = 5,
            Value = _settings.CompressionRate,
            Visible = false
        };
        _centerPanel.Controls.Add(_numValue);

        // Bidirectional sync — only number changes
        _slider.ValueChanged += (s, e) =>
        {
            int value = _slider.Value;
            _settings.CompressionRate = value;
            if (_numValue.Value != value)
                _numValue.Value = value;
            _numDisplay.Text = value.ToString();
        };

        _numValue.ValueChanged += (s, e) =>
        {
            if (_slider.Value != (int)_numValue.Value)
                _slider.Value = (int)_numValue.Value;
            _settings.CompressionRate = (int)_numValue.Value;
            _numDisplay.Text = ((int)_numValue.Value).ToString();
        };

        // === Row 4: Dateigröße (own line, above bottom wedge) ===
        var lblFileSize = new Label
        {
            Text = "Dateigröße",
            Location = new Point(FileSizeLabelX, FileSizeLabelY),
            AutoSize = true,
            Font = smallFont,
            ForeColor = Color.FromArgb(80, 80, 80),
            BackColor = Color.Transparent
        };
        _centerPanel.Controls.Add(lblFileSize);

        // === Row 5: (Groß) + bottom wedge + (Klein) ===
        var lblSizeLarge = new Label
        {
            Text = "(Groß)",
            Location = new Point(135, BottomSideLabelY),
            AutoSize = true,
            Font = smallFont,
            BackColor = Color.Transparent
        };
        _centerPanel.Controls.Add(lblSizeLarge);

        _bottomWedge = new Panel
        {
            Location = new Point(WedgeX, BottomWedgeY),
            Size = new Size(BottomWedgeWidth, WedgeHeight),
            BackColor = Color.White,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _bottomWedge.Paint += (sender, e) => PaintBottomWedge(e.Graphics, _bottomWedge.ClientRectangle);
        _centerPanel.Controls.Add(_bottomWedge);

        var lblSizeSmall = new Label
        {
            Text = "(Klein)",
            Location = new Point(HighLabelX, BottomSideLabelY),
            AutoSize = true,
            Font = smallFont,
            BackColor = Color.Transparent
        };
        _centerPanel.Controls.Add(lblSizeSmall);

        // === Row 6: Single static "Normal" centered under bottom wedge ===
        var lblNormal = new Label
        {
            Text = "Normal",
            AutoSize = true,
            Font = new Font("Microsoft Sans Serif", 8.25f, FontStyle.Regular),
            ForeColor = Color.FromArgb(35, 35, 35),
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        lblNormal.Location = new Point(
            WedgeX + (BottomWedgeWidth - lblNormal.PreferredWidth) / 2,
            NormalY);
        _centerPanel.Controls.Add(lblNormal);

        Controls.AddRange(new Control[] { infoBar, _centerPanel });

        // Ensure slider and number box are always on top of decorative panels
        _slider.BringToFront();
        _numBox.BringToFront();

        Dock = DockStyle.Fill;
        Resize += (s, e) => CenterPanel();
        HandleCreated += (s, e) => CenterPanel();
    }

    private void CenterPanel()
    {
        int x = (ClientSize.Width - _centerPanel.Width) / 2;
        int y = 28;
        _centerPanel.Location = new Point(Math.Max(0, x), y);
    }

    /// <summary>
    /// Top wedge: right triangle, point at bottom-left (near slider),
    /// wide at top-right (away from slider). Right angle at bottom-right.
    /// P1 = (Left, Bottom), P2 = (Right, Top), P3 = (Right, Bottom)
    /// </summary>
    private static void PaintTopWedge(Graphics g, Rectangle bounds)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.CompositingMode = CompositingMode.SourceOver;

        PointF[] points =
        {
            new(bounds.Left, bounds.Bottom - 1),
            new(bounds.Right - 1, bounds.Top),
            new(bounds.Right - 1, bounds.Bottom - 1)
        };

        using var path = new GraphicsPath();
        path.AddPolygon(points);

        using var brush = new LinearGradientBrush(
            bounds,
            Color.FromArgb(255, 239, 239),
            Color.FromArgb(211, 67, 48),
            LinearGradientMode.Horizontal);

        g.FillPath(brush, path);
    }

    /// <summary>
    /// Bottom wedge: right triangle, point at top-right (near slider),
    /// wide at bottom-left (away from slider). Right angle at top-left.
    /// P1 = (Left, Top), P2 = (Left, Bottom), P3 = (Right, Top)
    /// </summary>
    private static void PaintBottomWedge(Graphics g, Rectangle bounds)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.CompositingMode = CompositingMode.SourceOver;

        PointF[] points =
        {
            new(bounds.Left, bounds.Top),
            new(bounds.Left, bounds.Bottom - 1),
            new(bounds.Right - 1, bounds.Top)
        };

        using var path = new GraphicsPath();
        path.AddPolygon(points);

        using var brush = new LinearGradientBrush(
            bounds,
            Color.FromArgb(38, 125, 220),
            Color.FromArgb(220, 239, 255),
            LinearGradientMode.Horizontal);

        g.FillPath(brush, path);
    }
}
