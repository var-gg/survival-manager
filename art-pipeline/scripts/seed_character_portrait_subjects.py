#!/usr/bin/env python3
"""Create missing portrait_full_default subject pages from P09 identity data."""
from __future__ import annotations

import argparse
import sys
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


def find_pipeline_root(start: Path) -> Path:
    for candidate in [start.resolve(), *start.resolve().parents]:
        if candidate.name == "art-pipeline":
            return candidate
    return Path(__file__).resolve().parents[1]


def frontmatter(character_id: str) -> str:
    data = {
        "slug": f"{character_id}--portrait_full_default",
        "kind": "character_portrait_full",
        "subject_id": character_id,
        "variant": "portrait_full",
        "emotion": "default",
        "refs": [character_id, "hero_dawn_priest:portrait_full_style_seed_best"],
        "aspect": "2:3",
        "output_size": "1024x1536",
        "chroma": "#FF00FF",
        "status": "prompted",
    }
    return "---\n" + yaml.safe_dump(data, allow_unicode=True, sort_keys=False).strip() + "\n---\n"


def color_line(label: str, value: str | None) -> str:
    return f"- {label}: {value}" if value else f"- {label}: read from REF"


def render_subject(character: dict[str, Any], preset: dict[str, Any]) -> str:
    character_id = character["id"]
    display_name = character.get("display_name", character_id)
    visual_intent = character.get("visual_intent", "")
    class_line = f"{character.get('faction', 'unknown')} / {character.get('combat_class', 'unknown')}"
    colors = [
        color_line("Hair", main_color(preset, "hair")),
        color_line("Chest / top color zone", main_color(preset, "chest")),
        color_line("Waist / lower color zone", main_color(preset, "waist")),
        color_line("Leg / lower color zone", main_color(preset, "leg")),
        color_line("Weapon", main_color(preset, "weapon")),
        color_line("Shield", main_color(preset, "shield")) if preset.get("shieldId", 0) else "- Shield: none in P09 preset",
    ]
    ids = [
        f"- SexId {preset.get('sexId')} / FaceTypeId {preset.get('faceTypeId')} / HairStyleId {preset.get('hairStyleId')} / HairColorId {preset.get('hairColorId')}",
        f"- SkinId {preset.get('skinId')} / EyeColorId {preset.get('eyeColorId')} / FacialHairId {preset.get('facialHairId')} / BustSizeId {preset.get('bustSizeId')}",
        f"- Outfit {readable_outfit(preset)} / Head {preset.get('headId')} / Chest {preset.get('chestId')} / Arm {preset.get('armId')} / Waist {preset.get('waistId')} / Leg {preset.get('legId')}",
    ]

    shield_line = (
        "Shield presence: render the shield exactly as shown in the P09 reference."
        if preset.get("shieldId", 0)
        else "Shield slot is empty: do not add a shield, buckler, mirror shield, or defensive prop."
    )
    prompt = f"""
# {display_name} — Portrait Full Default

## 생성 의도

첫 번째 reference는 이 캐릭터의 P09 canon이다. 두 번째 reference `portrait_full_style_seed_best`는 단린 best full-body illustration seed이며 작가감, 선 밀도, 얼굴 polish, painterly rendering, clean silhouette만 맞추는 style-only seed다. 단린의 의상, 무기, 색상, 포즈, 사제 motif는 가져오지 않는다.

## prompt 명세

```prompt
Create one full-body polished JRPG character illustration of {display_name}, using the attached references with strict priority:

REFERENCE PRIORITY
1. The attached P09 model reference for {character_id} is CANON for identity, outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The attached Dawn Priest full-body illustration is STYLE ONLY. Match its professional 2D illustration quality, elegant face polish, confident hand-painted linework, layered cloth/armor rendering, clean silhouette outline, and refined character-art finish. Do NOT copy Dawn Priest costume, sword/shield setup, pose, colors, face, or priest motifs.

The illustration is a high-resolution painterly production version of the exact P09 character. Preserve canon, but do not make a rigid 3D-to-2D conversion.

DO NOT add garments, props, ornaments, scars, tattoos, pouches, capes, hoods, religious symbols, tribal decorations, or extra weapons that are not visible in the REF. Do not replace the P09 outfit with a genre-standard class costume.

=== CHARACTER IDENTITY CONTRACT ===
- Character: {display_name}
- Pipeline subject_id: {character_id}
- Faction / job lane: {class_line}
- Uniform group: {character.get('uniform_group', 'unknown')}
- Silhouette family: {character.get('silhouette_family', 'unknown')}
- Intent: {visual_intent}

=== P09 CURRENT IDS ===
{chr(10).join(ids)}

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when the capture lighting obscures the canonical color. The REF shape plus these color anchors is the character identity.
{chr(10).join(colors)}

=== VARIANT: portrait_full / default emotion ===
Expression: neutral default state that fits the character identity above. Keep the face readable and distinct from same-faction or same-job characters. Make the face appealing and professionally illustrated, not a literal 3D model face.

Pose and composition:
- Full body, single subject, vertical 2:3.
- Three-quarter front view, slight low camera angle, both feet visible.
- Keep the weapon and shield presence exactly as the REF shows.
- {shield_line}
- Head-to-feet visible with small margin at top and bottom.

OUTPUT: 1024 x 1536 PNG.

=== HARD CONSTRAINTS ===
- Background: solid #FF00FF magenta, flat fill, no gradient, no cast shadow.
- 1-2 px clean dark outline around the entire subject silhouette for chroma cleanup.
- No magenta tint on the subject.
- No companions, no environment, no UI frame.

=== STYLE TARGET ===
- Same internal house style as the Dawn Priest full-body seed: polished hand-painted JRPG character art, elegant face rendering, crisp costume zone separation, painterly folds and metal highlights, controlled cinematic rim light.
- Color zones must remain readable at thumbnail size; avoid monochrome blobs.
- Keep only this character's P09 weapon and shield state.
```
"""
    return frontmatter(character_id) + textwrap.dedent(prompt).lstrip()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--asset-manifest", type=Path, default=None)
    parser.add_argument("--identity-manifest", type=Path, default=None)
    parser.add_argument("--asset-dir", type=Path, default=None)
    parser.add_argument("--ids", nargs="*", default=None, help="only write the listed character ids")
    parser.add_argument("--force", action="store_true", help="overwrite existing portrait_full_default.md")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    pipeline_root = find_pipeline_root(Path(__file__))
    repo_root = pipeline_root.parent
    asset_manifest = load_yaml(args.asset_manifest or (pipeline_root / "config" / "character_asset_manifest.yaml"))
    identity_manifest = load_yaml(args.identity_manifest or (pipeline_root / "config" / "p09_visual_identity_manifest.yaml"))
    asset_dir = args.asset_dir or (repo_root / "Assets" / "Resources" / "_Game" / "Battle" / "Appearances" / "P09")
    presets = load_presets(asset_dir)
    identity_by_id = {item["id"]: item for item in identity_manifest.get("characters", [])}

    created: list[str] = []
    skipped: list[str] = []
    missing: list[str] = []
    requested_ids = set(args.ids or [])
    for character in asset_manifest.get("characters", []):
        character_id = character["id"]
        if requested_ids and character_id not in requested_ids:
            continue
        identity = identity_by_id.get(character_id)
        preset = presets.get(character_id)
        if identity is None or preset is None:
            missing.append(character_id)
            continue

        output_path = pipeline_root / "subjects" / "characters" / character_id / "portrait_full_default.md"
        if output_path.exists() and not args.force:
            skipped.append(character_id)
            continue

        if not args.dry_run:
            output_path.parent.mkdir(parents=True, exist_ok=True)
            output_path.write_text(render_subject(identity, preset), encoding="utf-8")
        created.append(character_id)

    print(f"created {len(created)}: {', '.join(created) if created else '-'}")
    print(f"skipped {len(skipped)} existing: {', '.join(skipped) if skipped else '-'}")
    if missing:
        print(f"missing identity or preset {len(missing)}: {', '.join(missing)}")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
