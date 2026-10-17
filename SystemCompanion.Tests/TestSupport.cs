using System.Text.Json;
using SystemCompanion.Models;
using SystemCompanion.Services;

namespace SystemCompanion.Tests;

/// <summary>Loads a written reading and builds the same inputs the app would.</summary>
public static class TestSupport
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static SystemSnapshot Reading(string fixtureName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName);
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<SystemSnapshot>(stream, Options)
               ?? throw new InvalidOperationException($"{fixtureName} did not parse.");
    }

    /// <summary>The BIOS name-to-label values the app loads from screenshot_check.json.</summary>
    public static IReadOnlyDictionary<string, string> BiosValues() =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Re-Size BAR Support"] = "Enabled",
            ["Above 4G memory/Crypto Currency mining"] = "Enabled"
        };

    /// <summary>The saved BIOS summary that goes with the written readings.</summary>
    public static BiosProfile Profile(string fastMemoryProfile = "On") => new()
    {
        File = new BiosFileInfo { Valid = true, BiosVersion = FirmwareReference.SupportedTag },
        Headline = new List<BiosFact>
        {
            new()
            {
                Label = "Fast memory profile",
                Value = fastMemoryProfile,
                Plain = "The memory runs at the speed the maker tested.",
                State = "good"
            },
            new()
            {
                Label = "Memory speed",
                Value = "4800 MT/s",
                Plain = "The current memory clock.",
                State = "neutral"
            }
        },
        Sections = new List<BiosSection>
        {
            new()
            {
                Title = "Overclocking",
                Items = new List<BiosEntry>
                {
                    new() { Name = "CPU cooler profile", Value = "MSI MAG CoreLiquid 360R" }
                }
            }
        }
    };

    public static IReadOnlyList<Suggestion> Advice(this SystemSnapshot snapshot, string fastMemoryProfile = "On")
        => new RecommendationEngine(snapshot, Profile(fastMemoryProfile), BiosValues()).Build();

    public static Suggestion Find(this IReadOnlyList<Suggestion> suggestions, string id)
        => suggestions.Single(suggestion => suggestion.Id == id);
}
