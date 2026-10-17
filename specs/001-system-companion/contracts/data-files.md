# Data file contracts

The app reads every displayed value from files under `Data/`. No file in this contract is written by the app. Keys are lower camel case. Readers must tolerate unknown keys, comments, and trailing commas.

## Data/current/system-snapshot.json

This is the current reading. Its shape mirrors `Models/SystemSnapshot.cs`. The scan script writes it and nothing else does.

Top-level keys are `generatedAt`, `generatedLocal`, `computerName`, `userName`, `identity`, `cpu`, `memory`, `graphics`, `storage`, `windows`, `performance`, `tooling`, `aiTools`, `dotnetSdks`, `wslDistros`.

The example below is abridged inside nested objects but every top-level key is present.

```json
{
  "generatedAt": "2026-10-17T13:40:11.4820000-05:00",
  "generatedLocal": "2026-10-17 13:40",
  "computerName": "STUDIO", "userName": "johnk",
  "identity": { "manufacturer": "MSI", "model": "MS-7E06", "board": "PRO Z790-A WIFI", "boardVersion": "1.0", "biosVendor": "AMI", "biosVersion": "E7E06IMS.AK0", "biosDate": "2024-01-10" },
  "cpu": { "name": "Intel Core i7-14700K", "cores": 20, "threads": 28, "baseMhz": 3400, "l3CacheMb": 33 },
  "memory": { "installedGb": 64, "visibleGb": 63.8, "moduleCount": 2, "configuredMt": 4800, "ratedMt": 6000, "modules": [] },
  "graphics": { "adapters": [], "nvidia": [] }, "storage": { "drives": [], "volumes": [] },
  "windows": { "edition": "Windows 11 Pro", "version": "10.0.22631", "build": "22631", "displayVersion": "23H2", "installDate": "2024-02-01", "lastBootHours": 12.5, "systemDrive": "C:", "longPaths": false, "pageFile": { "automatic": true, "files": [] }, "startupItems": [] },
  "performance": { "powerPlan": "Balanced", "gameMode": true, "gameDvrEnabled": false, "hardwareScheduling": true, "hardwareSchedulingRaw": 2, "memoryIntegrity": true, "vbsConfigured": true, "hypervisorLaunch": "Auto", "defender": { "realtimeDisabled": false, "exclusionCount": 0, "exclusions": [] } },
  "tooling": { "dotnet": "9.0.100", "git": "2.47.0", "node": null },
  "aiTools": { "lmStudio": false, "ollamaDataDir": false },
  "dotnetSdks": ["9.0.100"], "wslDistros": []
}
```

`generatedLocal` is the recorded time of the reading, formatted `yyyy-MM-dd HH:mm`. It is the ordering key for history.

Missing or unreadable nested blocks are allowed. A value that is absent, unreadable, or not applicable is displayed as exactly `Unknown`. A plausible substitute must never be shown.

## Data/bios/bios-profile.json

This is the plain language translation of the saved BIOS settings file. Its shape mirrors `Models/BiosProfile.cs`.

Top-level keys are `generatedAt`, `file`, `headline`, `sections`.

`file` carries `name`, `board`, `biosVersion`, `savedOn`, `savedOnDay`, `valid`, `checksum`, `sizeBytes`, `savedVariables`, `changedBytes`, `cpu`.

Each `headline` row carries `label`, `value`, `plain`, `state`. Each `sections` row carries `title`, `subtitle`, `icon`, `items`. Each item carries `name`, `value`, `plain`, `state`, `changed`.

`state` is `good`, `warn`, or `neutral`. `changed` marks a value that differs from the factory default.

```json
{
  "generatedAt": "2026-10-17T13:40:11",
  "file": { "name": "MsOcFile.ocb", "board": "PRO Z790-A WIFI", "biosVersion": "E7E06IMS.AK0", "savedOn": "2024-05-02 18:20", "savedOnDay": "2024-05-02", "valid": true, "checksum": "0x8A3F", "sizeBytes": 4096, "savedVariables": 412, "changedBytes": 7, "cpu": "Intel Core i7-14700K" },
  "headline": [ { "label": "Memory speed", "value": "4800 MT/s", "plain": "Memory runs at its safe base speed.", "state": "warn" } ],
  "sections": [
    { "title": "Memory", "subtitle": "How fast the memory runs", "icon": "\uE9D9",
      "items": [ { "name": "XMP", "value": "Disabled", "plain": "The board uses the safe profile.", "state": "warn", "changed": true } ] }
  ]
}
```

## Data/bios/screenshot_check.json

This file records which stored value matched a BIOS screenshot. The engine reads only `rows`.

Each row carries `name`, `ui`, `status`, `label`, `stored`. Rows may also carry `page`, `varstore`, `varoffset`, `size`, `ifr_name`, `form`, `type`, and readers must ignore extras.

`name` is the BIOS field name the advice looks up. `label` is the value to trust for that field. The lookup is case insensitive.

```json
{
  "summary": { "matches-ui": 145, "label-differs": 5, "stored": 8 },
  "rows": [
    { "page": "Overclocking", "name": "OC Explore Mode", "ui": "Expert", "status": "matches-ui",
      "varstore": "Setup", "varoffset": 3170, "size": 1, "stored": 2, "label": "Expert" }
  ]
}
```

A missing row is not an error. The lookup returns an empty string and callers keep their existing fallback.
