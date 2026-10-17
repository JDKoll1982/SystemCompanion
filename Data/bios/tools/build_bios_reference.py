#!/usr/bin/env python3
"""
build_bios_reference.py - build the reference data that ocb_decrypt.py needs.

An MSI .ocb profile stores raw UEFI setup-varstore bytes. To turn those bytes
into readable setting names you need two things from the *matching* BIOS image:

  1. the factory defaults of every varstore, which the AMI firmware keeps in
     "NVAR" default records inside the image, and
  2. the HII form definitions (IFR), which map (varstore, byte offset) to a
     setting name, width and option labels.

Both are extracted here into a small reference bundle:

    reference/bios_reference.json   varstore defaults, hex encoded
    reference/ifr_catalog.json      fields keyed by varstore + offset

IFR text itself is produced once with third-party extractors (see README.md):

    uefi-firmware-parser -e -o extract E7E06IMS.AK0
    ifrextractor <module>.pe all

Usage:
    python build_bios_reference.py --rom E7E06IMS.AK0 \
        --ifr extract/**/*.en-US.uefi.ifr.txt --out reference
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path
from typing import Any

NVAR_MAGIC = b"NVAR"
NAME_RE = re.compile(r"[A-Za-z0-9_ ./()+:-]+")

VARSTORE_RE = re.compile(
    r'^VarStore(?:Efi)? Guid: (?P<guid>[0-9A-Fa-f-]+), '
    r'VarStoreId: (?P<id>0x[0-9A-Fa-f]+), '
    r'(?:Attributes: 0x[0-9A-Fa-f]+, )?'
    r'Size: (?P<size>0x[0-9A-Fa-f]+|\d+), Name: "(?P<name>[^"]*)"')
QUESTION_RE = re.compile(
    r'^\s*(?P<type>OneOf|CheckBox|Numeric|String|Password|OrderedList|DateTime)\s+'
    r'Prompt: "(?P<prompt>(?:[^"\\]|\\.)*)"')
OPTION_RE = re.compile(
    # the label is matched lazily because some labels contain a literal '"'
    # (e.g. 'N-Trig/Samsung 13.3"'), which a [^"] class would truncate
    r'OneOfOption\s+Option:\s*"(?P<label>.*?)"\s+Value:\s*'
    r'(?P<value>-?0x[0-9A-Fa-f]+|-?\d+)(?P<rest>.*)$')
# a concatenation of several modules' IFR dumps starts each one with this
SOURCE_MARKER = "// ==== SOURCE:"
FORM_RE = re.compile(r'^Form FormId: (?P<id>0x[0-9A-Fa-f]+), Title: "(?P<title>[^"]*)"')
QID_RE = re.compile(r'QuestionId: (0x[0-9A-Fa-f]+)')
VSID_RE = re.compile(r'VarStoreId: (0x[0-9A-Fa-f]+)')
VOFF_RE = re.compile(r'VarOffset: (0x[0-9A-Fa-f]+)')
SIZE_RE = re.compile(r'(?<!VarStore)\bSize: (\d+)')  # question sizes are in BITS
MIN_RE = re.compile(r'\bMin: (0x[0-9A-Fa-f]+|-?\d+)')
MAX_RE = re.compile(r'\bMax: (0x[0-9A-Fa-f]+|-?\d+)')
STEP_RE = re.compile(r'\bStep: (0x[0-9A-Fa-f]+|-?\d+)')
DEF_RE = re.compile(r'\bDefault: ([A-Za-z0-9x]+)')


def to_int(text: str) -> int:
    return int(text, 16) if text.lower().startswith("0x") else int(text)


# --------------------------------------------------------------------------- #
# AMI NVAR default records
# --------------------------------------------------------------------------- #

def _printable_name(raw: bytes) -> str | None:
    try:
        text = raw.decode("ascii")
    except UnicodeDecodeError:
        return None
    return text if NAME_RE.fullmatch(text) else None


def _nvar_name(blob: bytes, pos: int, size: int) -> tuple[str | None, int]:
    """Return (variable name, offset just past its NUL) for an NVAR record."""
    for name_start in range(pos + 8, min(pos + 24, pos + size - 2)):
        zero = blob.find(b"\x00", name_start, min(pos + size, name_start + 96))
        if zero < 0:
            continue
        name = _printable_name(blob[name_start:zero])
        if name:
            return name, zero + 1
    return None, -1


def scan_nvar_defaults(rom: bytes) -> tuple[dict[str, bytes], int]:
    """Return {variable name: factory default value} from the BIOS image."""
    defaults: dict[str, bytes] = {}
    hits = 0
    pos = 0
    while True:
        pos = rom.find(NVAR_MAGIC, pos)
        if pos < 0:
            break
        hits += 1
        size = int.from_bytes(rom[pos + 4:pos + 6], "little")
        if 16 <= size <= 0x20000 and pos + size <= len(rom):
            name, value_offset = _nvar_name(rom, pos, size)
            if name and value_offset < pos + size:
                value = rom[value_offset:pos + size]
                # container records wrap other NVAR records; skip them
                if value and NVAR_MAGIC not in value:
                    defaults.setdefault(name, value)
        pos += 1
    return defaults, hits


def nvar_declaration_order(rom: bytes) -> list[str]:
    """Names in the order the firmware's own default store declares them.

    The top level "StdDefaults" record's value is a flat sequence of nested
    NVAR records; the profile table follows that same order.
    """
    pos = 0
    while True:
        pos = rom.find(NVAR_MAGIC, pos)
        if pos < 0:
            return []
        size = int.from_bytes(rom[pos + 4:pos + 6], "little")
        if not (16 <= size <= 0x20000 and pos + size <= len(rom)):
            pos += 1
            continue
        name, value_offset = _nvar_name(rom, pos, size)
        if name == "StdDefaults":
            value = rom[value_offset:pos + size]
            order: list[str] = []
            cursor = 0
            while cursor + 11 <= len(value) and value[cursor:cursor + 4] == NVAR_MAGIC:
                nested_size = int.from_bytes(value[cursor + 4:cursor + 6], "little")
                if nested_size < 11:
                    break
                nested_name, _ = _nvar_name(value, cursor, min(nested_size, len(value) - cursor))
                if not nested_name:
                    break
                order.append(nested_name)
                cursor += nested_size
            return order
        pos += 1


# --------------------------------------------------------------------------- #
# IFR text
# --------------------------------------------------------------------------- #

QUESTION_START_RE = re.compile(
    r"^\s*(OneOf|Numeric|CheckBox|String|Password|OrderedList|DateTime|Ref)\b")


def _logical_lines(text: str):
    """Yield (line_number, logical_line).

    IFR opcodes are emitted on one physical line, but long Help strings are
    hard-wrapped by the extractor, which pushes VarOffset/VarStoreId onto
    unindented continuation lines. A question opcode is therefore only complete
    once its trailing "QuestionFlags:" marker has been seen; without this join
    every question with a multi-line Help text is dropped.
    """
    buffer = ""
    start_line = 0
    for line_no, physical in enumerate(text.splitlines(), 1):
        if not buffer:
            if not physical.strip():
                continue
            start_line = line_no
            buffer = physical
        else:
            buffer += " " + physical.strip()

        incomplete = (QUESTION_START_RE.match(buffer) is not None
                      and "QuestionFlags:" not in buffer)
        if not incomplete:
            yield start_line, buffer
            buffer = ""
    if buffer:
        yield start_line, buffer


def parse_ifr_text(text: str) -> tuple[list[dict[str, Any]], dict[str, list[dict[str, Any]]]]:
    varstores: list[dict[str, Any]] = []
    by_name: dict[str, list[dict[str, Any]]] = {}
    id_to_name: dict[int, str] = {}

    form_stack: list[str] = []
    # open question scopes: (indent, entry or None, is_oneof)
    scopes: list[tuple[int, dict[str, Any] | None, bool]] = []

    def close_scopes(indent: int) -> None:
        while scopes and scopes[-1][0] >= indent:
            scopes.pop()

    def close_scopes_for_option(indent: int) -> None:
        # options are normally indented deeper than their question, but keep a
        # OneOf that sits at the very same indentation (some extractors do that)
        while scopes and scopes[-1][0] >= indent:
            if scopes[-1][2] and scopes[-1][0] == indent:
                break
            scopes.pop()

    def innermost_oneof() -> dict[str, Any] | None:
        for _indent, entry, is_oneof in reversed(scopes):
            if is_oneof:
                return entry
        return None

    for line_no, raw_line in _logical_lines(text):
        if raw_line.startswith(SOURCE_MARKER):
            # every module has its own VarStore id space and form tree
            id_to_name.clear()
            form_stack.clear()
            scopes.clear()
            continue

        line = raw_line.strip()
        if not line:
            continue
        indent = len(raw_line) - len(raw_line.lstrip("\t "))

        m = VARSTORE_RE.match(line)
        if m:
            vs = {
                "id": to_int(m.group("id")),
                "guid": m.group("guid"),
                "size": to_int(m.group("size")),
                "name": m.group("name"),
            }
            varstores.append(vs)
            id_to_name[vs["id"]] = vs["name"]
            continue

        m = FORM_RE.match(line)
        if m:
            close_scopes(indent)
            form_stack.append(m.group("title"))
            continue
        if line.startswith("End Form") or line.startswith("EndForm"):
            if form_stack:
                form_stack.pop()
            close_scopes(indent)
            continue

        m = QUESTION_RE.match(line)
        if m:
            close_scopes(indent)
            vsid = VSID_RE.search(line)
            voff = VOFF_RE.search(line)
            store = id_to_name.get(to_int(vsid.group(1))) if vsid else None
            entry: dict[str, Any] | None = None
            if store is not None and voff:
                # Min/Max/Step/Size belong to the opcode's field list, which
                # starts at QuestionFlags. Searching the whole line picks up
                # values quoted inside the Help text (e.g. "Max: 1.400V").
                tail_at = line.find("QuestionFlags:")
                tail = line[tail_at:] if tail_at >= 0 else line
                size_bits = SIZE_RE.search(tail)
                size = max(1, to_int(size_bits.group(1)) // 8) if size_bits else 1
                qid = QID_RE.search(tail)
                entry = {
                    "varoffset": to_int(voff.group(1)),
                    "size": size,
                    "type": m.group("type").lower(),
                    "name": m.group("prompt").replace('\\"', '"'),
                    "question_id": qid.group(1) if qid else None,
                    "form": form_stack[-1] if form_stack else None,
                    "line": line_no,
                }
                for key, regex in (("min", MIN_RE), ("max", MAX_RE), ("step", STEP_RE)):
                    found = regex.search(tail)
                    if found:
                        entry[key] = to_int(found.group(1))
                default = DEF_RE.search(tail)
                if default:
                    entry["default_raw"] = default.group(1)
                if entry["type"] == "oneof":
                    entry["options"] = []
                by_name.setdefault(store, []).append(entry)
            scopes.append((indent, entry, m.group("type").lower() == "oneof"))
            continue

        m = OPTION_RE.search(line)
        if m:
            close_scopes_for_option(indent)
            current = innermost_oneof()
            if current is not None:
                current["options"].append({
                    "label": m.group("label").replace('\\"', '"'),
                    "value": to_int(m.group("value")),
                })
            continue

        # any other opcode at this indentation closes the question it belongs to
        close_scopes(indent)

    return varstores, by_name


def merge_catalogs(target: dict[str, list[dict[str, Any]]],
                   incoming: dict[str, list[dict[str, Any]]]) -> None:
    for store, entries in incoming.items():
        bucket = target.setdefault(store, [])
        seen = {(e["varoffset"], e["question_id"]) for e in bucket}
        for entry in entries:
            key = (entry["varoffset"], entry["question_id"])
            if key in seen:
                continue
            seen.add(key)
            bucket.append(entry)


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="Build .ocb reference data from a BIOS image")
    parser.add_argument("--rom", required=True, type=Path, help="BIOS image, e.g. E7E06IMS.AK0")
    parser.add_argument("--ifr", nargs="*", type=Path, default=[],
                        help="IFR text files extracted with ifrextractor")
    parser.add_argument("--out", type=Path, default=Path(__file__).with_name("reference"))
    args = parser.parse_args(argv)

    rom = args.rom.read_bytes()
    defaults, nvar_hits = scan_nvar_defaults(rom)
    order = nvar_declaration_order(rom)
    print(f"ROM: {args.rom.name} ({len(rom)} bytes), NVAR markers: {nvar_hits}, "
          f"variables with defaults: {len(defaults)}, declaration order: {len(order)}")
    for name, value in sorted(defaults.items(), key=lambda kv: -len(kv[1]))[:12]:
        print(f"  {name:<24} {len(value):>6} bytes")

    catalog: dict[str, list[dict[str, Any]]] = {}
    varstores: list[dict[str, Any]] = []
    for path in args.ifr:
        if not path.exists():
            print(f"warning: IFR file not found: {path}", file=sys.stderr)
            continue
        vs, by_name = parse_ifr_text(path.read_text(encoding="utf-8", errors="replace"))
        varstores.extend(vs)
        merge_catalogs(catalog, by_name)
        print(f"IFR: {path.name}: {len(vs)} varstores, "
              f"{sum(len(v) for v in by_name.values())} questions")

    if not catalog:
        print("error: no IFR data parsed", file=sys.stderr)
        return 2

    # keep only varstores we actually have defaults for, so the bundle stays small
    keep = {name: entries for name, entries in catalog.items() if name in defaults}
    args.out.mkdir(parents=True, exist_ok=True)

    (args.out / "bios_reference.json").write_text(json.dumps({
        "bios_id": args.rom.stem,
        "source_rom": args.rom.name,
        "order": [name for name in (order or list(defaults))],
        "varstores": {name: defaults[name].hex() for name in defaults},
    }, indent=1), encoding="utf-8")

    # Keep every question at an offset: MSI re-declares some options later in
    # the form tree with the label its UI actually shows (for example
    # "Long Duration Power Limit(W)" over the firmware's own "Power Limit 1").
    deduped: dict[str, list[dict[str, Any]]] = {}
    for name, entries in keep.items():
        seen: set[tuple[int, str]] = set()
        kept: list[dict[str, Any]] = []
        for entry in entries:
            signature = (entry["varoffset"], entry.get("name") or "")
            if signature in seen:
                continue
            seen.add(signature)
            kept.append(entry)
        deduped[name] = sorted(kept, key=lambda e: (e["varoffset"], e.get("line", 0)))

    (args.out / "ifr_catalog.json").write_text(json.dumps({
        "varstores": varstores,
        "by_varstore": deduped,
        "stats": {
            "varstores_with_defaults": len(keep),
            "fields": {name: len(v) for name, v in deduped.items()},
        },
    }, indent=1), encoding="utf-8")

    print(f"\nwrote {args.out / 'bios_reference.json'}")
    print(f"wrote {args.out / 'ifr_catalog.json'}")
    for name, entries in sorted(deduped.items(), key=lambda kv: -len(kv[1]))[:10]:
        named = sum(1 for e in entries if e.get("name"))
        print(f"  {name:<16} {len(entries):>5} fields ({named} named)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
