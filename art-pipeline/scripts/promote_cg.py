#!/usr/bin/env python3
"""Promote a generated narrative illustration from raw output into the canonical CG store.

    art-pipeline/output/<subject_id>/<variant>.png  ->  art-pipeline/cg/<subject_id>.png

and writes a sidecar `art-pipeline/cg/<subject_id>.json` — the generation recipe that
asset-studio surfaces in the Inspector and binds to the scene in the Narrative Visual panel.

Why a separate canonical store: `art-pipeline/output` is the raw, churny generation staging
(every candidate + *_raw.png). The canonical `cg/` store holds one named keeper per backdrop id
so narrative illustrations become stable assets that do not drift, and so the asset-studio
`art-cg` scan root (default-visible) can show them as "Cutscene CG" / "Backgrounds".

Classification + metadata are pulled from `tools/narrative-visual-map.json` by matching the
override whose `backdrop == "bespoke:<subject_id>"`. The generation prompt is embedded from
`--prompt-json` (the subject JSON used at generation time) when available.

Usage:
    python art-pipeline/scripts/promote_cg.py <subject_id> [--variant default] \
        [--prompt-json <path>] [--engine chatgpt-image]
"""
import argparse
import json
import shutil
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]  # scripts -> art-pipeline -> repo root
OUTPUT = REPO / "art-pipeline" / "output"
CG = REPO / "art-pipeline" / "cg"
VMAP = REPO / "tools" / "narrative-visual-map.json"

_BG_PREFIXES = ("site_", "town_", "loc_")


def backdrop_mode(subject_id: str) -> str:
    """concept_art_* 신작은 bespoke, site_/town_/loc_ 공용 배경은 shared 토큰."""
    return "bespoke" if subject_id.startswith("concept_art_") else "shared"


def beatmap_meta(subject_id: str) -> dict:
    """Look up tier/kind/subjects/motion/note from the visual beat map override."""
    if not VMAP.exists():
        return {}
    vmap = json.loads(VMAP.read_text(encoding="utf-8"))
    target = f"{backdrop_mode(subject_id)}:{subject_id}"
    for scene_id, override in vmap.get("overrides", {}).items():
        if override.get("backdrop") == target:
            return {
                "scene_id": scene_id,
                "tier": override.get("tier"),
                "kind": override.get("kind", "env"),
                "subjects": override.get("subjects", []),
                "motion": override.get("motion"),
                "note": override.get("note"),
            }
    return {}


def category_for(subject_id: str) -> str:
    return "backgrounds" if subject_id.startswith(_BG_PREFIXES) else "cutscene"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("subject_id")
    parser.add_argument("--variant", default="default")
    parser.add_argument(
        "--prompt-json",
        default=None,
        help="subject JSON used at generation (embeds prompt + refs into the sidecar)",
    )
    parser.add_argument("--engine", default="chatgpt-image")
    args = parser.parse_args()

    src = OUTPUT / args.subject_id / f"{args.variant}.png"
    if not src.exists():
        sys.exit(f"source not found: {src}")

    CG.mkdir(parents=True, exist_ok=True)
    dst = CG / f"{args.subject_id}.png"
    shutil.copy2(src, dst)

    meta = beatmap_meta(args.subject_id)
    sidecar = {
        "id": args.subject_id,
        "asset_class": "narrative_cg",
        "category": category_for(args.subject_id),
        "backdrop": f"{backdrop_mode(args.subject_id)}:{args.subject_id}",
        "engine": args.engine,
        "kind": meta.get("kind", "env"),
        "subjects": meta.get("subjects", []),
        "tier": meta.get("tier"),
        "motion": meta.get("motion"),
        "scene_id": meta.get("scene_id"),
        "note": meta.get("note"),
        "variant": args.variant,
        "promoted_from": str(src.relative_to(REPO)).replace("\\", "/"),
    }

    if args.prompt_json:
        prompt_path = Path(args.prompt_json)
        if prompt_path.exists():
            data = json.loads(prompt_path.read_text(encoding="utf-8"))
            sidecar["prompt"] = data.get("prompt")
            frontmatter = data.get("frontmatter", {})
            sidecar["refs"] = frontmatter.get("refs", [])
            sidecar["aspect"] = frontmatter.get("aspect")
            sidecar["output_size"] = frontmatter.get("output_size")
            sidecar["gen_kind"] = frontmatter.get("kind")

    sidecar_path = CG / f"{args.subject_id}.json"
    sidecar_path.write_text(
        json.dumps(sidecar, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    print(f"promoted {args.subject_id} -> {dst.relative_to(REPO)}")
    print(f"sidecar  -> {sidecar_path.relative_to(REPO)}")


if __name__ == "__main__":
    main()
