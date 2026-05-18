#!/usr/bin/env python3
"""Seed skill-owned icon catalog subjects from authored SkillDefinition assets.

The generated catalog is intentionally skill-owned:
SkillDefinitionAsset.Id -> SkillDefinitionAsset.IconId -> catalog_v2 PNG.
It does not derive icon ownership from CharacterDefinition or character art.
"""
from __future__ import annotations

import argparse
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import yaml


REPO_ROOT = Path(__file__).resolve().parents[2]
PIPELINE_ROOT = REPO_ROOT / "art-pipeline"
SKILL_ROOT = REPO_ROOT / "Assets" / "Resources" / "_Game" / "Content" / "Definitions" / "Skills"
SUBJECT_ROOT = PIPELINE_ROOT / "subjects" / "icons" / "skill"
CATALOG_OUTPUT_DIR = "art-pipeline/output/icons/skill/catalog_v2"

SLOT_NAMES = {0: "CoreActive", 1: "UtilityActive", 2: "Passive", 3: "Support"}
KIND_NAMES = {0: "Strike", 1: "Heal", 2: "Shield", 3: "Buff", 4: "Debuff", 5: "Utility"}
DELIVERY_NAMES = {0: "Melee", 1: "Ranged", 2: "Projectile", 3: "Nova", 4: "Aura", 5: "Trap", 6: "Zone"}
DAMAGE_NAMES = {0: "Physical", 1: "Magical", 2: "Healing", 3: "True"}

CATALOG_SHEETS: list[tuple[str, str, list[str]]] = [
    (
        "skill_catalog_v2_vanguard_core",
        "Vanguard Core / Barrier",
        ["skill_priest_core", "skill_guardian_core", "skill_bulwark_core", "skill_warden_utility"],
    ),
    (
        "skill_catalog_v2_vanguard_stance",
        "Vanguard Utility / Stance",
        ["skill_bulwark_utility", "skill_guardian_utility", "skill_vanguard_passive_1", "skill_vanguard_passive_2"],
    ),
    (
        "skill_catalog_v2_vanguard_support",
        "Vanguard Support",
        ["skill_vanguard_support_1", "skill_vanguard_support_2", "support_anchored", "support_guarded"],
    ),
    (
        "skill_catalog_v2_duelist_core",
        "Duelist Core Strikes",
        ["skill_power_strike", "skill_raider_core", "skill_reaver_core", "skill_slayer_core"],
    ),
    (
        "skill_catalog_v2_duelist_motion",
        "Duelist Motion",
        ["skill_raider_utility", "skill_reaver_utility", "skill_slayer_utility", "support_executioner"],
    ),
    (
        "skill_catalog_v2_duelist_boons",
        "Duelist Passive / Support",
        ["skill_duelist_passive_1", "skill_duelist_passive_2", "skill_duelist_support_1", "skill_duelist_support_2"],
    ),
    (
        "skill_catalog_v2_ranger_core",
        "Ranger Core Shots",
        ["skill_scout_core", "skill_marksman_core", "skill_precision_shot", "support_hunter_mark"],
    ),
    (
        "skill_catalog_v2_ranger_utility",
        "Ranger Utility",
        ["skill_scout_utility", "skill_marksman_utility", "skill_hunter_utility", "support_swift"],
    ),
    (
        "skill_catalog_v2_ranger_boons",
        "Ranger Passive / Support",
        ["skill_ranger_passive_1", "skill_ranger_passive_2", "skill_ranger_support_1", "skill_ranger_support_2"],
    ),
    (
        "skill_catalog_v2_cross_support",
        "Cross-role Support Edges",
        ["support_brutal", "support_longshot", "support_piercing", "support_purifying"],
    ),
    (
        "skill_catalog_v2_mystic_core",
        "Mystic Core / Utility",
        ["skill_hexer_core", "skill_shaman_core", "skill_hexer_utility", "skill_shaman_utility"],
    ),
    (
        "skill_catalog_v2_mystic_recovery",
        "Mystic Recovery",
        ["skill_minor_heal", "skill_mystic_support_1", "skill_mystic_support_2", "support_siphon"],
    ),
    (
        "skill_catalog_v2_mystic_boons",
        "Mystic Passive / Echo",
        ["skill_mystic_passive_1", "skill_mystic_passive_2", "support_lingering", "support_echo"],
    ),
]


@dataclass(frozen=True)
class SkillInfo:
    asset_path: Path
    skill_id: str
    slot: str
    kind: str
    delivery: str
    damage: str
    power: str
    current_icon_id: str
    vfx_hook_id: str
    effect_family_id: str
    status_ids: tuple[str, ...]

    @property
    def icon_suffix(self) -> str:
        if self.skill_id.startswith("skill_"):
            return self.skill_id[len("skill_") :]
        return self.skill_id

    @property
    def icon_id(self) -> str:
        return f"skill_icon_{self.icon_suffix}"

    @property
    def source_path(self) -> str:
        return f"{CATALOG_OUTPUT_DIR}/{self.icon_id}.png"

    @property
    def placeholder_source_path(self) -> str:
        return f"art-pipeline/output/icons/skill/catalog_v1/{resolve_legacy_icon_id(self.skill_id)}.png"


def resolve_legacy_icon_id(skill_id: str) -> str:
    mapping = {
        "skill_priest_core": "skill_icon_sigil_shield",
        "skill_guardian_core": "skill_icon_sigil_shield",
        "skill_bulwark_core": "skill_icon_sigil_shield",
        "skill_minor_heal": "skill_icon_platinum_aegis",
        "skill_mystic_support_1": "skill_icon_platinum_aegis",
        "skill_mystic_support_2": "skill_icon_platinum_aegis",
        "skill_hexer_core": "skill_icon_time_distance",
        "skill_hexer_utility": "skill_icon_memory_project",
        "skill_shaman_core": "skill_icon_voice_scar",
        "skill_shaman_utility": "skill_icon_voice_scar",
        "skill_raider_core": "skill_icon_fang_strike",
        "skill_reaver_core": "skill_icon_fang_strike",
        "skill_slayer_core": "skill_icon_fang_strike",
        "skill_power_strike": "skill_icon_fang_strike",
        "skill_raider_utility": "skill_icon_return_path",
        "skill_reaver_utility": "skill_icon_return_path",
        "skill_slayer_utility": "skill_icon_return_path",
        "skill_scout_core": "skill_icon_knot_arrow",
        "skill_marksman_core": "skill_icon_knot_arrow",
        "skill_precision_shot": "skill_icon_knot_arrow",
        "skill_scout_utility": "skill_icon_wind_read",
        "skill_marksman_utility": "skill_icon_wind_read",
        "skill_hunter_utility": "skill_icon_wind_read",
        "support_purifying": "skill_icon_ash_purification",
        "support_guarded": "skill_icon_sigil_shield",
        "support_anchored": "skill_icon_sigil_shield",
        "support_brutal": "skill_icon_fang_strike",
        "support_executioner": "skill_icon_fang_strike",
        "support_hunter_mark": "skill_icon_knot_arrow",
        "support_longshot": "skill_icon_knot_arrow",
        "support_piercing": "skill_icon_knot_arrow",
        "support_swift": "skill_icon_wind_read",
        "support_lingering": "skill_icon_voice_scar",
        "support_siphon": "skill_icon_memory_project",
        "support_echo": "skill_icon_external_lexicon",
    }
    if skill_id in mapping:
        return mapping[skill_id]

    lower = skill_id.lower()
    if any(token in lower for token in ("vanguard", "warden", "guardian", "bulwark")):
        return "skill_icon_sigil_shield"
    if any(token in lower for token in ("duelist", "raider", "reaver", "slayer")):
        return "skill_icon_fang_strike"
    if any(token in lower for token in ("ranger", "scout", "marksman", "hunter")):
        return "skill_icon_knot_arrow"
    if any(token in lower for token in ("mystic", "priest", "hexer", "shaman")):
        return "skill_icon_memory_project"
    return "skill_icon_sigil_shield"


def scalar(text: str, key: str) -> str:
    match = re.search(rf"^  {re.escape(key)}:[ \t]*(.*)$", text, re.MULTILINE)
    return match.group(1).strip() if match else ""


def enum_name(value: str, names: dict[int, str], fallback: str) -> str:
    try:
        return names.get(int(value), fallback)
    except ValueError:
        return fallback


def parse_skill_asset(path: Path) -> SkillInfo:
    text = path.read_text(encoding="utf-8")
    skill_id = scalar(text, "Id") or path.stem
    return SkillInfo(
        asset_path=path,
        skill_id=skill_id,
        slot=enum_name(scalar(text, "SlotKind"), SLOT_NAMES, "Unknown"),
        kind=enum_name(scalar(text, "Kind"), KIND_NAMES, "Unknown"),
        delivery=enum_name(scalar(text, "Delivery"), DELIVERY_NAMES, "Unknown"),
        damage=enum_name(scalar(text, "DamageType"), DAMAGE_NAMES, "Unknown"),
        power=scalar(text, "Power"),
        current_icon_id=scalar(text, "IconId"),
        vfx_hook_id=scalar(text, "VfxHookId"),
        effect_family_id=scalar(text, "EffectFamilyId"),
        status_ids=tuple(re.findall(r"^\s+StatusId:\s*(\S+)", text, re.MULTILINE)),
    )


def load_skills() -> dict[str, SkillInfo]:
    skills = {info.skill_id: info for info in (parse_skill_asset(path) for path in sorted(SKILL_ROOT.glob("*.asset")))}
    if len(skills) != 52:
        raise ValueError(f"expected 52 SkillDefinition assets, found {len(skills)}")
    return skills


def validate_sheet_plan(skills: dict[str, SkillInfo]) -> None:
    planned = [skill_id for _sheet_id, _title, skill_ids in CATALOG_SHEETS for skill_id in skill_ids]
    duplicates = sorted({skill_id for skill_id in planned if planned.count(skill_id) > 1})
    missing = sorted(set(skills) - set(planned))
    unknown = sorted(set(planned) - set(skills))
    if duplicates or missing or unknown:
        raise ValueError(
            "invalid catalog sheet plan: "
            f"duplicates={duplicates}, missing={missing}, unknown={unknown}"
        )


def icon_line(skill: SkillInfo) -> str:
    status = ", ".join(skill.status_ids) if skill.status_ids else "none"
    family = skill.effect_family_id or "none"
    return (
        f"{skill.skill_id}: {skill.slot} {skill.kind}, {skill.delivery}, {skill.damage}, "
        f"power {skill.power or '0'}, statuses {status}, effect family {family}"
    )


def descriptor(skill: SkillInfo) -> str:
    sid = skill.skill_id
    tokens = sid.replace("skill_", "").replace("support_", "support_")

    known: dict[str, str] = {
        "skill_priest_core": "ivory shield sigil with a small gold sunburst and a clean barrier arc",
        "skill_guardian_core": "steel kite shield tilted forward with a blue guard chevron",
        "skill_bulwark_core": "heavy tower shield impact wedge with bronze edge plates",
        "skill_warden_utility": "unyielding stone bootstep and short silver shockwave, no foot or body",
        "skill_bulwark_utility": "fortified wall segment with two interlocking plates and amber brace lines",
        "skill_guardian_utility": "protective intercept arrow bending into a shield face",
        "skill_vanguard_passive_1": "compact stance rune with stacked shield plates and a calm blue core",
        "skill_vanguard_passive_2": "retaliation guard knot, shield corner plus restrained red counter-spark inside stroke",
        "skill_vanguard_support_1": "team guard banner abstracted as two small shields under one arch",
        "skill_vanguard_support_2": "resolute oath seal, square shield glyph bound by gold cords",
        "support_anchored": "iron anchor mark fused with a shield base, grounded and immovable",
        "support_guarded": "small protected ally diamond enclosed by a shield crescent",
        "skill_power_strike": "single heavy diagonal sword slash, steel and crimson, direct impact",
        "skill_raider_core": "hooked axe fang striking downward with orange motion bite",
        "skill_reaver_core": "crescent reaver blade with a dark red cleave trail inside the outline",
        "skill_slayer_core": "executioner greatblade point with bright red pressure notch",
        "skill_raider_utility": "returning hooked path, bronze arc curving back to its start",
        "skill_reaver_utility": "reposition slash trail, two steel arcs crossing like a sidestep",
        "skill_slayer_utility": "short lunge arrow embedded in a blade edge, aggressive forward motion",
        "support_executioner": "clean execution mark, blade tip over a small cracked target diamond",
        "skill_duelist_passive_1": "duelist tempo knot, paired red steel ticks circling a small blade shard",
        "skill_duelist_passive_2": "riposte emblem, narrow parry line bouncing a crimson spark back",
        "skill_duelist_support_1": "shared brutality mark, compact claw-like slash trio in copper red",
        "skill_duelist_support_2": "pressure support sigil, jagged red wedge pressing into a steel point",
        "skill_scout_core": "light scout arrowhead with green wind notch and minimal bow curve",
        "skill_marksman_core": "precise longbow arrow aligned through a small blue aiming diamond",
        "skill_precision_shot": "thin silver arrow piercing a tiny gold focus ring, no UI frame",
        "support_hunter_mark": "hunter mark diamond with an arrow notch and amber tracking dot",
        "skill_scout_utility": "exposed target marker, split green target plate opening at the center",
        "skill_marksman_utility": "sunder arrowhead cracking a small armor plate",
        "skill_hunter_utility": "slowing snare wind glyph, green cord loop with a small weighted dart",
        "support_swift": "swift footless wind streak, teal feather-like motion slash with no body",
        "skill_ranger_passive_1": "ranger patience glyph, arrow resting over a quiet green leaf-shaped notch",
        "skill_ranger_passive_2": "range advantage chevron, stacked arrow distance markers without numbers",
        "skill_ranger_support_1": "ranger support knot, two small arrows converging on one target diamond",
        "skill_ranger_support_2": "piercing support seal, arrowhead threading through layered plates",
        "support_brutal": "brutal pressure shard, red broken blade chunk with blunt impact notch",
        "support_longshot": "longshot arc, thin arrow flying over a blue distance curve",
        "support_piercing": "piercing point splitting two armor slivers, silver and teal",
        "support_purifying": "ash purification flame, pale ash plume wrapped around a small gold spark",
        "skill_hexer_core": "time-distance hex sigil, dark teal hourglass shard with violet-free black rune cuts",
        "skill_shaman_core": "voice scar wave, ochre sound ripple slicing through a cracked bead",
        "skill_hexer_utility": "memory projection prism, green spectral shard casting a contained echo",
        "skill_shaman_utility": "ritual healing drum mark, warm green pulse inside a small bone-white circle",
        "skill_minor_heal": "platinum aegis heal mark, small shield with a clean white-gold cross-like glint but no text",
        "skill_mystic_support_1": "mystic support seal, white-gold ward bead with green inner pulse",
        "skill_mystic_support_2": "linked recovery sigil, two small light motes joined inside a platinum crescent",
        "support_siphon": "siphon spiral, green energy thread pulling into a dark memory bead",
        "skill_mystic_passive_1": "memory anchor passive, green-gold knot around a tiny still prism",
        "skill_mystic_passive_2": "quiet ritual passive, layered bead halo with one pale gold spark",
        "support_lingering": "lingering echo wave, fading ochre ripple trapped inside the outer stroke",
        "support_echo": "external lexicon echo, small open abstract tablet with blue sound line, no letters",
    }
    if sid in known:
        return known[sid]

    if skill.kind == "Heal":
        return f"{tokens} healing symbol, white-gold and green, centered abstract recovery sigil"
    if skill.kind == "Shield":
        return f"{tokens} defensive shield symbol, steel and gold, centered barrier sigil"
    if skill.delivery == "Projectile":
        return f"{tokens} projectile symbol, arrowhead and clean motion line, centered"
    if skill.kind == "Buff":
        return f"{tokens} buff sigil, compact aura knot with one clear focal symbol"
    return f"{tokens} {skill.kind.lower()} symbol, {skill.damage.lower()} themed, one centered icon"


def write_subject(sheet_id: str, title: str, sheet_skills: list[SkillInfo], dry_run: bool) -> Path:
    suffixes = [skill.icon_suffix for skill in sheet_skills]
    bindings = [
        {
            "skill_id": skill.skill_id,
            "icon_id": skill.icon_id,
            "slot": skill.slot,
            "kind": skill.kind,
            "status_ids": list(skill.status_ids),
            "vfx_hook_id": skill.vfx_hook_id,
        }
        for skill in sheet_skills
    ]
    fm: dict[str, Any] = {
        "slug": f"{sheet_id}--default",
        "kind": "skill_icon_catalog_sheet",
        "subject_id": sheet_id,
        "variant": "default",
        "refs": [],
        "aspect": "1:1",
        "output_size": "1568x1568",
        "chroma": "#FF00FF",
        "skills": suffixes,
        "output_directory": CATALOG_OUTPUT_DIR,
        "output_prefix": "skill_icon",
        "skill_bindings": bindings,
        "status": "prompted",
    }

    reading_order = "\n".join(
        [
            f"- ({index % 2},{index // 2}): {skill.icon_id} ({skill.skill_id})"
            for index, skill in enumerate(sheet_skills)
        ]
    )
    details = "\n\n".join(
        [
            f"({index % 2},{index // 2}) {skill.icon_id}:\n"
            f"{descriptor(skill)}. Runtime: {icon_line(skill)}."
            for index, skill in enumerate(sheet_skills)
        ]
    )
    body = f"""# {title}

Canonical skill-owned icon sheet for actual authored SkillDefinition assets. This subject is not character-owned; every cell maps a runtime `SkillId` to exactly one stable `IconId`.

```prompt
=== CANONICAL SKILL ICON SHEET ===
Generate a 2-column x 2-row sprite sheet of 4 separate game skill icons.

Canvas: 1568 x 1568 px.
Cell size: 768 x 768 px.
Gutters and outer margin: exactly 32 px, solid #FF00FF.
Reading order:
{reading_order}

Global icon rules:
- Each cell has exactly one centered focal symbol, filling about 65-75% of the cell.
- No character, no hand, no face, no body, no portrait, no scene.
- No text, no numerals, no letters, no UI frame or ring.
- Background in every cell, gutter, and margin is flat #FF00FF.
- Each symbol has a continuous 2-4 px dark outer stroke.
- Outside the outer stroke there is only pure #FF00FF: no shadow, no blur, no glow, no particles, no haze.
- Subject colors must never use magenta, hot pink, or fuchsia.
- Icons in this sheet should share a coherent painterly game style, but each symbol must be visibly distinct at 64 px.

Per-cell descriptors:

{details}

Output a single transparent-ready chroma sprite sheet exactly matching the grid.
```
"""
    subject_path = SUBJECT_ROOT / sheet_id / "default.md"
    if not dry_run:
        subject_path.parent.mkdir(parents=True, exist_ok=True)
        subject_path.write_text(
            "---\n" + yaml.safe_dump(fm, sort_keys=False, allow_unicode=True) + "---\n\n" + body,
            encoding="utf-8",
            newline="\n",
        )
    return subject_path


def write_skill_asset_manifest(sheets: list[tuple[str, str, list[SkillInfo]]], dry_run: bool) -> None:
    manifest = {
        "schema": "skill_asset_manifest_v1",
        "updated_at": "2026-05-18",
        "policy": {
            "hierarchy": "Skill images live under art-pipeline/subjects/icons/skill/**, not under character subject folders.",
            "gameplay_binding": "Runtime ownership is one authored SkillDefinitionAsset.Id -> one SkillDefinitionAsset.IconId -> Sprite/presentation catalog.",
            "canonical_catalog": f"Canonical skill icon sheets write split outputs to {CATALOG_OUTPUT_DIR}/skill_icon_{{skill}}.png.",
            "generation_batch": "Generate 4 icons per imagegen call as a 1568x1568 2x2 sheet: 768px cells with 32px #FF00FF gutters.",
            "placeholder_backfill": "catalog_v2 files may be temporarily copied from catalog_v1 motifs only to keep runtime icons nonblank while fresh imagegen is blocked.",
            "retired_character_theme_bridge": "Former character_theme_* sheets were migration seeds only and are no longer active skill icon generation units.",
        },
        "profiles": {
            "skill_catalog_sheet": {
                "description": "Canonical 2x2 skill icon sheet grouped by skill semantics, not by character ownership.",
                "required": ["catalog_sheet"],
            }
        },
        "asset_requirements": {
            "catalog_sheet": {
                "kind": "sheet_output",
                "subject": "subjects/icons/skill/{id}/default.md",
                "outputs": ["output/{id}/default.png"],
                "dynamic_outputs": {
                    "from_frontmatter": "skills",
                    "pattern": "output/icons/skill/catalog_v2/skill_icon_{skill}.png",
                },
            }
        },
        "assets": [
            {
                "id": sheet_id,
                "profile": "skill_catalog_sheet",
                "semantic_family": sheet_id.replace("skill_catalog_v2_", ""),
                "binding_status": "canonical_skill_catalog",
                "skill_ids": [skill.skill_id for skill in sheet_skills],
            }
            for sheet_id, _title, sheet_skills in sheets
        ],
    }
    if not dry_run:
        (PIPELINE_ROOT / "config" / "skill_asset_manifest.yaml").write_text(
            yaml.safe_dump(manifest, sort_keys=False, allow_unicode=True),
            encoding="utf-8",
            newline="\n",
        )


def write_content_icon_catalog(skills: list[SkillInfo], dry_run: bool) -> None:
    path = PIPELINE_ROOT / "config" / "content_icon_catalog.yaml"
    catalog = yaml.safe_load(path.read_text(encoding="utf-8"))
    if not isinstance(catalog, dict):
        raise ValueError(f"{path}: expected YAML mapping")
    catalog["updated_at"] = "2026-05-18"
    policy = catalog.setdefault("policy", {})
    policy["skill_source_ownership"] = (
        "Canonical skill icon PNGs live under art-pipeline/output/icons/skill/catalog_v2 "
        "and are one-to-one with authored SkillDefinitionAsset.IconId."
    )
    policy["skill_placeholder_backfill"] = (
        "If fresh game-image-gen is blocked, catalog_v2 may hold catalog_v1 motif copies as explicit placeholders; "
        "subject pages remain status=prompted until fresh render replaces them."
    )
    icons = catalog.setdefault("icons", {})
    icons["skill"] = [
        {
            "id": skill.icon_id,
            "source": skill.source_path,
        }
        for skill in sorted(skills, key=lambda item: item.icon_id)
    ]
    if not dry_run:
        path.write_text(
            yaml.safe_dump(catalog, sort_keys=False, allow_unicode=True),
            encoding="utf-8",
            newline="\n",
        )


def write_generation_matrix(sheets: list[tuple[str, str, list[SkillInfo]]], dry_run: bool) -> None:
    matrix = {
        "schema": "skill_icon_generation_matrix_v1",
        "updated_at": "2026-05-18",
        "source_truth": "Assets/Resources/_Game/Content/Definitions/Skills/*.asset",
        "runtime_policy": "Each authored SkillDefinitionAsset owns a stable unique IconId; character art never owns skill icons.",
        "current_asset_status": "fresh_render_pending; catalog_v2 may be placeholder-backfilled from catalog_v1 motif art until ChatGPT image generation login is available",
        "imagegen_policy": {
            "batch_size": 4,
            "sheet_size": "1568x1568",
            "cell_size": "768x768",
            "gutter": "32px #FF00FF",
            "output_dir": CATALOG_OUTPUT_DIR,
            "outer_stroke": "2-4 px continuous dark stroke; outside stroke is flat #FF00FF only",
        },
        "sheets": [
            {
                "id": sheet_id,
                "title": title,
                "subject": f"art-pipeline/subjects/icons/skill/{sheet_id}/default.md",
                "skills": [
                    {
                        "skill_id": skill.skill_id,
                        "icon_id": skill.icon_id,
                        "slot": skill.slot,
                        "kind": skill.kind,
                        "delivery": skill.delivery,
                        "damage": skill.damage,
                        "power": skill.power,
                        "status_ids": list(skill.status_ids),
                        "effect_family_id": skill.effect_family_id,
                        "vfx_hook_id": skill.vfx_hook_id,
                        "source": skill.source_path,
                        "render_status": "pending_fresh_render",
                        "placeholder_source": skill.placeholder_source_path,
                    }
                    for skill in sheet_skills
                ],
            }
            for sheet_id, title, sheet_skills in sheets
        ],
    }
    if not dry_run:
        (PIPELINE_ROOT / "config" / "skill_icon_generation_matrix.yaml").write_text(
            yaml.safe_dump(matrix, sort_keys=False, allow_unicode=True),
            encoding="utf-8",
            newline="\n",
        )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    skills = load_skills()
    validate_sheet_plan(skills)
    sheets = [
        (sheet_id, title, [skills[skill_id] for skill_id in skill_ids])
        for sheet_id, title, skill_ids in CATALOG_SHEETS
    ]

    subject_paths = [write_subject(sheet_id, title, sheet_skills, args.dry_run) for sheet_id, title, sheet_skills in sheets]
    write_skill_asset_manifest(sheets, args.dry_run)
    write_content_icon_catalog([skill for _sheet_id, _title, sheet_skills in sheets for skill in sheet_skills], args.dry_run)
    write_generation_matrix(sheets, args.dry_run)

    verb = "would seed" if args.dry_run else "seeded"
    print(f"[seed_skill_icon_catalog] {verb} {len(subject_paths)} sheets / 52 skill icons")
    for path in subject_paths:
        print(f"  - {path.relative_to(REPO_ROOT)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
