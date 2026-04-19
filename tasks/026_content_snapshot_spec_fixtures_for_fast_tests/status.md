# FastUnit editor-free boundary closure status

## 메타데이터

- 작업명: FastUnit editor-free boundary closure
- 담당: Codex
- 상태: completed
- 최종수정일: 2026-04-19

## Current state

- 025 이후 session FastUnit은 public `GameSessionState` constructor를 피하지만, 여러 FastUnit class가 authored `ScriptableObject` fixture와 `SM.Content.Definitions`를 사용했다.
- 026은 FastUnit 의미를 editor-free/resource-free/authored-object-free로 강화한다.
- authored Unity object를 직접 검증하는 테스트는 `BatchOnly`로 이동 또는 재분류한다.

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 |
| --- | --- | --- | --- |
| FastUnit authored object ban | FastUnit에서 `ScriptableObject.CreateInstance`, `SM.Content.Definitions`, `DestroyImmediate` 금지 | 완료 | FastUnit token grep empty / guard |
| session pure fixture | RunLoop/TownBuild는 pure snapshot/spec fixture 사용 | 완료 | focused tests |
| coverage preservation | authored object coverage는 BatchOnly로 유지 | 완료 | reclassified BatchOnly tests pass in category run |
| guard | lint와 fast boundary guard가 회귀를 차단 | 완료 | lint / focused guard |
| docs parity | AGENTS/TESTING/dependency docs가 새 lane 의미 반영 | 완료 | docs checks |

## Evidence

- `pwsh -File tools/test-harness-lint.ps1 -RepoRoot .`: pass.
- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`: 108 total / 105 passed / 3 skipped / 0 failed.
- `FastUnit` forbidden-token scan: no `[Category("FastUnit")]` file contains `ScriptableObject.CreateInstance`, `using SM.Content.Definitions`, `UnityEngine.Object`, `DestroyImmediate`, `Resources.Load`, `RuntimeCombatContentLookup`, or public `new GameSessionState(...)`.
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.BuildBoundaryGuardFastTests`: 5 total / 5 passed / 0 failed.
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.RunLoopContractFastTests`: 5 total / 5 passed / 0 failed.
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.TownBuildHotPathTests`: 11 total / 11 passed / 0 failed.
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.MetaRewardPickTests`: 1 total / 1 passed / 0 failed.
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestCategory BatchOnly`: 81 total / 56 passed / 25 failed. 026에서 `BatchOnly`로 이동/재분류한 authored-object coverage는 통과했지만, 기존 `LoopBContractClosureTests` 1건과 `SceneIntegrityTests` 24건이 실패했다.
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`: pass.
- `pwsh -File tools/docs-check.ps1 -RepoRoot .`: pass.
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`: pass.

## Remaining blockers

- `BatchOnly` 전체 green은 이번 task scope 밖이다. 현재 실패는 `LoopBContractClosureTests.RetrainService_BlocksImmediateRecurrence_AndPityForcesPlanCoherentResult`의 `ArgumentOutOfRangeException` 1건과 `SceneIntegrityTests`의 Boot scene `GameBootstrap` component repair 실패 24건이다.
- `SceneIntegrityTests` 실행 중 Unity가 scene/localization/panel/prefab assets를 자동 touch했으나, 026의 scene/prefab/asset authoring 금지 범위에 맞춰 모두 복구했다.

## Deferred / debug-only

- `027_gamesession_phase2_ownership_migration`
- `030_editor_free_semantic_guard`

## Loop budget consumed

- compile/test retry: 0/2
- lint false positive fix: 0/2
- docs-check retry: 1/1. 첫 실행은 5분 timeout으로 중단했고, 단독 재실행은 pass.

## Handoff notes

- 이번 task는 FastUnit lane closure 전용이다.
- `GameSessionState` public surface와 `ICombatContentLookup` public interface는 유지한다.
