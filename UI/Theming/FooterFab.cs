using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Round action button in the bottom right corner of a <see cref="ModernWindow"/>, sitting on the
/// footer's top edge. The footer gets a round notch with soft shoulders around it. The control
/// covers the notch: above the footer edge it is clipped to the notch circle (same color as
/// the page behind it), below it paints the footer color itself so all edges are anti-aliased.
/// </summary>
internal sealed class FooterFab : Control, IThemeAware
{
    /// <summary>Button diameter (96-dpi units).</summary>
    public const int Diameter = 56;
    /// <summary>Gap between button and notch edge.</summary>
    public const int Gap = 7;
    /// <summary>Radius of the shoulders where the notch meets the footer edge.</summary>
    public const int Shoulder = 8;
    /// <summary>How far the button center sits below the footer edge.</summary>
    public const int Drop = 4;

    private readonly ModernWindow _window;
    private readonly Glyph _glyph;
    private bool _hover, _pressed;

    public FooterFab(ModernWindow window, Glyph glyph)
    {
        _window = window;
        _glyph = glyph;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
        AccessibleRole = AccessibleRole.PushButton;
    }

    public void ApplyTheme(ThemePalette palette) => Invalidate();

    private float S(float v) => v * DeviceDpi / 96f;

    /// <summary>Space the footer keeps free on the right (96-dpi units).</summary>
    public static int ReservedWidth => Diameter + 2 * (Gap + Shoulder) + 8;

    /// <summary>Places the control over the footer edge (window client coordinates).</summary>
    public void Place(Rectangle footer)
    {
        float r = S(Diameter) / 2f, rn = r + S(Gap), sh = S(Shoulder);
        float cx = footer.Right - S(20) - r - sh;
        float cy = footer.Top + S(Drop);
        int left = (int)Math.Floor(cx - rn - sh - 2), top = (int)Math.Floor(cy - rn - 2);
        int right = (int)Math.Ceiling(cx + rn + sh + 2), bottom = (int)Math.Ceiling(cy + rn + S(10));
        bottom = Math.Min(bottom, footer.Bottom);
        Bounds = new Rectangle(left, top, right - left, bottom - top);

        // Clip: notch circle above the footer edge, full rectangle below it.
        int edge = footer.Top - top;
        var region = new Region(new Rectangle(0, edge, Width, Height - edge));
        using (var circle = new GraphicsPath())
        {
            circle.AddEllipse(cx - left - rn, cy - top - rn, 2 * rn, 2 * rn);
            region.Union(circle);
        }
        Region?.Dispose();
        Region = region;
        _edge = edge;
        _center = new PointF(cx - left, cy - top);
        Invalidate();
    }

    private int _edge;
    private PointF _center;

    /// <summary>The round button itself in screen coordinates (guided tour).</summary>
    public Rectangle ButtonScreenBounds
    {
        get
        {
            float r = S(Diameter) / 2f;
            var local = Rectangle.Round(new RectangleF(_center.X - r, _center.Y - r, 2 * r, 2 * r));
            return RectangleToScreen(local);
        }
    }

    /// <summary>Color of the page behind the upper half of the button.</summary>
    private Color PageBack()
    {
        Control c = _window.Body;
        var pt = c.PointToClient(PointToScreen(new Point((int)_center.X, Math.Max(0, _edge - 2))));
        while (true)
        {
            var child = c.GetChildAtPoint(pt, GetChildAtPointSkip.Invisible);
            if (child == null) break;
            pt = child.PointToClient(c.PointToScreen(pt));
            c = child;
        }
        while (c.BackColor.A < 255 && c.Parent != null) c = c.Parent;
        return c.BackColor.A == 255 ? c.BackColor : ThemeManager.Current.Background;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left && InButton(e.Location)) { _pressed = true; Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        bool over = InButton(e.Location);
        Cursor = over ? Cursors.Hand : Cursors.Default;
        if (over != _hover) { _hover = over; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnClick(EventArgs e)
    {
        if (e is MouseEventArgs m && !InButton(m.Location)) return;
        base.OnClick(e);
    }

    private bool InButton(Point p)
    {
        float r = S(Diameter) / 2f;
        float dx = p.X - _center.X, dy = p.Y - _center.Y;
        return dx * dx + dy * dy <= r * r;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = ThemeManager.Current;
        var g = e.Graphics;
        Color page = PageBack();
        Color footer = p.Secondary;
        g.Clear(page);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float r = S(Diameter) / 2f, rn = r + S(Gap), sh = S(Shoulder);
        float cx = _center.X, cy = _center.Y, y0 = _edge;

        // Footer part with the notch: footer color, then the notch (page color) with shoulders.
        // Shoulder circles: tangent to the footer edge (center sh below it) and to the notch circle.
        float sy = y0 + sh;
        float d = (float)Math.Sqrt(Math.Max(0, (rn + sh) * (rn + sh) - (sy - cy) * (sy - cy)));
        var leftShoulder = new PointF(cx - d, sy);
        var rightShoulder = new PointF(cx + d, sy);
        // Tangent points on the notch circle
        PointF OnNotch(PointF c) => new(cx + (c.X - cx) * rn / (rn + sh), cy + (c.Y - cy) * rn / (rn + sh));
        var pl = OnNotch(leftShoulder);
        var pr = OnNotch(rightShoulder);

        // Pixel-exact like the footer itself (no anti-aliased seam at the edges)
        g.SmoothingMode = SmoothingMode.None;
        using (var footerBrush = new SolidBrush(footer))
            g.FillRectangle(footerBrush, 0, y0, Width, Height - y0);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var pageBrush = new SolidBrush(page))
        {
            g.FillPolygon(pageBrush, new[] { new PointF(leftShoulder.X, y0), pl, pr, new PointF(rightShoulder.X, y0) });
            g.FillEllipse(pageBrush, cx - rn, cy - rn, 2 * rn, 2 * rn);
        }
        using (var footerBrush = new SolidBrush(footer))
        {
            g.FillEllipse(footerBrush, leftShoulder.X - sh, sy - sh, 2 * sh, 2 * sh);
            g.FillEllipse(footerBrush, rightShoulder.X - sh, sy - sh, 2 * sh, 2 * sh);
        }

        if (!p.IsDark)
        {
            // Continue the footer's top border around the notch
            using var pen = new Pen(p.Border, 1f);
            float a1 = Deg(Math.Atan2(pl.Y - sy, pl.X - leftShoulder.X));
            float a2 = Deg(Math.Atan2(pr.Y - sy, pr.X - rightShoulder.X));
            float n1 = Deg(Math.Atan2(pl.Y - cy, pl.X - cx));
            float n2 = Deg(Math.Atan2(pr.Y - cy, pr.X - cx));
            g.DrawLine(pen, 0, y0, leftShoulder.X, y0);
            g.DrawArc(pen, leftShoulder.X - sh, sy - sh, 2 * sh, 2 * sh, 270, Sweep(270, a1));
            g.DrawArc(pen, cx - rn, cy - rn, 2 * rn, 2 * rn, n1, -Sweep(n2, n1));
            g.DrawArc(pen, rightShoulder.X - sh, sy - sh, 2 * sh, 2 * sh, a2, Sweep(a2, 270));
            g.DrawLine(pen, rightShoulder.X, y0, Width, y0);
        }

        // Shadow
        for (int i = 2; i >= 1; i--)
        {
            float grow = S(i * 0.8f);
            using var shadow = new SolidBrush(Color.FromArgb(p.IsDark ? 40 : 20, 0, 0, 0));
            g.FillEllipse(shadow, cx - r - grow, cy - r - grow + S(1.5f), 2 * (r + grow), 2 * (r + grow));
        }

        // Button: accent gradient, lighter on hover, darker when pressed
        Color top = _pressed ? p.AccentPressed : _hover ? ThemePalette.Blend(p.AccentHover, Color.White, 0.12f) : ThemePalette.Blend(p.Accent, Color.White, 0.12f);
        Color bottom = _pressed ? p.AccentPressed : _hover ? p.AccentHover : p.Accent;
        var circle = new RectangleF(cx - r, cy - r, 2 * r, 2 * r);
        using (var brush = new LinearGradientBrush(circle, top, bottom, LinearGradientMode.Vertical))
            g.FillEllipse(brush, circle);
        using (var shine = new Pen(Color.FromArgb(50, 255, 255, 255), S(1)))
            g.DrawArc(shine, circle.X + S(1), circle.Y + S(1), circle.Width - S(2), circle.Height - S(2), 200, 140);

        float gs = S(24);
        ThemeGlyphs.Draw(g, _glyph, new RectangleF(cx - gs / 2f, cy - gs / 2f, gs, gs), Color.White, 1.15f);
    }

    private static float Deg(double rad) => (float)(rad * 180 / Math.PI);

    /// <summary>Clockwise sweep from a to b (0..360).</summary>
    private static float Sweep(float a, float b)
    {
        float s = (b - a) % 360f;
        return s < 0 ? s + 360f : s;
    }
}
