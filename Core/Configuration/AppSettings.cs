using System.Text.Json;

namespace SpeedScanManager;

/// <summary>
/// A user-added application (Anwendung tab, "Installieren/Deinstallieren").
/// After a scan the tray launches: "ExePath" [Arguments] "file1" "file2" ...
/// </summary>
internal class CustomApplication
{
    public string Name { get; set; } = "";
    public string ExePath { get; set; } = "";
    public string Arguments { get; set; } = "";

    public CustomApplication Clone() => new() { Name = Name, ExePath = ExePath, Arguments = Arguments };
}

/// <summary>
/// Persisted application settings stored in %AppData%\SpeedScanManager\settings.json.
/// <see cref="ScanSettings"/> is the single source of truth for the current scan settings;
/// the flat legacy fields (FolderPath, FileNameFormat, ...) are only kept in sync on save so
/// older versions can still read the file, and are used to migrate old files on load.
/// </summary>
internal class AppSettings
{
    public const int CurrentVersion = 2;
    public const int MaxCustomApplications = 10;

    public int Version { get; set; } = CurrentVersion;
    public bool QuickMenuEnabled { get; set; } = true;
    /// <summary>Internal name of the active profile (ScanProfile.Name); "" when none.</summary>
    public string SelectedProfileName { get; set; } = "ScanToFolder";
    public string? SelectedSourceName { get; set; }
    /// <summary>Start SpeedScan Manager when the user logs on (HKCU Run key, written by the tray).</summary>
    public bool AutoStart { get; set; } = true;
    /// <summary>User-added applications (max <see cref="MaxCustomApplications"/>).</summary>
    public List<CustomApplication> CustomApplications { get; set; } = new();

    /// <summary>Current scan settings (full payload). Null only in files written by old versions.</summary>
    public ScanSettings? ScanSettings { get; set; }

    // Legacy fields (version 1). Written for downgrade compatibility, read only for migration.
    public ApplicationType CurrentApplicationType { get; set; } = ApplicationType.ScanToFolder;
    public string FolderPath { get; set; } = "";
    public FileNameFormatDialog.FormatMode FileNameFormat { get; set; } = FileNameFormatDialog.FormatMode.Timestamp;
    public string CustomFileName { get; set; } = FileNamePattern.DefaultCustomName;
    public int CounterDigits { get; set; } = 3;

    private static readonly string SettingsPath = Path.Combine(SettingsStorage.SettingsDir, "settings.json");

    public static AppSettings Load()
    {
        var settings = SettingsStorage.LoadWithFallback(SettingsPath,
            json => JsonSerializer.Deserialize<AppSettings>(json, SettingsStorage.JsonOptions));
        settings ??= new AppSettings();

        settings.SelectedProfileName ??= "";
        settings.CustomApplications = (settings.CustomApplications ?? new())
            .Where(a => a != null && !string.IsNullOrWhiteSpace(a.Name) && !string.IsNullOrWhiteSpace(a.ExePath))
            .Take(MaxCustomApplications)
            .Select(a => { a.Arguments ??= ""; return a; })
            .ToList();
        return settings;
    }

    /// <summary>
    /// Returns the current ScanSettings, migrating old settings files on first use:
    /// the previously selected profile (if any) is the base, then the legacy save fields
    /// stored in settings.json are applied on top. The result is normalized.
    /// </summary>
    public ScanSettings EnsureScanSettings(ProfileManager profileManager)
    {
        if (ScanSettings == null)
        {
            var migrated = new ScanSettings();
            profileManager.GetByName(SelectedProfileName)?.ApplyTo(migrated);
            migrated.ApplicationType = CurrentApplicationType;
            if (!string.IsNullOrEmpty(FolderPath))
                migrated.FolderPath = FolderPath;
            migrated.FileNameFormat = FileNameFormat;
            migrated.CustomFileName = CustomFileName;
            migrated.CounterDigits = CounterDigits;
            ScanSettings = migrated;
            DiagLog.Write("[Settings] Migrated settings.json to version 2");
        }
        ScanSettings.Normalize();
        return ScanSettings;
    }

    public void Save()
    {
        try
        {
            Version = CurrentVersion;
            if (ScanSettings != null)
            {
                CurrentApplicationType = ScanSettings.ApplicationType == ApplicationType.Custom
                    ? ApplicationType.ScanToFolder
                    : ScanSettings.ApplicationType;
                FolderPath = ScanSettings.FolderPath;
                FileNameFormat = ScanSettings.FileNameFormat;
                CustomFileName = ScanSettings.CustomFileName;
                CounterDigits = ScanSettings.CounterDigits;
            }
            var json = JsonSerializer.Serialize(this, SettingsStorage.JsonOptions);
            SettingsStorage.WriteAtomic(SettingsPath, json);
        }
        catch (Exception ex)
        {
            DiagLog.Write($"[Settings] AppSettings save failed: {ex.Message}");
        }
    }
}
