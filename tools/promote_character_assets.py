#!/usr/bin/env python3
"""art-pipeline/output의 캐릭터 아트를 Unity Resources로 승격한다.

  art-pipeline/output/{manifest_id}/  ->  Assets/Resources/_Game/Art/Characters/{narrative_id}/

- narrative_id = character_asset_manifest.yaml의 wiki_character_id (없으면 id 그대로).
  art-pipeline은 짧은 id(slayer, hunter ...)를 쓰지만 narrative/battle 런타임은
  hero_*/npc_* identity key를 쓰므로 승격 시 이름을 정합시킨다.
- production 자산만 복사한다 (sheets / _raw / diag / backup 제외).
- 자산 구조 SoT: pindoc://analysis-character-asset-matrix-dawn-priest
"""
from __future__ import annotations

import re
import shutil
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
MANIFEST = REPO_ROOT / "art-pipeline" / "config" / "character_asset_manifest.yaml"
OUTPUT_DIR = REPO_ROOT / "art-pipeline" / "output"
RESOURCES_DIR = REPO_ROOT / "Assets" / "Resources" / "_Game" / "Art" / "Characters"

# asset-matrix 표준 production 자산 (sheets / _raw / diag / portrait_full_v* backup 제외)
PRODUCTION_GLOBS = [
    "portrait_full.png",
    "portrait_face_*.png",
    "portrait_bust_*.png",
    "portrait_stance_*.png",
    "skill_icon_*.png",
]


def parse_manifest_id_map(manifest_path: Path) -> list[tuple[str, str]]:
    """character_asset_manifest.yaml -> [(output_id, narrative_id), ...]."""
    text = manifest_path.read_text(encoding="utf-8")
    chars_section = text.split("\ncharacters:", 1)[1]
    pairs: list[tuple[str, str]] = []
    for block in re.split(r"\n  - id:\s*", chars_section)[1:]:
        output_id = block.splitlines()[0].strip()
        match = re.search(r"wiki_character_id:\s*(\S+)", block)
        narrative_id = match.group(1) if match else output_id
        pairs.append((output_id, narrative_id))
    return pairs


def collect_production_files(char_dir: Path) -> list[Path]:
    files: list[Path] = []
    for pattern in PRODUCTION_GLOBS:
        files.extend(sorted(char_dir.glob(pattern)))
    return files


def main() -> int:
    if not MANIFEST.exists():
        print(f"[promote] manifest not found: {MANIFEST}", file=sys.stderr)
        return 1

    id_map = parse_manifest_id_map(MANIFEST)
    RESOURCES_DIR.mkdir(parents=True, exist_ok=True)

    total_copied = 0
    promoted_chars = 0
    missing_chars: list[str] = []

    for output_id, narrative_id in id_map:
        src_dir = OUTPUT_DIR / output_id
        files = collect_production_files(src_dir) if src_dir.is_dir() else []
        if not files:
            missing_chars.append(output_id)
            continue

        dst_dir = RESOURCES_DIR / narrative_id
        dst_dir.mkdir(parents=True, exist_ok=True)
        for src in files:
            shutil.copy2(src, dst_dir / src.name)
        total_copied += len(files)
        promoted_chars += 1
        rename_note = "" if output_id == narrative_id else f"  (output id: {output_id})"
        print(f"  {narrative_id:28s} {len(files):3d} files{rename_note}")

    print(f"[promote] {promoted_chars} characters, {total_copied} files -> {RESOURCES_DIR}")
    if missing_chars:
        print(f"[promote] output 없음/비어있음: {', '.join(missing_chars)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
