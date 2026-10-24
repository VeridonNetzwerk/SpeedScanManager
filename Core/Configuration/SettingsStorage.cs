using System.Reflection;
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
    /// Options for reading settings files: like <see cref="JsonOptions"/>, but numbers may be
    /// quoted and settings objects are read property by property, so one invalid value only
    /// resets that property to its default instead of making the whole file unreadable.
    /// Never use these options for writing.
    /// </summary>
    public static readonly JsonSerializerOptions ReadOptions = CreateReadOptions();

    private static JsonSerializerOptions CreateReadOptions()
    {
        var options = new JsonSerializerOptions(JsonOptions)
        {
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };
        options.Converters.Add(new TolerantObjectConverterFactory());
        return options;
    }

    /// <summary>Name of the legacy plain-text password property (profiles of version 0.1.x).</summary>
    private const string PlaintextPasswordProperty = "PdfPassword";

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// True if <paramref name="json"/> contains a non-empty plain-text "PdfPassword" value
    /// anywhere (legacy files). Invalid JSON counts as false.
    /// </summary>
    public static bool ContainsPlaintextSecret(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json, DocumentOptions);
            return ContainsPlaintextSecret(doc.RootElement);
        }
        catch
        {
            return false;
        }
    }

    private static bool ContainsPlaintextSecret(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if (string.Equals(prop.Name, PlaintextPasswordProperty, StringComparison.OrdinalIgnoreCase)
                        && prop.Value.ValueKind == JsonValueKind.String
                        && !string.IsNullOrEmpty(prop.Value.GetString()))
                        return true;
                    if (ContainsPlaintextSecret(prop.Value))
                        return true;
                }
                return false;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    if (ContainsPlaintextSecret(item))
                        return true;
                return false;
            default:
                return false;
        }
    }

    /// <summary>True if the file exists and contains a plain-text password (see <see cref="ContainsPlaintextSecret(string)"/>).</summary>
    public static bool FileContainsPlaintextSecret(string path)
    {
        try
        {
            return File.Exists(path) && ContainsPlaintextSecret(File.ReadAllText(path));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Replaces the .bak copy of <paramref name="path"/> with the current file (or deletes it
    /// when the current file is missing or still contains a plain-text password), so an old
    /// version with a plain-text password doesn't stay on disk after the file was rewritten.
    /// </summary>
    public static void ScrubBackup(string path)
    {
        string bak = path + ".bak";
        try
        {
            if (!File.Exists(bak)) return;
            if (File.Exists(path) && !FileContainsPlaintextSecret(path))
                File.Copy(path, bak, overwrite: true);
            else
                File.Delete(bak);
            DiagLog.Write($"[Settings] Removed the plain-text password from {Path.GetFileName(bak)}");
        }
        catch (Exception ex)
        {
            DiagLog.Write($"[Settings] Could not scrub {Path.GetFileName(bak)}: {ex.Message}");
        }
    }

    /// <summary>True if the top-level JSON object has a property <paramref name="name"/> (case-insensitive).</summary>
    public static bool HasTopLevelProperty(string json, string name)
    {
        try
        {
            using var doc = JsonDocument.Parse(json, DocumentOptions);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            foreach (var prop in doc.RootElement.EnumerateObject())
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
        catch
        {
            return false;
        }
    }

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
/// Enum converter that writes names and reads names or numbers (case-insensitive).
/// Unknown values (e.g. after an enum member was renamed) throw a <see cref="JsonException"/>;
/// <see cref="TolerantObjectConverterFactory"/> catches it per property, so the property
/// keeps its initializer default (e.g. ColorMode.Color) instead of falling back to member 0.
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
                throw new JsonException($"Unknown {typeof(T).Name} value \"{s}\"");
            }
            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int n))
            {
                var value = (T)Enum.ToObject(typeof(T), n);
                if (Enum.IsDefined(value))
                    return value;
            }
            throw new JsonException($"Invalid {typeof(T).Name} value ({reader.TokenType})");
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString());
    }
}

/// <summary>
/// Reads settings objects (<see cref="AppSettings"/>, <see cref="ScanSettings"/>,
/// <see cref="CustomApplication"/>) property by property: a property whose value cannot be
/// read (wrong type, unknown enum name, 3.5 for an int, ...) is logged and keeps the default
/// of a freshly constructed instance; null keeps the default as well. Lists are read item by
/// item (invalid items are dropped). Writing uses the normal serializer
/// (<see cref="SettingsStorage.JsonOptions"/>).
/// </summary>
internal sealed class TolerantObjectConverterFactory : JsonConverterFactory
{
    private static readonly HashSet<Type> TolerantTypes = new()
    {
        typeof(AppSettings),
        typeof(ScanSettings),
        typeof(CustomApplication)
    };

    public override bool CanConvert(Type typeToConvert) => TolerantTypes.Contains(typeToConvert);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => (JsonConverter)Activator.CreateInstance(typeof(TolerantObjectConverter<>).MakeGenericType(typeToConvert))!;

    private static bool IsReadError(Exception ex) => ex is JsonException or InvalidOperationException
        or NotSupportedException or FormatException or OverflowException or ArgumentException;

    private sealed class TolerantObjectConverter<T> : JsonConverter<T> where T : class, new()
    {
        private static readonly Dictionary<string, PropertyInfo> Properties = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0
                        && p.GetCustomAttribute<JsonIgnoreAttribute>() == null)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new JsonException($"{typeof(T).Name}: JSON object expected, found {root.ValueKind}");

            var target = new T();
            foreach (var jsonProp in root.EnumerateObject())
            {
                if (!Properties.TryGetValue(jsonProp.Name, out var prop))
                    continue;
                try
                {
                    Assign(target, prop, jsonProp.Value, options);
                }
                catch (Exception ex) when (IsReadError(ex))
                {
                    DiagLog.Write($"[Settings] Ignoring invalid value of {typeof(T).Name}.{prop.Name}: {ex.Message}");
                }
            }
            return target;
        }

        private static void Assign(T target, PropertyInfo prop, JsonElement value, JsonSerializerOptions options)
        {
            bool writable = prop.SetMethod?.IsPublic == true;
            var type = prop.PropertyType;

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                if (value.ValueKind == JsonValueKind.Null)
                    return; // keep the (empty) default list
                if (value.ValueKind != JsonValueKind.Array)
                    throw new JsonException($"array expected, found {value.ValueKind}");

                var itemType = type.GetGenericArguments()[0];
                var list = writable
                    ? (System.Collections.IList)Activator.CreateInstance(type)!
                    : prop.GetValue(target) as System.Collections.IList;
                if (list == null) return;
                if (!writable) list.Clear();

                foreach (var item in value.EnumerateArray())
                {
                    try
                    {
                        var parsed = item.Deserialize(itemType, options);
                        if (parsed != null) list.Add(parsed);
                    }
                    catch (Exception ex) when (IsReadError(ex))
                    {
                        DiagLog.Write($"[Settings] Ignoring invalid entry of {typeof(T).Name}.{prop.Name}: {ex.Message}");
                    }
                }
                if (writable) prop.SetValue(target, list);
                return;
            }

            if (!writable || value.ValueKind == JsonValueKind.Null)
                return;
            var result = value.Deserialize(type, options);
            if (result != null)
                prop.SetValue(target, result);
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            => JsonSerializer.Serialize(writer, value, SettingsStorage.JsonOptions);
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
