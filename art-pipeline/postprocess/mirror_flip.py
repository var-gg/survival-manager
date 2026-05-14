"""
art-pipeline/postprocess/mirror_flip.py

Horizontally mirror PNG files — primarily for generating bust_L from bust_R
for characters whose lore-only asymmetric details (scars, etc.) are not
rendered in the illustration anyway.

Usage:
    # Single file
    python mirror_flip.py input.png output.png

    # Bust R → bust L set (8 emotions)
    python mirror_flip.py --char-id hero_pack_raider --variant bust_emotion

    # Custom set
    python mirror_flip.py --in-glob "art-pipeline/output/X/portrait_bust_*_R.png" \
        --out-rename "_R.png:_L.png"

Lore-only asymmetric details (right wrist scar, etc.) are excluded from
illustrations per the V6 REF-first prompt policy, so left-right flipping
of bust crops is identity-preserving.

Caveat: do NOT flip portrait_full or battle_stance unless the character's
weapon-handedness is explicitly bidirectional in lore. Most characters carry
weapons in the right hand, and flipping makes them left-handed which reads
incorrectly even if no asymmetric details are visible.
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

from PIL import Image


BUST_EMOTIONS = ["default", "smile", "serious", "shock", "anger", "sad", "cry", "quiet"]


def flip_one(src: Path, dst: Path) -> None:
    """Mirror src horizontally and save to dst."""
    img = Image.open(src)
    flipped = img.transpose(Image.FLIP_LEFT_RIGHT)
    flipped.save(dst)
    print(f"  {src.name} -> {dst.name}", file=sys.stderr)


def flip_bust_set(char_id: str, output_root: Path) -> int:
    """Flip all 8 portrait_bust_<emotion>_R.png to portrait_bust_<emotion>_L.png."""
    char_dir = output_root / char_id
    if not char_dir.is_dir():
        print(f"[mirror_flip] error: {char_dir} not found", file=sys.stderr)
        return 1

    flipped = 0
    for emotion in BUST_EMOTIONS:
        src = char_dir / f"portrait_bust_{emotion}_R.png"
        dst = char_dir / f"portrait_bust_{emotion}_L.png"
        if not src.is_file():
            print(f"[mirror_flip] skip: {src.name} not found", file=sys.stderr)
            continue
        flip_one(src, dst)
        flipped += 1

    print(f"[mirror_flip] flipped {flipped} bust pairs for {char_id}", file=sys.stderr)
    return 0 if flipped == len(BUST_EMOTIONS) else 1


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--char-id", type=str, help="character id (e.g. hero_pack_raider) for bust set flip")
    ap.add_argument(
        "--variant",
        type=str,
        default="bust_emotion",
        choices=["bust_emotion"],
        help="which set to flip; only bust_emotion supported (the only safe variant)",
    )
    ap.add_argument(
        "--output-root",
        type=Path,
        default=Path("art-pipeline/output"),
        help="output root containing {char_id}/ subdirs",
    )
    # Single-file mode
    ap.add_argument("src", type=Path, nargs="?", help="single source PNG (single-file mode)")
    ap.add_argument("dst", type=Path, nargs="?", help="single dest PNG (single-file mode)")
    args = ap.parse_args()

    if args.char_id:
        return flip_bust_set(args.char_id, args.output_root)

    if args.src and args.dst:
        flip_one(args.src, args.dst)
        return 0

    ap.print_help()
    return 1


if __name__ == "__main__":
    sys.exit(main())
