using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// "Update verfügbar": version, what's new and "Jetzt installieren" (downloads the setup with
/// progress, then hands it to the tray, which starts it and exits). Compact, like the app's
/// other prompts.
/// </summary>
internal sealed class UpdateDialog : ModernWindow
{
    private const int TextWidth = 440;

    private readonly UpdateInfo _info;
    private readonly Func<string?> _blockedReason;
    private readonly NotesView _notes;
    private readonly ProgressLine _progress;
    private readonly Button _btnInstall;
    private readonly Button _btnLater;
    private CancellationTokenSource? _download;

    /// <summary>The setup was downloaded; the receiver starts it (and exits the app).</summary>
    public event Action<string>? SetupReady;

    /// <param name="blockedReason">Returns why installing isn't possible right now (a scan runs), or null.</param>
    public UpdateDialog(UpdateInfo info, Func<string?> blockedReason)
    {
        _info = info;
        _blockedReason = blockedReason;
        SuspendLayout();
        Text = "SpeedScan Manager – Update";
        Title = $"Version {UpdateService.Format(info.Version)} ist da";
        Subtitle = $"Installiert ist {UpdateService.CurrentVersionText}";
        HeaderIcon = ColorIcon.Tile(Glyph.Download, IconColors.Green);
        HeaderButtons = ModernHeaderButtons.Close;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = true;

        var stack = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 16, 20, 6),
        };
        var heading = new Label
        {
            Text = "Was ist neu",
            AutoSize = true,
            Font = ThemeFonts.Heading,
            Margin = new Padding(0, 0, 0, 8),
            UseMnemonic = false,
        };
        stack.Controls.Add(heading);
        var notes = UpdateService.PlainNotes(info.Notes);
        _notes = new NotesView(string.IsNullOrWhiteSpace(notes) ? "Fehlerbehebungen und Verbesserungen." : notes)
        {
            Width = TextWidth,
            Margin = new Padding(0, 0, 0, 10),
        };
        stack.Controls.Add(_notes);
        _progress = new ProgressLine { Width = TextWidth, Height = 34, Margin = new Padding(0, 0, 0, 4) };
        stack.Controls.Add(_progress);
        Body.Controls.Add(stack);

        var github = AddFooterButton("Auf GitHub", ThemeRole.Secondary, Glyph.OpenExternal, DialogResult.None, alignLeft: true);
        github.Click += (_, _) => OpenPage();
        _btnLater = AddFooterButton("Später", ThemeRole.Secondary, Glyph.None, DialogResult.Cancel);
        _btnInstall = AddFooterButton("Jetzt installieren", ThemeRole.Primary, Glyph.Download, DialogResult.None);
        _btnInstall.Click += async (_, _) => await InstallAsync();
        AcceptButton = _btnInstall;
        CancelButton = _btnLater;

        ClientSize = new Size(20 + TextWidth + 20, HeaderHeight + 16 + 24 + _notes.Height + 10 + _progress.Height + 4 + 6 + 56);
        ThemeManager.Apply(this);
        ResumeLayout(true);
        UpdateBlocked();
        FormClosing += (_, _) => _download?.Cancel();
    }

    /// <summary>Re-evaluates whether installing is possible (scan started/finished).</summary>
    public void UpdateBlocked()
    {
        if (_download != null) return;
        var reason = _blockedReason();
        _btnInstall.Enabled = reason == null;
        _progress.SetText(reason ?? "", ProgressLine.Mode.Text);
    }

    private void OpenPage()
    {
        try { Process.Start(new ProcessStartInfo(_info.PageUrl) { UseShellExecute = true }); }
        catch (Exception ex) { DiagLog.Write($"[Update] open page failed: {ex.Message}"); }
    }

    private async Task InstallAsync()
    {
        if (_download != null || _blockedReason() != null) { UpdateBlocked(); return; }
        _download = new CancellationTokenSource();
        _btnInstall.Enabled = false;
        _progress.SetProgress(0, "Wird heruntergeladen …");
        var progress = new Progress<double>(v => _progress.SetProgress(v, $"Wird heruntergeladen … {v * 100:0} %"));
        try
        {
            var path = await UpdateService.DownloadAsync(_info, progress, _download.Token);
            if (IsDisposed) return;
            _progress.SetProgress(1, "Setup wird gestartet …");
            SetupReady?.Invoke(path);
        }
        catch (OperationCanceledException)
        {
            // window closed during the download
        }
        catch (Exception ex)
        {
            DiagLog.Write($"[Update] download failed: {ex}");
            if (IsDisposed) return;
            _progress.SetText(ex is HttpRequestException or TaskCanceledException
                ? "Download fehlgeschlagen. Bitte Internetverbindung prüfen und erneut versuchen."
                : ex.Message, ProgressLine.Mode.Error);
        }
        finally
        {
            var cts = _download;
            _download = null;
            cts?.Dispose();
            if (!IsDisposed && _btnInstall.Text == "Jetzt installieren")
                _btnInstall.Enabled = _blockedReason() == null;
        }
    }

    /// <summary>The setup could not be started (UAC declined): allow another try.</summary>
    public void SetupNotStarted(string message)
    {
        if (IsDisposed) return;
        _progress.SetText(message, ProgressLine.Mode.Error);
        _btnInstall.Enabled = _blockedReason() == null;
    }

    /// <summary>Card with the release notes (plain text, clipped to a few lines).</summary>
    private sealed class NotesView : Control, IThemeAware
    {
        private readonly string _text;
        private const int MaxHeight = 190;

        public NotesView(string text)
        {
            _text = text;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            AccessibleRole = AccessibleRole.StaticText;
            AccessibleName = text;
            int h = TextRenderer.MeasureText(text, ThemeFonts.Regular, new Size(TextWidth - 28, 0), Flags).Height;
            Height = Math.Min(MaxHeight, h + 24);
        }

        private const TextFormatFlags Flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl | TextFormatFlags.EndEllipsis;

        public void ApplyTheme(ThemePalette palette) => Invalidate();

        private int S(float v) => ModernLayout.Scale(this, v);

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = ThemeManager.Current;
            var g = e.Graphics;
            g.Clear(ModernLayout.ParentBack(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = ModernLayout.RoundedRect(rect, S(8)))
            {
                using (var brush = new SolidBrush(p.Card)) g.FillPath(brush, path);
                using var pen = new Pen(p.CardBorder);
                g.DrawPath(pen, path);
            }
            TextRenderer.DrawText(g, _text, ThemeFonts.Regular, new Rectangle(S(14), S(12), Width - S(28), Height - S(24)), p.Text, Flags);
        }
    }

    /// <summary>Status line: hint text, or a progress bar with text, or an error.</summary>
    private sealed class ProgressLine : Control, IThemeAware
    {
        public enum Mode { Text, Progress, Error }

        private Mode _mode = Mode.Text;
        private string _text = "";
        private double _value;

        public ProgressLine()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
        }

        public void SetText(string text, Mode mode) { _text = text; _mode = mode; AccessibleName = text; Invalidate(); }

        public void SetProgress(double value, string text) { _value = value; _text = text; _mode = Mode.Progress; AccessibleName = text; Invalidate(); }

        public void ApplyTheme(ThemePalette palette) => Invalidate();

        private int S(float v) => ModernLayout.Scale(this, v);

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = ThemeManager.Current;
            var g = e.Graphics;
            g.Clear(ModernLayout.ParentBack(this));
            const TextFormatFlags F = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
            if (_mode != Mode.Progress)
            {
                TextRenderer.DrawText(g, _text, ThemeFonts.Regular, new Rectangle(0, 0, Width, S(20)),
                    _mode == Mode.Error ? p.Danger : p.TextMuted, F | TextFormatFlags.VerticalCenter);
                return;
            }
            TextRenderer.DrawText(g, _text, ThemeFonts.Regular, new Rectangle(0, 0, Width, S(18)), p.TextMuted, F | TextFormatFlags.VerticalCenter);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float y = S(24), h = S(6);
            var track = new RectangleF(0, y, Width - 1, h);
            using (var path = ModernLayout.RoundedRect(track, h / 2f))
            using (var brush = new SolidBrush(p.IsDark ? p.ButtonSecondary : p.Hover))
                g.FillPath(brush, path);
            if (_value > 0)
            {
                var fill = new RectangleF(0, y, Math.Max(h, (float)((Width - 1) * _value)), h);
                using var path = ModernLayout.RoundedRect(fill, h / 2f);
                using var brush = new SolidBrush(IconColors.Green);
                g.FillPath(brush, path);
            }
        }
    }
}
