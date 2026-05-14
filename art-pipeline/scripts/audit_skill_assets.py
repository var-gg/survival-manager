#!/usr/bin/env python3
"""Audit skill and presentation icon coverage against skill_asset_manifest.yaml.

This is read-only. It keeps skill/presentation icon readiness separate from the
character portrait matrix so character pages do not imply gameplay skill
ownership.
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


def render_path(pattern: str, asset: dict[str, Any], skill: str | None = None) -> str:
    values = dict(asset)
    if skill is not None:
        values["skill"] = skill
    return pattern.format(**values)


def exists(pipeline_root: Path, rel_path: str) -> bool:
    return (pipeline_root / rel_path).is_file()


def audit_asset(
    pipeline_root: Path,
    manifest: dict[str, Any],
    asset: dict[str, Any],
) -> dict[str, Any]:
    asset_id = asset["id"]
    profile_name = asset["profile"]
    profiles = manifest["profiles"]
    requirements = manifest["asset_requirements"]

    if profile_name not in profiles:
        raise ValueError(f"{asset_id}: unknown profile {profile_name}")

    result: dict[str, Any] = {
        "id": asset_id,
        "profile": profile_name,
        "theme_character_id": asset.get("theme_character_id"),
        "subjects": {"expected": 0, "present": 0, "missing": []},
        "outputs": {"expected": 0, "present": 0, "missing": []},
        "warnings": [],
    }

    for requirement_name in profiles[profile_name]["required"]:
        requirement = requirements[requirement_name]

        subject_rel = requirement.get("subject")
        subject_fm: dict[str, Any] = {}
        if subject_rel:
            subject_rel = render_path(subject_rel, asset)
            subject_path = pipeline_root / subject_rel
            result["subjects"]["expected"] += 1
            if subject_path.is_file():
                result["subjects"]["present"] += 1
                subject_fm = read_frontmatter(subject_path)
                if subject_fm.get("subject_id") != asset_id:
                    result["warnings"].append(
                        f"{subject_rel}: subject_id is {subject_fm.get('subject_id')!r}, expected {asset_id!r}"
                    )
                expected_theme = asset.get("theme_character_id")
                if expected_theme and subject_fm.get("theme_character_id") != expected_theme:
                    result["warnings"].append(
                        f"{subject_rel}: theme_character_id is {subject_fm.get('theme_character_id')!r}, expected {expected_theme!r}"
                    )
                if profile_name == "character_theme_bridge" and subject_fm.get("kind") != "skill_icon_theme_sheet":
                    result["warnings"].append(
                        f"{subject_rel}: kind is {subject_fm.get('kind')!r}, expected 'skill_icon_theme_sheet'"
                    )
            else:
                result["subjects"]["missing"].append(subject_rel)

        output_patterns = list(requirement.get("outputs", []))
        dynamic = requirement.get("dynamic_outputs")
        if isinstance(dynamic, dict):
            source_key = dynamic.get("from_frontmatter")
            pattern = dynamic.get("pattern")
            values = subject_fm.get(source_key, []) if subject_fm and source_key else []
            if isinstance(values, list) and pattern:
                for skill_id in values:
                    output_patterns.append(pattern.replace("{skill}", str(skill_id)))
            elif subject_fm and source_key:
                result["warnings"].append(
                    f"{requirement_name}: frontmatter {source_key!r} is not a list"
                )

        for output_pattern in output_patterns:
            rel = render_path(output_pattern, asset)
            result["outputs"]["expected"] += 1
            if exists(pipeline_root, rel):
                result["outputs"]["present"] += 1
            else:
                result["outputs"]["missing"].append(rel)

    missing_total = len(result["subjects"]["missing"]) + len(result["outputs"]["missing"])
    if missing_total == 0 and result["warnings"]:
        result["status"] = "review"
    elif missing_total == 0:
        result["status"] = "complete"
    elif result["subjects"]["present"] == 0 and result["outputs"]["present"] == 0:
        result["status"] = "empty"
    else:
        result["status"] = "partial"

    return result


def ratio(bucket: dict[str, Any]) -> str:
    return f"{bucket['present']}/{bucket['expected']}"


def print_table(results: list[dict[str, Any]]) -> None:
    headers = ["asset", "profile", "subject", "outputs", "warnings", "status"]
    rows = [
        [
            item["id"],
            item["profile"],
            ratio(item["subjects"]),
            ratio(item["outputs"]),
            str(len(item["warnings"])),
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
    print("| asset | profile | subject | outputs | warnings | status |")
    print("| --- | --- | ---: | ---: | ---: | --- |")
    for item in results:
        print(
            "| {id} | {profile} | {subjects} | {outputs} | {warnings} | {status} |".format(
                id=item["id"],
                profile=item["profile"],
                subjects=ratio(item["subjects"]),
                outputs=ratio(item["outputs"]),
                warnings=len(item["warnings"]),
                status=item["status"],
            )
        )


def summarize(results: list[dict[str, Any]]) -> dict[str, int]:
    summary = {"complete": 0, "review": 0, "partial": 0, "empty": 0}
    for item in results:
        summary[item["status"]] = summary.get(item["status"], 0) + 1
    return summary


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--manifest",
        type=Path,
        default=None,
        help="path to skill_asset_manifest.yaml",
    )
    parser.add_argument("--asset", action="append", help="limit to one or more asset ids")
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
        help="print missing subjects, outputs, and warnings after the summary",
    )
    parser.add_argument(
        "--strict",
        action="store_true",
        help="exit non-zero if any required asset is missing or flagged for review",
    )
    args = parser.parse_args()

    pipeline_root = find_pipeline_root(Path(__file__))
    manifest_path = args.manifest or (pipeline_root / "config" / "skill_asset_manifest.yaml")
    manifest = load_yaml(manifest_path)

    assets = manifest.get("assets", [])
    if args.asset:
        wanted = set(args.asset)
        assets = [item for item in assets if item["id"] in wanted]
    if args.profile:
        wanted_profiles = set(args.profile)
        assets = [item for item in assets if item["profile"] in wanted_profiles]

    results = [audit_asset(pipeline_root, manifest, asset) for asset in assets]

    if args.format == "json":
        print(json.dumps({"summary": summarize(results), "assets": results}, ensure_ascii=False, indent=2))
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
            diagnostics = item["subjects"]["missing"] + item["outputs"]["missing"] + item["warnings"]
            if not diagnostics:
                continue
            print()
            print(f"[{item['id']}] diagnostics {len(diagnostics)}")
            for line in diagnostics:
                print(f"  - {line}")

    if args.strict and any(item["status"] != "complete" for item in results):
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
