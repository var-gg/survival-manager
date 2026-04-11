#!/usr/bin/env python3
"""Narrative authoring validator.

Validates master-script.md and dialogue-event-schema.md for structural
correctness, cross-references, alias validity, and prose constraints.
"""
import json
import os
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
MASTER_SCRIPT = REPO_ROOT / "docs" / "02_design" / "narrative" / "master-script.md"
SCHEMA_DOC = REPO_ROOT / "docs" / "02_design" / "narrative" / "dialogue-event-schema.md"
AUTHORING_MAP = REPO_ROOT / "tools" / "narrative-authoring-map.json"
OUTPUT_DIR = REPO_ROOT / "Temp" / "Narrative"
OUTPUT_FILE = OUTPUT_DIR / "narrative-validate.json"

HEADING_RE = re.compile(r"^###\s+`([^`]+)`\s*(?:—|--)\s*(.+)$")
META_KIND_RE = re.compile(r">\s*\*\*연출\*\*:\s*(.+)$")
TABLE_SEP_RE = re.compile(r"^\|[-|:\s]+\|$")
BACKTICK_RE = re.compile(r"`([^`]+)`")
SCHEMA_HEADER_RE = re.compile(r"^\|\s*story_event_id\s*\|", re.IGNORECASE)
SCHEMA_SEQ_TABLE_RE = re.compile(r"^##\s+대사 시퀀스 표", re.IGNORECASE)

NARRATOR_FORBIDDEN = ["무겁다", "무섭다", "두렵다", "슬프다", "무거운", "무서운", "두려운", "슬픈",
                       "가장 ", "의미하는", "상징하는", "뜻하는"]


def load_authoring_map():
    with open(AUTHORING_MAP, encoding="utf-8") as f:
        return json.load(f)


def diag(code, severity, message, source_path, line_number):
    return {"code": code, "severity": severity, "message": message,
            "sourcePath": str(source_path), "lineNumber": line_number}


def strip_backticks(val):
    m = BACKTICK_RE.search(val.strip())
    return m.group(1) if m else val.strip().strip("`")


def validate_master_script(authoring_map, diagnostics):
    if not MASTER_SCRIPT.exists():
        diagnostics.append(diag("NAR-V001", "Error", "master-script.md not found", str(MASTER_SCRIPT), 0))
        return {}

    speakers = authoring_map.get("speakers", {})
    emotions = authoring_map.get("emotions", {})
    kind_aliases = authoring_map.get("presentationKindAliases", {})

    lines_raw = MASTER_SCRIPT.read_text(encoding="utf-8").splitlines()
    sections = {}
    i = 0

    while i < len(lines_raw):
        line = lines_raw[i]
        m = HEADING_RE.match(line)
        if not m:
            i += 1
            continue

        pkey = m.group(1).strip()
        line_num = i + 1
        sections[pkey] = line_num
        i += 1

        kind_raw = None
        while i < len(lines_raw) and lines_raw[i].startswith(">"):
            mk = META_KIND_RE.match(lines_raw[i])
            if mk:
                kind_raw = mk.group(1).strip()
            i += 1

        if kind_raw and kind_raw not in kind_aliases:
            diagnostics.append(diag("NAR-V010", "Error",
                f"Unknown presentation kind '{kind_raw}' in section '{pkey}'",
                str(MASTER_SCRIPT), line_num))

        kind_enum = kind_aliases.get(kind_raw, "")
        is_dialogue = kind_enum in ("DialogueScene", "DialogueOverlay")

        if not is_dialogue:
            continue

        in_table = False
        unique_speakers = set()
        while i < len(lines_raw):
            row = lines_raw[i]
            if not row.strip() and in_table:
                i += 1
                break
            if row.startswith("---") or row.startswith("###"):
                break
            if not row.startswith("|"):
                i += 1
                continue
            if TABLE_SEP_RE.match(row):
                i += 1
                continue

            cells = [c.strip() for c in row.split("|")]
            cells = [c for c in cells if c != ""]

            if not in_table:
                if cells and cells[0] == "#":
                    in_table = True
                i += 1
                continue

            if len(cells) < 4:
                i += 1
                continue

            speaker_alias = cells[1].strip()
            emotion_raw = cells[2].strip()
            text = cells[3].strip()
            row_line = i + 1

            if speaker_alias not in speakers:
                diagnostics.append(diag("NAR-V020", "Error",
                    f"Unknown speaker '{speaker_alias}' in '{pkey}'",
                    str(MASTER_SCRIPT), row_line))

            if emotion_raw and emotion_raw != "—" and emotion_raw not in emotions:
                diagnostics.append(diag("NAR-V021", "Error",
                    f"Unknown emotion '{emotion_raw}' in '{pkey}'",
                    str(MASTER_SCRIPT), row_line))

            is_narrator = speakers.get(speaker_alias) == "Narrator"
            if is_narrator:
                for forbidden in NARRATOR_FORBIDDEN:
                    if forbidden in text:
                        diagnostics.append(diag("NAR-V030", "Error",
                            f"Narrator forbidden pattern '{forbidden}' in '{pkey}'",
                            str(MASTER_SCRIPT), row_line))
                        break

            if not is_narrator:
                unique_speakers.add(speaker_alias)

            i += 1

        if kind_enum == "DialogueScene" and len(unique_speakers) > 4:
            diagnostics.append(diag("NAR-V040", "Error",
                f"dialogue-scene '{pkey}' has {len(unique_speakers)} non-narrator speakers (max 4)",
                str(MASTER_SCRIPT), sections[pkey]))

        if kind_enum == "DialogueOverlay" and len(unique_speakers) > 2:
            diagnostics.append(diag("NAR-V041", "Warning",
                f"dialogue-overlay '{pkey}' has {len(unique_speakers)} non-narrator speakers (recommend max 2)",
                str(MASTER_SCRIPT), sections[pkey]))

    return sections


def validate_schema(authoring_map, master_sections, diagnostics):
    if not SCHEMA_DOC.exists():
        diagnostics.append(diag("NAR-V002", "Error", "dialogue-event-schema.md not found", str(SCHEMA_DOC), 0))
        return

    cond_aliases = authoring_map.get("conditionAliases", {})
    effect_aliases = authoring_map.get("effectAliases", {})

    lines_raw = SCHEMA_DOC.read_text(encoding="utf-8").splitlines()
    i = 0
    in_seq_table = False

    while i < len(lines_raw):
        line = lines_raw[i]

        if SCHEMA_SEQ_TABLE_RE.match(line):
            in_seq_table = True
            i += 1
            continue
        if in_seq_table:
            i += 1
            continue

        if not SCHEMA_HEADER_RE.match(line):
            i += 1
            continue

        i += 1
        if i < len(lines_raw) and TABLE_SEP_RE.match(lines_raw[i]):
            i += 1

        while i < len(lines_raw):
            row = lines_raw[i]
            if not row.startswith("|"):
                break
            if TABLE_SEP_RE.match(row):
                i += 1
                continue

            cells = [c.strip() for c in row.split("|")]
            cells = [c for c in cells if c != ""]
            if len(cells) < 7:
                i += 1
                continue

            row_line = i + 1
            pkey = strip_backticks(cells[6])
            conditions_raw = cells[4].strip()
            effects_raw = cells[5].strip()

            if pkey and pkey != "—" and pkey not in master_sections:
                diagnostics.append(diag("NAR-V050", "Warning",
                    f"Schema presentation_key '{pkey}' not found in master-script.md",
                    str(SCHEMA_DOC), row_line))

            if conditions_raw and conditions_raw != "—":
                for part in conditions_raw.split(","):
                    part = strip_backticks(part.strip())
                    kind = part.split(":")[0].strip() if ":" in part else part.strip()
                    if kind and kind not in cond_aliases:
                        diagnostics.append(diag("NAR-V060", "Error",
                            f"Unknown condition kind '{kind}'",
                            str(SCHEMA_DOC), row_line))

            if effects_raw and effects_raw != "—":
                for part in effects_raw.split(","):
                    part = strip_backticks(part.strip())
                    kind = part.split(":")[0].strip() if ":" in part else part.strip()
                    if kind and kind not in effect_aliases:
                        diagnostics.append(diag("NAR-V061", "Error",
                            f"Unknown effect kind '{kind}'",
                            str(SCHEMA_DOC), row_line))

            i += 1


def main():
    authoring_map = load_authoring_map()
    diagnostics = []

    master_sections = validate_master_script(authoring_map, diagnostics)
    validate_schema(authoring_map, master_sections, diagnostics)

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    with open(OUTPUT_FILE, "w", encoding="utf-8") as f:
        json.dump({"diagnostics": diagnostics}, f, ensure_ascii=False, indent=2)

    errors = sum(1 for d in diagnostics if d["severity"] == "Error")
    warnings = sum(1 for d in diagnostics if d["severity"] == "Warning")

    print(f"[narrative-validate] {errors} errors, {warnings} warnings")
    for d in diagnostics:
        print(f"  [{d['severity']}] {d['code']}: {d['message']} ({d['sourcePath']}:{d['lineNumber']})")

    return 1 if errors > 0 else 0


if __name__ == "__main__":
    sys.exit(main())
