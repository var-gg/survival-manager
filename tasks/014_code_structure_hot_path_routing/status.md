# 작업 상태

## 메타데이터

- 작업명: Code Structure Hot-Path Routing
- 담당: Codex
- 상태: handoff-ready
- 최종수정일: 2026-04-02

## Current state

- code-structure governance를 문서 start surface로 끌어올리는 편집을 완료했다.
- 검증 스크립트와 범위 한정 diff check를 실행했고 결과를 정리했다.
- 현재 상태는 handoff-ready다.

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 / 다음 확인 |
| --- | --- | --- | --- |
| AGENTS routing | AGENTS가 code structure routing을 first-class trigger로 노출한다. | 완료 | `AGENTS.md`의 `## 코드 구조 하네스 규칙` |
| top-level docs index | docs index가 compact top-level index가 되고 `07_release`를 포함한다. | 완료 | `docs/index.md`의 문서 계층과 시작점 |
| architecture role split | architecture index가 oracle 문서와 testing draft의 역할을 분리한다. | 완료 | `docs/03_architecture/index.md`의 새 섹션과 설명 |
| source-of-truth row | source-of-truth matrix에 code structure governance 행이 생긴다. | 완료 | `docs/00_governance/source-of-truth-matrix.md`의 새 행 |
| testing strategy scope | `testing-strategy.md`가 closure 문서가 아니라 검증 우선순위 문서로 좁혀진다. | 완료 | `docs/03_architecture/testing-strategy.md`의 목적/비목표/역할 경계 |
| validation evidence | 검증 스크립트 결과와 repo-wide debt가 evidence에 기록된다. | 완료 | 아래 Evidence와 Remaining blockers |

## Evidence

- 실행한 명령
  - `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
  - `pwsh -File tools/docs-check.ps1 -RepoRoot .`
  - `pwsh -File tools/smoke-check.ps1 -RepoRoot .`
  - `git diff --check -- AGENTS.md docs tasks/014_code_structure_hot_path_routing`
- 결과 요약
  - `docs-policy-check`: 1차 실패 후 수정 반영, 재실행 통과.
  - `docs-check`: 실패. `Packages/com.coplaydev.unity-mcp/README.md:55:22`의 기존 `MD033/no-inline-html` debt.
  - `smoke-check`: 통과.
  - `git diff --check`: 통과.
- 관련 문서
  - `AGENTS.md`
  - `docs/index.md`
  - `docs/03_architecture/index.md`
  - `docs/03_architecture/testing-strategy.md`
  - `docs/07_release/index.md`
  - `docs/00_governance/source-of-truth-matrix.md`

## Remaining blockers

- `docs-check`는 touched file이 아니라 `Packages/com.coplaydev.unity-mcp/README.md`의 기존 markdownlint debt 때문에 실패한다.

## Deferred / debug-only

- `docs-governance.md`, `docs-harness.md`, `docs/00_governance/index.md` 본문 정리
- skill 본문 수정
- repo-wide markdownlint / markdown-link-check debt cleanup

## Loop budget consumed

- compile-fix: 0
- refresh/read-console: 0
- asset authoring retry: 0
- budget 초과 시 남긴 diagnosis: 없음

## Handoff notes

- 다음 세션이 시작할 문서는 `AGENTS.md` -> `docs/index.md` -> `docs/03_architecture/index.md` -> `tasks/014_code_structure_hot_path_routing/status.md`
- 구조 정책 본체를 다시 쓰지 말고 hot-path 노출만 유지한다.
- 검증 실패 시 touched file 원인과 기존 debt를 섞지 않는다.
