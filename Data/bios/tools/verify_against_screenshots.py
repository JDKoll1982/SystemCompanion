#!/usr/bin/env python3
"""
verify_against_screenshots.py - cross-check a decoded .ocb against BIOS screenshots.

The screenshots in `bios_images/` are ground truth for what the BIOS UI displays.
This script matches every setting transcribed in `bios_images/screenshot_settings.json`
to the IFR field catalog, reads the value actually stored in the profile, and
reports agreement, disagreement, or "not mapped".

Usage:
    python verify_against_screenshots.py E:\\MsOcFile.ocb [--json out.json]
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path
from typing import Any

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

from ocb_decrypt import Reference, decode  # noqa: E402

NON_ALNUM = re.compile(r"[^a-z0-9]+")


def normalise(name: str) -> str:
    return NON_ALNUM.sub("", name.lower())


def ui_tokens(ui: str) -> list[str]:
    """Turn a UI value such as '253W / Auto' into candidate labels."""
    return [part.strip() for part in ui.split("/") if part.strip()]


def match_score(entry_name: str, wanted: str) -> int:
    a, b = normalise(entry_name), normalise(wanted)
    if not a or not b:
        return 0
    if a == b:
        return 100
    if a.startswith(b) or b.startswith(a):
        return 80
    if b in a or a in b:
        return 60
    return 0


def find_fields(catalog: dict[str, dict[int, list[dict[str, Any]]]], name: str
                ) -> list[tuple[str, dict[str, Any], int]]:
    best: list[tuple[str, dict[str, Any], int]] = []
    best_score = 0
    for store, by_offset in catalog.items():
        for offset, entries in by_offset.items():
            for entry in entries:
                entry_name = entry.get("name") or ""
                if not entry_name:
                    continue
                score = match_score(entry_name, name)
                if score == 0:
                    continue
                if score > best_score:
                    best, best_score = [(store, entry, offset)], score
                elif score == best_score:
                    best.append((store, entry, offset))
    return best


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="Cross-check a decoded profile against BIOS screenshots")
    parser.add_argument("profile", type=Path)
    parser.add_argument("--expected", type=Path,
                        default=HERE / "bios_images" / "screenshot_settings.json")
    parser.add_argument("--json", type=Path, help="also write the result as JSON")
    args = parser.parse_args(argv)

    expected = json.loads(args.expected.read_text(encoding="utf-8"))
    reference = Reference.load(HERE / "reference")
    if not reference.defaults:
        print("error: reference/ not built", file=sys.stderr)
        return 2

    data = args.profile.read_bytes()
    report = decode(data, reference)
    payloads: dict[str, bytes] = {}
    for row in report["records"]:
        if row["name"] and row["name"] not in payloads and row["length"]:
            start = row["payload_offset"]
            payloads[row["name"]] = data[start:start + row["length"]]

    rows: list[dict[str, Any]] = []
    alias_index: dict[str, tuple[str, int, str]] = {}
    for store, mapping in reference.ui_aliases.items():
        for offset, label in mapping.items():
            alias_index[normalise(label)] = (store, offset, label)

    for item in expected["settings"]:
        name = item["name"]
        ui = item["ui"]
        hits = find_fields(reference.catalog, name)
        if not hits:
            pinned = alias_index.get(normalise(name))
            if pinned:
                store, offset, _label = pinned
                for entry in reference.catalog.get(store, {}).get(offset, []):
                    hits.append((store, entry, offset))
        if not hits:
            rows.append({"page": item["page"], "name": name, "ui": ui,
                         "status": "not-in-ifr"})
            continue
        store, entry, offset = hits[0]
        payload = payloads.get(store)
        if payload is None:
            rows.append({"page": item["page"], "name": name, "ui": ui,
                         "status": "varstore-absent", "varstore": store})
            continue
        size = max(1, int(entry.get("size") or 1))
        if offset + size > len(payload):
            rows.append({"page": item["page"], "name": name, "ui": ui,
                         "status": "offset-out-of-range", "varstore": store,
                         "varoffset": offset})
            continue
        value = int.from_bytes(payload[offset:offset + size], "little")
        label = None
        for option in entry.get("options", []):
            if int(option["value"]) == value:
                label = option["label"]
                break
        tokens = ui_tokens(ui)
        status = "stored"
        if label is not None:
            if any(normalise(t) == normalise(label) for t in tokens):
                status = "matches-ui"
            else:
                status = "label-differs"
        elif (entry.get("type") == "numeric" and value == 0
              and tokens and normalise(tokens[0]) == "auto"):
            # the BIOS prints "Auto" for the zero sentinel of a numeric range
            status = "matches-ui"
            label = "Auto"
        elif (entry.get("type") == "checkbox" and not entry.get("options")
              and tokens and normalise(tokens[0]) == ("enabled" if value else "disabled")):
            status = "matches-ui"
            label = "Enabled" if value else "Disabled"
        elif tokens and normalise(tokens[0]) in ("auto", "disabled", "enabled"):
            status = "label-missing"
        rows.append({
            "page": item["page"], "name": name, "ui": ui, "status": status,
            "varstore": store, "varoffset": offset, "size": size,
            "stored": value, "label": label,
            "ifr_name": entry.get("name"), "form": entry.get("form"),
            "type": entry.get("type"),
        })

    buckets: dict[str, int] = {}
    for row in rows:
        buckets[row["status"]] = buckets.get(row["status"], 0) + 1

    print(f"checked {len(rows)} settings transcribed from the screenshots")
    for key in sorted(buckets):
        print(f"  {key:<18} {buckets[key]}")

    print("\n=== settings that do not resolve to a profile field ===")
    for row in rows:
        if row["status"] in ("not-in-ifr", "varstore-absent", "offset-out-of-range"):
            print(f"  [{row['status']}] {row['page']}: {row['name']} (UI: {row['ui']})")

    print("\n=== settings whose stored value has no matching option label ===")
    for row in rows:
        if row["status"] == "label-missing":
            print(f"  {row['name']}: stored {row['stored']} (0x{row['stored']:02X}) "
                  f"but UI shows {row['ui']}")

    print("\n=== settings where the label disagrees with the UI ===")
    for row in rows:
        if row["status"] == "label-differs":
            print(f"  {row['name']}: stored {row['stored']} -> {row['label']!r}, "
                  f"UI shows {row['ui']}")

    print("\n=== settings that agree with the UI ===")
    for row in rows:
        if row["status"] == "matches-ui":
            print(f"  {row['page']:<32} {row['name']:<46} {row['label']} "
                  f"({row['stored']}) [{row['varstore']} 0x{row['varoffset']:04X}]")

    if args.json:
        args.json.write_text(json.dumps({"summary": buckets, "rows": rows}, indent=1),
                             encoding="utf-8")
        print(f"\nwritten {args.json}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
