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
        if (Directory.Exists(beside))
        {
            return beside;
        }

        var probe = new DirectoryInfo(AppContext.BaseDirectory);
        while (probe is not null)
        {
            var candidate = Path.Combine(probe.FullName, "Data");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            probe = probe.Parent;
        }

        return beside;
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
