#!/usr/bin/env python3
"""Backfill catalog_v2 skill icons from catalog_v1 motifs.

This keeps runtime UI nonblank when fresh game-image-gen is blocked. It does
not mark subject pages as rendered; the generated subject prompts remain the
source for the real replacement pass.
"""
from __future__ import annotations

import argparse
import shutil
from pathlib import Path
from typing import Any

import yaml
from PIL import Image


REPO_ROOT = Path(__file__).resolve().parents[2]
PIPELINE_ROOT = REPO_ROOT / "art-pipeline"
MATRIX_PATH = PIPELINE_ROOT / "config" / "skill_icon_generation_matrix.yaml"
MAGENTA = (255, 0, 255, 255)


def load_matrix(path: Path) -> dict[str, Any]:
    data = yaml.safe_load(path.read_text(encoding="utf-8"))
    if not isinstance(data, dict):
        raise ValueError(f"{path}: expected YAML mapping")
    return data


def normalize_source(source: Path, size: int = 768, safe_box: int = 600) -> Image.Image:
    image = Image.open(source).convert("RGBA")
    bbox = image.getbbox()
    if bbox is None:
        raise ValueError(f"{source}: no visible pixels")
    cropped = image.crop(bbox)
    scale = min(safe_box / cropped.width, safe_box / cropped.height)
    resized = cropped.resize(
        (max(1, round(cropped.width * scale)), max(1, round(cropped.height * scale))),
        Image.Resampling.LANCZOS,
    )
    canvas = Image.new("RGBA", (size, size), MAGENTA)
    offset = ((size - resized.width) // 2, (size - resized.height) // 2)
    canvas.alpha_composite(resized, offset)
    return canvas


def write_sheet(sheet: dict[str, Any], dry_run: bool) -> int:
    subject_id = str(sheet["id"])
    skills = sheet.get("skills")
    if not isinstance(skills, list) or len(skills) != 4:
        raise ValueError(f"{subject_id}: expected exactly 4 skills")

    out_dir = PIPELINE_ROOT / "output" / subject_id
    sheet_path = out_dir / "default.png"
    canvas = Image.new("RGBA", (1568, 1568), MAGENTA)
    written = 0

    for index, skill in enumerate(skills):
        if not isinstance(skill, dict):
            raise ValueError(f"{subject_id}: skill entry must be a mapping")
        source_rel = str(skill["placeholder_source"])
        target_rel = str(skill["source"])
        source = REPO_ROOT / source_rel
        target = REPO_ROOT / target_rel
        if not source.is_file():
            raise FileNotFoundError(source)

        if not dry_run:
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, target)

        cell = normalize_source(source)
        x = 32 + (index % 2) * (768 + 32)
        y = 32 + (index // 2) * (768 + 32)
        canvas.alpha_composite(cell, (x, y))
        written += 1

    if not dry_run:
        out_dir.mkdir(parents=True, exist_ok=True)
        canvas.save(sheet_path, "PNG")
    return written


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--matrix", type=Path, default=MATRIX_PATH)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    matrix = load_matrix(args.matrix)
    sheets = matrix.get("sheets")
    if not isinstance(sheets, list):
        raise ValueError("matrix missing sheets list")

    count = 0
    for sheet in sheets:
        count += write_sheet(sheet, args.dry_run)
    verb = "would backfill" if args.dry_run else "backfilled"
    print(f"[backfill_skill_icon_placeholders] {verb} {count} catalog_v2 skill icons")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
