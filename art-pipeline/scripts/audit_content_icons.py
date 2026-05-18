#!/usr/bin/env python3
"""Audit authored content IconId fields and runtime icon PNG coverage."""
from __future__ import annotations

import argparse
import json
import re
from pathlib import Path
from typing import Any

import yaml


REPO_ROOT = Path(__file__).resolve().parents[2]
PIPELINE_ROOT = REPO_ROOT / "art-pipeline"
DEFAULT_CATALOG = PIPELINE_ROOT / "config" / "content_icon_catalog.yaml"
CONTENT_ROOT = REPO_ROOT / "Assets" / "Resources" / "_Game" / "Content" / "Definitions"
TARGET_DIRS = {
    "skill": "Skill",
    "item": "Item",
    "augment": "Augment",
    "affix": "Affix",
}


def scalar(text: str, key: str) -> str:
    match = re.search(rf"^  {re.escape(key)}:[ \t]*(.*)$", text, re.MULTILINE)
    return match.group(1).strip() if match else ""


def load_catalog(path: Path) -> dict[str, Any]:
    data = yaml.safe_load(path.read_text(encoding="utf-8"))
    if not isinstance(data, dict):
        raise ValueError(f"{path}: expected YAML mapping")
    return data


def manifest_icons(catalog: dict[str, Any]) -> dict[str, set[str]]:
    icons = catalog.get("icons")
    if not isinstance(icons, dict):
        raise ValueError("catalog missing icons mapping")
    result: dict[str, set[str]] = {kind: set() for kind in TARGET_DIRS}
    for kind, entries in icons.items():
        if kind not in result:
            continue
        if not isinstance(entries, list):
            raise ValueError(f"icons.{kind}: expected list")
        for entry in entries:
            if not isinstance(entry, dict):
                continue
            icon_id = str(entry.get("id", "")).strip()
            if icon_id:
                result[kind].add(icon_id)
    return result


def audit_manifest_sources(catalog: dict[str, Any], runtime_root: Path) -> list[str]:
    issues: list[str] = []
    icons = catalog.get("icons", {})
    if not isinstance(icons, dict):
        return ["catalog missing icons mapping"]
    for kind, entries in icons.items():
        if kind not in TARGET_DIRS or not isinstance(entries, list):
            continue
        for entry in entries:
            if not isinstance(entry, dict):
                continue
            icon_id = str(entry.get("id", "")).strip()
            source = REPO_ROOT / str(entry.get("source", "")).strip()
            target = runtime_root / TARGET_DIRS[kind] / f"{icon_id}.png"
            if not source.is_file():
                issues.append(f"{kind}:{icon_id}: missing source {source}")
            if not target.is_file():
                issues.append(f"{kind}:{icon_id}: missing runtime icon {target}")
    return issues


def audit_assets(kind: str, folder: str, runtime_root: Path, known_ids: set[str]) -> dict[str, Any]:
    missing_field: list[str] = []
    missing_icon: list[str] = []
    unknown_manifest: list[str] = []
    assets = sorted((CONTENT_ROOT / folder).glob("*.asset"))
    for path in assets:
        text = path.read_text(encoding="utf-8")
        asset_id = scalar(text, "Id") or path.stem
        icon_id = scalar(text, "IconId")
        if not icon_id:
            missing_field.append(asset_id)
            continue
        if icon_id not in known_ids:
            unknown_manifest.append(f"{asset_id}:{icon_id}")
        target = runtime_root / TARGET_DIRS[kind] / f"{icon_id}.png"
        if not target.is_file():
            missing_icon.append(f"{asset_id}:{icon_id}")
    return {
        "asset_count": len(assets),
        "missing_field": missing_field,
        "missing_icon": missing_icon,
        "unknown_manifest": unknown_manifest,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--catalog", type=Path, default=DEFAULT_CATALOG)
    parser.add_argument("--strict", action="store_true")
    parser.add_argument("--json", type=Path)
    args = parser.parse_args()

    catalog = load_catalog(args.catalog)
    runtime_root = REPO_ROOT / catalog.get("policy", {}).get(
        "runtime_root",
        "Assets/Resources/_Game/Art/Icons",
    )
    known = manifest_icons(catalog)
    result: dict[str, Any] = {
        "manifest": {
            "source_and_runtime_issues": audit_manifest_sources(catalog, runtime_root),
        },
        "skill": audit_assets("skill", "Skills", runtime_root, known["skill"]),
        "item": audit_assets("item", "Items", runtime_root, known["item"]),
        "augment": audit_assets("augment", "Augments", runtime_root, known["augment"]),
    }

    failures = list(result["manifest"]["source_and_runtime_issues"])
    for kind in ("skill", "item", "augment"):
        section = result[kind]
        failures.extend(f"{kind}: missing IconId {item}" for item in section["missing_field"])
        failures.extend(f"{kind}: missing icon {item}" for item in section["missing_icon"])
        failures.extend(f"{kind}: unknown manifest {item}" for item in section["unknown_manifest"])

    for kind in ("skill", "item", "augment"):
        section = result[kind]
        print(
            f"[audit_content_icons] {kind}: assets={section['asset_count']} "
            f"missing_field={len(section['missing_field'])} "
            f"missing_icon={len(section['missing_icon'])} "
            f"unknown_manifest={len(section['unknown_manifest'])}"
        )
    print(f"[audit_content_icons] manifest issues={len(result['manifest']['source_and_runtime_issues'])}")

    if args.json:
        args.json.parent.mkdir(parents=True, exist_ok=True)
        args.json.write_text(json.dumps(result, indent=2), encoding="utf-8")

    if failures:
        print("[audit_content_icons] failures:")
        for failure in failures:
            print(f"  - {failure}")
        return 1 if args.strict else 0

    print("[audit_content_icons] OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
