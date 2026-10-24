using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Brings windows of the tray app to the front. Post-scan dialogs are often opened while
/// another application has the focus (scan started with the scanner button), and Windows'
/// foreground lock would otherwise leave them behind other windows.
/// </summary>
internal static class WindowActivation
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);

    private const int ASFW_ANY = -1;

    /// <summary>Activates <paramref name="form"/> and tries to make it the foreground window.</summary>
    public static void BringToFront(Form form)
    {
        try
        {
            if (form.WindowState == FormWindowState.Minimized)
                form.WindowState = FormWindowState.Normal;
            form.BringToFront();
            form.Activate();
            SetForegroundWindow(form.Handle);
        }
        catch
        {
            // Activation is best effort.
        }
    }

    /// <summary>
    /// Lets any process (the running instance) take the foreground. Called by a secondary
    /// instance before it forwards a command, while it may still hold foreground rights.
    /// </summary>
    public static void AllowAnyProcessToActivate()
    {
        try { AllowSetForegroundWindow(ASFW_ANY); } catch { }
    }
}

/// <summary>
/// Message boxes for the tray app. The owner is usually the invisible tray window, so the
/// box must be top-most and take the foreground to never be hidden behind other windows.
///
/// On a thread with a WinForms message loop (the UI thread) the box is a WinForms modal
/// form: its message loop runs the application's message filters, through which the TWAIN
/// session receives its events while a scan is running. A native MessageBox has its own
/// loop without those filters and would stall the scan while it is open. Other threads
/// (e.g. crash reports) use the native MessageBox with MB_TOPMOST | MB_SETFOREGROUND.
/// </summary>
internal static class TrayMessageBox
{
    public const string Caption = "SpeedScan Manager";

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    private const uint MB_TOPMOST = 0x00040000;
    private const uint MB_SETFOREGROUND = 0x00010000;
    private const uint MB_DEFBUTTON2 = 0x00000100;

    public static DialogResult Show(IWin32Window? owner, string text,
        MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxIcon icon = MessageBoxIcon.Information,
        bool defaultSecondButton = false)
    {
        if (Application.MessageLoop && buttons is MessageBoxButtons.OK or MessageBoxButtons.YesNo)
        {
            try
            {
                using var form = new TrayPromptForm(text, Caption, buttons, icon, defaultSecondButton);
                return owner != null ? form.ShowDialog(owner) : form.ShowDialog();
            }
            catch (Exception ex)
            {
                DiagLog.Write($"TrayMessageBox: WinForms prompt failed, using native box: {ex.Message}");
            }
        }

        uint type = (uint)buttons | (uint)icon | MB_TOPMOST | MB_SETFOREGROUND;
        if (defaultSecondButton) type |= MB_DEFBUTTON2;
        IntPtr hwnd = IntPtr.Zero;
        try { hwnd = owner?.Handle ?? IntPtr.Zero; } catch { }
        // IDOK=1, IDCANCEL=2, IDABORT=3, IDRETRY=4, IDIGNORE=5, IDYES=6, IDNO=7 = DialogResult values
        return (DialogResult)MessageBoxW(hwnd, text, Caption, type);
    }

    public static void Error(IWin32Window? owner, string text) => Show(owner, text, MessageBoxButtons.OK, MessageBoxIcon.Error);
    public static void Warning(IWin32Window? owner, string text) => Show(owner, text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    public static void Info(IWin32Window? owner, string text) => Show(owner, text, MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static bool AskYesNo(IWin32Window? owner, string text, bool defaultNo = false)
        => Show(owner, text, MessageBoxButtons.YesNo, MessageBoxIcon.Question, defaultNo) == DialogResult.Yes;
}

/// <summary>
/// WinForms replacement for the native message box (see <see cref="TrayMessageBox"/>):
/// icon, text and OK or Ja/Nein buttons; top-most, centered on the screen, brought to the
/// foreground when shown.
/// </summary>
internal sealed class TrayPromptForm : Form
{
    public TrayPromptForm(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, bool defaultSecondButton)
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = caption;
        Font = SystemFonts.MessageBoxFont ?? Control.DefaultFont;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = SystemColors.Window;

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(12, 16, 12, 0),
            Margin = Padding.Empty,
            BackColor = SystemColors.Window
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var systemIcon = icon switch
        {
            MessageBoxIcon.Error => SystemIcons.Error,
            MessageBoxIcon.Warning => SystemIcons.Warning,
            MessageBoxIcon.Question => SystemIcons.Question,
            MessageBoxIcon.Information => SystemIcons.Information,
            _ => null
        };
        if (systemIcon != null)
        {
            layout.Controls.Add(new PictureBox
            {
                Image = systemIcon.ToBitmap(),
                SizeMode = PictureBoxSizeMode.AutoSize,
                Margin = new Padding(0, 0, 12, 12)
            }, 0, 0);
        }

        var label = new Label
        {
            Text = text,
            AutoSize = true,
            MaximumSize = new Size(420, 0),
            Margin = new Padding(0, 2, 8, 16),
            UseMnemonic = false
        };
        layout.Controls.Add(label, 1, 0);

        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(0, 8, 0, 8),
            BackColor = SystemColors.Control
        };

        Button MakeButton(string caption2, DialogResult result) => new()
        {
            Text = caption2,
            DialogResult = result,
            MinimumSize = new Size(86, 26),
            AutoSize = true,
            Margin = new Padding(0, 0, 10, 0),
            UseVisualStyleBackColor = true
        };

        Button first, second;
        if (buttons == MessageBoxButtons.YesNo)
        {
            first = MakeButton("&Ja", DialogResult.Yes);
            second = MakeButton("&Nein", DialogResult.No);
            buttonPanel.Controls.Add(second);
            buttonPanel.Controls.Add(first);
            CancelButton = second;
        }
        else
        {
            first = MakeButton("OK", DialogResult.OK);
            second = first;
            buttonPanel.Controls.Add(first);
            CancelButton = first;
        }
        var defaultButton = defaultSecondButton ? second : first;
        AcceptButton = defaultButton;

        var outer = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        outer.Controls.Add(layout, 0, 0);
        outer.Controls.Add(buttonPanel, 0, 1);
        Controls.Add(outer);

        MinimumSize = new Size(260, 0);
        ResumeLayout(true);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        WindowActivation.BringToFront(this);
        (AcceptButton as Control)?.Focus();
    }
}
