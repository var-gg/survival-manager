#!/usr/bin/env python3
"""Audit runtime roster and skill content against the 22-hero quota."""
from __future__ import annotations

import argparse
import math
import re
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any

import yaml


REPO_ROOT = Path(__file__).resolve().parents[2]
CONTENT_ROOT = REPO_ROOT / "Assets" / "Resources" / "_Game" / "Content" / "Definitions"
QUOTA_PATH = REPO_ROOT / "art-pipeline" / "config" / "combat_roster_skill_quota.yaml"
CHARACTER_MANIFEST_PATH = REPO_ROOT / "art-pipeline" / "config" / "character_asset_manifest.yaml"

SLOT_NAMES = {0: "CoreActive", 1: "UtilityActive", 2: "Passive", 3: "Support"}
DELIVERY_NAMES = {0: "Melee", 1: "Ranged", 2: "Projectile", 3: "Nova", 4: "Aura", 5: "Trap", 6: "Zone"}
DAMAGE_NAMES = {0: "Physical", 1: "Magical", 2: "Healing", 3: "True"}


def scalar(text: str, key: str) -> str:
    match = re.search(rf"^  {re.escape(key)}:[ \t]*(.*)$", text, re.MULTILINE)
    return match.group(1).strip() if match else ""


def enum_name(value: str, names: dict[int, str], fallback: str = "Unknown") -> str:
    try:
        return names.get(int(value), fallback)
    except ValueError:
        return fallback


def load_yaml(path: Path) -> dict[str, Any]:
    data = yaml.safe_load(path.read_text(encoding="utf-8"))
    if not isinstance(data, dict):
        raise ValueError(f"{path}: expected YAML mapping")
    return data


def build_guid_map() -> dict[str, Path]:
    result: dict[str, Path] = {}
    for meta in CONTENT_ROOT.rglob("*.meta"):
        match = re.search(r"^guid:\s*(\S+)", meta.read_text(encoding="utf-8", errors="ignore"), re.MULTILINE)
        if match:
            result[match.group(1)] = Path(str(meta)[:-5])
    return result


def top_ref_guid(text: str, key: str) -> str:
    match = re.search(rf"^  {re.escape(key)}:\s*{{[^}}]*guid:\s*([0-9a-f]+)", text, re.MULTILINE)
    return match.group(1) if match else ""


def section_ref_guids(text: str, key: str) -> list[str]:
    match = re.search(rf"^  {re.escape(key)}:\s*\n((?:  - .*\n)*)", text, re.MULTILINE)
    if not match:
        return []
    guids: list[str] = []
    for line in match.group(1).splitlines():
        ref = re.search(r"guid:\s*([0-9a-f]+)", line)
        if ref:
            guids.append(ref.group(1))
    return guids


def asset_id(path: Path) -> str:
    text = path.read_text(encoding="utf-8", errors="ignore")
    return scalar(text, "Id") or path.stem


def id_by_guid(guid_map: dict[str, Path], guid: str) -> str:
    path = guid_map.get(guid)
    return asset_id(path) if path and path.exists() else ""


def runtime_characters(guid_map: dict[str, Path]) -> list[dict[str, str]]:
    classes_by_guid = {
        guid: asset_id(path)
        for guid, path in guid_map.items()
        if path.parent.name == "Classes" and path.suffix == ".asset"
    }
    rows: list[dict[str, str]] = []
    for path in sorted((CONTENT_ROOT / "Characters").glob("*.asset")):
        text = path.read_text(encoding="utf-8")
        rows.append(
            {
                "id": scalar(text, "Id") or path.stem,
                "class": classes_by_guid.get(top_ref_guid(text, "Class"), id_by_guid(guid_map, top_ref_guid(text, "Class"))),
                "archetype": id_by_guid(guid_map, top_ref_guid(text, "DefaultArchetype")),
            }
        )
    return rows


def runtime_skills() -> dict[str, dict[str, Any]]:
    skills: dict[str, dict[str, Any]] = {}
    for path in sorted((CONTENT_ROOT / "Skills").glob("*.asset")):
        text = path.read_text(encoding="utf-8")
        skill_id = scalar(text, "Id") or path.stem
        skills[skill_id] = {
            "slot": enum_name(scalar(text, "SlotKind"), SLOT_NAMES),
            "delivery": enum_name(scalar(text, "Delivery"), DELIVERY_NAMES),
            "damage": enum_name(scalar(text, "DamageType"), DAMAGE_NAMES),
            "statuses": re.findall(r"^\s+StatusId:\s*(\S+)", text, re.MULTILINE),
        }
    return skills


def class_skill_unions(guid_map: dict[str, Path], skills: dict[str, dict[str, Any]]) -> dict[str, set[str]]:
    skill_by_guid = {
        guid: asset_id(path)
        for guid, path in guid_map.items()
        if path.parent.name == "Skills" and path.suffix == ".asset"
    }
    class_by_guid = {
        guid: asset_id(path)
        for guid, path in guid_map.items()
        if path.parent.name == "Classes" and path.suffix == ".asset"
    }
    result: dict[str, set[str]] = defaultdict(set)
    for path in sorted((CONTENT_ROOT / "Archetypes").glob("*.asset")):
        text = path.read_text(encoding="utf-8")
        class_id = class_by_guid.get(top_ref_guid(text, "Class"), id_by_guid(guid_map, top_ref_guid(text, "Class")))
        for key in ("Skills", "FlexUtilitySkillPool", "FlexSupportSkillPool", "RecruitFlexActivePool", "RecruitFlexPassivePool"):
            for guid in section_ref_guids(text, key):
                skill_id = skill_by_guid.get(guid, id_by_guid(guid_map, guid))
                if skill_id in skills:
                    result[class_id].add(skill_id)
    return result


def story_manifest_counts() -> tuple[int, int, int]:
    manifest = load_yaml(CHARACTER_MANIFEST_PATH)
    characters = manifest.get("characters", [])
    if not isinstance(characters, list):
        raise ValueError(f"{CHARACTER_MANIFEST_PATH}: characters must be a list")
    total = len(characters)
    npc = sum(1 for character in characters if str(character.get("id", "")).startswith("npc_"))
    return total, total - npc, npc


def print_gap(label: str, current: int, target: int, failures: list[str], strict: bool) -> None:
    gap = max(0, target - current)
    status = "OK" if gap == 0 else f"gap {gap}"
    print(f"{label}: current={current} target={target} {status}")
    if strict and gap:
        failures.append(f"{label}: current={current}, target={target}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--quota", type=Path, default=QUOTA_PATH)
    parser.add_argument("--strict", action="store_true")
    args = parser.parse_args()

    quota = load_yaml(args.quota)
    guid_map = build_guid_map()
    characters = runtime_characters(guid_map)
    skills = runtime_skills()
    class_unions = class_skill_unions(guid_map, skills)
    story_total, story_heroes, story_npcs = story_manifest_counts()

    failures: list[str] = []
    roster_policy = quota["policy"]["roster_layers"]
    target_roster = quota["target_roster"]
    skill_quota = quota["skill_quota"]

    print("[roster]")
    print_gap("runtime playable characters", len(characters), int(target_roster["total_playable_heroes"]), failures, args.strict)
    print(f"story manifest total={story_total} heroes/non-npc={story_heroes} npc={story_npcs}")
    expected_story_heroes = int(roster_policy["story_hero_target_non_npc"])
    expected_story_total = int(roster_policy["story_portrait_total_with_npc"])
    if story_heroes != expected_story_heroes:
        failures.append(f"story heroes: current={story_heroes}, target={expected_story_heroes}")
    if story_total != expected_story_total:
        failures.append(f"story total: current={story_total}, target={expected_story_total}")

    runtime_by_class = Counter(row["class"] for row in characters)
    for class_id, target in target_roster["class_targets"].items():
        print_gap(f"runtime class {class_id}", runtime_by_class.get(class_id, 0), int(target), failures, args.strict)

    print("\n[skills]")
    print_gap("skill definitions", len(skills), int(skill_quota["target_total_for_vfx_ready_22_roster"]), failures, args.strict)
    print(f"minimum for 22 roster={skill_quota['minimum_total_for_22_roster']}")
    slot_counts = Counter(skill["slot"] for skill in skills.values())
    for slot, target in skill_quota["slot_targets"].items():
        print_gap(f"slot {slot}", slot_counts.get(slot, 0), int(target), failures, args.strict)

    print("\n[class skill unions]")
    for class_id, target_info in skill_quota["class_skill_targets"].items():
        union = class_unions.get(class_id, set())
        print_gap(f"class skill union {class_id}", len(union), int(target_info["total"]), failures, args.strict)
        slot_minimums = target_info["slot_minimums"]
        class_slots = Counter(skills[skill_id]["slot"] for skill_id in union)
        for slot, minimum in slot_minimums.items():
            print_gap(f"class {class_id} slot {slot}", class_slots.get(slot, 0), int(minimum), failures, args.strict)

    print("\n[diversity]")
    diversity = quota["diversity_quota"]
    status_interactions = sum(1 for skill in skills.values() if skill["statuses"])
    unique_statuses = sorted({status for skill in skills.values() for status in skill["statuses"]})
    print_gap("status interaction skills", status_interactions, int(diversity["status_interaction_minimum"]), failures, args.strict)
    print(f"unique status ids={len(unique_statuses)} band={diversity['unique_status_id_band']['min']}-{diversity['unique_status_id_band']['max']} ids={unique_statuses}")

    delivery_counts = Counter(skill["delivery"] for skill in skills.values())
    aura_max = int(diversity["delivery_caps"]["Aura"]["max"])
    print(f"delivery Aura: current={delivery_counts.get('Aura', 0)} max={aura_max} {'OK' if delivery_counts.get('Aura', 0) <= aura_max else 'over cap'}")
    if args.strict and delivery_counts.get("Aura", 0) > aura_max:
        failures.append(f"delivery Aura over cap: current={delivery_counts.get('Aura', 0)}, max={aura_max}")
    ranged_projectile = delivery_counts.get("Ranged", 0) + delivery_counts.get("Projectile", 0)
    zone_nova_trap = delivery_counts.get("Zone", 0) + delivery_counts.get("Nova", 0) + delivery_counts.get("Trap", 0)
    print_gap("delivery Melee", delivery_counts.get("Melee", 0), int(diversity["delivery_minimums"]["Melee"]), failures, args.strict)
    print_gap("delivery RangedOrProjectile", ranged_projectile, int(diversity["delivery_minimums"]["RangedOrProjectile"]), failures, args.strict)
    print_gap("delivery ZoneNovaTrap", zone_nova_trap, int(diversity["delivery_minimums"]["ZoneNovaTrap"]), failures, args.strict)

    damage_counts = Counter(skill["damage"] for skill in skills.values())
    physical_max = int(diversity["damage_mix"]["Physical"]["max"])
    magical_or_true = damage_counts.get("Magical", 0) + damage_counts.get("True", 0)
    print(f"damage Physical: current={damage_counts.get('Physical', 0)} max={physical_max} {'OK' if damage_counts.get('Physical', 0) <= physical_max else 'over cap'}")
    if args.strict and damage_counts.get("Physical", 0) > physical_max:
        failures.append(f"damage Physical over cap: current={damage_counts.get('Physical', 0)}, max={physical_max}")
    print_gap("damage MagicalOrTrue", magical_or_true, int(diversity["damage_mix"]["MagicalOrTrue"]["min"]), failures, args.strict)
    print_gap("damage Healing", damage_counts.get("Healing", 0), int(diversity["damage_mix"]["Healing"]["min"]), failures, args.strict)

    if failures:
        print("\n[quota] FAIL")
        for failure in failures:
            print(f"  - {failure}")
        return 1

    print("\n[quota] OK" if args.strict else "\n[quota] report complete")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
