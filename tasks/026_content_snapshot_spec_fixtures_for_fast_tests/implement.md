# FastUnit editor-free boundary closure implementation

## Phase 1 summary

- `EditorFreeCombatContentFixture`를 추가해 run loop와 town build FastUnit이 pure `CombatContentSnapshot`/`FirstPlayableSliceDefinition` fixture를 사용하도록 했다.
- campaign chapter/site ordering과 passive board existence 경로는 snapshot/template을 우선 사용하도록 `SM.Unity` 내부 private path를 보강했다.
- `RunLoopContractFastTests`와 `TownBuildHotPathTests`에서 authored `ScriptableObject` fixture를 제거했다.
- `CombatSandboxConfig` coverage는 `TownBuildHotPathBatchOnlyTests`로 이동했다.
- authored Unity object를 직접 생성하던 presentation/content formatter/sandbox lookup 계열 FastUnit은 `BatchOnly`로 재분류했다.
- `MetaRewardPickTests`는 `FastUnit`으로 명시 분류하고 불필요한 authored content using을 제거했다.

## Phase 2 summary

Asset authoring 없음. `test-batch-fast` 실행 중 Unity가 `BattleActor_PrimitiveWrapper.prefab`를 자동 touch했으나 task 범위 밖 side effect로 복구했다.

## Phase 3 summary

검증은 `status.md`의 evidence에 누적한다.

## deviation

- 원래 026 후보는 두 session FastUnit 중심이었지만, 사용자 요청에 따라 FastUnit 전체 closure로 범위를 확장했다.
- authored object 테스트를 모두 pure fixture로 치환하지 않고, authored Unity object 자체가 테스트 대상인 경우 `BatchOnly`로 격리했다.

## blockers

026 scope blocker는 없다.

`BatchOnly` 전체 category run은 기존 범위 밖 실패가 남아 green이 아니다. 026에서 `BatchOnly`로 이동하거나 재분류한 authored-object coverage는 통과했지만, 기존 `LoopBContractClosureTests` 1건과 `SceneIntegrityTests` 24건이 실패한다. Scene repair가 자동으로 건드린 scene/localization/panel/prefab asset 변경은 모두 복구했다.

## diagnostics

- `FastUnit` 위반 token grep은 빈 결과를 기대한다.
- `test-batch-fast`의 테스트 수 감소는 coverage 삭제가 아니라 authored-object coverage의 `BatchOnly` 이동으로 해석한다.

## why this loop happened

025는 hidden narrative `Resources` bootstrap을 제거했지만, 기존 FastUnit 안에는 in-memory authored `ScriptableObject` fixture가 남아 있었다. 이번 loop는 category 의미를 실제 fixture dependency와 맞추기 위해 필요했다.
