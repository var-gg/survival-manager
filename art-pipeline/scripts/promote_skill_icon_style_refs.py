#!/usr/bin/env python3
"""Promote selected generated expansion skill icons into icon REF files."""
from __future__ import annotations

import argparse
import shutil
from pathlib import Path
from typing import Any

import yaml


REPO_ROOT = Path(__file__).resolve().parents[2]
PIPELINE_ROOT = REPO_ROOT / "art-pipeline"
CATALOG_PATH = PIPELINE_ROOT / "config" / "skill_expansion_design_catalog.yaml"


def load_catalog(path: Path) -> dict[str, Any]:
    data = yaml.safe_load(path.read_text(encoding="utf-8"))
    if not isinstance(data, dict):
        raise ValueError(f"{path}: expected YAML mapping")
    return data


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--catalog", type=Path, default=CATALOG_PATH)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    catalog = load_catalog(args.catalog)
    output_dir = REPO_ROOT / catalog["icon_pipeline"]["output_dir"]
    refs = catalog["icon_pipeline"].get("promoted_style_refs", [])
    if not isinstance(refs, list) or not refs:
        raise ValueError("catalog icon_pipeline.promoted_style_refs must be a non-empty list")

    ref_root = PIPELINE_ROOT / "ref" / "icons"
    copied = 0
    for entry in refs:
        ref_id = str(entry["ref_id"])
        source_icon_id = str(entry["source_icon_id"])
        source = output_dir / f"{source_icon_id}.png"
        target = ref_root / f"{ref_id}.png"
        if not source.is_file():
            raise FileNotFoundError(source)
        if not args.dry_run:
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, target)
        copied += 1
        print(f"{source.relative_to(REPO_ROOT)} -> {target.relative_to(REPO_ROOT)}")

    verb = "would promote" if args.dry_run else "promoted"
    print(f"[promote_skill_icon_style_refs] {verb} {copied} style refs")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
