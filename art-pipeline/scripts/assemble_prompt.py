#!/usr/bin/env python3
"""Assemble final ChatGPT image prompt from a v2 schema subject page + project style-anchor.

Standalone testable (no Playwright dependency). Reads files from disk,
produces the string that would be submitted to ChatGPT.

Usage:
    python assemble_prompt.py <subject_page.md>            # prints to stdout
    python assemble_prompt.py <subject_page.md> --json     # also prints metadata to stderr

Domain (survival-manager game):
    subject_page = art-pipeline/subjects/{kind_dir}/{subject_id}/{variant}.md
    style-anchor = art-pipeline/style/style-anchor.md (project-level, single)
    refs         = art-pipeline/ref/characters/{slug}/anchor.png

Adapted from vargg-webtoon comic-imagegen assemble_prompt.py
(A:\\vargg-workspace\\vargg-webtoon\\.claude\\skills\\comic-imagegen\\scripts\\assemble_prompt.py).
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

    fm = yaml.safe_load(fm_raw)
    if not isinstance(fm, dict):
        raise ValueError(f"{subject_path}: frontmatter must be a YAML mapping")

    missing = [k for k in REQUIRED_FRONTMATTER if k not in fm]
    if missing:
        raise ValueError(f"{subject_path}: frontmatter missing fields {missing}")

    fence_match = re.search(r"```prompt\s*\n(.*?)\n```", body, re.DOTALL)
    if not fence_match:
        raise ValueError(f"{subject_path}: ```prompt fence not found")
    fence = fence_match.group(1).strip()

    return fm, fence


def extract_anchor_blocks(anchor_path: Path) -> dict[str, str]:
    """Extract the 4 base blocks from style-anchor.md.

    Returns dict with keys: art_style, shading, chroma, negative.
    """
    text = anchor_path.read_text(encoding="utf-8")

    def grab(start_marker: str) -> str:
        pattern = re.compile(
            rf"=== {re.escape(start_marker)}.*?===\n(.*?)(?=\n=== |\n```|\Z)",
            re.DOTALL,
        )
        m = pattern.search(text)
        if not m:
            raise ValueError(f"{anchor_path}: block '{start_marker}' not found")
        return m.group(1).strip()

    return {
        "art_style": grab("ART STYLE"),
        "shading": grab("SHADING / LIGHTING"),
        "chroma": grab("CHROMA BACKGROUND"),
        "negative": grab("NEGATIVE"),
    }


def find_pipeline_root(subject_path: Path) -> Path:
    """Walk up from subject_path until finding the art-pipeline directory."""
    for p in [subject_path] + list(subject_path.resolve().parents):
        if p.name == "art-pipeline":
            return p
    raise RuntimeError(f"art-pipeline root not found in path of {subject_path}")


def resolve_ref_paths(pipeline_root: Path, ref_specs: list[str]) -> list[tuple[Path, str]]:
    """Resolve REF specs to absolute paths with kind labels.

    Two ref kinds (chained REF policy):

    1. Anchor (P09 model capture) — "{char_id}"
       -> art-pipeline/ref/characters/{char_id}/anchor.png

    2. Prior output (previously generated illustration of same character) — "{char_id}:{file_stem}"
       -> art-pipeline/output/{char_id}/{file_stem}.png

    Returns list of (path, kind_label) tuples for downstream prompt assembly.

    Raises FileNotFoundError if any ref is missing.
    """
    paths: list[tuple[Path, str]] = []
    missing: list[str] = []
    for spec in ref_specs:
        if ":" in spec:
            char_id, file_stem = spec.split(":", 1)
            p = pipeline_root / "output" / char_id / f"{file_stem}.png"
            label = f"{char_id}/{file_stem} (prior output illustration)"
        else:
            p = pipeline_root / "ref" / "characters" / spec / "anchor.png"
            label = f"{spec}/anchor (P09 simplified 3D model capture)"
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
- The "anchor" image is the P09 simplified 3D model capture — silhouette / color zone source.
  Use it for shape / outfit-layout reference. The illustration MUST be more detailed.
- The "prior output illustration" images (if attached) are the canonical visual style and
  proportion baseline that prior cycles already locked in. The current variant must match
  these prior outputs in identity, hair color, eye color, outfit layer count, and palette.
- The character spec block below is the authoritative source for fine details (color hex,
  defining features). When the spec contradicts a ref, the spec wins for details, but the
  prior outputs win for identity continuity."""


def assemble(
    fm: dict[str, Any],
    fence: str,
    anchor: dict[str, str],
    ref_paths: list[tuple[Path, str]],
) -> str:
    """Compose the final prompt string.

    Order: ART STYLE → SHADING → REF attachment → SUBJECT prompt fence → CHROMA → NEGATIVE.
    """
    parts = [
        f"=== ART STYLE (엄수) ===\n{anchor['art_style']}",
        f"=== SHADING / LIGHTING ===\n{anchor['shading']}",
        build_ref_attachment_block(ref_paths),
        fence,
        f"=== CHROMA BACKGROUND (CRITICAL) ===\n{anchor['chroma']}",
        f"=== NEGATIVE ===\n{anchor['negative']}",
    ]
    return "\n\n".join(parts)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("subject", type=Path, help="path to subject page .md")
    ap.add_argument("--json", action="store_true", help="print metadata as JSON to stderr")
    args = ap.parse_args()

    subject_path = args.subject.resolve()
    if not subject_path.is_file():
        print(f"error: subject file not found: {subject_path}", file=sys.stderr)
        return 1

    try:
        fm, fence = parse_subject(subject_path)
    except ValueError as e:
        print(f"error: {e}", file=sys.stderr)
        return 2

    pipeline_root = find_pipeline_root(subject_path)
    anchor_path = pipeline_root / "style" / "style-anchor.md"
    if not anchor_path.is_file():
        print(f"error: style-anchor not found: {anchor_path}", file=sys.stderr)
        return 3

    try:
        anchor = extract_anchor_blocks(anchor_path)
    except ValueError as e:
        print(f"error: {e}", file=sys.stderr)
        return 4

    try:
        ref_paths = resolve_ref_paths(pipeline_root, fm["refs"])
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
