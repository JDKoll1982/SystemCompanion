# Next steps — System Companion

> Paste this whole file into a new conversation to continue the work.
> It describes what exists, what is proven, and exactly what to build next.

---

## What this project is

A WinUI 3 desktop app at `C:\Users\johnk\source\repos\SystemCompanion` that explains
a Windows PC in plain language. Three screens:

1. **BIOS — right now** — the saved motherboard settings, translated out of firmware jargon.
2. **Windows — right now** — hardware and the Windows switches that affect speed.
3. **What could be better** — advice for programming, gaming and AI work.

**Audience rule (non-negotiable):** someone who has never opened a BIOS screen must
be able to read every screen and understand what to do. Plain words, no unexplained
abbreviations, no raw hex in the UI.

**Safety rule:** the app is read-only. It never changes a setting. Advice is text.

---

## Current state — working and verified

The app builds and runs. All three pages were launched and visually checked on
17 Oct 2026.

```powershell
cd C:\Users\johnk\source\repos\SystemCompanion
dotnet build SystemCompanion.csproj -c Debug
.\bin\Debug\net10.0-windows10.0.19041.0\SystemCompanion.exe [bios|windows|improve]
```

The optional first argument picks the starting page (used for development).

Stack: .NET 10 (`net10.0-windows10.0.19041.0`), WinUI 3 via
`Microsoft.WindowsAppSDK 1.8.*`, unpackaged (`WindowsPackageType=None`), no MVVM
package (there is a tiny `ObservableBase`), no third-party UI libraries.

### Folder map

```
App.xaml / App.xaml.cs          application entry, merges Styles/Theme.xaml
MainWindow.xaml / .cs           title bar, NavigationView, Frame, page routing
Views/                          BiosNowPage, WindowsNowPage, ImprovementsPage
ViewModels/                     ShellViewModel + one per page + ObservableBase
Models/                         SystemSnapshot, BiosProfile, Suggestion, Display
Services/                       AppData (paths + JSON), RecommendationEngine
Styles/Theme.xaml, Palette.cs   colours, typography, card and chip styles
Data/current/                   system-snapshot.json  (runtime input)
Data/bios/                      bios-profile.json     (runtime input)
                                screenshot_check.json (runtime input)
                                MsOcFile.ocb, decoded report, reference/, screenshots/
Data/bios/tools/                ocb_decrypt.py, build_bios_reference.py, verify_against_screenshots.py
tools/                          discover-system.ps1, build-bios-summary.py, refresh-and-run.ps1
docs/                           README.md, What-I-found.md, How-to-refresh.md
specs/001-system-companion/     plain-language requirements
```

Only `Data/current/system-snapshot.json`, `Data/bios/bios-profile.json` and
`Data/bios/screenshot_check.json` are copied to the output folder. The large
archive files (`.ocb`, screenshots, decoded report) stay in the project only.

### Data contracts (do not break these)

`Data/current/system-snapshot.json` — produced by `tools/discover-system.ps1`.
Top-level keys: `identity, cpu, memory, graphics{adapters[],nvidia[]}, storage,
windows, performance, tooling, aiTools, dotnetSdks, wslDistros`. C# mirror is
`Models/SystemSnapshot.cs`; JSON is camelCase and read case-insensitively.

`Data/bios/bios-profile.json` — produced by `tools/build-bios-summary.py`.
Keys: `file{}`, `headline[]{label,value,plain,state}`, `sections[]{title,subtitle,
icon,items[]{name,value,plain,state,changed}}`. `state` is `good|warn|neutral`.
C# mirror is `Models/BiosProfile.cs`.

`Data/bios/screenshot_check.json` — rows of `{name, ui, status, label, stored}`,
used by `RecommendationEngine.BestValue()` to read BIOS values such as
`Re-Size Bar Support`. Do not rename those `name` values without updating the engine.

---

## Start here — put this work under spec-kit, then build

The spec-kit tooling is installed but not yet used. State checked on 17 Oct 2026:

| Thing | State |
| --- | --- |
| `.specify/` — spec-kit 1.1.3, copilot integration, PowerShell scripts | present |
| `.specify/extensions/companion` — Companion 0.25.0, and its skills under `.github/skills/` | present |
| `.specify/memory/constitution.md` | still the raw template: `[PROJECT_NAME]`, `[PRINCIPLE_1_NAME]`, … |
| `specs/001-system-companion/` | `README.md` only, plus a `.spec-context.json` sitting at `currentStep: specify`, `status: draft` |
| `.specify/feature.json` | missing |
| `spec.md`, `checklists/`, `plan.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md`, `tasks.md` | all missing |

Right now `python3 .specify/extensions/companion/scripts/status-context.py` prints
"Could not resolve the active feature directory". Steps 0 and 1 below clear that.

**Do these two steps before touching any feature code.** They are how the backlog
already written in this file gets a home, and every later step reads their output.

### 0. Initialize the constitution

Run the `speckit-constitution` skill from the repository root
(`/speckit-constitution`). Do not hand-write `.specify/memory/constitution.md`.

**Preflight (verified working):**

```powershell
cd C:\Users\johnk\source\repos\SystemCompanion
.\.specify\scripts\powershell\resolve-template.ps1 constitution-template -Json
```

It must return `TEMPLATE_CONTENT`. If it fails, stop and fix template resolution
before going further.

**Give the skill this content** — the rules the repository already enforces, not
aspirations. Project name: `System Companion`. Ratified: `2026-10-17`. First
ratification, so `CONSTITUTION_VERSION` is `1.0.0`.

- **I. Read-only by construction.** The app MUST NOT write to the operating
  system, the firmware, any device, or `Data/bios/MsOcFile.ocb`. Every output is
  information. Advice is text the person carries out themselves.
- **II. Plain language is a functional requirement.** Every user-visible string
  MUST be understandable to someone who has never opened a BIOS screen: no
  unexplained abbreviations, no raw hex or property names. When a value cannot be
  read, the UI MUST say `Unknown` rather than show a plausible number.
- **III. Evidence before claims.** Every suggestion MUST trace to a reading in
  `Data/current/system-snapshot.json` or `Data/bios/screenshot_check.json`.
  `Data/bios/` is a verified audit trail and MUST change only by re-running the
  scripts in `Data/bios/tools/`; hand-editing it is prohibited.
- **IV. Verifiable advice.** `Services/RecommendationEngine.cs` MUST be covered by
  automated tests that feed hand-written snapshots in and assert suggestion states
  out. A behaviour change to the engine without a test is incomplete.
- **V. No new frameworks.** The dependency surface stays as it is: WinUI 3 via
  `Microsoft.WindowsAppSDK`, unpackaged (`WindowsPackageType=None`), no
  third-party UI or MVVM library, no network calls, no telemetry, no accounts.
  `ViewModels/ObservableBase.cs` exists for that reason.

Section 2 of the template (`Additional Constraints`) should hold the technical
constraints: `net10.0-windows10.0.19041.0`, x86/x64/ARM64, all file paths through
`Services/AppData.cs`, the three data contracts in the section above, and a build
with zero warnings from project code.

Section 3 (`Development Workflow`) should hold the quality gates: spec-kit
pipeline before feature work, `tools/refresh-and-run.ps1` plus a visual check of
each page as the manual verification, and no hand-editing of
`.spec-context.json` or `Data/bios/`.

Governance should state that amendments go through `speckit-constitution` with
semantic versioning (MAJOR for removing or redefining a principle, MINOR for a new
principle or materially expanded guidance, PATCH for clarification), that the
plan's Constitution Check must pass or justify each violation in Complexity
Tracking, and that the constitution outranks this file when the two disagree.

**Done when:**

```powershell
# no placeholder tokens left
Select-String .specify\memory\constitution.md -Pattern '\[[A-Z0-9_]+\]'
# Version: 1.0.0 | Ratified: 2026-10-17 | Last Amended: 2026-10-17
Select-String .specify\memory\constitution.md -Pattern '^\*\*Version'
```

The skill also writes a temporary Sync Impact Report as an HTML comment at the top
of the file. Keep it while reviewing, then delete it in the commit that ratifies
the constitution.

### 1. Generate the full `001-system-companion` spec folder

This is three commands in order: specify, plan, tasks. Stop after `tasks.md`.
Implementation is a separate pass (the backlog below), not part of generating the
structure.

Target the directory that already exists. **Do not let a fresh feature number be
minted.** Verified on 17 Oct 2026: `create-new-feature.ps1` given
`-ShortName "system-companion"` returns `002-system-companion`, because `001`
already exists. If the script has to be used at all, it is this form:

```powershell
.\.specify\scripts\powershell\create-new-feature.ps1 -Json -AllowExistingBranch -Number 1 -ShortName "system-companion" "System Companion"
```

Otherwise point the pipeline at the directory explicitly:

```powershell
cd C:\Users\johnk\source\repos\SystemCompanion
$env:SPECIFY_FEATURE_DIRECTORY = 'C:\Users\johnk\source\repos\SystemCompanion\specs\001-system-companion'
```

The environment variable does not survive between separate PowerShell calls, so
either set it in the same call that runs the script, or name the target path in the
command input as shown next.

**Step 1a — specify.** Use the Companion command, not the core `/speckit-specify`.
Only the Companion variant writes `.spec-context.json`, the size verdict and the
checklist.

Write the spec as **`spec.md`**, not the `<short-name>.spec.md` the Companion body
defaults to. Verified 17 Oct 2026: `.specify/scripts/powershell/common.ps1` sets
`FEATURE_SPEC` to `<feature dir>\spec.md`, and `setup-tasks.ps1` fails outright with
"spec.md not found" unless that file exists. Companion reads `spec.md` as its
documented fallback (`spec_context.py`), so the stock name works for both tools.
One file, no duplicate to drift.

```text
/speckit-companion-specify Target directory: specs/001-system-companion. It already
exists and holds only README.md, no spec file.

Write specs/001-system-companion/spec.md from these sources, in this order: the
whole of specs/001-system-companion/README.md (the plain-language requirements),
then docs/README.md, docs/What-I-found.md, and PROMPT-NEXT-STEPS.md.

The app is already built for three screens: BIOS — right now, Windows — right now,
and What could be better. The spec describes the intended behaviour of those three
as one user story (P1), then the unbuilt work in priority order: P2 Over time,
P3 automated tests for the advice engine, P4 live refresh, P5 broader BIOS support.
The change badge folds into P2 because it depends on history.

Requirements the spec must carry: read-only, plain language for every visible
string, `Unknown` instead of a guessed value, every suggestion traceable to a
reading, no network or telemetry, and automated tests for the recommendation
engine (constitution principle IV).

Keep the README's out-of-scope list as explicit non-goals: changing settings
automatically, live sensor graphs, reading a BIOS other than E7E06IMS.AK0, and
accounts, cloud sync or notifications.

Do not rewrite, rename, or delete README.md.
```

Expect the size verdict `oversized`: five stories, more than ten files, multiple
subsystems. That only adds a Scale note to the plan; it never trims anything. If
specify returns `simple`, the classification is wrong for this scope — stop and
re-run rather than accepting the lean fold.

If the spec comes back with any `[NEEDS CLARIFICATION]` marker, run
`/speckit-clarify` and answer it. A marker left in the spec becomes a guessed
requirement later.

**Step 1b — plan.**

```text
/speckit-companion-plan specs/001-system-companion
```

The plan must load and pass a Constitution Check against the file written in step 0.
`research.md` must answer the one real design question: how the advice engine gets
tested from a separate project. Either the test project references the WinUI app
project (same target framework, `UseWinUI`, Windows App SDK available to the test
host) or the engine and models move into a class library the app and the tests both
reference. Record the choice and the reason; do not leave it implicit.

`data-model.md` must cover the existing `SystemSnapshot`, `BiosProfile` and
`Suggestion` shapes plus the new `ScanComparison` and `HistoryScan` types.

`contracts/` must hold the data contracts the app depends on, because this file
declares them non-negotiable: `system-snapshot`, `bios-profile`,
`screenshot_check`, the new history scan file, and the command-line entry point
(`bios | windows | improve`, plus the new `history`).

**Step 1c — tasks.**

```text
/speckit-companion-tasks specs/001-system-companion
```

Then, optionally, `/speckit-analyze` for a cross-artifact check of spec, plan and
tasks. It is read-only and worth running once.

**Files that must exist when step 1 finishes:**

```text
specs/001-system-companion/
├── README.md                  # source input, left exactly as it is
├── spec.md                    # specify: User Scenarios & Testing, Requirements,
│                              #   Key Entities, Success Criteria, Assumptions
├── checklists/requirements.md # specify
├── plan.md                    # plan: Summary, Technical Context, Constitution Check,
│                              #   Project Structure, Complexity Tracking
├── research.md                # plan, phase 0 (the test-project decision)
├── data-model.md              # plan, phase 1
├── quickstart.md              # plan, phase 1 (build, refresh, launch each page)
├── contracts/                 # plan, phase 1 (the four JSON shapes + CLI entry point)
├── tasks.md                   # tasks: phased by story, wave-ordered, one owner per file
└── .spec-context.json         # Companion only — never hand-edited
```

Plus `.specify/feature.json` containing
`{"feature_directory": "specs/001-system-companion"}`.

Living specs are not configured in this repository (no `.specify/companion.yml`,
no `living-specs.yml`), so the load step is a silent no-op. Do not create a
registry as part of this work.

### Backlog that must appear in the spec and the task list

Each row is an item from this file that is **not built yet**. The spec's user
stories and `tasks.md` must cover it; the three existing screens must appear as a
baseline story whose tasks verify them rather than rewrite them.

| # | Item | Source in this file | Story | Required in `tasks.md` |
| --- | --- | --- | --- | --- |
| 1 | Over time screen: keep every scan, compare two, timeline | "The very next move" | P2 | history folder + seeded first entry in `tools/discover-system.ps1`, `Services/HistoryService.cs`, `Models/ScanComparison.cs`, `Views/HistoryPage.xaml` + `ViewModels/HistoryViewModel.cs`, fourth `NavigationViewItem` and route, `history` CLI switch |
| 2 | Change badge since the last scan | After that, #4 | P2 (needs history) | badge in `MainWindow`/`ShellViewModel`, click-through to the history page |
| 3 | Testable advice engine | After that, #2 | P3 | test project, the five named cases (Balanced plan, `longPaths`, four DIMMs at 6000, `memoryIntegrity`, no GPU) |
| 4 | Live refresh without restarting | After that, #3 | P4 | "Scan again" command per page, `Process.Start` of `tools/discover-system.ps1` with `-NoProfile`, re-read JSON, rebuild view models, progress on the button |
| 5 | Broader BIOS support and a mismatch warning | After that, #5 | P5 | firmware-tag check against the reference, explicit warning when it does not match |

If a task list comes back describing a rewrite of an existing page, or omits one
of these five rows, the task generation is wrong. Fix it before implementing.

### Verify the structure exists

```powershell
cd C:\Users\johnk\source\repos\SystemCompanion

# 1. the artifact set
Get-ChildItem specs\001-system-companion -Recurse -File | Select-Object -ExpandProperty FullName

# 2. the pipeline recognises everything it needs
.\.specify\scripts\powershell\check-prerequisites.ps1 -Json -RequireSpec -RequireTasks -IncludeTasks

# 3. Companion state: expect  Step: tasks   Status: ready-to-implement
python3 .specify\extensions\companion\scripts\status-context.py --feature-dir specs\001-system-companion

# 4. the plan's cited paths resolve and its calls and screens exist
python3 .specify\extensions\companion\scripts\check_plan.py --feature-dir specs\001-system-companion --no-record

# 5. run health: unfinished steps, unjournaled tasks, step bleed (read-only)
python3 .specify\extensions\companion\scripts\doctor.py --feature-dir specs\001-system-companion
```

`AVAILABLE_DOCS` from check 2 must list `research.md`, `data-model.md`,
`contracts/`, `quickstart.md` and `tasks.md`. A missing one means plan or tasks did
not finish.

### Do not

- **Run `/speckit-companion-auto`.** It runs implement and marks the spec complete,
  so it writes code in the same pass as spec generation.
- **Run `/speckit-companion-implement` yet.** Generation stops at `tasks.md`.
- **Hand-edit `.spec-context.json`.** The scripts stamp the clock and write
  atomically; a hand-authored entry is what corrupts the file.
- **Create `specs/002-*`.** The existing directory is the target.
- **Rename or delete `specs/001-system-companion/README.md`.** It is the source the
  spec is written from and the plain-language reference afterwards.

---

## The very next move

> Tracked by the spec as P2 (`Over time`). `tasks.md` now owns the order of work;
> this section stays as the design brief.

**Add a fourth screen: "Over time".** This is the highest-value next feature
because the app can already take a reading on demand but cannot show whether
anything improved.

### Why this and not something else

The user's questions are all of the form "is my machine set up well, and did my
change help?". Today the app answers the first. It cannot answer the second,
because each scan overwrites the previous one. A history screen closes that loop
and makes the whole app worth opening twice.

### What to build

1. **Keep every scan, not just the latest.** In `tools/discover-system.ps1`, before
   writing the snapshot, copy the new result to
   `Data/history/scan-YYYY-MM-DD-HHmm.json`. Keep the existing
   `Data/current/system-snapshot.json` exactly as it is so nothing else breaks.
2. **Compare two scans.** Add `Services/HistoryService.cs` plus
   `Models/ScanComparison.cs`. Produce a list of `{ area, name, before, after,
   direction }` for the values that changed, and a list of values that did not.
3. **Add `Views/HistoryPage.xaml`** with `ViewModels/HistoryViewModel.cs`.
   - A picker for "compare this scan with that scan" (default: newest two).
   - Headline: what improved, what got worse, what stayed the same.
   - A timeline list of past scans with date, and a one-line summary.
   - Plain language: "Memory speed went from 4800 to 6000 MT/s — this is a real
     improvement", not "value 4800 → 6000".
4. **Register the page** in `MainWindow.xaml` as a fourth `NavigationViewItem`
   with `Tag="history"`, and handle it in `MainWindow.xaml.cs`
   (also extend the CLI switch in `App.xaml.cs` to accept `history`).
5. **Seed the folder** with the current snapshot copied as the first history entry,
   so the screen is not empty on first run.

### Acceptance criteria

- Launching with `history` shows the new screen; the other three still work.
- With one scan present, the screen says so in plain words instead of showing an
  empty table.
- With two scans that differ only in memory speed, exactly one row is reported as
  changed, and it reads in a sentence.
- The app still runs with no network.
- `dotnet build` has zero warnings from project code.

---

## After that, in priority order

> These map onto the spec's stories P3–P5: `#2` tests is P3, `#3` live refresh is
> P4, `#5` BIOS breadth is P5. `#4`, the change badge, folds into P2 because it
> needs history. Once `tasks.md` exists it owns the order of work; the text here is
> the design brief for each one.

### 2. Make the advice engine testable

`RecommendationEngine` currently computes everything in one pass and is only
verifiable by looking at the screen. Add a small console project or an xUnit
project `SystemCompanion.Tests` that feeds hand-written snapshots in and asserts
the states out. Cover at least:

- power plan set to `Balanced` → `power-plan` is `WorthDoing`
- `longPaths: true` → `long-paths` is `AlreadyGood`
- 4 DIMMs at 6000 → memory suggestion is not flagged
- `memoryIntegrity: true` → the security trade-off card is `WorthDoing`
- a snapshot with no GPU → no crash, and the AI cards degrade gracefully

This is the single best guard against the app quietly giving wrong advice.

### 3. Live refresh without restarting

Right now data is read once in the `ShellViewModel` constructor. Add a "Scan
again" button on each page that re-runs `tools/discover-system.ps1` (via
`Process.Start`, `-NoProfile`), waits for exit, re-reads the JSON, and rebuilds
the view models. Show progress on the button. Keep it explicit — never rescan
silently.

### 4. Explain "what changed since you last looked"

Once history exists, show a small badge on the shell — for example
"3 things changed since your last scan" — clickable through to the history screen.

### 5. Broaden the BIOS side beyond this one board

`Data/bios/reference/` is tied to firmware `E7E06IMS.AK0`. To support another
BIOS version, `build_bios_reference.py` needs the new image plus extracted IFR
text; the method is documented in `Data/bios/README-format-notes.md`. If that is
attempted, the app must clearly warn when the profile's firmware tag does not
match the reference, rather than showing wrong names against right offsets.

---

## Constraints to respect

- **Do not add a UI framework or MVVM library.** The project deliberately has no
  external UI dependencies. `ObservableBase` exists for that reason.
- **Do not edit anything under `Data/bios/` except by re-running the provided
  scripts.** Those files are the verified audit trail for a reverse-engineered
  format; hand-editing them destroys the ability to re-check the work.
- **Never write to `Data/bios/MsOcFile.ocb`.** It is the user's original profile.
- **Keep the app read-only** with respect to the operating system.
- **Plain language is a functional requirement**, not a style preference. Any new
  UI text must be understandable to a non-technical reader.
- Unpackaged app: file paths must go through `Services/AppData.cs`, which already
  handles both the `bin` location and the project-root location.

---

## Known pitfalls already hit (do not rediscover these)

| Symptom | Cause | Fix already in place |
| --- | --- | --- |
| `Unknown member 'text'` XAML error | lowercase attribute name | attribute must be `Text` |
| `ToggleButton` not found in code-behind | lives in `Microsoft.UI.Xaml.Controls.Primitives` | add that `using` |
| Build fails with file locked | the app is still running and holds the exe | stop the process before building |
| `Shared cache 0 MB` | `Win32_Processor.L3CacheSize` is in **kilobytes**, not megabytes | divide by `1KB` |
| Graphics card reports 4 GB | `Win32_VideoController.AdapterRAM` is a signed 32-bit value | read VRAM from `nvidia-smi` instead |
| Duplicate advice cards | one suggestion serving two goals | `Suggestion.AlsoHelps` + `Goals` list |
| BIOS rows showing `Profile 1 (1)` | label helper appended the raw value | label helper returns the label only |

---

## How to verify a change

```powershell
# 1. refresh data, rebuild, launch
.\tools\refresh-and-run.ps1 -Page improve

# 2. check the build is clean
dotnet build SystemCompanion.csproj -c Debug | Select-String 'warning|error'

# 3. confirm the JSON the app relies on actually exists in the output
Get-ChildItem .\bin\Debug\net10.0-windows10.0.19041.0\Data -Recurse
```

Visual check: launch with each of `bios`, `windows`, `improve` (and `history` once
it exists) and confirm the page header text, the headline tiles and at least one
detail card render with real values (not `Unknown`, not `—`).

After a spec-kit step, also confirm the pipeline still agrees with the files on disk:

```powershell
python3 .specify\extensions\companion\scripts\status-context.py --feature-dir specs\001-system-companion
.\.specify\scripts\powershell\check-prerequisites.ps1 -Json -RequireSpec -RequireTasks -IncludeTasks
```

---

## Background you may need

- The BIOS profile format, checksum rule, record layout and the reference data
  pipeline are documented in `Data/bios/README-format-notes.md`. Read it before
  touching anything BIOS-related.
- The machine's current readings and what they mean are in
  `docs/What-I-found.md`.
- The plain-language requirements, including what is explicitly out of scope, are in
  `specs/001-system-companion/README.md`. Once step 1 has run, the normative
  documents are `specs/001-system-companion/spec.md`, `plan.md`, and `tasks.md`;
  the README stays as the readable introduction.
