# Tasks: System Companion

**Input**: Design documents from `/specs/001-system-companion/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Scale note**: Forty-five tasks across three projects and five stories. The
Foundational phase is the long one, at fifteen tasks, because the library
extraction and the shell state both block every story. Watch two joins: the
engine move (Wave 2) must land before the advice screen changes, and the shell
reload (Wave 3) must land before the history screen can show anything.

## Format

`- [ ] **T###** [P?] [US#] Description · exact/file/path`

`[P]` marks a task that is independent of the others in its wave. Every file below
belongs to exactly one phase. US1 owns no page file: the three screens already
exist, so its tasks fix the display mapping and verify against the requirements.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: The two new projects and the references between them.

**Wave 1 — independent (different files):**

- [x] **T001** [P] Create the Core library project targeting `net10.0` with nullable enabled · `SystemCompanion.Core/SystemCompanion.Core.csproj`
- [x] **T002** [P] Create the test project with `xunit`, `xunit.runner.visualstudio` and `Microsoft.NET.Test.Sdk` · `SystemCompanion.Tests/SystemCompanion.Tests.csproj`

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 — independent (different files):**

- [x] **T003** [P] Add a project reference from the app to the Core library · `SystemCompanion.csproj`
- [x] **T004** [P] Add a project reference from the test project to the Core library · `SystemCompanion.Tests/SystemCompanion.Tests.csproj`
- [x] **T005** [P] Register both new projects in the solution · `SystemCompanion.slnx`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The library extraction, the history and comparison services, and the
shell state. No story can start until this phase completes.

**Files**: `SystemCompanion.Core/Models/`, `SystemCompanion.Core/Services/`,
`Services/AppData.cs`, `Services/ScanRunner.cs`, `ViewModels/ImprovementsViewModel.cs`,
`ViewModels/ShellViewModel.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`,
`tools/discover-system.ps1`

**Wave 1 — independent (different files):**

- [x] **T006** [P] Move the snapshot model into Core, keeping every property and JSON key unchanged · `SystemCompanion.Core/Models/SystemSnapshot.cs`
- [x] **T007** [P] Move the BIOS profile model into Core, keeping every property and JSON key unchanged · `SystemCompanion.Core/Models/BiosProfile.cs`
- [x] **T008** [P] Move the suggestion model into Core, keeping both enums and every property unchanged · `SystemCompanion.Core/Models/Suggestion.cs`
- [x] **T009** [P] Declare the compared-value table with key, area, label, direction and formatter for every row in data-model.md · `SystemCompanion.Core/Services/ComparisonField.cs`
- [x] **T010** [P] Add the supported firmware tag constant and its comparison helper · `SystemCompanion.Core/Services/FirmwareReference.cs`
- [x] **T011** [P] After writing the snapshot, copy it to `Data/history/scan-YYYY-MM-DD-HHmm.json`, appending a counter when that name exists, and keep the current file untouched · `tools/discover-system.ps1`
- [x] **T012** [P] Read `bios/screenshot_check.json` once into a name-to-label dictionary · `Services/AppData.cs`

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 — independent (different files):**

- [x] **T013** [P] Move the engine into Core, take the BIOS values as a constructor argument, and delete its `AppData` call · `SystemCompanion.Core/Services/RecommendationEngine.cs`
- [x] **T014** [P] Build the history service: enumerate entries, parse the time inside each file, order by that time, skip unreadable entries with a reason, and compare two entries through the table · `SystemCompanion.Core/Services/HistoryService.cs`

**⟶ Wait for Wave 2 to finish, then:**

**Wave 3 — independent (different files):**

- [x] **T015** [P] Pass the BIOS values when the engine is built · `ViewModels/ImprovementsViewModel.cs`
- [x] **T016** [P] Extract loading into a reload method, then expose the history entries and the count of values changed since the previous reading, and raise change notification for both · `ViewModels/ShellViewModel.cs`

**⟶ Wait for Wave 3 to finish, then:**

**Wave 4 — independent (different files):**

- [x] **T017** [P] Run the scan script with `-NoProfile -ExecutionPolicy Bypass -File`, wait for exit, and report success or failure · `Services/ScanRunner.cs`
- [x] **T018** [P] Add the fourth navigation item with `Tag="history"`, the change-count line, and the host for the scan bar · `MainWindow.xaml`

**⟶ Wait for Wave 4 to finish, then:**

**Wave 5 — independent (different files):**

- [x] **T019** [P] Route `history` to the history page and keep the existing three routes · `MainWindow.xaml.cs`
- [x] **T020** [P] Add the rescan command that runs one scan at a time, reloads on success, and reports failure without changing the displayed values · `ViewModels/ShellViewModel.cs`

**Checkpoint**: The library builds, the app still opens on all three screens, and a
scan writes one history entry.

---

## Phase 3: User Story 1 - Understand the machine as it is today (Priority: P1) 🎯 MVP

**Goal**: Every visible value reads in plain language, and an unreadable value says
`Unknown` rather than a guess.

**Independent Test**: Open each screen with no network and confirm every line shows
a reading plus a sentence, and that no internal name or hex value appears.

**Files**: `Models/Display.cs`

### Implementation

**Wave 1:**

- [x] **T021** [US1] Make the display mapping return `Unknown` for any unreadable value and pair every value with its explanation sentence · `Models/Display.cs`

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 — independent (different screens):**

- [x] **T022** [P] [US1] Verify the BIOS screen against FR-005 and record any gap as a new task
- [x] **T023** [P] [US1] Verify the Windows screen against FR-006, including startup programs and developer tools
- [x] **T024** [P] [US1] Verify the advice screen against FR-007 to FR-010, including the four filters and the duplicate-goal rule

**Checkpoint**: The three original screens satisfy their requirements, and the app
is still the product it was.

---

## Phase 4: User Story 2 - See whether a change helped (Priority: P2)

**Goal**: Past readings are kept, any two can be compared in sentences, and the
shell states how much changed since the last reading.

**Independent Test**: With two readings differing only in memory speed, open
`history` and confirm exactly one changed row.

**Files**: `ViewModels/HistoryViewModel.cs`, `Views/HistoryPage.xaml`,
`Views/HistoryPage.xaml.cs`, `App.xaml.cs`, `tools/refresh-and-run.ps1`

### Implementation

**Wave 1 — independent (different files):**

- [x] **T025** [P] [US2] Build the history view model: entry list, two selections defaulting to the newest pair, changed and unchanged rows, and the one-reading message · `ViewModels/HistoryViewModel.cs`
- [x] **T026** [P] [US2] Build the history page with two pickers, a summary line, a change list and a timeline, reusing `Card` and `H2Text` · `Views/HistoryPage.xaml`
- [x] **T027** [P] [US2] Accept `history` as a startup page value and keep the existing three · `App.xaml.cs`
- [x] **T028** [P] [US2] Add `history` to the accepted page values · `tools/refresh-and-run.ps1`

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2:**

- [x] **T029** [US2] Wire the page to the shell, rebuild the view model on navigation, and keep the selected pair across a rebuild · `Views/HistoryPage.xaml.cs`

**Checkpoint**: The comparison is reachable from the fourth navigation item and
from startup, and it is honest with zero, one or many readings.

---

## Phase 5: User Story 3 - Trust the advice (Priority: P3)

**Goal**: The advice engine is exercised without the window, and five named cases
are asserted.

**Independent Test**: Change one expectation, watch the suite fail, restore it and
watch it pass.

**Files**: `SystemCompanion.Tests/Fixtures/`, `SystemCompanion.Tests/TestSupport.cs`,
`SystemCompanion.Tests/RecommendationEngineTests.cs`,
`SystemCompanion.Tests/HistoryServiceTests.cs`,
`SystemCompanion.Tests/FirmwareReferenceTests.cs`

### Tests

> Write these to fail first where the behaviour is missing.

**Wave 1 — independent (different files):**

- [x] **T030** [P] [US3] Write the five written readings as fixtures, each a complete snapshot with only the relevant values differing · `SystemCompanion.Tests/Fixtures/`
- [x] **T031** [P] [US3] Add a fixture loader and a builder for the BIOS value dictionary · `SystemCompanion.Tests/TestSupport.cs`

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 — independent (different files):**

- [x] **T032** [P] [US3] Assert the five named cases: `Balanced` power plan is worth doing, long paths enabled is already good, four sticks at 6000 is not flagged, memory integrity is flagged with its trade-off text, and no graphics adapter neither crashes nor invents a verdict · `SystemCompanion.Tests/RecommendationEngineTests.cs`
- [x] **T033** [P] [US3] Assert ordering by recorded time, the counter suffix on a same-minute scan, an unreadable entry being skipped, and one changed value reported as a sentence · `SystemCompanion.Tests/HistoryServiceTests.cs`
- [x] **T034** [P] [US3] Assert a matching tag passes, a different tag fails, and a missing tag fails · `SystemCompanion.Tests/FirmwareReferenceTests.cs`

**Checkpoint**: The advice engine is covered and the suite runs offline.

---

## Phase 6: User Story 4 - Take a fresh reading without restarting (Priority: P4)

**Goal**: One explicit rescan action, visible on every screen, with progress and no
overlapping scans.

**Independent Test**: With the app open, select the action twice quickly; one scan
runs and the values refresh.

**Files**: `Views/Controls/RescanBar.xaml`, `Views/Controls/RescanBar.xaml.cs`

### Implementation

**Wave 1:**

- [x] **T035** [US4] Build the rescan bar: a button that binds to the shell command, shows progress while running and is unavailable while a scan is in flight · `Views/Controls/RescanBar.xaml`

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2:**

- [x] **T036** [US4] Bind the bar to the shell from its host and report a failed scan in a plain sentence · `Views/Controls/RescanBar.xaml.cs`

**⟶ Wait for Wave 2 to finish, then:**

**Wave 3:**

- [x] **T037** [US4] Verify no scan runs unattended and that a failure leaves the previous values on screen

**Checkpoint**: A reading can be taken from any screen without restarting.

---

## Phase 7: User Story 5 - Know when the firmware does not match (Priority: P5)

**Goal**: A mismatched or missing firmware tag produces a warning, and reference
values are not presented as decoded readings.

**Independent Test**: Edit a copy of the profile's tag and confirm the warning names
both values.

**Files**: `ViewModels/BiosNowViewModel.cs`, `Views/BiosNowPage.xaml`,
`Views/BiosNowPage.xaml.cs`

### Implementation

**Wave 1 — independent (different files):**

- [x] **T038** [P] [US5] Expose a warning line from the view model when the profile tag differs from the supported tag or is missing, naming both values · `ViewModels/BiosNowViewModel.cs`
- [x] **T039** [P] [US5] Show the warning above the headline facts, in the same card style the screen already uses · `Views/BiosNowPage.xaml`

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2:**

- [x] **T040** [US5] Pass the loaded profile through to the view model so the warning is computed on navigation · `Views/BiosNowPage.xaml.cs`

**Checkpoint**: A machine with other firmware is told so, and a matching machine
sees nothing.

---

## Phase 8: Polish

**Purpose**: Cross-cutting validation against the Success Criteria.

**Wave 1 — independent (different files):**

- [x] **T041** [P] Build the solution and fix every warning from project code
- [x] **T042** [P] Run the test suite and fix every failure

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 — independent (different files):**

- [x] **T043** [P] Walk the quickstart checks for all four screens and both firmware cases, and record the result
- [x] **T044** [P] Update the screen table and folder map to include the fourth screen, the history folder and the two new projects · `docs/README.md`, `PROMPT-NEXT-STEPS.md`
- [x] **T045** [P] Remove the temporary Sync Impact Report from the ratified constitution · `.specify/memory/constitution.md`

---

## Dependencies & Execution Order

Phases run in order: Setup → Foundational → US1 → US2 → US3 → US4 → US5 → Polish.

- **Setup**: Wave 1 (T001, T002) blocks Wave 2 (T003, T004, T005).
- **Foundational**: Wave 1 (T006 to T012) blocks Wave 2 (T013, T014), which blocks
  Wave 3 (T015, T016), which blocks Wave 4 (T017, T018), which blocks Wave 5
  (T019, T020).
- **US1**: T021 blocks the three verification tasks.
- **US2**: Wave 1 (T025 to T028) blocks T029.
- **US3**: fixtures (T030, T031) block the three test files.
- **US4**: T035 blocks T036, which blocks the check in T037.
- **US5**: T038 and T039 block T040.
- **Polish**: the build and the suite block the three checks and doc updates.

Story independence: US2 needs the Foundational shell and history service, nothing
from US3, US4 or US5. US4 needs the shell rescan command only. US5 needs the
firmware constant only. US3 needs the Core library only, so it can run any time
after Foundational.

## Parallel Opportunities

Within Foundational Wave 1, seven files are independent. Within the US3 test wave,
three files are independent. Within each story phase, the marked `[P]` tasks touch
different files and can be built in any order.
