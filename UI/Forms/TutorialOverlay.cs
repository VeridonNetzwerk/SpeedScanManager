using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Guided tour over a window: dims the window, highlights one area per step and shows a
/// speech bubble with title, text and Zurück / Weiter / Überspringen. A layered top-level
/// window owned by the target window (per-pixel alpha), so the controls below need no changes.
/// Keys: Enter/→ next, ← back, Esc skip.
/// </summary>
internal sealed class TutorialOverlay : Form
{
    /// <summary>One step. <see cref="Target"/> returns the area to highlight in screen coordinates (null = none).</summary>
    public sealed record Step(string Title, string Text, Glyph Glyph, Color Color,
        Func<Rectangle?>? Target = null, Action? Enter = null, bool Round = false);

    private enum Hit { None, Next, Back, Skip }

    private readonly Form _target;
    private readonly IReadOnlyList<Step> _steps;
    private int _index;
    private Rectangle _btnNext, _btnBack, _btnSkip;
    private Hit _hover;

    /// <summary>The tour ended (finished or skipped).</summary>
    public event EventHandler? Finished;

    public TutorialOverlay(Form target, IReadOnlyList<Step> steps)
    {
        _target = target;
        _steps = steps;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        KeyPreview = true;
        Text = "Einführung";
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x80000 /* WS_EX_LAYERED */ | 0x80 /* WS_EX_TOOLWINDOW */;
            return cp;
        }
    }

    public void Start()
    {
        _target.LocationChanged += OnTargetMoved;
        _target.SizeChanged += OnTargetMoved;
        _target.FormClosed += OnTargetClosed;
        Bounds = _target.Bounds;
        Show(_target);
        Go(0);
        Activate();
    }

    private void OnTargetMoved(object? sender, EventArgs e)
    {
        if (_target.WindowState == FormWindowState.Minimized) return;
        Bounds = _target.Bounds;
        Render();
    }

    private void OnTargetClosed(object? sender, FormClosedEventArgs e) => End();

    private void Go(int index)
    {
        _index = Math.Clamp(index, 0, _steps.Count - 1);
        _hover = Hit.None;
        _steps[_index].Enter?.Invoke();
        // The step may switch pages: let the window lay out before measuring the target.
        _target.Update();
        Render();
    }

    private void Next()
    {
        if (_index >= _steps.Count - 1) End();
        else Go(_index + 1);
    }

    private bool _ended;

    private void End()
    {
        if (_ended) return;
        _ended = true;
        _target.LocationChanged -= OnTargetMoved;
        _target.SizeChanged -= OnTargetMoved;
        _target.FormClosed -= OnTargetClosed;
        Finished?.Invoke(this, EventArgs.Empty);
        Close();
    }

    // ───────────────────────── input

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Enter or Keys.Right or Keys.Space: Next(); break;
            case Keys.Left: if (_index > 0) Go(_index - 1); break;
            case Keys.Escape: End(); break;
            default: base.OnKeyDown(e); return;
        }
        e.Handled = true;
    }

    private Hit HitTest(Point p) =>
        _btnNext.Contains(p) ? Hit.Next
        : _btnBack.Contains(p) && _index > 0 ? Hit.Back
        : _btnSkip.Contains(p) ? Hit.Skip
        : Hit.None;

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var hit = HitTest(e.Location);
        Cursor = hit == Hit.None ? Cursors.Default : Cursors.Hand;
        if (hit == _hover) return;
        _hover = hit;
        Render();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        if (_hover == Hit.None) return;
        _hover = Hit.None;
        Render();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        switch (HitTest(e.Location))
        {
            case Hit.Next: Next(); break;
            case Hit.Back: Go(_index - 1); break;
            case Hit.Skip: End(); break;
        }
    }

    // ───────────────────────── painting

    private int S(float v) => (int)Math.Round(v * DeviceDpi / 96f);

    private void Render()
    {
        if (!IsHandleCreated || Width <= 0 || Height <= 0) return;
        using var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            PaintFrame(g);
        }
        Present(bmp);
    }

    private void PaintFrame(Graphics g)
    {
        var p = ThemeManager.Current;
        var step = _steps[_index];
        var full = new Rectangle(0, 0, Width, Height);

        Rectangle? spot = null;
        if (step.Target?.Invoke() is Rectangle screen && !screen.IsEmpty)
        {
            var r = screen;
            r.Offset(-Left, -Top);
            r.Inflate(S(step.Round ? 4 : 6), S(step.Round ? 4 : 6));
            r.Intersect(full);
            if (!r.IsEmpty) spot = r;
        }

        // Dimmed window with a hole for the highlighted area
        using (var dimRegion = new Region(full))
        using (var dim = new SolidBrush(Color.FromArgb(p.IsDark ? 170 : 140, 0, 0, 0)))
        {
            GraphicsPath? hole = null;
            var s = spot.GetValueOrDefault();
            if (spot.HasValue)
            {
                hole = Rounded(s, step.Round ? s.Width / 2 : S(10));
                dimRegion.Exclude(hole);
            }
            g.FillRegion(dim, dimRegion);
            if (hole != null)
            {
                using var ring = new Pen(p.Accent, S(2.5f));
                g.DrawPath(ring, hole);
                using var glow = new Pen(Color.FromArgb(70, p.Accent), S(7));
                var outer = s;
                outer.Inflate(S(3), S(3));
                using var outerPath = Rounded(outer, step.Round ? outer.Width / 2 : S(13));
                g.DrawPath(glow, outerPath);
                hole.Dispose();
            }
        }

        DrawBubble(g, p, step, spot, full);
    }

    private void DrawBubble(Graphics g, ThemePalette p, Step step, Rectangle? spot, Rectangle full)
    {
        int pad = S(18), width = S(360), tile = S(34), gap = S(12);
        // Pixel-sized fonts: the same size in GDI (TextRenderer) and GDI+, independent of the bitmap's DPI.
        float px = DeviceDpi / 72f;
        using var titleFont = new Font("Segoe UI Semibold", 12f * px, GraphicsUnit.Pixel);
        using var textFont = new Font("Segoe UI", 9.75f * px, GraphicsUnit.Pixel);
        using var smallFont = new Font("Segoe UI", 8.5f * px, GraphicsUnit.Pixel);
        using var buttonFont = new Font("Segoe UI Semibold", 9.5f * px, GraphicsUnit.Pixel);

        const TextFormatFlags Wrap = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        const TextFormatFlags Line = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        int textLeft = pad + tile + gap;
        int textWidth = width - textLeft - pad;
        var titleSize = TextRenderer.MeasureText(step.Title, titleFont, new Size(textWidth, int.MaxValue), Wrap);
        var textSize = TextRenderer.MeasureText(step.Text, textFont, new Size(width - 2 * pad, int.MaxValue), Wrap);
        var smallSize = TextRenderer.MeasureText("Schritt", smallFont, Size.Empty, Line);
        int headerHeight = Math.Max(tile, smallSize.Height + S(2) + titleSize.Height);
        int btnHeight = S(32);
        int height = pad + headerHeight + S(12) + textSize.Height + S(18) + btnHeight + pad;

        var bubble = PlaceBubble(new Size(width, height), spot, full);

        // Shadow
        for (int i = 3; i >= 1; i--)
        {
            var sh = bubble;
            sh.Inflate(S(i * 2), S(i * 2));
            sh.Offset(0, S(3));
            using var shadowPath = Rounded(sh, S(14 + i * 2));
            using var shadow = new SolidBrush(Color.FromArgb(18, 0, 0, 0));
            g.FillPath(shadow, shadowPath);
        }

        // The card is drawn on an opaque bitmap so text gets ClearType like the rest of the app,
        // then copied into the frame through the rounded outline.
        Color cardBack = p.IsDark ? p.Secondary : Color.White;
        using var card = new Bitmap(width, height, PixelFormat.Format32bppRgb);
        using (var c = Graphics.FromImage(card))
        {
            c.Clear(cardBack);
            c.SmoothingMode = SmoothingMode.AntiAlias;

            ColorIcons.DrawTile(c, new RectangleF(pad, pad, tile, tile), step.Color, step.Glyph);
            int headerTop = pad + (headerHeight - (smallSize.Height + S(2) + titleSize.Height)) / 2;
            TextRenderer.DrawText(c, $"Schritt {_index + 1} von {_steps.Count}", smallFont,
                new Point(textLeft, headerTop), p.TextMuted, cardBack, Line);
            TextRenderer.DrawText(c, step.Title, titleFont,
                new Rectangle(textLeft, headerTop + smallSize.Height + S(2), textWidth, titleSize.Height), p.Text, cardBack, Wrap);

            int ty = pad + headerHeight + S(12);
            TextRenderer.DrawText(c, step.Text, textFont, new Rectangle(pad, ty, width - 2 * pad, textSize.Height),
                p.TextSecondary, cardBack, Wrap);

            // Progress dots
            int by = height - pad - btnHeight;
            int dot = S(6), dotGap = S(5);
            int dotsY = by + (btnHeight - dot) / 2;
            int dx = pad;
            for (int i = 0; i < _steps.Count; i++)
            {
                bool active = i == _index;
                int w = active ? S(16) : dot;
                using var dotBrush = new SolidBrush(active ? p.Accent : ThemePalette.Blend(cardBack, p.Text, p.IsDark ? 0.35f : 0.24f));
                using var dotPath = Rounded(new Rectangle(dx, dotsY, w, dot), dot / 2);
                c.FillPath(dotBrush, dotPath);
                dx += w + dotGap;
            }

            // Buttons (right to left): Weiter / Los geht's, Zurück
            bool last = _index == _steps.Count - 1;
            string nextText = last ? "Los geht's" : "Weiter";
            int nextWidth = TextRenderer.MeasureText(nextText, buttonFont, Size.Empty, Line).Width + S(34);
            var next = new Rectangle(width - pad - nextWidth, by, nextWidth, btnHeight);
            DrawButton(c, next, nextText, buttonFont, _hover == Hit.Next ? p.AccentHover : p.Accent, Color.White);
            _btnNext = Offset(next, bubble);

            if (_index > 0)
            {
                int backWidth = TextRenderer.MeasureText("Zurück", buttonFont, Size.Empty, Line).Width + S(28);
                var back = new Rectangle(next.X - S(8) - backWidth, by, backWidth, btnHeight);
                DrawButton(c, back, "Zurück", buttonFont,
                    _hover == Hit.Back ? p.ButtonSecondaryHover : p.ButtonSecondary, p.ButtonSecondaryText);
                _btnBack = Offset(back, bubble);
            }
            else
            {
                _btnBack = Rectangle.Empty;
            }

            if (!last)
            {
                // "Überspringen" in the top right corner
                using var skipFont = _hover == Hit.Skip ? new Font(smallFont, FontStyle.Underline) : (Font)smallFont.Clone();
                var skipSize = TextRenderer.MeasureText("Überspringen", skipFont, Size.Empty, Line);
                var skip = new Rectangle(width - pad - skipSize.Width - S(4), headerTop - S(3), skipSize.Width + S(8), skipSize.Height + S(6));
                TextRenderer.DrawText(c, "Überspringen", skipFont, new Point(skip.X + S(4), skip.Y + S(3)),
                    _hover == Hit.Skip ? p.Accent : p.TextMuted, cardBack, Line);
                _btnSkip = Offset(skip, bubble);
            }
            else
            {
                _btnSkip = Rectangle.Empty;
            }
        }

        using (var cardPath = Rounded(bubble, S(14)))
        using (var texture = new TextureBrush(card, WrapMode.Clamp))
        using (var border = new Pen(p.CardBorder, 1f))
        {
            texture.TranslateTransform(bubble.X, bubble.Y);
            g.FillPath(texture, cardPath);
            g.DrawPath(border, cardPath);
        }
    }

    private static Rectangle Offset(Rectangle r, Rectangle by) => new(r.X + by.X, r.Y + by.Y, r.Width, r.Height);

    private void DrawButton(Graphics g, Rectangle r, string text, Font font, Color back, Color fore)
    {
        using var path = Rounded(r, S(6));
        using var brush = new SolidBrush(back);
        g.FillPath(brush, path);
        TextRenderer.DrawText(g, text, font, r, fore, back,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
    }

    /// <summary>Right of the highlighted area, else left, below, above; centered without one.</summary>
    private Rectangle PlaceBubble(Size size, Rectangle? spot, Rectangle full)
    {
        int margin = S(16);
        var area = Rectangle.Inflate(full, -margin, -margin);
        Rectangle Fit(int x, int y) => new(
            Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - size.Width)),
            Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - size.Height)),
            size.Width, size.Height);

        if (spot is not Rectangle s)
            return Fit(full.X + (full.Width - size.Width) / 2, full.Y + (full.Height - size.Height) / 2);

        int centerY = s.Y + s.Height / 2 - size.Height / 2;
        int centerX = s.X + s.Width / 2 - size.Width / 2;
        if (s.Right + margin + size.Width <= area.Right)
            return Fit(s.Right + margin, centerY);
        if (s.Left - margin - size.Width >= area.Left)
            return Fit(s.Left - margin - size.Width, centerY);
        if (s.Bottom + margin + size.Height <= area.Bottom)
            return Fit(centerX, s.Bottom + margin);
        return Fit(centerX, s.Top - margin - size.Height);
    }

    private static GraphicsPath Rounded(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        int d = Math.Max(1, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    // ───────────────────────── layered window

    private void Present(Bitmap bmp)
    {
        IntPtr screenDc = GetDC(IntPtr.Zero);
        IntPtr memDc = CreateCompatibleDC(screenDc);
        IntPtr hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
        IntPtr old = SelectObject(memDc, hBitmap);
        try
        {
            var size = new SIZE { cx = bmp.Width, cy = bmp.Height };
            var src = new POINT();
            var dst = new POINT { x = Left, y = Top };
            var blend = new BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
            UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, 2 /* ULW_ALPHA */);
        }
        finally
        {
            SelectObject(memDc, old);
            DeleteObject(hBitmap);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    /// <summary>Test helper: the current frame as a bitmap.</summary>
    internal Bitmap RenderFrame(int index)
    {
        _index = index;
        _steps[_index].Enter?.Invoke();
        var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        PaintFrame(g);
        return bmp;
    }

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] private struct SIZE { public int cx, cy; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
        IntPtr hdcSrc, ref POINT pprSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);
}
