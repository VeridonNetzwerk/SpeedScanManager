using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Sub-dialog for PDF file format options: page splitting and password protection.
/// </summary>
internal class PdfOptionsDialog : Form
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
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "PDF-Dateiformat-Optionen";
        Icon = TrayIcons.GetAppIcon();
        ClientSize = new Size(460, 274);
        ShowInTaskbar = false;

        var font = UiFonts.Default;
        Font = font;

        // === Page split group ===
        var grpSplit = new GroupBox
        {
            Text = "PDF-Seiten teilen",
            Location = new Point(12, 12),
            Size = new Size(436, 100),
            Font = font
        };

        _rbMultiPage = new RadioButton
        {
            Text = "Mehrseitige PDF-Datei (gesamter Stapel in einer PDF)",
            Location = new Point(16, 22),
            AutoSize = true,
            Font = font,
            Checked = true
        };

        _rbSplitByPages = new RadioButton
        {
            Text = "Seitenzahl, für die jeweils eine neue PDF-Datei erstellt werden soll",
            Location = new Point(16, 46),
            Size = new Size(410, 22),
            Font = font
        };

        _numSplitPages = new NumericUpDown
        {
            Location = new Point(34, 70),
            Size = new Size(60, 24),
            Font = font,
            Minimum = 1,
            Maximum = 999,
            Value = 1,
            Enabled = false
        };

        var lblPages = new Label
        {
            Text = "Seite(n)",
            Location = new Point(100, 73),
            AutoSize = true,
            Font = font
        };

        _rbSplitByPages.CheckedChanged += (s, e) =>
        {
            _numSplitPages.Enabled = _rbSplitByPages.Checked;
        };

        grpSplit.Controls.AddRange(new Control[] { _rbMultiPage, _rbSplitByPages, _numSplitPages, lblPages });

        // === Password group ===
        var grpPassword = new GroupBox
        {
            Text = "Kennwort",
            Location = new Point(12, 122),
            Size = new Size(436, 100),
            Font = font
        };

        _chkUsePassword = new CheckBox
        {
            Text = "Kennwort für PDF-Datei einstellen",
            Location = new Point(16, 22),
            AutoSize = true,
            Font = font
        };

        var lblPassword = new Label
        {
            Text = "Kennwort:",
            Location = new Point(32, 48),
            AutoSize = true,
            Font = font
        };

        _txtPassword = new TextBox
        {
            Location = new Point(130, 45),
            Size = new Size(240, 24),
            Font = font,
            UseSystemPasswordChar = true,
            Enabled = false
        };

        var lblConfirm = new Label
        {
            Text = "Bestätigen:",
            Location = new Point(32, 76),
            AutoSize = true,
            Font = font
        };

        _txtConfirmPassword = new TextBox
        {
            Location = new Point(130, 73),
            Size = new Size(240, 24),
            Font = font,
            UseSystemPasswordChar = true,
            Enabled = false
        };

        _chkUsePassword.CheckedChanged += (s, e) =>
        {
            bool enabled = _chkUsePassword.Checked;
            _txtPassword.Enabled = enabled;
            _txtConfirmPassword.Enabled = enabled;
        };

        grpPassword.Controls.AddRange(new Control[]
        {
            _chkUsePassword, lblPassword, _txtPassword, lblConfirm, _txtConfirmPassword
        });

        // === Buttons ===
        var btnOk = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(282, 234),
            Size = new Size(75, 28),
            Font = font
        };

        var btnCancel = new Button
        {
            Text = "Abbrechen",
            DialogResult = DialogResult.Cancel,
            Location = new Point(370, 234),
            Size = new Size(75, 28),
            Font = font
        };

        Controls.AddRange(new Control[] { grpSplit, grpPassword, btnOk, btnCancel });
        AcceptButton = btnOk;
        CancelButton = btnCancel;
        ResumeLayout(false);
        PerformLayout();

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
                    MessageBox.Show(this, error, "SpeedScan Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
