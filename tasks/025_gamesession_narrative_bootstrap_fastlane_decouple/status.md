# GameSessionState fast-lane narrative bootstrap decouple status

## 메타데이터

- 작업명: GameSessionState fast-lane narrative bootstrap decouple
- 담당: Codex
- 상태: completed
- 최종수정일: 2026-04-19

## Historical snapshot / current-state implications

- 이 문서는 025 완료 시점의 historical snapshot이다.
- 현재 source-of-truth는 `AGENTS.md`, `docs/TESTING.md`, `docs/03_architecture/index.md`의 closure scope를 우선한다.
- 025의 closure는 FastUnit session construction에서 hidden narrative `Resources` bootstrap을 제거했다는 뜻이며, `SM.Unity` production bootstrap이나 repo 전체가 pure/editor-free로 닫혔다는 뜻이 아니다.

## Current state

- `GameSessionState(ICombatContentLookup)` public constructor가 `NarrativeRuntimeBootstrap.LoadFromResources()`를 직접 호출한다.
- 여러 `FastUnit` 테스트가 `FakeCombatContentLookup`을 쓰면서도 direct `new GameSessionState(...)`로 hidden narrative `Resources.LoadAll` 경로를 간접적으로 밟는다.
- `SM.Unity`는 이미 `InternalsVisibleTo("SM.Tests.EditMode")`를 가지고 있어 internal test-only constructor 주입이 가능하다.

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 |
| --- | --- | --- | --- |
| production constructor | public signature와 resource-backed behavior 유지 | 완료 | focused `GameSessionStateTests` |
| fast session construction | FastUnit은 empty narrative bootstrap 주입 | 완료 | direct constructor grep / `test-batch-fast` |
| guard | non-`BatchOnly` direct `new GameSessionState(...)` 차단 | 완료 | lint / fast boundary guard |
| docs parity | FastUnit semantics와 Integration lane 설명 정리 | 완료 | docs-policy/docs-check/smoke-check |

## Evidence

- preflight direct constructor grep 기준 FastUnit direct callsite는 10개다.
- `NarrativeRuntimeBootstrap.LoadFromResources()`는 `StoryEventDefinition`, `DialogueSequenceDefinition`, `HeroLoreDefinition`를 `Resources.LoadAll`로 로드한다.
- FastUnit direct callsite 10개를 `GameSessionTestFactory.Create(...)`로 전환했다.
- `pwsh -File tools/test-harness-lint.ps1 -RepoRoot .`: pass.
- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`: 139 total / 136 passed / 3 skipped / 0 failed.
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.BuildBoundaryGuardFastTests`: 4 total / 4 passed / 0 failed.
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.RunLoopContractFastTests`: 5 total / 5 passed / 0 failed.
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.TownBuildHotPathTests`: 12 total / 12 passed / 0 failed.
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.GameSessionStateTests`: 6 total / 6 passed / 0 failed.
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`: pass.
- `pwsh -File tools/docs-check.ps1 -RepoRoot .`: pass, markdownlint 356 files / 0 errors.
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`: pass.
- Unity batch test가 `Assets/_Game/Prefabs/Battle/Actors/BattleActor_PrimitiveWrapper.prefab`를 자동 touch했으나 025 범위 밖 변경으로 복구했다.

## Remaining blockers

- 없음.

## Deferred / debug-only

- `026_content_snapshot_spec_fixtures_for_fast_tests`
- `027_gamesession_phase2_ownership_migration`

## Loop budget consumed

- compile/test retry: 0/2
- lint false positive fix: 0/2
- docs-check retry: 0/1

## Handoff notes

- 이번 task는 hidden narrative bootstrap decouple 전용이다.
- `ScriptableObject.CreateInstance` 기반 in-memory fixture가 FastUnit에 남는 문제는 026에서 다룬다.
