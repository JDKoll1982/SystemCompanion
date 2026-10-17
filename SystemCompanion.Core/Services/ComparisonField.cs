using System.Globalization;
using SystemCompanion.Models;

namespace SystemCompanion.Services;

/// <summary>
/// Which way a compared value is expected to move.
/// </summary>
public enum ComparisonFieldDirection
{
    HigherIsBetter,
    LowerIsBetter,
    Neutral
}

/// <summary>
/// One row of the fixed table that decides what counts as a change between two readings.
/// </summary>
public sealed record ComparisonField(
    string Key,
    string Area,
    string Label,
    ComparisonFieldDirection Direction,
    Func<SystemSnapshot, IReadOnlyList<(string Label, string Value)>> Read)
{
    /// <summary>
    /// Where a rendered value sits in its own order, or null when it cannot be ordered.
    /// Directions alone say which way is better; this says which of two values is further along.
    /// </summary>
    public Func<string, double?>? Rank { get; init; }

    /// <summary>
    /// The text shown for a value that could not be read.
    /// </summary>
    public static string FormatUnknown => "Unknown";

    /// <summary>
    /// The ordered table of compared values.
    /// </summary>
    public static IReadOnlyList<ComparisonField> All { get; } = new ComparisonField[]
    {
        new(
            "Identity.Board",
            "Identity",
            "Motherboard",
            ComparisonFieldDirection.Neutral,
            snapshot => Single("Motherboard", FormatText(snapshot?.Identity?.Board))),

        new(
            "Identity.BiosVersion",
            "Identity",
            "Firmware version",
            ComparisonFieldDirection.Neutral,
            snapshot => Single("Firmware version", FormatText(snapshot?.Identity?.BiosVersion))),

        new(
            "Cpu.Name",
            "Processor",
            "Processor",
            ComparisonFieldDirection.Neutral,
            snapshot => Single("Processor", FormatText(snapshot?.Cpu?.Name))),

        new(
            "Memory.ConfiguredMt",
            "Memory",
            "Memory speed",
            ComparisonFieldDirection.HigherIsBetter,
            snapshot => Single("Memory speed", FormatInteger(snapshot?.Memory?.ConfiguredMt, "MT/s")))
        { Rank = NumberRank },

        new(
            "Memory.ModuleCount",
            "Memory",
            "Memory sticks",
            ComparisonFieldDirection.HigherIsBetter,
            snapshot => Single("Memory sticks", FormatInteger(snapshot?.Memory?.ModuleCount)))
        { Rank = NumberRank },

        new(
            "Graphics.Adapters[0].Name",
            "Graphics",
            "Graphics card",
            ComparisonFieldDirection.Neutral,
            snapshot => Single("Graphics card", FormatText(PhysicalAdapter(snapshot)?.Name))),

        new(
            "Graphics.Adapters[0].DriverDate",
            "Graphics",
            "Driver date",
            ComparisonFieldDirection.HigherIsBetter,
            snapshot => Single("Driver date", FormatText(PhysicalAdapter(snapshot)?.DriverDate)))
        { Rank = DateRank },

        new(
            "Graphics.Adapters[0].RefreshHz",
            "Graphics",
            "Screen refresh rate",
            ComparisonFieldDirection.HigherIsBetter,
            snapshot => Single("Screen refresh rate", FormatInteger(PhysicalAdapter(snapshot)?.RefreshHz, "Hz")))
        { Rank = NumberRank },

        new(
            "Storage.Volumes[].FreeGb",
            "Storage",
            "Free space",
            ComparisonFieldDirection.HigherIsBetter,
            FreeSpaceRows)
        { Rank = NumberRank },

        new(
            "Windows.Build",
            "Windows",
            "Windows build",
            ComparisonFieldDirection.HigherIsBetter,
            snapshot => Single("Windows build", FormatText(snapshot?.Windows?.Build)))
        { Rank = NumberRank },

        new(
            "Windows.LongPaths",
            "Windows",
            "Long path support",
            ComparisonFieldDirection.HigherIsBetter,
            snapshot => Single(
                "Long path support",
                FormatBoolean(snapshot?.Windows is { } windows ? windows.LongPaths : null)))
        { Rank = BooleanRank },

        new(
            "Performance.PowerPlan",
            "Performance",
            "Power plan",
            ComparisonFieldDirection.HigherIsBetter,
            snapshot => Single("Power plan", FormatText(snapshot?.Performance?.PowerPlan)))
        { Rank = PowerPlanRank },

        new(
            "Performance.GameMode",
            "Performance",
            "Game Mode",
            ComparisonFieldDirection.HigherIsBetter,
            snapshot => Single("Game Mode", FormatBoolean(snapshot?.Performance?.GameMode)))
        { Rank = BooleanRank },

        new(
            "Performance.GameDvrEnabled",
            "Performance",
            "Game recording",
            ComparisonFieldDirection.LowerIsBetter,
            snapshot => Single("Game recording", FormatBoolean(snapshot?.Performance?.GameDvrEnabled)))
        { Rank = BooleanRank },

        new(
            "Performance.HardwareScheduling",
            "Performance",
            "Hardware scheduling",
            ComparisonFieldDirection.HigherIsBetter,
            snapshot => Single("Hardware scheduling", FormatBoolean(snapshot?.Performance?.HardwareScheduling)))
        { Rank = BooleanRank },

        new(
            "Performance.MemoryIntegrity",
            "Performance",
            "Memory integrity",
            ComparisonFieldDirection.Neutral,
            snapshot => Single("Memory integrity", FormatBoolean(snapshot?.Performance?.MemoryIntegrity)))
    };

    private static IReadOnlyList<(string Label, string Value)> Single(string label, string value) =>
        new[] { (label, value) };

    private static IReadOnlyList<(string Label, string Value)> FreeSpaceRows(SystemSnapshot snapshot)
    {
        var volumes = snapshot?.Storage?.Volumes;
        if (volumes is null || volumes.Count == 0)
        {
            return Single("Free space", FormatUnknown);
        }

        var rows = new List<(string Label, string Value)>(volumes.Count);
        foreach (var volume in volumes)
        {
            if (volume is null)
            {
                continue;
            }

            var letter = FormatText(volume.Letter);
            rows.Add(($"Free space on {letter}", FormatFreeGb(volume.FreeGb)));
        }

        return rows.Count == 0 ? Single("Free space", FormatUnknown) : rows;
    }

    private static GraphicsInfo? PhysicalAdapter(SystemSnapshot snapshot)
    {
        var adapters = snapshot?.Graphics?.Adapters;
        if (adapters is null)
        {
            return null;
        }

        foreach (var adapter in adapters)
        {
            if (adapter is not null && !adapter.IsVirtual)
            {
                return adapter;
            }
        }

        return null;
    }

    private static string FormatText(string? value) =>
        string.IsNullOrEmpty(value) ? FormatUnknown : value;

    private static string FormatInteger(int? value, string unit = "") =>
        value is null
            ? FormatUnknown
            : value.Value.ToString(CultureInfo.InvariantCulture) + (unit.Length == 0 ? "" : " " + unit);

    private static string FormatBoolean(bool? value) =>
        value switch
        {
            true => "On",
            false => "Off",
            _ => FormatUnknown
        };

    private static string FormatFreeGb(double? value) =>
        value is null
            ? FormatUnknown
            : value.Value.ToString("0.0", CultureInfo.InvariantCulture) + " GB";

    private static double? NumberRank(string value)
    {
        var text = value.Trim();
        var end = 0;
        while (end < text.Length
               && (char.IsDigit(text[end]) || text[end] is '.' or '-' or '+' or ','))
        {
            end++;
        }

        if (end == 0)
        {
            return null;
        }

        var number = text[..end].Replace(",", "", StringComparison.Ordinal);
        return double.TryParse(number, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static double? DateRank(string value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.Ticks
            : null;

    private static double? BooleanRank(string value) => value switch
    {
        "On" => 1,
        "Off" => 0,
        _ => null
    };

    private static double? PowerPlanRank(string value)
    {
        if (value.Contains("Ultimate", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (value.Contains("High performance", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (value.Contains("Balanced", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (value.Contains("Power saver", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return null;
    }
}
