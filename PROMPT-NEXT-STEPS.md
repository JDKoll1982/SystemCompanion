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

## The very next move

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

Visual check: launch with each of `bios`, `windows`, `improve` and confirm the
page header text, the headline tiles and at least one detail card render with
real values (not `Unknown`, not `—`).

---

## Background you may need

- The BIOS profile format, checksum rule, record layout and the reference data
  pipeline are documented in `Data/bios/README-format-notes.md`. Read it before
  touching anything BIOS-related.
- The machine's current readings and what they mean are in
  `docs/What-I-found.md`.
- The requirements, including what is explicitly out of scope, are in
  `specs/001-system-companion/README.md`.
