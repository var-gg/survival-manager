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

Output: Logs/Narrative/narrative-seed-wiki.json. Logs/ 는 Unity batchmode cold
launch 시 Temp/ 처럼 비워지지 않아 headless narrative-build가 seed를 잃지 않는다.
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
EVENT_MAP = REPO_ROOT / "tools" / "narrative-event-map.json"
VISUAL_MAP = REPO_ROOT / "tools" / "narrative-visual-map.json"
AUDIO_MAP = REPO_ROOT / "tools" / "narrative-audio-map.json"
OUTPUT_DIR = REPO_ROOT / "Logs" / "Narrative"
OUTPUT_FILE = OUTPUT_DIR / "narrative-seed-wiki.json"

# ---------------------------------------------------------------------------
# Regex
# ---------------------------------------------------------------------------

# `## \`scene_id\` — 제목` or `## \`scene_id\` - 제목`
SCENE_HEADING_RE = re.compile(
    r"^##\s+`([^`]+)`\s*(?:[—\-–])\s*(.+?)\s*$"
)

# pindoc 그룹형/스펙형 heading: `## ch1 intro — 재의 문 너머로 (`cutscene_chapter_intro_ashen_frontier`)`
# scene_id 가 제목 끝 괄호 안 백틱에 들어 있는 형식. SCENE_HEADING_RE 가 우선, 이건 fallback.
SCENE_HEADING_PAREN_RE = re.compile(
    r"^##\s+(.+?)\s*\(`([^`]+)`\)\s*$"
)
# 제목 앞 `ch1 intro — ` 류 authoring prefix 제거용
SCENE_TITLE_PREFIX_RE = re.compile(r"^ch[1-5]\s+\S+\s*[—\-–]\s*")

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


def clean_display_text(text: str) -> str:
    """대사창에 그대로 노출할 표시용 텍스트로 정리한다.

    제작 주석(voice 마커), 선두 연기 지문 괄호, 바깥 따옴표 한 겹을 제거한다.
    presenter가 비-내레이터 줄을 다시 따옴표로 감싸므로 바깥 따옴표는 여기서 뗀다.
    line-ID 해시는 normalize_content를 계속 쓰므로 ID 안정성에는 영향이 없다."""
    t = VOICE_MARKER_RE.sub("", text).strip()
    t = t.replace("…", "...").replace("​", "")
    t = re.sub(r"\s+", " ", t).strip()
    t = re.sub(r"^\([^)]{1,24}\)\s*", "", t).strip()
    for open_q, close_q in (('"', '"'), ("“", "”"),
                            ("「", "」"), ("『", "』")):
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
                scene_id = None
                title = None
                if m:
                    scene_id = m.group(1).strip()
                    title = m.group(2).strip()
                else:
                    m2 = SCENE_HEADING_PAREN_RE.match(raw)
                    if m2 and "_" in m2.group(2):
                        scene_id = m2.group(2).strip()
                        title = SCENE_TITLE_PREFIX_RE.sub("", m2.group(1).strip()).strip()
                if scene_id:
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

def to_sequence_id(scene_id: str) -> str:
    """presentation scene_id를 런타임 DialogueSequence stable id로 변환한다.

    런타임 `NarrativePresentationKeyNormalizer.ToDialogueSequenceId`와 정확히
    같은 규약을 따른다: `dialogue_scene_`/`dialogue_overlay_` prefix를 떼고
    `dialogue_seq_`로 교체한다. 그 외 prefix(`story_card_`/`cutscene_`/`toast_`,
    그리고 `dialogue_town_`/`dialogue_atlas_`/`dialogue_reward_` 같은 변종)는
    그대로 둔다 — 런타임이 presentationKey로 1차 직접 조회하기 때문이다.

    버그 이력: 이전 구현은 `f"dialogue_seq_{scene_id}"`로 무조건 prefix를
    덧붙여 `dialogue_seq_dialogue_scene_ashen_gate_intro` 같은 이중 prefix를
    만들었다. 런타임 fallback이 기대하는 `dialogue_seq_ashen_gate_intro`와
    영원히 어긋나, StoryEvent가 신규 시퀀스를 한 번도 resolve하지 못했다.
    """
    for src in ("dialogue_scene_", "dialogue_overlay_"):
        if scene_id.startswith(src):
            return "dialogue_seq_" + scene_id[len(src):]
    return scene_id


def load_event_map() -> list[dict]:
    """Git-tracked canonical event manifest를 읽는다(ADR-0025 규칙 2).

    파일이 없으면 빈 목록을 반환해 dialogue-only 추출도 계속 동작하게 둔다."""
    if not EVENT_MAP.exists():
        return []
    with open(EVENT_MAP, encoding="utf-8") as f:
        return json.load(f).get("events", [])


def build_story_events(
    event_defs: list[dict],
    sequence_ids: set[str],
    diagnostics: list[ExtractDiagnostic],
) -> list[dict]:
    """event manifest를 런타임 storyEvents로 변환한다.

    presentationKey가 dialogue 종류(DialogueScene/DialogueOverlay)면 대응
    sequence가 실제로 추출됐는지 검증하고, 없으면 drop + diagnostic을 남긴다.
    raw-wiki에 scene이 없는 event가 런타임에서 throw하지 않도록 막는 게이트다.
    presentationKey 자체는 raw-wiki scene_id 규약을 그대로 따른다 — 런타임
    DialogueAssemblyService가 1차 직접 조회 후 ToDialogueSequenceId로 fallback한다."""
    out: list[dict] = []
    for index, ev in enumerate(event_defs):
        pkey = ev.get("presentationKey", "")
        kind = ev.get("presentationKind", "")
        if kind in ("DialogueScene", "DialogueOverlay"):
            seq_id = to_sequence_id(pkey)
            if seq_id not in sequence_ids:
                diagnostics.append(ExtractDiagnostic(
                    code="WIKI-E030",
                    severity="Error",
                    message=(f"Event '{ev.get('eventId')}' presentationKey "
                             f"'{pkey}' → sequence '{seq_id}' not found in "
                             f"extracted dialogue; event dropped."),
                    source="narrative-event-map.json",
                ))
                continue
        out.append({**ev, "sourceOrder": index})
    return out


# ---------------------------------------------------------------------------
# Visual beat map — presentation tier / backdrop / motion / LUT
# ---------------------------------------------------------------------------
#
# 각 scene이 어떤 매체(T0~T4)·배경(없음/공용/전용)·모션·색보정으로 연출되는지를
# 비파괴적으로 산출한다. medium은 기존 `> **연출**:` 첫 토큰에서 추론하고, chapter는
# artifact_slug에서, 세부 배급은 git-tracked narrative-visual-map.json overrides로
# 덮어쓴다. 결과는 scene.meta["visual"]에 실려 seed → asset-studio(Tauri)로 흐른다.
# (narrative.rs가 meta를 serde_json::Value로 그대로 통과시키므로 Rust 변경 불요.)

_MEDIUM_PREFIXES = [
    ("cutscene", "cutscene"),
    ("dialogue-scene", "dialogue-scene"),
    ("dialogue-overlay", "dialogue-overlay"),
    ("combat bark", "combat-bark"),
    ("combat-bark", "combat-bark"),
    ("reward", "reward-join"),
    ("story card", "story-card"),
    ("story-card", "story-card"),
    ("dialogue", "dialogue"),
]

_CHAPTER_RE = re.compile(r"ch([1-5])\b")


def load_visual_map() -> dict:
    """narrative-visual-map.json (defaults + per-scene overrides)을 읽는다.

    없으면 빈 dict → medium/chapter 추론만으로 동작한다."""
    if not VISUAL_MAP.exists():
        return {}
    with open(VISUAL_MAP, encoding="utf-8") as f:
        return json.load(f)


def detect_medium(staging: str) -> str:
    """`연출` 첫 토큰에서 매체를 식별한다 (cutscene / dialogue-scene / ...)."""
    s = (staging or "").strip().lower()
    for prefix, medium in _MEDIUM_PREFIXES:
        if s.startswith(prefix):
            return medium
    return ""


def chapter_of(artifact_slug: str) -> str:
    m = _CHAPTER_RE.search(artifact_slug or "")
    return f"ch{m.group(1)}" if m else ""


_SITES = [
    "ashen_gate", "wolfpine_trail", "sunken_bastion", "tithe_road",
    "ruined_crypts", "bone_orchard", "glass_forest", "starved_menagerie",
    "heartforge_gate", "worldscar_depths",
]

def is_combat_overlay(scene_id: str) -> bool:
    """전투 위 overlay(배경 비움 T0): atlas route, boss bark/engage/break.

    boss bark류는 scene_id에 적 이름이 끼어들 수 있어(boss_wolfpine_engage)
    'boss' + 동작 키워드 조합으로 판정한다. boss_defeat는 전투 후라 제외(배경 가능)."""
    if "atlas_route" in scene_id:
        return True
    return "boss" in scene_id and any(k in scene_id for k in ("bark", "engage", "break"))


def detect_site(scene: WikiScene, sites: list) -> str:
    """scene_id + 컨텍스트에서 site 토큰을 찾는다 (공용 site 배경 자동 bind용)."""
    hay = f"{scene.scene_id} {scene.meta.get('컨텍스트', '')}"
    for s in (sites or _SITES):
        if s in hay:
            return s
    return ""


def compute_visual(scene: WikiScene, vmap: dict, inherited_site: str = "") -> dict:
    """scene별 비주얼 계획을 산출한다. medium/chapter/site 추론 위에 overrides를 덮는다.

    - tier: 연출 medium 토큰 → mediumTier. 단 boss bark/atlas 류 scene_id는 T0(배경 공백).
    - backdrop: T0/card는 없음. town scene은 공용 town 배경. site는 scene 자체 토큰,
      없으면 같은 챕터 직전 site(inherited_site) 상속 → 공용 site 배경.
    - bespoke/예외 배급은 overrides가 결정(curated=True).
    반환 dict는 scene.meta["visual"]로 실려 asset-studio가 그대로 읽는다."""
    defaults = vmap.get("defaults", {})
    medium_tier = defaults.get("mediumTier", {})
    chapter_lut = defaults.get("chapterLut", {})
    tier_motion = defaults.get("tierMotion", {})
    town_backdrop = defaults.get("townBackdrop", "shared:town_ashglen")
    slug_backdrop = defaults.get("slugBackdrop", {})
    sites = defaults.get("sites", _SITES)

    medium = detect_medium(scene.meta.get("연출", ""))
    tier = medium_tier.get(medium, "T1")
    sid = scene.scene_id
    if is_combat_overlay(sid):
        tier = "T0"

    lut = chapter_lut.get(chapter_of(scene.artifact_slug), "neutral")

    backdrop = None
    if tier not in ("T0", "card"):
        slug = scene.artifact_slug or ""
        if "town" in sid or "town" in slug:
            backdrop = town_backdrop
        else:
            # slug 기반 story-location 배경(전투 site 아닌 반복 장소, 예: 본 대성당)
            loc = next((bd for key, bd in slug_backdrop.items() if key in slug), "")
            if loc:
                backdrop = f"shared:{loc}"
            else:
                site = detect_site(scene, sites) or inherited_site
                if site:
                    backdrop = f"shared:site_{site}"

    visual = {
        "tier": tier,
        "medium": medium or "dialogue",
        "backdrop": backdrop,
        "motion": tier_motion.get(tier, "static"),
        "lut": lut,
        "curated": False,
    }

    ov = vmap.get("overrides", {}).get(scene.scene_id)
    if ov:
        for key, value in ov.items():
            if key == "note":
                continue
            visual[key] = value
        if "tier" in ov and "motion" not in ov:
            visual["motion"] = tier_motion.get(ov["tier"], visual["motion"])
        visual["curated"] = True
        if ov.get("note"):
            visual["note"] = ov["note"]

    # 배경 종류(env/char/prop) + 등장 캐릭터: 순수 배경 일러와 캐릭터 포함 일러를
    # 분리한다. char는 생성 시 캐릭터 ref(P09 anchor + 포트레잇 chained REF) 필수.
    # bespoke override가 kind/subjects를 주면 그대로, 아니면 backdrop 유무로 기본값.
    if not visual.get("backdrop"):
        visual["kind"] = None
        visual["subjects"] = []
    else:
        visual.setdefault("kind", "env")
        visual.setdefault("subjects", [])
    return visual


def load_audio_map() -> dict:
    """narrative-audio-map.json (defaults + per-scene overrides)을 읽는다."""
    if not AUDIO_MAP.exists():
        return {}
    with open(AUDIO_MAP, encoding="utf-8") as f:
        return json.load(f)


def detect_register(scene: WikiScene) -> str:
    """beat 종류에서 음악 register 추론. 연결 조직(town/atlas/TC)은 warmth-humor,
    그 외 기본 politics-weight. growth-emotion 등 예외는 overrides가 결정."""
    sid = scene.scene_id
    slug = scene.artifact_slug or ""
    if "town" in sid or "town" in slug or "atlas" in sid or "_tc" in sid:
        return "warmth-humor"
    return "politics-weight"


def compute_audio(scene: WikiScene, amap: dict) -> dict:
    """scene별 음악 계획을 산출한다. register/mood/cue 추론 위에 overrides를 덮는다.

    scene.meta["audio"] = {register, mood, cue_id, leitmotif, channel, reuse, curated}.
    cue_id는 scene-mood 재사용(chapter/town/boss 루프), bespoke는 overrides."""
    defaults = amap.get("defaults", {})
    chapter_mood = defaults.get("chapterMood", {})
    channel = defaults.get("channel", "Bgm")

    sid = scene.scene_id
    slug = scene.artifact_slug or ""
    chapter = chapter_of(slug)
    register = detect_register(scene)
    mood = chapter_mood.get(chapter, "neutral")

    if "town" in sid or "town" in slug:
        cue = "bgm_town_ashglen"
    elif "atlas" in sid:
        cue = "bgm_atlas"
    elif any(k in sid for k in ("boss_bark", "boss_engage", "boss_break")):
        cue = f"bgm_{chapter or 'x'}_boss"
    elif chapter:
        cue = f"bgm_{chapter}_field"
    else:
        cue = "bgm_event_field"

    audio = {
        "register": register,
        "mood": mood,
        "cue_id": cue,
        "leitmotif": None,
        "channel": channel,
        "reuse": "shared",
        "curated": False,
    }

    ov = amap.get("overrides", {}).get(sid)
    if ov:
        for key, value in ov.items():
            if key == "note":
                continue
            audio[key] = value
        audio["curated"] = True
        if ov.get("note"):
            audio["note"] = ov["note"]
    return audio


def build_seed(scenes: dict[str, WikiScene], visual_map: Optional[dict] = None,
               audio_map: Optional[dict] = None) -> dict:
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

    visual_map = visual_map or {}
    audio_map = audio_map or {}
    site_list = visual_map.get("defaults", {}).get("sites")
    prev_slug = None
    current_site = ""
    for scene_id, scene in scenes.items():
        # 같은 챕터(artifact) 안에서 직전 site를 상속한다. 캐릭터 외전/메모리얼/
        # 프롤로그는 site-intro가 없어 current_site가 비어 null로 남는다.
        if scene.artifact_slug != prev_slug:
            prev_slug = scene.artifact_slug
            current_site = ""
        own_site = detect_site(scene, site_list)
        if own_site:
            current_site = own_site
        scene.meta["visual"] = compute_visual(scene, visual_map, inherited_site=current_site)
        scene.meta["audio"] = compute_audio(scene, audio_map)
        # 분기 평탄화: 첫 분기만 정본으로 채택한다. 선택지 미지원 상태에서
        # branch별 lineIndex(3a/3b/3c → 3) 충돌로 textKey가 겹쳐, 같은 줄이
        # 화자만 바뀐 채 반복 재생되던 문제를 제거한다.
        first_branch = next(
            (ln.branch_tag for ln in scene.lines if ln.branch_tag), "")
        kept_lines = [ln for ln in scene.lines
                      if not ln.branch_tag or ln.branch_tag == first_branch]
        # lineIndex 재번호: 원본 # 는 분기 letter 접미사(3a/3b)·call-response 등으로
        # 같은 번호가 겹친다. 출력 순서대로 0..N-1 부여해 textKey 충돌을 원천 차단한다.
        lines_out = []
        for new_index, ln in enumerate(kept_lines):
            lines_out.append({
                "lineId": ln.line_id,
                "lineIndex": new_index,
                "speakerAlias": ln.speaker_alias,
                "speakerId": ln.speaker_id,
                "emotionRaw": ln.emotion_raw,
                "emotionId": ln.emotion_id,
                "emoteId": ln.emote_id,
                "text": clean_display_text(ln.text_ko),
                "en": "",
                "branchTag": ln.branch_tag,
                "sectionLabel": ln.section_label,
                "voiceRole": ln.voice_role,
                "refs": ln.refs,
            })
        seq = {
            "sequenceId": to_sequence_id(scene.scene_id),
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
        line_index_map[scene_id] = [ln.line_id for ln in kept_lines]

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

    seed = build_seed(parser.scenes, load_visual_map(), load_audio_map())
    sequence_ids = {seq["sequenceId"] for seq in seed["dialogueSequences"]}
    seed["storyEvents"] = build_story_events(
        load_event_map(), sequence_ids, parser.diagnostics)
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
    print(f"  story events:   {len(seed['storyEvents'])}")
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
