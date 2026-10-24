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
/// box is shown with MB_TOPMOST | MB_SETFOREGROUND (WinForms' MessageBox doesn't allow
/// these flags) to make sure it is never hidden behind other windows.
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
