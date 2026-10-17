# Core API contract

The library target is `net10.0` with no UI dependency. Models live in `SystemCompanion.Core.Models`, services in `SystemCompanion.Core.Services`.

## RecommendationEngine

```csharp
public sealed class RecommendationEngine
{
    public RecommendationEngine(
        SystemSnapshot system,
        BiosProfile bios,
        IReadOnlyDictionary<string, string> screenshotCheck);

    public IReadOnlyList<Suggestion> Build();
}
```

`Build()` returns every suggestion in a fixed order. It does not read files, does not use a network, and does not touch the user interface.

`screenshotCheck` maps a BIOS field name to its trusted label. A missing key yields an empty string and the existing fallback applies.

The engine must not depend on `AppData` or on any file location. A test constructs it entirely from in memory objects.

## Suggestion

| Member | Type | Meaning |
| --- | --- | --- |
| `Id` | `string` | Stable identifier, lowercase and hyphenated |
| `Title` | `string` | Short name |
| `Summary` | `string` | One line plain description |
| `Why` | `string` | Why it helps |
| `HowTo` | `string` | How to do it |
| `Goal` | `Goal` | Primary goal |
| `AlsoHelps` | `Goal[]` | Other goals it serves, so it is listed once |
| `Goals` | `IEnumerable<Goal>` | Primary goal followed by `AlsoHelps` |
| `State` | `SuggestionState` | Verdict |
| `Current` | `string` | Current reading |
| `Target` | `string` | Target reading |
| `Benefit` | `string` | Expected feel of the change |
| `Risk` | `string` | `Safe`, `Changes behaviour`, or `Advanced` |
| `NeedsRestart` | `bool` | True when a restart is needed |

`Goal` values are `Programming`, `Gaming`, `Ai`, `Everyday`. The display labels are `Programming`, `Gaming`, `AI work`, `Everyday`.

`SuggestionState` values are `AlreadyGood`, `WorthDoing`, `Optional`, `CheckYourself`. The display labels are `Already set up`, `Worth doing`, `Optional`, `Check yourself`.

## Suggestion identifiers

The identifier list is fixed and these strings must not be recased or renamed.

`power-plan`, `long-paths`, `startup-apps`, `defender-code-exclusions`, `system-drive-space`, `single-dotnet-sdk`, `wsl-for-linux-tooling`, `page-file`, `game-mode`, `gpu-scheduling`, `game-dvr`, `memory-integrity`, `refresh-rate`, `vrr-check`, `game-drive-space`, `gpu-vram`, `ai-model-exclusions`, `ollama-on-path`, `gpu-driver-fresh`, `ai-app-detected`, `bios-xmp`, `bios-resizable-bar`, `bios-4g`, `bios-cpu-leave-alone`, `bios-cooler-profile`.

The test suite asserts at least `power-plan`, `long-paths` and `memory-integrity`.

## HistoryService

```csharp
public sealed class HistoryService
{
    public IReadOnlyList<HistoryEntry> ReadEntries(string historyDirectory);
    public HistoryEntry? ReadEntry(string path);
    public string WriteEntry(SystemSnapshot snapshot, string historyDirectory);
    public IReadOnlyList<HistoryEntry> Order(IEnumerable<HistoryEntry> entries);
    public ScanComparison Compare(string leftPath, string rightPath);
}
```

`HistoryEntry` carries `Path`, `TakenAt` and `Snapshot`. `TakenAt` is parsed from `generatedLocal` inside the file.

`ReadEntries` reads every `scan-*.json` file in the directory and returns the readable entries only. `Compare` records the file name of every entry it had to skip in `ScanComparison.Skipped`.

`Order` sorts by `TakenAt` ascending. It never sorts by file name or write time.

`WriteEntry` applies the pinned name `scan-YYYY-MM-DD-HHmm.json` and the counter suffix rule from `history-files.md`. Production history writes happen in `tools/discover-system.ps1`, which follows the same rule. The method exists so the rule is testable.

`Compare` pairs two entries, walks `ComparisonField.All`, and returns `ScanComparison`.

## Comparison types

`ComparisonField` carries `Key`, `Label`, `Direction`, `Selector`, and `Format`.

`Direction` values are `HigherIsBetter`, `LowerIsBetter` and `Neutral`.

`ComparisonField.All` is the ordered table of compared values. It covers the identity line, processor, memory speed, memory module count, graphics adapter, graphics driver date, Windows build, power plan, game mode, game recording, hardware scheduling, memory integrity, long paths, refresh rate and free space per volume.

`ScanComparison` carries `Left`, `Right`, `Changed`, `Unchanged` and `Skipped`. `Changed` and `Unchanged` hold `ComparedValue` rows.

`ComparedValue` carries `Area`, `Name`, `Before`, `After` and `Direction`.

A changed row reads as one sentence, for example `Memory speed went from 4800 to 6000 MT/s, which is a real improvement`. A value that cannot be read is shown as exactly `Unknown`.

## FirmwareReference

```csharp
public static class FirmwareReference
{
    public const string SupportedTag = "E7E06IMS.AK0";
    public static bool Matches(string? fileTag);
}
```

`SupportedTag` is the firmware the BIOS explanations were built from. `Matches` compares it to `BiosProfile.File.BiosVersion`.

A missing or empty tag is not a match. A mismatch produces a plain language warning that names the file tag as `Unknown` when absent and names `E7E06IMS.AK0` as the supported firmware. A match produces no warning.
