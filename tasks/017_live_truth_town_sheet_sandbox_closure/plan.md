# Task 017 Plan

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-08
- 소스오브트루스: `tasks/017_live_truth_town_sheet_sandbox_closure/plan.md`

## Preflight

- `AGENTS.md` / `docs/index.md` / `docs/02_design/index.md` / `docs/03_architecture/index.md`를 먼저 확인한다
- Town screen은 단일 `SelectedHero` 요약 문자열 구조이고, sandbox는 provenance/metrics까지만 보여 준다는 점을 확인한다
- runtime truth는 계속 committed asset + `FirstPlayableSlice`가 맡고, editor-only drift surface만 추가한다

## Phase 1 code-only

- Town view-state를 `TownCharacterSheetViewState` 기반 5-panel 구조로 교체한다
- `TownCharacterSheetFormatter`를 추가하고 presenter는 formatter 조립만 맡긴다
- `ContentTextResolver`, `ICombatContentLookup`, `RuntimeCombatContentLookup`, `FakeCombatContentLookup`에 tactic/synergy lookup을 추가한다
- `CombatSandboxLaunchTruthDiffService`를 추가하고 `CombatSandboxState` / `CombatSandboxWindow`에 preview/result summary를 붙인다
- starter scenario tag sync를 `CombatSandboxAuthoringAssetUtility`에서 덮어쓴다

## Phase 2 doc/task

- launch-core roster sheet 문서를 추가한다
- Town 5-panel UI 문서와 architecture contract 문서를 추가한다
- 관련 index와 `editor-sandbox-tooling.md`를 갱신한다
- task `implement.md`, `status.md`에 evidence와 blocker를 남긴다

## Phase 3 validation

- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
- `pwsh -File tools/docs-check.ps1 -RepoRoot .`
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`
- editor lock이 열려 있지 않으면 수동 smoke는 blocker로 남긴다

## rollback / escape hatch

- Town UI compile break가 길어지면 `TownCharacterSheetFormatter`와 `TownScreenViewState`만 먼저 복구 가능한 최소 단위로 본다
- sandbox drift surface가 noisy하면 result-only가 아니라 preview-only로 줄이지 않고, category rule 자체를 단순화한다
- 이번 task에서 runtime compiled default item/passive package bake-in까지 밀리지 않는다

## tool usage plan

- file-first로 현재 UXML/view-state/presenter/sandbox window를 읽는다
- manual edit는 `apply_patch`만 사용한다
- validation은 `unity-bridge.ps1`와 docs/smoke script를 우선 사용한다

## loop budget

- compile-fix: 3회
- refresh/read-console: 3회
- asset authoring retry: 1회
