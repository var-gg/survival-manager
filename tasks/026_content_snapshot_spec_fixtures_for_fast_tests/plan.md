# FastUnit editor-free boundary closure plan

## Preflight

- `git status --short --branch`로 기준 상태를 확인한다.
- FastUnit 위반 후보를 grep한다.
- `RunLoopContractFastTests`, `TownBuildHotPathTests`, `ICombatContentLookup`, `SessionExpeditionFlow`, `SessionProfileSync`의 current dependency path를 확인한다.

## Phase 1 code-only

- `EditorFreeCombatContentFixture`를 추가해 session tests가 pure snapshot/spec fixture를 공유하게 한다.
- campaign/site ordering과 passive board existence는 snapshot/template을 먼저 보도록 runtime private path를 좁게 보강한다.
- `RunLoopContractFastTests`와 `TownBuildHotPathTests`에서 authored `ScriptableObject` fixture를 제거한다.
- `CombatSandboxConfig` fixture 테스트는 `TownBuildHotPathBatchOnlyTests`로 분리한다.
- authored Unity object가 테스트 대상인 기존 FastUnit class는 `BatchOnly`로 재분류한다.
- `MetaRewardPickTests`는 pure reward test로 `FastUnit` category를 명시한다.

## Phase 2 asset authoring

없음. prefab, scene, Resources content asset은 수정하지 않는다. Unity batch run이 prefab을 자동 touch하면 task 범위 밖 side effect로 복구한다.

## Phase 3 validation

- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`
- `pwsh -File tools/test-harness-lint.ps1 -RepoRoot .`
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.BuildBoundaryGuardFastTests`
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.RunLoopContractFastTests`
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.TownBuildHotPathTests`
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.MetaRewardPickTests`
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestCategory BatchOnly`
- docs policy/check/smoke

## rollback / escape hatch

- FastUnit에서 pure fixture로 표현 불가능한 테스트는 production API를 늘리지 않고 `BatchOnly`로 격리한다.
- BatchOnly 전체가 과도하게 느려지면 focused BatchOnly filter를 status에 남기고 full category run은 후속 task로 넘긴다.

## tool usage plan

- 파일 수정은 `apply_patch`를 사용한다.
- Unity batch 명령은 순차 실행한다.
- 문서 검증은 docs policy/check/smoke 순서로 실행한다.

## loop budget

- compile/test retry: 2회
- lint false positive fix: 2회
- docs-check retry: 1회
