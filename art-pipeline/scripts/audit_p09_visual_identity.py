#!/usr/bin/env python3
"""Audit P09 character visual identity, aliases, and outfit overlap.

This is a read-only preflight for wiki/model-capture preparation. It compares
the design-facing visual identity manifest with actual Unity P09 preset YAML.
"""
from __future__ import annotations

import argparse
import json
import math
import re
import sys
from collections import defaultdict
from pathlib import Path
from typing import Any

import yaml


PART_KEYS = [
    "sexId",
    "faceTypeId",
    "hairStyleId",
    "hairColorId",
    "skinId",
    "eyeColorId",
    "facialHairId",
    "bustSizeId",
    "headId",
    "chestId",
    "armId",
    "waistId",
    "legId",
    "weaponId",
    "shieldId",
]

PART_NAMES = {
    1: "weapon",
    2: "shield",
    3: "head",
    4: "chest",
    5: "arm",
    6: "waist",
    7: "leg",
    10: "hair",
}


def find_pipeline_root(start: Path) -> Path:
    for candidate in [start.resolve(), *start.resolve().parents]:
        if candidate.name == "art-pipeline":
            return candidate
    return Path(__file__).resolve().parents[1]


def load_yaml(path: Path) -> dict[str, Any]:
    data = yaml.safe_load(path.read_text(encoding="utf-8"))
    if not isinstance(data, dict):
        raise ValueError(f"{path}: expected YAML mapping")
    return data


def parse_scalar(text: str) -> int | str:
    text = text.strip()
    if text.startswith('"') and text.endswith('"'):
        return text[1:-1]
    if re.fullmatch(r"-?\d+", text):
        return int(text)
    return text


def parse_color_inline(text: str) -> str | None:
    match = re.search(
        r"\{r:\s*([0-9.]+),\s*g:\s*([0-9.]+),\s*b:\s*([0-9.]+),\s*a:\s*([0-9.]+)\}",
        text,
    )
    if not match:
        return None
    channels = [max(0, min(255, round(float(value) * 255))) for value in match.groups()[:3]]
    return "#{:02X}{:02X}{:02X}".format(*channels)


def parse_preset_asset(path: Path) -> dict[str, Any]:
    text = path.read_text(encoding="utf-8")
    data: dict[str, Any] = {"asset_path": str(path), "colors": []}

    for key in ["characterId", "displayName", *PART_KEYS]:
        match = re.search(rf"^\s*{re.escape(key)}:\s*(.+)$", text, re.MULTILINE)
        if match:
            data[key] = parse_scalar(match.group(1))

    current: dict[str, Any] | None = None
    for line in text.splitlines():
        if re.match(r"^\s*-\s+Label:", line):
            if current is not None:
                data["colors"].append(current)
            current = {"label": parse_scalar(line.split(":", 1)[1])}
            continue
        if current is None:
            continue
        field = re.match(r"^\s*(TargetPart|TargetContains|Enabled|MainColor|SecondColor|ThirdColor|UseEmissionColor|EmissionColor):\s*(.+)$", line)
        if not field:
            continue
        key = field.group(1)
        value = field.group(2)
        if key.endswith("Color"):
            current[key] = parse_color_inline(value) or value.strip()
        else:
            current[key] = parse_scalar(value)

    if current is not None:
        data["colors"].append(current)

    return data


def load_presets(asset_dir: Path) -> dict[str, dict[str, Any]]:
    result: dict[str, dict[str, Any]] = {}
    for path in sorted(asset_dir.glob("p09_appearance_*.asset")):
        preset = parse_preset_asset(path)
        character_id = preset.get("characterId")
        if isinstance(character_id, str) and character_id:
            result[character_id] = preset
    return result


def main_color(preset: dict[str, Any], part_name: str) -> str | None:
    for color in preset.get("colors", []):
        part = color.get("TargetPart")
        if isinstance(part, int) and PART_NAMES.get(part) == part_name:
            value = color.get("MainColor")
            return value if isinstance(value, str) else None
    return None


def hex_to_rgb(color: str | None) -> tuple[float, float, float] | None:
    if not color or not re.fullmatch(r"#[0-9A-Fa-f]{6}", color):
        return None
    return (
        int(color[1:3], 16) / 255.0,
        int(color[3:5], 16) / 255.0,
        int(color[5:7], 16) / 255.0,
    )


def color_distance(left: str | None, right: str | None) -> float | None:
    left_rgb = hex_to_rgb(left)
    right_rgb = hex_to_rgb(right)
    if left_rgb is None or right_rgb is None:
        return None
    return math.sqrt(sum((a - b) ** 2 for a, b in zip(left_rgb, right_rgb)))


def palette_distance(left: dict[str, Any], right: dict[str, Any]) -> float | None:
    channels = [
        color_distance(main_color(left, "hair"), main_color(right, "hair")),
        color_distance(main_color(left, "chest"), main_color(right, "chest")),
        color_distance(main_color(left, "waist"), main_color(right, "waist")),
    ]
    present = [value for value in channels if value is not None]
    if not present:
        return None
    return sum(present) / len(present)


def body_signature(preset: dict[str, Any]) -> tuple[Any, ...]:
    return tuple(preset.get(key) for key in PART_KEYS)


def outfit_signature(preset: dict[str, Any]) -> tuple[Any, ...]:
    return tuple(preset.get(key) for key in ["headId", "chestId", "armId", "waistId", "legId", "weaponId", "shieldId"])


def armor_signature(preset: dict[str, Any]) -> tuple[Any, ...]:
    return tuple(preset.get(key) for key in ["headId", "chestId", "armId", "waistId", "legId"])


def palette_signature(preset: dict[str, Any]) -> tuple[str | None, ...]:
    return tuple(main_color(preset, part) for part in ["hair", "head", "chest", "waist", "leg", "weapon", "shield"])


def readable_outfit(preset: dict[str, Any]) -> str:
    armor = preset.get("chestId", "?")
    weapon = preset.get("weaponId", "?")
    shield = preset.get("shieldId", "?")
    return f"A{armor}/W{weapon}/S{shield}"


def audit(manifest: dict[str, Any], presets: dict[str, dict[str, Any]]) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    findings: list[dict[str, Any]] = []
    characters = manifest.get("characters", [])
    by_id = {item["id"]: item for item in characters}
    canonical_ids = [item["id"] for item in characters]

    rows: list[dict[str, Any]] = []
    for item in characters:
        character_id = item["id"]
        preset = presets.get(character_id)
        if preset is None:
            findings.append({"severity": "error", "kind": "missing_preset", "characters": [character_id]})
            continue
        if preset.get("characterId") != character_id:
            findings.append(
                {
                    "severity": "error",
                    "kind": "character_id_mismatch",
                    "characters": [character_id],
                    "actual": preset.get("characterId"),
                }
            )
        rows.append(
            {
                "id": character_id,
                "faction": item.get("faction", ""),
                "combat_class": item.get("combat_class", ""),
                "uniform_group": item.get("uniform_group", ""),
                "outfit": readable_outfit(preset),
                "hair": main_color(preset, "hair"),
                "chest": main_color(preset, "chest"),
                "waist": main_color(preset, "waist"),
            }
        )

    for alias in manifest.get("alias_presets", []):
        alias_id = alias["alias_id"]
        target_id = alias["target_id"]
        alias_preset = presets.get(alias_id)
        target_preset = presets.get(target_id)
        if alias_preset is None or target_preset is None:
            findings.append(
                {
                    "severity": "error",
                    "kind": "alias_missing",
                    "characters": [alias_id, target_id],
                }
            )
            continue
        if body_signature(alias_preset) != body_signature(target_preset) or palette_signature(alias_preset) != palette_signature(target_preset):
            findings.append(
                {
                    "severity": "error",
                    "kind": "alias_drift",
                    "characters": [alias_id, target_id],
                    "detail": "runtime alias no longer matches its named target preset",
                }
            )

    exact_groups: dict[tuple[Any, ...], list[str]] = defaultdict(list)
    outfit_groups: dict[tuple[Any, ...], list[str]] = defaultdict(list)
    armor_groups: dict[tuple[Any, ...], list[str]] = defaultdict(list)
    faction_class_groups: dict[tuple[str, str], list[str]] = defaultdict(list)

    for character_id in canonical_ids:
        preset = presets.get(character_id)
        item = by_id[character_id]
        if preset is None:
            continue
        exact_groups[(body_signature(preset), palette_signature(preset))].append(character_id)
        outfit_groups[outfit_signature(preset)].append(character_id)
        armor_groups[armor_signature(preset)].append(character_id)
        faction_class_groups[(item.get("faction", ""), item.get("combat_class", ""))].append(character_id)

    for duplicate_ids in exact_groups.values():
        if len(duplicate_ids) > 1:
            findings.append(
                {
                    "severity": "error",
                    "kind": "exact_character_duplicate",
                    "characters": duplicate_ids,
                    "detail": "non-alias characters share body, outfit, weapon, shield, and palette signature",
                }
            )

    for duplicate_ids in outfit_groups.values():
        if len(duplicate_ids) <= 1:
            continue
        lanes = {(by_id[item].get("faction", ""), by_id[item].get("combat_class", "")) for item in duplicate_ids}
        groups = {by_id[item].get("uniform_group", "") for item in duplicate_ids}
        distances = []
        for index, left_id in enumerate(duplicate_ids):
            for right_id in duplicate_ids[index + 1 :]:
                distance = palette_distance(presets[left_id], presets[right_id])
                if distance is not None:
                    distances.append(distance)
        min_distance = min(distances) if distances else None
        if len(lanes) > 1 or len(groups) > 1:
            findings.append(
                {
                    "severity": "warning",
                    "kind": "cross_lane_outfit_overlap",
                    "characters": duplicate_ids,
                    "palette_distance_min": round(min_distance, 3) if min_distance is not None else None,
                    "detail": "same armor/weapon/shield appears across different faction, job, or uniform group",
                }
            )
        elif min_distance is not None and min_distance < 0.28:
            findings.append(
                {
                    "severity": "warning",
                    "kind": "same_lane_low_palette_contrast",
                    "characters": duplicate_ids,
                    "palette_distance_min": round(min_distance, 3),
                    "detail": "same faction/job/outfit needs stronger color or hair separation",
                }
            )

    for duplicate_ids in armor_groups.values():
        if len(duplicate_ids) <= 1:
            continue
        lanes = {(by_id[item].get("faction", ""), by_id[item].get("combat_class", "")) for item in duplicate_ids}
        if len(lanes) <= 1:
            continue
        findings.append(
            {
                "severity": "review",
                "kind": "armor_family_reuse",
                "characters": duplicate_ids,
                "detail": "same armor geometry appears across multiple faction/job lanes; verify weapon, hair, palette, and wiki role text keep them readable",
            }
        )

    for (faction, combat_class), ids in faction_class_groups.items():
        if len(ids) <= 1:
            continue
        outfits = {outfit_signature(presets[item]) for item in ids if item in presets}
        if len(outfits) > 1:
            findings.append(
                {
                    "severity": "review",
                    "kind": "same_faction_class_outfit_variation",
                    "characters": ids,
                    "detail": f"{faction}/{combat_class} uses multiple outfit signatures; confirm rank/story reason in wiki text",
                }
            )

    return rows, findings


def print_table(rows: list[dict[str, Any]], findings: list[dict[str, Any]]) -> None:
    headers = ["character", "faction", "class", "group", "outfit", "hair", "chest", "waist"]
    body = [
        [
            row["id"],
            row["faction"],
            row["combat_class"],
            row["uniform_group"],
            row["outfit"],
            row["hair"] or "-",
            row["chest"] or "-",
            row["waist"] or "-",
        ]
        for row in rows
    ]
    widths = [max(len(str(row[index])) for row in [headers, *body]) for index in range(len(headers))]
    print("  ".join(header.ljust(widths[index]) for index, header in enumerate(headers)))
    print("  ".join("-" * width for width in widths))
    for row in body:
        print("  ".join(str(value).ljust(widths[index]) for index, value in enumerate(row)))
    print()
    print_findings(findings)


def print_markdown(rows: list[dict[str, Any]], findings: list[dict[str, Any]]) -> None:
    print("| character | faction | class | group | outfit | hair | chest | waist |")
    print("| --- | --- | --- | --- | --- | --- | --- | --- |")
    for row in rows:
        print(
            "| {id} | {faction} | {combat_class} | {uniform_group} | {outfit} | {hair} | {chest} | {waist} |".format(
                **{key: (value or "-") for key, value in row.items()}
            )
        )
    print()
    print_findings(findings, markdown=True)


def print_findings(findings: list[dict[str, Any]], markdown: bool = False) -> None:
    if not findings:
        print("Findings: none")
        return
    print("Findings:")
    for item in findings:
        prefix = "-" if markdown else "  -"
        chars = ", ".join(item.get("characters", []))
        detail = item.get("detail", "")
        distance = item.get("palette_distance_min")
        suffix = f" palette_distance_min={distance}" if distance is not None else ""
        print(f"{prefix} [{item['severity']}] {item['kind']}: {chars}{suffix}{(' - ' + detail) if detail else ''}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, default=None)
    parser.add_argument("--asset-dir", type=Path, default=None)
    parser.add_argument("--format", choices=["table", "markdown", "json"], default="table")
    parser.add_argument("--strict", action="store_true", help="exit non-zero on warning/error findings")
    args = parser.parse_args()

    pipeline_root = find_pipeline_root(Path(__file__))
    repo_root = pipeline_root.parent
    manifest_path = args.manifest or (pipeline_root / "config" / "p09_visual_identity_manifest.yaml")
    asset_dir = args.asset_dir or (repo_root / "Assets" / "Resources" / "_Game" / "Battle" / "Appearances" / "P09")

    manifest = load_yaml(manifest_path)
    presets = load_presets(asset_dir)
    rows, findings = audit(manifest, presets)

    if args.format == "json":
        print(json.dumps({"rows": rows, "findings": findings}, ensure_ascii=False, indent=2))
    elif args.format == "markdown":
        print_markdown(rows, findings)
    else:
        print_table(rows, findings)

    if args.strict and any(item["severity"] in {"warning", "error"} for item in findings):
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
