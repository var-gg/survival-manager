#!/usr/bin/env python3
"""Wiki-aware narrative seed builder.

Parses pindoc wiki markdown bundles (tools/raw-wiki/*.md) into the same
narrative-seed.json schema consumed by NarrativeSeedImporter.cs.

Source of truth: pindoc wiki (사용자 결정 2026-05-07).
This script supersedes narrative_build.py's git-canonical path for
narrative content. Legacy paths remain for transition.

Wiki body conventions (best-effort, graceful on variants):
- H2 scene heading:  `## \`scene_id\` — 제목` (em-dash or hyphen)
- Meta blockquote:   `> **컨텍스트**: ...`, `> **연출**: ...`, `> **분량**: ...`
- Dialogue table:    `| # | 화자 | 감정 | 대사 |` (4 column markdown table)
- Branch sub-block:  `### Branch ... \`tag\`` introduces branch-scoped lines
- Common sections:   `### Opening`, `### Common middle`, `### Common closure`, etc.
- Voice markers:     `*[voice / hook]*`, `*[voice / closure]*` inside dialogue cell
- Admin sections:    `## 개요`, `## post-launch hook`, `## v3 craft guideline 적용 메모`,
                     `## 2026-05-13 sanity check` — skipped (no backtick scene_id)

Stable line ID: stable_hash(scene_id, branch_tag, speaker_id, content_norm).
content_norm strips quotes, voice markers, whitespace runs, ellipsis variants.

Output: Temp/Narrative/narrative-seed-wiki.json (parallel to legacy seed
during transition). Once trusted, swap NarrativeSeedImporter to read this.
"""
from __future__ import annotations

import hashlib
import json
import re
import sys
from dataclasses import dataclass, field, asdict
from pathlib import Path
from typing import Optional

REPO_ROOT = Path(__file__).resolve().parent.parent
RAW_WIKI_DIR = REPO_ROOT / "tools" / "raw-wiki"
AUTHORING_MAP = REPO_ROOT / "tools" / "narrative-authoring-map.json"
OUTPUT_DIR = REPO_ROOT / "Temp" / "Narrative"
OUTPUT_FILE = OUTPUT_DIR / "narrative-seed-wiki.json"

# ---------------------------------------------------------------------------
# Regex
# ---------------------------------------------------------------------------

# `## \`scene_id\` — 제목` or `## \`scene_id\` - 제목`
SCENE_HEADING_RE = re.compile(
    r"^##\s+`([^`]+)`\s*(?:[—\-–])\s*(.+?)\s*$"
)

# `### Branch A — \`tag\`` or `### Branch — \`tag\`` or `### Branch 단일 — \`tag\``
BRANCH_HEADING_RE = re.compile(
    r"^###\s+Branch[^—\-–]*[—\-–]\s*`([^`]+)`\s*$"
)

# `### Opening`, `### Common middle`, `### Common closure`, etc.
SECTION_HEADING_RE = re.compile(r"^###\s+(.+?)\s*$")

META_LINE_RE = re.compile(r"^>\s*\*\*(.+?)\*\*\s*:\s*(.+?)\s*$")

TABLE_SEP_RE = re.compile(r"^\|[\-|:\s]+\|$")
TABLE_ROW_RE = re.compile(r"^\|.*\|\s*$")

# voice markers inside text cell
VOICE_MARKER_RE = re.compile(
    r"\*\[voice\s*/\s*([^\]]+)\]\*"
)

BACKTICK_RE = re.compile(r"`([^`]+)`")

# Korean speakers that should preserve quotes-as-is (Narrator is text-only)
NARRATOR_IDS = {"Narrator"}


# ---------------------------------------------------------------------------
# Data classes
# ---------------------------------------------------------------------------

@dataclass
class WikiLine:
    line_index: int
    speaker_alias: str
    speaker_id: str
    emotion_raw: str
    emotion_id: str
    emote_id: str
    text_ko: str
    branch_tag: str = ""
    section_label: str = ""   # "Opening", "Common middle", etc.
    voice_role: str = ""      # "hook", "closure", "명상 line", etc.
    line_id: str = ""         # stable_hash(scene_id, branch_tag, speaker_id, content_norm)
    refs: list[dict] = field(default_factory=list)  # cross-ref to other characters
                                                     # [{"alias": "단린", "id": "hero_dawn_priest",
                                                     #   "spans": [[start, end], ...]}]


@dataclass
class WikiScene:
    scene_id: str
    title: str
    artifact_slug: str
    presentation_kind: str = "DialogueScene"
    meta: dict = field(default_factory=dict)  # context / 연출 / 분량 / portrait / voice
    lines: list[WikiLine] = field(default_factory=list)
    branches: list[dict] = field(default_factory=list)  # [{tag, label, user_line, line_ids: [...]}]


@dataclass
class ExtractDiagnostic:
    code: str
    severity: str
    message: str
    source: str
    line_number: int = 0


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def load_authoring_map() -> dict:
    with open(AUTHORING_MAP, encoding="utf-8") as f:
        return json.load(f)


def normalize_content(text: str) -> str:
    """Normalize dialogue text for stable line ID hashing.

    - strip voice markers
    - unify ellipsis variants
    - collapse whitespace
    - strip outer quotes (한국어/영어/한자식)
    """
    t = VOICE_MARKER_RE.sub("", text).strip()

    # ellipsis variants
    t = t.replace("…", "...").replace("．．．", "...")
    # zero-width chars
    t = t.replace("​", "")
    # whitespace runs
    t = re.sub(r"\s+", " ", t)
    # outer quotes
    pairs = [('"', '"'), ('"', '"'), ('“', '”'),
             ("'", "'"), ("‘", "’"), ("「", "」"), ("『", "』")]
    for open_q, close_q in pairs:
        if len(t) >= 2 and t.startswith(open_q) and t.endswith(close_q):
            t = t[1:-1].strip()
            break
    return t


def extract_voice_role(text: str) -> str:
    """Pull first voice marker role out (hook / closure / 명상 line / etc.)."""
    m = VOICE_MARKER_RE.search(text)
    if not m:
        return ""
    return m.group(1).strip()


def stable_line_id(scene_id: str, branch_tag: str, speaker_id: str, content_norm: str) -> str:
    """Stable per-line identifier. Survives line insertions/deletions,
    only changes when (scene_id, branch_tag, speaker_id, content) changes."""
    h = hashlib.sha1()
    h.update(scene_id.encode("utf-8"))
    h.update(b"|")
    h.update(branch_tag.encode("utf-8"))
    h.update(b"|")
    h.update(speaker_id.encode("utf-8"))
    h.update(b"|")
    h.update(content_norm.encode("utf-8"))
    return f"ln_{h.hexdigest()[:12]}"


def is_korean_alias(s: str) -> bool:
    """True if string contains at least one Hangul syllable.
    English aliases (Dawn Priest, Mirror Cantor 등) wiki 본문에는 안 나오므로 detection 대상에서 제외."""
    return any('가' <= c <= '힣' for c in s)


def build_alias_index(speakers: dict) -> list[tuple[str, str]]:
    """speakers map → sorted alias list for greedy longest-match detection.

    Returns [(alias, character_id), ...] sorted by alias length desc.
    English aliases excluded — only Korean appears in wiki prose."""
    korean = [(alias, cid) for alias, cid in speakers.items()
              if is_korean_alias(alias)]
    # longest first so "단린 사제님" matches before "단린" alone
    korean.sort(key=lambda x: len(x[0]), reverse=True)
    return korean


def detect_character_refs(
    text: str,
    speaker_id: str,
    alias_index: list[tuple[str, str]],
) -> list[dict]:
    """Greedy longest-match substring scan for character aliases in line text.

    Returns list of {alias, id, spans: [[start, end], ...], selfRef: bool}.
    - Korean particles ('은', '이', '을', '의', '에게', '께' 등) are NOT in alias,
      so "단린은", "단린께" both match span [0, 2] and leave the particle intact.
    - Overlap avoidance: once a substring is consumed by a longer alias, shorter
      aliases will not double-claim the same span.
    - selfRef=True if speaker_id == ref.id (vocative/3인칭 자기 호명).
    """
    if not text or not alias_index:
        return []

    occupied = [False] * len(text)
    by_id: dict[str, dict] = {}

    for alias, char_id in alias_index:
        if not alias or len(alias) > len(text):
            continue
        start = 0
        while True:
            idx = text.find(alias, start)
            if idx == -1:
                break
            end = idx + len(alias)
            # overlap check
            if any(occupied[i] for i in range(idx, end)):
                start = idx + 1
                continue
            for i in range(idx, end):
                occupied[i] = True
            entry = by_id.setdefault(char_id, {
                "alias": alias,
                "id": char_id,
                "spans": [],
                "selfRef": char_id == speaker_id,
            })
            entry["spans"].append([idx, end])
            start = end

    return list(by_id.values())


def split_table_row(row: str) -> list[str]:
    cells = [c.strip() for c in row.split("|")]
    if cells and cells[0] == "":
        cells = cells[1:]
    if cells and cells[-1] == "":
        cells = cells[:-1]
    return cells


# ---------------------------------------------------------------------------
# Parser state machine
# ---------------------------------------------------------------------------

class WikiParser:
    def __init__(self, authoring_map: dict):
        self.speakers = authoring_map.get("speakers", {})
        self.emotions = authoring_map.get("emotions", {})
        self.kind_aliases = authoring_map.get("presentationKindAliases", {})
        self.alias_index = build_alias_index(self.speakers)
        self.scenes: dict[str, WikiScene] = {}
        self.diagnostics: list[ExtractDiagnostic] = []

    def parse_file(self, md_path: Path) -> None:
        text = md_path.read_text(encoding="utf-8")
        artifact_slug = md_path.stem
        lines = text.splitlines()
        i = 0
        current_scene: Optional[WikiScene] = None
        current_branch = ""
        current_section = ""
        in_table = False
        table_header_seen = False

        while i < len(lines):
            raw = lines[i]
            line_num = i + 1

            # ---- H2: scene heading or admin heading ----
            if raw.startswith("## "):
                m = SCENE_HEADING_RE.match(raw)
                if m:
                    scene_id = m.group(1).strip()
                    title = m.group(2).strip()
                    current_scene = WikiScene(
                        scene_id=scene_id,
                        title=title,
                        artifact_slug=artifact_slug,
                    )
                    self.scenes[scene_id] = current_scene
                    current_branch = ""
                    current_section = ""
                    in_table = False
                    table_header_seen = False
                else:
                    # admin H2 — close current scene, skip
                    current_scene = None
                    current_branch = ""
                    current_section = ""
                    in_table = False
                    table_header_seen = False
                i += 1
                continue

            # ---- H3: branch or common section ----
            if raw.startswith("### "):
                if current_scene is None:
                    i += 1
                    continue
                bm = BRANCH_HEADING_RE.match(raw)
                if bm:
                    current_branch = bm.group(1).strip()
                    current_section = ""
                else:
                    sm = SECTION_HEADING_RE.match(raw)
                    current_section = sm.group(1).strip() if sm else ""
                    # entering Opening / Common closure / etc. resets branch only if it's not "Branch A"-like
                    if not current_section.lower().startswith("branch"):
                        # Common sections apply to scene-wide lines (branch cleared)
                        if "common" in current_section.lower() or current_section in ("Opening", "본문"):
                            current_branch = ""
                in_table = False
                table_header_seen = False
                i += 1
                continue

            # ---- H1, H4+: ignored ----
            if raw.startswith("# ") or raw.startswith("#### "):
                i += 1
                continue

            # ---- Meta blockquote ----
            if raw.startswith(">") and current_scene is not None:
                mm = META_LINE_RE.match(raw)
                if mm:
                    key = mm.group(1).strip()
                    value = mm.group(2).strip()
                    current_scene.meta[key] = value
                i += 1
                continue

            # ---- Table row ----
            if current_scene is not None and raw.startswith("|"):
                if TABLE_SEP_RE.match(raw):
                    i += 1
                    continue
                cells = split_table_row(raw)
                # detect header row
                if not table_header_seen:
                    if cells and cells[0] == "#":
                        table_header_seen = True
                        in_table = True
                        i += 1
                        continue
                    # also accept branch tag table (used for user choice display)
                    if cells and cells[0] in ("분기", "tag"):
                        i += 1
                        continue
                if in_table:
                    if len(cells) < 4:
                        i += 1
                        continue
                    self._parse_dialogue_row(
                        cells, current_scene, current_branch,
                        current_section, line_num)
                i += 1
                continue

            # ---- Blank line: end table block ----
            if not raw.strip():
                in_table = False
                table_header_seen = False
            i += 1

    def _parse_dialogue_row(
        self,
        cells: list[str],
        scene: WikiScene,
        branch_tag: str,
        section_label: str,
        source_line: int,
    ) -> None:
        index_cell = cells[0]
        speaker_alias = cells[1].strip()
        emotion_raw = cells[2].strip()
        text_raw = cells[3].strip()

        # line_index: digits possibly suffixed with letter (e.g., "3a", "4b", "12")
        m_idx = re.match(r"^(\d+)([a-zA-Z\-\.\d]*)$", index_cell)
        if not m_idx:
            # may be the user-choice-line table (rows like "| A | tag | text |")
            return

        line_index = int(m_idx.group(1))

        speaker_id = self.speakers.get(speaker_alias, "")
        if not speaker_id:
            # heuristic: Narrator and Narrator-like fallback
            if speaker_alias in ("Narrator", "Narrator"):
                speaker_id = "Narrator"
            elif speaker_alias.endswith("Narrator"):
                speaker_id = "Narrator"
            else:
                # unknown speaker — keep raw alias as ID so importer can flag, but record diag
                speaker_id = f"unknown:{speaker_alias}"
                self.diagnostics.append(ExtractDiagnostic(
                    code="WIKI-E020",
                    severity="Error",
                    message=f"Unknown speaker alias '{speaker_alias}' in scene '{scene.scene_id}'",
                    source=scene.artifact_slug,
                    line_number=source_line,
                ))

        emo_info = self.emotions.get(emotion_raw)
        if emo_info:
            emotion_id = emo_info.get("emotionId", "none")
            emote_id = emo_info.get("emoteId", "Default")
        else:
            # raw passthrough policy (see authoring-map.emotionFallback)
            emotion_id = emotion_raw or "none"
            emote_id = "Default"

        voice_role = extract_voice_role(text_raw)
        content_norm = normalize_content(text_raw)
        refs = detect_character_refs(
            text=text_raw, speaker_id=speaker_id, alias_index=self.alias_index)

        line = WikiLine(
            line_index=line_index,
            speaker_alias=speaker_alias,
            speaker_id=speaker_id,
            emotion_raw=emotion_raw,
            emotion_id=emotion_id,
            emote_id=emote_id,
            text_ko=text_raw,
            branch_tag=branch_tag,
            section_label=section_label,
            voice_role=voice_role,
            refs=refs,
        )
        line.line_id = stable_line_id(
            scene.scene_id, branch_tag, speaker_id, content_norm)
        scene.lines.append(line)


# ---------------------------------------------------------------------------
# Output
# ---------------------------------------------------------------------------

def build_seed(scenes: dict[str, WikiScene]) -> dict:
    """Emit narrative-seed-wiki.json.

    Schema-compatible with NarrativeSeedImporter.cs (NarrativeSeedManifest):
    - top-level: version / sourceHash / storyEvents / dialogueSequences /
      presentations / archiveEntries / diagnostics
    - dialogueSequences[].lines[] expose `text` (KO source) consumed by the
      importer's ParseLines; `en` is the translation slot, filled later.
    - extended fields (lineId, branchTag, sectionLabel, voiceRole, refs) and
      the `lineIndex` map are ignored by the current importer but preserved
      for the theater-mode viewer and a future extended importer.
    """
    dialogue_sequences = []
    presentations = []
    line_index_map = {}  # scene_id -> [line_id, ...] in order

    for scene_id, scene in scenes.items():
        lines_out = []
        for ln in scene.lines:
            lines_out.append({
                "lineId": ln.line_id,
                "lineIndex": ln.line_index,
                "speakerAlias": ln.speaker_alias,
                "speakerId": ln.speaker_id,
                "emotionRaw": ln.emotion_raw,
                "emotionId": ln.emotion_id,
                "emoteId": ln.emote_id,
                "text": ln.text_ko,
                "en": "",
                "branchTag": ln.branch_tag,
                "sectionLabel": ln.section_label,
                "voiceRole": ln.voice_role,
                "refs": ln.refs,
            })
        seq = {
            "sequenceId": f"dialogue_seq_{scene.scene_id}",
            "presentationKey": scene.scene_id,
            "presentationKind": scene.presentation_kind,
            "artifactSlug": scene.artifact_slug,
            "title": scene.title,
            "meta": scene.meta,
            "lines": lines_out,
        }
        dialogue_sequences.append(seq)

        presentations.append({
            "presentationKey": scene.scene_id,
            "kind": scene.presentation_kind,
            "runtimeContext": scene.meta.get("런타임 컨텍스트", "None"),
            "title": scene.title,
            "body": None,
            "iconId": None,
        })
        line_index_map[scene_id] = [ln.line_id for ln in scene.lines]

    return {
        "version": 1,
        "source": "pindoc-wiki",
        "sourceHash": "",
        "storyEvents": [],
        "dialogueSequences": dialogue_sequences,
        "presentations": presentations,
        "archiveEntries": [],
        "lineIndex": line_index_map,
    }


# ---------------------------------------------------------------------------
# main
# ---------------------------------------------------------------------------

def compute_source_hash(md_files: list[Path]) -> str:
    """Stable hash over all raw-wiki source files (name + bytes).

    Lets the importer detect whether the seed changed since the last import."""
    h = hashlib.sha1()
    for md in sorted(md_files, key=lambda p: p.name):
        h.update(md.name.encode("utf-8"))
        h.update(b"\0")
        h.update(md.read_bytes())
        h.update(b"\0")
    return h.hexdigest()[:16]


def main() -> int:
    if not RAW_WIKI_DIR.exists():
        print(f"[wiki-extract] raw-wiki dir not found: {RAW_WIKI_DIR}", file=sys.stderr)
        print(f"[wiki-extract] dump pindoc artifacts to that dir first.", file=sys.stderr)
        return 1

    authoring_map = load_authoring_map()
    parser = WikiParser(authoring_map)

    md_files = sorted(RAW_WIKI_DIR.glob("*.md"))
    if not md_files:
        print(f"[wiki-extract] no .md files in {RAW_WIKI_DIR}", file=sys.stderr)
        return 1

    for md in md_files:
        parser.parse_file(md)

    seed = build_seed(parser.scenes)
    seed["sourceHash"] = compute_source_hash(md_files)
    seed["diagnostics"] = [asdict(d) for d in parser.diagnostics]

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    with open(OUTPUT_FILE, "w", encoding="utf-8") as f:
        json.dump(seed, f, ensure_ascii=False, indent=2)

    line_total = sum(len(s.lines) for s in parser.scenes.values())
    voiced_total = sum(
        1 for s in parser.scenes.values()
        for ln in s.lines if ln.voice_role
    )
    ref_total = sum(
        len(ln.refs)
        for s in parser.scenes.values()
        for ln in s.lines
    )
    # voice line catalog by speaker (Japanese TTS targeting)
    voice_by_speaker: dict[str, int] = {}
    for s in parser.scenes.values():
        for ln in s.lines:
            if ln.voice_role:
                voice_by_speaker[ln.speaker_id] = voice_by_speaker.get(ln.speaker_id, 0) + 1

    err = sum(1 for d in parser.diagnostics if d.severity == "Error")
    warn = sum(1 for d in parser.diagnostics if d.severity == "Warning")

    print(f"[wiki-extract] {OUTPUT_FILE}")
    print(f"  source files:   {len(md_files)}")
    print(f"  scenes:         {len(parser.scenes)}")
    print(f"  total lines:    {line_total}")
    print(f"  voiced lines:   {voiced_total}")
    print(f"  cross-refs:     {ref_total}")
    if voice_by_speaker:
        print(f"  voice catalog (for JP TTS):")
        for sp, cnt in sorted(voice_by_speaker.items(), key=lambda x: -x[1]):
            print(f"    {sp:30s} {cnt}")
    print(f"  diagnostics:    {err} errors, {warn} warnings")
    return 0 if err == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
