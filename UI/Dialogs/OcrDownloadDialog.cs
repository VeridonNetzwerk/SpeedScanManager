using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Installs a missing OCR language: question in the app's design, then the download with a
/// progress bar and "Abbrechen" (see <see cref="OcrData.DownloadAsync"/>).
/// </summary>
internal sealed class OcrDownloadDialog : ModernWindow
{
    private readonly string _code;
    private readonly ProgressBar _bar = new() { Size = new Size(340, 8), Margin = new Padding(0, 10, 0, 0) };
    private readonly Label _status;
    private readonly CancellationTokenSource _cts = new();
    private readonly Button _cancel;
    private bool _running;

    /// <summary>
    /// Asks whether the language <paramref name="code"/> should be installed and downloads it.
    /// True if it is installed afterwards. <paramref name="question"/> replaces the standard text.
    /// <paramref name="declined"/>: the user answered "Nein" (not: the download failed).
    /// </summary>
    public static bool Offer(IWin32Window? owner, string code, string? question = null) => Offer(owner, code, question, out _);

    public static bool Offer(IWin32Window? owner, string code, string? question, out bool declined)
    {
        declined = false;
        if (OcrData.IsInstalled(code)) return true;
        string name = LanguageName(code);
        string text = (question ?? L.F("Die Texterkennung für {0} ist nicht installiert.", name)) + "\n\n" +
                      L.F("Jetzt herunterladen (ca. {0} MB)?", OcrData.DownloadSizeMb(code));
        if (!TrayMessageBox.AskYesNo(owner, text)) { declined = true; return false; }
        using var dialog = new OcrDownloadDialog(code, name);
        var result = owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
        return result == DialogResult.OK && OcrData.IsInstalled(code);
    }

    public static string LanguageName(string code) => FileTypeTabContent.GetOcrLanguageName(OcrData.LanguageOf(code));

    private OcrDownloadDialog(string code, string name)
    {
        _code = code;
        SuspendLayout();
        Text = TrayMessageBox.Caption;
        Title = L.T("Texterkennung installieren");
        HeaderHeight = 52;
        HeaderButtons = ModernHeaderButtons.None;
        CloseOnEscape = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        AutoSizeToContent = true;
        MinimumContentWidth = 380;

        var heading = new Label
        {
            Text = L.F("{0} wird heruntergeladen …", name),
            AutoSize = true,
            Font = ThemeFonts.Heading,
            Margin = new Padding(0, 0, 0, 4),
            UseMnemonic = false
        };
        _status = new Label { Text = L.T("Verbinde …"), AutoSize = true, Margin = Padding.Empty, UseMnemonic = false };
        ThemeManager.SetRole(_status, ThemeRole.Muted);
        var stack = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Padding = new Padding(22, 20, 22, 20)
        };
        stack.Controls.Add(heading);
        stack.Controls.Add(_status);
        stack.Controls.Add(_bar);
        Body.Controls.Add(stack);

        _cancel = AddFooterButton(L.T("Abbrechen"), ThemeRole.Secondary, Glyph.None, DialogResult.None);
        _cancel.Click += (_, _) => { _cancel.Enabled = false; _cts.Cancel(); };
        ThemeManager.Apply(this);
        ResumeLayout(true);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _running = true;
        var progress = new Progress<double>(v =>
        {
            _bar.Value = Math.Clamp(v, 0, 1);
            _status.Text = L.F("{0} % von ca. {1} MB", (int)(v * 100), OcrData.DownloadSizeMb(_code));
        });
        try
        {
            await OcrData.DownloadAsync(_code, progress, _cts.Token);
            DialogResult = DialogResult.OK;
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            DialogResult = DialogResult.Cancel;
        }
        catch (Exception ex)
        {
            DiagLog.Write($"OCR download '{_code}' failed: {ex.Message}");
            _running = false;
            Hide();
            TrayMessageBox.Error(Owner, L.F("Die Sprachdaten konnten nicht heruntergeladen werden:\n\n{0}", ex.Message));
            DialogResult = DialogResult.Cancel;
        }
        finally
        {
            _running = false;
        }
    }

    // Closing (Alt+F4) while running cancels the download instead
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_running && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            _cts.Cancel();
        }
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _cts.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>Thin rounded progress bar in the accent color.</summary>
    private sealed class ProgressBar : Control, IThemeAware
    {
        private double _value;

        public ProgressBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            AccessibleRole = AccessibleRole.ProgressBar;
        }

        public double Value { get => _value; set { _value = value; Invalidate(); } }

        public void ApplyTheme(ThemePalette palette) => Invalidate();

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = ThemeManager.Current;
            var g = e.Graphics;
            g.Clear(ModernLayout.ParentBack(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float h = Height - 1f;
            using (var path = ModernLayout.RoundedRect(new RectangleF(0, 0, Width - 1, h), h / 2f))
            using (var brush = new SolidBrush(p.IsDark ? p.ButtonSecondary : p.Hover))
                g.FillPath(brush, path);
            if (_value > 0)
            {
                using var path = ModernLayout.RoundedRect(new RectangleF(0, 0, Math.Max(h, (float)((Width - 1) * _value)), h), h / 2f);
                using var brush = new SolidBrush(p.Accent);
                g.FillPath(brush, path);
            }
        }
    }
}
