using System.Drawing;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Options dialog for "Scan to E-Mail" application.
/// Allows configuring default recipient and subject template.
/// </summary>
internal class ScanToEmailOptionsDialog : ModernWindow
{
    public const string DefaultSubject = "Gescanntes Dokument";

    private readonly TextBox _txtRecipient;
    private readonly TextBox _txtSubject;
    private readonly Button _btnOk;
    private readonly Button _btnCancel;

    public string Recipient { get; set; } = "";
    public string SubjectTemplate { get; set; } = DefaultSubject;

    public ScanToEmailOptionsDialog()
    {
        SuspendLayout();
        StartPosition = FormStartPosition.CenterParent;
        Text = "Scan to E-Mail – Optionen";
        Title = "Scan to E-Mail";
        Subtitle = "Optionen für den E-Mail-Versand";
        HeaderButtons = ModernHeaderButtons.Close;
        ShowInTaskbar = false;
        AutoSizeToContent = true;
        MinimumContentWidth = 500;

        _txtRecipient = new TextBox { AutoSize = false, Size = new Size(260, 28) };
        _txtSubject = new TextBox { AutoSize = false, Size = new Size(260, 28) };

        var card = new SettingsCard();
        card.AddRow("Standard-Empfänger", "Empfänger der E-Mail.", _txtRecipient, 260);
        card.AddRow("Betreff-Vorlage", "Betreff der E-Mail.", _txtSubject, 260);

        var stack = new ModernStack { Dock = DockStyle.Top, Padding = new Padding(20, 18, 20, 20) };
        stack.Controls.Add(card);
        Body.Controls.Add(stack);

        _btnCancel = AddFooterButton("Abbrechen", ThemeRole.Secondary, Glyph.Cross, DialogResult.Cancel);
        _btnOk = AddFooterButton("OK", ThemeRole.Primary, Glyph.Check, DialogResult.OK);

        AcceptButton = _btnOk;
        CancelButton = _btnCancel;

        ThemeManager.Apply(this);
        ResumeLayout(true);
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
                ? DefaultSubject
                : _txtSubject.Text.Trim();
        }
        base.OnFormClosing(e);
    }
}
