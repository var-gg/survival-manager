# 작업 계획

## 메타데이터

- 작업명: EditMode failure closure
- 담당: Codex
- 상태: active
- 최종수정일: 2026-04-01

## Preflight

- `AGENTS.md` -> `docs/index.md` -> 관련 index -> 직전 task `status.md` 순서로 범위를 확인한다.
- 현재 failing test 목록과 메시지를 다시 뽑아 전투 런타임 / validator 축으로 분리한다.
- unrelated dirty scene/localization 변경은 건드리지 않는다.

## Phase 1 code-only

- `BattleResolutionTests`, `BattleSimulationSpatialTests`, `CombatContractsTests`를 기준으로 전투 런타임 경로를 수선한다.
- 필요 시 `BattleSimulator`, `MovementResolver`, `TacticEvaluator`, `TargetScoringService`, `HitResolutionService`, 관련 model을 수정한다.
- 테스트 기대를 바꾸기보다, 최근 contract에 맞는 동작을 회복하는 쪽을 우선한다.

## Phase 2 asset authoring

- `ContentValidationWorkflowTests` failure가 canonical asset drift 또는 load path 문제면 최소 asset/seed 보정만 수행한다.
- sample content regenerate가 필요하면 `seed-content`로만 수행하고, read path implicit rewrite는 다시 열지 않는다.

## Phase 3 validation

- targeted filter로 실패 class/test를 개별 검증한다.
- 마지막에 full `pwsh -File tools/unity-bridge.ps1 test-edit`를 돌린다.
- compile와 docs/status sync를 함께 마감한다.

## rollback / escape hatch

- 전투 runtime 수정이 acceptance보다 광범위한 동작 변화를 만들면, broad refactor 대신 테스트를 깨는 회귀만 먼저 복구한다.
- canonical asset 보정이 과도한 reserialize를 유발하면, 최소 subset만 유지하고 나머지는 status에 blocker로 남긴다.

## tool usage plan

- 파일 탐색/읽기: `rg`, `Get-Content`
- Unity smoke/test: `pwsh -File tools/unity-bridge.ps1 <verb>` 우선
- targeted test 재현: direct `unity-cli test --mode EditMode --filter ...`
- 수동 편집: `apply_patch`

## loop budget

- targeted test loop: 6
- compile loop: 3
- seed-content / asset repair loop: 2
