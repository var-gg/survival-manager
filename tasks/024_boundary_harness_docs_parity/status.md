# boundary harness docs parity 상태

## 메타데이터

- 작업명: boundary harness docs parity
- 담당: Codex
- 상태: completed
- 최종수정일: 2026-04-19

## Current state

- `BuildCompileAuditTests`는 class-level `BatchOnly`였고, pure boundary guard 3개가 기본 `test-batch-fast`에 포함되지 않았다.
- `AGENTS.md`와 `docs/TESTING.md`에는 stale FastUnit count/time 문구가 남아 있었다.
- `tasks/023_session_service_object_extraction/status.md`에는 `GameSessionState.cs` 1,894줄과 2,048줄이 함께 남아 있었다.
- `TownBuildHotPathTests.cs`는 `Assets/Tests/EditMode/TownBuildHotPathTests.cs`에 존재한다. Pro 리포트의 확인 필요 항목은 resolved: exists.

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 |
| --- | --- | --- | --- |
| fast boundary guard | pure boundary guard가 FastUnit에서 실행 | 완료 | `test-batch-fast`, focused `BuildBoundaryGuardFastTests` |
| lint | unguarded editor API와 direct lookup 감지 | 완료 | `test-harness-lint` |
| docs parity | stale count/test asmdef/session status 정리 | 완료 | docs-policy/docs-check/smoke-check |
| BatchOnly audit | asset-loading audit 유지 | 완료 | focused `BuildCompileAuditTests` |

## Evidence

- `Assets/Tests/EditMode/TownBuildHotPathTests.cs` 존재 확인.
- 실제 test asmdef는 `SM.Tests.EditMode`, `SM.Tests.EditMode.Integration`, `SM.Tests.PlayMode` 3종이다.
- `pwsh -File tools/test-harness-lint.ps1 -RepoRoot .`: pass.
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.BuildBoundaryGuardFastTests`: 3 total / 3 passed / 0 failed.
- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`: 138 total / 135 passed / 3 skipped / 0 failed.
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.BuildCompileAuditTests`: 7 total / 7 passed / 0 failed.
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`: pass.
- `pwsh -File tools/docs-check.ps1 -RepoRoot .`: pass, markdownlint 352 files / 0 errors.
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`: pass.
- Unity batch test가 `Assets/_Game/Prefabs/Battle/Actors/BattleActor_PrimitiveWrapper.prefab`를 자동 touch했으나 024 범위 밖 변경으로 복구했다.

## Remaining blockers

- 없음.

## Deferred / debug-only

- `GameSessionState` ownership migration phase 2.

## Loop budget consumed

- compile/test retry: 0/2
- lint false positive fix: 0/2
- docs-check retry: 0/1

## Handoff notes

- 이번 task는 하네스 hardening과 문서 parity 전용이다.
