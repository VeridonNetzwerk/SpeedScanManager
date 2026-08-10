using System.Text.Json;

namespace SpeedScanManager;

/// <summary>
/// Persisted application settings stored in %AppData%\SpeedScanManager\settings.json.
/// Includes ScanSettings, QuickMenu flag, and selected profile name.
/// </summary>
internal class AppSettings
{
    public bool QuickMenuEnabled { get; set; } = true;
    public string SelectedProfileName { get; set; } = "Standard";
    public ApplicationType CurrentApplicationType { get; set; } = ApplicationType.ScanToFolder;
    public string FolderPath { get; set; } = "";
    public FileNameFormatDialog.FormatMode FileNameFormat { get; set; } = FileNameFormatDialog.FormatMode.Timestamp;
    public string CustomFileName { get; set; } = "unbenannt";
    public int CounterDigits { get; set; } = 3;

    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SpeedScanManager");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOpts);
                if (settings != null)
                    return settings;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AppSettings load failed: {ex.Message}");
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(this, JsonOpts);
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AppSettings save failed: {ex.Message}");
        }
    }
}
