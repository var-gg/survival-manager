# GameSessionState fast-lane narrative bootstrap decouple implement

## 메타데이터

- 작업명: GameSessionState fast-lane narrative bootstrap decouple
- 담당: Codex
- 상태: completed
- 최종수정일: 2026-04-19

## Phase summary

- Phase 0: direct `GameSessionState` constructor callsite와 category를 대조했다.
- Phase 1: `NarrativeRuntimeBootstrap.CreateEmpty()`와 `GameSessionState` internal injection constructor를 추가했다.
- Phase 2: FastUnit session tests를 `GameSessionTestFactory.Create(...)`로 전환했다.
- Phase 3: lint와 fast boundary guard가 non-`BatchOnly` direct public session construction을 차단하도록 확장했다.

## Diagnostics

- public `GameSessionState(ICombatContentLookup)` 생성자는 production resource-backed narrative bootstrap을 직접 수행한다.
- `FastUnit` session tests는 `FakeCombatContentLookup`으로 combat content asset load는 피하지만, session construction에서 narrative `Resources.LoadAll`을 간접 호출한다.

## Deviation

- 아직 없음.

## Blockers

- 아직 없음.

## Verification

- `pwsh -File tools/test-harness-lint.ps1 -RepoRoot .`: pass.
- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`: 139 total / 136 passed / 3 skipped / 0 failed.
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.BuildBoundaryGuardFastTests`: 4 total / 4 passed / 0 failed.
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.RunLoopContractFastTests`: 5 total / 5 passed / 0 failed.
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.TownBuildHotPathTests`: 12 total / 12 passed / 0 failed.
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.GameSessionStateTests`: 6 total / 6 passed / 0 failed.
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`: pass.
- `pwsh -File tools/docs-check.ps1 -RepoRoot .`: pass, markdownlint 356 files / 0 errors.
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`: pass.

## why this loop happened

- combat content lookup은 fake로 분리했지만 narrative bootstrap은 session constructor 내부 production path로 남아 있어 fast lane과 resource-backed path가 다시 섞였다.
