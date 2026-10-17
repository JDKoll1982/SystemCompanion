# What this computer actually is

A plain-language record of what was found on 17 October 2026.

## The machine

| Part | What it is |
| --- | --- |
| Motherboard | MSI PRO Z790-P WIFI (model MS-7E06), version 2.0 |
| Firmware (BIOS) | E7E06IMS.AK0, built 19 August 2026 |
| Processor | Intel Core i9-14900K — 24 cores, 32 threads, 3.2 GHz base, 36 MB shared cache |
| Memory | 64 GB in four 16 GB sticks, running at 6000 MT/s |
| Graphics | NVIDIA GeForce RTX 5070 Ti with about 16 GB of its own memory, driver 617.42 |
| Also has | Intel UHD 770 built-in graphics (unused while the NVIDIA card is present) |
| Screen | 3440 × 1440 at 240 Hz |
| System drive | Samsung 990 PRO 2 TB — 452 GB free of 837 GB |
| Second drive | Samsung 980 PRO 1 TB ("Game Drive") — 370 GB free of 1677 GB, mounted as D: |
| Windows | Windows 11 Home, build 26300, installed 7 December 2024 |

## What is already set up well

These are the things people usually get wrong, and on this machine they are
already right. Nothing needs doing.

- **Windows power mode is Ultimate Performance.** The processor is never held
  back to save electricity.
- **Game Mode is on**, so background housekeeping is paused while a game runs.
- **Background game recording is off**, so the graphics card is not secretly
  recording all the time.
- **Hardware GPU scheduling is on**, letting the card manage its own work.
- **The memory's fast profile is on and is actually working.** All four sticks
  report 6000 MT/s — the same number they are rated for, so nothing fell back to
  a slower speed. This is the single biggest free speed win on a machine like
  this, and it is already done.
- **Re-Size BAR and Above 4G decoding are both enabled in the BIOS**, which lets
  the graphics card expose all of its memory at once.
- **The refresh rate is set to 240 Hz**, so motion really is smooth.
- **The processor multipliers are all on Auto**, which is correct — modern Intel
  chips boost themselves better than a hand-written number does.
- **The graphics driver is recent** (September 2026).

## The five things worth doing

Ranked by how much they are likely to matter.

### 1. Stop the antivirus from re-scanning your code

Windows Security inspects every file a build creates. A large .NET build creates
tens of thousands of them, so the scanner can be a large share of build time.
Add your source folder to the exclusion list and leave the rest of the machine
protected.

*Expect:* builds noticeably faster — often a quarter to a half on big solutions.
*Risk:* low, but be careful to exclude only your own folders.

### 2. Decide whether memory integrity is worth its cost

Memory integrity is a security feature that wraps the operating system's core.
It is currently **on**. It costs a few percent in games and virtual machines, and
a little in AI work too. Keep it on if you install software from untrusted
sources; turn it off if you want the last few percent and trust what you install.

*Expect:* roughly 3-10% in games, a small but steady gain in AI work.
*Risk:* this genuinely lowers protection — it is a trade, not a free win.

### 3. Exclude AI model folders from scanning

Model files are tens of gigabytes and never change. Scanning them on every load
costs start-up time and disk bandwidth. Both LM Studio and Ollama are installed
here, so this is easy to do.

*Expect:* faster model loading, less disk contention while a model runs.
*Risk:* low.

### 4. Put the Ollama command on your PATH

Ollama's data folder exists but the command is not available in a terminal, so
any script that calls `ollama` will fail even though the app is installed.

*Expect:* scripts and terminals work without full paths.
*Risk:* none.

### 5. Keep an eye on free space

The system drive is at 54% free — healthy. The game drive is at 22% free, which
is close to the point where games start loading more slowly. Not urgent, but
worth watching.

## Things that look wrong but are not

- **"Shared cache 36 MB"** — Intel reports this in kilobytes, so the first
  version of the app showed 0 MB. It is fixed; the real value is 36 MB.
- **"Virtual Desktop Monitor" in the graphics list** — that is a software display
  used for remote sessions, not a real card. The app now labels it as virtual.
- **Four memory sticks at 6000 MT/s** — this is unusual. Four sticks are much
  harder to run fast than two, and this machine does it correctly. It is a
  genuinely well-configured setup.
- **The BIOS screenshots show "DDR Speed 4000 MHz"** while the settings say 6000.
  Windows reports the sticks at 6000, which is what matters — the BIOS screen
  header was showing the board's safety fallback figure, not the running speed.

## Where the details live

- `Data\bios\MsOcFile.ocb` — the original settings file, untouched
- `Data\bios\MsOcFile.decoded.txt` — the technical decode, for reference
- `Data\bios\MsOcFile.fields.csv` — every setting with its value
- `Data\bios\screenshots\` — the 54 BIOS screenshots the summary was checked against
- `Data\bios\README-format-notes.md` — how the file format was worked out
