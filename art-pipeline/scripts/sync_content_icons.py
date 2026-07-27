#!/usr/bin/env python3
"""Sync generated icon outputs into Unity Resources as normalized PNGs."""
from __future__ import annotations

import argparse
import hashlib
import re
from pathlib import Path
from typing import Any

import yaml
from PIL import Image


REPO_ROOT = Path(__file__).resolve().parents[2]
PIPELINE_ROOT = REPO_ROOT / "art-pipeline"
DEFAULT_CATALOG = PIPELINE_ROOT / "config" / "content_icon_catalog.yaml"
TARGET_DIRS = {
    "skill": "Skill",
    "item": "Item",
    "augment": "Augment",
    "affix": "Affix",
    "site_event_choice": "SiteEventChoice",
}
CHROMA = (255, 0, 255)


def load_catalog(path: Path) -> dict[str, Any]:
    data = yaml.safe_load(path.read_text(encoding="utf-8"))
    if not isinstance(data, dict):
        raise ValueError(f"{path}: expected YAML mapping")
    return data


def transparent_chroma(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    pixels = rgba.load()
    width, height = rgba.size
    for y in range(height):
        for x in range(width):
            r, g, b, a = pixels[x, y]
            if a == 0:
                continue
            if abs(r - CHROMA[0]) <= 6 and abs(g - CHROMA[1]) <= 6 and abs(b - CHROMA[2]) <= 6:
                pixels[x, y] = (r, g, b, 0)
    return rgba


def normalize_icon(source: Path, target: Path, canvas_size: int, safe_box: int) -> None:
    image = transparent_chroma(Image.open(source))
    bbox = image.getbbox()
    if bbox is None:
        raise ValueError(f"{source}: no visible pixels after chroma removal")

    cropped = image.crop(bbox)
    scale = min(safe_box / cropped.width, safe_box / cropped.height)
    size = (
        max(1, round(cropped.width * scale)),
        max(1, round(cropped.height * scale)),
    )
    resized = cropped.resize(size, Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", (canvas_size, canvas_size), (0, 0, 0, 0))
    offset = ((canvas_size - size[0]) // 2, (canvas_size - size[1]) // 2)
    canvas.alpha_composite(resized, dest=offset)
    target.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(target, "PNG")


def deterministic_guid(target: Path) -> str:
    rel = target.relative_to(REPO_ROOT).as_posix()
    return hashlib.md5(f"survival-manager:{rel}".encode("utf-8")).hexdigest()


def ensure_unity_meta(target: Path) -> bool:
    meta = target.with_name(f"{target.name}.meta")
    if meta.is_file():
        return False

    template = next((path for path in sorted(target.parent.glob("*.png.meta")) if path != meta), None)
    if template is None:
        # A brand-new icon kind starts with an empty directory. Every icon directory
        # shares the same Unity import settings, so borrow a sibling kind's template
        # rather than forcing the first icon of a new kind to be seeded by hand.
        template = next(
            (
                path
                for path in sorted(target.parent.parent.glob("*/*.png.meta"))
                if path != meta
            ),
            None,
        )
    if template is None:
        raise FileNotFoundError(
            f"no .png.meta template found in {target.parent} or any sibling icon "
            f"directory under {target.parent.parent}"
        )

    text = template.read_text(encoding="utf-8")
    updated = re.sub(
        r"^guid:\s*[0-9a-fA-F]{32}\s*$",
        f"guid: {deterministic_guid(target)}",
        text,
        count=1,
        flags=re.MULTILINE,
    )
    if updated == text:
        raise ValueError(f"{template}: missing guid line")
    meta.write_text(updated, encoding="utf-8", newline="\n")
    return True


def iter_icons(catalog: dict[str, Any]) -> list[tuple[str, str, Path]]:
    icons = catalog.get("icons")
    if not isinstance(icons, dict):
        raise ValueError("catalog missing icons mapping")

    result: list[tuple[str, str, Path]] = []
    for kind, entries in icons.items():
        if kind not in TARGET_DIRS:
            raise ValueError(
                f"icons.{kind}: unknown icon kind. Every catalog kind must have a "
                f"TARGET_DIRS entry naming its Resources subdirectory, otherwise its "
                f"icons are silently never synced. Known kinds: "
                f"{', '.join(sorted(TARGET_DIRS))}."
            )
        if not isinstance(entries, list):
            raise ValueError(f"icons.{kind}: expected list")
        for entry in entries:
            if not isinstance(entry, dict):
                raise ValueError(f"icons.{kind}: expected mapping entries")
            icon_id = str(entry.get("id", "")).strip()
            source = str(entry.get("source", "")).strip()
            if not icon_id or not source:
                raise ValueError(f"icons.{kind}: entries require id and source")
            result.append((kind, icon_id, REPO_ROOT / source))
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--catalog", type=Path, default=DEFAULT_CATALOG)
    parser.add_argument("--canvas-size", type=int, default=512)
    parser.add_argument("--safe-box", type=int, default=448)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    catalog = load_catalog(args.catalog)
    runtime_root = REPO_ROOT / catalog.get("policy", {}).get(
        "runtime_root",
        "Assets/Resources/_Game/Art/Icons",
    )

    written = 0
    metas = 0
    for kind, icon_id, source in iter_icons(catalog):
        if not source.is_file():
            raise FileNotFoundError(source)
        target = runtime_root / TARGET_DIRS[kind] / f"{icon_id}.png"
        if args.dry_run:
            print(f"[sync_content_icons] {source} -> {target}")
            continue
        normalize_icon(source, target, args.canvas_size, args.safe_box)
        if ensure_unity_meta(target):
            metas += 1
        written += 1

    if not args.dry_run:
        print(f"[sync_content_icons] wrote {written} icons to {runtime_root}; created {metas} meta files")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
