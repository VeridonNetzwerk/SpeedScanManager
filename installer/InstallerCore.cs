using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using Microsoft.Win32;

namespace SpeedScanInstaller;

/// <summary>
/// Shared install/uninstall logic: payload access, path validation, install manifest,
/// registry constants and process handling.
/// </summary>
internal static class InstallerCore
{
    public const string ProductName = "SpeedScan Manager";
    public const string Publisher = "VeridonNetzwerk";
    public const string AppFolderName = "SpeedScanManager";
    public const string AppExeName = "SpeedScanManager.exe";
    public const string AppProcessName = "SpeedScanManager";
    public const string UninstallerExeName = "uninstall.exe";
    public const string ManifestFileName = "install-manifest.txt";
    public const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\SpeedScanManager";
    public const string PublisherKeyPath = @"Software\" + Publisher;
    public const string AppKeyPath = PublisherKeyPath + @"\SpeedScanManager";
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string RunValueName = "SpeedScanManager";

    /// <summary>Logical name of the embedded app zip (see SpeedScanInstaller.csproj).</summary>
    private const string PayloadResourceName = "AppPayload.zip";

    // ===== Payload =====

    /// <summary>One file of the app payload, addressed by its path relative to the install dir.</summary>
    public sealed record PayloadFile(string RelativePath, long Length, Func<Stream> Open);

    /// <summary>
    /// The app files to install. Source is (in order): the zip embedded in the installer,
    /// a "dist" folder next to the installer exe, or the dev tree's dist folder.
    /// </summary>
    public sealed class Payload : IDisposable
    {
        private readonly IDisposable? _owner;

        public string Source { get; }
        public IReadOnlyList<PayloadFile> Files { get; }

        private Payload(string source, IReadOnlyList<PayloadFile> files, IDisposable? owner)
        {
            Source = source;
            Files = files;
            _owner = owner;
        }

        public void Dispose() => _owner?.Dispose();

        public static Payload Open()
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResourceName);
            if (stream != null)
            {
                var zip = new ZipArchive(stream, ZipArchiveMode.Read);
                var files = zip.Entries
                    .Where(e => !string.IsNullOrEmpty(e.Name)) // skip directory entries
                    .Select(e => new PayloadFile(NormalizeRelative(e.FullName), e.Length, e.Open))
                    .Where(f => IncludeFile(f.RelativePath))
                    .ToList();
                return new Payload("eingebettetes Paket", files, zip);
            }

            var baseDir = AppContext.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDir, "dist"),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "dist")),
            };
            foreach (var dir in candidates)
            {
                if (!File.Exists(Path.Combine(dir, AppExeName)))
                    continue;
                var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
                    .Select(f => new PayloadFile(
                        NormalizeRelative(Path.GetRelativePath(dir, f)),
                        new FileInfo(f).Length,
                        () => File.OpenRead(f)))
                    .Where(f => IncludeFile(f.RelativePath))
                    .ToList();
                return new Payload(dir, files, null);
            }

            throw new InvalidOperationException(
                "Das Installationspaket wurde nicht gefunden.\n\n" +
                "Dieses Setup enthält keine eingebetteten Programmdateien und es wurde auch kein " +
                $"\"dist\"-Ordner mit {AppExeName} gefunden. Bitte das Setup mit build-release.ps1 erstellen " +
                "oder die App vorher nach \"dist\" veröffentlichen (dotnet publish ... -o dist).");
        }

        private static bool IncludeFile(string rel)
        {
            if (rel.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(rel, ManifestFileName, StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(rel, UninstallerExeName, StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }
    }

    /// <summary>
    /// Normalizes a relative path to backslashes and rejects anything that would escape the
    /// install directory (absolute paths, "..").
    /// </summary>
    public static string NormalizeRelative(string rel)
    {
        var norm = rel.Replace('/', '\\').TrimStart('\\');
        if (Path.IsPathRooted(norm) || norm.Split('\\').Any(p => p == ".."))
            throw new InvalidDataException($"Ungültiger Pfad im Installationspaket: {rel}");
        return norm;
    }

    /// <summary>Combines installDir and rel, guaranteeing the result lies inside installDir.</summary>
    public static string ResolveInside(string installDir, string rel)
    {
        var root = Path.GetFullPath(installDir).TrimEnd('\\') + "\\";
        var full = Path.GetFullPath(Path.Combine(root, NormalizeRelative(rel)));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Pfad liegt außerhalb des Installationsordners: {rel}");
        return full;
    }

    // ===== Install path validation =====

    /// <summary>
    /// Validates a user-entered install path. Appends the "SpeedScanManager" folder if the path
    /// doesn't already end with it. Returns null and an error message if the path is unsafe.
    /// </summary>
    public static string? ValidateInstallPath(string? input, out string error)
    {
        error = "";
        var raw = Environment.ExpandEnvironmentVariables((input ?? "").Trim().Trim('"'));
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "Bitte geben Sie einen Installationspfad an.";
            return null;
        }

        string full;
        try
        {
            if (!Path.IsPathFullyQualified(raw))
            {
                error = "Bitte geben Sie einen vollständigen Pfad an (z. B. C:\\Programme\\SpeedScanManager).";
                return null;
            }
            if (raw.StartsWith(@"\\"))
            {
                error = "Netzwerkpfade werden nicht unterstützt. Bitte wählen Sie einen lokalen Ordner.";
                return null;
            }
            full = Path.GetFullPath(raw).TrimEnd('\\');
        }
        catch (Exception ex)
        {
            error = $"Der Pfad ist ungültig: {ex.Message}";
            return null;
        }

        if (!string.Equals(Path.GetFileName(full), AppFolderName, StringComparison.OrdinalIgnoreCase))
            full = Path.Combine(full, AppFolderName);

        if (!IsSafeAppDirectory(full, out error))
            return null;

        if (File.Exists(full))
        {
            error = "Unter diesem Pfad existiert bereits eine Datei.";
            return null;
        }
        return full;
    }

    /// <summary>
    /// True if dir is a plausible, non-system directory for the app (used for install and,
    /// again, before uninstall deletes anything).
    /// </summary>
    public static bool IsSafeAppDirectory(string? dir, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(dir) || !Path.IsPathFullyQualified(dir))
        {
            error = "Kein gültiger Installationsordner.";
            return false;
        }

        var full = Path.GetFullPath(dir).TrimEnd('\\');
        var root = Path.GetPathRoot(full)?.TrimEnd('\\') ?? "";
        if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
        {
            error = "Ein Laufwerks-Stammverzeichnis kann nicht als Installationsordner verwendet werden.";
            return false;
        }

        var forbidden = new[]
        {
            Environment.SpecialFolder.Windows,
            Environment.SpecialFolder.System,
            Environment.SpecialFolder.SystemX86,
            Environment.SpecialFolder.ProgramFiles,
            Environment.SpecialFolder.ProgramFilesX86,
            Environment.SpecialFolder.CommonProgramFiles,
            Environment.SpecialFolder.CommonProgramFilesX86,
            Environment.SpecialFolder.UserProfile,
            Environment.SpecialFolder.Desktop,
            Environment.SpecialFolder.DesktopDirectory,
            Environment.SpecialFolder.CommonDesktopDirectory,
            Environment.SpecialFolder.MyDocuments,
            Environment.SpecialFolder.CommonDocuments,
            Environment.SpecialFolder.ApplicationData,
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolder.CommonApplicationData,
        }
        .Select(f => Environment.GetFolderPath(f))
        .Append(GetDownloadsFolder())
        .Append(Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)) ?? "")
        .Where(p => !string.IsNullOrEmpty(p))
        .Select(p => Path.GetFullPath(p).TrimEnd('\\'));

        foreach (var f in forbidden)
        {
            if (string.Equals(full, f, StringComparison.OrdinalIgnoreCase) ||
                f.StartsWith(full + "\\", StringComparison.OrdinalIgnoreCase))
            {
                error = $"Dieser Ordner ist ein Systemordner und kann nicht verwendet werden:\n{full}";
                return false;
            }
        }

        // The app's own data folders must never double as install dir ("Einstellungen löschen" removes them).
        foreach (var dataRoot in new[] { Environment.SpecialFolder.ApplicationData, Environment.SpecialFolder.LocalApplicationData })
        {
            var dataDir = Path.Combine(Environment.GetFolderPath(dataRoot), AppFolderName);
            if (string.Equals(full, dataDir, StringComparison.OrdinalIgnoreCase))
            {
                error = "Der Einstellungsordner von SpeedScan Manager kann nicht als Installationsordner verwendet werden.";
                return false;
            }
        }

        var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\');
        if (!string.IsNullOrEmpty(windir) && full.StartsWith(windir + "\\", StringComparison.OrdinalIgnoreCase))
        {
            error = "Eine Installation im Windows-Ordner ist nicht erlaubt.";
            return false;
        }
        return true;
    }

    private static string GetDownloadsFolder()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrEmpty(profile) ? "" : Path.Combine(profile, "Downloads");
    }

    // ===== Existing installation =====

    /// <summary>Install location of an existing installation from the Uninstall key, or null.</summary>
    public static string? GetExistingInstallLocation()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(UninstallKeyPath);
            var loc = key?.GetValue("InstallLocation") as string;
            return string.IsNullOrWhiteSpace(loc) ? null : loc.Trim().Trim('"').TrimEnd('\\');
        }
        catch
        {
            return null;
        }
    }

    // ===== Install manifest =====

    /// <summary>Reads the list of installed files (relative paths), or null if there is no manifest.</summary>
    public static List<string>? ReadManifest(string installDir)
    {
        var path = Path.Combine(installDir, ManifestFileName);
        if (!File.Exists(path)) return null;
        var result = new List<string>();
        foreach (var line in File.ReadAllLines(path))
        {
            var t = line.Trim();
            if (t.Length == 0 || t.StartsWith('#')) continue;
            try { result.Add(NormalizeRelative(t)); } catch { /* ignore invalid lines */ }
        }
        return result;
    }

    public static void WriteManifest(string installDir, IEnumerable<string> relativePaths)
    {
        var lines = new List<string>
        {
            $"# {ProductName} – installierte Dateien (vom Deinstallationsprogramm verwendet)",
        };
        lines.AddRange(relativePaths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.OrdinalIgnoreCase));
        File.WriteAllLines(Path.Combine(installDir, ManifestFileName), lines);
    }

    /// <summary>
    /// Files a pre-manifest installer (v0.1.x) copied: top-level exe/dll/pdb/vbs plus x86\ and x64\.
    /// Used only when an existing install has no manifest.
    /// </summary>
    public static List<string> GetLegacyFileList(string installDir)
    {
        var result = new List<string>();
        foreach (var f in Directory.GetFiles(installDir, "*", SearchOption.TopDirectoryOnly))
        {
            var ext = Path.GetExtension(f).ToLowerInvariant();
            if (ext is ".exe" or ".dll" or ".pdb" or ".vbs")
                result.Add(Path.GetFileName(f));
        }
        foreach (var sub in new[] { "x86", "x64" })
        {
            var d = Path.Combine(installDir, sub);
            if (Directory.Exists(d))
                result.AddRange(Directory.GetFiles(d, "*", SearchOption.AllDirectories)
                    .Select(f => Path.GetRelativePath(installDir, f)));
        }
        return result;
    }

    /// <summary>Deletes empty directories below installDir (deepest first), never installDir itself.</summary>
    public static void RemoveEmptySubdirectories(string installDir)
    {
        if (!Directory.Exists(installDir)) return;
        foreach (var d in Directory.GetDirectories(installDir, "*", SearchOption.AllDirectories)
                     .OrderByDescending(d => d.Length))
        {
            try
            {
                if (!Directory.EnumerateFileSystemEntries(d).Any())
                    Directory.Delete(d);
            }
            catch { }
        }
    }

    // ===== Processes =====

    public static Process[] GetRunningAppProcesses() => Process.GetProcessesByName(AppProcessName);

    /// <summary>Terminates all running SpeedScanManager processes. Returns false if one survived.</summary>
    public static bool KillRunningApp()
    {
        foreach (var proc in GetRunningAppProcesses())
        {
            try
            {
                proc.Kill(entireProcessTree: true);
                proc.WaitForExit(5000);
            }
            catch { }
            finally { proc.Dispose(); }
        }
        return GetRunningAppProcesses().Length == 0;
    }

    /// <summary>
    /// Runs SpeedScanManager.exe with the given argument (hidden, already elevated through the
    /// installer) and waits up to timeout. Returns the exit code, or null on failure/timeout.
    /// </summary>
    public static async Task<int?> RunAppHelperAsync(string installDir, string argument, TimeSpan timeout)
    {
        var exe = Path.Combine(installDir, AppExeName);
        if (!File.Exists(exe)) return null;
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = argument,
                WorkingDirectory = installDir,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (proc == null) return null;
            using var cts = new CancellationTokenSource(timeout);
            try
            {
                await proc.WaitForExitAsync(cts.Token);
                return proc.ExitCode;
            }
            catch (OperationCanceledException)
            {
                try { proc.Kill(); } catch { }
                Debug.WriteLine($"{AppExeName} {argument} timed out");
                return null;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"{AppExeName} {argument} failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>Starts a program non-elevated (via explorer.exe) from an elevated process.</summary>
    public static void StartDeElevated(string exePath)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
                Arguments = $"\"{exePath}\"",
                UseShellExecute = false,
            });
        }
        catch
        {
            Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
        }
    }

    // ===== Misc =====

    public static long GetDirectorySizeKb(string dir)
    {
        try
        {
            var bytes = Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
            return bytes / 1024;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>Product version of the installed exe, falling back to the installer's own version.</summary>
    public static string GetAppVersion(string installDir)
    {
        try
        {
            var exe = Path.Combine(installDir, AppExeName);
            if (File.Exists(exe))
            {
                var info = FileVersionInfo.GetVersionInfo(exe);
                if (info.FileMajorPart + info.FileMinorPart + info.FileBuildPart > 0)
                    return $"{info.FileMajorPart}.{info.FileMinorPart}.{info.FileBuildPart}";
            }
        }
        catch { }
        var v = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
        return $"{v.Major}.{v.Minor}.{v.Build}";
    }
}
