# 작업 계획

## 메타데이터

- 작업명: Code Structure Hot-Path Routing
- 담당: Codex
- 상태: handoff-ready
- 최종수정일: 2026-04-02
- 의존:
  - `docs/00_governance/task-execution-pattern.md`
  - `docs/00_governance/docs-governance.md`
  - `docs/00_governance/docs-harness.md`

## Preflight

- `AGENTS.md`, `docs/index.md`, `docs/03_architecture/index.md`, `docs/03_architecture/testing-strategy.md`, `docs/07_release/index.md`, `docs/00_governance/source-of-truth-matrix.md`의 현재 상태를 먼저 확인한다.
- 기존 task인 `tasks/002_code_governance_hardening/`, `tasks/003_docs_harness_hardening/`은 참고만 하고 reopen하지 않는다.
- docs-only 작업임을 `spec.md`와 `status.md`에 먼저 고정한다.
- forbidden 범위가 없는지, 문서 하네스와 코드 구조 하네스 둘 다 필요한 작업인지 먼저 확인한다.

## Phase 1 code-only

- docs/governance start surface만 수정한다.
- `AGENTS.md`에 코드 구조 하네스 규칙을 추가한다.
- `docs/index.md`를 compact top-level index로 재작성한다.
- `docs/03_architecture/index.md`에 코드 구조 거버넌스 섹션과 시작 컨텍스트를 추가한다.
- `docs/03_architecture/testing-strategy.md`를 role-narrowed draft로 재작성한다.
- `docs/07_release/index.md`를 메타데이터가 있는 최소 인덱스로 재작성한다.
- `docs/00_governance/source-of-truth-matrix.md`에 code structure governance 행을 추가한다.

## Phase 2 asset authoring

- N/A (docs-only)
- asset 생성, refresh, scene/prefab 변경, Play Mode 경로는 수행하지 않는다.

## Phase 3 validation

- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
- `pwsh -File tools/docs-check.ps1 -RepoRoot .`
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`
- `git diff --check -- AGENTS.md docs tasks/014_code_structure_hot_path_routing`
- 실패는 touched file 유발인지 기존 repo-wide debt인지 구분해 `status.md`에 남긴다.

## rollback / escape hatch

- docs-policy-check가 새 변경 때문에 실패하면 touched 문서만 우선 수정하고 다시 검증한다.
- docs-check가 기존 repo-wide debt로 실패하면 touched file 문제를 제거한 뒤 debt로 기록하고 범위를 더 넓히지 않는다.
- 인덱스 재작성 중 링크 단절이 생기면 quick-link 복구가 아니라 compact start surface 원칙을 유지한 채 entry link만 보강한다.

## tool usage plan

- file-first로 `AGENTS.md` -> `docs/index.md` -> 관련 폴더 `index.md` -> task 문서 순서로 읽고 수정한다.
- 문서 편집은 patch 기반으로 수행한다.
- 검증은 PowerShell 스크립트 우선으로 하고, 결과는 task `status.md`에 집약한다.

## loop budget

- compile-fix 허용 횟수: 0
- refresh/read-console 반복 허용 횟수: 0
- blind asset generation 재시도 허용 횟수: 0
- docs validation 재시도 허용 횟수: 2
