#!/usr/bin/env python3
"""Apply stable presentation IconId fields to authored content assets."""
from __future__ import annotations

import argparse
import re
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
CONTENT_ROOT = REPO_ROOT / "Assets" / "Resources" / "_Game" / "Content" / "Definitions"


def scalar(text: str, key: str) -> str:
    match = re.search(rf"^  {re.escape(key)}:\s*(.*)$", text, re.MULTILINE)
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
