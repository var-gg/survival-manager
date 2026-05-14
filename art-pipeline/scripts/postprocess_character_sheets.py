#!/usr/bin/env python3
"""Split generated character sheets into manifest-named PNG outputs.

This script is deterministic postprocess only. It does not generate images.
"""
from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path


FACE_EMOTIONS = ["default", "smile", "serious", "shock", "anger", "sad", "cry", "quiet"]
COMBAT_STATES = ["wounded", "stunned", "feared", "charmed", "pained", "downed"]
STANCE_IDS = ["idle", "attack", "guard", "cast"]


def find_pipeline_root(start: Path) -> Path:
    for candidate in [start.resolve(), *start.resolve().parents]:
        if candidate.name == "art-pipeline":
            return candidate
    return Path(__file__).resolve().parents[1]


def run(cmd: list[str], cwd: Path) -> None:
    print("[postprocess] " + " ".join(cmd), file=sys.stderr)
    subprocess.run(cmd, cwd=cwd, check=True)


def split_sheet(
    pipeline_root: Path,
    character_id: str,
    sheet_name: str,
    rows: int,
    cols: int,
    emotions: list[str],
    prefix: str,
    method: str,
) -> None:
    sheet = pipeline_root / "output" / character_id / f"{sheet_name}.png"
    if not sheet.is_file():
        raise FileNotFoundError(f"missing sheet output: {sheet}")
    out_dir = pipeline_root / "output" / character_id
    run(
        [
            sys.executable,
            str(pipeline_root / "postprocess" / "sheet_split.py"),
            str(sheet),
            "--rows",
            str(rows),
            "--cols",
            str(cols),
            "--emotions",
            ",".join(emotions),
            "--out-dir",
            str(out_dir),
            "--prefix",
            prefix,
            "--method",
            method,
        ],
        cwd=pipeline_root.parent,
    )


def mirror_bust(pipeline_root: Path, character_id: str) -> None:
    run(
        [
            sys.executable,
            str(pipeline_root / "postprocess" / "mirror_flip.py"),
            "--char-id",
            character_id,
            "--output-root",
            str(pipeline_root / "output"),
        ],
        cwd=pipeline_root.parent,
    )


def postprocess_variant(pipeline_root: Path, character_id: str, variant: str, method: str) -> None:
    if variant == "face_emotion_sheet":
        split_sheet(
            pipeline_root,
            character_id,
            variant,
            2,
            4,
            FACE_EMOTIONS,
            "portrait_face",
            method,
        )
    elif variant == "face_combat_state_sheet":
        split_sheet(
            pipeline_root,
            character_id,
            variant,
            2,
            3,
            COMBAT_STATES,
            "portrait_face",
            method,
        )
    elif variant == "bust_emotion_sheet_R":
        split_sheet(
            pipeline_root,
            character_id,
            variant,
            2,
            4,
            [f"{emotion}_R" for emotion in FACE_EMOTIONS],
            "portrait_bust",
            method,
        )
        mirror_bust(pipeline_root, character_id)
    elif variant == "battle_stance_sheet":
        split_sheet(
            pipeline_root,
            character_id,
            variant,
            2,
            2,
            STANCE_IDS,
            "portrait_stance",
            method,
        )
    else:
        raise ValueError(f"unsupported variant: {variant}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("character_id")
    parser.add_argument("variants", nargs="+")
    parser.add_argument(
        "--method",
        choices=["auto", "hardcoded"],
        default="auto",
        help="sheet_split detection method",
    )
    args = parser.parse_args()

    pipeline_root = find_pipeline_root(Path(__file__))
    for variant in args.variants:
        postprocess_variant(pipeline_root, args.character_id, variant, args.method)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
