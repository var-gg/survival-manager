#!/usr/bin/env python3
"""Assemble final ChatGPT image prompt from a v2 schema subject page + project style-anchor.

Standalone testable (no Playwright dependency). Reads files from disk,
produces the string that would be submitted to ChatGPT.

Multi-subject support (v2 — 2026-05-09):
- subject frontmatter `kind` 분기로 sub-anchor 선택 + ref directory 결정.
- common anchor (`style/style-anchor-common.md`)는 항상 prepend.
- sub anchor: character / map / icon / cutscene 4종 (`style/style-anchor-{family}.md`).
- LAYOUT block (map/icon/cutscene)이 정의되면 prompt에 포함.

Usage:
    python assemble_prompt.py <subject_page.md>            # legacy file-backed subject
    python assemble_prompt.py --subject-json <subject.json> --pipeline-root art-pipeline

Domain (survival-manager game):
    subject_page = art-pipeline/subjects/{kind_dir}/{subject_id}/{variant}.md
    style-anchor = art-pipeline/style/style-anchor-common.md + style-anchor-{family}.md
    refs         = art-pipeline/ref/{kind_dir}/{subject_id}/anchor.png

Adapted from vargg-webtoon comic-imagegen assemble_prompt.py.
"""
import argparse
import json
import re
import sys
from pathlib import Path
from typing import Any

import yaml

if sys.stdout.encoding and sys.stdout.encoding.lower() != "utf-8":
    sys.stdout.reconfigure(encoding="utf-8")
if sys.stderr.encoding and sys.stderr.encoding.lower() != "utf-8":
    sys.stderr.reconfigure(encoding="utf-8")

REQUIRED_FRONTMATTER = ["slug", "kind", "subject_id", "variant", "refs", "aspect", "status"]

# kind → sub-anchor filename (under art-pipeline/style/)
KIND_TO_ANCHOR_FILE = {
    # character family
    "character_portrait_full": "style-anchor-character.md",
    "character_portrait_bust": "style-anchor-character.md",
    "character_portrait_face": "style-anchor-character.md",
    "character_battle_stance": "style-anchor-character.md",
    # character sheet family
    "face_emotion_sheet": "style-anchor-character.md",
    "face_combat_state_sheet": "style-anchor-character.md",
    "bust_emotion_sheet": "style-anchor-character.md",
    "battle_stance_sheet": "style-anchor-character.md",
    "skill_icon_theme_sheet": "style-anchor-icon.md",
    "skill_icon_catalog_sheet": "style-anchor-icon.md",
    # map family (cycle stages)
    "map_concept": "style-anchor-map.md",
    "map_layout": "style-anchor-map.md",
    "map_decor_breakdown": "style-anchor-map.md",
    "map_painted": "style-anchor-map.md",
    "environment_site": "style-anchor-map.md",  # legacy alias / town BG
    # UI full-screen mockup / art bible reference family
    "ui_mockup_reference": "style-anchor-ui-mockup.md",
    # icon family
    "skill_icon": "style-anchor-icon.md",
    "passive_icon": "style-anchor-icon.md",
    "equipment_icon": "style-anchor-icon.md",
    # UI detail asset family
    "ui_detail_asset": "style-anchor-ui-detail.md",
    # cutscene
    "cutscene_cut": "style-anchor-cutscene.md",
}

# kind → ref directory under art-pipeline/ref/
KIND_TO_REF_DIR = {
    "character_portrait_full": "characters",
    "character_portrait_bust": "characters",
    "character_portrait_face": "characters",
    "character_battle_stance": "characters",
    "face_emotion_sheet": "characters",
    "face_combat_state_sheet": "characters",
    "bust_emotion_sheet": "characters",
    "battle_stance_sheet": "characters",
    "skill_icon_theme_sheet": "characters",
    "skill_icon_catalog_sheet": "icons",
    "map_concept": "maps",
    "map_layout": "maps",
    "map_decor_breakdown": "maps",
    "map_painted": "maps",
    "environment_site": "backgrounds",
    "ui_mockup_reference": "backgrounds",
    "skill_icon": "icons",
    "passive_icon": "icons",
    "equipment_icon": "icons",
    "ui_detail_asset": "ui_detail",
    "cutscene_cut": "cutscenes",
}


def _validate_subject_frontmatter(fm: Any, source: Path) -> dict[str, Any]:
    if not isinstance(fm, dict):
        raise ValueError(f"{source}: frontmatter must be a mapping")

    missing = [k for k in REQUIRED_FRONTMATTER if k not in fm]
    if missing:
        raise ValueError(f"{source}: frontmatter missing fields {missing}")

    if not isinstance(fm["refs"], list):
        raise ValueError(f"{source}: frontmatter field 'refs' must be a list")

    return fm


def parse_subject(subject_path: Path) -> tuple[dict[str, Any], str]:
    """Return (frontmatter_dict, prompt_fence_content).

    Raises ValueError if frontmatter missing required fields or no prompt fence.
    """
    text = subject_path.read_text(encoding="utf-8")

    if not text.startswith("---\n"):
        raise ValueError(f"{subject_path}: no frontmatter (must start with ---)")
    end = text.find("\n---\n", 4)
    if end == -1:
        raise ValueError(f"{subject_path}: frontmatter closing --- not found")
    fm_raw = text[4:end]
    body = text[end + 5 :]

    fm = _validate_subject_frontmatter(yaml.safe_load(fm_raw), subject_path)

    fence_match = re.search(r"```prompt\s*\n(.*?)\n```", body, re.DOTALL)
    if not fence_match:
        raise ValueError(f"{subject_path}: ```prompt fence not found")
    fence = fence_match.group(1).strip()

    return fm, fence


def parse_subject_json(subject_path: Path) -> tuple[dict[str, Any], str]:
    """Return (frontmatter_dict, prompt_content) from a repo-external JSON subject.

    This keeps Pindoc-hydrated image generation inputs out of the repository. The
    JSON shape is intentionally close to the legacy Markdown schema:

        {"frontmatter": {...}, "prompt": "..."}
    """
    try:
        data = json.loads(subject_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as e:
        raise ValueError(f"{subject_path}: invalid JSON ({e})") from e

    if not isinstance(data, dict):
        raise ValueError(f"{subject_path}: JSON root must be an object")

    fm = _validate_subject_frontmatter(
        data.get("frontmatter") if "frontmatter" in data else data.get("fm"),
        subject_path,
    )
    prompt = data.get("prompt") or data.get("prompt_fence") or data.get("fence")
    if not isinstance(prompt, str) or not prompt.strip():
        raise ValueError(f"{subject_path}: JSON field 'prompt' must be a non-empty string")

    return fm, prompt.strip()


def _extract_blocks(path: Path, names: list[str]) -> dict[str, str]:
    """Extract === NAME === blocks from anchor markdown. Missing names map to empty string."""
    text = path.read_text(encoding="utf-8")
    out: dict[str, str] = {}
    for name in names:
        pattern = re.compile(
            rf"=== {re.escape(name)}.*?===\n(.*?)(?=\n=== |\n```|\Z)",
            re.DOTALL,
        )
        m = pattern.search(text)
        out[name] = m.group(1).strip() if m else ""
    return out


def _extract_anchor_json(path: Path) -> dict[str, str]:
    """Extract style blocks from a repo-external JSON style anchor."""
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as e:
        raise ValueError(f"{path}: invalid JSON ({e})") from e

    if not isinstance(data, dict):
        raise ValueError(f"{path}: JSON root must be an object")

    blocks = data.get("blocks", data)
    if not isinstance(blocks, dict):
        raise ValueError(f"{path}: JSON field 'blocks' must be an object")

    aliases = {
        "ART STYLE": ("ART STYLE", "art_style"),
        "LAYOUT / COMPOSITION": ("LAYOUT / COMPOSITION", "layout"),
        "SHADING / LIGHTING": ("SHADING / LIGHTING", "shading"),
        "CHROMA BACKGROUND": ("CHROMA BACKGROUND", "chroma"),
        "NEGATIVE": ("NEGATIVE", "negative"),
    }
    out: dict[str, str] = {}
    for name, keys in aliases.items():
        value = next((blocks[k] for k in keys if isinstance(blocks.get(k), str)), "")
        out[name] = value.strip()
    return out


def extract_anchor_for_kind(
    pipeline_root: Path,
    kind: str,
    *,
    style_root: Path | None = None,
    style_json: Path | None = None,
) -> dict[str, str]:
    """Resolve common + sub anchor for a subject kind.

    Returns dict with keys: art_style / layout / shading / chroma / negative.
    common's STYLE BASELINE prepends art_style; common's NEGATIVE COMMON appends negative.
    """
    common_path = pipeline_root / "style" / "style-anchor-common.md"
    sub_filename = KIND_TO_ANCHOR_FILE.get(kind)
    if sub_filename is None:
        raise ValueError(
            f"unknown kind '{kind}' — add mapping in KIND_TO_ANCHOR_FILE"
        )
    if not common_path.is_file():
        raise FileNotFoundError(f"style-anchor-common not found: {common_path}")

    common = _extract_blocks(common_path, ["STYLE BASELINE", "NEGATIVE COMMON"])
    block_names = [
        "ART STYLE",
        "LAYOUT / COMPOSITION",
        "SHADING / LIGHTING",
        "CHROMA BACKGROUND",
        "NEGATIVE",
    ]
    if style_json is not None:
        sub = _extract_anchor_json(style_json)
    else:
        candidates: list[Path] = []
        if style_root is not None:
            candidates.append(style_root / sub_filename)
        candidates.append(pipeline_root / "style" / sub_filename)
        sub_path = next((p for p in candidates if p.is_file()), None)
        if sub_path is None:
            searched = ", ".join(str(p) for p in candidates)
            raise FileNotFoundError(
                f"style-anchor for kind '{kind}' not found. "
                "Pindoc-owned prompt sources must be passed with --style-json "
                f"or a repo-external --style-root. Searched: {searched}"
            )
        sub = _extract_blocks(sub_path, block_names)

    art_style_parts = [p for p in [common["STYLE BASELINE"], sub["ART STYLE"]] if p]
    negative_parts = [p for p in [sub["NEGATIVE"], common["NEGATIVE COMMON"]] if p]

    return {
        "art_style": "\n\n".join(art_style_parts),
        "layout": sub["LAYOUT / COMPOSITION"],
        "shading": sub["SHADING / LIGHTING"],
        "chroma": sub["CHROMA BACKGROUND"],
        "negative": "\n\n".join(negative_parts),
    }


# Backward-compat alias for any external caller still using the old name.
def extract_anchor_blocks(anchor_path: Path) -> dict[str, str]:  # pragma: no cover
    """Deprecated. Use extract_anchor_for_kind(pipeline_root, kind)."""
    blocks = _extract_blocks(
        anchor_path,
        ["ART STYLE", "LAYOUT / COMPOSITION", "SHADING / LIGHTING", "CHROMA BACKGROUND", "NEGATIVE"],
    )
    return {
        "art_style": blocks["ART STYLE"],
        "layout": blocks["LAYOUT / COMPOSITION"],
        "shading": blocks["SHADING / LIGHTING"],
        "chroma": blocks["CHROMA BACKGROUND"],
        "negative": blocks["NEGATIVE"],
    }


def find_pipeline_root(subject_path: Path) -> Path:
    """Walk up from subject_path until finding the art-pipeline directory."""
    for p in [subject_path] + list(subject_path.resolve().parents):
        if p.name == "art-pipeline":
            return p
    raise RuntimeError(f"art-pipeline root not found in path of {subject_path}")


def resolve_ref_paths(
    pipeline_root: Path, ref_specs: list[str], kind: str
) -> list[tuple[Path, str]]:
    """Resolve REF specs to absolute paths with kind labels.

    Three ref forms:
    1. Anchor — "{subject_id}" → ref/{kind_dir}/{subject_id}/anchor.png
    2. Prior output — "{subject_id}:{file_stem}" → output/{subject_id}/{file_stem}.png
    3. Direct file — "{subject_id}" with no anchor.png subdir → ref/{kind_dir}/{subject_id}.png
       (fallback for maps/icons where anchor.png subdir convention may not apply)

    Empty refs list returns [] (icon/cutscene may have no refs).
    """
    if not ref_specs:
        return []
    kind_dir = KIND_TO_REF_DIR.get(kind, "characters")
    paths: list[tuple[Path, str]] = []
    missing: list[str] = []
    for spec in ref_specs:
        if ":" in spec:
            subject_id, file_stem = spec.split(":", 1)
            p = pipeline_root / "output" / subject_id / f"{file_stem}.png"
            label = f"{subject_id}/{file_stem} (prior output illustration — canonical visual style baseline)"
        else:
            p = pipeline_root / "ref" / kind_dir / spec / "anchor.png"
            if not p.is_file():
                alt = pipeline_root / "ref" / kind_dir / f"{spec}.png"
                if alt.is_file():
                    p = alt
            label = f"{spec}/anchor (reference image)"
        if not p.is_file():
            missing.append(f"{spec} -> {p}")
        else:
            paths.append((p, label))
    if missing:
        raise FileNotFoundError(f"missing REF images: {missing}")
    return paths


def build_ref_attachment_block(ref_paths: list[tuple[Path, str]]) -> str:
    """Build the REFERENCE IMAGES block listing attached files with their kinds."""
    lines = []
    for i, (p, label) in enumerate(ref_paths, start=1):
        lines.append(f"  {i}. {label}")
    listing = "\n".join(lines)
    return f"""=== REFERENCE IMAGES (attached, in order) ===
{listing}

Instruction:
- "anchor" image is a reference (P09 simplified 3D model capture for characters,
  or stage screenshot for maps). Use it for shape / layout / silhouette / color zone reference.
- "prior output illustration" images are canonical visual style baseline that prior cycles
  locked in. Match identity, palette, and visual style to these.
- The subject prompt fence below is the authoritative source for fine details (color hex,
  defining features, layout markers). When the spec contradicts a ref, the spec wins for details,
  but prior outputs win for identity/style continuity."""


def assemble(
    fm: dict[str, Any],
    fence: str,
    anchor: dict[str, str],
    ref_paths: list[tuple[Path, str]],
) -> str:
    """Compose the final prompt string.

    Order: ART STYLE → SHADING → LAYOUT (if present) → REF attachment (if any) → SUBJECT prompt fence → CHROMA → NEGATIVE.
    """
    parts = [
        f"=== ART STYLE (엄수) ===\n{anchor['art_style']}",
        f"=== SHADING / LIGHTING ===\n{anchor['shading']}",
    ]
    if anchor.get("layout"):
        parts.append(f"=== LAYOUT / COMPOSITION ===\n{anchor['layout']}")
    if ref_paths:
        parts.append(build_ref_attachment_block(ref_paths))
    parts.append(fence)
    if anchor.get("chroma"):
        parts.append(f"=== CHROMA BACKGROUND (CRITICAL) ===\n{anchor['chroma']}")
    if anchor.get("negative"):
        parts.append(f"=== NEGATIVE ===\n{anchor['negative']}")
    return "\n\n".join(parts)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("subject", nargs="?", type=Path, help="path to legacy subject page .md")
    ap.add_argument("--subject-json", type=Path, help="repo-external subject JSON")
    ap.add_argument("--pipeline-root", type=Path, help="art-pipeline root for repo-external JSON input")
    ap.add_argument("--style-json", type=Path, help="repo-external style anchor JSON")
    ap.add_argument("--style-root", type=Path, help="repo-external directory containing style-anchor-*.md files")
    ap.add_argument("--json", action="store_true", help="print metadata as JSON to stderr")
    args = ap.parse_args()

    if args.subject is not None and args.subject_json is not None:
        print("error: pass either legacy subject .md or --subject-json, not both", file=sys.stderr)
        return 1
    if args.subject is None and args.subject_json is None:
        print("error: subject .md or --subject-json is required", file=sys.stderr)
        return 1

    subject_path = (args.subject_json or args.subject).resolve()
    if not subject_path.is_file():
        print(f"error: subject file not found: {subject_path}", file=sys.stderr)
        return 1

    try:
        if args.subject_json is not None:
            fm, fence = parse_subject_json(subject_path)
        else:
            fm, fence = parse_subject(subject_path)
    except ValueError as e:
        print(f"error: {e}", file=sys.stderr)
        return 2

    if args.pipeline_root is not None:
        pipeline_root = args.pipeline_root.resolve()
    elif args.subject_json is not None:
        print("error: --pipeline-root is required with --subject-json", file=sys.stderr)
        return 1
    else:
        pipeline_root = find_pipeline_root(subject_path)

    try:
        anchor = extract_anchor_for_kind(
            pipeline_root,
            fm["kind"],
            style_root=args.style_root.resolve() if args.style_root else None,
            style_json=args.style_json.resolve() if args.style_json else None,
        )
    except (ValueError, FileNotFoundError) as e:
        print(f"error: {e}", file=sys.stderr)
        return 4

    try:
        ref_paths = resolve_ref_paths(pipeline_root, fm["refs"], fm["kind"])
    except FileNotFoundError as e:
        print(f"error: {e}", file=sys.stderr)
        return 5

    prompt = assemble(fm, fence, anchor, ref_paths)

    if args.json:
        meta = {
            "slug": fm["slug"],
            "kind": fm["kind"],
            "subject_id": fm["subject_id"],
            "variant": fm["variant"],
            "emotion": fm.get("emotion"),
            "refs": fm["refs"],
            "ref_paths": [{"path": str(p), "kind": label} for (p, label) in ref_paths],
            "aspect": fm["aspect"],
            "output_size": fm.get("output_size"),
            "chroma": fm.get("chroma"),
            "status": fm["status"],
            "prompt_length": len(prompt),
        }
        print(json.dumps(meta, ensure_ascii=False, indent=2), file=sys.stderr)

    print(prompt)
    return 0


if __name__ == "__main__":
    sys.exit(main())
