# System Companion

A small Windows program that explains your computer in ordinary words.

It has four screens:

| Screen | What it tells you |
| --- | --- |
| **BIOS — right now** | What the motherboard's saved settings file says, translated out of firmware jargon. |
| **Windows — right now** | What hardware is installed and which Windows switches are on. |
| **What could be better** | Concrete changes that would make the machine better at programming, gaming and AI work — each with why, how, and how risky it is. |
| **What changed** | Any two kept readings compared, value by value, in a sentence each. It is also where the shell's "3 things changed since your last scan" line leads. |

Nothing in the app changes your computer. It only reads.

A **Take a new reading** button sits in the window's title strip, so a fresh scan is
reachable from every screen without restarting the app.

---

## Running it

You need the .NET 10 SDK (already installed on this machine).

```powershell
cd C:\Users\johnk\source\repos\SystemCompanion
dotnet build SystemCompanion.csproj -c Debug
.\bin\Debug\net10.0-windows10.0.19041.0\SystemCompanion.exe
```

Optional: start on a particular screen.

```powershell
.\SystemCompanion.exe windows
.\SystemCompanion.exe improve
.\SystemCompanion.exe history
```

## Where the numbers come from

Three small files feed the app. All live in `Data\`.

| File | Filled in by | Contains |
| --- | --- | --- |
| `Data\current\system-snapshot.json` | `tools\discover-system.ps1` | Hardware and Windows settings, read fresh from this PC |
| `Data\history\scan-YYYY-MM-DD-HHmm.json` | `tools\discover-system.ps1` | One dated copy of every reading, so two can be compared |
| `Data\bios\bios-profile.json` | `tools\build-bios-summary.py` | Your BIOS settings, rewritten in plain language |

To refresh both and then start the app:

```powershell
.\tools\refresh-and-run.ps1
```

That is the only command you normally need. It re-scans the PC, rebuilds the
plain-language BIOS summary, compiles, and launches.

## A tour of the folders

```
SystemCompanion\
  App.xaml, MainWindow.xaml      the window frame and navigation
  Views\                         the four screens
  Views\Controls\RescanBar.xaml  the one rescan action, hosted by the window
  ViewModels\                    what each screen shows
  Models\Display.cs              the display types and the Unknown rule
  Services\                      reading the data, running a scan
  Styles\Theme.xaml              colours, fonts, card styles
  Data\                          the readings, the BIOS files and the history folder
  tools\                         the scripts that produce the data
  docs\                          notes written for humans
  specs\                         feature notes for future work
  PROMPT-NEXT-STEPS.md           where development should go next

SystemCompanion.Core\            class library: models, advice engine, history service
SystemCompanion.Tests\           xUnit suite with written readings under Fixtures\
```

The library holds everything that does not need a window, so the advice engine and
the comparison are tested without starting the app. The app project keeps the views,
the view models and the WinUI display types.

## A note about the words used

Everything the app shows to you is written so that someone who has never opened
a BIOS screen can follow it. The technical detail still exists in `Data\bios\`
(the original settings file, the decoded report and the raw screenshots), but
you never have to look at it to use the app.
