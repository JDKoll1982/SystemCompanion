# MsOcFile.ocb — format notes and decoder

Tools and findings for the MSI BIOS overclock profile `E:\MsOcFile.ocb`.

**Short answer: the file is not encrypted.** It is a proprietary container of raw
UEFI setup-variable ("varstore") bytes plus a metadata trailer. Everything inside
is plaintext — the work is structural decoding, not cryptography.

Decoded profile under test:

| item | value |
| --- | --- |
| board | MSI PRO Z790-P WIFI / PRO Z790-P (**MS-7E06**, `E7E06IMS`) |
| BIOS | **E7E06IMS.AK0** (7E06vAK), build 08/19/2026 |
| CPU (recorded in file) | Intel Core i9-14900K |
| memory | **4 × 16 GB** Corsair `CMH32GX5M2B6000Z30` (DDR5), 65536 MB total |
| saved at | 2026-10-17 12:35:45 (Saturday) |
| file size | 11,889 bytes |
| checksum | valid (`0xFFFE43ED`) |
| setup variables carried | 22 populated (incl. `Setup`, `CpuSetup`, `SaSetup`, `PchSetup`, `MeSetup`, `UsbSupport`, `FixedBoot`) |
| bytes differing from BIOS defaults | 72 |
| overclocking actually applied | **XMP enabled** (Profile 1: DDR5-6000 30-36-36-76 @ 1.400 V); CPU ratios still auto |

Cross-checked against 47 unique BIOS setup screenshots in `bios_images/`:
**145 of 158 visible settings match the stored bytes exactly**, none are
unresolved, and the 5 remaining differences are explained (see
[Verifying against BIOS screenshots](#verifying-against-bios-screenshots)).

## Memory configuration (4 DIMMs)

The board runs **four 16 GB sticks** (`65536 MB ÷ 16384 MB = 4`), i.e. DDR5 at
**2 DIMMs per channel (2DPC)**. That matters for reading this profile:

* Everything memory-related is still on **`Auto`** except XMP itself — the
  profile stores `Extreme Memory Profile(XMP) = Profile 1` and the derived
  `Adjusted DRAM Frequency Value = 6000`, with `CPU SA Voltage`, `CPU VDDQ`,
  `CPU VDD2`, `DRAM Voltage`, `DRAM VDDQ` and `DRAM VPP` all at `Auto`.
  Nothing was hand-tuned for a 4-DIMM load.
* 4 × single-rank DDR5 at 6000 MT/s is **above Intel's validated 2DPC speed**
  for this platform, so training headroom comes entirely from the XMP SPD
  (30-36-36-76 @ 1.400 V) rather than from BIOS-side voltage tuning.
* The board is configured defensively, which fits a 4-DIMM XMP attempt:
  `Memory OC Retry Count = 2`, `Memory Fast Boot When OC Fail = Disabled`,
  `Memory Fast Boot = Auto` — it will retry and fall back instead of hanging.
* **Observation worth confirming:** every one of the 47 screenshots shows
  `DDR Speed 4000 MHz` in the EZ Mode header, while the Overclocking page
  reports `Adjusted DRAM Frequency Value 6000MHz`. 4000 MT/s is the classic
  Intel 2DPC fallback, so the possibility that XMP is *selected but not
  training* at 6000 deserves a check (CPU-Z → Memory → DRAM Frequency should
  read ~3000 MHz if XMP actually applied; ~2000 MHz means it fell back).

## Files

| file | purpose |
| --- | --- |
| `ocb_decrypt.py` | decoder/CLI: `inspect`, `decode`, `verify`, `rechecksum` |
| `build_bios_reference.py` | builds `reference/` from a BIOS image + extracted IFR text |
| `verify_against_screenshots.py` | cross-checks the profile against the BIOS screenshots |
| `reference/bios_reference.json` | factory defaults of 22 setup variables, hex encoded |
| `reference/ifr_catalog.json` | BIOS setting catalog: varstore + byte offset → name, width, options |
| `reference/ui_aliases.json` | MSI UI labels that differ from the firmware's own prompt |
| `bios_images/screenshot_settings.json` | settings transcribed from the screenshots (ground truth) |
| `decoded/MsOcFile.decoded.txt` | human-readable report |
| `decoded/MsOcFile.decoded.json` | full machine-readable report |
| `decoded/MsOcFile.fields.csv` | every named setting with default and current value |
| `decoded/screenshot_check.json` | per-setting agreement between screenshots and stored bytes |

## Container layout (verified)

```
0x00  "$MOS$"                    magic
0x05  board id, ASCII            "E7E06IMS"
0x0D  "."                        separator
0x0E  version tag, ASCII         "AK0"
0x11  u32 LE  checksum
0x15  u32 LE  record count       114
0x19  record table
        record := u32 LE id | u16 LE length | <length> bytes of payload
      ids are sparse (0x00 Setup, 0x01 PlatformLang, ..., 0x77 FixedBoot);
      length 0 means the slot is empty. On this file the 114 records tile
      0x19..0x2D1B exactly, with no padding, no alignment and no gaps.
0x2D1B metadata trailer (342 bytes)
```

The record payloads are the *raw contents* of the corresponding UEFI variables,
in the same order the firmware's own default store declares them.

### Checksum

```
F = (-(sum of every other byte in the file)) mod 2**32
    i.e. S + F == 0 (mod 2**32)
```

`ocb_decrypt.py rechecksum` recomputes it and reproduces this file bit-for-bit
(identical SHA-256), which confirms the rule.

Caveat: the checksum bytes can look like ASCII. In an earlier save the field was
`0xFFFE4432`, which made the header read as `$MOS$E7E06IMS.AK02D`; a naive
printable-ASCII scan then reports the version tag as `AK02D`. The decoder pins
the tag length from the record count and first record id instead.

### Metadata trailer

| offset | contents |
| --- | --- |
| `0x2D1B` | packed BCD save time: `sec, 00, min, 00, hour, 00, weekday, day, month, year` → `05 00 57 00 11 00 06 17 10 26` |
| `0x2E12` | `$OCI$` marker (preceded by 2 bytes `23 81`, role not established) |
| `0x2E1E` | board id `E7E06IMS`, version `AK0`, CPU string, ASCII time `20261017123545` |
| `0x2E59` | six u32 values: `10000, 32, 888, 4000, 1120, 33` — `10000` is BCLK in 0.01 MHz (100.00 MHz); the `888`/`1120` pair tracks live voltage readouts and changes between saves |

## How settings are named

Two inputs from the matching BIOS image (`E7E06IMS.AK0`, 32 MiB):

1. **Defaults** — the firmware stores factory defaults in AMI `NVAR` records
   inside the image. Each record is `"NVAR" | u16 size | FF FF FF | 83 00 |
   NUL-terminated name | value`. The nested records inside the top-level
   `StdDefaults` record also give the declaration order used by the profile.
2. **Field catalog** — `IFRExtractor-RS` output over the extracted HII form
   packages maps `(varstore, VarOffset)` to a prompt, width and option labels.
   `Setup` declares size `0x13D9` = 5081, matching the profile's first record.

A record is identified by similarity to a default, constrained so that assigned
names follow firmware declaration order (solved exactly by dynamic programming —
a greedy pass lets an all-zero look-alike record steal a later variable's name).

Independently verified correct:

* each varstore's payload was located at a base whose byte-match is sharply
  maximal (99.1%–99.9%) — every neighbouring offset scores 62%–91%;
* all `OneOf` factory-default values land on a declared option value, and
  shifting any payload by one byte produces hundreds of invalid values.

### Catalog parsing details that matter

Extracting all 16 HII form packages and parsing them correctly took four fixes;
each one silently cost real settings before it was found:

| problem | effect if unfixed | fix |
| --- | --- | --- |
| OneOf option lists are interleaved with `Default`, `SuppressIf`, `EqIdValList` | only the first option of a dropdown survived — XMP showed `Disabled` with no `Profile 1` | indentation-scope stack; options attach to the innermost open OneOf |
| long `Help:` strings are hard-wrapped, pushing `VarStoreId`/`VarOffset` onto unindented continuation lines | every question with multi-line help was dropped (PLL SFR voltages, PCH 0.82 V, power limits) | logical-line join until `QuestionFlags:` appears |
| `Min:`/`Max:` were regex-searched across the whole line | values were read out of the help text (`"Max: 1.400V"` → max `1`) | search only the field list after `QuestionFlags:` |
| the catalog was de-duplicated per offset | MSI's own relabelled questions were discarded (e.g. `Long Duration Power Limit(W)` vs firmware's `Power Limit 1`) | keep all questions per offset, prefer the last-declared one |
| `UsbSupport` is declared with the `VarStoreEfi` opcode | the whole varstore was unmapped (`XHCI Hand-off`, `Legacy USB Support`, …) | accept `VarStore` and `VarStoreEfi` |

Result: 6,325 questions across 79 varstores, **2,911 `OneOf` questions with zero
empty option lists** (previously 212 were empty).

### Value conventions in the report

MSI's UI prints things the raw bytes don't say directly; the decoder mirrors them:

* **`Auto`** — numerics whose stored value is `0` and whose minimum is `0`.
* **`checkboxes`** — `0` = Disabled, `1` = Enabled when the IFR lists no options.
* **`Profile 1`** for `Extreme Memory Profile(XMP)` is what the UI calls
  **Enabled**; the profile's own subtitle (e.g. `DDR5 6000MHz 30-36-36-96 1.400V`)
  identifies which XMP profile that is.
* Some numerics carry a display scale: `CPU Base Clock (MHz)` is stored in
  0.01 MHz (`10000` → `100.00`), `AVX Voltage Guardband Scale` is stored at
  double the displayed value (`256` → `128`).
* A few UI rows are live read-outs, not settings (`CPU Core Voltage 0.890V`,
  `DRAM Reference Clock 100`, `CPU IMC : DRAM Clock Gear2`, `Adjusted DRAM
  Frequency 6000MHz`); the stored setting behind them is still `Auto`.

## Verifying against BIOS screenshots

`bios_images/` holds 47 unique MSI Click BIOS 5 screenshots (1024×768, taken
2026-10-17). Every visible setting was transcribed into
`bios_images/screenshot_settings.json` (158 entries) and compared with the bytes
in the profile:

```powershell
python verify_against_screenshots.py 'E:\MsOcFile.ocb' --json decoded\screenshot_check.json
```

Result:

| outcome | count |
| --- | --- |
| matches the BIOS UI | 145 |
| stored value with no dropdown label (numerics, scales) | 8 |
| label differs, explained | 5 |
| unresolved | 0 |

The five explained differences:

| setting | stored | BIOS shows | why |
| --- | --- | --- | --- |
| CPU Cooler Tuning | `Intel Default Settings` | `Intel Default Settings (PL1: 253W)` | UI appends the resulting limit |
| Extreme Memory Profile(XMP) | `Profile 1` | `Enabled` | MSI prints "Enabled" for profile 1 |
| DRAM Reference Clock | `Auto` | `100` | live read-out |
| CPU IMC : DRAM Clock | `Auto` | `Gear2` | live read-out |
| SA PLL Frequency | `Auto` | `3200MHz` | live read-out |

The screenshots also caught a bug in the first pass of this project: the earlier
report claimed XMP was `Disabled` purely because the option list had been
truncated to `{0: Disabled}` while the stored byte was already `1`.

## Usage

```powershell
cd 'E:\OCB Decryption'

python ocb_decrypt.py inspect 'E:\MsOcFile.ocb'     # header only
python ocb_decrypt.py verify  'E:\MsOcFile.ocb'     # checksum + counts
python ocb_decrypt.py decode  'E:\MsOcFile.ocb' --out decoded
python ocb_decrypt.py rechecksum copy.ocb           # fix the checksum field
```

`decode` needs `reference/` next to the script (default) or `--reference DIR`.

### Rebuilding the reference data (other board / BIOS)

IFR text is produced once with third-party tools:

```powershell
uefi-firmware-parser -e -o extract E7E06IMS.AK0
ifrextractor extract\**\*.pe all        # writes *.en-US.uefi.ifr.txt

python build_bios_reference.py --rom E7E06IMS.AK0 `
    --ifr extract\...\section1.pe.0.0.en-US.uefi.ifr.txt --out reference
```

`build_bios_reference.py` needs only the ROM to produce defaults; the IFR file is
what turns offsets into names. Without it the decoder still parses the container
and reports changed offsets, just unnamed.

## Bytes that are not settings

38 of the 72 differing bytes have no setup question at their offset. They were
tested rather than guessed:

* **they are not per-varstore checksums.** The same board was saved twice, at
  11:57 (XMP off) and 12:35 (XMP on). Only two field bytes changed between the
  saves (`Setup 0xD34/0xD35` = DRAM frequency, `Setup 0xE65` = XMP), while all
  38 bytes stayed **byte-identical**. A checksum covering the payload would have
  moved. The only checksum in the container is the whole-file field at `0x11`,
  which did change (`0xFFFE4432` → `0xFFFE43ED`) and validates.
* an exhaustive search for a consistent CRC-8 (10 polynomials × init/xorout/
  reflection combinations, several slice windows) found no variant that explains
  more than chance across those bytes.
* 25 of them sit in the hidden `Overclocking Profile 8` form and 13 have no
  question at all; the values look like firmware bookkeeping (`0x01` flags,
  `0xA7` appearing in both `PchSetup 0x03` and `SaSetup 0x29`).

Practical consequence: a decoder should treat them as opaque. They are written
by the firmware at save time and are independent of the user's settings.

## Limitations

* The record *id* values are not derived from the ROM here; records are named by
  content. Ids are reported for reference only.
* 30 payloads have no matching BIOS default (mostly all-zero slots plus a few
  unidentifiable blobs, e.g. id `0x71`, 109 bytes). They are listed with a hex or
  string preview.
* Bytes with no IFR question at their offset are reported as
  `(no BIOS question at this offset)`. Several changed bytes are of this kind:
  DMA/PCIe EQ coefficients (`DMI Gen3/Gen4 RTCO …`), which the firmware itself
  writes at POST, plus unnamed slots in the hidden `Overclocking Profile 8` form.
* Profiles are tied to a BIOS version; MSI rejects a profile built for another
  build even when the payload looks compatible.
* `rechecksum` only repairs the checksum. It does not make a profile portable
  across BIOS versions.

## Provenance

Format facts here were derived by direct analysis of this profile and the
matching BIOS image, cross-checked against public community work:

* `lzghzr/msi-ocb-profile` — confirms the `S + F ≡ 0 (mod 2^32)` checksum rule
  for MSI `.ocb` profiles (`$MOS$` header / `$OCI$` trailer).
* `Asofwar/ocb-studio-native` and `voltara13/msi-ocb-tools` — the approach of
  resolving fields through AMI `NVAR` defaults plus IFR form definitions.

Modifying a BIOS profile can make a system unstable. Keep the untouched
`E:\MsOcFile.ocb` as your known-good copy.
