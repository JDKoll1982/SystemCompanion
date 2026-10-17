# Research: System Companion

Phase 0. Each entry is a decision the plan needed and the codebase did not settle.

## Testing the advice engine from outside the app

**Decision**: Move `Models/SystemSnapshot.cs`, `Models/BiosProfile.cs`,
`Models/Suggestion.cs` and `Services/RecommendationEngine.cs` into a new
`SystemCompanion.Core` class library targeting `net10.0`, referenced by the app
and by a new `SystemCompanion.Tests` project.

**Rationale**: The three models and the engine use only the standard library and
`System.Text.Json`, which the mapping confirmed. A plain library means the test
host needs no Windows App SDK bootstrap, no XAML compilation, and no window. It
also makes the constitution's "must not depend on the user interface framework"
requirement structural rather than a promise.

**Alternatives considered**: Let the test project reference the WinUI app project
directly. Rejected because the app is an unpackaged WinUI executable: referencing
it pulls in the XAML compiler and needs the Windows App SDK runtime initialized in
the test host, which fails in a way that looks like a test bug. A console harness
was also considered and rejected: it proves nothing on a build machine and cannot
assert.

## Getting the screenshot-checked BIOS values into the engine

**Decision**: Add an `IReadOnlyDictionary<string, string>` of BIOS name to label to
the `RecommendationEngine` constructor. The app builds it from
`Data/bios/screenshot_check.json` through `AppData.ReadJson`. Tests pass a literal
dictionary.

**Rationale**: `BestValue` currently calls `AppData.ReadJson` from inside the
engine, which ties the engine to a static path resolver and to the app's file
layout. Injecting the already-parsed values removes both couplings with a smaller
change than an interface with one implementation. The existing fallback of an
empty string is preserved, so callers behave the same.

**Alternatives considered**: Inject an interface with one implementation, or move
`AppData` into the library. Both add indirection or drag path resolution into a
library that should not know where files live. Rejected.

## Who writes a history entry

**Decision**: `tools/discover-system.ps1` writes the history copy itself, right
after it writes `Data/current/system-snapshot.json`.

**Rationale**: There are three ways a reading gets taken: the refresh script, the
new in-app rescan, and a person running the scan by hand. If the app wrote the
history copy, two of those three would silently produce no history, and the two
writers could race. One writer at the end of the scan covers all three.

**Alternatives considered**: Having the app copy the file after a rescan.
Rejected: a manual scan then leaves no history, and the app would need to know
which scan produced which file.

## Two readings inside the same minute

**Decision**: The first entry for a minute takes the pinned name
`scan-YYYY-MM-DD-HHmm.json`. If that file already exists, append a counter before
the extension: `scan-2026-10-17-1340-2.json`, then `-3`, and so on.

**Rationale**: The spec requires every completed scan to be kept and also pins the
file name pattern. Overwriting would lose a reading. The counter keeps the pinned
pattern as the normal case and bends it only when the alternative is data loss.

**Alternatives considered**: Overwrite the existing entry. Rejected, it loses a
reading. Always include seconds in the name. Rejected, it changes the pinned
pattern for every file to solve a rare case.

## Ordering readings when the clock moves

**Decision**: Order entries by `generatedLocal` parsed from inside the file.
A file that cannot be parsed is skipped and named. File names are used only for
display and never for ordering.

**Rationale**: The spec requires ordering by the reading the entry contains. A
clock change, a copied file, or a counter suffix all break name order while the
recorded time inside the file stays honest.

**Alternatives considered**: Order by file write time. Rejected, copying a folder
rewrites it. Order by name. Rejected, the counter suffix and a clock change both
defeat it.

## What counts as a changed value

**Decision**: A fixed table in `SystemCompanion.Core/Services/ComparisonField.cs`,
one row per compared value, each with a key, a plain-language label, a direction
(higher is better, lower is better, or neutral) and a formatter. The first table
covers the identity line, processor, memory speed and module count, graphics
adapter and driver date, Windows build, power plan, game mode, game recording,
hardware scheduling, memory integrity, long paths, refresh rate, and free space
per volume.

**Rationale**: Diffing arbitrary JSON would report internal bookkeeping and would
have no way to say whether a change is an improvement. A declared table is
readable, testable, and gives each row a sentence template. Restricting the table
to values the app already shows also satisfies the traceability requirement.

**Alternatives considered**: Reflect over the whole snapshot. Rejected: nested
lists reorder, timestamps always differ, and no row would know which direction is
better.

## Running a scan from inside the app

**Decision**: Start `powershell.exe` with `-NoProfile -ExecutionPolicy Bypass -File
tools/discover-system.ps1`, no shell execute, no window, and await process exit.
Disable the action while running. On a non-zero exit or a missing file, keep the
current values and report the failure in a sentence.

**Rationale**: The script has one parameter and already defaults its output path,
so it needs no arguments. `-NoProfile` keeps a person's profile from changing
behaviour or printing to the console. Not showing a window keeps the app from
flashing a console.

**Alternatives considered**: Reimplement the collection in C#. Rejected: it
duplicates a verified script and doubles the surface that can drift. Run the scan
in-process. Rejected: the script needs PowerShell 5.1 semantics that the app does
not host.

## Where the supported firmware tag lives

**Decision**: A constant in `SystemCompanion.Core/Services/FirmwareReference.cs`,
`SupportedTag = "E7E06IMS.AK0"`, compared against `BiosProfile.File.BiosVersion`.

**Rationale**: The tag is a property of the reference data set, not of the user's
machine. A constant is testable in one line and cannot drift from the value the
spec pins.

**Alternatives considered**: Read it from `Data/bios/reference/`. Rejected: the
reference files describe decoded fields, not the tag, and reading them at startup
adds a fourth file dependency to a check that needs one string.
