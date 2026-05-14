#!/usr/bin/env python3
"""Run image generation jobs for character subject pages sequentially.

The script wraps upload_subject.py and postprocess_character_sheets.py with
per-job logs under Logs/imagegen. It is intentionally simple because ChatGPT
generation should run one browser session at a time.
"""
from __future__ import annotations

import argparse
import datetime as dt
import subprocess
import sys
from pathlib import Path


POSTPROCESS_VARIANTS = {
    "face_emotion_sheet",
    "face_combat_state_sheet",
    "bust_emotion_sheet_R",
    "battle_stance_sheet",
}

NAMED_STORY_IDS = [
    "npc_lyra_sternfeld",
    "npc_grey_fang",
    "npc_black_vellum",
    "npc_silent_moon",
    "npc_baekgyu_sternheim",
]

BATTLE_CORE_IDS = [
    "warden",
    "guardian",
    "bulwark",
    "slayer",
    "reaver",
    "hunter",
    "scout",
    "marksman",
    "shaman",
    "rift_stalker",
    "bastion_penitent",
    "pale_executor",
    "mirror_cantor",
]

LEAD_IDS = [
    "hero_dawn_priest",
    "hero_pack_raider",
    "hero_grave_hexer",
    "hero_echo_savant",
]

PENDING_PINDOC_IDS = [
    "hero_aegis_sentinel",
    "hero_shardblade",
    "hero_prism_seeker",
    "hero_ember_runner",
    "hero_iron_pelt",
    "npc_aldric",
]

STORY_SHEET_VARIANTS = [
    "face_emotion_sheet",
    "face_combat_state_sheet",
    "bust_emotion_sheet_R",
    "battle_stance_sheet",
]

CURRENT_DIALOGUE_IDS = [*LEAD_IDS, *NAMED_STORY_IDS, *BATTLE_CORE_IDS]


def repo_root_from_script() -> Path:
    return Path(__file__).resolve().parents[2]


def parse_job(spec: str) -> tuple[str, str]:
    if ":" not in spec:
        raise ValueError(f"job must be character_id:variant, got {spec!r}")
    character_id, variant = spec.split(":", 1)
    if not character_id or not variant:
        raise ValueError(f"job must be character_id:variant, got {spec!r}")
    return character_id, variant


def phase_jobs(phase: str) -> list[tuple[str, str]]:
    if phase == "named-face":
        return [(character_id, "face_emotion_sheet") for character_id in NAMED_STORY_IDS]
    if phase == "named-rest":
        return [
            (character_id, variant)
            for character_id in NAMED_STORY_IDS
            for variant in ["face_combat_state_sheet", "bust_emotion_sheet_R", "battle_stance_sheet"]
        ]
    if phase == "core-minimal":
        print("[queue] phase core-minimal is deprecated; running full story sheet set for former battle-core ids.", flush=True)
        return [
            (character_id, variant)
            for character_id in BATTLE_CORE_IDS
            for variant in STORY_SHEET_VARIANTS
        ]
    if phase == "core-story-gap":
        return [
            (character_id, variant)
            for character_id in BATTLE_CORE_IDS
            for variant in ["face_emotion_sheet", "bust_emotion_sheet_R"]
        ]
    if phase == "lead-sheets":
        return [
            (character_id, variant)
            for character_id in LEAD_IDS
            for variant in STORY_SHEET_VARIANTS
        ]
    if phase == "story-all-current":
        return [
            (character_id, variant)
            for character_id in CURRENT_DIALOGUE_IDS
            for variant in STORY_SHEET_VARIANTS
        ]
    if phase == "story-all":
        return [
            (character_id, variant)
            for character_id in [*CURRENT_DIALOGUE_IDS, *PENDING_PINDOC_IDS]
            for variant in STORY_SHEET_VARIANTS
        ]
    raise ValueError(f"unknown phase: {phase}")


def run_job(
    repo_root: Path,
    logs_dir: Path,
    character_id: str,
    variant: str,
    timeout: int,
    force: bool,
    split_method: str,
) -> int:
    stamp = dt.datetime.now().strftime("%Y%m%d-%H%M%S")
    subject = repo_root / "art-pipeline" / "subjects" / "characters" / character_id / f"{variant}.md"
    if not subject.is_file():
        print(f"[queue] missing subject: {subject}", flush=True)
        return 2

    stdout_path = logs_dir / f"{character_id}_{variant}_{stamp}.out.log"
    stderr_path = logs_dir / f"{character_id}_{variant}_{stamp}.err.log"
    cmd = [
        sys.executable,
        "art-pipeline/scripts/upload_subject.py",
        str(subject.relative_to(repo_root)),
        "--timeout",
        str(timeout),
    ]
    if force:
        cmd.append("--force")

    print(f"[queue] START {character_id}/{variant}", flush=True)
    print(f"[queue] stdout={stdout_path}", flush=True)
    print(f"[queue] stderr={stderr_path}", flush=True)
    with stdout_path.open("w", encoding="utf-8") as out, stderr_path.open("w", encoding="utf-8") as err:
        result = subprocess.run(cmd, cwd=repo_root, stdout=out, stderr=err)
    print(f"[queue] DONE {character_id}/{variant} exit={result.returncode}", flush=True)
    if result.returncode != 0:
        return result.returncode

    if variant in POSTPROCESS_VARIANTS:
        pp_cmd = [
            sys.executable,
            "art-pipeline/scripts/postprocess_character_sheets.py",
            character_id,
            variant,
            "--method",
            split_method,
        ]
        print(f"[queue] POSTPROCESS {character_id}/{variant}", flush=True)
        pp = subprocess.run(pp_cmd, cwd=repo_root)
        print(f"[queue] POSTPROCESS_DONE {character_id}/{variant} exit={pp.returncode}", flush=True)
        if pp.returncode != 0:
            return pp.returncode
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--phase",
        action="append",
        choices=[
            "named-face",
            "named-rest",
            "core-minimal",
            "core-story-gap",
            "lead-sheets",
            "story-all-current",
            "story-all",
        ],
    )
    parser.add_argument("--job", action="append", help="explicit character_id:variant job")
    parser.add_argument("--skip-existing-output", action="store_true", help="skip when output/{id}/{variant}.png already exists")
    parser.add_argument("--timeout", type=int, default=600)
    parser.add_argument("--no-force", action="store_true", help="do not pass --force to upload_subject.py")
    parser.add_argument("--split-method", choices=["auto", "hardcoded"], default="auto")
    parser.add_argument("--continue-on-error", action="store_true")
    args = parser.parse_args()

    if not args.phase and not args.job:
        parser.error("provide --phase and/or --job")

    repo_root = repo_root_from_script()
    logs_dir = repo_root / "Logs" / "imagegen"
    logs_dir.mkdir(parents=True, exist_ok=True)
    output_root = repo_root / "art-pipeline" / "output"

    jobs: list[tuple[str, str]] = []
    for phase in args.phase or []:
        jobs.extend(phase_jobs(phase))
    for spec in args.job or []:
        jobs.append(parse_job(spec))

    failures: list[tuple[str, str, int]] = []
    for character_id, variant in jobs:
        if args.skip_existing_output and (output_root / character_id / f"{variant}.png").is_file():
            print(f"[queue] SKIP existing {character_id}/{variant}", flush=True)
            continue
        code = run_job(
            repo_root,
            logs_dir,
            character_id,
            variant,
            args.timeout,
            force=not args.no_force,
            split_method=args.split_method,
        )
        if code != 0:
            failures.append((character_id, variant, code))
            if not args.continue_on_error:
                break

    if failures:
        print("[queue] FAILURES", flush=True)
        for character_id, variant, code in failures:
            print(f"  - {character_id}/{variant}: exit={code}", flush=True)
        return 1
    print("[queue] ALL_DONE", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
