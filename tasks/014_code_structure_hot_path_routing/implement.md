# 작업 구현

## 메타데이터

- 작업명: Code Structure Hot-Path Routing
- 담당: Codex
- 상태: handoff-ready
- 최종수정일: 2026-04-02
- 실행범위:
  - `AGENTS.md`
  - `docs/index.md`
  - `docs/03_architecture/index.md`
  - `docs/03_architecture/testing-strategy.md`
  - `docs/07_release/index.md`
  - `docs/00_governance/source-of-truth-matrix.md`
  - `tasks/014_code_structure_hot_path_routing/`

## Phase log

- Phase 0 preflight
  - 현재 AGENTS, 상위 index, architecture index, testing draft, release index, source-of-truth matrix를 확인했다.
  - `docs/index.md`의 `07_release` 누락, architecture/testing 역할 경계 약함, matrix의 code-structure governance 행 부재를 확인했다.
  - 기존 `tasks/002_code_governance_hardening`, `tasks/003_docs_harness_hardening`는 참고만 하고 새 task `014`로 분리하기로 고정했다.
- Phase 1 code-only
  - `AGENTS.md`에 code-structure hot-path 규칙과 기본 시작 컨텍스트를 추가했다.
  - `docs/index.md`를 compact start surface로 재작성하고 `07_release`를 포함시켰다.
  - `docs/03_architecture/index.md`에 code structure governance 문서군과 시작 컨텍스트를 추가했다.
  - `docs/03_architecture/testing-strategy.md`를 closure 문서가 아닌 검증 우선순위 draft로 재작성했다.
  - `docs/07_release/index.md`를 메타데이터가 있는 최소 인덱스로 올렸다.
  - `docs/00_governance/source-of-truth-matrix.md`에 code structure governance 행과 관련문서 링크를 추가했다.
- Phase 2 asset authoring
  - N/A (docs-only)
- Phase 3 validation
  - docs policy, full docs check, smoke check, 범위 한정 `git diff --check`를 실행하고 결과를 `status.md`에 정리했다.

## deviation

- 계획과 다르게 범위를 넓힌 것은 없다.
- `docs-governance.md`, `docs-harness.md`, `docs/00_governance/index.md`, ADR, skill 문서는 편집하지 않았다.

## blockers

- 예상 blocker는 repo-wide markdown/style/link debt 가능성이며, touched file 유발과 분리해서 기록한다.

## diagnostics

- 구조 정책은 이미 `coding-principles.md`, ADR-0012, `implementation-review-checklist.md`에 존재했고, 이번 작업은 그 규칙을 start surface로 재배선하는 성격이다.
- `docs/index.md`는 기존 quick-link 허브 성격이 강해 top-level start surface로는 과밀했다.
- `testing-strategy.md`는 `validation-and-acceptance-oracles.md`와 역할이 겹치고 있어 scope를 좁히는 방향이 적절했다.
- `docs-policy-check`는 첫 실행에서 `docs/index.md -> docs/status.md` 해석 오류를 잡았고, 표현을 `현재 task 상태 문서`로 바꾼 뒤 재실행에서 통과했다.
- `docs-check`는 touched file이 아니라 `Packages/com.coplaydev.unity-mcp/README.md:55:22`의 기존 `MD033/no-inline-html` markdownlint debt로 실패했다.
- `smoke-check`와 범위 한정 `git diff --check`는 통과했다.

## why this loop happened

- code-quality 정책은 있었지만 `AGENTS.md`와 source-of-truth matrix의 기본 라우팅에 올라오지 않아 cold path로 남아 있었다.
- docs harness는 활성화됐지만 code-structure governance는 start surface에 직접 노출되지 않아 세션 시작 시 우선순위가 낮아질 수 있었다.

## 기록 규칙

- 미시 검증 로그는 누적하지 않고 결과와 원인만 `status.md`에 남긴다.
- touched file와 repo-wide debt를 분리해서 적는다.
- index와 task 문서를 같은 변경 단위에서 갱신한다.
