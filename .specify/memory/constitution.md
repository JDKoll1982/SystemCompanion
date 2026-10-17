<!--
Sync Impact Report
==================
Version change: unratified template → 1.0.0
Ratification: first adoption. There is no prior ratified version, so no
backward-compatibility comparison applies.
Modified principles: none (first ratification).
Added principles:
  - I. Read-only by construction
  - II. Plain language is a functional requirement
  - III. Evidence before claims
  - IV. Verifiable advice
  - V. No new frameworks
Added sections:
  - Technical Constraints
  - Development Workflow & Quality Gates
  - Governance
Removed sections: none.
Follow-up TODOs: none. Every placeholder was filled; no token was deferred.
-->

# System Companion Constitution

## Core Principles

### I. Read-only by construction

The app MUST NOT write to the operating system, the firmware, any device, or the
user's BIOS profile at `Data/bios/MsOcFile.ocb`. Every reading MUST come from a
read-only query. Advice MUST be presented as text the person carries out
themselves; the app MUST NOT execute a settings change on their behalf.

Rationale: the app exists to tell someone whether their machine is set up well. A
write path turns a diagnostic tool into a risk, and a diagnostic that can change
what it measures cannot be trusted to report it.

### II. Plain language is a functional requirement

Every user-visible string MUST be understandable to someone who has never opened
a BIOS screen. The interface MUST NOT show unexplained abbreviations, raw
hexadecimal values, or internal property names. Where a technical term is
unavoidable, a sentence or tooltip MUST explain it. When a value cannot be read,
the interface MUST show `Unknown` and MUST NOT substitute a plausible-looking
number.

Rationale: a screen the reader cannot follow is a failed screen. Full marks on
the reading and zero on the explanation is a failure of this principle, not a
presentation detail.

### III. Evidence before claims

Every suggestion MUST trace to at least one reading in
`Data/current/system-snapshot.json` or `Data/bios/screenshot_check.json`. A
suggestion MUST NOT be emitted when its supporting reading is absent, unreadable,
or unknown. `Data/bios/` is a verified audit trail and MUST change only by
re-running the scripts in `Data/bios/tools/`; hand-editing those files is
prohibited. `Data/bios/MsOcFile.ocb` MUST NEVER be written.

Rationale: advice that cannot be traced cannot be checked, and an audit trail
edited by hand can no longer be re-verified against the original firmware image.

### IV. Verifiable advice

`Services/RecommendationEngine.cs` MUST be covered by automated tests that supply
hand-written snapshots and assert the resulting suggestion states. The suite MUST
cover at least: a `Balanced` power plan, `longPaths` enabled, four DIMMs running at
6000 MT/s, `memoryIntegrity` enabled, and a snapshot with no graphics adapter. A
behaviour change to the engine MUST land with a test that fails before the change
and passes after it.

Rationale: the engine is the part most likely to be quietly wrong, and a wrong
suggestion looks exactly like a right one until a person acts on it.

### V. No new frameworks

The app MUST remain a WinUI 3 application built on `Microsoft.WindowsAppSDK`,
running unpackaged with `WindowsPackageType` set to `None`. The project MUST NOT
add a third-party UI framework, an MVVM library, or a dependency-injection
container. Change notification MUST continue to use `ViewModels/ObservableBase.cs`
rather than a replacement. The app MUST NOT make network calls, collect telemetry,
or require an account.

Rationale: a small dependency surface is what keeps an offline diagnostic tool
auditable and runnable on a machine with no network.

## Technical Constraints

- Target framework is `net10.0-windows10.0.19041.0`; supported platforms are x86,
  x64, and ARM64.
- Every file path MUST resolve through `Services/AppData.cs`. Literal paths in
  code are prohibited because they break the unpackaged `bin` and project-root
  layouts.
- Three data contracts are non-negotiable:
  `Data/current/system-snapshot.json`, `Data/bios/bios-profile.json`, and
  `Data/bios/screenshot_check.json`. Renaming a key requires updating the model
  and the producing script in the same change.
- Only the three small JSON files the app reads at start-up are copied to the
  output folder. The large archive material (the original profile, the decoded
  report, the screenshots) stays in the project.
- The BIOS reference data is tied to firmware `E7E06IMS.AK0`. When a profile's
  firmware tag does not match the reference, the app MUST warn and MUST NOT
  display reference names against unknown offsets.
- A build MUST report zero warnings from project code.

## Development Workflow & Quality Gates

- Every feature MUST pass through the spec-kit pipeline: specify, plan, tasks,
  then implement. The Companion extension records each step in
  `.spec-context.json`.
- A plan MUST pass its Constitution Check. A violation MUST be recorded in
  Complexity Tracking together with the simpler alternative that was rejected;
  an unjustified violation blocks the plan.
- `.spec-context.json` is written only by the Companion scripts. Hand-editing it
  is prohibited, because the timing and status history is append-only and cannot
  be repaired afterwards.
- Verification of a change MUST include `tools/refresh-and-run.ps1` and a visual
  check of every affected page, confirming real values rather than `Unknown` or a
  placeholder.
- `dotnet build SystemCompanion.csproj -c Debug` MUST report no warnings from
  project code before a change is called done.
- New user-visible text MUST be reviewed against Principle II before it is merged.

## Governance

This constitution supersedes `PROMPT-NEXT-STEPS.md`, `docs/`, and ad-hoc
instructions wherever they disagree. Those documents inform the work; they do not
override these principles.

Amendments are made only through the `speckit-constitution` command. The Sync
Impact Report it produces MUST be reviewed and then removed before the amendment
is committed.

Versioning follows semantic versioning. MAJOR applies to removing or redefining a
principle. MINOR applies to adding a principle or materially expanding guidance.
PATCH applies to clarification that does not change meaning.

Compliance review is mandatory. Every plan MUST include a Constitution Check, and
every change review MUST verify all five principles. Unjustified complexity is a
blocking finding.

**Version**: 1.0.0 | **Ratified**: 2026-10-17 | **Last Amended**: 2026-10-17
