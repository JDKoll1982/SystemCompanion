# System Companion

A small Windows program that explains your computer in ordinary words.

It has three screens:

| Screen | What it tells you |
| --- | --- |
| **BIOS — right now** | What the motherboard's saved settings file says, translated out of firmware jargon. |
| **Windows — right now** | What hardware is installed and which Windows switches are on. |
| **What could be better** | Concrete changes that would make the machine better at programming, gaming and AI work — each with why, how, and how risky it is. |

Nothing in the app changes your computer. It only reads.

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
```

## Where the numbers come from

Two small files feed the app. Both live in `Data\`.

| File | Filled in by | Contains |
| --- | --- | --- |
| `Data\current\system-snapshot.json` | `tools\discover-system.ps1` | Hardware and Windows settings, read fresh from this PC |
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
  Views\                         the three screens
  ViewModels\                    what each screen shows
  Models\                        the shape of the data
  Services\                      reading the data, and the advice engine
  Styles\Theme.xaml              colours, fonts, card styles
  Data\                          the readings and the BIOS files
  tools\                         the scripts that produce the data
  docs\                          notes written for humans
  specs\                         feature notes for future work
  PROMPT-NEXT-STEPS.md           where development should go next
```

## A note about the words used

Everything the app shows to you is written so that someone who has never opened
a BIOS screen can follow it. The technical detail still exists in `Data\bios\`
(the original settings file, the decoded report and the raw screenshots), but
you never have to look at it to use the app.
