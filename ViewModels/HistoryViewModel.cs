using SystemCompanion.Services;

namespace SystemCompanion.ViewModels;

/// <summary>One line of the reading timeline.</summary>
public sealed class TimelineItem
{
    public required string When { get; init; }
    public required string Summary { get; init; }
    public bool IsNewest { get; init; }
}

/// <summary>
/// Pairs two kept readings and turns every compared value into a sentence.
/// The two newest entries are paired by default; either side can be swapped.
/// </summary>
public sealed class HistoryViewModel : ObservableBase
{
    private readonly HistoryService _service = new();
    private readonly IReadOnlyList<HistoryEntry> _entries;
    private HistoryEntry? _left;
    private HistoryEntry? _right;
    private ScanComparison _comparison;

    public HistoryViewModel(
        IReadOnlyList<HistoryEntry> entries,
        IReadOnlyList<HistoryEntry>? skipped = null,
        string? leftPath = null,
        string? rightPath = null)
    {
        _entries = entries;

        Timeline = entries
            .Select((entry, index) => new TimelineItem
            {
                When = entry.DisplayName,
                Summary = Summarize(entry),
                IsNewest = index == entries.Count - 1
            })
            .Reverse()
            .ToList();

        var defaultLeft = entries.Count >= 2 ? entries[^2] : entries.FirstOrDefault();
        var defaultRight = entries.Count >= 1 ? entries[^1] : null;

        _left = Find(leftPath) ?? defaultLeft;
        _right = Find(rightPath) ?? defaultRight;
        _comparison = Build();

        SkippedNote = BuildSkippedNote(skipped);
    }

    public IReadOnlyList<HistoryEntry> Entries => _entries;

    public IReadOnlyList<TimelineItem> Timeline { get; }

    public bool HasTwoReadings => _comparison.Left is not null && _comparison.Right is not null;

    public IReadOnlyList<ComparisonRow> Changed => _comparison.Changed;

    public IReadOnlyList<ComparisonRow> Unchanged => _comparison.Unchanged;

    public bool HasChanged => _comparison.Changed.Count > 0;

    public bool EverythingEqual => _comparison.EverythingEqual;

    public string SkippedNote { get; }

    public bool HasSkippedNote => !string.IsNullOrWhiteSpace(SkippedNote);

    public HistoryEntry? Left
    {
        get => _left;
        set
        {
            if (!Set(ref _left, value) || value is null)
            {
                return;
            }

            _comparison = Build();
            RaiseAll();
        }
    }

    public HistoryEntry? Right
    {
        get => _right;
        set
        {
            if (!Set(ref _right, value) || value is null)
            {
                return;
            }

            _comparison = Build();
            RaiseAll();
        }
    }

    /// <summary>The one line above the list: what changed, or why nothing can be compared yet.</summary>
    public string SummaryLine
    {
        get
        {
            if (!HasTwoReadings)
            {
                return "A second reading is needed before two can be compared. Take another reading and come back.";
            }

            if (EverythingEqual)
            {
                return "Nothing changed between these two readings.";
            }

            var changed = _comparison.Changed.Count;
            var improved = _comparison.ImprovedCount;
            var declined = _comparison.DeclinedCount;
            var values = changed == 1 ? "1 value changed" : $"{changed} values changed";

            if (improved == 0 && declined == 0)
            {
                return $"{values}.";
            }

            return $"{values}: {improved} better, {declined} worse.";
        }
    }

    public string CountLine => !HasTwoReadings
        ? $"{_entries.Count} reading{(_entries.Count == 1 ? "" : "s")} kept"
        : $"{_comparison.Unchanged.Count} values stayed the same";

    public string LeftLabel => _left is null ? "No reading" : _left.DisplayName;

    public string RightLabel => _right is null ? "No reading" : _right.DisplayName;

    public void SelectByPath(string? leftPath, string? rightPath)
    {
        var left = Find(leftPath) ?? _left;
        var right = Find(rightPath) ?? _right;
        if (ReferenceEquals(left, _left) && ReferenceEquals(right, _right))
        {
            return;
        }

        _left = left;
        _right = right;
        _comparison = Build();
        Raise(nameof(Left));
        Raise(nameof(Right));
        RaiseAll();
    }

    private ScanComparison Build()
        => _left is null || _right is null
            ? new ScanComparison { Left = _left, Right = _right, NeedsSecondReading = true }
            : _service.Compare(_left, _right);

    private HistoryEntry? Find(string? path)
        => string.IsNullOrWhiteSpace(path)
            ? null
            : _entries.FirstOrDefault(e => string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase));

    private void RaiseAll()
    {
        Raise(nameof(HasTwoReadings));
        Raise(nameof(Changed));
        Raise(nameof(Unchanged));
        Raise(nameof(HasChanged));
        Raise(nameof(EverythingEqual));
        Raise(nameof(SummaryLine));
        Raise(nameof(CountLine));
        Raise(nameof(LeftLabel));
        Raise(nameof(RightLabel));
    }

    private static string Summarize(HistoryEntry entry)
    {
        var snapshot = entry.Snapshot;
        if (snapshot is null)
        {
            return entry.SkipNote;
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(snapshot.ComputerName))
        {
            parts.Add(snapshot.ComputerName);
        }

        if (snapshot.Memory.ConfiguredMt > 0)
        {
            parts.Add($"{snapshot.Memory.ConfiguredMt} MT/s");
        }

        if (!string.IsNullOrWhiteSpace(snapshot.Performance.PowerPlan))
        {
            parts.Add(snapshot.Performance.PowerPlan);
        }

        return parts.Count == 0 ? entry.FileName : string.Join(" · ", parts);
    }

    private static string BuildSkippedNote(IReadOnlyList<HistoryEntry>? skipped)
    {
        if (skipped is null || skipped.Count == 0)
        {
            return "";
        }

        return string.Join(" ", skipped.Select(entry => entry.SkipNote));
    }
}
