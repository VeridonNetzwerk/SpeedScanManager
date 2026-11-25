using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
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

    /// <summary>Logical name of the embedded payload-less uninstaller build (see SpeedScanInstaller.csproj).</summary>
    private const string UninstallerResourceName = "Uninstaller.exe";

    // ===== Registry views =====

    /// <summary>
    /// View for the HKLM\SOFTWARE keys (Uninstall entry, app key). The app is x86 and the
    /// release installer is published win-x86, so existing installs live in the 32-bit view
    /// (WOW6432Node). Using it explicitly keeps AnyCPU dev builds consistent with that.
    /// </summary>
    public const RegistryView AppRegistryView = RegistryView.Registry32;

    /// <summary>Both views; deletes and lookups go through both (on 32-bit Windows they coincide).</summary>
    public static readonly RegistryView[] AllRegistryViews = { RegistryView.Registry32, RegistryView.Registry64 };

    public static RegistryKey OpenHklm(RegistryView view) => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);

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
                I18n.T("Das Installationspaket wurde nicht gefunden.\n\n") +
                I18n.T("Dieses Setup enthält keine eingebetteten Programmdateien und es wurde auch kein ") +
                I18n.F("\"dist\"-Ordner mit {0} gefunden. Bitte das Setup mit build-release.ps1 erstellen ", AppExeName) +
                I18n.T("oder die App vorher nach \"dist\" veröffentlichen (dotnet publish ... -o dist)."));
        }

        private static bool IncludeFile(string rel)
        {
            if (rel.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(rel, ManifestFileName, StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(rel, UninstallerExeName, StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }
    }

    // ===== OCR models =====

    /// <summary>
    /// Language code of an OCR model in the payload (tessdata\best\ita.traineddata → "ita"),
    /// null for every other file. The payload holds all languages; only some are installed.
    /// </summary>
    public static string? OcrModelCode(string relativePath)
    {
        var parts = relativePath.Replace('/', '\\').Split('\\');
        if (parts.Length == 3 && parts[0].Equals("tessdata", StringComparison.OrdinalIgnoreCase)
            && (parts[1].Equals("best", StringComparison.OrdinalIgnoreCase) || parts[1].Equals("fast", StringComparison.OrdinalIgnoreCase))
            && parts[2].EndsWith(".traineddata", StringComparison.OrdinalIgnoreCase))
            return Path.GetFileNameWithoutExtension(parts[2]).ToLowerInvariant();
        return null;
    }

    /// <summary>OCR language code for a setup / app language ("it-IT" → "ita").</summary>
    public static string? OcrCodeFor(string language) => language switch
    {
        "de-DE" => "deu", "en-US" => "eng", "ja-JP" => "jpn", "fr-FR" => "fra", "es-ES" => "spa",
        "zh-CN" => "chi_sim", "zh-TW" => "chi_tra", "pt-BR" => "por", "ru-RU" => "rus", "ko-KR" => "kor",
        "it-IT" => "ita", "pl-PL" => "pol", "nl-NL" => "nld", "tr-TR" => "tur",
        _ => null
    };

    /// <summary>
    /// OCR languages to install: English (automatic detection), the setup language and those a
    /// previous installation in this folder already had.
    /// </summary>
    public static HashSet<string> OcrLanguagesToInstall(string setupLanguage, IEnumerable<string> previousFiles)
    {
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "eng" };
        if (OcrCodeFor(setupLanguage) is { } code) keep.Add(code);
        foreach (var rel in previousFiles)
            if (OcrModelCode(rel) is { } old) keep.Add(old);
        return keep;
    }

    /// <summary>
    /// Normalizes a relative path to backslashes and rejects anything that would escape the
    /// install directory (absolute paths, "..").
    /// </summary>
    public static string NormalizeRelative(string rel)
    {
        var norm = rel.Replace('/', '\\').TrimStart('\\');
        if (Path.IsPathRooted(norm) || norm.Split('\\').Any(p => p == ".."))
            throw new InvalidDataException(I18n.F("Ungültiger Pfad im Installationspaket: {0}", rel));
        return norm;
    }

    /// <summary>
    /// Combines installDir and rel, guaranteeing the result lies inside installDir — also
    /// physically: installDir itself and every existing directory between it and the result
    /// must not be a reparse point (junction, symlink, mount point), otherwise the elevated
    /// installer/uninstaller could be redirected to write or delete elsewhere. The last
    /// component itself may be a link; callers delete such links without following them.
    /// </summary>
    public static string ResolveInside(string installDir, string rel)
    {
        var rootDir = Path.GetFullPath(installDir).TrimEnd('\\');
        var root = rootDir + "\\";
        var full = Path.GetFullPath(Path.Combine(root, NormalizeRelative(rel)));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(I18n.F("Pfad liegt außerhalb des Installationsordners: {0}", rel));

        if (IsReparsePoint(rootDir))
            throw new IOException(I18n.F("Der Installationsordner ist ein Link (Junction/Symlink) und wird nicht verwendet: {0}", rootDir));
        for (var dir = Path.GetDirectoryName(full); dir != null && dir.Length > rootDir.Length; dir = Path.GetDirectoryName(dir))
        {
            if (IsReparsePoint(dir))
                throw new IOException(I18n.F("Pfad führt über einen Link (Junction/Symlink) und wird nicht verwendet: {0}", dir));
        }
        return full;
    }

    /// <summary>True if path exists and is a reparse point (the link itself is inspected, not its target).</summary>
    public static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }

    /// <summary>
    /// Creates (or replaces) installDir\rel for writing. An existing file or link at that
    /// path is deleted first instead of being opened, so neither a symlink nor a hard link
    /// can redirect the write to a file outside the install dir.
    /// </summary>
    public static FileStream CreateFileInside(string installDir, string rel)
    {
        var path = ResolveInside(installDir, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        path = ResolveInside(installDir, rel); // re-check the directories just created
        if (File.Exists(path) || IsReparsePoint(path))
            File.Delete(path);
        return new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    }

    /// <summary>
    /// Deletes installDir\rel if it exists. A link is removed itself, never its target;
    /// paths that lead through a link are refused (see <see cref="ResolveInside"/>).
    /// </summary>
    public static void DeleteFileInside(string installDir, string rel, string? skipPath = null)
    {
        var path = ResolveInside(installDir, rel);
        if (skipPath != null && string.Equals(path, skipPath, StringComparison.OrdinalIgnoreCase))
            return;
        FileAttributes attr;
        try { attr = File.GetAttributes(path); }
        catch (FileNotFoundException) { return; }
        catch (DirectoryNotFoundException) { return; }

        if ((attr & FileAttributes.Directory) != 0)
        {
            // A directory where the manifest lists a file: only remove it if it is a link
            // (RemoveDirectory deletes the link, not the target's contents).
            if ((attr & FileAttributes.ReparsePoint) != 0)
                Directory.Delete(path, false);
            return;
        }
        File.Delete(path);
    }

    /// <summary>
    /// Restricts the install dir's ACL when it is outside Program Files (where the inherited
    /// ACL would typically let standard users modify it): no inheritance, Administrators and
    /// SYSTEM full control, Users read and execute, owner Administrators. Prevents a standard
    /// user from tampering with install-manifest.txt, the exe the elevated setup starts, or
    /// planting junctions. Throws on failure.
    /// </summary>
    public static void SecureInstallDirectory(string installDir)
    {
        if (IsUnderProgramFiles(installDir))
            return;

        var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        const InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;

        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(admins, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(system, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(users, FileSystemRights.ReadAndExecute, inherit, PropagationFlags.None, AccessControlType.Allow));
        var dirInfo = new DirectoryInfo(installDir);
        dirInfo.SetAccessControl(security);

        // A folder created beforehand by a standard user is owned by that user, and the owner
        // can always rewrite the ACL — hand ownership to Administrators.
        var owner = new DirectorySecurity();
        owner.SetOwner(admins);
        dirInfo.SetAccessControl(owner);
    }

    private static bool IsUnderProgramFiles(string dir)
    {
        var full = Path.GetFullPath(dir).TrimEnd('\\') + "\\";
        return new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetEnvironmentVariable("ProgramW6432") ?? "",
            }
            .Where(p => !string.IsNullOrEmpty(p))
            .Any(p => full.StartsWith(Path.GetFullPath(p).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase));
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
            error = I18n.T("Bitte geben Sie einen Installationspfad an.");
            return null;
        }

        string full;
        try
        {
            if (!Path.IsPathFullyQualified(raw))
            {
                error = I18n.T("Bitte geben Sie einen vollständigen Pfad an (z. B. C:\\Programme\\SpeedScanManager).");
                return null;
            }
            if (raw.StartsWith(@"\\"))
            {
                error = I18n.T("Netzwerkpfade werden nicht unterstützt. Bitte wählen Sie einen lokalen Ordner.");
                return null;
            }
            full = Path.GetFullPath(raw).TrimEnd('\\');
        }
        catch (Exception ex)
        {
            error = I18n.F("Der Pfad ist ungültig: {0}", ex.Message);
            return null;
        }

        if (!string.Equals(Path.GetFileName(full), AppFolderName, StringComparison.OrdinalIgnoreCase))
            full = Path.Combine(full, AppFolderName);

        if (!IsSafeAppDirectory(full, out error))
            return null;

        if (File.Exists(full))
        {
            error = I18n.T("Unter diesem Pfad existiert bereits eine Datei.");
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
            error = I18n.T("Kein gültiger Installationsordner.");
            return false;
        }

        var full = Path.GetFullPath(dir).TrimEnd('\\');
        var root = Path.GetPathRoot(full)?.TrimEnd('\\') ?? "";
        if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
        {
            error = I18n.T("Ein Laufwerks-Stammverzeichnis kann nicht als Installationsordner verwendet werden.");
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
                error = I18n.F("Dieser Ordner ist ein Systemordner und kann nicht verwendet werden:\n{0}", full);
                return false;
            }
        }

        // The app's own data folders must never double as install dir ("Einstellungen löschen" removes them).
        foreach (var dataRoot in new[] { Environment.SpecialFolder.ApplicationData, Environment.SpecialFolder.LocalApplicationData })
        {
            var dataDir = Path.Combine(Environment.GetFolderPath(dataRoot), AppFolderName);
            if (string.Equals(full, dataDir, StringComparison.OrdinalIgnoreCase))
            {
                error = I18n.T("Der Einstellungsordner von SpeedScan Manager kann nicht als Installationsordner verwendet werden.");
                return false;
            }
        }

        var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\');
        if (!string.IsNullOrEmpty(windir) && full.StartsWith(windir + "\\", StringComparison.OrdinalIgnoreCase))
        {
            error = I18n.T("Eine Installation im Windows-Ordner ist nicht erlaubt.");
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
        // 32-bit view first (release installs); 64-bit view only for older AnyCPU dev builds.
        foreach (var view in AllRegistryViews)
        {
            try
            {
                using var hklm = OpenHklm(view);
                using var key = hklm.OpenSubKey(UninstallKeyPath);
                var loc = key?.GetValue("InstallLocation") as string;
                if (!string.IsNullOrWhiteSpace(loc))
                    return loc.Trim().Trim('"').TrimEnd('\\');
            }
            catch { }
        }
        return null;
    }

    /// <summary>
    /// Deletes the Uninstall entry and the app key (HKLM\SOFTWARE\VeridonNetzwerk\SpeedScanManager)
    /// in both registry views, plus the publisher key when it is empty. With keepScanButtonBackup
    /// the app key's "RemovedHandlers" backup survives (only "InstallDir" is removed) so a failed
    /// scanner-button restore can still be repeated later.
    /// </summary>
    public static void DeleteProductRegistryKeys(bool keepScanButtonBackup)
    {
        foreach (var view in AllRegistryViews)
        {
            try
            {
                using var hklm = OpenHklm(view);
                try { hklm.DeleteSubKeyTree(UninstallKeyPath, false); } catch { }

                if (keepScanButtonBackup)
                {
                    try
                    {
                        using var appKey = hklm.OpenSubKey(AppKeyPath, writable: true);
                        appKey?.DeleteValue("InstallDir", false);
                    }
                    catch { }
                }
                else
                {
                    try { hklm.DeleteSubKeyTree(AppKeyPath, false); } catch { }
                }

                try
                {
                    bool publisherKeyEmpty;
                    using (var pubKey = hklm.OpenSubKey(PublisherKeyPath))
                        publisherKeyEmpty = pubKey != null && pubKey.SubKeyCount == 0 && pubKey.ValueCount == 0;
                    if (publisherKeyEmpty)
                        hklm.DeleteSubKey(PublisherKeyPath, false);
                }
                catch { }
            }
            catch { }
        }
    }

    /// <summary>
    /// Removes the autostart value from HKCU\...\Run of the current (elevated) account and of
    /// every user hive loaded under HKEY_USERS — that includes the logged-on, non-elevated user
    /// who started the uninstaller when a different admin account was used for elevation.
    /// Users who are not logged on (hive not loaded) keep their value; it then points to a
    /// missing exe and Windows ignores it.
    /// </summary>
    public static void RemoveAutostartEntries()
    {
        try
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            runKey?.DeleteValue(RunValueName, false);
        }
        catch { }

        try
        {
            using var users = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Registry64);
            foreach (var sid in users.GetSubKeyNames())
            {
                if (sid.EndsWith("_Classes", StringComparison.OrdinalIgnoreCase))
                    continue;
                try
                {
                    using var runKey = users.OpenSubKey($@"{sid}\{RunKeyPath}", writable: true);
                    runKey?.DeleteValue(RunValueName, false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Removing autostart for {sid} failed: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Enumerating HKEY_USERS failed: {ex.Message}");
        }
    }

    // ===== Install manifest =====

    /// <summary>Reads the list of installed files (relative paths), or null if there is no manifest.</summary>
    public static List<string>? ReadManifest(string installDir)
    {
        var path = ResolveInside(installDir, ManifestFileName);
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
        using var stream = CreateFileInside(installDir, ManifestFileName);
        using var writer = new StreamWriter(stream);
        foreach (var line in lines)
            writer.WriteLine(line);
    }

    /// <summary>Enumeration that neither returns nor descends into reparse points.</summary>
    private static readonly EnumerationOptions NoLinksRecursive = new()
    {
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
        IgnoreInaccessible = true,
    };

    /// <summary>
    /// Files to remove for an install without manifest (v0.1.x). That installer copied every
    /// top-level *.exe/*.dll/*.pdb of its dist folder plus x86\ and x64\, so the file set can
    /// only be reconstructed by pattern. That is safe only in a folder that belongs to the app
    /// (leaf name "SpeedScanManager"); anywhere else (e.g. a shared tools folder) only
    /// SpeedScanManager.exe and uninstall.exe are returned and restricted is true — the caller
    /// must tell the user to remove the remaining files manually.
    /// </summary>
    public static List<string> GetLegacyFileList(string installDir, out bool restricted)
    {
        var result = new List<string> { AppExeName, UninstallerExeName, "scanbutton.vbs" };
        restricted = !string.Equals(Path.GetFileName(Path.GetFullPath(installDir).TrimEnd('\\')),
            AppFolderName, StringComparison.OrdinalIgnoreCase);
        if (restricted || IsReparsePoint(installDir))
            return result;

        foreach (var f in Directory.GetFiles(installDir, "*", new EnumerationOptions { AttributesToSkip = FileAttributes.ReparsePoint }))
        {
            var ext = Path.GetExtension(f).ToLowerInvariant();
            if (ext is ".exe" or ".dll" or ".pdb")
                result.Add(Path.GetFileName(f));
        }
        foreach (var sub in new[] { "x86", "x64" })
        {
            var d = Path.Combine(installDir, sub);
            if (Directory.Exists(d) && !IsReparsePoint(d))
                result.AddRange(Directory.GetFiles(d, "*", NoLinksRecursive)
                    .Select(f => Path.GetRelativePath(installDir, f)));
        }
        return result;
    }

    /// <summary>
    /// Deletes empty directories below installDir (deepest first), never installDir itself.
    /// Never descends into a reparse point; such a link is removed itself (not its target).
    /// </summary>
    public static void RemoveEmptySubdirectories(string installDir)
    {
        if (!Directory.Exists(installDir) || IsReparsePoint(installDir)) return;
        try
        {
            foreach (var d in Directory.GetDirectories(installDir))
                RemoveEmptyTree(d);
        }
        catch { }
    }

    private static void RemoveEmptyTree(string dir)
    {
        try
        {
            if (IsReparsePoint(dir))
            {
                Directory.Delete(dir, false); // removes the link only
                return;
            }
            foreach (var sub in Directory.GetDirectories(dir))
                RemoveEmptyTree(sub);
            if (!Directory.EnumerateFileSystemEntries(dir).Any())
                Directory.Delete(dir, false);
        }
        catch { }
    }

    /// <summary>
    /// Removes the files of an installation: those in its manifest, or the legacy file set
    /// (see <see cref="GetLegacyFileList"/>), the manifest itself and empty subfolders.
    /// skipPath (the running uninstaller) is left alone. Returns false if the legacy set had
    /// to be restricted, i.e. files the user must remove manually may remain.
    /// </summary>
    public static bool RemoveInstalledFiles(string installDir, string? skipPath)
    {
        if (IsReparsePoint(installDir))
            throw new IOException(I18n.F("Der Installationsordner ist ein Link (Junction/Symlink) und wird nicht verwendet: {0}", installDir));

        var restricted = false;
        var files = ReadManifest(installDir) ?? GetLegacyFileList(installDir, out restricted);
        files.Add(ManifestFileName);
        files.Add("scanbutton.vbs");

        foreach (var rel in files.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try { DeleteFileInside(installDir, rel, skipPath); }
            catch (Exception ex) { Debug.WriteLine($"Deleting {rel} failed: {ex.Message}"); }
        }
        RemoveEmptySubdirectories(installDir);
        return !restricted;
    }

    /// <summary>User message for <see cref="RemoveInstalledFiles"/> returning false.</summary>
    public static string LegacyLeftoverMessage(string installDir) =>
        I18n.F("Für die ältere Installation in \"{0}\" gibt es keine Dateiliste. Da dies kein eigener {1}-Ordner ist, " +
            "wurden nur {2} und {3} entfernt. Bitte löschen Sie die übrigen Programmdateien (DLLs sowie die Ordner x86 und x64) manuell.",
            installDir, AppFolderName, AppExeName, UninstallerExeName);

    /// <summary>
    /// Writes the uninstaller to installDir\uninstall.exe: the payload-less build embedded by
    /// build-release.ps1, or (dev builds without it) a copy of the running setup.
    /// </summary>
    public static void WriteUninstaller(string installDir)
    {
        var source = Assembly.GetExecutingAssembly().GetManifestResourceStream(UninstallerResourceName);
        if (source == null)
        {
            var self = Environment.ProcessPath;
            if (self == null) throw new InvalidOperationException(I18n.T("Der Pfad des Setups konnte nicht ermittelt werden."));
            if (string.Equals(Path.GetFullPath(self), ResolveInside(installDir, UninstallerExeName), StringComparison.OrdinalIgnoreCase))
                return; // running as the installed uninstaller already
            source = File.OpenRead(self);
        }
        using (source)
        using (var dst = CreateFileInside(installDir, UninstallerExeName))
            source.CopyTo(dst);
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
            var bytes = Directory.GetFiles(dir, "*", NoLinksRecursive).Sum(f => new FileInfo(f).Length);
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
