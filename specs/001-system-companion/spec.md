# Feature Specification: System Companion

**Feature Branch**: `001-system-companion`

**Created**: 2026-10-17

**Status**: Draft

**Input**: User description: "System Companion — a read-only Windows program that explains a computer in plain language across three screens, plus the remaining work described in PROMPT-NEXT-STEPS.md"

## User Scenarios & Testing (mandatory)

### User Story 1 - Understand the machine as it is today (Priority: P1)

A person opens the app and reads three screens. The first shows what the motherboard's saved settings file says, translated out of firmware jargon. The second shows the parts and Windows switches that decide how fast the machine feels. The third lists changes that would actually help, each with what it is worth, how to do it, and how risky it is. Nothing on any screen is a raw hexadecimal value or an unexplained abbreviation, and a person who has never opened a BIOS screen can follow all three.

**Why this priority**: This is the product. Without it there is nothing to extend, and every later story depends on the same readings and the same plain-language rule.

**Independent Test**: Launch the app with no network available. Walk each of the three screens and confirm every line shows a real reading plus a sentence explaining it, and that at least one advice card states what is already good rather than only what is wrong.

**Acceptance Scenarios**:

1. **Given** a machine with a readable settings file and a completed hardware scan, **When** the person opens the BIOS screen, **Then** the board name, firmware version and save date are shown, four headline facts appear, and every detail line carries a name, a value and one sentence of explanation.
2. **Given** the same machine, **When** the person opens the Windows screen, **Then** processor, memory, graphics and Windows build are shown as headline facts, and the grouped detail covers storage, speed settings, startup programs and developer tools.
3. **Given** the same machine, **When** the person opens the advice screen, **Then** a summary counts how many suggestions are worth doing, optional, need a manual check, and already correct, and each card shows current value, target value, why it helps, how to do it, expected benefit and risk level.
4. **Given** a machine that is already configured well, **When** the person opens the advice screen, **Then** the things that are already right are reported as already correct rather than silently omitted.
5. **Given** any screen, **When** a value cannot be read, **Then** the screen shows `Unknown` for that value and does not replace it with a plausible number.

---

### User Story 2 - See whether a change helped (Priority: P2)

The app can take a reading on demand but cannot say whether anything improved, because each scan overwrites the last one. The person keeps every reading instead, then compares any two of them and reads the differences as sentences: what got better, what got worse, what stayed the same, and when each reading was taken. The app also states, without being asked, how many things have changed since the person last looked, and that statement leads to the comparison.

**Why this priority**: The person's real question is "did my change help?". Answering it is what makes the app worth opening a second time, and it is the only story in this list that turns a one-off report into a tool.

**Independent Test**: Take two readings that differ only in memory speed. Open the comparison and confirm exactly one change is reported, in a sentence, and that the screen also works with a single reading present.

**Acceptance Scenarios**:

1. **Given** a completed scan, **When** the scan finishes, **Then** a copy of that reading is kept as a dated history entry and the current reading is left in place unchanged.
2. **Given** two history entries, **When** the person opens the comparison, **Then** the two newest are compared by default, and each of the two can be swapped for any other entry.
3. **Given** two entries that differ only in memory speed, **When** the comparison is shown, **Then** exactly one change is listed, in a sentence such as "Memory speed went from 4800 to 6000 MT/s, which is a real improvement", and every other value is listed as unchanged.
4. **Given** exactly one history entry, **When** the person opens the comparison, **Then** the screen explains in plain words that a second reading is needed and does not show an empty table.
5. **Given** two history entries with identical readings, **When** the comparison is shown, **Then** the screen states that nothing changed.
6. **Given** at least two history entries that differ, **When** the person opens the app, **Then** the shell states how many things changed since the previous reading, and selecting it opens the comparison.
7. **Given** a history entry that cannot be read or is incomplete, **When** the comparison is built, **Then** that entry is skipped and the screen says which reading was skipped, rather than failing.
8. **Given** the person has never taken a second reading, **When** the app is opened, **Then** no change statement is shown and no error is produced.

---

### User Story 3 - Trust the advice (Priority: P3)

The advice is produced by one component that decides what is worth doing. Today the only way to check it is to look at the screen. The person who maintains the app supplies written readings with known answers and confirms the component reports the expected verdict for each, so a later change cannot quietly make the advice wrong.

**Why this priority**: A wrong suggestion looks exactly like a right one until somebody acts on it. This story is the guard that makes every other advice change safe to ship.

**Independent Test**: Change a known-bad expectation in the fixtures and confirm the suite fails, then restore it and confirm it passes, without launching the app.

**Acceptance Scenarios**:

1. **Given** a written reading whose power plan is `Balanced`, **When** the advice is built, **Then** the power plan suggestion is reported as worth doing.
2. **Given** a written reading with long path support enabled, **When** the advice is built, **Then** the long path suggestion is reported as already good.
3. **Given** a written reading with four memory sticks running at 6000 MT/s, **When** the advice is built, **Then** the memory suggestion is not flagged as worth doing.
4. **Given** a written reading with memory integrity enabled, **When** the advice is built, **Then** the security trade-off suggestion is reported as worth doing and its risk text says it lowers protection.
5. **Given** a written reading with no graphics adapter, **When** the advice is built, **Then** no failure occurs and the graphics and AI suggestions degrade to an explanation rather than a wrong verdict.
6. **Given** the suite is run on a machine with no network, **When** it executes, **Then** it passes.

---

### User Story 4 - Take a fresh reading without restarting (Priority: P4)

The readings are captured once when the app starts. After the person changes something, they want the app to re-read the machine on demand, with visible progress, and without closing it. The scan never starts on its own.

**Why this priority**: Without it, checking a change means closing and reopening the app, which makes the comparison story awkward to use in practice.

**Independent Test**: With the app open, start a scan from a page, confirm the button shows progress, then confirm the page shows the new values once the scan finishes.

**Acceptance Scenarios**:

1. **Given** the app is open, **When** the person selects the rescan action on a page, **Then** the scan starts, the action shows that it is running, and the person can see that it has finished.
2. **Given** a scan has finished, **When** the page refreshes, **Then** every visible value reflects the new reading and a new history entry exists.
3. **Given** the app is idle, **When** no rescan action has been selected, **Then** no scan runs in the background.
4. **Given** the scan fails because the tool is missing or the machine refuses the query, **When** the action completes, **Then** the page keeps the previous values and says in plain words that the reading could not be refreshed.
5. **Given** a scan is already running, **When** the action is selected again, **Then** a second scan does not start and the action remains unavailable until the first finishes.

---

### User Story 5 - Know when the firmware does not match (Priority: P5)

The BIOS explanations are tied to one firmware version. When the app is used on a machine with different firmware, the setting names no longer line up with the offsets they were decoded from. The person is told clearly that the explanation does not apply, instead of being shown confident wrong names.

**Why this priority**: This is a correctness guard for a wider audience. It matters, but only once the app is used beyond the one machine it was built on.

**Independent Test**: Supply a settings file whose firmware tag differs from the reference and confirm the BIOS screen shows a warning and does not present reference names as if they were decoded from that file.

**Acceptance Scenarios**:

1. **Given** a settings file whose firmware tag matches the reference, **When** the BIOS screen is shown, **Then** no warning appears.
2. **Given** a settings file whose firmware tag does not match the reference, **When** the BIOS screen is shown, **Then** a plain-language warning names both the file's firmware and the supported firmware, and the screen does not present reference values as decoded readings.
3. **Given** a settings file with no firmware tag at all, **When** the BIOS screen is shown, **Then** the screen reports the tag as `Unknown` and shows the same warning rather than assuming a match.

### Edge Cases

- A reading is taken twice within the same minute, so two history files would share a name.
- The scan is interrupted, leaving a partial or unreadable history file.
- The system clock moves backwards between readings, so file order and timestamp order disagree.
- A history folder exists but is empty.
- The hardware scan succeeds while the BIOS summary fails, or the reverse.
- The advice component is given a reading with sections missing entirely, not merely empty.
- A suggestion serves two goals and would otherwise be listed twice.
- The display reports a virtual adapter but no physical graphics card.
- The settings file's firmware tag matches but its size or checksum does not.

## Requirements (mandatory)

### Functional Requirements

**Reading and display**

- **FR-001**: The app MUST read all displayed values from the stored reading files and MUST NOT query a network service.
- **FR-002**: The app MUST NOT write to the operating system, the firmware, a device, or the user's original settings file.
- **FR-003**: Every user-visible value MUST be accompanied by one plain-language sentence explaining what it means.
- **FR-004**: When a value is absent, unreadable, or not applicable, the app MUST show `Unknown` for it rather than a substituted value.
- **FR-005**: The BIOS screen MUST show the board, the firmware version, the save date, four headline facts, and grouped detail with a marker when a value differs from the factory default.
- **FR-006**: The Windows screen MUST show processor, memory, graphics and Windows build as headline facts, plus grouped detail for storage, speed settings, version, startup programs and developer tools.
- **FR-007**: The advice screen MUST show a summary count for worth doing, optional, needs a manual check, and already correct.
- **FR-008**: The advice screen MUST offer the filters Programming, Gaming, AI work and Everyday.
- **FR-009**: Each advice card MUST show status, goal, title, one-line summary, current value, target value, why it helps, how to do it, expected benefit, risk level, and whether a restart is needed.
- **FR-010**: A suggestion that serves more than one goal MUST appear once, and MUST remain visible under every goal it serves.

**History and comparison**

- **FR-011**: Every completed scan MUST be kept as a separate dated entry, and the most recent reading MUST remain in its existing location.
- **FR-012**: History entries MUST be ordered by the reading they contain, and an unreadable entry MUST be skipped with a plain-language note rather than aborting the comparison.
- **FR-013**: The comparison MUST default to the two most recent entries and MUST allow either side to be replaced by any other entry.
- **FR-014**: The comparison MUST report each changed value as a sentence naming the before value, the after value and whether the change is an improvement or a decline.
- **FR-015**: The comparison MUST report values that did not change as unchanged.
- **FR-016**: With fewer than two entries, the comparison MUST explain that a second reading is needed instead of showing an empty table.
- **FR-017**: The shell MUST state how many values changed since the previous reading whenever at least two readings exist and they differ, and MUST state nothing when they are equal or only one reading exists.
- **FR-018**: The change statement MUST lead to the comparison when selected.
- **FR-019**: The comparison MUST be reachable as a fourth screen and MUST be selectable at startup by name.

**Advice correctness**

- **FR-020**: The advice MUST be produced by a component that can be exercised without launching the user interface.
- **FR-021**: An automated test suite MUST cover, at minimum, a `Balanced` power plan, long path support enabled, four memory sticks at 6000 MT/s, memory integrity enabled, and a reading with no graphics adapter.
- **FR-022**: Each suggestion MUST trace to at least one value present in the reading, and MUST NOT be produced when that value is missing.
- **FR-023**: The advice component MUST NOT depend on the user interface framework or on the location of the reading files at run time.

**Refresh**

- **FR-024**: Each screen that shows a reading MUST offer an explicit action to take a new reading.
- **FR-025**: Taking a new reading MUST NOT happen without the person asking for it.
- **FR-026**: The action MUST show that a reading is in progress, MUST prevent a second reading starting at the same time, and MUST show when it has finished.
- **FR-027**: A new reading MUST replace the displayed values and add a history entry, or, on failure, leave the displayed values and previous readings untouched and state that the refresh failed.

**Firmware match**

- **FR-028**: The app MUST compare the settings file's firmware tag against the firmware the BIOS explanations were built from.
- **FR-029**: When the tags differ, or the tag is missing, the app MUST show a warning naming both values and MUST NOT present reference-derived names as decoded readings from that file.
- **FR-030**: When the tags match, the app MUST NOT show that warning.

### Key Entities

- **Reading**: one capture of the machine at a point in time. Records when it was taken, identity, processor, memory, graphics, storage, Windows, performance, developer tools, AI tools and storage volumes. One reading is the current one; every capture is also kept as a history entry.
- **History entry**: a dated copy of a reading, with the date it was taken and the location it is stored at. Distinguished from other entries by when it was taken.
- **Comparison**: the result of pairing two history entries. Holds changed values, each with an area, a name, a before value, an after value and a direction; and unchanged values.
- **Advice suggestion**: a recommendation. Records an identifier, status, goal or goals, title, one-line summary, current value, target value, why it helps, how to do it, expected benefit, risk level, and whether a restart is needed. Status is one of worth doing, optional, needs a manual check, or already correct.
- **BIOS profile**: the plain-language translation of the settings file, with the file's identity, headline facts, grouped sections and per-setting explanations, including whether a value differs from the factory default.
- **Firmware reference**: the specific firmware the BIOS explanations were derived from, identified by its tag. A profile whose file tag differs from this is not explained from it.

## Success Criteria (mandatory)

### Measurable Outcomes

- **SC-001**: A person who has never opened a BIOS screen can read all four screens and identify at least one action to take, without asking for help.
- **SC-002**: Every suggestion on the advice screen can be traced to a named value on the BIOS or Windows screen.
- **SC-003**: After a change is made outside the app, one refresh shows the new value and the comparison reports that value as improved or declined, with no code change.
- **SC-004**: With two readings that differ in exactly one value, the comparison reports exactly one changed row and at least one unchanged row.
- **SC-005**: The automated advice suite covers the five named cases and passes with no network available.
- **SC-006**: A build of the application reports zero warnings from project code.
- **SC-007**: A machine with firmware other than the supported tag sees the mismatch warning on the BIOS screen 100 percent of the time, and a machine with the supported tag sees it never.
- **SC-008**: The app opens and shows real readings on a machine with no network, no account and no telemetry.

## Assumptions

- The three existing screens already work as described in User Story 1. This work verifies them and does not rewrite them.
- The comparison screen is the fourth screen, and the change statement lives in the shell so it is visible from any screen.
- History is kept as one file per reading in a dedicated folder beside the existing reading files, and no pruning or retention limit is added in this scope.
- The advice component moves, or is made reachable, so a separate test project can exercise it without starting the window. Which of the two shapes is chosen is a planning decision.
- The advice test suite runs on the same Windows development machine and does not need a hosted build agent.
- The BIOS warning is driven by the firmware tag in the profile; a matching tag with a mismatched checksum is reported as a data-quality warning, not as a firmware mismatch.
- Startup page selection keeps its current behaviour: a name selects a screen, and an unrecognised name falls back to the BIOS screen.

## Verbatim Constraints

- The fourth screen's startup selector value is `history`.
- The existing selector values remain `bios`, `windows` and `improve`.
- History entries are stored under `Data/history/` with the file name pattern `scan-YYYY-MM-DD-HHmm.json`.
- The current reading stays at `Data/current/system-snapshot.json`.
- The unreadable-value text is exactly `Unknown`.
- The advice identifiers asserted by the test suite are `power-plan`, `long-paths` and `memory-integrity`.
- The supported firmware tag is `E7E06IMS.AK0`.
