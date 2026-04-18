# docs markdownlint cleanup 상태

## 메타데이터

- 작업명: docs markdownlint cleanup
- 담당: Codex
- 상태: completed
- 최종수정일: 2026-04-19

## Current state

- 021 commit `96e9c4c` 이후 main에서 시작했다.
- full `docs-check`의 markdownlint backlog 14개 파일, 1716건을 정리했다.
- `.markdownlint-cli2.jsonc` rule 완화 없이 active/root/package Markdown 형식을 수정했다.

## Backlog classification

| Rule | Count | 처리 방향 |
| --- | ---: | --- |
| MD060 | 1654 | table spacing mechanical cleanup 완료 |
| MD036 | 26 | emphasis heading을 실제 heading으로 전환 완료 |
| MD040 | 18 | code fence language 지정 완료 |
| MD029 | 9 | ordered list 번호 정리 완료 |
| MD028 | 8 | blockquote blank line 정리 완료 |
| MD033 | 1 | inline HTML 제거 완료 |

## Affected files

| File | Count |
| --- | ---: |
| `docs/02_design/narrative/master-script.md` | 614 |
| `docs/02_design/narrative/dialogue-event-schema.md` | 584 |
| `docs/02_design/narrative/chapter-beat-sheet.md` | 152 |
| `docs/02_design/deck/character-lore-registry.md` | 68 |
| `docs/02_design/narrative/narrative-pacing-formula.md` | 67 |
| `docs/02_design/narrative/faction-conflict-matrix.md` | 54 |
| `docs/02_design/narrative/world-building-bible.md` | 54 |
| `docs/02_design/narrative/campaign-story-arc.md` | 48 |
| `docs/02_design/deck/hero-expansion-roadmap.md` | 42 |
| `docs/02_design/narrative/authoring-guide.md` | 18 |
| `docs/02_design/meta/augment-catalog-v1.md` | 9 |
| `docs/03_architecture/testing-strategy.md` | 4 |
| `CLAUDE.md` | 1 |
| `Packages/com.coplaydev.unity-mcp/README.md` | 1 |

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 / 다음 확인 |
| --- | --- | --- | --- |
| markdownlint | full docs-check error 0 | 통과 | `docs-check` Summary: 0 error(s) |
| policy | docs policy pass | 통과 | `docs-policy-check` pass |
| smoke | repo smoke pass | 통과 | `smoke-check` pass |
| scope | rule 완화 없음 | 통과 | `.markdownlint-cli2.jsonc` 변경 없음 |

## Loop budget consumed

- markdownlint retry: 2/4
- docs-check retry: 2/3
- docs policy/smoke retry: 1/1

## Handoff notes

- package README issue는 full docs-check green 기준 때문에 함께 닫았다.
- full `docs-check`는 기본 `LinkCheckTimeoutSeconds`로 약 9분 15초 소요되어 통과했다.
