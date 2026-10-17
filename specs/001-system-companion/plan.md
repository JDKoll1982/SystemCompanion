# Implementation Plan: System Companion

**Branch**: `001-system-companion` | **Date**: 2026-10-17 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-system-companion/spec.md`

## Scale note

This change spans three projects and roughly twenty files, across five areas: the
data models, the advice engine, history storage, the shell and navigation, and the
scan script. It also adds two projects that did not exist. A reader should watch
for the project split in Phase 1, because every later file reference depends on
which project owns it, and for the comparison field table, because it is the one
place that decides what counts as a change.

## Summary

The app already reads and explains a machine. This work adds the three things it
cannot do: keep past readings and compare them, prove its advice with tests, and
take a fresh reading without restarting. It also adds a guard for firmware the
BIOS explanations were not built from.

The technical shape is one new class library holding the models and the advice
engine so a test project can exercise them without WinUI, one new test project
with written readings as fixtures, and a history folder written by the existing
scan script so there is a single writer. The WinUI project keeps its views, view
models and WinUI-dependent display types.

## Technical Context

**Language/Version**: C# on `net10.0`, with the app on `net10.0-windows10.0.19041.0`

**Primary Dependencies**: `Microsoft.WindowsAppSDK` for the app only. The new
library and test project take no UI dependency. Tests use `xunit` and
`Microsoft.NET.Test.Sdk`, which are development dependencies and ship with
nothing.

**Storage**: JSON files under `Data/`. History is a new `Data/history/` folder,
one file per reading.

**Testing**: `dotnet test` on `SystemCompanion.Tests`, fed by written snapshot
fixtures. No network, no hosted agent.

**Target Platform**: Windows 10 19041 and later; x86, x64 and ARM64.

**Project Type**: desktop application with an extracted logic library.

**Performance Goals**: A scan takes as long as the existing script takes, roughly
a few seconds. Comparison over a folder of readings stays under a second for
hundreds of files.

**Constraints**: Read-only with respect to the system. Offline. No new UI, MVVM or
DI library. Every user-visible string uses plain language. Paths resolve through
`Services/AppData.cs`.

**Scale/Scope**: Four screens. Five user stories. Thirty functional requirements.

## Project Structure

### Documentation (this feature)

```text
specs/001-system-companion/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
└── tasks.md            # created by the tasks step
```

### Source code (repository root)

```text
SystemCompanion.slnx                        adds the two new projects
SystemCompanion.csproj                      WinUI 3 app, unchanged dependencies

SystemCompanion.Core/                       NEW class library, net10.0, no UI
├── SystemCompanion.Core.csproj
├── Models/
│   ├── SystemSnapshot.cs                   moved from Models/
│   ├── BiosProfile.cs                      moved from Models/
│   └── Suggestion.cs                       moved from Models/
└── Services/
    ├── RecommendationEngine.cs             moved from Services/, data injected
    ├── HistoryService.cs                   NEW: read, write, order, compare
    ├── ComparisonField.cs                  NEW: the compared-value table
    └── FirmwareReference.cs                NEW: the supported firmware tag

SystemCompanion.Tests/                      NEW xUnit project, net10.0
├── SystemCompanion.Tests.csproj
├── RecommendationEngineTests.cs
├── HistoryServiceTests.cs
├── FirmwareReferenceTests.cs
└── Fixtures/
    ├── balanced-power-plan.json
    ├── long-paths-on.json
    ├── four-sticks-6000.json
    ├── memory-integrity-on.json
    └── no-graphics.json

Models/Display.cs                           stays: WinUI brushes and visibility
Services/AppData.cs                         stays: path resolution for the app
ViewModels/
├── ShellViewModel.cs                       gains history and change count
├── HistoryViewModel.cs                     NEW
└── (three existing page view models)       gain a rescan command
Views/
├── HistoryPage.xaml(.cs)                   NEW
└── (three existing pages)                  gain a rescan button
MainWindow.xaml(.cs)                        fourth NavigationViewItem and route
App.xaml.cs                                 accepts `history` as a startup page

tools/discover-system.ps1                   writes the history copy after the snapshot
```

**Structure Decision**: Three projects in one solution. The library exists so the
advice engine can be tested without starting the window, which the constitution
requires. WinUI-dependent code stays in the app because it cannot move.

## Calls

The rescan path crosses four files. A page's command calls the shell, the shell
runs the script and reloads, and the reload rebuilds both the history view and the
change count.

```text
rescan command (page view model)
  -> ShellViewModel.RescanAsync()
       -> ScanRunner.RunAsync()            starts tools/discover-system.ps1
       -> AppData.ReadJson("current", "system-snapshot.json")
       -> HistoryService.ReadEntries()     re-reads Data/history
       -> ShellViewModel.System / History / ChangesSinceLastScan updated
  -> page rebuilds its view model from the shell
```

The comparison path stays inside the library.

```text
HistoryViewModel.Compare(left, right)
  -> HistoryService.Compare(leftPath, rightPath)
       -> ComparisonField.All                 the compared-value table
       -> ScanComparison { Changed[], Unchanged[] }
```

## Screens

The history screen is a picker, a summary, a timeline and a change list.

```text screen history
A person compares two readings. Header states how many values changed,
how many improved and how many declined. Two pickers choose the readings,
defaulting to the two newest. Below, one row per changed value reads as a
sentence. A timeline lists every past reading with its date and a one-line
summary. With one reading, a sentence explains a second is needed.
```

```text screen shell
A person sees how much has changed since the last reading. A short line under
the title, for example "3 things changed since your last scan", appears only when
two readings exist and differ. Selecting it opens the history screen.
```

## States

A reading has one lifecycle, and a scan has one.

```text reading
Recorded: a scan finished and wrote Data/current/system-snapshot.json. shows shell
Kept:     the same reading was copied to Data/history/scan-YYYY-MM-DD-HHmm.json.
Compared: two readings are paired and every compared value is classified. shows history
Skipped:  an entry could not be read, so it is named and left out. shows history
```

```text scan
Idle:      no scan running; the rescan action is available. shows shell
Running:   the script is executing; the action is disabled and shows progress. shows shell
Succeeded: the reading was reloaded and a history entry exists. shows shell
Failed:    the previous values stay on screen and a plain sentence reports it. shows shell
```

## Code

One line needs a note because it looks redundant next to the timestamp. Readings
are ordered by the time recorded inside the file, not by the file name, because a
clock change can make the two disagree.

`entries.OrderBy(e => e.TakenAt)` reads the parsed timestamp, never the path.

## Constitution Check

*GATE: must pass before Phase 0 research, re-checked after Phase 1 design.*

| Principle | Assessment | Notes |
| --- | --- | --- |
| I. Read-only by construction | PASS | The scan script already only reads. History writes are to the app's own data folder, never to the system. |
| II. Plain language is a functional requirement | PASS | Comparison text is a sentence per value. The field table carries a written label and a direction word for every entry. |
| III. Evidence before claims | PASS | No new advice is added. The comparison reports only values present in both readings. `Data/bios/` is untouched. |
| IV. Verifiable advice | PASS | This plan is what satisfies it. The engine moves to a library and gets a test project covering all five named cases. |
| V. No new frameworks | PASS | The app's shipped dependencies do not change. The two new projects add a class library and development-only test packages, no UI, MVVM or DI library, and no network use. |

No violations, so Complexity Tracking is omitted.
