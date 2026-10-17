#!/usr/bin/env python3
"""
build-bios-summary.py - turn the technical OCB decode into plain-language facts.

The decoder writes Data/bios/MsOcFile.decoded.json, which is precise but full of
firmware terminology. This script picks the settings that matter and rewrites them
the way a non-technical person would describe them.

Usage:
    python build-bios-summary.py
"""

from __future__ import annotations

import json
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DECODED = ROOT / "Data" / "bios" / "MsOcFile.decoded.json"
OUTPUT = ROOT / "Data" / "bios" / "bios-profile.json"

WEEKDAYS = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"]


def load_fields(path: Path) -> dict[tuple[str, int], dict]:
    report = json.loads(path.read_text(encoding="utf-8"))
    fields: dict[tuple[str, int], dict] = {}
    for field in report.get("fields", []):
        key = (field["varstore"], field["varoffset"])
        fields.setdefault(key, field)
    return report, fields


def label(field: dict | None) -> str:
    """The words the BIOS itself shows, falling back to the raw prompt."""
    if not field:
        return "-"
    if field.get("ui_label"):
        return field["ui_label"]
    if field.get("value_label"):
        return field["value_label"]
    return field.get("name") or f'{field["value"]}'


def pick(fields: dict, varstore: str, offset: int) -> dict | None:
    return fields.get((varstore, offset))


def main() -> int:
    report, fields = load_fields(DECODED)
    info = report["file"]
    meta = report["metadata"]

    saved_ascii = (meta.get("ascii_timestamp") or {}).get("iso", "")
    saved_display = ""
    saved_day = ""
    if saved_ascii:
        stamp = datetime.fromisoformat(saved_ascii)
        saved_display = stamp.strftime("%d %b %Y at %H:%M")
        saved_day = WEEKDAYS[stamp.weekday()]

    cpu_line = ""
    for text in (meta.get("oci") or {}).get("strings", []):
        if "Core" in text or "Ryzen" in text:
            cpu_line = text
            break

    xmp = pick(fields, "Setup", 0x0E65)
    dram_freq = pick(fields, "Setup", 0x0D34)
    oc_mode = pick(fields, "Setup", 0x0C62)
    p_apply = pick(fields, "Setup", 0x0C69)
    p_ratio = pick(fields, "Setup", 0x0C66)
    e_ratio = pick(fields, "Setup", 0x0C67)
    ring = pick(fields, "Setup", 0x0CA7)
    ratio_mode = pick(fields, "Setup", 0x0C68)
    bclk = pick(fields, "Setup", 0x0F2D)
    timing_mode = pick(fields, "Setup", 0x0D45)
    mem_try = pick(fields, "Setup", 0x0F15)
    mem_ext = pick(fields, "Setup", 0x0F87)
    fast_boot = pick(fields, "Setup", 0x0E67)
    retry = pick(fields, "Setup", 0x0C65)
    mem_changed = pick(fields, "Setup", 0x0C63)
    cooler = pick(fields, "Setup", 0x0F88)
    current_limit = pick(fields, "Setup", 0x0F29)
    long_limit = pick(fields, "CpuSetup", 0x0017)
    short_limit = pick(fields, "CpuSetup", 0x001E)
    lite_load = pick(fields, "Setup", 0x0F63)
    vcore_mode = pick(fields, "Setup", 0x0CB2)
    sa_mode = pick(fields, "Setup", 0x0CBE)
    vddq_mode = pick(fields, "Setup", 0x0CC6)
    vdd2_mode = pick(fields, "Setup", 0x1263)
    dram_volt = pick(fields, "Setup", 0x0F90)
    dram_vddq = pick(fields, "Setup", 0x0F9A)
    dram_vpp = pick(fields, "Setup", 0x0FA4)
    ez_mode = pick(fields, "Setup", 0x119C)
    dmi = pick(fields, "Setup", 0x0F39)

    xmp_on = bool(xmp and xmp["value"] >= 1)
    mem_speed = dram_freq["value"] if dram_freq else None

    saved_short = ""
    if saved_ascii:
        saved_short = datetime.fromisoformat(saved_ascii).strftime("%d %b, %H:%M")

    headline = [
        {
            "label": "Fast memory profile",
            "value": "Profile 1" if xmp_on else "Off",
            "plain": ("Your memory is running its advertised fast profile instead of the slow safe default."
                      if xmp_on else "Your memory is running the slow safe default. Turning on its fast profile is free speed."),
            "state": "good" if xmp_on else "warn",
        },
        {
            "label": "Memory speed",
            "value": f"{mem_speed} MT/s" if mem_speed else "Unknown",
            "plain": ("The speed stored in the BIOS profile for your memory." if mem_speed
                      else "Could not read the memory speed from this profile."),
            "state": "good" if (mem_speed or 0) >= 5600 else "warn",
        },
        {
            "label": "CPU tuning mode",
            "value": label(oc_mode),
            "plain": "The full expert menu is switched on, so every setting below is available.",
            "state": "neutral",
        },
        {
            "label": "Profile saved",
            "value": saved_short or "Unknown",
            "plain": (f"Saved on a {saved_day}. This file is a snapshot of the BIOS at that moment."
                      if saved_day else "The save date could not be read."),
            "state": "neutral",
        },
    ]

    sections = [
        {
            "title": "Memory",
            "subtitle": "How fast your memory talks to the CPU",
            "icon": "\uE950",
            "items": [
                {"name": "Fast memory profile (XMP)", "value": label(xmp),
                 "plain": "One switch that loads the memory maker's tested fast settings.",
                 "state": "good" if xmp_on else "warn", "changed": True},
                {"name": "Memory speed", "value": f"{mem_speed} MT/s" if mem_speed else "-",
                 "plain": "Bigger numbers mean the memory can hand data over faster.",
                 "state": "good" if (mem_speed or 0) >= 5600 else "warn", "changed": True},
                {"name": "Timing mode", "value": label(timing_mode),
                 "plain": "Linked keeps all memory sticks on the same timing rules, which is the safe choice.",
                 "state": "neutral", "changed": False},
                {"name": "Memory Try It!", "value": label(mem_try),
                 "plain": "A built-in overclocking shortcut. Leaving it off means the fast profile is doing the work.",
                 "state": "neutral", "changed": False},
                {"name": "Memory extension mode", "value": label(mem_ext),
                 "plain": "Extra tuning for memory performance. Off is the normal, stable setting.",
                 "state": "neutral", "changed": False},
                {"name": "Retry count after a failed memory boot", "value": label(retry),
                 "plain": "If the fast profile ever fails to start, the board tries this many times before falling back.",
                 "state": "good", "changed": False},
                {"name": "Fast boot", "value": label(fast_boot),
                 "plain": "Shortens memory training on every start. Auto lets the board decide.",
                 "state": "neutral", "changed": False},
            ],
        },
        {
            "title": "Processor",
            "subtitle": "How the CPU is allowed to boost",
            "icon": "\uE9D9",
            "items": [
                {"name": "CPU cooler profile", "value": label(cooler),
                 "plain": "Tells the board what cooling you have. It decides the power limits.",
                 "state": "neutral", "changed": False},
                {"name": "P-core ratio", "value": label(p_ratio),
                 "plain": "The main speed multiplier. Auto means the CPU picks its own safe boost.",
                 "state": "neutral", "changed": False},
                {"name": "E-core ratio", "value": label(e_ratio),
                 "plain": "The multiplier for the small efficiency cores. Auto is the normal choice.",
                 "state": "neutral", "changed": False},
                {"name": "Ratio apply mode", "value": label(p_apply),
                 "plain": "All Core means every core follows the same rule.",
                 "state": "neutral", "changed": False},
                {"name": "Ring ratio", "value": label(ring),
                 "plain": "The speed of the CPU's internal shared cache. Auto is normal.",
                 "state": "neutral", "changed": False},
                {"name": "Base clock", "value": label(bclk),
                 "plain": "The heartbeat everything else multiplies from. 100.00 is the stock value.",
                 "state": "neutral", "changed": False},
                {"name": "CPU current limit", "value": label(current_limit),
                 "plain": "A safety ceiling on electrical current. Auto follows the cooler profile.",
                 "state": "neutral", "changed": False},
                {"name": "Long duration power limit", "value": label(long_limit),
                 "plain": "How many watts the CPU may use for long jobs. Auto follows the cooler profile.",
                 "state": "neutral", "changed": False},
                {"name": "Short duration power limit", "value": label(short_limit),
                 "plain": "How many watts the CPU may use briefly for bursty jobs.",
                 "state": "neutral", "changed": False},
                {"name": "CPU Lite Load", "value": label(lite_load),
                 "plain": "How aggressively the board feeds voltage. Auto is the safe default.",
                 "state": "neutral", "changed": False},
            ],
        },
        {
            "title": "Voltages",
            "subtitle": "Electrical settings that are best left alone",
            "icon": "\uE945",
            "items": [
                {"name": "CPU core voltage", "value": label(vcore_mode),
                 "plain": "Auto lets the CPU ask for exactly what it needs.", "state": "neutral", "changed": False},
                {"name": "System agent voltage", "value": label(sa_mode),
                 "plain": "Feeds the part of the CPU that talks to memory.", "state": "neutral", "changed": False},
                {"name": "CPU VDDQ voltage", "value": label(vddq_mode),
                 "plain": "A memory-related CPU voltage.", "state": "neutral", "changed": False},
                {"name": "CPU VDD2 voltage", "value": label(vdd2_mode),
                 "plain": "Another memory-related CPU voltage.", "state": "neutral", "changed": False},
                {"name": "Memory voltage", "value": label(dram_volt),
                 "plain": "Auto follows the fast memory profile you selected.", "state": "neutral", "changed": False},
                {"name": "Memory VDDQ voltage", "value": label(dram_vddq),
                 "plain": "Memory signalling voltage.", "state": "neutral", "changed": False},
                {"name": "Memory VPP voltage", "value": label(dram_vpp),
                 "plain": "Memory activation voltage.", "state": "neutral", "changed": False},
            ],
        },
        {
            "title": "Other saved details",
            "subtitle": "Smaller settings stored in the same file",
            "icon": "\uE946",
            "items": [
                {"name": "DMI link speed", "value": label(dmi),
                 "plain": "The link between the CPU and the chipset. Gen4 is the fastest this board supports.",
                 "state": "good", "changed": False},
                {"name": "Memory changed detection", "value": label(mem_changed),
                 "plain": "The board notices when memory has been swapped and retrains it.",
                 "state": "good", "changed": False},
                {"name": "Boot straight to the simple screen", "value": label(ez_mode),
                 "plain": "Whether the BIOS opens on its easy screen. Off means it opens in the advanced menu.",
                 "state": "neutral", "changed": True},
            ],
        },
    ]

    payload = {
        "generatedAt": datetime.now().isoformat(timespec="seconds"),
        "file": {
            "name": "MsOcFile.ocb",
            "board": "MSI PRO Z790-P WIFI (MS-7E06)",
            "biosVersion": f'{info["board_id"]}.{info["bios_version"].split(".")[-1]}',
            "savedOn": saved_display,
            "savedOnDay": saved_day,
            "valid": info["checksum"]["valid"],
            "checksum": f'0x{info["checksum"]["stored"]:08X}',
            "sizeBytes": info["size"],
            "savedVariables": len([r for r in report["records"] if r.get("name")]),
            "changedBytes": len(report["changes"]),
            "cpu": cpu_line,
        },
        "headline": headline,
        "sections": sections,
    }

    OUTPUT.write_text(json.dumps(payload, indent=1), encoding="utf-8")
    print(f"wrote {OUTPUT}")
    print(f"  board      : {payload['file']['board']}")
    print(f"  BIOS       : {payload['file']['biosVersion']}")
    print(f"  saved      : {saved_display} ({saved_day})")
    print(f"  XMP        : {'on, profile 1' if xmp_on else 'off'}")
    print(f"  memory     : {mem_speed} MT/s")
    print(f"  sections   : {len(sections)}, items {sum(len(s['items']) for s in sections)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
