#!/usr/bin/env python3
"""Seed expansion-v1 skill icon sheet subjects from the design catalog."""
from __future__ import annotations

import argparse
from collections import Counter
from pathlib import Path
from typing import Any

import yaml


REPO_ROOT = Path(__file__).resolve().parents[2]
PIPELINE_ROOT = REPO_ROOT / "art-pipeline"
CATALOG_PATH = PIPELINE_ROOT / "config" / "skill_expansion_design_catalog.yaml"
SUBJECT_ROOT = PIPELINE_ROOT / "subjects" / "icons" / "skill"


def load_catalog(path: Path) -> dict[str, Any]:
    data = yaml.safe_load(path.read_text(encoding="utf-8"))
    if not isinstance(data, dict):
        raise ValueError(f"{path}: expected YAML mapping")
    return data


def validate_catalog(catalog: dict[str, Any]) -> None:
    skills = catalog.get("skills")
    sheets = catalog.get("sheets")
    if not isinstance(skills, list) or not isinstance(sheets, list):
        raise ValueError("catalog must contain skills and sheets lists")

    by_id = {str(skill["id"]): skill for skill in skills}
    if len(by_id) != len(skills):
        raise ValueError("duplicate skill ids in expansion catalog")
    if len(skills) != 36:
        raise ValueError(f"expected 36 expansion skills, found {len(skills)}")

    planned = [skill_id for sheet in sheets for skill_id in sheet.get("skills", [])]
    if sorted(planned) != sorted(by_id):
        missing = sorted(set(by_id) - set(planned))
        unknown = sorted(set(planned) - set(by_id))
        raise ValueError(f"sheet plan mismatch missing={missing} unknown={unknown}")
    for sheet in sheets:
        count = len(sheet.get("skills", []))
        if count != 4:
            raise ValueError(f"{sheet.get('id')}: expected exactly 4 skills, found {count}")

    expected_slots = catalog.get("slot_backlog", {})
    actual_slots = Counter(str(skill.get("slot", "")) for skill in skills)
    if dict(actual_slots) != dict(expected_slots):
        raise ValueError(f"slot backlog mismatch expected={expected_slots} actual={dict(actual_slots)}")

    expected_classes = catalog.get("class_backlog", {})
    actual_classes = Counter(str(skill.get("target_class", "")) for skill in skills)
    if dict(actual_classes) != dict(expected_classes):
        raise ValueError(f"class backlog mismatch expected={expected_classes} actual={dict(actual_classes)}")


def suffix(skill_id: str) -> str:
    return skill_id[len("skill_") :] if skill_id.startswith("skill_") else skill_id


def icon_line(skill: dict[str, Any]) -> str:
    statuses = skill.get("statuses") or []
    status_text = ", ".join(
        f"{status['id']}({status.get('duration', 0)}s/{status.get('magnitude', 0)})"
        for status in statuses
    ) or "none"
    return (
        f"{skill['id']} / {skill['name_en']} ({skill['name_ko']}): "
        f"{skill['target_class']} {skill['slot']} {skill['kind']}, "
        f"{skill['delivery']}, {skill['damage']}, power {skill['power']}, "
        f"cooldown {skill['cooldown']}, statuses {status_text}. "
        f"Effect: {skill['effect_en']}"
    )


def write_subject(catalog: dict[str, Any], sheet: dict[str, Any], skills_by_id: dict[str, dict[str, Any]], dry_run: bool) -> Path:
    sheet_id = str(sheet["id"])
    sheet_skills = [skills_by_id[str(skill_id)] for skill_id in sheet["skills"]]
    output_dir = catalog["icon_pipeline"]["output_dir"]
    refs = list(sheet.get("refs", []))
    fm = {
        "slug": f"{sheet_id}--default",
        "kind": "skill_icon_catalog_sheet",
        "subject_id": sheet_id,
        "variant": "default",
        "refs": refs,
        "aspect": "1:1",
        "output_size": catalog["icon_pipeline"]["sheet_size"],
        "chroma": "#FF00FF",
        "skills": [suffix(skill["id"]) for skill in sheet_skills],
        "output_directory": output_dir,
        "output_prefix": "skill_icon",
        "skill_bindings": [
            {
                "skill_id": skill["id"],
                "icon_id": skill["icon_id"],
                "target_hero": skill["target_hero"],
                "target_class": skill["target_class"],
                "slot": skill["slot"],
                "kind": skill["kind"],
                "delivery": skill["delivery"],
                "damage": skill["damage"],
                "statuses": skill.get("statuses") or [],
            }
            for skill in sheet_skills
        ],
        "style_seed": bool(sheet.get("style_seed", False)),
        "status": "prompted" if sheet.get("style_seed", False) else "blocked_style_seed_ref",
    }

    reading_order = "\n".join(
        f"- ({index % 2},{index // 2}): {skill['icon_id']} ({skill['name_en']})"
        for index, skill in enumerate(sheet_skills)
    )
    details = "\n\n".join(
        f"({index % 2},{index // 2}) {skill['icon_id']}:\n"
        f"{skill['icon_prompt']}. Palette: {skill['palette']}. Runtime: {icon_line(skill)}."
        for index, skill in enumerate(sheet_skills)
    )
    ref_note = (
        "This first sheet uses character portrait refs to lock the painterly hand, palette discipline, and line weight."
        if sheet.get("style_seed", False)
        else "Use the attached promoted seed icon refs as the canonical same-artist baseline from the first expansion sheet."
    )
    body = f"""# {sheet['title']}

Expansion-v1 skill icon sheet. {ref_note}

```prompt
=== EXPANSION V1 SKILL ICON SHEET ===
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
- Match the attached refs for painterly line quality and color restraint, but every cell must remain a standalone skill symbol.
- Icons in this sheet should share a coherent same-artist game style, while each symbol stays readable at 64 px.

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


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--catalog", type=Path, default=CATALOG_PATH)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    catalog = load_catalog(args.catalog)
    validate_catalog(catalog)
    skills_by_id = {str(skill["id"]): skill for skill in catalog["skills"]}
    paths = [
        write_subject(catalog, sheet, skills_by_id, args.dry_run)
        for sheet in catalog["sheets"]
    ]
    verb = "would seed" if args.dry_run else "seeded"
    print(f"[seed_skill_expansion_icon_subjects] {verb} {len(paths)} sheets / {len(skills_by_id)} skills")
    for path in paths:
        print(f"  - {path.relative_to(REPO_ROOT)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
