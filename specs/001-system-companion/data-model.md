# Data Model: System Companion

This feature moves three existing models into `SystemCompanion.Core` without changing their shape. It adds a reading record, a history entry, a comparison with its rows, and the fixed table that decides which values are compared. Every field name below is taken from the current code or from the plan, and no new name is invented for an existing value.

`Unknown` is the required text for any value that cannot be read. It is a display value only. It is never written into a snapshot file and never stored in place of a real reading.

## Entity overview

| Entity | Owner | Status |
| --- | --- | --- |
| `SystemSnapshot` | `SystemCompanion.Core/Models/SystemSnapshot.cs` | moved, unchanged |
| `BiosProfile` | `SystemCompanion.Core/Models/BiosProfile.cs` | moved, unchanged |
| `Suggestion` | `SystemCompanion.Core/Models/Suggestion.cs` | moved, unchanged |
| Reading | logical record over one `SystemSnapshot` file | new |
| `HistoryEntry` | `SystemCompanion.Core/Services/HistoryService.cs` | new |
| `ScanComparison` | `SystemCompanion.Core/Services/HistoryService.cs` | new |
| `ComparisonRow` | `SystemCompanion.Core/Services/HistoryService.cs` | new |
| `ComparisonField` | `SystemCompanion.Core/Services/ComparisonField.cs` | new |
| `FirmwareReference` | `SystemCompanion.Core/Services/FirmwareReference.cs` | new |

## Relationships

One Reading holds exactly one `SystemSnapshot`. The current reading is the file at `Data/current/system-snapshot.json`. Every completed scan also produces one `HistoryEntry`, and that entry holds a copy of the same snapshot. A `ScanComparison` pairs exactly two `HistoryEntry` values and holds zero or more `ComparisonRow` values. Each `ComparisonRow` comes from exactly one `ComparisonField`. A `Suggestion` does not store a reference to a value. It is derived from one Reading, and the value it names must exist in that Reading. A `BiosProfile` describes one saved settings file, and `FirmwareReference` decides whether that profile may be explained from the reference data.

## SystemSnapshot, moved unchanged

The type keeps its properties and its JSON names. The namespace changes to `SystemCompanion.Core.Models`.

| Section | Properties |
| --- | --- |
| Top level | `GeneratedLocal`, `ComputerName`, `UserName`, `DotnetSdks`, `WslDistros` |
| `Identity` | `Manufacturer`, `Model`, `Board`, `BoardVersion`, `BiosVendor`, `BiosVersion`, `BiosDate` |
| `Cpu` | `Name`, `Cores`, `Threads`, `BaseMhz`, `L3CacheMb` |
| `Memory` | `InstalledGb`, `VisibleGb`, `ModuleCount`, `ConfiguredMt`, `RatedMt`, `Modules` |
| `Memory.Modules[]` | `Slot`, `SizeGb`, `SpeedMt`, `Part` |
| `Graphics` | `Adapters`, `Nvidia` |
| `Graphics.Adapters[]` | `Name`, `IsVirtual`, `VramGb`, `DriverVersion`, `DriverDate`, `CurrentWidth`, `CurrentHeight`, `RefreshHz` |
| `Storage` | `Drives`, `Volumes` |
| `Storage.Volumes[]` | `Letter`, `Label`, `SizeGb`, `FreeGb`, `FileSystem` |
| `Windows` | `Edition`, `Version`, `Build`, `DisplayVersion`, `InstallDate`, `LastBootHours`, `SystemDrive`, `LongPaths`, `PageFile`, `StartupItems` |
| `Performance` | `PowerPlan`, `GameMode`, `GameDvrEnabled`, `HardwareScheduling`, `HardwareSchedulingRaw`, `MemoryIntegrity`, `VbsConfigured`, `HypervisorLaunch`, `Defender` |
| `Tooling` | `Dotnet`, `Git`, `Node`, `Npm`, `Python`, `Pip`, `Docker`, `Code`, `Ollama`, `Nvidia` |
| `AiTools` | `LmStudio`, `OllamaDataDir` |

The nullable properties are `GraphicsInfo.VramGb`, `GraphicsInfo.CurrentWidth`, `GraphicsInfo.CurrentHeight`, `GraphicsInfo.RefreshHz`, `PerformanceInfo.GameMode`, `PerformanceInfo.GameDvrEnabled`, `PerformanceInfo.HardwareScheduling`, `PerformanceInfo.MemoryIntegrity`, `PerformanceInfo.VbsConfigured`, `DefenderInfo.RealtimeDisabled`, and the `Tooling` strings. A null value means the scan did not read it, which is different from a read value of false or zero. Those fields are the ones that can be rendered as `Unknown` without guessing.

The display layer must treat an absent section, an empty string, and a null nullable as unreadable. A non-nullable number is only trusted when its section is present. This keeps the app from presenting a default zero as a real speed or a real free-space figure.

## BiosProfile, moved unchanged

The type keeps its properties and its JSON names. The namespace changes to `SystemCompanion.Core.Models`.

`BiosProfile` has `GeneratedAt`, `File`, `Headline`, and `Sections`. `BiosFileInfo` has `Name`, `Board`, `BiosVersion`, `SavedOn`, `SavedOnDay`, `Valid`, `Checksum`, `SizeBytes`, `SavedVariables`, `ChangedBytes`, and `Cpu`. `BiosFact` has `Label`, `Value`, `Plain`, and `State`. `BiosSection` has `Title`, `Subtitle`, `Icon`, and `Items`. `BiosEntry` has `Name`, `Value`, `Plain`, `State`, and `Changed`.

`State` is one of `good`, `warn`, or `neutral`. `Changed` is true only when the setting differs from the factory default. A missing label, value, or explanation renders as `Unknown`, and the screen still shows the row. When `File.Valid` is false, the screen shows the identity and the save date but presents no reference-derived name as a decoded reading.

## Suggestion, moved unchanged

`Suggestion` keeps `Id`, `Title`, `Summary`, `Why`, `HowTo`, `Goal`, `AlsoHelps`, `State`, `Current`, `Target`, `Benefit`, `Risk`, and `NeedsRestart`. `Goals`, `GoalLabel`, `StateLabel`, and `StateGlyph` remain computed display helpers and are not serialized.

`Goal` is `Programming`, `Gaming`, `Ai`, or `Everyday`. `SuggestionState` is `AlreadyGood`, `WorthDoing`, `Optional`, or `CheckYourSelf`. State labels stay `Already good`, `Worth doing`, `Optional`, and `Check yourself` in plain language.

Every `Id` is unique in one advice build. A suggestion that serves more than one goal keeps one `Goal` and lists the rest in `AlsoHelps`. It is built once and shown under every goal in `Goals`. Each suggestion names at least one value that exists in the reading, and no suggestion is produced when that value is missing.

## New entity: Reading

A Reading is one capture of the machine at a point in time. Its fields are the `SystemSnapshot` plus the facts about where and when it was captured.

| Field | Type | Rule |
| --- | --- | --- |
| `Snapshot` | `SystemSnapshot` | Required. |
| `TakenAt` | `DateTimeOffset` | Parsed from `Snapshot.GeneratedLocal`. Never taken from the file name or the file write time. |
| `Path` | `string` | Full path of the file that holds the reading. |
| `FileName` | `string` | Display name only. Never used for ordering. |
| `IsCurrent` | `bool` | True for the reading at `Data/current/system-snapshot.json`. |

A Reading is `Recorded` when a scan writes it to the current location. It is `Kept` when the same content also exists under `Data/history/`. It is `Compared` when it is one of the two sides of a `ScanComparison`. It is `Skipped` when its file cannot be read or parsed, in which case it is named in plain language and left out.

Validation. `GeneratedLocal` must parse as a local date and time. A reading whose timestamp does not parse is not ordered and not compared. A reading with a missing section stays usable, and only the affected values render as `Unknown`.

## New entity: HistoryEntry

A `HistoryEntry` is the dated copy of a Reading.

| Field | Type | Rule |
| --- | --- | --- |
| `TakenAt` | `DateTimeOffset` | The ordering key. Parsed from `GeneratedLocal`. |
| `Path` | `string` | File under `Data/history/`. |
| `FileName` | `string` | `scan-YYYY-MM-DD-HHmm.json`, or a counter variant. |
| `Reading` | `Reading` | The parsed snapshot, or null when unreadable. |
| `IsReadable` | `bool` | False when the file is partial, empty, or not valid JSON. |
| `SkipNote` | `string` | Plain-language sentence naming the skipped file. Empty when readable. |

History entries are stored under `Data/history/` with the pattern `scan-YYYY-MM-DD-HHmm.json`. The first entry for a minute takes the plain pattern. A second entry for the same minute takes `scan-YYYY-MM-DD-HHmm-2.json`, then `-3`, and so on, so no reading is lost. The current reading stays at `Data/current/system-snapshot.json` and is never moved.

Entries are ordered by `TakenAt` and never by name. A missing or empty history folder yields zero entries and no error. An unreadable entry is skipped with a note, and the rest of the comparison still builds. No pruning or retention limit applies in this scope.

## New entity: ScanComparison

A `ScanComparison` is the result of pairing two history entries.

| Field | Type | Rule |
| --- | --- | --- |
| `Left` | `HistoryEntry` | Older side by default. Replaceable by any other entry. |
| `Right` | `HistoryEntry` | Newer side by default. Replaceable by any other entry. |
| `Changed` | `List<ComparisonRow>` | One row per compared value whose rendered values differ. |
| `Unchanged` | `List<ComparisonRow>` | One row per compared value whose rendered values are equal. |
| `Skipped` | `List<HistoryEntry>` | Entries left out because they could not be read. |
| `NeedsSecondReading` | `bool` | True when fewer than two readable entries exist. |
| `EverythingEqual` | `bool` | True when two readable entries exist and no row changed. |

The default pair is the two newest entries by `TakenAt`. Either side can be replaced by any other readable entry. With fewer than two entries the screen explains in plain words that a second reading is needed and shows no table. With two entries that are identical the screen states that nothing changed. The header counts changed values, improved values, and declined values from `Changed`.

The shell builds a change count from the two newest entries. It states the count only when both entries are readable and the count is greater than zero. Selecting that statement opens the comparison screen, whose startup selector value is `history`.

## New entity: ComparisonRow

A `ComparisonRow` is one compared value inside a comparison.

| Field | Type | Rule |
| --- | --- | --- |
| `Key` | `string` | The `ComparisonField` key, with an index or volume letter filled in where needed. |
| `Area` | `string` | The group the value belongs to, such as Memory or Windows. |
| `Label` | `string` | Plain-language name copied from the field table. |
| `Before` | `string` | Rendered value from the left reading, or `Unknown`. |
| `After` | `string` | Rendered value from the right reading, or `Unknown`. |
| `Direction` | `ComparisonFieldDirection` | Higher is better, lower is better, or neutral. |
| `Verdict` | `ComparisonVerdict` | Improved, declined, changed, same, or cannot tell. |
| `Sentence` | `string` | One plain-language sentence with the label, both values, and the verdict. |

`ComparisonVerdict` has five values. `Improved` and `Declined` are only used when the direction is not neutral and both values are readable. `Changed` is used when the direction is neutral and the values differ. `Same` is used when the rendered values are equal. `CannotTell` is used when either side is `Unknown`.

A row is built only for a value that both readings have a location for. A value that is unreadable on one side still produces a row, with `Unknown` on that side and the verdict `CannotTell`, so the difference is never hidden. Two readings that differ in exactly one value produce exactly one row in `Changed` and at least one row in `Unchanged`.

Neutral rows never say better or worse. Their sentence states that the value changed, for example that the motherboard still reads the same model or that it now reads a different one.

## New entity: ComparisonField

`ComparisonField` is one row of the fixed table that decides what counts as a change. It has five members.

| Member | Type | Rule |
| --- | --- | --- |
| `Key` | `string` | Exact property path inside `SystemSnapshot`. |
| `Area` | `string` | Group used for display and ordering. |
| `Label` | `string` | Plain-language name. Required and never empty. |
| `Direction` | `ComparisonFieldDirection` | HigherIsBetter, LowerIsBetter, or Neutral. |
| `Format` | `Func<object?, string>` | Turns the read value into display text, and returns `Unknown` when it cannot. |

The table is declared once in `SystemCompanion.Core/Services/ComparisonField.cs` as `ComparisonField.All`. The initial table is below.

| Key | Area | Label | Direction |
| --- | --- | --- | --- |
| `Identity.Board` | Identity | Motherboard | Neutral |
| `Identity.BiosVersion` | Identity | Firmware version | Neutral |
| `Cpu.Name` | Processor | Processor | Neutral |
| `Memory.ConfiguredMt` | Memory | Memory speed | Higher is better |
| `Memory.ModuleCount` | Memory | Memory sticks | Higher is better |
| `Graphics.Adapters[0].Name` | Graphics | Graphics card | Neutral |
| `Graphics.Adapters[0].DriverDate` | Graphics | Driver date | Higher is better |
| `Graphics.Adapters[0].RefreshHz` | Graphics | Screen refresh rate | Higher is better |
| `Storage.Volumes[].FreeGb` | Storage | Free space | Higher is better |
| `Windows.Build` | Windows | Windows build | Higher is better |
| `Windows.LongPaths` | Windows | Long path support | Higher is better |
| `Performance.PowerPlan` | Performance | Power plan | Higher is better |
| `Performance.GameMode` | Performance | Game Mode | Higher is better |
| `Performance.GameDvrEnabled` | Performance | Game recording | Lower is better |
| `Performance.HardwareScheduling` | Performance | Hardware scheduling | Higher is better |
| `Performance.MemoryIntegrity` | Performance | Memory integrity | Neutral |

The formatters use the same units the screens already show. Memory speed is MT/s, module count is a count of sticks, refresh rate is Hz, free space is GB, and the two toggles read On or Off. `Windows.Build` is compared as a number when both values parse, and as text otherwise. `Graphics.Adapters[0].DriverDate` is compared as a date when both values parse, and as text otherwise. `Performance.PowerPlan` uses a fixed rank order: Power saver, then Balanced, then High performance, then Ultimate Performance. A plan outside that order compares as neutral.

`Graphics.Adapters[0]` means the first adapter whose `IsVirtual` is false. When no physical adapter exists, the three graphics rows render `Unknown` and the comparison still builds. `Storage.Volumes[]` expands to one row per volume `Letter` that both readings contain. The row key carries the letter, for example `Storage.Volumes[C:].FreeGb`, and the label names the drive, for example `Free space on C:`. A volume present in only one reading produces no row.

## `FirmwareReference`

`FirmwareReference` is a single constant. `SupportedTag` is `E7E06IMS.AK0`. It is compared against `BiosProfile.File.BiosVersion`.

The comparison is exact and case sensitive. When the values differ, or the profile has no tag, the BIOS screen shows a plain-language warning that names the file's firmware and the supported firmware. A missing tag is also reported as `Unknown`. When the tags match, no warning is shown. A matching tag with a mismatched checksum, size, or variable count is a data-quality warning only and is not reported as a firmware mismatch.

## Validation rules

These rules are drawn from the functional requirements and apply across the entities above.

- Read-only. Nothing is written outside the app's own data folder. The system, the firmware, devices, and the original settings file are never modified.
- Storage. The current reading stays at `Data/current/system-snapshot.json`. History lives under `Data/history/`.
- Naming. A history file is `scan-YYYY-MM-DD-HHmm.json`. A collision adds a counter suffix rather than overwriting.
- Ordering. Entries sort by `TakenAt` parsed from `GeneratedLocal`. A file name never decides order.
- Unreadable entries. A partial, empty, or malformed file is skipped, named in plain language, and never aborts the comparison.
- Empty history. An empty folder yields no entries, no error, and the explanation that a second reading is needed.
- Unknown. Every unreadable, absent, or not-applicable value renders as exactly `Unknown`. No plausible substitute is shown and no default is presented as a reading.
- Explanations. Every user-visible value carries one plain-language sentence. No raw hexadecimal value and no unexplained abbreviation reaches a screen.
- Change count. The shell states the number of changed values only when two readable readings exist and differ. It states nothing when they are equal or when only one reading exists.
- Comparison reach. The comparison is the fourth screen. Its startup selector value is `history`, and the existing values `bios`, `windows`, and `improve` keep working. An unrecognised startup value falls back to the BIOS screen.
- Advice traceability. Each suggestion names at least one value present in the reading and is not produced when that value is missing.
- Advice independence. The engine takes the BIOS name-to-label map through its constructor and does not read files or reference the UI framework.
- Firmware gate. Reference-derived names are not presented as decoded readings when the tag differs or is missing.

## State transitions

A reading moves through four states.

| From | Event | To |
| --- | --- | --- |
| none | a scan writes the current file | Recorded |
| Recorded | the scan copies the same content to `Data/history/` | Kept |
| Kept | two entries are paired for comparison | Compared |
| Recorded or Kept | the file cannot be parsed | Skipped |

A scan moves through four states. No scan starts without a person asking for it.

| From | Event | To |
| --- | --- | --- |
| Idle | the rescan action is selected | Running |
| Running | the script exits successfully and the reading reloads | Succeeded |
| Running | the script exits non-zero, or the current file is missing | Failed |
| Succeeded or Failed | the action completes | Idle |

A failed scan leaves the displayed values and the existing history untouched. It reports the failure in one plain sentence. A successful scan replaces the displayed values and adds one history entry.

A suggestion state is derived on every advice build and is never stored. Any state may follow any other when the reading changes. The four states stay fixed: `AlreadyGood`, `WorthDoing`, `Optional`, and `CheckYourSelf`. The screen counts each state for its summary, and `AlreadyGood` suggestions stay visible so that work already correct is reported rather than omitted.
