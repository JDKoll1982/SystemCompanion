# 001 — System Companion

A plain description of what this app is for and what it must do. Written so it
can be read by someone who does not write software.

## The problem

Most people cannot tell whether their computer is set up well. The information
exists, but it is scattered across a BIOS screen full of abbreviations, a Windows
settings app with a hundred switches, and advice on the internet that contradicts
itself. The result is either doing nothing, or changing something random and
making things worse.

## The idea

One window, three screens, plain words.

1. **What the firmware says.** The motherboard keeps a small file describing how
   it is configured. Show that as sentences, not as `0x0E65 = 1`.
2. **What Windows sees.** Show the parts and the switches that decide how fast
   the machine feels.
3. **What could be better.** List the changes that would actually help, each with
   what it is worth, how to do it, and how risky it is.

## Rules the app must follow

- **Read only.** The app never changes a setting. Advice is text, not an action.
- **Plain words first.** No abbreviations without an explanation. A reader who has
  never opened the BIOS should understand every screen.
- **Say when something is already good.** Advice is not a to-do list; a machine
  that is already configured well should be told so.
- **Never guess silently.** If a value cannot be read, say "unknown" rather than
  showing a plausible-looking number.
- **Two kinds of honesty about risk.** Some changes are free and safe; some trade
  security or stability for speed. The card must say which.
- **Works offline.** No account, no telemetry, no network calls.

## What each screen must contain

### BIOS — right now

- Which board and firmware version the settings belong to, and when they were saved.
- A health statement for the settings file itself.
- Four headline facts: memory profile, memory speed, tuning mode, save date.
- Grouped detail: memory, processor, voltages, other.
- For each line: the setting name, its value, and one sentence explaining what it
  does — plus a marker when the value differs from the factory default.

### Windows — right now

- Four headline facts: processor, memory, graphics, Windows build.
- Groups: processor, memory, graphics, storage, Windows speed settings, Windows
  version, programs that start at sign-in, developer tools.
- For each line: the reading and what it means.

### What could be better

- A summary: how many suggestions are worth doing, optional, need a manual check,
  and already correct.
- Filters for Programming, Gaming, AI work and Everyday.
- One card per suggestion containing: status, goal, title, one-line summary,
  current value → target value, why it helps, how to do it, expected benefit,
  risk level, and whether a restart is needed.
- Suggestions that help more than one goal appear once, not twice.

## What is explicitly out of scope

- Changing settings automatically. That is a separate, much riskier feature.
- Live sensor graphs. Useful, but a different app.
- Reading the BIOS on a machine other than this one. The reference data is tied to
  firmware `E7E06IMS.AK0`.
- Accounts, cloud sync, notifications.

## How we will know it works

- The app opens on a machine with no network and shows real values.
- Every suggestion can be traced back to a reading in the snapshot.
- A change that was made (for example turning XMP on) is reflected on the next
  scan without any code change.
- Someone who does not know what "XMP" means can still read every screen and know
  what to do next.
