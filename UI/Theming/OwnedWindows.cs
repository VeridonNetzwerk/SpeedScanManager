using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Opens windows above their owner without locking it (instead of ShowDialog, which disables
/// every window of the app until it closes). The window stays in front of its owner, the owner
/// stays usable. Each kind of window is open at most once: opening it again brings the open
/// one to the front. Results are applied when the window closes with OK.
/// Short questions that wait for an answer (save?, delete?, name?) stay modal on purpose.
/// </summary>
/// <summary>A window that wants to know when one of its owned windows applied a change.</summary>
internal interface IOwnedWindowHost
{
    void OwnedWindowApplied();
}

internal static class OwnedWindows
{
    private static readonly Dictionary<string, Form> Open = new();

    /// <summary>The open window for <paramref name="key"/> (or its type), if any.</summary>
    public static Form? Find(string key) =>
        Open.TryGetValue(key, out var f) && !f.IsDisposed ? f : null;

    public static Form? Find<T>() where T : Form => Find(typeof(T).FullName!);

    /// <summary>
    /// Shows <paramref name="window"/> owned by <paramref name="owner"/>. <paramref name="onOk"/>
    /// runs when it closes with DialogResult.OK, <paramref name="onClosed"/> whenever it closes
    /// (both before the window is disposed). Returns false if a window of that kind was already
    /// open; then <paramref name="window"/> is disposed and the open one comes to the front.
    /// </summary>
    public static bool Show(Form window, IWin32Window? owner, Action? onOk = null, Action? onClosed = null, string? key = null)
    {
        key ??= window.GetType().FullName!;
        if (Find(key) is { } existing)
        {
            window.Dispose();
            Activate(existing);
            return false;
        }

        Open[key] = window;
        window.DialogResult = DialogResult.None;
        HookDialogButtons(window, window);
        window.FormClosed += (_, _) =>
        {
            if (Open.TryGetValue(key, out var f) && ReferenceEquals(f, window))
                Open.Remove(key);
            try
            {
                if (window.DialogResult == DialogResult.OK && onOk != null)
                {
                    onOk();
                    // e.g. the settings window: enable "Übernehmen"
                    for (var o = window.Owner; o != null; o = o.Owner)
                        if (o is IOwnedWindowHost host && !o.IsDisposed) { host.OwnedWindowApplied(); break; }
                }
            }
            finally
            {
                onClosed?.Invoke();
            }
        };

        window.Show(owner);
        Activate(window);
        return true;
    }

    /// <summary>Closes the window of <paramref name="key"/> without applying it (Cancel).</summary>
    public static void Cancel(string key)
    {
        if (Find(key) is not { } f) return;
        f.DialogResult = DialogResult.Cancel;
        f.Close();
    }

    public static void Cancel<T>() where T : Form => Cancel(typeof(T).FullName!);

    /// <summary>Closes every window whose key starts with <paramref name="prefix"/> without applying it.</summary>
    public static void CancelPrefix(string prefix)
    {
        foreach (var key in Open.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
            Cancel(key);
    }

    /// <summary>Closes all windows owned (directly or further down) by <paramref name="owner"/> without applying them.</summary>
    public static void CancelOwnedBy(Form owner)
    {
        foreach (var f in Open.Values.ToList())
        {
            if (f.IsDisposed) continue;
            for (var o = f.Owner; o != null; o = o.Owner)
            {
                if (!ReferenceEquals(o, owner)) continue;
                f.DialogResult = DialogResult.Cancel;
                f.Close();
                break;
            }
        }
    }

    private static void Activate(Form f)
    {
        if (f.WindowState == FormWindowState.Minimized)
            f.WindowState = FormWindowState.Normal;
        WindowActivation.BringToFront(f);
    }

    /// <summary>
    /// A button with a DialogResult closes only a modal window by itself. For an owned window
    /// close it here, after the window's own click handlers (they may have reset the result
    /// because a check failed). If closing was cancelled (validation in FormClosing), the
    /// result is reset so a later close with the X button doesn't count as OK.
    /// </summary>
    private static void HookDialogButtons(Control parent, Form window)
    {
        foreach (Control c in parent.Controls)
        {
            if (c is Button b && b.DialogResult != DialogResult.None)
            {
                b.Click += (_, _) =>
                {
                    if (window.IsDisposed || window.Modal || window.DialogResult == DialogResult.None) return;
                    window.Close();
                    if (!window.IsDisposed && window.Visible)
                        window.DialogResult = DialogResult.None;
                };
            }
            HookDialogButtons(c, window);
        }
    }
}
