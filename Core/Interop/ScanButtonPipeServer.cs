using System.IO.Pipes;
using System.Text;

namespace SpeedScanManager;

/// <summary>
/// Named pipe server that receives commands from secondary process instances.
/// Runs for the whole application lifetime, independent of scanner/WIA state.
///
/// PROTOCOL (pipe name <see cref="Program.PipeName"/> = per session and user, local machine,
/// byte mode, <see cref="PipeOptions.CurrentUserOnly"/> on both ends):
///   The client connects, writes exactly one UTF-8 text line and disconnects.
///   Commands (case-insensitive, surrounding whitespace ignored):
///     "SCAN" – the scanner button was pressed (sent by "SpeedScanManager.exe /scanbutton",
///              which the WIA/STI event handler launches).
///     "SHOW" – a second plain launch of the app; the running instance should show its UI.
///   Unknown commands are forwarded unchanged (upper-cased) to <see cref="CommandReceived"/>.
///
/// THREADING: <see cref="CommandReceived"/> is raised on a thread-pool thread.
/// Handlers must marshal to the UI thread themselves (e.g. Control.BeginInvoke).
/// Commands that arrive before a handler subscribed (the server starts before the tray) are
/// queued and delivered to the first subscriber.
/// </summary>
internal sealed class ScanButtonPipeServer : IDisposable
{
    public const string CommandScan = "SCAN";
    public const string CommandShow = "SHOW";

    private static readonly object SharedSync = new();
    private static ScanButtonPipeServer? _shared;

    private const int MaxQueuedCommands = 10;
    private static readonly TimeSpan QueuedCommandLifetime = TimeSpan.FromMinutes(2);

    private readonly object _sync = new();
    private CancellationTokenSource? _cts;
    private Task? _loop;

    private readonly object _handlerSync = new();
    private Action<string>? _commandReceived;
    private readonly Queue<(string Command, DateTime ReceivedUtc)> _pending = new();

    /// <summary>
    /// Raised for every received command (upper-cased, e.g. "SCAN", "SHOW").
    /// Raised on a thread-pool thread (queued commands: on the subscribing thread).
    /// </summary>
    public event Action<string>? CommandReceived
    {
        add
        {
            if (value == null) return;
            List<string> queued;
            lock (_handlerSync)
            {
                _commandReceived += value;
                var now = DateTime.UtcNow;
                queued = _pending.Where(p => now - p.ReceivedUtc <= QueuedCommandLifetime)
                    .Select(p => p.Command).ToList();
                _pending.Clear();
            }
            foreach (var command in queued)
            {
                LogDiag($"PipeServer: delivering queued '{command}'");
                Invoke(value, command);
            }
        }
        remove
        {
            lock (_handlerSync) _commandReceived -= value;
        }
    }

    /// <summary>
    /// App-wide instance. Program.Main creates and starts it before the tray starts,
    /// so secondary instances can reach the app as early as possible.
    /// </summary>
    public static ScanButtonPipeServer Shared
    {
        get
        {
            lock (SharedSync)
            {
                _shared ??= new ScanButtonPipeServer();
                return _shared;
            }
        }
    }

    public bool IsRunning
    {
        get { lock (_sync) return _loop != null; }
    }

    private static void LogDiag(string msg) => DiagLog.WriteWia(msg);

    public void Start()
    {
        lock (_sync)
        {
            if (_loop != null) return;
            var cts = new CancellationTokenSource();
            _cts = cts;
            // Capture the token locally: Stop() may replace/dispose _cts concurrently.
            var token = cts.Token;
            _loop = Task.Run(() => RunAsync(token));
        }
        LogDiag("PipeServer: started");
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        Task? loop;
        lock (_sync)
        {
            cts = _cts;
            loop = _loop;
            _cts = null;
            _loop = null;
        }
        if (cts == null) return;

        try { cts.Cancel(); } catch { }
        try { loop?.Wait(2000); } catch { }
        cts.Dispose();
        LogDiag("PipeServer: stopped");
    }

    public void Dispose() => Stop();

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    Program.PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);

                // A client must deliver its line promptly; never let a stuck client block the server.
                using var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                readCts.CancelAfter(5000);

                using var reader = new StreamReader(server, Encoding.UTF8);
                var line = await reader.ReadLineAsync(readCts.Token).ConfigureAwait(false);
                var command = (line ?? "").Trim().ToUpperInvariant();
                LogDiag($"PipeServer: received '{command}'");

                if (command.Length > 0)
                    RaiseCommand(command);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                LogDiag("PipeServer: client did not send a command in time");
            }
            catch (Exception ex)
            {
                LogDiag($"PipeServer exception: {ex.GetType().Name}: {ex.Message}");
                try { await Task.Delay(1000, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private void RaiseCommand(string command)
    {
        Action<string>? handlers;
        lock (_handlerSync)
        {
            handlers = _commandReceived;
            if (handlers == null)
            {
                // The tray is not subscribed yet: keep the command for it.
                if (_pending.Count >= MaxQueuedCommands)
                    _pending.Dequeue();
                _pending.Enqueue((command, DateTime.UtcNow));
                LogDiag($"PipeServer: no handler yet, queued '{command}'");
                return;
            }
        }
        Invoke(handlers, command);
    }

    private static void Invoke(Action<string> handlers, string command)
    {
        foreach (Action<string> handler in handlers.GetInvocationList())
        {
            try { handler(command); }
            catch (Exception ex) { LogDiag($"PipeServer: handler failed: {ex.Message}"); }
        }
    }

    /// <summary>
    /// Client side: sends one command line to the running instance.
    /// Returns false if no server answered within <paramref name="timeoutMs"/>.
    /// </summary>
    public static bool TrySend(string command, int timeoutMs)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", Program.PipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            client.Connect(timeoutMs);
            using var writer = new StreamWriter(client, new UTF8Encoding(false));
            writer.WriteLine(command);
            writer.Flush();
            return true;
        }
        catch (Exception ex)
        {
            LogDiag($"PipeClient: sending '{command}' failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }
}
