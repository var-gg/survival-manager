"""
art-pipeline/postprocess/chroma_key.py

Chroma key (fluorescent magenta) -> transparent PNG.
Flood-fill from corners + tolerance + spill suppress + edge feather.

Usage:
    python chroma_key.py <input.png> <output.png>
    python chroma_key.py <input_dir>/ <output_dir>/    # batch mode

Defaults:
    chroma    = (255, 0, 255)   # fluorescent magenta
    tolerance = 70              # RGB distance threshold (was 40 — too low for ChatGPT magenta)
    feather   = 1.0             # edge gaussian blur radius (px)
    spill     = 0.2             # subject-edge magenta tint suppression strength
                                # (was 0.6 — over-aggressive on amber/warm pixels;
                                # turned amber sense-glow into cool violet/purple
                                # in small assets where anti-alias dominates.
                                # 0.2 keeps mild edge cleanup without hue distortion.)

Dependencies: pillow, numpy, scipy
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter
from scipy.ndimage import label


def chroma_to_alpha(
    in_path: Path,
    out_path: Path,
    chroma: tuple[int, int, int] = (255, 0, 255),
    tolerance: float = 70.0,
    feather: float = 1.0,
    spill_strength: float = 0.2,
) -> None:
    img = Image.open(in_path).convert("RGBA")
    arr = np.array(img)
    # int32 (not int16) — (255-0)^2 = 65,025 overflows int16 max (32,767),
    # producing NaN in sqrt and zero-effect masking. int32 max is 2.1B, safe.
    rgb = arr[:, :, :3].astype(np.int32)
    chroma_arr = np.array(chroma, dtype=np.int32)

    # 1) chroma color match mask (RGB euclidean distance)
    dist = np.sqrt(((rgb - chroma_arr) ** 2).sum(axis=2))
    color_match = dist < tolerance

    # 2) Magic-wand mode — every pixel matching chroma color becomes transparent,
    #    regardless of spatial connectivity. Prompt forbids chroma color on the
    #    subject, so any chroma-tinted pixel is background.
    #
    #    Earlier flood-fill-from-corners only caught outline-connected regions,
    #    leaving interior pockets (between arm and torso, around weapon,
    #    behind hair strands) untouched. Magic-wand fixes that.
    bg_mask = color_match

    # 3) set background alpha to 0
    arr[bg_mask, 3] = 0

    # 4) chroma spill suppression on subject edge (magenta tint bleed)
    keep = ~bg_mask
    spill = (
        (rgb[..., 0] > rgb[..., 1] + 30)
        & (rgb[..., 2] > rgb[..., 1] + 30)
        & keep
    )
    arr[spill, 0] = (
        arr[spill, 0] * (1.0 - spill_strength)
        + arr[spill, 1] * spill_strength
    ).astype(np.uint8)
    arr[spill, 2] = (
        arr[spill, 2] * (1.0 - spill_strength)
        + arr[spill, 1] * spill_strength
    ).astype(np.uint8)

    # 5) edge feather (gaussian blur on alpha channel)
    result = Image.fromarray(arr, "RGBA")
    if feather > 0:
        a = result.split()[3].filter(ImageFilter.GaussianBlur(feather))
        result.putalpha(a)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    result.save(out_path, "PNG")


def parse_color(s: str) -> tuple[int, int, int]:
    s = s.lstrip("#")
    if len(s) == 6:
        return int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16)
    parts = [int(x) for x in s.split(",")]
    return parts[0], parts[1], parts[2]


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("input", type=Path, help="single image or directory")
    ap.add_argument("output", type=Path, help="output path or directory")
    ap.add_argument("--chroma", default="#FF00FF", help="hex (e.g. #FF00FF) or 'r,g,b'")
    ap.add_argument("--tolerance", type=float, default=70.0)
    ap.add_argument("--feather", type=float, default=1.0)
    ap.add_argument("--spill", type=float, default=0.2)
    args = ap.parse_args()

    chroma = parse_color(args.chroma)

    if args.input.is_dir():
        args.output.mkdir(parents=True, exist_ok=True)
        files = sorted(
            p for p in args.input.iterdir()
            if p.suffix.lower() in {".png", ".jpg", ".jpeg", ".webp"}
        )
        if not files:
            print(f"no images in {args.input}", file=sys.stderr)
            return 1
        for f in files:
            target = args.output / f"{f.stem}.png"
            chroma_to_alpha(f, target, chroma, args.tolerance, args.feather, args.spill)
            print(f"{f.name} -> {target.name}")
        return 0

    chroma_to_alpha(args.input, args.output, chroma, args.tolerance, args.feather, args.spill)
    print(f"{args.input.name} -> {args.output.name}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
