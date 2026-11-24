using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Asks what to do with settings that were changed but not saved, before a scan from the
/// settings window or before closing it. The choices are large option cards.
/// </summary>
internal sealed class UnsavedChangesDialog : ModernWindow
{
    public enum Choice { Cancel, Save, KeepUnsaved, Discard }

    public enum Purpose { Scan, Close }

    public Choice Result { get; private set; } = Choice.Cancel;

    public UnsavedChangesDialog(Purpose purpose)
    {
        SuspendLayout();
        bool scan = purpose == Purpose.Scan;
        Text = "SpeedScan Manager";
        Title = "Nicht gespeicherte Änderungen";
        Subtitle = scan ? "Womit soll gescannt werden?" : "Sollen die Änderungen gespeichert werden?";
        HeaderIcon = ColorIcon.Tile(scan ? Glyph.Scanner : Glyph.Gear, scan ? IconColors.Blue : IconColors.Amber);
        HeaderButtons = ModernHeaderButtons.Close;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;

        var options = scan
            ? new[]
            {
                (Choice.Save, "Speichern und scannen", "Die Änderungen übernehmen und gleich damit scannen.", Glyph.Check, IconColors.Green),
                (Choice.KeepUnsaved, "Mit gespeicherten Einstellungen scannen", "Deine Änderungen bleiben im Fenster, sind aber noch nicht gespeichert.", Glyph.Scanner, IconColors.Blue),
                (Choice.Discard, "Änderungen verwerfen und scannen", "Zurück zu den gespeicherten Einstellungen und damit scannen.", Glyph.Cross, IconColors.Red),
            }
            : new[]
            {
                (Choice.Save, "Speichern und schließen", "Die Änderungen übernehmen und das Fenster schließen.", Glyph.Check, IconColors.Green),
                (Choice.Discard, "Verwerfen und schließen", "Die Änderungen gehen verloren, die gespeicherten Einstellungen bleiben.", Glyph.Cross, IconColors.Red),
            };

        var list = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 14, 16, 8) };
        // Docked top in reverse order
        for (int i = options.Length - 1; i >= 0; i--)
        {
            var (choice, title, text, glyph, color) = options[i];
            var row = new OptionCard(title, text, glyph, color) { Dock = DockStyle.Top, Height = 64 };
            row.Click += (_, _) => { Result = choice; DialogResult = DialogResult.OK; Close(); };
            list.Controls.Add(row);
            if (i > 0) list.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 8 });
        }
        Body.Controls.Add(list);

        var cancel = AddFooterButton(scan ? "Abbrechen" : "Zurück", ThemeRole.Secondary, Glyph.None, DialogResult.Cancel);
        CancelButton = cancel;

        ClientSize = new Size(560, 64 /* header */ + 14 + options.Length * 64 + (options.Length - 1) * 8 + 8 + 56 /* footer */ + 4);
        ThemeManager.Apply(this);
        ResumeLayout(true);
    }

    /// <summary>Shows the dialog over <paramref name="owner"/> and returns the choice.</summary>
    public static Choice Ask(IWin32Window owner, Purpose purpose)
    {
        using var dlg = new UnsavedChangesDialog(purpose);
        return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.Result : Choice.Cancel;
    }

    /// <summary>Clickable card: colored tile, title, description, chevron.</summary>
    private sealed class OptionCard : Control, IThemeAware
    {
        private readonly string _title, _text;
        private readonly Glyph _glyph;
        private readonly Color _color;
        private bool _hover, _pressed;

        public OptionCard(string title, string text, Glyph glyph, Color color)
        {
            _title = title;
            _text = text;
            _glyph = glyph;
            _color = color;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.PushButton;
            AccessibleName = title;
            AccessibleDescription = text;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
        }

        public void ApplyTheme(ThemePalette palette) => Invalidate();

        private int S(float v) => ModernLayout.Scale(this, v);

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override bool IsInputKey(Keys keyData) => keyData is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode is Keys.Enter or Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = ThemeManager.Current;
            var g = e.Graphics;
            g.Clear(ModernLayout.ParentBack(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color back = _pressed ? p.Selected : _hover ? ThemePalette.Blend(p.Card, p.Accent, p.IsDark ? 0.10f : 0.06f) : p.Card;
            Color border = _hover || Focused ? ThemePalette.Blend(p.CardBorder, p.Accent, 0.6f) : p.CardBorder;
            var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = ModernLayout.RoundedRect(rect, S(10)))
            {
                using (var brush = new SolidBrush(back)) g.FillPath(brush, path);
                using var pen = new Pen(border, Focused ? 1.5f : 1f);
                g.DrawPath(pen, path);
            }

            int tile = S(36);
            ColorIcons.DrawTile(g, new RectangleF(S(14), (Height - tile) / 2f, tile, tile), _color, _glyph);

            int x = S(14) + tile + S(14);
            int w = Width - x - S(40);
            const TextFormatFlags f = TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            int th = TextRenderer.MeasureText("Ag", ModernFonts.CardTitle).Height;
            int sh = TextRenderer.MeasureText("Ag", ModernFonts.Small).Height;
            int top = (Height - th - sh - S(1)) / 2;
            TextRenderer.DrawText(g, _title, ModernFonts.CardTitle, new Rectangle(x, top, w, th), p.Text, back, f);
            TextRenderer.DrawText(g, _text, ModernFonts.Small, new Rectangle(x, top + th + S(1), w, sh), p.TextMuted, back, f);

            float cs = S(14);
            ThemeGlyphs.Draw(g, Glyph.ChevronRight, new RectangleF(Width - S(16) - cs, (Height - cs) / 2f, cs, cs),
                _hover ? p.Accent : p.TextMuted);
        }
    }
}
