# CLI and route contract

## Startup page selector

`App.xaml.cs` reads the first command line argument in `OnLaunched`. With no argument the start page is `bios`.

The accepted selector values are `history`, `bios`, `windows` and `improve`.

`history` selects the fourth screen, the comparison. It is new in this feature.

`bios`, `windows` and `improve` keep their current meaning and their current spelling.

Matching is case insensitive. An unrecognised value falls back to the BIOS screen and produces no error.

The selector reaches `MainWindow(string startPage = "bios")`, which maps it to the matching navigation index. The existing alias `improvements` for the advice screen is still accepted.

## Navigation tags

Each `NavigationViewItem` in `MainWindow.xaml` carries its route in `Tag`. The tags are `bios`, `windows`, `improve` and `history`.

The `history` item is the fourth item in the menu. Selecting it navigates the content frame to the history page with the shell as its parameter.

## Shell change statement

The shell shows one short line when at least two readable readings exist and they differ, for example `3 things changed since your last scan`. The count is the number of changed values in the comparison of the two newest entries.

The line is hidden when the two newest entries are equal or when fewer than two entries exist. Selecting the line opens the `history` route.

## Rescan

A page rescan action calls `ShellViewModel.RescanAsync()`. The shell starts the scan with `ScanRunner.RunAsync()`.

The runner starts `powershell.exe` with `-NoProfile -ExecutionPolicy Bypass -File tools/discover-system.ps1`, with no shell execute and no window, and awaits process exit.

The action is disabled while a scan runs, so a second scan cannot start at the same time. The action shows that a scan is running and shows when it finished.

The scan never starts without the person selecting the action. On success the shell reloads `Data/current/system-snapshot.json` and re-reads `Data/history/`. On failure it keeps the displayed values, leaves history untouched, and reports the failure in one plain sentence.

## Paths

All file paths in the app resolve through `Services/AppData.cs`. The library does not resolve paths and never assumes where files live.

The two contracts a caller passes to the library are a parsed `SystemSnapshot` plus `BiosProfile`, and a directory path for history.
