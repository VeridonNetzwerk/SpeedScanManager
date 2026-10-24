using System.IO;
using System.Text;

namespace SpeedScanManager;

/// <summary>
/// Shared diagnostic logger that writes to %LocalAppData%\SpeedScanManager\twain_diag.log.
/// Used by ScanPipeline, ScannerStateService, WiaEventWatcher, and TrayApplicationContext.
/// Thread-safe: all writers share one lock and one append stream, so lines from
/// concurrent threads are never interleaved or lost. The log is rotated to
/// twain_diag.log.1 when it exceeds <see cref="MaxLogBytes"/>.
/// </summary>
internal static class DiagLog
{
    private const long MaxLogBytes = 2 * 1024 * 1024;

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SpeedScanManager", "twain_diag.log");

    private static readonly object Sync = new();
    private static StreamWriter? _writer;

    public static void Write(string msg)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} [{Environment.CurrentManagedThreadId}] {msg}";
        lock (Sync)
        {
            try
            {
                var writer = GetWriter();
                if (writer == null) return;
                writer.WriteLine(line);
                writer.Flush();

                if (writer.BaseStream.Length > MaxLogBytes)
                    Rotate();
            }
            catch
            {
                // Logging must never throw; drop the writer so the next call reopens it.
                CloseWriter();
            }
        }
    }

    public static void WriteWia(string msg)
    {
        Write($"[WIA] {msg}");
    }

    private static StreamWriter? GetWriter()
    {
        if (_writer != null) return _writer;

        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
        // FileShare.ReadWrite | Delete: other processes (elevated /setup helper,
        // /scanbutton launcher) may append to the same file at the same time.
        var fs = new FileStream(LogPath, FileMode.Append, FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete);
        _writer = new StreamWriter(fs, new UTF8Encoding(false)) { AutoFlush = false, NewLine = "\n" };
        return _writer;
    }

    private static void Rotate()
    {
        CloseWriter();
        try
        {
            var old = LogPath + ".1";
            if (File.Exists(old)) File.Delete(old);
            File.Move(LogPath, old);
        }
        catch
        {
            // Another process may hold the file; rotation is retried on the next oversize write.
        }
    }

    private static void CloseWriter()
    {
        try { _writer?.Dispose(); } catch { }
        _writer = null;
    }
}
