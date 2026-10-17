using System.Globalization;
using System.Text.Json;
using SystemCompanion.Models;

namespace SystemCompanion.Services;

/// <summary>One dated copy of a reading, or the note explaining why it could not be read.</summary>
public sealed class HistoryEntry
{
    public string Path { get; init; } = "";

    public string FileName => Path.Length == 0 ? "" : System.IO.Path.GetFileName(Path);

    /// <summary>Parsed from <c>generatedLocal</c> inside the file. Never taken from the file name.</summary>
    public DateTimeOffset TakenAt { get; init; }

    public SystemSnapshot? Snapshot { get; init; }

    public bool IsReadable => Snapshot is not null;

    /// <summary>Empty when readable, otherwise a plain sentence naming the skipped file.</summary>
    public string SkipNote { get; init; } = "";

    public string DisplayName => IsReadable
        ? TakenAt.ToString("dddd d MMMM yyyy, HH:mm", CultureInfo.InvariantCulture)
        : FileName;
}

/// <summary>Whether a compared value moved in a direction anyone would want.</summary>
public enum ComparisonVerdict
{
    Improved,
    Declined,
    Changed,
    Same,
    CannotTell
}

/// <summary>One compared value inside a comparison.</summary>
public sealed class ComparisonRow
{
    public required string Key { get; init; }
    public required string Area { get; init; }
    public required string Label { get; init; }
    public required string Before { get; init; }
    public required string After { get; init; }
    public ComparisonFieldDirection Direction { get; init; }
    public ComparisonVerdict Verdict { get; init; }

    /// <summary>One plain sentence naming the value, both readings and the verdict.</summary>
    public required string Sentence { get; init; }
}

/// <summary>The result of pairing two history entries.</summary>
public sealed class ScanComparison
{
    public HistoryEntry? Left { get; init; }
    public HistoryEntry? Right { get; init; }
    public IReadOnlyList<ComparisonRow> Changed { get; init; } = Array.Empty<ComparisonRow>();
    public IReadOnlyList<ComparisonRow> Unchanged { get; init; } = Array.Empty<ComparisonRow>();
    public IReadOnlyList<HistoryEntry> Skipped { get; init; } = Array.Empty<HistoryEntry>();
    public bool NeedsSecondReading { get; init; }
    public bool EverythingEqual { get; init; }

    public int ImprovedCount => Changed.Count(r => r.Verdict == ComparisonVerdict.Improved);
    public int DeclinedCount => Changed.Count(r => r.Verdict == ComparisonVerdict.Declined);
}

/// <summary>
/// Reads the kept readings, orders them by the time recorded inside each file, and
/// compares two of them through <see cref="ComparisonField.All"/>.
/// </summary>
public sealed class HistoryService
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>The pinned time inside a snapshot, for example <c>2026-10-17 13:40</c>.</summary>
    public static bool TryParseTakenAt(string? generatedLocal, out DateTimeOffset takenAt)
    {
        takenAt = default;
        if (string.IsNullOrWhiteSpace(generatedLocal))
        {
            return false;
        }

        if (DateTime.TryParseExact(
                generatedLocal.Trim(),
                "yyyy-MM-dd HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var exact))
        {
            takenAt = new DateTimeOffset(exact, TimeZoneInfo.Local.GetUtcOffset(exact));
            return true;
        }

        if (DateTime.TryParse(
                generatedLocal,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var loose))
        {
            takenAt = new DateTimeOffset(loose, TimeZoneInfo.Local.GetUtcOffset(loose));
            return true;
        }

        return false;
    }

    /// <summary>Reads one file. An unreadable file comes back as an entry carrying a skip note.</summary>
    public HistoryEntry ReadEntry(string path)
    {
        var fileName = System.IO.Path.GetFileName(path);

        if (!File.Exists(path))
        {
            return Unreadable(path, $"{fileName} could not be found, so it was left out.");
        }

        SystemSnapshot? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<SystemSnapshot>(File.ReadAllText(path), ReadOptions);
        }
        catch (Exception)
        {
            return Unreadable(path, $"{fileName} could not be read, so it was left out.");
        }

        if (snapshot is null)
        {
            return Unreadable(path, $"{fileName} is empty, so it was left out.");
        }

        if (!TryParseTakenAt(snapshot.GeneratedLocal, out var takenAt))
        {
            return Unreadable(path, $"{fileName} has no readable date, so it was left out.");
        }

        return new HistoryEntry { Path = path, TakenAt = takenAt, Snapshot = snapshot };
    }

    /// <summary>Every readable entry under <paramref name="historyDirectory"/>, oldest first.</summary>
    public IReadOnlyList<HistoryEntry> ReadEntries(string historyDirectory)
        => Order(ReadAllEntries(historyDirectory));

    /// <summary>Every entry the folder holds, readable or not, so a skipped one can be named.</summary>
    public IReadOnlyList<HistoryEntry> ReadAllEntries(string historyDirectory)
    {
        if (string.IsNullOrWhiteSpace(historyDirectory) || !Directory.Exists(historyDirectory))
        {
            return Array.Empty<HistoryEntry>();
        }

        var entries = new List<HistoryEntry>();
        foreach (var file in Directory.EnumerateFiles(historyDirectory, "scan-*.json"))
        {
            entries.Add(ReadEntry(file));
        }

        return entries;
    }

    /// <summary>Sorts by the time recorded in the file. A file name never decides order.</summary>
    public IReadOnlyList<HistoryEntry> Order(IEnumerable<HistoryEntry> entries)
        => entries.Where(e => e.IsReadable).OrderBy(e => e.TakenAt).ToList();

    /// <summary>
    /// Writes a copy of <paramref name="snapshot"/> and returns its path. The first
    /// reading in a minute takes the pinned name; a second takes a counter suffix.
    /// </summary>
    public string WriteEntry(SystemSnapshot snapshot, string historyDirectory)
    {
        Directory.CreateDirectory(historyDirectory);

        var stamp = TryParseTakenAt(snapshot.GeneratedLocal, out var takenAt)
            ? takenAt.LocalDateTime
            : DateTime.Now;

        var baseName = "scan-" + stamp.ToString("yyyy-MM-dd-HHmm", CultureInfo.InvariantCulture);
        var path = System.IO.Path.Combine(historyDirectory, baseName + ".json");

        var counter = 2;
        while (File.Exists(path))
        {
            path = System.IO.Path.Combine(historyDirectory, baseName + "-" + counter + ".json");
            counter++;
        }

        File.WriteAllText(path, JsonSerializer.Serialize(snapshot, WriteOptions));
        return path;
    }

    /// <summary>Pairs two entries and classifies every compared value.</summary>
    public ScanComparison Compare(string leftPath, string rightPath)
    {
        var left = ReadEntry(leftPath);
        var right = ReadEntry(rightPath);

        var skipped = new List<HistoryEntry>();
        if (!left.IsReadable)
        {
            skipped.Add(left);
        }

        if (!right.IsReadable)
        {
            skipped.Add(right);
        }

        if (skipped.Count > 0)
        {
            return new ScanComparison
            {
                Left = left.IsReadable ? left : null,
                Right = right.IsReadable ? right : null,
                Skipped = skipped,
                NeedsSecondReading = true
            };
        }

        return Compare(left, right);
    }

    /// <summary>Pairs two readable entries and classifies every compared value.</summary>
    public ScanComparison Compare(HistoryEntry left, HistoryEntry right)
    {
        var changed = new List<ComparisonRow>();
        var unchanged = new List<ComparisonRow>();

        foreach (var field in ComparisonField.All)
        {
            var before = field.Read(left.Snapshot!);
            var after = field.Read(right.Snapshot!);

            foreach (var (label, value) in before)
            {
                var match = after.FirstOrDefault(r => r.Label == label);
                if (match.Label is null)
                {
                    continue;
                }

                var row = BuildRow(field, label, value, match.Value);
                if (row.Verdict == ComparisonVerdict.Same)
                {
                    unchanged.Add(row);
                }
                else
                {
                    changed.Add(row);
                }
            }
        }

        return new ScanComparison
        {
            Left = left,
            Right = right,
            Changed = changed,
            Unchanged = unchanged,
            NeedsSecondReading = false,
            EverythingEqual = changed.Count == 0
        };
    }

    private static ComparisonRow BuildRow(
        ComparisonField field,
        string label,
        string before,
        string after)
    {
        var verdict = Verdict(field, before, after);

        return new ComparisonRow
        {
            Key = RowKey(field, label),
            Area = field.Area,
            Label = label,
            Before = before,
            After = after,
            Direction = field.Direction,
            Verdict = verdict,
            Sentence = Sentence(label, before, after, verdict)
        };
    }

    private static ComparisonVerdict Verdict(ComparisonField field, string before, string after)
    {
        // Equal first: two readings that both fail to read a value have not changed.
        if (string.Equals(before, after, StringComparison.Ordinal))
        {
            return ComparisonVerdict.Same;
        }

        if (before == ComparisonField.FormatUnknown || after == ComparisonField.FormatUnknown)
        {
            return ComparisonVerdict.CannotTell;
        }

        if (field.Direction == ComparisonFieldDirection.Neutral)
        {
            return ComparisonVerdict.Changed;
        }

        var beforeRank = field.Rank?.Invoke(before);
        var afterRank = field.Rank?.Invoke(after);
        if (beforeRank is null || afterRank is null)
        {
            return ComparisonVerdict.Changed;
        }

        var wentUp = afterRank.Value > beforeRank.Value;
        var better = field.Direction == ComparisonFieldDirection.HigherIsBetter ? wentUp : !wentUp;
        return better ? ComparisonVerdict.Improved : ComparisonVerdict.Declined;
    }

    private static string Sentence(string label, string before, string after, ComparisonVerdict verdict)
        => verdict switch
        {
            ComparisonVerdict.Improved => $"{label} went from {before} to {after}, which is a real improvement.",
            ComparisonVerdict.Declined => $"{label} went from {before} to {after}, which is a step backwards.",
            ComparisonVerdict.Changed => $"{label} changed from {before} to {after}.",
            ComparisonVerdict.CannotTell => $"{label} could not be compared because one reading shows {ComparisonField.FormatUnknown}.",
            _ => $"{label} is still {after}."
        };

    private static string RowKey(ComparisonField field, string label)
    {
        if (!field.Key.Contains("[]", StringComparison.Ordinal))
        {
            return field.Key;
        }

        var letter = label.Replace("Free space on ", "", StringComparison.Ordinal).Trim();
        return field.Key.Replace("[]", "[" + letter + "]", StringComparison.Ordinal);
    }

    private static HistoryEntry Unreadable(string path, string note)
        => new() { Path = path, SkipNote = note };
}
