#!/usr/bin/env python3
"""Narrative seed manifest builder.

Parses master-script.md and dialogue-event-schema.md to produce
Temp/Narrative/narrative-seed.json for the Unity NarrativeSeedImporter.
"""
import hashlib
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
OUTPUT_FILE = OUTPUT_DIR / "narrative-seed.json"

DIALOGUE_SCENE_PREFIX = "dialogue_scene_"
DIALOGUE_OVERLAY_PREFIX = "dialogue_overlay_"
DIALOGUE_SEQ_PREFIX = "dialogue_seq_"
STORY_CARD_PREFIX = "story_card_"
TOAST_PREFIX = "toast_"

HEADING_RE = re.compile(r"^###\s+`([^`]+)`\s*(?:—|--)\s*(.+)$")
META_KIND_RE = re.compile(r">\s*\*\*연출\*\*:\s*(.+)$")
META_RUNTIME_CTX_RE = re.compile(r">\s*\*\*런타임 컨텍스트\*\*:\s*(.+)$")
TABLE_SEP_RE = re.compile(r"^\|[-|:\s]+\|$")
BACKTICK_RE = re.compile(r"`([^`]+)`")
SCHEMA_HEADER_RE = re.compile(
    r"^\|\s*story_event_id\s*\|", re.IGNORECASE
)
SCHEMA_SEQ_TABLE_RE = re.compile(r"^##\s+대사 시퀀스 표", re.IGNORECASE)


def load_authoring_map():
    with open(AUTHORING_MAP, encoding="utf-8") as f:
        return json.load(f)


def compute_source_hash(*paths):
    h = hashlib.sha256()
    for p in paths:
        if p.exists():
            h.update(p.read_bytes())
    return h.hexdigest()


def strip_backticks(val: str) -> str:
    m = BACKTICK_RE.search(val.strip())
    return m.group(1) if m else val.strip().strip("`")


def strip_outer_quotes(text: str) -> str:
    t = text.strip()
    if len(t) >= 2:
        if (t[0] == "\u201c" and t[-1] == "\u201d") or \
           (t[0] == '"' and t[-1] == '"') or \
           (t[0] == "\u300c" and t[-1] == "\u300d"):
            return t[1:-1]
    return t


def infer_presentation_kind(key: str) -> str:
    if key.startswith(DIALOGUE_SCENE_PREFIX):
        return "DialogueScene"
    if key.startswith(DIALOGUE_OVERLAY_PREFIX):
        return "DialogueOverlay"
    if key.startswith(STORY_CARD_PREFIX):
        return "StoryCard"
    if key.startswith(TOAST_PREFIX):
        return "ToastBanner"
    return "ToastBanner"


def to_sequence_id(key: str) -> str:
    if key.startswith(DIALOGUE_SCENE_PREFIX):
        return DIALOGUE_SEQ_PREFIX + key[len(DIALOGUE_SCENE_PREFIX):]
    if key.startswith(DIALOGUE_OVERLAY_PREFIX):
        return DIALOGUE_SEQ_PREFIX + key[len(DIALOGUE_OVERLAY_PREFIX):]
    return key


# ---------------------------------------------------------------------------
# master-script.md parser
# ---------------------------------------------------------------------------
def parse_master_script(authoring_map, diagnostics):
    if not MASTER_SCRIPT.exists():
        diagnostics.append(make_diag("NAR-E001", "Error",
                                     f"master-script.md not found at {MASTER_SCRIPT}",
                                     str(MASTER_SCRIPT), 0))
        return {}, {}

    speakers = authoring_map.get("speakers", {})
    emotions = authoring_map.get("emotions", {})
    kind_aliases = authoring_map.get("presentationKindAliases", {})

    sequences = {}
    presentations = {}

    lines_raw = MASTER_SCRIPT.read_text(encoding="utf-8").splitlines()
    i = 0
    while i < len(lines_raw):
        line = lines_raw[i]
        m = HEADING_RE.match(line)
        if not m:
            i += 1
            continue

        pkey = m.group(1).strip()
        display_title = m.group(2).strip()
        i += 1

        kind_raw = None
        runtime_ctx = "None"
        while i < len(lines_raw) and lines_raw[i].startswith(">"):
            mk = META_KIND_RE.match(lines_raw[i])
            if mk:
                kind_raw = mk.group(1).strip()
            mr = META_RUNTIME_CTX_RE.match(lines_raw[i])
            if mr:
                runtime_ctx = mr.group(1).strip()
            i += 1

        kind_enum = kind_aliases.get(kind_raw, kind_raw) if kind_raw else infer_presentation_kind(pkey)

        if kind_enum in ("DialogueScene", "DialogueOverlay"):
            seq_lines, i = _parse_dialogue_table(
                lines_raw, i, pkey, speakers, emotions, diagnostics)
            seq_id = to_sequence_id(pkey)
            sequences[pkey] = {
                "sequenceId": seq_id,
                "presentationKey": pkey,
                "presentationKind": kind_enum,
                "runtimeContext": runtime_ctx,
                "lines": seq_lines,
            }
        presentations[pkey] = {
            "presentationKey": pkey,
            "kind": kind_enum,
            "runtimeContext": runtime_ctx,
            "title": display_title,
            "body": None,
            "iconId": None,
        }

    return sequences, presentations


def _parse_dialogue_table(lines_raw, start, pkey, speakers, emotions, diags):
    i = start
    header_found = False
    result = []

    while i < len(lines_raw):
        line = lines_raw[i]
        if not line.strip():
            i += 1
            if header_found:
                break
            continue
        if line.startswith("---"):
            i += 1
            break
        if line.startswith("###"):
            break
        if not line.startswith("|"):
            i += 1
            continue
        if TABLE_SEP_RE.match(line):
            i += 1
            continue
        cells = [c.strip() for c in line.split("|")]
        cells = [c for c in cells if c != ""]

        if not header_found:
            if cells and cells[0] == "#":
                header_found = True
            i += 1
            continue

        if len(cells) < 4:
            i += 1
            continue

        try:
            line_idx = int(cells[0])
        except ValueError:
            i += 1
            continue

        speaker_alias = cells[1].strip()
        emotion_raw = cells[2].strip()
        text_raw = cells[3].strip()

        speaker_id = speakers.get(speaker_alias, speaker_alias)
        is_narrator = speaker_id == "Narrator"

        emo_info = emotions.get(emotion_raw, emotions.get("", {"emotionId": "none", "emoteId": "Default"}))
        emotion_id = emo_info.get("emotionId", "none")
        emote_id = emo_info.get("emoteId", "Default")

        if not is_narrator:
            text_raw = strip_outer_quotes(text_raw)

        result.append({
            "lineIndex": line_idx,
            "speakerAlias": speaker_alias,
            "speakerId": speaker_id,
            "emotionId": emotion_id,
            "emoteId": emote_id,
            "text": text_raw,
        })
        i += 1

    return result, i


# ---------------------------------------------------------------------------
# dialogue-event-schema.md parser
# ---------------------------------------------------------------------------
def parse_schema(authoring_map, diagnostics):
    if not SCHEMA_DOC.exists():
        diagnostics.append(make_diag("NAR-E002", "Error",
                                     f"dialogue-event-schema.md not found at {SCHEMA_DOC}",
                                     str(SCHEMA_DOC), 0))
        return []

    cond_aliases = authoring_map.get("conditionAliases", {})
    effect_aliases = authoring_map.get("effectAliases", {})

    lines_raw = SCHEMA_DOC.read_text(encoding="utf-8").splitlines()
    events = []
    source_order = 0
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

            event_id = strip_backticks(cells[0])
            moment = strip_backticks(cells[1])
            try:
                priority = int(cells[2].strip())
            except ValueError:
                priority = 0
            once_policy = strip_backticks(cells[3])
            conditions_raw = cells[4].strip()
            effects_raw = cells[5].strip()
            pkey = strip_backticks(cells[6])

            conditions = _parse_conditions(conditions_raw, cond_aliases)
            effects = _parse_effects(effects_raw, effect_aliases)

            chapter_id = ""
            site_id = ""
            for c in conditions:
                if c["kindToken"] == "ChapterIs":
                    chapter_id = c["operandA"]
                elif c["kindToken"] == "SiteIs":
                    site_id = c["operandA"]

            p_kind = infer_presentation_kind(pkey)

            if pkey and pkey != "—":
                effects.append({
                    "kindToken": "EnqueuePresentation",
                    "payload": p_kind,
                })

            events.append({
                "eventId": event_id,
                "chapterId": chapter_id,
                "siteId": site_id,
                "moment": moment,
                "priority": priority,
                "oncePolicy": once_policy,
                "presentationKey": pkey,
                "presentationKind": p_kind,
                "conditions": conditions,
                "effects": effects,
                "sourceOrder": source_order,
            })
            source_order += 1
            i += 1

    return events


def _parse_conditions(raw, aliases):
    if not raw or raw == "—":
        return []
    parts = [p.strip() for p in raw.split(",")]
    result = []
    for part in parts:
        part = strip_backticks(part)
        if ":" in part:
            kind, operand = part.split(":", 1)
        else:
            kind, operand = part, ""
        kind = aliases.get(kind.strip(), kind.strip())
        result.append({"kindToken": kind, "operandA": operand.strip()})
    return result


def _parse_effects(raw, aliases):
    if not raw or raw == "—":
        return []
    parts = [p.strip() for p in raw.split(",")]
    result = []
    for part in parts:
        part = strip_backticks(part)
        if ":" in part:
            kind, payload = part.split(":", 1)
        else:
            kind, payload = part, ""
        kind = aliases.get(kind.strip(), kind.strip())
        result.append({"kindToken": kind, "payload": payload.strip()})
    return result


# ---------------------------------------------------------------------------
# archive entries builder
# ---------------------------------------------------------------------------
def build_archive_entries(events, presentations):
    entries = []
    for ev in events:
        pkey = ev.get("presentationKey", "")
        pres = presentations.get(pkey, {})
        entries.append({
            "eventId": ev["eventId"],
            "chapterId": ev.get("chapterId", ""),
            "siteId": ev.get("siteId", ""),
            "presentationKey": pkey,
            "kind": ev.get("presentationKind", "ToastBanner"),
            "runtimeContext": pres.get("runtimeContext", "None"),
            "displayTitle": pres.get("title", pkey),
            "sourceOrder": ev.get("sourceOrder", 0),
        })
    return entries


def make_diag(code, severity, message, source_path, line_number):
    return {
        "code": code,
        "severity": severity,
        "message": message,
        "sourcePath": source_path,
        "lineNumber": line_number,
    }


def main():
    authoring_map = load_authoring_map()
    diagnostics = []

    source_hash = compute_source_hash(MASTER_SCRIPT, SCHEMA_DOC, AUTHORING_MAP)

    sequences, presentations = parse_master_script(authoring_map, diagnostics)
    events = parse_schema(authoring_map, diagnostics)

    for ev in events:
        pkey = ev.get("presentationKey", "")
        if pkey and pkey not in presentations:
            diagnostics.append(make_diag(
                "NAR-W010", "Warning",
                f"Schema event '{ev['eventId']}' references presentation_key "
                f"'{pkey}' not found in master-script.md",
                str(SCHEMA_DOC), 0))

    archive_entries = build_archive_entries(events, presentations)

    manifest = {
        "version": 1,
        "sourceHash": source_hash,
        "storyEvents": events,
        "dialogueSequences": list(sequences.values()),
        "presentations": list(presentations.values()),
        "archiveEntries": archive_entries,
        "diagnostics": diagnostics,
    }

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    with open(OUTPUT_FILE, "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)

    error_count = sum(1 for d in diagnostics if d["severity"] == "Error")
    warn_count = sum(1 for d in diagnostics if d["severity"] == "Warning")

    print(f"[narrative-build] Generated {OUTPUT_FILE}")
    print(f"  events: {len(events)}")
    print(f"  sequences: {len(sequences)}")
    print(f"  presentations: {len(presentations)}")
    print(f"  archive entries: {len(archive_entries)}")
    print(f"  diagnostics: {error_count} errors, {warn_count} warnings")

    return 1 if error_count > 0 else 0


if __name__ == "__main__":
    sys.exit(main())
