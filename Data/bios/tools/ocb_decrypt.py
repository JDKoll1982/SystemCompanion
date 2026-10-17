#!/usr/bin/env python3
"""
ocb_decrypt.py - decode an MSI BIOS OC profile ("MsOcFile.ocb").

The .ocb file is NOT encrypted. It is a proprietary binary container that holds
raw UEFI setup variables (varstores). "Decrypting" it means:

  1. parsing the container: header, checksum, the record table, the metadata
     trailer with its save timestamps,
  2. naming each record by matching its payload against the BIOS's own factory
     defaults (AMI "NVAR" records inside the firmware image),
  3. diffing every payload against those defaults, and
  4. translating changed byte offsets into BIOS setting names through the
     firmware's HII form definitions (IFR).

Verified container layout (E7E06IMS.AK0, MSI PRO Z790-P WIFI / MS-7E06):

  0x00  "$MOS$"                       magic
  0x05  board id, ASCII               "E7E06IMS"
  0x0D  "."                           separator
  0x0E  version tag, ASCII            "AK0"
  0x11  u32 LE  checksum              see rule below
  0x15  u32 LE  record count          114
  0x19  record table, 114 records tiling to 0x2D1B with no gaps:

            record := u32 LE id | u16 LE length | <length> bytes of payload

        ids are sparse (0x00 Setup, 0x01 PlatformLang, ..., 0x77 FixedBoot);
        a length of zero means "this slot is empty".
  0x2D1B metadata trailer (342 bytes on this file):
            packed BCD save time, ASCII board/BIOS/CPU strings, ASCII
            "YYYYMMDDHHMMSS" save time, "$OCI$" marker, six u32 values

Checksum rule (verified exactly, S + F == 0 mod 2**32):

    F = (-(sum of every other byte in the file)) mod 2**32

so the four bytes at 0x11..0x14 are the two's complement of the rest of the
file's byte sum. The checksum bytes can look like ASCII (this file's 0xFFFE4432
makes "AK0" read as "AK02D"), so the version tag length is pinned from the
fields that follow it rather than by printable-ASCII scanning.

Usage:
    python ocb_decrypt.py inspect     MsOcFile.ocb
    python ocb_decrypt.py decode      MsOcFile.ocb --out decoded
    python ocb_decrypt.py verify      MsOcFile.ocb
    python ocb_decrypt.py rechecksum  MsOcFile.ocb

Only the Python 3.10+ standard library is required. Reference data is produced
once by build_bios_reference.py from the matching BIOS image.
"""

from __future__ import annotations

import argparse
import json
import struct
import sys
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

MAGIC = b"$MOS$"
OCI_MAGIC = b"$OCI$"
RECORD_COUNT_OFFSET = 0x15
RECORD_START = 0x19
RECORD_HEADER_SIZE = 6


# --------------------------------------------------------------------------- #
# primitive readers
# --------------------------------------------------------------------------- #

def _u32(data: bytes, off: int) -> int:
    return struct.unpack_from("<I", data, off)[0]


def _u16(data: bytes, off: int) -> int:
    return struct.unpack_from("<H", data, off)[0]


# --------------------------------------------------------------------------- #
# container header
# --------------------------------------------------------------------------- #

@dataclass
class Header:
    board_id: str
    version_tag: str
    checksum_offset: int
    checksum_value: int
    checksum_ok: bool
    record_count: int
    first_record_id: int

    @property
    def payload_offset(self) -> int:
        return self.checksum_offset + 14  # u32 count, u32 id, u16 length


@dataclass
class _Candidate:
    tag_length: int
    checksum_offset: int
    record_count: int
    structural_score: int = 0

    @property
    def payload_offset(self) -> int:
        return self.checksum_offset + 14


def _match_score(haystack: bytes, start: int, needle: bytes) -> int:
    if start < 0 or start + len(needle) > len(haystack):
        return -1
    return sum(1 for a, b in zip(needle, haystack[start:start + len(needle)]) if a == b)


def _header_candidates(data: bytes, dot: int) -> list[_Candidate]:
    """Enumerate possible version-tag lengths.

    The checksum field's bytes can themselves be printable ASCII, so the tag
    length is pinned by the two u32 fields that follow it: the record count and
    the first record's id. Both must be small for any real profile.
    """
    scored: list[_Candidate] = []
    for length in range(1, 9):
        checksum_offset = dot + 1 + length
        if checksum_offset + 14 > len(data):
            break
        tag = data[dot + 1:checksum_offset]
        if not all(0x21 <= c <= 0x7E for c in tag):
            continue
        candidate = _Candidate(length, checksum_offset, _u32(data, checksum_offset + 4))
        first_id = _u32(data, checksum_offset + 8)
        first_length = _u16(data, checksum_offset + 12)
        score = 0
        score += 3 if 16 <= candidate.record_count <= 4096 else 0
        score += 2 if first_id == 0 else 0
        if first_length and candidate.payload_offset + first_length < len(data):
            score += 2
        if length == 3:  # MSI version tags are three characters
            score += 1
        candidate.structural_score = score
        scored.append(candidate)
    if not scored:
        raise ValueError("could not parse the profile header")
    scored.sort(key=lambda c: (c.structural_score, -c.tag_length), reverse=True)
    return scored


def parse_header(data: bytes, reference: "Reference | None" = None) -> Header:
    if not data.startswith(MAGIC):
        raise ValueError(f"not an MSI OC profile: expected {MAGIC!r} at offset 0")
    dot = data.find(b".", len(MAGIC), 0x40)
    if dot < 0:
        raise ValueError("board id / version separator '.' not found in header")

    candidates = _header_candidates(data, dot)
    if reference and reference.defaults.get("Setup"):
        probe = reference.defaults["Setup"][:96]
        candidates.sort(key=lambda c: _match_score(data, c.payload_offset, probe), reverse=True)
    best = candidates[0]

    checksum_value = _u32(data, best.checksum_offset)
    other_bytes = sum(data) - sum(data[best.checksum_offset:best.checksum_offset + 4])
    combined = (other_bytes + checksum_value) & 0xFFFFFFFF

    return Header(
        board_id=data[len(MAGIC):dot].decode("ascii", "replace"),
        version_tag=data[dot + 1:best.checksum_offset].decode("ascii", "replace"),
        checksum_offset=best.checksum_offset,
        checksum_value=checksum_value,
        checksum_ok=(combined == 0),
        record_count=best.record_count,
        first_record_id=_u32(data, best.checksum_offset + 8),
    )


def recompute_checksum(data: bytes, checksum_offset: int) -> int:
    other = sum(data) - sum(data[checksum_offset:checksum_offset + 4])
    return (-other) & 0xFFFFFFFF


# --------------------------------------------------------------------------- #
# record table
# --------------------------------------------------------------------------- #

@dataclass
class Record:
    index: int
    record_id: int
    header_offset: int
    payload_offset: int
    length: int
    name: str | None = None
    match_ratio: float = 0.0
    payload: bytes = field(default=b"", repr=False)

    @property
    def end(self) -> int:
        return self.payload_offset + self.length


def walk_records(data: bytes, header: Header) -> list[Record]:
    """Walk the record table; raise if it does not tile the file cleanly."""
    records: list[Record] = []
    pos = RECORD_START
    for index in range(header.record_count):
        if pos + RECORD_HEADER_SIZE > len(data):
            raise ValueError(f"record {index} header runs past end of file at 0x{pos:X}")
        record_id = _u32(data, pos)
        length = _u16(data, pos + 4)
        payload_offset = pos + RECORD_HEADER_SIZE
        if payload_offset + length > len(data):
            raise ValueError(f"record {index} (id 0x{record_id:X}) payload runs past end of file")
        records.append(Record(
            index=index,
            record_id=record_id,
            header_offset=pos,
            payload_offset=payload_offset,
            length=length,
            payload=data[payload_offset:payload_offset + length],
        ))
        pos = payload_offset + length
    return records


def _similarity(a: bytes, b: bytes) -> float:
    n = min(len(a), len(b))
    if n == 0:
        return 0.0
    equal = sum(1 for i in range(n) if a[i] == b[i])
    return equal / max(len(a), len(b))


def name_records(records: list[Record], reference: "Reference") -> None:
    """Attach varstore names by matching payloads against BIOS defaults.

    The profile stores the same variables in the same order as the firmware's
    own default store, so assignments must respect NVAR order. A greedy pass
    can commit to a look-alike early and then block the real variable, so the
    assignment is solved exactly: dynamic programming over
    (record index, last assigned NVAR rank), maximising the number of named
    records and then the total similarity.
    """
    rank = reference.rank()
    options: list[list[tuple[int, str, float]]] = []
    for record in records:
        record_options: list[tuple[int, str, float]] = []
        if record.length >= 1:
            for name, default in reference.defaults.items():
                index = rank.get(name)
                if index is None:
                    continue
                if abs(len(default) - record.length) > 8:
                    continue
                ratio = _similarity(default, record.payload)
                prefix = default == record.payload[:len(default)]
                if ratio >= 0.80 or prefix:
                    if max(len(default), record.length) < 8 and not prefix:
                        continue
                    record_options.append((index, name, ratio))
        options.append(record_options)

    # states: last rank -> (count, total ratio, assignments)
    states: dict[int, tuple[int, float, list[tuple[Record, str, float]]]] = {
        -1: (0, 0.0, [])
    }
    for record, record_options in zip(records, options):
        updated = dict(states)
        for last_rank, (count, total, assigned) in states.items():
            for index, name, ratio in record_options:
                if index <= last_rank:
                    continue
                candidate = (count + 1, total + ratio, assigned + [(record, name, ratio)])
                current = updated.get(index)
                if current is None or (candidate[0], candidate[1]) > (current[0], current[1]):
                    updated[index] = candidate
        states = updated

    best = max(states.values(), key=lambda state: (state[0], state[1]))
    for record, name, ratio in best[2]:
        record.name = name
        record.match_ratio = ratio


def unknown_preview(payload: bytes) -> str:
    head = payload[:48]
    text = "".join(chr(c) if 0x20 <= c < 0x7F else "." for c in head)
    if sum(1 for c in head if 0x20 <= c < 0x7F) >= 0.5 * len(head):
        return f'"{text}"'
    return payload[:24].hex(" ")


# --------------------------------------------------------------------------- #
# reference data
# --------------------------------------------------------------------------- #

@dataclass
class Reference:
    bios_id: str = ""
    defaults: dict[str, bytes] = field(default_factory=dict)
    catalog: dict[str, dict[int, list[dict[str, Any]]]] = field(default_factory=dict)
    order: list[str] = field(default_factory=list)
    ui_aliases: dict[str, dict[int, str]] = field(default_factory=dict)

    def ui_label(self, varstore: str | None, offset: int) -> str | None:
        return (self.ui_aliases.get(varstore or "") or {}).get(offset)

    def rank(self) -> dict[str, int]:
        names = self.order or list(self.defaults)
        return {name: index for index, name in enumerate(names)}

    @classmethod
    def load(cls, directory: Path) -> "Reference":
        ref = cls()
        meta = directory / "bios_reference.json"
        if not meta.exists():
            return ref
        raw = json.loads(meta.read_text(encoding="utf-8"))
        ref.bios_id = raw.get("bios_id", "")
        for name, hexval in raw.get("varstores", {}).items():
            ref.defaults[name] = bytes.fromhex(hexval)
        ref.order = list(raw.get("order") or [])
        aliases = directory / "ui_aliases.json"
        if aliases.exists():
            data = json.loads(aliases.read_text(encoding="utf-8"))
            for store, mapping in data.items():
                if store.startswith("_"):
                    continue
                ref.ui_aliases[store] = {int(k): v for k, v in mapping.items()}
        fields_file = directory / "ifr_catalog.json"
        if fields_file.exists():
            catalog = json.loads(fields_file.read_text(encoding="utf-8"))
            for store, entries in catalog.get("by_varstore", {}).items():
                by_offset: dict[int, list[dict[str, Any]]] = {}
                for entry in entries:
                    by_offset.setdefault(int(entry["varoffset"]), []).append(entry)
                ref.catalog[store] = by_offset
        return ref


# --------------------------------------------------------------------------- #
# metadata trailer
# --------------------------------------------------------------------------- #

def _valid_bcd(value: int) -> bool:
    return (value & 0x0F) <= 9 and (value >> 4) <= 9


def _bcd(value: int) -> int:
    return (value >> 4) * 10 + (value & 0x0F)


def find_bcd_time(data: bytes, start: int) -> dict[str, Any] | None:
    """sec, 00, min, 00, hour, 00, weekday, day, month, year - all BCD."""
    for i in range(max(0, start), len(data) - 10):
        if data[i + 1] or data[i + 3] or data[i + 5]:
            continue
        sec, minute, hour = data[i], data[i + 2], data[i + 4]
        weekday, day, month, year = data[i + 6], data[i + 7], data[i + 8], data[i + 9]
        if not all(_valid_bcd(v) for v in (sec, minute, hour, day, month, year)):
            continue
        s, m, h = _bcd(sec), _bcd(minute), _bcd(hour)
        d, mo, y = _bcd(day), _bcd(month), _bcd(year)
        if not (s < 60 and m < 60 and h < 24 and 1 <= d <= 31 and 1 <= mo <= 12 and weekday <= 6):
            continue
        return {
            "offset": i,
            "iso": f"20{y:02d}-{mo:02d}-{d:02d}T{h:02d}:{m:02d}:{s:02d}",
            "weekday_0_sunday": weekday,
            "raw": data[i:i + 10].hex(" "),
        }
    return None


def find_ascii_timestamp(data: bytes) -> dict[str, Any] | None:
    for i in range(len(data) - 14):
        chunk = data[i:i + 14]
        if not chunk.isdigit() or not chunk.startswith(b"20"):
            continue
        month, day = int(chunk[4:6]), int(chunk[6:8])
        hour, minute = int(chunk[8:10]), int(chunk[10:12])
        if 1 <= month <= 12 and 1 <= day <= 31 and hour < 24 and minute < 60:
            text = chunk.decode("ascii")
            return {
                "offset": i,
                "value": text,
                "iso": (f"{text[0:4]}-{text[4:6]}-{text[6:8]}T"
                        f"{text[8:10]}:{text[10:12]}:{text[12:14]}"),
            }
    return None


def parse_oci_block(data: bytes) -> dict[str, Any]:
    """'$OCI$' trailer: marker, printable NUL separated values, trailing u32s."""
    idx = data.find(OCI_MAGIC)
    if idx < 0:
        return {"found": False}

    strings: list[str] = []
    pos = idx + len(OCI_MAGIC)
    last_end = pos
    while pos < len(data):
        if not (0x20 <= data[pos] < 0x7F):
            pos += 1
            continue
        end = pos
        while end < len(data) and 0x20 <= data[end] < 0x7F:
            end += 1
        run = data[pos:end]
        if len(run) >= 3:  # single printable bytes are per-field tags
            strings.append(run.decode("ascii"))
            last_end = end + 1 if end < len(data) and data[end] == 0 else end
        pos = end

    tail: list[int] = []
    if len(data) - last_end >= 4 and (len(data) - last_end) % 4 == 0:
        tail = [_u32(data, off) for off in range(last_end, len(data), 4)]
    return {
        "found": True,
        "offset": idx,
        "prefix_bytes": data[max(0, idx - 2):idx].hex(" "),
        "strings": strings,
        "trailing_u32": tail,
    }


def parse_metadata(data: bytes, trailer_start: int) -> dict[str, Any]:
    return {
        "trailer_start": trailer_start,
        "trailer_length": len(data) - trailer_start,
        "ascii_timestamp": find_ascii_timestamp(data),
        "bcd_timestamp": find_bcd_time(data, max(trailer_start, len(data) - 0x200)),
        "oci": parse_oci_block(data),
    }


# --------------------------------------------------------------------------- #
# field naming
# --------------------------------------------------------------------------- #

def _option_label(entry: dict[str, Any] | None, value: int) -> str | None:
    if not entry:
        return None
    for opt in entry.get("options", []):
        if int(opt["value"]) == value:
            return str(opt["label"])
    return None


def _pick_field(candidates: list[dict[str, Any]], payload: bytes, offset: int) -> dict[str, Any]:
    """Choose among questions declared at one offset.

    Preference order: a field whose option list contains the stored value,
    then a named field, then the *last* declaration (MSI re-declares settings
    with the label its own UI shows, e.g. "Long Duration Power Limit(W)" is
    declared after the firmware's "Power Limit 1"), then the narrower width.
    """
    def rank(entry: dict[str, Any]) -> tuple[int, int, int, int]:
        size = max(1, int(entry.get("size") or 1))
        value = int.from_bytes(payload[offset:offset + size], "little")
        options = {int(o["value"]) for o in entry.get("options", [])}
        return (
            0 if options and value in options else 1,
            0 if entry.get("name") else 1,
            -int(entry.get("line") or 0),
            size,
        )

    return sorted(candidates, key=rank)[0]


def _alias_names(candidates: list[dict[str, Any]], primary: dict[str, Any]) -> list[str]:
    names = []
    for entry in candidates:
        name = entry.get("name")
        if name and name != primary.get("name") and name not in names:
            names.append(name)
    return names


OC_FORM_HINTS = (
    "overclocking",
    "advanced dram",
    "memory overclocking",
    "dram training",
    "turbo ratio",
    "custom p-state",
    "cpu vr",
    "core/ia vr",
    "gt vr",
    "sa vr",
    "ring",
    "memory configuration",
    "power management",
)


def _describe(record: Record, entry: dict[str, Any], default: bytes,
              aliases: list[str] | None = None,
              ui_label: str | None = None) -> dict[str, Any]:
    offset = int(entry["varoffset"])
    size = max(1, int(entry.get("size") or 1))
    size = max(1, min(size, len(default) - offset, len(record.payload) - offset))
    row = {
        "varstore": record.name,
        "varoffset": offset,
        "file_offset": record.payload_offset + offset,
        "size": size,
        "type": entry.get("type"),
        "name": entry.get("name") or None,
        "ui_label": ui_label,
        "aliases": aliases or [],
        "form": entry.get("form"),
        "default": int.from_bytes(default[offset:offset + size], "little"),
        "value": int.from_bytes(record.payload[offset:offset + size], "little"),
    }
    row["default_label"] = _option_label(entry, row["default"])
    row["value_label"] = _option_label(entry, row["value"])
    if row["value_label"] is None and not entry.get("options"):
        if entry.get("type") == "checkbox":
            row["value_label"] = "Enabled" if row["value"] else "Disabled"
        elif entry.get("type") == "numeric" and row["value"] == 0 and int(entry.get("min") or 0) == 0:
            # the BIOS UI prints "Auto" for the zero sentinel of a numeric range
            row["value_label"] = "Auto"
    return row


def decode(data: bytes, reference: Reference) -> dict[str, Any]:
    header = parse_header(data, reference)
    records = walk_records(data, header)
    name_records(records, reference)
    trailer_start = records[-1].end if records else header.payload_offset
    meta = parse_metadata(data, trailer_start)
    result: dict[str, Any] = {
        "file": {
            "size": len(data),
            "magic": MAGIC.decode(),
            "board_id": header.board_id,
            "bios_version": f"{header.board_id}.{header.version_tag}",
            "checksum": {
                "offset": header.checksum_offset,
                "stored": header.checksum_value,
                "expected": recompute_checksum(data, header.checksum_offset),
                "valid": header.checksum_ok,
            },
            "record_count": header.record_count,
            "records_walked": len(records),
            "record_table_end": trailer_start,
        },
        "metadata": meta,
        "records": [],
        "changes": [],
        "fields": [],
    }

    for record in records:
        row: dict[str, Any] = {
            "index": record.index,
            "id": record.record_id,
            "header_offset": record.header_offset,
            "payload_offset": record.payload_offset,
            "length": record.length,
            "name": record.name,
            "match_ratio": round(record.match_ratio, 4) if record.name else None,
        }
        default = reference.defaults.get(record.name) if record.name else None
        if default:
            row["changed_bytes"] = sum(
                1 for a, b in zip(default, record.payload) if a != b)
        elif record.length:
            row["preview"] = unknown_preview(record.payload)
        result["records"].append(row)

        if not default:
            continue

        fields_by_offset = reference.catalog.get(record.name) or {}
        changed = {i for i in range(min(len(default), len(record.payload)))
                   if default[i] != record.payload[i]}
        covered: set[int] = set()

        for offset in sorted(fields_by_offset):
            if offset >= len(default):
                continue
            entry = _pick_field(fields_by_offset[offset], record.payload, offset)
            size = max(1, int(entry.get("size") or 1))
            span = set(range(offset, min(offset + size, len(record.payload))))
            if span & changed:
                covered |= span
                change = _describe(record, entry, default,
                                   _alias_names(fields_by_offset[offset], entry),
                                   reference.ui_label(record.name, offset))
                change["kind"] = "field"
                result["changes"].append(change)

        for offset in sorted(changed - covered):
            result["changes"].append({
                "varstore": record.name,
                "varoffset": offset,
                "file_offset": record.payload_offset + offset,
                "size": 1,
                "kind": "reserved",
                "name": None,
                "default": default[offset],
                "value": record.payload[offset],
            })

        for offset in sorted(fields_by_offset):
            if offset >= len(default):
                continue
            entry = _pick_field(fields_by_offset[offset], record.payload, offset)
            if not entry.get("name"):
                continue
            row_field = _describe(record, entry, default,
                                  _alias_names(fields_by_offset[offset], entry),
                                  reference.ui_label(record.name, offset))
            row_field["changed"] = bool(
                set(range(offset, min(offset + max(1, row_field["size"]), len(record.payload))))
                & changed)
            row_field["oc_relevant"] = any(
                hint in (entry.get("form") or "").lower() for hint in OC_FORM_HINTS)
            result["fields"].append(row_field)

    return result


# --------------------------------------------------------------------------- #
# rendering
# --------------------------------------------------------------------------- #

KEY_SETTINGS = (
    "oc explore mode",
    "cpu ratio mode",
    "p-core ratio",
    "e-core ratio",
    "ring ratio",
    "cpu core voltage",
    "cpu ratio offset",
    "cpu current limit",
    "cpu vddq voltage",
    "dram voltage",
    "dram frequency",
    "memory voltage",
    "extreme memory profile",
    "xmp",
    "cpu lite load",
    "load line calibration",
    "long duration power limit",
    "short duration power limit",
    "power limit 1",
    "power limit 2",
    "cpu base clock",
    "bclk",
    "cpu undervolt protection",
)


def _fmt(value: int, label: str | None) -> str:
    return f"{label} ({value})" if label else str(value)


def render_text(report: dict[str, Any]) -> str:
    f = report["file"]
    meta = report["metadata"]
    out: list[str] = []
    add = out.append

    add("MSI OC profile (MsOcFile.ocb) - decoded")
    add("=" * 78)
    add(f"board             : {f['board_id']}")
    add(f"BIOS version      : {f['bios_version']}")
    add(f"file size         : {f['size']} bytes")
    checksum = f["checksum"]
    status = "VALID" if checksum["valid"] else f"INVALID (expected 0x{checksum['expected']:08X})"
    add(f"checksum @0x{checksum['offset']:02X}   : 0x{checksum['stored']:08X}  {status}")
    add(f"record count      : {f['record_count']} (walked {f['records_walked']}, "
        f"table ends 0x{f['record_table_end']:04X})")
    if meta["ascii_timestamp"]:
        add(f"saved (ASCII)     : {meta['ascii_timestamp']['iso']}  "
            f"@0x{meta['ascii_timestamp']['offset']:04X}")
    if meta["bcd_timestamp"]:
        add(f"saved (BCD)       : {meta['bcd_timestamp']['iso']}  "
            f"@0x{meta['bcd_timestamp']['offset']:04X}  [{meta['bcd_timestamp']['raw']}]")
    oci = meta["oci"]
    if oci.get("found"):
        add(f"$OCI$ trailer     : @0x{oci['offset']:04X}  " + " | ".join(oci["strings"]))
        if oci["trailing_u32"]:
            add("trailing u32      : " + ", ".join(str(v) for v in oci["trailing_u32"]))
    add("")

    add("records carrying a setup variable (name resolved from BIOS defaults)")
    add("-" * 78)
    add(f"{'id':>6}{'payload':>10}{'length':>8}{'match':>8}  {'varstore':<18}{'changed':>8}")
    for row in report["records"]:
        if not row["name"]:
            continue
        match = f"{row['match_ratio']:.3f}" if row["match_ratio"] else ""
        changed = row.get("changed_bytes")
        changed_text = "" if changed is None else str(changed)
        add(f"0x{row['id']:04X}{'':>2}0x{row['payload_offset']:04X}{row['length']:>8}"
            f"{match:>8}  {row['name']:<44}{changed_text:>8}")
    add("")

    unknown = [r for r in report["records"] if not r["name"] and r["length"]]
    if unknown:
        add(f"other payloads without a BIOS default ({len(unknown)} records)")
        add("-" * 78)
        for row in unknown:
            add(f"0x{row['id']:04X}{'':>2}0x{row['payload_offset']:04X}{row['length']:>8}  "
                f"{row.get('preview', '')}")
        add("")

    key: list[dict[str, Any]] = []
    seen: dict[tuple[str, str], list[dict[str, Any]]] = {}
    for row in report["fields"]:
        name = (row["name"] or "").lower().strip()
        if not any(name == tag or name.startswith(tag) for tag in KEY_SETTINGS):
            continue
        seen.setdefault((row["varstore"] or "", name), []).append(row)
    for (_store, _name), rows in seen.items():
        key.append(rows[0])
        key[-1]["aliases"] = len(rows) - 1
    if key:
        add("key overclocking settings as stored in this profile")
        add("-" * 78)
        add(f"{'varstore':<10}{'varoff':>8}  {'setting':<44}{'value':>16}")
        for row in key:
            value = _fmt(row["value"], row["value_label"])
            flag = " *" if row["changed"] else ""
            aliases = f" (+{row['aliases']} more)" if row.get("aliases") else ""
            add(f"{row['varstore']:<10}0x{row['varoffset']:04X}{'':>4}  "
                f"{(row.get('ui_label') or row['name'] or '')[:44]:<44}{value:>16}{flag}{aliases}")
        add("")
        add("(* = differs from the BIOS factory default)")
        add("")

    if report["changes"]:
        add("every byte that differs from the BIOS factory default")
        add("-" * 78)
        current_store = None
        for change in report["changes"]:
            if change["varstore"] != current_store:
                current_store = change["varstore"]
                add(f"== {current_store}")
                add(f"{'varoff':>8}{'fileoff':>10}{'size':>6}  {'setting':<44}"
                    f"{'default':>12} -> value")
            if change["kind"] == "field":
                display = change.get("ui_label") or change["name"]
                if display:
                    name = display
                elif change.get("form"):
                    name = f"(unnamed, {change['form']})"
                else:
                    name = "(unnamed question)"
                add(f"0x{change['varoffset']:04X}{'':>4}0x{change['file_offset']:04X}"
                    f"{change['size']:>6}  {name[:44]:<44}"
                    f"{_fmt(change['default'], change['default_label']):>12}"
                    f" -> {_fmt(change['value'], change['value_label'])}")
            else:
                add(f"0x{change['varoffset']:04X}{'':>4}0x{change['file_offset']:04X}"
                    f"{change['size']:>6}  {'(no BIOS question at this offset)':<44}"
                    f"{change['default']:>12} -> {change['value']}")
        add("")
    else:
        add("no bytes differ from the BIOS factory defaults")
        add("")
    return "\n".join(out)


def render_csv(report: dict[str, Any]) -> str:
    rows = ["varstore,form,varoffset,file_offset,size,type,setting,ui_label,default,"
            "default_label,value,value_label,changed"]
    for row in report["fields"]:
        rows.append(",".join([
            row["varstore"] or "",
            _csv(row.get("form") or ""),
            f"0x{row['varoffset']:04X}",
            f"0x{row['file_offset']:04X}",
            str(row["size"]),
            row.get("type") or "",
            _csv(row["name"] or ""),
            _csv(row.get("ui_label") or ""),
            str(row["default"]),
            _csv(row.get("default_label") or ""),
            str(row["value"]),
            _csv(row.get("value_label") or ""),
            "yes" if row.get("changed") else "",
        ]))
    return "\n".join(rows)


def _csv(value: str) -> str:
    return '"' + value.replace('"', '""') + '"' if ("," in value or '"' in value) else value


# --------------------------------------------------------------------------- #
# main
# --------------------------------------------------------------------------- #

def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="Decode an MSI MsOcFile.ocb BIOS profile")
    parser.add_argument("command", choices=["inspect", "decode", "verify", "rechecksum"])
    parser.add_argument("profile", type=Path)
    parser.add_argument("--reference", type=Path,
                        default=Path(__file__).with_name("reference"),
                        help="directory holding bios_reference.json and ifr_catalog.json")
    parser.add_argument("--out", type=Path, help="write decoded output here")
    args = parser.parse_args(argv)

    data = args.profile.read_bytes()
    reference = Reference.load(args.reference)

    if args.command == "rechecksum":
        header = parse_header(data, reference)
        fixed = bytearray(data)
        value = recompute_checksum(data, header.checksum_offset)
        struct.pack_into("<I", fixed, header.checksum_offset, value)
        target = args.out or args.profile.with_suffix(".rechecksummed.ocb")
        target.write_bytes(fixed)
        print(f"checksum 0x{header.checksum_value:08X} -> 0x{value:08X}, wrote {target}")
        return 0

    if args.command == "inspect":
        header = parse_header(data, reference)
        print(f"magic        : {MAGIC.decode()}")
        print(f"board        : {header.board_id}")
        print(f"version tag  : {header.version_tag}")
        print(f"checksum     : 0x{header.checksum_value:08X} "
              f"({'VALID' if header.checksum_ok else 'INVALID'})")
        print(f"record count : {header.record_count}")
        print(f"first record : id=0x{header.first_record_id:X} "
              f"payload @0x{header.payload_offset:04X}")
        oci = data.find(OCI_MAGIC)
        print(f"$OCI$ @      : {'0x%04X' % oci if oci >= 0 else 'none'}")
        if not reference.defaults:
            print("reference    : none (run build_bios_reference.py first)")
        return 0

    if not reference.defaults:
        print(f"error: no BIOS reference data in {args.reference}; run "
              "build_bios_reference.py <bios.rom> --ifr <ifr.txt> first", file=sys.stderr)
        return 2

    report = decode(data, reference)

    if args.command == "verify":
        print(f"checksum: {'VALID' if report['file']['checksum']['valid'] else 'INVALID'}")
        named = sum(1 for r in report["records"] if r["name"])
        print(f"records : {len(report['records'])} ({named} named)")
        print(f"changes : {len(report['changes'])}")
        print(f"fields  : {len(report['fields'])}")
        return 0 if report["file"]["checksum"]["valid"] else 1

    text = render_text(report)
    print(text)
    if args.out:
        args.out.mkdir(parents=True, exist_ok=True)
        stem = args.profile.stem
        (args.out / f"{stem}.decoded.json").write_text(
            json.dumps(report, indent=1), encoding="utf-8")
        (args.out / f"{stem}.decoded.txt").write_text(text, encoding="utf-8")
        (args.out / f"{stem}.fields.csv").write_text(render_csv(report), encoding="utf-8")
        print(f"\nwritten to {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
