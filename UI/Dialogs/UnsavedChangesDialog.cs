using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Compact prompt in the style of the app's other prompts: what to do with settings that were
/// changed but not saved, before a scan from the settings window or before closing it. Lists
/// the changes like a BIOS does before saving ("Farbmodus: Farbe → Graustufen").
/// </summary>
internal sealed class UnsavedChangesDialog : ModernWindow
{
    public enum Choice { Cancel, Save, KeepUnsaved, Discard }

    public enum Purpose { Scan, Close }

    public Choice Result { get; private set; } = Choice.Cancel;

    private const int MaxRows = 7;

    public UnsavedChangesDialog(Purpose purpose, IReadOnlyList<SettingsChangeList.Change> changes)
    {
        SuspendLayout();
        bool scan = purpose == Purpose.Scan;
        Text = "SpeedScan Manager";
        Title = scan ? L.T("Vor dem Scannen speichern?") : L.T("Änderungen speichern?");
        HeaderHeight = 52;
        HeaderButtons = ModernHeaderButtons.Close;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(20, 18, 22, 6),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.Controls.Add(new TrayPromptForm.PromptBadge(TrayPromptForm.PromptBadge.Kind.Question)
        {
            Size = new Size(36, 36),
            Margin = new Padding(0, 0, 14, 0),
        }, 0, 0);

        var stack = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
        };
        int textWidth = scan ? 470 : 430;
        var heading = new Label
        {
            Text = changes.Count == 1 ? L.T("Diese Einstellung wurde geändert:") : L.T("Diese Einstellungen wurden geändert:"),
            AutoSize = true,
            Font = ThemeFonts.Heading,
            Margin = new Padding(0, 0, 0, 8),
            UseMnemonic = false,
        };
        stack.Controls.Add(heading);
        var table = new ChangeTable(changes, MaxRows) { Width = textWidth, Margin = new Padding(0, 0, 0, 10) };
        stack.Controls.Add(table);
        if (scan)
        {
            var hint = new Label
            {
                Text = L.T("„Ohne Speichern“ scannt mit den gespeicherten Einstellungen, deine Änderungen bleiben im Fenster. „Verwerfen“ setzt sie zurück."),
                AutoSize = true,
                MaximumSize = new Size(textWidth, 0),
                Margin = new Padding(0, 0, 0, 6),
                UseMnemonic = false,
            };
            ThemeManager.SetRole(hint, ThemeRole.Muted);
            stack.Controls.Add(hint);
        }
        layout.Controls.Add(stack, 1, 0);
        Body.Controls.Add(layout);

        Button Add(string text, ThemeRole role, Glyph glyph, Choice choice, bool left = false)
        {
            var b = AddFooterButton(text, role, glyph, DialogResult.None, left);
            b.Click += (_, _) => { Result = choice; DialogResult = choice == Choice.Cancel ? DialogResult.Cancel : DialogResult.OK; Close(); };
            return b;
        }

        Button cancel, save;
        if (scan)
        {
            cancel = Add(L.T("Abbrechen"), ThemeRole.Secondary, Glyph.None, Choice.Cancel, left: true);
            Add(L.T("Verwerfen"), ThemeRole.Secondary, Glyph.None, Choice.Discard);
            Add(L.T("Ohne Speichern"), ThemeRole.Secondary, Glyph.None, Choice.KeepUnsaved);
            save = Add(L.T("Speichern"), ThemeRole.Primary, Glyph.Check, Choice.Save);
        }
        else
        {
            Add(L.T("Verwerfen"), ThemeRole.Secondary, Glyph.None, Choice.Discard, left: true);
            cancel = Add(L.T("Abbrechen"), ThemeRole.Secondary, Glyph.None, Choice.Cancel);
            save = Add(L.T("Speichern"), ThemeRole.Primary, Glyph.Check, Choice.Save);
        }
        CancelButton = cancel;
        AcceptButton = save;

        int hintHeight = scan ? TextRenderer.MeasureText(
            L.T("„Ohne Speichern“ scannt mit den gespeicherten Einstellungen, deine Änderungen bleiben im Fenster. „Verwerfen“ setzt sie zurück."),
            ThemeFonts.Regular, new Size(textWidth, 0), TextFormatFlags.WordBreak).Height + 6 : 0;
        int bodyHeight = 18 + 24 + table.Height + 10 + hintHeight + 6;
        ClientSize = new Size(20 + 36 + 14 + textWidth + 22, 52 + Math.Max(36 + 24, bodyHeight) + 56);
        ThemeManager.Apply(this);
        ResumeLayout(true);
    }

    /// <summary>Shows the prompt over <paramref name="owner"/> and returns the choice.</summary>
    public static Choice Ask(IWin32Window owner, Purpose purpose, IReadOnlyList<SettingsChangeList.Change> changes)
    {
        using var dlg = new UnsavedChangesDialog(purpose, changes);
        return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.Result : Choice.Cancel;
    }

    /// <summary>Card with one line per change: "Bereich · Name   alt → neu".</summary>
    private sealed class ChangeTable : Control, IThemeAware
    {
        private readonly IReadOnlyList<SettingsChangeList.Change> _changes;
        private readonly int _rows;
        private const int RowHeight = 26;

        public ChangeTable(IReadOnlyList<SettingsChangeList.Change> changes, int maxRows)
        {
            _changes = changes;
            _rows = Math.Min(changes.Count, maxRows) + (changes.Count > maxRows ? 1 : 0);
            Height = 8 + _rows * RowHeight + 8;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            AccessibleRole = AccessibleRole.List;
            AccessibleName = string.Join(", ", changes.Select(c => $"{c.Name}: {c.Old} zu {c.New}"));
        }

        public void ApplyTheme(ThemePalette palette) => Invalidate();

        private int S(float v) => ModernLayout.Scale(this, v);

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = ThemeManager.Current;
            var g = e.Graphics;
            g.Clear(ModernLayout.ParentBack(this));
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = ModernLayout.RoundedRect(rect, S(8)))
            {
                using (var brush = new SolidBrush(p.Card)) g.FillPath(brush, path);
                using var pen = new Pen(p.CardBorder);
                g.DrawPath(pen, path);
            }

            const TextFormatFlags F = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;
            int pad = S(12), rowH = S(RowHeight);
            int y = S(8);
            int shown = Math.Min(_changes.Count, MaxRows);
            for (int i = 0; i < shown; i++)
            {
                var c = _changes[i];
                var row = new Rectangle(pad, y, Width - 2 * pad, rowH);
                if (i > 0)
                {
                    using var div = new Pen(p.CardDivider);
                    g.DrawLine(div, pad, y, Width - pad, y);
                }

                // Right: old (muted) → new (accent), measured from the right
                string arrow = "  →  ";
                int newW = TextRenderer.MeasureText(c.New, ThemeFonts.Semibold, Size.Empty, F).Width;
                int arrowW = TextRenderer.MeasureText(arrow, ThemeFonts.Regular, Size.Empty, F).Width;
                int maxOld = Math.Max(S(40), row.Width / 2 - newW - arrowW);
                int oldW = Math.Min(maxOld, TextRenderer.MeasureText(c.Old, ThemeFonts.Regular, Size.Empty, F).Width);
                int x = row.Right - newW;
                TextRenderer.DrawText(g, c.New, ThemeFonts.Semibold, new Rectangle(x, y, newW, rowH), p.Accent, F);
                x -= arrowW;
                TextRenderer.DrawText(g, arrow, ThemeFonts.Regular, new Rectangle(x, y, arrowW, rowH), p.TextMuted, F);
                x -= oldW;
                TextRenderer.DrawText(g, c.Old, ThemeFonts.Regular, new Rectangle(x, y, oldW, rowH), p.TextMuted, F | TextFormatFlags.EndEllipsis);

                // Left: "Bereich ·" muted, name
                int areaW = TextRenderer.MeasureText(c.Area + " · ", ThemeFonts.Regular, Size.Empty, F).Width;
                int nameRight = x - S(12);
                TextRenderer.DrawText(g, c.Area + " · ", ThemeFonts.Regular, new Rectangle(row.X, y, Math.Min(areaW, nameRight - row.X), rowH), p.TextMuted, F);
                TextRenderer.DrawText(g, c.Name, ThemeFonts.Regular, new Rectangle(row.X + areaW, y, Math.Max(0, nameRight - row.X - areaW), rowH),
                    p.Text, F | TextFormatFlags.EndEllipsis);
                y += rowH;
            }
            if (_changes.Count > shown)
            {
                using var div = new Pen(p.CardDivider);
                g.DrawLine(div, pad, y, Width - pad, y);
                TextRenderer.DrawText(g, L.F("… und {0} weitere", _changes.Count - shown), ThemeFonts.Regular,
                    new Rectangle(pad, y, Width - 2 * pad, rowH), p.TextMuted, F);
            }
        }
    }
}
