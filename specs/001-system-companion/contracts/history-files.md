# History file contract

## Location and name

History entries are stored under `Data/history/` with the file name pattern `scan-YYYY-MM-DD-HHmm.json`.

The pattern uses the local time of the scan. One file holds one reading. The current reading stays at `Data/current/system-snapshot.json` and is never moved or replaced by a history write.

When a second scan lands in the same minute, the first file keeps the pinned name. Later files append a counter before the extension, for example `scan-2026-10-17-1340-2.json`, then `-3`, and so on. This is the only allowed deviation from the pattern.

## Shape

A history entry has the same shape as a snapshot, so it can be read with the same model. See `data-files.md` for the full shape.

```json
{
  "generatedAt": "2026-10-17T13:40:11.4820000-05:00",
  "generatedLocal": "2026-10-17 13:40",
  "computerName": "STUDIO",
  "memory": { "configuredMt": 4800, "moduleCount": 2 }
}
```

## Writer

`tools/discover-system.ps1` is the only writer. It writes the history copy immediately after it writes `Data/current/system-snapshot.json`.

This covers the three ways a reading is taken: the script run by a refresh, the script run by the in app rescan, and the script run by hand. The app must not copy history files.

## Ordering

Entries are ordered by the `generatedLocal` value parsed from inside the file. The parsed value becomes `TakenAt`.

File names are for display only and are never used for ordering. A clock change or a counter suffix can make name order and reading order disagree, so name order must not be trusted.

## Unreadable entries

An entry that cannot be read, cannot be parsed, or has no usable `generatedLocal` is skipped. Skipping is not a failure.

The skipped entry is named in a plain language note so the person can see which reading was left out. A comparison continues with the remaining entries.

## Fewer than two entries

Zero entries or one entry is not an error. The comparison screen explains in plain words that a second reading is needed and shows no empty table. The shell change statement stays hidden.
