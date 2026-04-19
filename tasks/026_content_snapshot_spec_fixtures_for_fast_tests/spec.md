# FastUnit editor-free boundary closure spec

## Goal

`FastUnit` 카테고리를 editor-free/resource-free/authored-object-free lane으로 닫는다. `FastUnit` 테스트는 pure snapshot/spec fixture만 사용하고, authored `ScriptableObject` fixture나 production content bootstrap은 `BatchOnly`로 격리한다.

## Authoritative boundary

- `FastUnit`: `ScriptableObject.CreateInstance`, `UnityEngine.Object` lifecycle, `DestroyImmediate`, `using SM.Content.Definitions`, `Resources.Load*`, `RuntimeCombatContentLookup`, public `new GameSessionState(...)` 금지.
- `BatchOnly`: authored Unity object, `RuntimeCombatContentLookup`, `Resources.LoadAll`, editor diagnostic fallback 검증 허용.
- `SM.Meta` pure boundary와 asmdef 참조 방향은 변경하지 않는다.

## In scope

- session FastUnit fixture를 pure `CombatContentSnapshot` / `FirstPlayableSliceDefinition` 기반으로 전환.
- authored object를 직접 테스트하는 기존 FastUnit을 `BatchOnly`로 재분류.
- `BuildBoundaryGuardFastTests`와 `tools/test-harness-lint.ps1`에 semantic guard 추가.
- `AGENTS.md`, `docs/TESTING.md`, `dependency-direction.md`의 lane 설명 갱신.

## Out of scope

- scene, prefab, Resources asset authoring.
- 새 asmdef 또는 새 public abstraction 추가.
- `ICombatContentLookup` public surface 변경.
- `GameSessionState` service ownership phase 2.

## asmdef impact

없음. `SM.Tests.EditMode` 안에서 test class category와 fixture만 조정한다.

## persistence impact

저장 계약 변경 없음. `RunLoopContractFastTests`의 reload 시나리오는 existing `JsonSaveRepository`를 사용해 editor-free persistence roundtrip을 유지한다.

## validator / test oracle

- `test-batch-fast`
- `tools/test-harness-lint.ps1`
- focused `BuildBoundaryGuardFastTests`
- focused `RunLoopContractFastTests`
- focused `TownBuildHotPathTests`
- focused `MetaRewardPickTests`
- `test-batch-edit -TestCategory BatchOnly`
- docs policy/check/smoke

## done definition

- `[Category("FastUnit")]` 파일에서 authored object / production content bootstrap token grep이 비어 있다.
- session FastUnit은 `EditorFreeCombatContentFixture`와 `GameSessionTestFactory`를 사용한다.
- authored object 테스트는 `BatchOnly`로 이동 또는 재분류되어 coverage가 사라지지 않는다.
- 문서와 task status가 실제 lane 의미와 테스트 결과를 반영한다.

## deferred

- `027_gamesession_phase2_ownership_migration`: `GameSessionState` remaining `*Core` ownership migration.
- `030_editor_free_semantic_guard`: 필요 시 session envelope budget과 더 넓은 semantic guard 추가.
