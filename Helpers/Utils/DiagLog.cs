using System.IO;

namespace SpeedScanManager;

/// <summary>
/// Shared diagnostic logger that writes to %LocalAppData%\SpeedScanManager\twain_diag.log.
/// Used by ScanPipeline, ScannerStateService, WiaEventWatcher, and TrayApplicationContext.
/// </summary>
internal static class DiagLog
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SpeedScanManager", "twain_diag.log");

    public static void Write(string msg)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} {msg}\n");
        }
        catch { }
    }

    public static void WriteWia(string msg)
    {
        Write($"[WIA] {msg}");
    }
}
