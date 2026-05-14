#!/usr/bin/env python3
"""Split generated character-theme skill icon sheets into manifest outputs."""
from __future__ import annotations

import argparse
import sys
from pathlib import Path
from typing import Any

import yaml


PIPELINE_ROOT = Path(__file__).resolve().parents[1]
POSTPROCESS_DIR = PIPELINE_ROOT / "postprocess"
sys.path.insert(0, str(POSTPROCESS_DIR))

from sheet_split import split_sheet  # noqa: E402


THEME_SPILL_TARGETS: dict[str, tuple[int, int, int]] = {
    "character_theme_hero_dawn_priest": (255, 217, 121),
    "character_theme_hero_pack_raider": (208, 138, 46),
    "character_theme_hero_grave_hexer": (109, 190, 124),
    "character_theme_hero_echo_savant": (143, 212, 232),
}


def read_frontmatter(path: Path) -> dict[str, Any]:
    text = path.read_text(encoding="utf-8")
    if not text.startswith("---\n"):
        raise ValueError(f"{path}: missing YAML frontmatter")
    end = text.find("\n---\n", 4)
    if end == -1:
        raise ValueError(f"{path}: unterminated YAML frontmatter")
    data = yaml.safe_load(text[4:end])
    if not isinstance(data, dict):
        raise ValueError(f"{path}: frontmatter must be a mapping")
    return data


def resolve_subject_path(subject: str) -> Path:
    candidate = Path(subject)
    if candidate.is_file():
        return candidate
    if candidate.suffix != ".md":
        candidate = PIPELINE_ROOT / "subjects" / "icons" / "skill" / subject / "default.md"
    elif not candidate.is_absolute():
        candidate = PIPELINE_ROOT / candidate
    if not candidate.is_file():
        raise FileNotFoundError(candidate)
    return candidate


def suppress_magenta_spill(image_path: Path, target_rgb: tuple[int, int, int]) -> int:
    """Recolor generated hot-pink/fuchsia spill pixels to the theme accent.

    The icon prompts reserve #FF00FF for chroma only, but generators sometimes
    reuse that hue in glow strokes. This keeps the symbol geometry intact while
    restoring the intended palette.
    """
    import numpy as np
    from PIL import Image

    img = Image.open(image_path).convert("RGBA")
    arr = np.array(img)
    rgb = arr[..., :3].astype(np.int16)
    alpha = arr[..., 3]
    r = rgb[..., 0]
    g = rgb[..., 1]
    b = rgb[..., 2]

    spill = (
        (alpha > 0)
        & (r > g + 45)
        & (b > g + 45)
        & ((r + b) > 290)
    )
    count = int(spill.sum())
    if count == 0:
        return 0

    target = np.array(target_rgb, dtype=np.float32)
    target_luma = max(1.0, float(0.299 * target[0] + 0.587 * target[1] + 0.114 * target[2]))
    luma = (
        0.299 * arr[..., 0].astype(np.float32)
        + 0.587 * arr[..., 1].astype(np.float32)
        + 0.114 * arr[..., 2].astype(np.float32)
    )
    scale = np.clip(luma[spill] / target_luma, 0.35, 1.55)[:, None]
    arr[..., :3][spill] = np.clip(target[None, :] * scale, 0, 255).astype(np.uint8)
    Image.fromarray(arr, "RGBA").save(image_path, "PNG")
    return count


def postprocess_subject(subject_path: Path, method: str = "auto", clean_spill: bool = True) -> list[Path]:
    fm = read_frontmatter(subject_path)
    if fm.get("kind") != "skill_icon_theme_sheet":
        raise ValueError(f"{subject_path}: kind must be skill_icon_theme_sheet")

    subject_id = fm.get("subject_id")
    skills = fm.get("skills")
    if not isinstance(subject_id, str) or not subject_id:
        raise ValueError(f"{subject_path}: missing subject_id")
    if not isinstance(skills, list) or len(skills) != 4:
        raise ValueError(f"{subject_path}: skills must contain exactly 4 ids")

    sheet_path = PIPELINE_ROOT / "output" / subject_id / "default.png"
    if not sheet_path.is_file():
        raise FileNotFoundError(sheet_path)
    if clean_spill and subject_id in THEME_SPILL_TARGETS:
        changed = suppress_magenta_spill(sheet_path, THEME_SPILL_TARGETS[subject_id])
        if changed:
            print(f"[postprocess_skill_theme_sheets] recolored {changed} magenta spill px in {sheet_path.name}")

    out_dir = PIPELINE_ROOT / "output" / "icons" / "skill" / subject_id
    return split_sheet(
        sheet_path,
        rows=2,
        cols=2,
        emotions=[str(skill) for skill in skills],
        out_dir=out_dir,
        prefix="skill_icon",
        method=method,
    )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "subjects",
        nargs="+",
        help="subject ids, subject markdown paths, or subjects/icons/skill/... relative paths",
    )
    parser.add_argument(
        "--method",
        choices=["auto", "hardcoded"],
        default="auto",
        help="split strategy passed to sheet_split.py",
    )
    parser.add_argument(
        "--no-clean-spill",
        action="store_true",
        help="skip hot-pink/fuchsia spill recolor before splitting",
    )
    args = parser.parse_args()

    written: list[Path] = []
    for subject in args.subjects:
        subject_path = resolve_subject_path(subject)
        written.extend(
            postprocess_subject(
                subject_path,
                method=args.method,
                clean_spill=not args.no_clean_spill,
            )
        )

    print(f"[postprocess_skill_theme_sheets] wrote {len(written)} icons")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
