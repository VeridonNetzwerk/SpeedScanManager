using System.Text.Json;

namespace SpeedScanManager;

/// <summary>
/// Manages scan profiles: loading, saving, creating defaults.
/// Persists to %AppData%\SpeedScanManager\profiles.json
/// </summary>
internal class ProfileManager
{
    private static readonly string ProfileDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SpeedScanManager");

    private static readonly string ProfilePath = Path.Combine(ProfileDir, "profiles.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public List<ScanProfile> Profiles { get; } = new();

    public ProfileManager()
    {
        Load();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(ProfilePath))
            {
                var json = File.ReadAllText(ProfilePath);
                var loaded = JsonSerializer.Deserialize<List<ScanProfile>>(json, JsonOpts);
                if (loaded != null)
                {
                    Profiles.AddRange(loaded);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Profile load failed: {ex.Message}");
        }

        // First start: create default profiles
        CreateDefaultProfiles();
    }

    private void CreateDefaultProfiles()
    {
        var defaultSettings = new ScanSettings();
        var defaultFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SpeedScanManager");

        Profiles.Add(new ScanProfile
        {
            Name = "Standard",
            IsBuiltIn = true,
            ApplicationType = ApplicationType.ScanToFolder,
            FolderPath = defaultFolder,
            FileNameFormat = FileNameFormatDialog.FormatMode.Timestamp,
            CustomFileName = "unbenannt",
            CounterDigits = 3
        });

        Profiles.Add(new ScanProfile
        {
            Name = "Scan to Folder",
            IsBuiltIn = true,
            ApplicationType = ApplicationType.ScanToFolder,
            FolderPath = defaultFolder,
            FileNameFormat = FileNameFormatDialog.FormatMode.Timestamp,
            CustomFileName = "unbenannt",
            CounterDigits = 3
        });

        Save();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ProfileDir);
            var json = JsonSerializer.Serialize(Profiles, JsonOpts);
            File.WriteAllText(ProfilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Profile save failed: {ex.Message}");
        }
    }

    public ScanProfile? GetByName(string name)
    {
        return Profiles.FirstOrDefault(p => p.Name == name);
    }

    public void AddProfile(ScanProfile profile)
    {
        Profiles.Add(profile);
        Save();
    }

    public void RemoveProfile(int index)
    {
        if (index >= 0 && index < Profiles.Count && !Profiles[index].IsBuiltIn)
        {
            Profiles.RemoveAt(index);
            Save();
        }
    }

    public void RenameProfile(int index, string newName)
    {
        if (index >= 0 && index < Profiles.Count && !Profiles[index].IsBuiltIn)
        {
            Profiles[index].Name = newName;
            Save();
        }
    }

    public void MoveUp(int index)
    {
        if (index > 0 && index < Profiles.Count && !Profiles[index].IsBuiltIn)
        {
            (Profiles[index - 1], Profiles[index]) = (Profiles[index], Profiles[index - 1]);
            Save();
        }
    }

    public void MoveDown(int index)
    {
        if (index >= 0 && index < Profiles.Count - 1 && !Profiles[index].IsBuiltIn)
        {
            (Profiles[index + 1], Profiles[index]) = (Profiles[index], Profiles[index + 1]);
            Save();
        }
    }
}
