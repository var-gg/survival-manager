# 작업 계획

## 메타데이터

- 작업명: System Deepening Pass
- 담당: Codex
- 상태: 진행 중
- 최종수정일: 2026-04-01
- 의존:
  - `tasks/005_battle_contract_closure/status.md`
  - `docs/00_governance/task-execution-pattern.md`
  - `docs/03_architecture/content-authoring-and-balance-data.md`

## Preflight

- worktree clean 상태와 기존 compile blocker 유무를 먼저 확인한다.
- `docs-maintainer` 기준에 따라 task packet, index sync, 관련문서 링크를 같은 작업 단위로 묶는다.
- `SM.Content` / `SM.Editor` / `SM.Tests` 경계를 유지하고 `SM.Combat`에는 authoring 세부가 새지 않게 한다.
- done criterion은 `spec.md`, `status.md`에 먼저 기록한다.

## Phase 1 code-only

- definition schema를 additive로 확장한다.
- backward-compatible default를 둬 기존 committed asset이 바로 깨지지 않게 한다.
- validator와 balance sweep report가 새 필드를 읽고 consistency를 검사하도록 확장한다.
- EditMode test는 새 schema drift와 report field shape를 먼저 확인한다.

## Phase 2 asset authoring

- runtime asset 대량 재생성은 기본으로 하지 않는다.
- committed asset은 current launch floor를 유지하고, 새 schema는 default/fallback로 통과하게 둔다.
- PR 2 catalog는 asset이 아니라 Markdown source-of-truth로 추가한다.
- 필요 시 `SampleSeedGenerator`는 새 schema의 최소 canonical field만 채우도록 보강한다.

## Phase 3 validation

- compile
- docs policy check
- docs check
- smoke check
- 가능한 범위의 targeted EditMode test
- evidence는 `status.md`에 명령과 핵심 결과만 요약한다.

## rollback / escape hatch

- schema 확장 때문에 existing asset validator가 대량 붕괴하면, 필수값 강제를 warning/fallback로 낮추고 task 문서에 deferred를 남긴다.
- balance metric이 runtime event만으로 안정적으로 산출되지 않으면, PR 1에서는 artifact field scaffold까지만 닫고 metric source를 `phase-02`로 미룬다.
- catalog 문서가 과도하게 길어지면 PR 2 phase 문서에서 live subset과 full catalog를 분리한다.

## tool usage plan

- file-first로 `Get-Content`, `rg`, `git status`를 사용한다.
- 코드 편집은 `apply_patch`만 사용한다.
- Unity 확인은 `tools/unity-bridge.ps1`와 docs/smoke 스크립트를 우선한다.
- scene/prefab 수동 편집이나 MCP는 이번 task의 기본 경로가 아니다.

## loop budget

- compile-fix 허용 횟수: 3
- refresh/read-console 반복 허용 횟수: 2
- blind asset generation 재시도 허용 횟수: 1
