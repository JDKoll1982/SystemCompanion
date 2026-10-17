using System.Text.Json;

namespace SystemCompanion.Services;

/// <summary>
/// Finds the bundled Data folder and reads JSON from it.
/// When the app runs from bin\Debug the content files sit next to the exe;
/// during early development they may still be in the project folder, so both
/// locations are tried.
/// </summary>
public static class AppData
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static string Root { get; } = ResolveRoot();

    private static string ResolveRoot()
    {
        var beside = Path.Combine(AppContext.BaseDirectory, "Data");
        var probe = new DirectoryInfo(AppContext.BaseDirectory);
        string? nearest = null;

        while (probe is not null)
        {
            var candidate = Path.Combine(probe.FullName, "Data");
            if (nearest is null && Directory.Exists(candidate))
            {
                nearest = candidate;
            }

            // The repository copy is the one the scan script writes, so prefer it over
            // the copy made beside the exe at build time.
            if (Directory.Exists(candidate)
                && File.Exists(Path.Combine(probe.FullName, "tools", "discover-system.ps1")))
            {
                return candidate;
            }

            probe = probe.Parent;
        }

        return nearest ?? beside;
    }

    /// <summary>
    /// Reads the screenshot-checked BIOS labels once into a name-to-label map.
    /// The map is case-insensitive; a missing or malformed file yields an empty map.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ReadBiosValues()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var document = ReadJson<JsonElement>("bios", "screenshot_check.json");
            if (document.ValueKind != JsonValueKind.Object
                || !document.TryGetProperty("rows", out var rows)
                || rows.ValueKind != JsonValueKind.Array)
            {
                return values;
            }

            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object
                    || !row.TryGetProperty("name", out var name)
                    || name.ValueKind != JsonValueKind.String
                    || !row.TryGetProperty("label", out var label)
                    || label.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var key = name.GetString();
                var labelText = label.GetString();
                if (key is not null && !string.IsNullOrEmpty(labelText))
                {
                    values[key] = labelText;
                }
            }
        }
        catch (Exception)
        {
            values.Clear();
        }

        return values;
    }

    public static T? ReadJson<T>(params string[] relativeParts)
    {
        var path = Path.Combine(new[] { Root }.Concat(relativeParts).ToArray());
        return File.Exists(path) ? Deserialize<T>(path) : default;
    }

    public static T? Deserialize<T>(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options);
        }
        catch (Exception)
        {
            return default;
        }
    }

    public static string PathFor(params string[] relativeParts)
        => Path.Combine(new[] { Root }.Concat(relativeParts).ToArray());
}
