#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""pindoc → raw-wiki 덤프 (narrative 파이프라인의 누락된 첫 단계).

배경
----
narrative 파이프라인은 다음과 같다:

    pindoc (SoT)  ──[이 스크립트]──▶  tools/raw-wiki/<slug>.md
                  ──[wiki_narrative_extract.py]──▶  Logs/Narrative/narrative-seed-wiki.json
                  ──[NarrativeSeedImporter (Unity)]──▶  StoryEvent/DialogueSequence SO

`raw-wiki`는 pindoc 본문(frontmatter 제거)의 미러다. 이 덤프 단계가 수동이라
오래 안 돌리면 raw-wiki가 stale해지고 "pindoc done ≠ game done" drift가 생긴다
(앱은 pindoc보다 옛 narrative를 보여주게 됨). 이 스크립트로 그 단계를 표준화한다.

사용법
------
1) 에이전트가 MCP로 pindoc export를 받아 JSON으로 저장:
     mcp__pindoc__pindoc_project_export(project_slug="survival-manager",
         areas=["narrative"], format="zip")
   → 결과(JSON: {content_base64, ...})를 파일로 저장 (예: export.json)
   (대용량이면 도구가 자동으로 파일에 저장해 경로를 돌려준다.)

2) 이 스크립트로 raw-wiki 갱신:
     python tools/pindoc_wiki_dump.py <export.json> [--check]

   --check : 쓰지 않고 raw-wiki와 diff만 보고 (drift 점검용).

raw-wiki는 gitignored이므로 이 스크립트 출력은 커밋되지 않는다. 갱신 후
`python tools/wiki_narrative_extract.py`로 seed를 재생성한다.
"""
import argparse
import base64
import io
import json
import pathlib
import re
import sys
import zipfile

REPO_ROOT = pathlib.Path(__file__).resolve().parent.parent
RAW_WIKI_DIR = REPO_ROOT / "tools" / "raw-wiki"

_FRONTMATTER_RE = re.compile(r"^---\r?\n.*?\r?\n---\r?\n", re.DOTALL)


def strip_frontmatter(text: str) -> str:
    """raw-wiki는 frontmatter 없는 body 미러. export는 frontmatter 포함이라 제거."""
    if text.startswith("---"):
        m = _FRONTMATTER_RE.match(text)
        if m:
            return text[m.end():]
    return text


def load_export(export_path: pathlib.Path) -> "zipfile.ZipFile":
    data = json.loads(export_path.read_text(encoding="utf-8"))
    if "content_base64" not in data:
        raise SystemExit(
            f"'{export_path}' 에 content_base64 가 없음 — pindoc_project_export 결과 JSON 인지 확인."
        )
    return zipfile.ZipFile(io.BytesIO(base64.b64decode(data["content_base64"])))


def main() -> int:
    ap = argparse.ArgumentParser(description="pindoc export → tools/raw-wiki/<slug>.md")
    ap.add_argument("export_json", help="pindoc_project_export 결과 JSON 파일 경로")
    ap.add_argument("--check", action="store_true",
                    help="쓰지 않고 raw-wiki와의 차이만 보고 (drift 점검)")
    args = ap.parse_args()

    if not RAW_WIKI_DIR.exists():
        RAW_WIKI_DIR.mkdir(parents=True, exist_ok=True)

    zf = load_export(pathlib.Path(args.export_json))
    md_names = [n for n in zf.namelist() if n.endswith(".md")]
    if not md_names:
        print("export 에 .md 가 없음.", file=sys.stderr)
        return 1

    written = 0
    drifted = []
    for name in md_names:
        slug = pathlib.PurePosixPath(name).stem
        body = strip_frontmatter(zf.read(name).decode("utf-8"))
        target = RAW_WIKI_DIR / f"{slug}.md"
        current = target.read_text(encoding="utf-8") if target.exists() else None
        if args.check:
            if current != body:
                drifted.append(slug)
            continue
        if current != body:
            target.write_text(body, encoding="utf-8", newline="\n")
            written += 1

    if args.check:
        if drifted:
            print(f"DRIFT: raw-wiki 가 pindoc 과 다른 아티팩트 {len(drifted)}개:")
            for s in drifted:
                print(f"  - {s}")
            return 1
        print(f"OK: raw-wiki 가 pindoc export({len(md_names)}개)와 일치.")
        return 0

    print(f"raw-wiki 갱신: {written}/{len(md_names)} 파일 변경 (나머지는 이미 동일).")
    print("다음: python tools/wiki_narrative_extract.py 로 seed 재생성.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
