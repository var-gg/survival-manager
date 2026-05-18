#!/usr/bin/env python3
"""Apply stable presentation IconId fields to authored content assets."""
from __future__ import annotations

import argparse
import re
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
CONTENT_ROOT = REPO_ROOT / "Assets" / "Resources" / "_Game" / "Content" / "Definitions"


def scalar(text: str, key: str) -> str:
    match = re.search(rf"^  {re.escape(key)}:[ \t]*(.*)$", text, re.MULTILINE)
    return match.group(1).strip() if match else ""


def set_field_after(text: str, key: str, value: str, after_key: str) -> str:
    field_pattern = re.compile(rf"(^  {re.escape(key)}:\s*).*$", re.MULTILINE)
    if field_pattern.search(text):
        return field_pattern.sub(rf"\g<1>{value}", text, count=1)

    after_pattern = re.compile(rf"(^  {re.escape(after_key)}:.*(?:\r?\n))", re.MULTILINE)
    if not after_pattern.search(text):
        raise ValueError(f"missing insertion anchor {after_key}")
    return after_pattern.sub(rf"\g<1>  {key}: {value}\n", text, count=1)


def resolve_skill_icon_id(skill_id: str) -> str:
    if not skill_id:
        return ""
    suffix = skill_id.removeprefix("skill_")
    return f"skill_icon_{suffix}"


def resolve_item_icon_id(item_id: str, slot_type: str, weapon_family: str) -> str:
    if slot_type == "0":
        family = weapon_family.strip().lower() or infer_weapon_family(item_id)
        if family in {"shield", "bow", "focus"}:
            return f"item_icon_{family}"
        return "item_icon_blade"
    if slot_type == "1":
        return "item_icon_armor"
    return "item_icon_trinket"


def infer_weapon_family(item_id: str) -> str:
    lower = item_id.lower()
    if "shield" in lower:
        return "shield"
    if "bow" in lower:
        return "bow"
    if "focus" in lower or "bead" in lower:
        return "focus"
    return "blade"


def resolve_augment_icon_id(augment_id: str, family_id: str) -> str:
    tokens = f"{augment_id} {family_id}".lower()
    if any(token in tokens for token in ("ward", "guard", "bastion", "wall", "oath")):
        return "augment_shield"
    if any(token in tokens for token in ("hunt", "scope", "signal", "eye")):
        return "augment_eye"
    if any(token in tokens for token in ("haste", "stride", "reach", "overrun", "spur")):
        return "augment_wing"
    if any(token in tokens for token in ("fury", "reckoning", "blade", "fang")):
        return "augment_blade"
    if any(token in tokens for token in ("mending", "clarity", "focus", "grace", "chalice")):
        return "augment_seal"
    if any(token in tokens for token in ("hex", "catacomb", "bone", "void")):
        return "augment_void"
    if any(token in tokens for token in ("pack", "pact", "hinterland", "hide")):
        return "augment_moon"
    return "augment_star"


def update_assets(root: Path, kind: str, dry_run: bool) -> int:
    changed = 0
    for path in sorted(root.glob("*.asset")):
        text = path.read_text(encoding="utf-8")
        asset_id = scalar(text, "Id") or path.stem
        if kind == "skill":
            icon_id = resolve_skill_icon_id(asset_id)
            updated = set_field_after(text, "IconId", icon_id, "AnimationHookId")
        elif kind == "item":
            icon_id = resolve_item_icon_id(asset_id, scalar(text, "SlotType"), scalar(text, "WeaponFamilyTag"))
            updated = set_field_after(text, "IconId", icon_id, "DescriptionKey")
        elif kind == "augment":
            icon_id = resolve_augment_icon_id(asset_id, scalar(text, "FamilyId"))
            updated = set_field_after(text, "IconId", icon_id, "DescriptionKey")
        else:
            raise ValueError(kind)

        if updated != text:
            changed += 1
            if not dry_run:
                path.write_text(updated, encoding="utf-8", newline="\n")
    return changed


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    counts = {
        "skill": update_assets(CONTENT_ROOT / "Skills", "skill", args.dry_run),
        "item": update_assets(CONTENT_ROOT / "Items", "item", args.dry_run),
        "augment": update_assets(CONTENT_ROOT / "Augments", "augment", args.dry_run),
    }
    for kind, count in counts.items():
        verb = "would update" if args.dry_run else "updated"
        print(f"[apply_content_icon_ids] {verb} {count} {kind} assets")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
