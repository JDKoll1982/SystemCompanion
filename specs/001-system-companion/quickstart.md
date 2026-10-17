# Quickstart: System Companion

How to build it, prove it, and check each screen by hand. Run everything from the
repository root.

## Build

```powershell
dotnet build SystemCompanion.slnx -c Debug
```

The solution gains two projects: `SystemCompanion.Core` and
`SystemCompanion.Tests`. If the solution file was not updated, build the app
directly with `dotnet build SystemCompanion.csproj -c Debug` and treat a missing
project as a failed task.

A build must report zero warnings from project code.

## Test the advice engine

```powershell
dotnet test SystemCompanion.Tests\SystemCompanion.Tests.csproj
```

The suite reads written readings from `SystemCompanion.Tests\Fixtures\` and needs
no network and no running app. It covers a `Balanced` power plan, long path support
enabled, four memory sticks at 6000 MT/s, memory integrity enabled, and a reading
with no graphics adapter.

To confirm the suite can fail, change one expectation in
`RecommendationEngineTests.cs` from already good to worth doing, run it, watch it
fail, then restore the line.

## Take a reading

```powershell
.\tools\discover-system.ps1
```

That writes `Data/current/system-snapshot.json` and one entry under
`Data/history/`. Run it twice, a minute apart, and confirm two entries exist.

## Refresh and start the app

```powershell
.\tools\refresh-and-run.ps1 -Page history
```

`-Page` accepts `bios`, `windows`, `improve` and `history`. The same values work as
the first argument to the executable:

```powershell
.\bin\Debug\net10.0-windows10.0.19041.0\SystemCompanion.exe windows
```

An unrecognised value falls back to `bios`.

## Check each screen by hand

| Screen | What to confirm |
| --- | --- |
| `bios` | Board, firmware and save date are shown. Four headline facts. Every detail row has a name, a value and a sentence. A changed value carries a default marker. |
| `windows` | Four headline facts. Grouped detail includes startup programs and developer tools. |
| `improve` | Summary counts appear. Filters work. One card per suggestion, with current, target, why, how, benefit, risk and restart. |
| `history` | Two readings compared by default. Changed values read as sentences. Unchanged values listed. Timeline shows every reading. |

None of the four should show a raw hexadecimal value or `Unknown` where the machine
can be read.

## Check the history behaviour

1. With one reading present, open `history`. It must say a second reading is
   needed, not show an empty table.
2. Take a second reading with nothing changed. The comparison must say nothing
   changed, and the shell must not show a change count.
3. Change something the table covers, such as the power plan, then take a reading.
   The comparison must report exactly that value, and the shell must show a change
   count that opens the history screen.
4. Rename one history file to `scan-broken.json` and put a single brace in it.
   Open `history`. The entry must be named as skipped and the screen must still
   work.

## Check the firmware warning

`Data/bios/bios-profile.json` carries the firmware tag at `file.biosVersion`. The
supported tag is `E7E06IMS.AK0`.

1. With the tag unchanged, open `bios`. No warning appears.
2. Edit a copy of the profile's tag to `E7E06IMS.ZZ9`, then open `bios`. A
   plain-language warning names both tags and no reference-derived name is
   presented as a reading.
3. Remove the tag entirely. The screen shows `Unknown` for it and the same
   warning.

Restore the original tag afterwards. Do not hand-edit anything else under
`Data/bios/`.

## Check a rescan

With the app open on any screen, select the rescan action. The action shows
progress and is unavailable while running. When it finishes, the visible values
come from the new reading and a new entry appears under `Data/history/`. Select it
twice in a row: only one scan runs.

## Pipeline checks

```powershell
python3 .specify\extensions\companion\scripts\status-context.py --feature-dir specs\001-system-companion
.\.specify\scripts\powershell\check-prerequisites.ps1 -Json -RequireSpec -RequireTasks -IncludeTasks
```

The first prints the step and status. The second lists `research.md`,
`data-model.md`, `contracts/`, `quickstart.md` and `tasks.md` once the documents
exist.
