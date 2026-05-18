#!/usr/bin/env python3
"""Audit generated expansion skill icon PNG outputs."""
from __future__ import annotations

import argparse
from pathlib import Path
from typing import Any

import yaml
from PIL import Image


REPO_ROOT = Path(__file__).resolve().parents[2]
CATALOG_PATH = REPO_ROOT / "art-pipeline" / "config" / "skill_expansion_design_catalog.yaml"


def load_yaml(path: Path) -> dict[str, Any]:
    data = yaml.safe_load(path.read_text(encoding="utf-8"))
    if not isinstance(data, dict):
        raise ValueError(f"{path}: expected YAML mapping")
    return data


def icon_ids(catalog: dict[str, Any]) -> list[str]:
    skills = catalog.get("skills")
    if not isinstance(skills, list):
        raise ValueError("catalog.skills must be a list")
    ids: list[str] = []
    for row in skills:
        if not isinstance(row, dict) or not row.get("icon_id"):
            raise ValueError("every catalog skill must define icon_id")
        ids.append(str(row["icon_id"]))
    return ids


def icon_dir(catalog: dict[str, Any], source: str) -> Path:
    icon_pipeline = catalog.get("icon_pipeline")
    if not isinstance(icon_pipeline, dict):
        raise ValueError("catalog.icon_pipeline is required")
    key = "selected_dir" if source == "selected" else "output_dir"
    if not icon_pipeline.get(key):
        raise ValueError(f"catalog.icon_pipeline.{key} is required")
    return (REPO_ROOT / str(icon_pipeline[key])).resolve()


def alpha_and_magenta_metrics(path: Path) -> tuple[float, float, tuple[int, int]]:
    img = Image.open(path).convert("RGBA")
    width, height = img.size
    pixels = img.tobytes()
    total = width * height
    transparent = 0
    opaque_magenta = 0
    for index in range(0, len(pixels), 4):
        r, g, b, a = pixels[index], pixels[index + 1], pixels[index + 2], pixels[index + 3]
        if a <= 16:
            transparent += 1
        if a > 16 and r > 245 and g < 20 and b > 245:
            opaque_magenta += 1
    return transparent / total, opaque_magenta / total, (width, height)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--catalog", type=Path, default=CATALOG_PATH)
    parser.add_argument("--source", choices=["selected", "output"], default="selected")
    parser.add_argument("--min-transparent-ratio", type=float, default=0.03)
    parser.add_argument("--max-opaque-magenta-ratio", type=float, default=0.02)
    args = parser.parse_args()

    catalog = load_yaml(args.catalog)
    expected_ids = icon_ids(catalog)
    expected_files = {f"{icon_id}.png" for icon_id in expected_ids}
    out_dir = icon_dir(catalog, args.source)

    failures: list[str] = []
    if len(expected_ids) != len(set(expected_ids)):
        failures.append("duplicate icon_id values in catalog")
    if not out_dir.is_dir():
        failures.append(f"missing output directory: {out_dir}")
        print("[skill expansion icon outputs] FAIL")
        for failure in failures:
            print(f"  - {failure}")
        return 1

    actual_files = {path.name for path in out_dir.glob("skill_icon_*.png")}
    missing = sorted(expected_files - actual_files)
    extra = sorted(actual_files - expected_files)
    if missing:
        failures.append(f"missing icon PNGs: {missing}")
    if extra:
        failures.append(f"unexpected icon PNGs: {extra}")

    print("[skill expansion icon outputs]")
    print(f"expected={len(expected_files)} actual={len(actual_files)} dir={out_dir.relative_to(REPO_ROOT)}")

    worst_magenta: tuple[str, float] | None = None
    lowest_transparent: tuple[str, float] | None = None
    for filename in sorted(expected_files & actual_files):
        path = out_dir / filename
        transparent_ratio, magenta_ratio, size = alpha_and_magenta_metrics(path)
        if size[0] < 256 or size[1] < 256:
            failures.append(f"{filename}: too small {size[0]}x{size[1]}")
        if transparent_ratio < args.min_transparent_ratio:
            failures.append(
                f"{filename}: transparent ratio {transparent_ratio:.2%} < {args.min_transparent_ratio:.2%}"
            )
        if magenta_ratio > args.max_opaque_magenta_ratio:
            failures.append(
                f"{filename}: opaque magenta ratio {magenta_ratio:.2%} > {args.max_opaque_magenta_ratio:.2%}"
            )
        if worst_magenta is None or magenta_ratio > worst_magenta[1]:
            worst_magenta = (filename, magenta_ratio)
        if lowest_transparent is None or transparent_ratio < lowest_transparent[1]:
            lowest_transparent = (filename, transparent_ratio)

    if lowest_transparent:
        print(f"lowest transparent ratio: {lowest_transparent[0]} {lowest_transparent[1]:.2%}")
    if worst_magenta:
        print(f"worst opaque magenta ratio: {worst_magenta[0]} {worst_magenta[1]:.2%}")

    if failures:
        print("\n[skill expansion icon outputs] FAIL")
        for failure in failures:
            print(f"  - {failure}")
        return 1

    print("\n[skill expansion icon outputs] OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
