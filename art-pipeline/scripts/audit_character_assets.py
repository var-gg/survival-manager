#!/usr/bin/env python3
"""Audit character image-generation coverage against the P09 asset manifest.

This is intentionally read-only. It checks subject pages, generated sheet
outputs, split outputs, and P09 anchor refs without invoking ChatGPT.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any

import yaml


def find_pipeline_root(start: Path) -> Path:
    for candidate in [start.resolve(), *start.resolve().parents]:
        if candidate.name == "art-pipeline":
            return candidate
    return Path(__file__).resolve().parents[1]


def load_yaml(path: Path) -> dict[str, Any]:
    data = yaml.safe_load(path.read_text(encoding="utf-8"))
    if not isinstance(data, dict):
        raise ValueError(f"{path}: expected YAML mapping")
    return data


def read_frontmatter(path: Path) -> dict[str, Any]:
    text = path.read_text(encoding="utf-8")
    if not text.startswith("---\n"):
        return {}
    end = text.find("\n---\n", 4)
    if end == -1:
        return {}
    data = yaml.safe_load(text[4:end])
    return data if isinstance(data, dict) else {}


def render_path(pattern: str, character_id: str) -> str:
    return pattern.format(id=character_id)


def exists(pipeline_root: Path, rel_path: str) -> bool:
    return (pipeline_root / rel_path).is_file()


def audit_character(
    pipeline_root: Path,
    manifest: dict[str, Any],
    character: dict[str, Any],
) -> dict[str, Any]:
    character_id = character["id"]
    profile_name = character["profile"]
    profiles = manifest["profiles"]
    requirements = manifest["asset_requirements"]

    if profile_name not in profiles:
        raise ValueError(f"{character_id}: unknown profile {profile_name}")

    result: dict[str, Any] = {
        "id": character_id,
        "profile": profile_name,
        "wiki_slug": character.get("wiki_slug"),
        "refs": {"expected": 0, "present": 0, "missing": []},
        "subjects": {"expected": 0, "present": 0, "missing": []},
        "outputs": {"expected": 0, "present": 0, "missing": []},
        "notes": [],
    }

    for requirement_name in profiles[profile_name]["required"]:
        requirement = requirements[requirement_name]

        for file_pattern in requirement.get("files", []):
            rel = render_path(file_pattern, character_id)
            result["refs"]["expected"] += 1
            if exists(pipeline_root, rel):
                result["refs"]["present"] += 1
            else:
                result["refs"]["missing"].append(rel)

        subject_rel = requirement.get("subject")
        subject_path: Path | None = None
        subject_fm: dict[str, Any] = {}
        if subject_rel:
            subject_rel = render_path(subject_rel, character_id)
            subject_path = pipeline_root / subject_rel
            result["subjects"]["expected"] += 1
            if subject_path.is_file():
                result["subjects"]["present"] += 1
                subject_fm = read_frontmatter(subject_path)
            else:
                result["subjects"]["missing"].append(subject_rel)

        output_patterns = list(requirement.get("outputs", []))
        output_patterns.extend(requirement.get("derived_outputs", []))

        dynamic_outputs = requirement.get("dynamic_outputs")
        if dynamic_outputs == "skill_icons_from_subject_frontmatter":
            skills = subject_fm.get("skills", []) if subject_fm else []
            if isinstance(skills, list):
                for skill_id in skills:
                    output_patterns.append(f"output/{{id}}/skill_icon_{skill_id}.png")
            elif subject_path and subject_path.is_file():
                result["notes"].append(
                    f"{requirement_name}: frontmatter skills is not a list"
                )

        for output_pattern in output_patterns:
            rel = render_path(output_pattern, character_id)
            result["outputs"]["expected"] += 1
            if exists(pipeline_root, rel):
                result["outputs"]["present"] += 1
            else:
                result["outputs"]["missing"].append(rel)

    missing_total = (
        len(result["refs"]["missing"])
        + len(result["subjects"]["missing"])
        + len(result["outputs"]["missing"])
    )
    if missing_total == 0:
        result["status"] = "complete"
    elif result["subjects"]["present"] == 0 and result["outputs"]["present"] == 0:
        result["status"] = "empty"
    else:
        result["status"] = "partial"

    return result


def ratio(bucket: dict[str, Any]) -> str:
    return f"{bucket['present']}/{bucket['expected']}"


def print_table(results: list[dict[str, Any]]) -> None:
    headers = ["character", "profile", "refs", "subjects", "outputs", "status"]
    rows = [
        [
            item["id"],
            item["profile"],
            ratio(item["refs"]),
            ratio(item["subjects"]),
            ratio(item["outputs"]),
            item["status"],
        ]
        for item in results
    ]
    widths = [
        max(len(str(row[index])) for row in [headers, *rows])
        for index in range(len(headers))
    ]

    print("  ".join(header.ljust(widths[index]) for index, header in enumerate(headers)))
    print("  ".join("-" * width for width in widths))
    for row in rows:
        print("  ".join(str(value).ljust(widths[index]) for index, value in enumerate(row)))


def print_markdown(results: list[dict[str, Any]]) -> None:
    print("| character | profile | refs | subjects | outputs | status |")
    print("| --- | --- | ---: | ---: | ---: | --- |")
    for item in results:
        print(
            "| {id} | {profile} | {refs} | {subjects} | {outputs} | {status} |".format(
                id=item["id"],
                profile=item["profile"],
                refs=ratio(item["refs"]),
                subjects=ratio(item["subjects"]),
                outputs=ratio(item["outputs"]),
                status=item["status"],
            )
        )


def summarize(results: list[dict[str, Any]]) -> dict[str, int]:
    summary = {"complete": 0, "partial": 0, "empty": 0}
    for item in results:
        summary[item["status"]] = summary.get(item["status"], 0) + 1
    return summary


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--manifest",
        type=Path,
        default=None,
        help="path to character_asset_manifest.yaml",
    )
    parser.add_argument("--character", action="append", help="limit to one or more character ids")
    parser.add_argument("--profile", action="append", help="limit to one or more profiles")
    parser.add_argument(
        "--format",
        choices=["table", "markdown", "json"],
        default="table",
        help="output format",
    )
    parser.add_argument(
        "--show-missing",
        action="store_true",
        help="print missing refs, subjects, and outputs after the summary",
    )
    parser.add_argument(
        "--strict",
        action="store_true",
        help="exit non-zero if any required asset is missing",
    )
    args = parser.parse_args()

    pipeline_root = find_pipeline_root(Path(__file__))
    manifest_path = args.manifest or (pipeline_root / "config" / "character_asset_manifest.yaml")
    manifest = load_yaml(manifest_path)

    characters = manifest.get("characters", [])
    if args.character:
        wanted = set(args.character)
        characters = [item for item in characters if item["id"] in wanted]
    if args.profile:
        wanted_profiles = set(args.profile)
        characters = [item for item in characters if item["profile"] in wanted_profiles]

    results = [audit_character(pipeline_root, manifest, character) for character in characters]

    if args.format == "json":
        print(json.dumps({"summary": summarize(results), "characters": results}, ensure_ascii=False, indent=2))
    elif args.format == "markdown":
        print_markdown(results)
        print()
        print(f"Summary: {summarize(results)}")
    else:
        print_table(results)
        print()
        print(f"Summary: {summarize(results)}")

    if args.show_missing and args.format != "json":
        for item in results:
            missing = item["refs"]["missing"] + item["subjects"]["missing"] + item["outputs"]["missing"]
            if not missing:
                continue
            print()
            print(f"[{item['id']}] missing {len(missing)}")
            for rel in missing:
                print(f"  - {rel}")

    if args.strict and any(item["status"] != "complete" for item in results):
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
