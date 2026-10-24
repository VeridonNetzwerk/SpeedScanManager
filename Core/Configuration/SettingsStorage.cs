using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpeedScanManager;

/// <summary>
/// Shared persistence helpers for settings.json and profiles.json:
/// tolerant JSON options, atomic writes with .bak, and quarantine of unreadable files.
/// </summary>
internal static class SettingsStorage
{
    public static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SpeedScanManager");

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new TolerantEnumConverterFactory() }
    };

    /// <summary>
    /// Writes the file atomically: content goes to a .tmp file first, then replaces the
    /// target (previous version is kept as .bak). Returns false on failure (logged).
    /// </summary>
    public static bool WriteAtomic(string path, string content)
    {
        string tmp = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(tmp, content, Encoding.UTF8);
            if (File.Exists(path))
                File.Replace(tmp, path, path + ".bak", ignoreMetadataErrors: true);
            else
                File.Move(tmp, path);
            return true;
        }
        catch (Exception ex)
        {
            DiagLog.Write($"[Settings] Write failed for {Path.GetFileName(path)}: {ex.Message}");
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            return false;
        }
    }

    /// <summary>
    /// Tries to parse <paramref name="path"/> with <paramref name="parse"/>. If the file exists
    /// but cannot be parsed, it is renamed to .corrupt.&lt;timestamp&gt; (never silently overwritten)
    /// and the .bak copy is tried instead. Returns default when nothing usable was found.
    /// </summary>
    public static T? LoadWithFallback<T>(string path, Func<string, T?> parse) where T : class
    {
        foreach (var candidate in new[] { path, path + ".bak" })
        {
            if (!File.Exists(candidate))
                continue;

            try
            {
                var result = parse(File.ReadAllText(candidate));
                if (result != null)
                {
                    if (candidate != path)
                        DiagLog.Write($"[Settings] Restored {Path.GetFileName(path)} from backup");
                    return result;
                }
                throw new JsonException("Deserialized to null");
            }
            catch (Exception ex)
            {
                DiagLog.Write($"[Settings] Load failed for {Path.GetFileName(candidate)}: {ex.Message}");
                Quarantine(candidate);
            }
        }
        return null;
    }

    private static void Quarantine(string path)
    {
        try
        {
            string target = $"{path}.corrupt.{DateTime.Now:yyyyMMddHHmmss}";
            File.Move(path, target, overwrite: true);
            DiagLog.Write($"[Settings] Unreadable file moved to {Path.GetFileName(target)}");
        }
        catch (Exception ex)
        {
            DiagLog.Write($"[Settings] Could not quarantine {Path.GetFileName(path)}: {ex.Message}");
        }
    }
}

/// <summary>
/// Enum converter that writes names and reads names or numbers. Unknown values
/// (e.g. after an enum member was renamed) fall back to the enum's default instead of
/// failing the whole file.
/// </summary>
internal sealed class TolerantEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => (JsonConverter)Activator.CreateInstance(typeof(TolerantEnumConverter<>).MakeGenericType(typeToConvert))!;

    private sealed class TolerantEnumConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                string? s = reader.GetString();
                if (s != null && Enum.TryParse<T>(s, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
                    return parsed;
                DiagLog.Write($"[Settings] Unknown {typeof(T).Name} value \"{s}\" - using default");
                return default;
            }
            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int n))
            {
                var value = (T)Enum.ToObject(typeof(T), n);
                if (Enum.IsDefined(value))
                    return value;
            }
            DiagLog.Write($"[Settings] Invalid {typeof(T).Name} token - using default");
            reader.Skip();
            return default;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString());
    }
}

/// <summary>
/// Protects secrets (PDF password) with DPAPI for the current Windows user.
/// Stored values are Base64 with a "dpapi:" prefix.
/// </summary>
internal static class SecretProtector
{
    private const string Prefix = "dpapi:";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("SpeedScanManager.PdfPassword");

    public static string Protect(string? plain)
    {
        if (string.IsNullOrEmpty(plain)) return "";
        try
        {
            var data = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser);
            return Prefix + Convert.ToBase64String(data);
        }
        catch (Exception ex)
        {
            DiagLog.Write($"[Settings] DPAPI protect failed: {ex.Message}");
            return "";
        }
    }

    public static string Unprotect(string? stored)
    {
        if (string.IsNullOrEmpty(stored) || !stored.StartsWith(Prefix, StringComparison.Ordinal))
            return "";
        try
        {
            var data = ProtectedData.Unprotect(Convert.FromBase64String(stored[Prefix.Length..]), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(data);
        }
        catch (Exception ex)
        {
            // e.g. file copied from another user/machine
            DiagLog.Write($"[Settings] DPAPI unprotect failed: {ex.Message}");
            return "";
        }
    }
}
