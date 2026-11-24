using System.Text.Json.Serialization;

namespace SpeedScanManager;

/// <summary>
/// A named snapshot of all settings across all 6 tabs.
/// The profile stores a complete ScanSettings copy, so every setting is covered.
/// Persisted as JSON by ProfileManager.
/// </summary>
internal class ScanProfile
{
    /// <summary>Internal, stable name (built-ins keep their technical name, e.g. "ScanToFolder").</summary>
    public string Name { get; set; } = "";
    public bool IsBuiltIn { get; set; }
    public ScanSettings Settings { get; set; } = new();

    /// <summary>Name shown in the UI (friendly German name for built-in profiles).</summary>
    [JsonIgnore]
    public string DisplayName => IsBuiltIn ? GetBuiltInDisplayName(Name) : Name;

    /// <summary>
    /// Creates a profile from a deep copy of <paramref name="settings"/>.
    /// </summary>
    public static ScanProfile FromCurrent(ScanSettings settings, string name, bool isBuiltIn = false)
    {
        return new ScanProfile
        {
            Name = name,
            IsBuiltIn = isBuiltIn,
            Settings = settings.Clone()
        };
    }

    /// <summary>
    /// Applies this profile's settings to a ScanSettings instance (deep copy).
    /// </summary>
    public void ApplyTo(ScanSettings settings)
    {
        settings.RestoreFrom(Settings);
    }

    /// <summary>
    /// Replaces the stored settings with a deep copy of <paramref name="settings"/>.
    /// </summary>
    public void UpdateFrom(ScanSettings settings)
    {
        Settings = settings.Clone();
    }

    public static string GetBuiltInDisplayName(string name) => name switch
    {
        nameof(ApplicationType.ScanToFolder) => L.T("In Ordner speichern"),
        nameof(ApplicationType.ScanToEmail) => L.T("Per E-Mail senden"),
        nameof(ApplicationType.ScanToPrint) => L.T("Drucken"),
        nameof(ApplicationType.ScanToWord) => L.T("In Word-Dokument umwandeln"),
        nameof(ApplicationType.ScanToExcel) => L.T("In Excel-Tabelle umwandeln"),
        nameof(ApplicationType.ScanToPowerPoint) => L.T("In PowerPoint-Präsentation umwandeln"),
        nameof(ApplicationType.ScanPictureFolder) => L.T("In Bilderordner speichern"),
        nameof(ApplicationType.EditWithPdf) => L.T("Mit PDF-Programm bearbeiten"),
        _ => name
    };
}
