#!/usr/bin/env python3
"""Create character sheet subject pages from P09 identity data.

This complements seed_character_portrait_subjects.py. It writes the
sprite-sheet subject pages required by character_asset_manifest.yaml without
calling image generation.
"""
from __future__ import annotations

import argparse
import textwrap
from pathlib import Path
from typing import Any

import yaml

from audit_p09_visual_identity import (
    load_presets,
    load_yaml,
    main_color,
    readable_outfit,
)


FACE_EMOTIONS = ["default", "smile", "serious", "shock", "anger", "sad", "cry", "quiet"]
COMBAT_STATES = ["wounded", "stunned", "feared", "charmed", "pained", "downed"]
STANCES = ["idle", "attack", "guard", "cast"]

WEAPON_FAMILIES = {
    1: "short sword / hunting blade",
    2: "straight sword",
    3: "regular military sword",
    4: "oath execution sword",
    5: "formal duelist sword",
    7: "ritual staff",
    8: "totem staff",
    9: "ceremonial staff",
    10: "long bow",
    11: "forest bow",
    12: "cold marksman bow",
    13: "prism ritual bow",
}

BOW_IDS = {10, 11, 12, 13}
STAFF_IDS = {7, 8, 9}
SWORD_IDS = {1, 2, 3, 4, 5}


def find_pipeline_root(start: Path) -> Path:
    for candidate in [start.resolve(), *start.resolve().parents]:
        if candidate.name == "art-pipeline":
            return candidate
    return Path(__file__).resolve().parents[1]


def dump_frontmatter(data: dict[str, Any]) -> str:
    return "---\n" + yaml.safe_dump(data, allow_unicode=True, sort_keys=False).strip() + "\n---\n"


def color_line(label: str, value: str | None) -> str:
    return f"- {label}: {value}" if value else f"- {label}: read from REF"


def identity_block(character: dict[str, Any], preset: dict[str, Any]) -> str:
    weapon_id = int(preset.get("weaponId", 0) or 0)
    shield_id = int(preset.get("shieldId", 0) or 0)
    colors = [
        color_line("Hair", main_color(preset, "hair")),
        color_line("Head gear", main_color(preset, "head")),
        color_line("Chest / upper outfit", main_color(preset, "chest")),
        color_line("Arm / sleeve", main_color(preset, "arm")),
        color_line("Waist / sash", main_color(preset, "waist")),
        color_line("Leg / lower outfit", main_color(preset, "leg")),
        color_line("Weapon", main_color(preset, "weapon")),
        color_line("Shield", main_color(preset, "shield")) if shield_id else "- Shield: none in P09 preset",
    ]
    ids = [
        f"- SexId {preset.get('sexId')} / FaceTypeId {preset.get('faceTypeId')} / HairStyleId {preset.get('hairStyleId')} / HairColorId {preset.get('hairColorId')}",
        f"- SkinId {preset.get('skinId')} / EyeColorId {preset.get('eyeColorId')} / FacialHairId {preset.get('facialHairId')} / BustSizeId {preset.get('bustSizeId')}",
        f"- Outfit {readable_outfit(preset)} / Head {preset.get('headId')} / Chest {preset.get('chestId')} / Arm {preset.get('armId')} / Waist {preset.get('waistId')} / Leg {preset.get('legId')}",
        f"- WeaponId {weapon_id} ({WEAPON_FAMILIES.get(weapon_id, 'P09 weapon from REF')}) / ShieldId {shield_id}",
    ]
    return f"""=== CHARACTER IDENTITY CONTRACT ===
- Character: {character.get('display_name', character['id'])}
- Pipeline subject_id: {character['id']}
- Faction / job lane: {character.get('faction', 'unknown')} / {character.get('combat_class', 'unknown')}
- Uniform group: {character.get('uniform_group', 'unknown')}
- Silhouette family: {character.get('silhouette_family', 'unknown')}
- Intent: {character.get('visual_intent', '')}

=== P09 CURRENT IDS ===
{chr(10).join(ids)}

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when capture lighting or prior illustrations obscure the canonical color.
{chr(10).join(colors)}
"""


def reference_priority(character_id: str, include_face_default: bool) -> str:
    face_line = (
        "3. The portrait_face_default prior output locks face proportions and should be matched closely."
        if include_face_default
        else "3. No face-default ref is attached yet because this sheet creates it; use portrait_full for face and costume continuity."
    )
    return f"""=== REFERENCE PRIORITY ===
1. The P09 anchor for {character_id} is canon for outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The {character_id}:portrait_full prior output is canon for the current 2D face polish, house style, and translated costume rendering.
{face_line}
4. If a Dawn Priest style seed is attached, it is style-only. Do not copy Dawn Priest costume, colors, shield, pose, face, or priest motifs.
"""


def frontmatter(
    character_id: str,
    variant: str,
    kind: str,
    aspect: str,
    output_size: str,
    refs: list[str],
    extra: dict[str, Any] | None = None,
) -> str:
    data: dict[str, Any] = {
        "slug": f"{character_id}--{variant}",
        "kind": kind,
        "subject_id": character_id,
        "variant": variant,
        "refs": refs,
        "aspect": aspect,
        "output_size": output_size,
        "chroma": "#FF00FF",
        "status": "prompted",
    }
    if extra:
        data.update(extra)
    return dump_frontmatter(data)


def render_face_emotion(character: dict[str, Any], preset: dict[str, Any]) -> str:
    character_id = character["id"]
    display_name = character.get("display_name", character_id)
    body = f"""
# {display_name} — Face Emotion Sheet

## prompt 명세

```prompt
Create a 4-column x 2-row face emotion sprite sheet for {display_name}.

{reference_priority(character_id, include_face_default=False)}

{identity_block(character, preset)}

=== GRID SHEET LAYOUT ===
- Canvas: 3168 x 1568 PNG.
- 4 columns x 2 rows.
- Each cell: exactly 768 x 768 px.
- Gap between cells: exactly 32 px, solid #FF00FF.
- Outside grid and each cell background: solid #FF00FF.
- Reading order:
  - row 0: default, smile, serious, shock
  - row 1: anger, sad, cry, quiet

=== CELL CONTENT ===
- Face close-up only: top of hair visible, ears inside frame, neck/collar visible at bottom.
- Same character in all 8 cells: same hair, eye color, skin, face proportions, collar, and outfit colors.
- Frontal view with a subtle 5-10 degree turn only.
- Same zoom, same head height, same lighting, same line thickness across all cells.
- Clean 1-2 px dark outline around the visible head / hair / collar silhouette.

=== EXPRESSION SET ===
- default: neutral readable resting state that fits the role and faction.
- smile: restrained character-appropriate warmth, not a broad comedy grin.
- serious: focused, jaw set, eyes intent.
- shock: eyes widened and breath caught, controlled rather than theatrical.
- anger: controlled anger, narrowed eyes, tension in brow and mouth.
- sad: quiet grief, lowered gaze, no melodrama.
- cry: one restrained tear, dignity preserved.
- quiet: emotionally emptied stillness, distant eyes, almost no mouth movement.

=== HARD CONSTRAINTS ===
- Single subject per cell.
- No weapon, hands, UI, captions, labels, environment, shadows, or props.
- Do not change hairstyle, hair color, eye color, collar color, gender presentation, facial hair, or head gear between cells.
- Do not add scars, tattoos, extra ornaments, glasses, animal ears, halos, or jewelry unless visible in the P09 ref.
- No magenta tint on the character.
```
"""
    return frontmatter(
        character_id,
        "face_emotion_sheet",
        "face_emotion_sheet",
        "2:1",
        "3168x1568",
        [character_id, f"{character_id}:portrait_full", "hero_dawn_priest:portrait_full_style_seed_best"],
        {"emotions": FACE_EMOTIONS},
    ) + textwrap.dedent(body).lstrip()


def render_face_combat(character: dict[str, Any], preset: dict[str, Any], include_face_default: bool) -> str:
    character_id = character["id"]
    display_name = character.get("display_name", character_id)
    refs = [character_id, f"{character_id}:portrait_full"]
    if include_face_default:
        refs.append(f"{character_id}:portrait_face_default")
    refs.append("hero_dawn_priest:portrait_full_style_seed_best")
    body = f"""
# {display_name} — Face Combat State Sheet

## prompt 명세

```prompt
Create a 3-column x 2-row face combat-state sprite sheet for {display_name}.

{reference_priority(character_id, include_face_default=include_face_default)}

{identity_block(character, preset)}

=== GRID SHEET LAYOUT ===
- Canvas: 2368 x 1568 PNG.
- 3 columns x 2 rows.
- Each cell: exactly 768 x 768 px.
- Gap between cells: exactly 32 px, solid #FF00FF.
- Outside grid and each cell background: solid #FF00FF.
- Reading order:
  - row 0: wounded, stunned, feared
  - row 1: charmed, pained, downed

=== CELL CONTENT ===
- Face close-up only: top of hair visible, ears inside frame, neck/collar visible at bottom.
- Same character in all 6 cells: same identity, same hair, same eye color, same face proportions, same collar.
- Frontal view with a subtle 5-10 degree turn only.
- Same zoom and head height across all cells.
- Clean 1-2 px dark outline around the visible silhouette.

=== GAME STATE SET ===
- wounded: HP critical; small blood trace, sweat, focused pain, still alive and fighting.
- stunned: unfocused pupils, slack jaw, head slightly tilted, no gore.
- feared: sustained fear, wide eyes, cold sweat, not comedic panic.
- charmed: absent softened smile, unfocused gaze, very faint non-magenta violet/cyan iris hint only.
- pained: damage-over-time endurance, eyes squeezed or narrowed, clenched jaw, sweat.
- downed: recoverable HP=0 state, eyes closed or half-closed, limp but not dead, small blood trace.

=== HARD CONSTRAINTS ===
- Single subject per cell.
- No weapon, hands, UI, captions, labels, environment, shadows, or props.
- Do not change hairstyle, hair color, eye color, collar color, gender presentation, facial hair, or head gear between cells.
- No horror gore, exposed injury, corpse framing, or rolled-back eyes.
- No magenta tint on the character. Charmed effect must avoid #FF00FF/fuchsia.
```
"""
    return frontmatter(
        character_id,
        "face_combat_state_sheet",
        "face_combat_state_sheet",
        "1.51:1",
        "2368x1568",
        refs,
        {"states": COMBAT_STATES},
    ) + textwrap.dedent(body).lstrip()


def render_bust_emotion(character: dict[str, Any], preset: dict[str, Any]) -> str:
    character_id = character["id"]
    display_name = character.get("display_name", character_id)
    body = f"""
# {display_name} — Bust Emotion Sheet R

## prompt 명세

```prompt
Create a 4-column x 2-row bust emotion sprite sheet for {display_name}, facing camera-right.

{reference_priority(character_id, include_face_default=True)}

{identity_block(character, preset)}

=== GRID SHEET LAYOUT ===
- Canvas: 3168 x 2336 PNG.
- 4 columns x 2 rows.
- Each cell: exactly 768 x 1152 px.
- Gap between cells: exactly 32 px, solid #FF00FF.
- Outside grid and each cell background: solid #FF00FF.
- Reading order:
  - row 0: default, smile, serious, shock
  - row 1: anger, sad, cry, quiet

=== CAMERA AND CROP ===
- Bust crop: head, shoulders, upper chest, and the most important collar/upper armor zones.
- Facing camera-right for VN left-side placement.
- Eyes look toward the right side of frame at about 25-30 degrees from frontal axis.
- Head turns about 20-25 degrees toward the right side of frame.
- Body rotates the same direction; the camera-side shoulder is closer.
- Same zoom, same shoulder height, same head height, same facing angle across all 8 cells.
- Clean 1-2 px dark outline around the visible silhouette.

=== EXPRESSION SET ===
- default: neutral readable resting state that fits the role and faction.
- smile: restrained character-appropriate warmth, not a broad comedy grin.
- serious: focused, jaw set, eyes intent.
- shock: eyes widened and breath caught, controlled rather than theatrical.
- anger: controlled anger, narrowed eyes, tension in brow and mouth.
- sad: quiet grief, lowered gaze, no melodrama.
- cry: one restrained tear, dignity preserved.
- quiet: emotionally emptied stillness, distant eyes, almost no mouth movement.

=== HARD CONSTRAINTS ===
- Single subject per cell, all facing camera-right.
- No full weapon display unless a small part is already naturally visible in the bust crop. Never add a new prop.
- Do not change hairstyle, hair color, eye color, outfit colors, gender presentation, facial hair, or head gear between cells.
- Do not add scars, tattoos, extra ornaments, glasses, animal ears, halos, or jewelry unless visible in the P09 ref.
- No magenta tint on the character.
```
"""
    return frontmatter(
        character_id,
        "bust_emotion_sheet_R",
        "bust_emotion_sheet",
        "1.36:1",
        "3168x2336",
        [character_id, f"{character_id}:portrait_full", f"{character_id}:portrait_face_default", "hero_dawn_priest:portrait_full_style_seed_best"],
        {"direction": "facing_right", "emotions": FACE_EMOTIONS},
    ) + textwrap.dedent(body).lstrip()


def stance_weapon_guidance(preset: dict[str, Any]) -> str:
    weapon_id = int(preset.get("weaponId", 0) or 0)
    shield_id = int(preset.get("shieldId", 0) or 0)
    if weapon_id in BOW_IDS:
        attack = "attack: bow fully drawn, arrow aimed diagonally toward an off-screen threat; no sword or staff."
        guard = "guard: bow angled defensively while stepping back or lowering stance; off-hand controls the string/arrow, no shield unless P09 has one."
        cast = "cast: tactical focus / class technique pose using the bow as focus, subtle non-magenta energy only if appropriate."
    elif weapon_id in STAFF_IDS:
        attack = "attack: staff thrust or sweeping strike, staff clearly in the correct hand, no sword or bow."
        guard = "guard: staff held across the body as a warding bar or barrier focus."
        cast = "cast: staff raised or planted with restrained faction-colored glow; avoid oversized spell effects."
    elif weapon_id in SWORD_IDS:
        attack = "attack: sword lunge or diagonal strike, clear blade silhouette, no bow or staff."
        guard = (
            "guard: shield-forward defensive stance using the P09 shield, sword held ready."
            if shield_id
            else "guard: sword parry or evasive low guard; off-hand empty, no shield or buckler."
        )
        cast = "cast: class technique pose with weapon held low or used as focus; restrained effect, not a new weapon."
    else:
        attack = "attack: use the exact P09 weapon in a readable offensive motion."
        guard = "guard: defensive posture using only the P09 weapon/shield state."
        cast = "cast: class technique pose using only the P09 weapon/shield state."

    shield_rule = (
        f"- ShieldId {shield_id}: render the shield exactly as P09 shows in every stance where the shield arm is visible."
        if shield_id
        else "- ShieldId 0: no shield, no buckler, no mirror shield, no defensive prop in any cell."
    )
    return f"""=== WEAPON / SHIELD STANCE RULES ===
- WeaponId {weapon_id}: {WEAPON_FAMILIES.get(weapon_id, 'use the exact P09 weapon from REF')}.
{shield_rule}
- {attack}
- {guard}
- {cast}
"""


def render_battle_stance(character: dict[str, Any], preset: dict[str, Any], include_face_default: bool) -> str:
    character_id = character["id"]
    display_name = character.get("display_name", character_id)
    refs = [character_id, f"{character_id}:portrait_full"]
    if include_face_default:
        refs.append(f"{character_id}:portrait_face_default")
    refs.append("hero_dawn_priest:portrait_full_style_seed_best")
    body = f"""
# {display_name} — Battle Stance Sheet

## prompt 명세

```prompt
Create a 2-column x 2-row full-body battle stance sprite sheet for {display_name}.

{reference_priority(character_id, include_face_default=include_face_default)}

{identity_block(character, preset)}

{stance_weapon_guidance(preset)}

=== GRID SHEET LAYOUT ===
- Canvas: 1568 x 2336 PNG.
- 2 columns x 2 rows.
- Each cell: exactly 768 x 1152 px.
- Gap between cells: exactly 32 px, solid #FF00FF.
- Outside grid and each cell background: solid #FF00FF.
- Reading order:
  - row 0: idle, attack
  - row 1: guard, cast

=== CELL CONTENT ===
- Full body in every cell: head to feet visible, weapon and shield state readable.
- Same character, same outfit, same hair, same color zones, same weapon, same shield state across all 4 cells.
- Three-quarter front view with dynamic stance differences.
- Same scale and vertical footprint across all cells.
- Clean 1-2 px dark outline around the entire character silhouette for chroma cleanup.

=== STANCE SET ===
- idle: combat-ready rest posture, readable class identity.
- attack: active offensive motion as specified in weapon rules.
- guard: defensive posture as specified in weapon/shield rules.
- cast: class technique or support stance; restrained effect only, no environment.

=== HARD CONSTRAINTS ===
- Single subject per cell.
- No UI, captions, labels, environment, ground shadows, companions, extra weapons, or extra props.
- Do not change gender presentation, hair, face, outfit slots, weapon family, shield state, or palette between cells.
- Do not add capes, halos, animal features, scars, tattoos, jewelry, class symbols, or ornaments not visible in P09.
- No magenta tint on the character. Any effect must avoid #FF00FF/fuchsia and must not bridge cells.
```
"""
    return frontmatter(
        character_id,
        "battle_stance_sheet",
        "battle_stance_sheet",
        "1:1.49",
        "1568x2336",
        refs,
        {"stances": STANCES},
    ) + textwrap.dedent(body).lstrip()


FULL_DIALOGUE_PROFILES = {
    "story_dialogue_character",
    "lead_story_combat",
    "named_story_battle",
    "battle_actor_core",
}


def variants_for_profile(profile: str) -> list[str]:
    if profile in FULL_DIALOGUE_PROFILES:
        return [
            "face_emotion_sheet",
            "face_combat_state_sheet",
            "bust_emotion_sheet_R",
            "battle_stance_sheet",
        ]
    raise ValueError(f"unknown profile {profile}")


def render_variant(character: dict[str, Any], preset: dict[str, Any], profile: str, variant: str) -> str:
    include_face_default = profile in FULL_DIALOGUE_PROFILES
    if variant == "face_emotion_sheet":
        return render_face_emotion(character, preset)
    if variant == "face_combat_state_sheet":
        return render_face_combat(character, preset, include_face_default)
    if variant == "bust_emotion_sheet_R":
        return render_bust_emotion(character, preset)
    if variant == "battle_stance_sheet":
        return render_battle_stance(character, preset, include_face_default)
    raise ValueError(f"unknown variant {variant}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--asset-manifest", type=Path, default=None)
    parser.add_argument("--identity-manifest", type=Path, default=None)
    parser.add_argument("--asset-dir", type=Path, default=None)
    parser.add_argument("--ids", nargs="*", default=None, help="only write the listed character ids")
    parser.add_argument("--profiles", nargs="*", default=None, help="only write characters in listed profiles")
    parser.add_argument("--variants", nargs="*", default=None, help="only write listed variants")
    parser.add_argument("--force", action="store_true", help="overwrite existing subject pages")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument(
        "--skip-missing-identity",
        action="store_true",
        help="do not fail when registry targets are still missing P09 identity manifest entries or preset assets",
    )
    args = parser.parse_args()

    pipeline_root = find_pipeline_root(Path(__file__))
    repo_root = pipeline_root.parent
    asset_manifest = load_yaml(args.asset_manifest or (pipeline_root / "config" / "character_asset_manifest.yaml"))
    identity_manifest = load_yaml(args.identity_manifest or (pipeline_root / "config" / "p09_visual_identity_manifest.yaml"))
    asset_dir = args.asset_dir or (repo_root / "Assets" / "Resources" / "_Game" / "Battle" / "Appearances" / "P09")
    presets = load_presets(asset_dir)
    identity_by_id = {item["id"]: item for item in identity_manifest.get("characters", [])}

    requested_ids = set(args.ids or [])
    requested_profiles = set(args.profiles or [])
    requested_variants = set(args.variants or [])
    created: list[str] = []
    skipped: list[str] = []
    missing: list[str] = []

    for manifest_character in asset_manifest.get("characters", []):
        character_id = manifest_character["id"]
        profile = manifest_character["profile"]
        if requested_ids and character_id not in requested_ids:
            continue
        if requested_profiles and profile not in requested_profiles:
            continue
        character = identity_by_id.get(character_id)
        preset = presets.get(character_id)
        if character is None or preset is None:
            missing.append(character_id)
            continue
        for variant in variants_for_profile(profile):
            if requested_variants and variant not in requested_variants:
                continue
            output_path = pipeline_root / "subjects" / "characters" / character_id / f"{variant}.md"
            if output_path.exists() and not args.force:
                skipped.append(f"{character_id}/{variant}")
                continue
            if not args.dry_run:
                output_path.parent.mkdir(parents=True, exist_ok=True)
                output_path.write_text(render_variant(character, preset, profile, variant), encoding="utf-8")
            created.append(f"{character_id}/{variant}")

    print(f"created {len(created)}")
    for item in created:
        print(f"  + {item}")
    print(f"skipped {len(skipped)} existing")
    for item in skipped:
        print(f"  = {item}")
    if missing:
        print(f"missing identity or preset {len(missing)}: {', '.join(missing)}")
        if not args.skip_missing_identity:
            return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
