# 작업 계획

## 메타데이터

- 작업명: Battle Contract Closure
- 담당: Codex
- 상태: 진행 중
- 최종수정일: 2026-04-08
- 의존:
  - `tasks/004_launch_floor_catalog_closure/status.md`
  - `docs/03_architecture/unity-agent-harness-contract.md`

## Preflight

- Edit Mode와 compile blocker를 먼저 확인한다.
- `SM.Content -> SM.Meta -> SM.Combat -> SM.Unity -> SM.Editor ->
  SM.Tests` 순서로 write scope를 유지한다.
- docs 작업은 `AGENTS.md -> docs/index.md -> 관련 index -> task status` 순서로만 확장한다.
- done criterion과 acceptance를 spec/status에 먼저 적고 들어간다.

## Phase 1 code-only

- profile authoring/runtime 타입 추가
- archetype/template/loadout carry-through
- slot reservation, separation, range band, reevaluation cadence, hit resolver 도입
- battle presentation code를 head anchor + screen-space presenter로 재구성
- localization controller/binder fallback guard 추가

## Phase 2 asset authoring

- sample content generator와 localization bootstrap을
  새 contract에 맞게 확장한다.
- 필요한 profile asset / string table entry / sandbox scenario
  evidence를 이 phase에서 정리한다.
- Play Mode에서 reserialize/asset authoring을 섞지 않는다.
- battle wrapper seam은 code-first로 닫고,
  prefab/catalog/sandbox asset materialize는 editor compile green 이후 bootstrap으로 복구한다.

## Phase 3 validation

- content/localization validator 실행
- targeted EditMode tests 실행
- battle harness compile/bootstrap/report/console smoke 실행
- docs policy, docs check, smoke check를 실행하고 evidence를
  status에 기록한다.

## rollback / escape hatch

- compile-fix 같은 종류의 반복이 2회를 넘으면 loop를 멈추고
  blocker를 status에 남긴다.
- asset authoring이 Play Mode/refresh timing과 충돌하면
  code-only phase와 asset phase를 다시 분리한다.
- docs-check가 기존 repo-wide debt로 red면 신규 debt와 기존 debt를
  분리 기록하고 task 자체 blocker로 과대해석하지 않는다.

## tool usage plan

- file-first로 코드/문서를 수정한다.
- Unity 확인은 `pwsh -File tools/unity-bridge.ps1 <verb>`를 먼저
  사용한다.
- scene/prefab 구조를 직접 고칠 필요가 생기기 전까지 MCP는 쓰지 않는다.
- edit는 `apply_patch`, 검증은 PowerShell/Unity bridge, 탐색은 `rg` 중심으로 유지한다.

## loop budget

- compile-fix 허용 횟수: 2
- refresh/read-console 반복 허용 횟수: 1
- blind asset generation 재시도 허용 횟수: 1
