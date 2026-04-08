# 작업 계획

## 메타데이터

- 작업명: Combat Sandbox Re-centering
- 담당: Codex
- 상태: active
- 최종수정일: 2026-04-09
- 의존:
  - `tasks/001_mvp_vertical_slice/status.md`
  - `tasks/017_live_truth_town_sheet_sandbox_closure/status.md`

## Preflight

- dirty worktree와 user asset 변경을 보존한 채 코드/문서 범위만 편집한다.
- `docs/index.md`, `docs/03_architecture/index.md`, sandbox/runtime 관련 source-of-truth 문서를 먼저 유지한다.
- `SM.Unity`와 `SM.Editor` 의존 방향을 유지하고 runtime -> editor 참조를 만들지 않는다.
- 종료 조건과 oracle을 `spec.md`, `status.md`에 먼저 적고 진행한다.

## Phase 1 code-only

- task 018 문서 생성
- `FirstPlayableBootstrap`, `GameSessionRoot`, `GameBootstrap`, `BattleScreenController`, `tools/unity-bridge.ps1`에서 legacy alias 제거와 runtime bootstrap 단일화
- `SM/Play` preflight fail-fast 경로 추가
- `CombatSandboxConfig`, editor session/cache, inspector action helper 추가
- `CombatSandboxWindow`를 secondary tool surface로 축소

## Phase 2 asset authoring

- sandbox layout / preview settings asset 타입 추가
- active config와 scene controller가 새 asset 참조를 읽도록 연결
- shared `PanelSettings` 기본 해상도 정책 고정
- 기존 authoring asset과 scene asset은 destructive reset 없이 호환적으로 보강한다.

## Phase 3 validation

- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`
- `pwsh -File tools/test-harness-lint.ps1 -RepoRoot .`
- 필요 시 `pwsh -File tools/unity-bridge.ps1 test-batch-edit`
- PlayMode smoke와 수동 release gate는 blocker가 없을 때 추가로 확인하고 status에 남긴다.

## rollback / escape hatch

- Unity project lock / stale connector가 지속되면 문서와 code-only evidence까지만 확보하고 blocker를 status에 남긴다.
- scene binder typed reference 전환이 현재 scene asset과 충돌하면 runtime repair를 internal lane으로만 후퇴시키고 직접 asset authoring loop를 분리한다.
- giant class 분해가 범위를 넘기면 sandbox direct lane hot path만 1차 분리하고 후속 task로 넘긴다.

## tool usage plan

- file-first로 코드/문서를 읽고 `apply_patch`로만 편집한다.
- Unity validation은 `tools/unity-bridge.ps1` 우선
- docs 검증은 `tools/docs-policy-check.ps1`, `tools/docs-check.ps1`, `tools/smoke-check.ps1`
- low-level Unity scene mutation은 최소화하고 code path/asset contract 우선으로 닫는다.

## loop budget

- compile-fix 허용 횟수: 2
- refresh/read-console 반복 허용 횟수: 3
- blind asset generation 재시도 허용 횟수: 1
