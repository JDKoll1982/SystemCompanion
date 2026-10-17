using System.Text.Json;
using SystemCompanion.Models;
using SystemCompanion.Services;
using Xunit;

namespace SystemCompanion.Tests;

public sealed class HistoryServiceTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "sc-history-" + Guid.NewGuid().ToString("N"));

    public HistoryServiceTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, true);
        }
        catch (IOException)
        {
            // A locked file must not fail a test run.
        }
    }

    [Fact]
    public void Entries_are_ordered_by_the_time_inside_the_file()
    {
        WriteRaw("scan-2026-10-17-1500.json", SnapshotJson("2026-10-17 09:00", 4800));
        WriteRaw("scan-2026-10-17-0900.json", SnapshotJson("2026-10-17 18:00", 4800));

        var entries = new HistoryService().ReadEntries(_folder);

        Assert.Equal(2, entries.Count);
        Assert.True(entries[0].TakenAt < entries[1].TakenAt);
        Assert.Equal("scan-2026-10-17-0900.json", entries[1].FileName);
    }

    [Fact]
    public void A_second_reading_in_the_same_minute_takes_a_counter_suffix()
    {
        var service = new HistoryService();
        var snapshot = Snapshot("2026-10-17 13:40", 4800);

        var first = service.WriteEntry(snapshot, _folder);
        var second = service.WriteEntry(snapshot, _folder);

        Assert.Equal("scan-2026-10-17-1340.json", Path.GetFileName(first));
        Assert.Equal("scan-2026-10-17-1340-2.json", Path.GetFileName(second));
        Assert.True(File.Exists(first));
        Assert.True(File.Exists(second));
    }

    [Fact]
    public void An_unreadable_entry_is_skipped_with_a_plain_note()
    {
        var service = new HistoryService();
        var good = service.WriteEntry(Snapshot("2026-10-17 13:40", 4800), _folder);
        WriteRaw("scan-broken.json", "{");

        Assert.Single(service.ReadEntries(_folder));

        var comparison = service.Compare(good, Path.Combine(_folder, "scan-broken.json"));

        Assert.True(comparison.NeedsSecondReading);
        var skipped = Assert.Single(comparison.Skipped);
        Assert.Contains("scan-broken.json", skipped.SkipNote, StringComparison.Ordinal);
    }

    [Fact]
    public void One_changed_value_is_reported_as_a_sentence()
    {
        var service = new HistoryService();
        var older = service.ReadEntry(service.WriteEntry(Snapshot("2026-10-17 13:40", 4800), _folder));
        var newer = service.ReadEntry(service.WriteEntry(Snapshot("2026-10-17 14:00", 6000), _folder));

        var comparison = service.Compare(older, newer);

        var row = Assert.Single(comparison.Changed);
        Assert.Equal("Memory speed", row.Label);
        Assert.Equal(ComparisonVerdict.Improved, row.Verdict);
        Assert.Contains("4800", row.Sentence, StringComparison.Ordinal);
        Assert.Contains("6000", row.Sentence, StringComparison.Ordinal);
        Assert.Contains("improvement", row.Sentence, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(comparison.Unchanged);
    }

    private string WriteRaw(string fileName, string content)
    {
        var path = Path.Combine(_folder, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private static string SnapshotJson(string generatedLocal, int configuredMt)
        => JsonSerializer.Serialize(Snapshot(generatedLocal, configuredMt));

    private static SystemSnapshot Snapshot(string generatedLocal, int configuredMt) => new()
    {
        GeneratedLocal = generatedLocal,
        ComputerName = "STUDIO",
        Memory = new MemoryInfo
        {
            InstalledGb = 32,
            VisibleGb = 31.8,
            ModuleCount = 2,
            ConfiguredMt = configuredMt,
            RatedMt = 6000
        }
    };
}
