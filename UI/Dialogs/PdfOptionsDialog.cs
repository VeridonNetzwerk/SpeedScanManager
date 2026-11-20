using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Sub-dialog for PDF file format options: page splitting and password protection.
/// Modern layout (<see cref="ModernWindow"/>): one card per option group.
/// </summary>
internal class PdfOptionsDialog : ModernWindow
{
    private readonly RadioButton _rbMultiPage;
    private readonly RadioButton _rbSplitByPages;
    private readonly NumericUpDown _numSplitPages;
    private readonly CheckBox _chkUsePassword;
    private readonly TextBox _txtPassword;
    private readonly TextBox _txtConfirmPassword;

    public PdfSplitMode SplitMode { get; set; } = PdfSplitMode.MultiPage;
    public int SplitPages { get; set; } = 1;
    public bool UsePassword { get; set; }
    public string Password { get; set; } = "";

    public PdfOptionsDialog()
    {
        SuspendLayout();
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        Text = "PDF-Dateiformat-Optionen";
        Title = "PDF-Dateiformat-Optionen";
        Subtitle = "Seiten teilen und Kennwortschutz";
        HeaderButtons = ModernHeaderButtons.Close;
        HeaderIcon = ColorIcons.Pdf;
        AutoSizeToContent = true;
        MinimumContentWidth = 500;

        // === Page split card (radio buttons share the card as their group) ===
        _rbMultiPage = new RadioButton
        {
            Text = "Mehrseitige PDF-Datei (gesamter Stapel in einer PDF)",
            AutoSize = false,
            Height = 34,
            Checked = true
        };

        _rbSplitByPages = new RadioButton
        {
            Text = "Seitenzahl, für die jeweils eine neue PDF-Datei erstellt werden soll",
            AutoSize = false,
            Height = 34
        };

        _numSplitPages = new NumericUpDown
        {
            Size = new Size(70, 24),
            Minimum = 1,
            Maximum = 999,
            Value = 1,
            Enabled = false,
            Margin = Padding.Empty,
            AccessibleName = "Seitenzahl"
        };

        var lblPages = new Label
        {
            Text = "Seite(n)",
            AutoSize = true,
            Margin = new Padding(8, 4, 0, 0)
        };

        var pagesLine = new FlowLayoutPanel
        {
            AutoSize = false,
            Height = 40,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(24, 2, 0, 10),
            Margin = Padding.Empty
        };
        pagesLine.Controls.Add(_numSplitPages);
        pagesLine.Controls.Add(lblPages);

        _rbSplitByPages.CheckedChanged += (s, e) =>
        {
            _numSplitPages.Enabled = _rbSplitByPages.Checked;
        };

        var cardSplit = new SettingsCard("PDF-Seiten teilen") { ShowDividers = false, Dense = true, Icon = ColorIcon.Tile(Glyph.Split, IconColors.Red) };
        cardSplit.Add(new Panel { Height = 4 });
        cardSplit.Add(_rbMultiPage);
        cardSplit.Add(_rbSplitByPages);
        cardSplit.Add(pagesLine);

        // === Password card ===
        _chkUsePassword = new ToggleSwitch();

        _txtPassword = new TextBox
        {
            Size = new Size(240, 24),
            UseSystemPasswordChar = true,
            Enabled = false
        };

        _txtConfirmPassword = new TextBox
        {
            Size = new Size(240, 24),
            UseSystemPasswordChar = true,
            Enabled = false
        };

        _chkUsePassword.CheckedChanged += (s, e) =>
        {
            bool enabled = _chkUsePassword.Checked;
            _txtPassword.Enabled = enabled;
            _txtConfirmPassword.Enabled = enabled;
        };

        var cardPassword = new SettingsCard("Kennwort") { Dense = true, Icon = ColorIcon.Tile(Glyph.Lock, IconColors.Amber) };
        cardPassword.AddRow("Kennwort für PDF-Datei einstellen", null, _chkUsePassword);
        cardPassword.AddRow("Kennwort", null, _txtPassword, 240);
        cardPassword.AddRow("Bestätigen", null, _txtConfirmPassword, 240);

        var stack = new ModernStack { Dock = DockStyle.Fill, Padding = new Padding(16, 16, 16, 16), Spacing = 12 };
        stack.Controls.Add(cardSplit);
        stack.Controls.Add(cardPassword);
        Body.Controls.Add(stack);

        // === Footer ===
        var btnCancel = AddFooterButton("Abbrechen", ThemeRole.Secondary, Glyph.Cross, DialogResult.Cancel);
        var btnOk = AddFooterButton("OK", ThemeRole.Primary, Glyph.Check, DialogResult.OK);

        AcceptButton = btnOk;
        CancelButton = btnCancel;
        ResumeLayout(false);

        ThemeManager.Apply(this);
    }

    protected override void OnShown(EventArgs e)
    {
        _rbMultiPage.Checked = SplitMode == PdfSplitMode.MultiPage;
        _rbSplitByPages.Checked = SplitMode == PdfSplitMode.SplitByPageCount;
        _numSplitPages.Value = Math.Clamp(SplitPages, 1, 999);
        _numSplitPages.Enabled = _rbSplitByPages.Checked;

        _chkUsePassword.Checked = UsePassword;
        _txtPassword.Text = Password;
        _txtConfirmPassword.Text = Password;
        _txtPassword.Enabled = UsePassword;
        _txtConfirmPassword.Enabled = UsePassword;

        base.OnShown(e);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK)
        {
            if (_chkUsePassword.Checked)
            {
                string? error = string.IsNullOrEmpty(_txtPassword.Text)
                    ? "Bitte geben Sie ein Kennwort ein."
                    : _txtPassword.Text != _txtConfirmPassword.Text
                        ? "Die eingegebenen Kennwörter stimmen nicht überein."
                        : null;
                if (error != null)
                {
                    TrayMessageBox.Show(this, error, "SpeedScan Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _txtPassword.Focus();
                    e.Cancel = true;
                    return;
                }
            }

            SplitMode = _rbSplitByPages.Checked ? PdfSplitMode.SplitByPageCount : PdfSplitMode.MultiPage;
            SplitPages = (int)_numSplitPages.Value;
            UsePassword = _chkUsePassword.Checked;

            if (UsePassword)
            {
                Password = _txtPassword.Text;
            }
            else
            {
                Password = "";
            }
        }
        base.OnFormClosing(e);
    }
}
