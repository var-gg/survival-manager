#!/usr/bin/env python3
"""Audit the expansion skill design catalog against the 22-hero quota."""
from __future__ import annotations

import argparse
import re
from collections import Counter
from pathlib import Path
from typing import Any

import yaml


REPO_ROOT = Path(__file__).resolve().parents[2]
CONTENT_ROOT = REPO_ROOT / "Assets" / "Resources" / "_Game" / "Content" / "Definitions"
CATALOG_PATH = REPO_ROOT / "art-pipeline" / "config" / "skill_expansion_design_catalog.yaml"
QUOTA_PATH = REPO_ROOT / "art-pipeline" / "config" / "combat_roster_skill_quota.yaml"

SLOT_NAMES = {0: "CoreActive", 1: "UtilityActive", 2: "Passive", 3: "Support"}
DELIVERY_NAMES = {0: "Melee", 1: "Ranged", 2: "Projectile", 3: "Nova", 4: "Aura", 5: "Trap", 6: "Zone"}
DAMAGE_NAMES = {0: "Physical", 1: "Magical", 2: "Healing", 3: "True"}


def load_yaml(path: Path) -> dict[str, Any]:
    data = yaml.safe_load(path.read_text(encoding="utf-8"))
    if not isinstance(data, dict):
        raise ValueError(f"{path}: expected YAML mapping")
    return data


def scalar(text: str, key: str) -> str:
    match = re.search(rf"^  {re.escape(key)}:[ \t]*(.*)$", text, re.MULTILINE)
    return match.group(1).strip() if match else ""


def stable_tag_ids_by_guid() -> dict[str, str]:
    result: dict[str, str] = {}
    for path in sorted((CONTENT_ROOT / "StableTags").glob("*.asset")):
        text = path.read_text(encoding="utf-8")
        tag_id = scalar(text, "Id")
        meta = path.with_name(f"{path.name}.meta")
        if not tag_id or not meta.is_file():
            continue
        meta_text = meta.read_text(encoding="utf-8")
        match = re.search(r"^guid:\s*([0-9a-fA-F]{32})\s*$", meta_text, re.MULTILINE)
        if match:
            result[match.group(1)] = tag_id
    return result


def section_guids(text: str, section: str) -> list[str]:
    match = re.search(rf"^  {re.escape(section)}:\s*(?:\[\])?\s*$", text, re.MULTILINE)
    if not match:
        return []
    start = match.end()
    end_match = re.search(r"^  [A-Za-z0-9_]+:", text[start:], re.MULTILINE)
    end = start + end_match.start() if end_match else len(text)
    return re.findall(r"guid:\s*([0-9a-fA-F]{32})", text[start:end])


def enum_name(value: str, names: dict[int, str]) -> str:
    try:
        return names[int(value)]
    except (KeyError, ValueError):
        return value or "Unknown"


def runtime_skill_rows() -> list[dict[str, Any]]:
    tags_by_guid = stable_tag_ids_by_guid()
    rows: list[dict[str, Any]] = []
    for path in sorted((CONTENT_ROOT / "Skills").glob("*.asset")):
        text = path.read_text(encoding="utf-8")
        rows.append(
            {
                "id": scalar(text, "Id") or path.stem,
                "icon_id": scalar(text, "IconId"),
                "slot": enum_name(scalar(text, "SlotKind"), SLOT_NAMES),
                "delivery": enum_name(scalar(text, "Delivery"), DELIVERY_NAMES),
                "damage": enum_name(scalar(text, "DamageType"), DAMAGE_NAMES),
                "statuses": re.findall(r"^\s+StatusId:\s*(\S+)", text, re.MULTILINE),
                "required_classes": [
                    tags_by_guid[guid]
                    for guid in section_guids(text, "RequiredClassTags")
                    if guid in tags_by_guid
                ],
            }
        )
    return rows


def expansion_rows(catalog: dict[str, Any]) -> list[dict[str, Any]]:
    skills = catalog.get("skills")
    if not isinstance(skills, list):
        raise ValueError("catalog.skills must be a list")
    rows: list[dict[str, Any]] = []
    for skill in skills:
        rows.append(
            {
                "id": str(skill["id"]),
                "icon_id": str(skill["icon_id"]),
                "slot": str(skill["slot"]),
                "target_class": str(skill["target_class"]),
                "delivery": str(skill["delivery"]),
                "damage": str(skill["damage"]),
                "statuses": [str(status["id"]) for status in skill.get("statuses", [])],
            }
        )
    return rows


def validate_catalog(catalog: dict[str, Any], failures: list[str]) -> None:
    rows = expansion_rows(catalog)
    ids = [row["id"] for row in rows]
    if len(ids) != len(set(ids)):
        failures.append("duplicate expansion skill ids")
    true_damage = [row["id"] for row in rows if row["damage"] == "True"]
    if true_damage:
        failures.append(f"True damage is blocked by current V1 validation policy: {true_damage}")

    sheets = catalog.get("sheets")
    if not isinstance(sheets, list):
        failures.append("catalog.sheets must be a list")
        return
    planned = [skill_id for sheet in sheets for skill_id in sheet.get("skills", [])]
    if sorted(planned) != sorted(ids):
        failures.append("sheet plan does not cover exactly the expansion skill ids")
    bad_sheets = [sheet.get("id") for sheet in sheets if len(sheet.get("skills", [])) != 4]
    if bad_sheets:
        failures.append(f"sheets must contain exactly 4 skills: {bad_sheets}")

    for key, counter_key in (("slot_backlog", "slot"), ("class_backlog", "target_class")):
        expected = catalog.get(key, {})
        actual = Counter(row[counter_key] for row in rows)
        if dict(actual) != dict(expected):
            failures.append(f"{key} mismatch expected={expected} actual={dict(actual)}")


def validate_authored_assets(current: list[dict[str, Any]], expansion: list[dict[str, Any]], failures: list[str]) -> None:
    current_by_id = {row["id"]: row for row in current}
    authored = 0
    for row in expansion:
        current_row = current_by_id.get(row["id"])
        if current_row is None:
            continue
        authored += 1
        for key in ("icon_id", "slot", "delivery", "damage"):
            if current_row.get(key) != row.get(key):
                failures.append(f"{row['id']}: authored {key}={current_row.get(key)!r}, catalog={row.get(key)!r}")
        if current_row.get("required_classes") != [row["target_class"]]:
            failures.append(
                f"{row['id']}: authored required_classes={current_row.get('required_classes')!r}, catalog target_class={row['target_class']!r}"
            )
        if sorted(current_row.get("statuses", [])) != sorted(row.get("statuses", [])):
            failures.append(f"{row['id']}: authored statuses={current_row.get('statuses')!r}, catalog={row.get('statuses')!r}")
    print(f"authored expansion skills={authored}/{len(expansion)}")


def compare_min(label: str, current: int, target: int, failures: list[str]) -> None:
    status = "OK" if current >= target else f"gap {target - current}"
    print(f"{label}: projected={current} target>={target} {status}")
    if current < target:
        failures.append(f"{label}: projected={current}, target={target}")


def compare_max(label: str, current: int, limit: int, failures: list[str]) -> None:
    status = "OK" if current <= limit else f"over {current - limit}"
    print(f"{label}: projected={current} max={limit} {status}")
    if current > limit:
        failures.append(f"{label}: projected={current}, max={limit}")


def row_classes(row: dict[str, Any]) -> list[str]:
    if "target_class" in row:
        return [row["target_class"]]
    return [str(value) for value in row.get("required_classes", [])]


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--catalog", type=Path, default=CATALOG_PATH)
    parser.add_argument("--quota", type=Path, default=QUOTA_PATH)
    parser.add_argument(
        "--strict-class-pools",
        action="store_true",
        help="fail class_skill_targets gaps; default is advisory until the skill acquisition model is settled",
    )
    args = parser.parse_args()

    catalog = load_yaml(args.catalog)
    quota = load_yaml(args.quota)
    current = runtime_skill_rows()
    expansion = expansion_rows(catalog)
    current_ids = {row["id"] for row in current}
    pending_expansion = [row for row in expansion if row["id"] not in current_ids]
    projected = current + pending_expansion

    failures: list[str] = []
    validate_catalog(catalog, failures)
    validate_authored_assets(current, expansion, failures)

    skill_quota = quota["skill_quota"]
    diversity = quota["diversity_quota"]

    print("[catalog]")
    print(f"expansion skills={len(expansion)} sheets={len(catalog.get('sheets', []))}")
    print(f"pending expansion skills={len(pending_expansion)}")
    print(f"expansion slots={dict(Counter(row['slot'] for row in expansion))}")
    print(f"expansion classes={dict(Counter(row['target_class'] for row in expansion))}")

    print("\n[projected skill quota]")
    compare_min("skill definitions", len(projected), int(skill_quota["target_total_for_vfx_ready_22_roster"]), failures)
    slot_counts = Counter(row["slot"] for row in projected)
    for slot, target in skill_quota["slot_targets"].items():
        compare_min(f"slot {slot}", slot_counts.get(slot, 0), int(target), failures)

    print("\n[projected class pools]")
    class_failures: list[str] = []
    class_targets = skill_quota.get("class_skill_targets", {})
    class_slot_counts: dict[str, Counter[str]] = {}
    for row in projected:
        for class_id in row_classes(row):
            if not class_id:
                continue
            class_slot_counts.setdefault(class_id, Counter())[row["slot"]] += 1
    for class_id, target in class_targets.items():
        if not isinstance(target, dict) or "total" not in target:
            continue
        counts = class_slot_counts.get(class_id, Counter())
        compare_min(f"class {class_id} total", sum(counts.values()), int(target["total"]), class_failures)
        for slot, slot_target in target.get("slot_minimums", {}).items():
            compare_min(f"class {class_id} slot {slot}", counts.get(slot, 0), int(slot_target), class_failures)
    if class_failures:
        print("class pool gaps are advisory until skill acquisition/loadout ownership is settled")
        if args.strict_class_pools:
            failures.extend(class_failures)

    print("\n[projected diversity]")
    delivery_counts = Counter(row["delivery"] for row in projected)
    compare_max("delivery Aura", delivery_counts.get("Aura", 0), int(diversity["delivery_caps"]["Aura"]["max"]), failures)
    compare_min("delivery Melee", delivery_counts.get("Melee", 0), int(diversity["delivery_minimums"]["Melee"]), failures)
    compare_min(
        "delivery RangedOrProjectile",
        delivery_counts.get("Ranged", 0) + delivery_counts.get("Projectile", 0),
        int(diversity["delivery_minimums"]["RangedOrProjectile"]),
        failures,
    )
    compare_min(
        "delivery ZoneNovaTrap",
        delivery_counts.get("Zone", 0) + delivery_counts.get("Nova", 0) + delivery_counts.get("Trap", 0),
        int(diversity["delivery_minimums"]["ZoneNovaTrap"]),
        failures,
    )

    damage_counts = Counter(row["damage"] for row in projected)
    compare_max("damage Physical", damage_counts.get("Physical", 0), int(diversity["damage_mix"]["Physical"]["max"]), failures)
    compare_min(
        "damage MagicalOrTrue",
        damage_counts.get("Magical", 0) + damage_counts.get("True", 0),
        int(diversity["damage_mix"]["MagicalOrTrue"]["min"]),
        failures,
    )
    compare_min("damage Healing", damage_counts.get("Healing", 0), int(diversity["damage_mix"]["Healing"]["min"]), failures)

    status_interactions = sum(1 for row in projected if row["statuses"])
    unique_statuses = sorted({status for row in projected for status in row["statuses"]})
    compare_min("status interaction skills", status_interactions, int(diversity["status_interaction_minimum"]), failures)
    band = diversity["unique_status_id_band"]
    print(f"unique status ids={len(unique_statuses)} band={band['min']}-{band['max']} ids={unique_statuses}")
    if not (int(band["min"]) <= len(unique_statuses) <= int(band["max"])):
        failures.append(f"unique status ids out of band: {len(unique_statuses)}")

    if failures:
        print("\n[skill expansion catalog] FAIL")
        for failure in failures:
            print(f"  - {failure}")
        return 1

    print("\n[skill expansion catalog] OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
