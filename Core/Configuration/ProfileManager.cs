using System.Text.Json;

namespace SpeedScanManager;

/// <summary>
/// Manages scan profiles: loading, saving, creating defaults.
/// Persists to %AppData%\SpeedScanManager\profiles.json (atomic write, .bak kept,
/// unreadable files are quarantined instead of being overwritten).
/// </summary>
internal class ProfileManager
{
    public const int MaxProfiles = 20;
    public const int MaxNameLength = 64;

    private static readonly string ProfilePath = Path.Combine(SettingsStorage.SettingsDir, "profiles.json");

    public List<ScanProfile> Profiles { get; } = new();

    /// <summary>A user profile was renamed (old name, new name).</summary>
    public event Action<string, string>? ProfileRenamed;

    /// <summary>A user profile was deleted (its name).</summary>
    public event Action<string>? ProfileRemoved;

    /// <summary>The list changed (added, removed, renamed, moved or saved). Open windows refresh on it.</summary>
    public event Action? Changed;

    public ProfileManager()
    {
        Load();
    }

    private void Load()
    {
        bool plaintextSecret = false;
        var loaded = SettingsStorage.LoadWithFallback(ProfilePath, json =>
        {
            plaintextSecret = SettingsStorage.ContainsPlaintextSecret(json);
            return ParseProfiles(json);
        });
        if (loaded != null && loaded.Count > 0)
        {
            Profiles.AddRange(loaded.Take(MaxProfiles));

            // Legacy profiles stored the PDF password in plain text: rewrite the file right
            // away (DPAPI-protected) and replace the .bak copy that still contains it.
            if (plaintextSecret || SettingsStorage.FileContainsPlaintextSecret(ProfilePath + ".bak"))
            {
                DiagLog.Write("[Settings] profiles.json contained a plain-text password, rewriting");
                Save();
                SettingsStorage.ScrubBackup(ProfilePath);
            }
            return;
        }

        // First start (or nothing usable on disk): create default profiles
        CreateDefaultProfiles();
    }

    /// <summary>
    /// Parses profiles.json. Supports the current format ({ Name, IsBuiltIn, Settings })
    /// and the legacy flat format where all settings were stored on the profile itself.
    /// </summary>
    private static List<ScanProfile>? ParseProfiles(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new JsonException("profiles.json is not an array");

        var result = new List<ScanProfile>();
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
                continue;

            string name = GetString(element, "Name") ?? "";
            if (string.IsNullOrWhiteSpace(name))
                continue;

            ScanSettings settings;
            if (TryGetProperty(element, "Settings", out var settingsElement) && settingsElement.ValueKind == JsonValueKind.Object)
            {
                settings = settingsElement.Deserialize<ScanSettings>(SettingsStorage.ReadOptions) ?? new ScanSettings();
            }
            else
            {
                // Legacy flat profile: setting names match ScanSettings properties
                settings = element.Deserialize<ScanSettings>(SettingsStorage.ReadOptions) ?? new ScanSettings();
                string? legacyPassword = GetString(element, "PdfPassword");
                if (!string.IsNullOrEmpty(legacyPassword))
                    settings.PdfPassword = legacyPassword;
            }
            settings.Normalize();

            bool isBuiltIn = TryGetProperty(element, "IsBuiltIn", out var b)
                             && (b.ValueKind == JsonValueKind.True);

            if (result.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
                continue; // drop duplicates

            result.Add(new ScanProfile { Name = name.Trim(), IsBuiltIn = isBuiltIn, Settings = settings });
        }
        return result;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static string? GetString(JsonElement element, string name)
        => TryGetProperty(element, name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private void CreateDefaultProfiles()
    {
        var appTypes = new[]
        {
            ApplicationType.ScanToFolder,
            ApplicationType.ScanToEmail,
            ApplicationType.ScanToPrint,
            ApplicationType.ScanToWord,
            ApplicationType.ScanToExcel,
            ApplicationType.ScanToPowerPoint,
            ApplicationType.ScanPictureFolder,
            ApplicationType.EditWithPdf
        };

        foreach (var appType in appTypes)
        {
            var settings = new ScanSettings
            {
                ApplicationType = appType,
                FolderPath = ScanSettings.DefaultFolderPath
            };
            settings.Normalize();
            Profiles.Add(new ScanProfile
            {
                Name = appType.ToString(),
                IsBuiltIn = true,
                Settings = settings
            });
        }

        Save();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Profiles, SettingsStorage.JsonOptions);
            SettingsStorage.WriteAtomic(ProfilePath, json);
        }
        catch (Exception ex)
        {
            DiagLog.Write($"[Settings] Profile save failed: {ex.Message}");
        }
        Changed?.Invoke();
    }

    public ScanProfile? GetByName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        return Profiles.FirstOrDefault(p => p.Name == name)
               ?? Profiles.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public int IndexOf(string? name)
    {
        var profile = GetByName(name);
        return profile == null ? -1 : Profiles.IndexOf(profile);
    }

    public bool CanAddProfile => Profiles.Count < MaxProfiles;

    /// <summary>
    /// Checks a new or changed profile name (case-insensitive unique against names and
    /// display names). Returns a German error message, or null when valid.
    /// </summary>
    public string? ValidateName(string? name, ScanProfile? except = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Bitte geben Sie einen Profilnamen ein.";
        name = name.Trim();
        if (name.Length > MaxNameLength)
            return $"Der Profilname darf höchstens {MaxNameLength} Zeichen lang sein.";
        if (name == "-")
            return "Dieser Profilname ist nicht zulässig.";
        foreach (var p in Profiles)
        {
            if (ReferenceEquals(p, except)) continue;
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(p.DisplayName, name, StringComparison.OrdinalIgnoreCase))
                return $"Ein Profil mit dem Namen \"{name}\" existiert bereits.";
        }
        return null;
    }

    public void AddProfile(ScanProfile profile)
    {
        if (!CanAddProfile) return;
        Profiles.Add(profile);
        Save();
    }

    public void RemoveProfile(int index)
    {
        if (index >= 0 && index < Profiles.Count && !Profiles[index].IsBuiltIn)
        {
            string name = Profiles[index].Name;
            Profiles.RemoveAt(index);
            Save();
            ProfileRemoved?.Invoke(name);
        }
    }

    public void RenameProfile(int index, string newName)
    {
        if (index >= 0 && index < Profiles.Count && !Profiles[index].IsBuiltIn)
        {
            string oldName = Profiles[index].Name;
            Profiles[index].Name = newName.Trim();
            Save();
            ProfileRenamed?.Invoke(oldName, Profiles[index].Name);
        }
    }

    public void MoveUp(int index)
    {
        if (index > 0 && index < Profiles.Count)
        {
            (Profiles[index - 1], Profiles[index]) = (Profiles[index], Profiles[index - 1]);
            Save();
        }
    }

    public void MoveDown(int index)
    {
        if (index >= 0 && index < Profiles.Count - 1)
        {
            (Profiles[index + 1], Profiles[index]) = (Profiles[index], Profiles[index + 1]);
            Save();
        }
    }
}
