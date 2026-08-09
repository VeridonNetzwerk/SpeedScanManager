using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Options dialog for "Scan to E-Mail" application.
/// Allows configuring default recipient and subject template.
/// </summary>
internal class ScanToEmailOptionsDialog : Form
{
    private readonly TextBox _txtRecipient;
    private readonly TextBox _txtSubject;
    private readonly Button _btnOk;
    private readonly Button _btnCancel;

    public string Recipient { get; set; } = "";
    public string SubjectTemplate { get; set; } = "Gescanntes Dokument";

    public ScanToEmailOptionsDialog()
    {
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Scan to E-Mail – Optionen";
        ClientSize = new Size(380, 180);
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;

        var font = new Font("Microsoft Sans Serif", 8.25f);
        Font = font;

        var lblRecipient = new Label
        {
            Text = "Standard-Empfänger:",
            Location = new Point(12, 16),
            AutoSize = true,
            Font = font
        };

        _txtRecipient = new TextBox
        {
            Location = new Point(140, 13),
            Size = new Size(220, 24),
            Font = font
        };

        var lblSubject = new Label
        {
            Text = "Betreff-Vorlage:",
            Location = new Point(12, 48),
            AutoSize = true,
            Font = font
        };

        _txtSubject = new TextBox
        {
            Location = new Point(140, 45),
            Size = new Size(220, 24),
            Font = font
        };

        _btnOk = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(190, 104),
            Size = new Size(75, 28),
            Font = font
        };

        _btnCancel = new Button
        {
            Text = "Abbrechen",
            DialogResult = DialogResult.Cancel,
            Location = new Point(285, 104),
            Size = new Size(75, 28),
            Font = font
        };

        Controls.AddRange(new Control[]
        {
            lblRecipient, _txtRecipient,
            lblSubject, _txtSubject,
            _btnOk, _btnCancel
        });

        AcceptButton = _btnOk;
        CancelButton = _btnCancel;
    }

    protected override void OnShown(EventArgs e)
    {
        _txtRecipient.Text = Recipient;
        _txtSubject.Text = SubjectTemplate;
        base.OnShown(e);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK)
        {
            Recipient = _txtRecipient.Text.Trim();
            SubjectTemplate = string.IsNullOrWhiteSpace(_txtSubject.Text)
                ? "Gescanntes Dokument"
                : _txtSubject.Text.Trim();
        }
        base.OnFormClosing(e);
    }
}
