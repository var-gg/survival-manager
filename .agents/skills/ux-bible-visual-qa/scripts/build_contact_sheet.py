#!/usr/bin/env python3
"""Build UX Bible reference/current contact sheets for survival-manager."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Iterable

from PIL import Image, ImageDraw, ImageFont


DEFAULT_SURFACES = [
    {
        "surface": "Town Service Hub",
        "reference": "ui_ux_bible_town_service_hub_v0.png",
        "currentScreenshot": "town_hub.png",
    },
    {
        "surface": "Character Sheet",
        "reference": "ui_ux_bible_character_sheet_class_detail_v0.png",
        "currentScreenshot": "character_sheet.png",
    },
    {
        "surface": "Tactical Setup",
        "reference": "ui_ux_bible_squad_builder_v0.png",
        "currentScreenshot": "tactical_setup.png",
    },
    {
        "surface": "Inventory Compare",
        "reference": "ui_ux_bible_inventory_equipment_compare_v0.png",
        "currentScreenshot": "inventory_compare.png",
    },
    {
        "surface": "Recruit Detail",
        "reference": "ui_ux_bible_recruit_candidate_choice_v0.png",
        "currentScreenshot": "recruit_detail.png",
    },
    {
        "surface": "Atlas Enemy Intel",
        "reference": "ui_ux_bible_atlas_overworld_map_v0.png",
        "currentScreenshot": "atlas_enemy_intel.png",
    },
    {
        "surface": "Battle HUD Shell",
        "reference": "ui_ux_bible_battle_stage_hud_v0.png",
        "currentScreenshot": "battle_authored.png",
    },
    {
        "surface": "Reward Result",
        "reference": "ui_ux_bible_reward_result_v0.png",
        "currentScreenshot": "reward_result.png",
    },
]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", default=".", help="Repository root. Defaults to current directory.")
    parser.add_argument("--evidence-dir", required=True, help="Evidence packet directory containing current screenshots.")
    parser.add_argument("--reference-dir", default="Screenshots/mockups", help="Directory containing UX Bible mockups.")
    parser.add_argument("--strict", action="store_true", help="Fail if any default reference/current pair is missing.")
    parser.add_argument("--only", nargs="*", help="Optional surface names to include, case-insensitive substring match.")
    parser.add_argument("--tile-width", type=int, default=640, help="Width for each image tile in the contact sheet.")
    return parser.parse_args()


def load_font(size: int) -> ImageFont.ImageFont:
    for name in ("arial.ttf", "segoeui.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def matches_filter(surface: str, filters: Iterable[str] | None) -> bool:
    if not filters:
        return True
    lowered = surface.lower()
    return any(item.lower() in lowered for item in filters)


def open_tile(path: Path, label: str, width: int, placeholder: str) -> Image.Image:
    label_height = 34
    if path.exists():
        image = Image.open(path).convert("RGB")
        ratio = width / image.width
        resized = image.resize((width, max(1, int(image.height * ratio))), Image.Resampling.LANCZOS)
    else:
        resized = Image.new("RGB", (width, int(width * 0.56)), "#1b1110")
        draw_missing = ImageDraw.Draw(resized)
        font = load_font(18)
        draw_missing.text((24, 24), placeholder, fill="#f3c66f", font=font)

    tile = Image.new("RGB", (width, resized.height + label_height), "#080706")
    tile.paste(resized, (0, label_height))
    draw = ImageDraw.Draw(tile)
    font = load_font(18)
    draw.rectangle((0, 0, width, label_height), fill="#201611")
    draw.text((12, 7), label, fill="#f4d28a", font=font)
    return tile


def write_markdown(path: Path, repo_root: Path, evidence_dir: Path, rows: list[dict]) -> None:
    lines = [
        "# UX Bible Reference / Current Contact Sheet",
        "",
        "| Surface | Reference | Current |",
        "| --- | --- | --- |",
    ]
    for row in rows:
        reference = repo_root / row["referencePath"]
        current = evidence_dir / row["currentScreenshot"]
        lines.append(f"| {row['surface']} | ![]({reference}) | ![]({current}) |")
    lines.append("")
    path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    args = parse_args()
    repo_root = Path(args.repo_root).resolve()
    evidence_dir = (repo_root / args.evidence_dir).resolve() if not Path(args.evidence_dir).is_absolute() else Path(args.evidence_dir).resolve()
    reference_dir = (repo_root / args.reference_dir).resolve() if not Path(args.reference_dir).is_absolute() else Path(args.reference_dir).resolve()

    if not evidence_dir.exists():
        raise SystemExit(f"Evidence directory does not exist: {evidence_dir}")

    rows: list[dict] = []
    missing: list[str] = []
    for surface in DEFAULT_SURFACES:
        if not matches_filter(surface["surface"], args.only):
            continue

        reference_path = reference_dir / surface["reference"]
        current_path = evidence_dir / surface["currentScreenshot"]
        reference_exists = reference_path.exists()
        current_exists = current_path.exists()
        if not reference_exists:
            missing.append(f"reference missing: {reference_path}")
        if not current_exists:
            missing.append(f"current missing: {current_path}")

        if reference_exists and current_exists or not args.strict:
            rows.append(
                {
                    "surface": surface["surface"],
                    "reference": str(reference_path.relative_to(repo_root)).replace("\\", "/"),
                    "referencePath": str(reference_path.relative_to(repo_root)).replace("\\", "/"),
                    "currentScreenshot": surface["currentScreenshot"],
                    "referenceExists": reference_exists,
                    "currentExists": current_exists,
                }
            )

    if args.strict and missing:
        raise SystemExit("\n".join(missing))
    if not rows:
        raise SystemExit("No reference/current rows selected.")

    row_gap = 18
    margin = 18
    label_width = 220
    column_gap = 14
    tile_width = args.tile_width
    composed_rows: list[Image.Image] = []
    label_font = load_font(22)
    small_font = load_font(15)

    for row in rows:
        reference = repo_root / row["referencePath"]
        current = evidence_dir / row["currentScreenshot"]
        ref_tile = open_tile(reference, "REFERENCE", tile_width, f"Missing reference:\n{reference.name}")
        cur_tile = open_tile(current, "CURRENT", tile_width, f"Missing current:\n{current.name}")
        height = max(ref_tile.height, cur_tile.height, 150)
        row_img = Image.new("RGB", (label_width + column_gap + tile_width * 2 + column_gap, height), "#0d0b0a")
        draw = ImageDraw.Draw(row_img)
        draw.text((0, 12), row["surface"], fill="#f4d28a", font=label_font)
        draw.text((0, 44), row["currentScreenshot"], fill="#b9aa8c", font=small_font)
        row_img.paste(ref_tile, (label_width + column_gap, 0))
        row_img.paste(cur_tile, (label_width + column_gap + tile_width + column_gap, 0))
        composed_rows.append(row_img)

    total_width = max(row.width for row in composed_rows) + margin * 2
    total_height = sum(row.height for row in composed_rows) + row_gap * (len(composed_rows) - 1) + margin * 2
    sheet = Image.new("RGB", (total_width, total_height), "#050403")
    y = margin
    for row in composed_rows:
        sheet.paste(row, (margin, y))
        y += row.height + row_gap

    sheet_path = evidence_dir / "comparison_contact_sheet.png"
    sheet.save(sheet_path)

    reference_map = {
        "canonicalReferencePolicy": "Existing UX Bible mockups under Screenshots/mockups/ui_ux_bible_*_v0.png are the visual QA baseline.",
        "surfaces": [
            {
                "surface": row["surface"],
                "reference": row["referencePath"],
                "currentScreenshot": row["currentScreenshot"],
                "referenceExists": row["referenceExists"],
                "currentExists": row["currentExists"],
            }
            for row in rows
        ],
    }
    (evidence_dir / "reference_map.json").write_text(json.dumps(reference_map, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    write_markdown(evidence_dir / "comparison_contact_sheet.md", repo_root, evidence_dir, rows)

    template_path = evidence_dir / "visual_verdict.template.json"
    if not template_path.exists():
        template = {
            "overall": "red",
            "redCount": None,
            "reviewedScreens": [row["currentScreenshot"] for row in rows],
            "green": [],
            "yellow": [],
            "red": ["Fill this after direct visual review."],
            "notes": [
                "Automated witness green is not visual QA green.",
                "User handoff is blocked until redCount is 0.",
            ],
        }
        template_path.write_text(json.dumps(template, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    print(f"Wrote {sheet_path}")
    print(f"Wrote {evidence_dir / 'reference_map.json'}")
    print(f"Wrote {evidence_dir / 'comparison_contact_sheet.md'}")
    print(f"Wrote {template_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
