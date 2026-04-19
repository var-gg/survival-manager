# GameSessionState fast-lane narrative bootstrap decouple plan

## Preflight

- `git status --short --branch`로 clean main 확인.
- direct `new GameSessionState(...)` 사용처와 category를 grep한다.
- `GameSessionState` constructor, `NarrativeRuntimeBootstrap`, fast guard, lint 정책을 확인한다.

## Phase 1 code-only

- `NarrativeRuntimeBootstrap.CreateEmpty()`를 추가한다.
- `GameSessionState` public constructor는 `LoadFromResources()`를 호출하는 forwarding constructor로 유지한다.
- 새 internal constructor가 `ICombatContentLookup`과 `NarrativeRuntimeBootstrap`를 받아 field/service/director 초기화를 수행한다.
- `Assets/Tests/EditMode/Fakes/GameSessionTestFactory.cs`를 추가해 empty narrative bootstrap을 fast tests에 제공한다.
- FastUnit direct constructor callsite를 factory로 교체한다.

## Phase 2 asset authoring

- 없음. 이번 task는 code, tests, tools, docs만 수정한다.

## Phase 3 validation

- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`
- `pwsh -File tools/test-harness-lint.ps1 -RepoRoot .`
- focused boundary/session tests:
  - `BuildBoundaryGuardFastTests`
  - `RunLoopContractFastTests`
  - `TownBuildHotPathTests`
  - `GameSessionStateTests`
- 문서 검증:
  - `docs-policy-check`
  - `docs-check`
  - `smoke-check`

## Rollback / escape hatch

- factory 적용 중 FastUnit behavior가 바뀌면 해당 test의 expected narrative behavior 여부를 먼저 확인한다.
- production public constructor 변경이 필요해지면 중지한다.
- asset authoring이나 scene/prefab edit가 필요해지면 task 범위 밖으로 분리한다.

## Tool usage plan

- 수동 편집은 `apply_patch`로 수행한다.
- Unity batch test 후 prefab/asset auto-touch가 있으면 025 범위 밖 변경인지 확인하고 복구한다.
- 두 Unity batch command는 동시에 실행하지 않는다.

## Loop budget

- compile/test retry: 2회
- lint false positive fix: 2회
- docs-check retry: 1회
