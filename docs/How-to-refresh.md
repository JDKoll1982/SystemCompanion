# Refreshing the readings

The app shows whatever is in two files. When the machine changes, regenerate
them.

## One command

```powershell
cd C:\Users\johnk\source\repos\SystemCompanion
.\tools\refresh-and-run.ps1
```

That does all three steps below and then starts the app.

## What it does

### 1. Read the Windows side

```powershell
.\tools\discover-system.ps1 -OutputPath .\Data\current\system-snapshot.json
```

Reads hardware, Windows settings and installed tools, and writes one JSON file.
Read-only.

### 2. Rebuild the plain-language BIOS summary

```powershell
python .\tools\build-bios-summary.py
```

Reads `Data\bios\MsOcFile.decoded.json` and rewrites `Data\bios\bios-profile.json`
in ordinary words.

### 3. Build and run

```powershell
dotnet build SystemCompanion.csproj -c Debug
.\bin\Debug\net10.0-windows10.0.19041.0\SystemCompanion.exe
```

## When the BIOS settings change

If you change something in the BIOS and save a new profile to the USB stick:

1. Copy the new `MsOcFile.ocb` over `Data\bios\MsOcFile.ocb`.
2. Re-run the decoder (needs Python):

   ```powershell
   python .\Data\bios\tools\ocb_decrypt.py decode .\Data\bios\MsOcFile.ocb `
       --reference .\Data\bios\reference --out .\Data\bios
   ```

3. Rebuild the summary with step 2 above.

The decoder writes `MsOcFile.decoded.json`, `.txt` and `.fields.csv` next to the
profile.

## If the BIOS version changes

The `reference` folder only matches BIOS `E7E06IMS.AK0`. A different BIOS version
needs its own reference data, otherwise settings come out unnamed. The method is
described in `Data\bios\README-format-notes.md`.
