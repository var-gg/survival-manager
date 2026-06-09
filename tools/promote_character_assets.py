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

# asset-matrix 표준 production 자산만 선별한다.
# 주의: 느슨한 glob("portrait_face_*.png")은 portrait_face_anger_raw.png /
# .backup-*.png / _preview_dark.png 같은 중간 산출물까지 매칭한다(docstring과 모순).
# art-pipeline/output에는 이 중간물이 영구 보존되므로, 정밀 정규식 allowlist +
# 중간물 토큰 배제로 런타임 Resources 오염을 막는다.
#  - face : 단일 감정/전투상태 토큰 (default/smile/.../downed)
#  - bust : VN 좌우 배치용 _L/_R suffix만 (suffix 없는 portrait_bust_anger.png는 런타임 미사용)
#  - stance: 단일 애니 상태 (idle/attack/guard/cast)
#  - full / skill_icon
PRODUCTION_PATTERNS = [
    re.compile(r"^portrait_full\.png$"),
    re.compile(r"^portrait_face_[a-z]+\.png$"),
    re.compile(r"^portrait_bust_[a-z]+_[LR]\.png$"),
    re.compile(r"^portrait_stance_[a-z]+\.png$"),
    re.compile(r"^skill_icon_[a-z0-9_]+\.png$"),
]
INTERMEDIATE_TOKENS = ("_raw.", ".backup-", "_preview", "_style_seed", "diag_", "_sheet")


def is_production_asset(name: str) -> bool:
    if any(tok in name for tok in INTERMEDIATE_TOKENS):
        return False
    return any(p.match(name) for p in PRODUCTION_PATTERNS)


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
    return sorted(p for p in char_dir.glob("*.png") if is_production_asset(p.name))


def main() -> int:
    if not MANIFEST.exists():
        print(f"[promote] manifest not found: {MANIFEST}", file=sys.stderr)
        return 1

    dry_run = "--dry-run" in sys.argv[1:]
    id_map = parse_manifest_id_map(MANIFEST)
    if not dry_run:
        RESOURCES_DIR.mkdir(parents=True, exist_ok=True)

    total_copied = 0
    total_changed = 0
    promoted_chars = 0
    missing_chars: list[str] = []

    for output_id, narrative_id in id_map:
        src_dir = OUTPUT_DIR / output_id
        files = collect_production_files(src_dir) if src_dir.is_dir() else []
        if not files:
            missing_chars.append(output_id)
            continue

        dst_dir = RESOURCES_DIR / narrative_id
        if not dry_run:
            dst_dir.mkdir(parents=True, exist_ok=True)
        changed = 0
        for src in files:
            dst = dst_dir / src.name
            if (not dst.exists()) or dst.read_bytes() != src.read_bytes():
                changed += 1
            if not dry_run:
                shutil.copy2(src, dst)
        total_copied += len(files)
        total_changed += changed
        promoted_chars += 1
        rename_note = "" if output_id == narrative_id else f"  (output id: {output_id})"
        print(f"  {narrative_id:28s} {len(files):3d} files  ({changed:3d} new/changed){rename_note}")

    mode = " (dry-run)" if dry_run else ""
    print(f"[promote]{mode} {promoted_chars} characters, {total_copied} production files "
          f"({total_changed} new/changed) -> {RESOURCES_DIR}")
    if missing_chars:
        print(f"[promote] output 없음/비어있음: {', '.join(missing_chars)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
