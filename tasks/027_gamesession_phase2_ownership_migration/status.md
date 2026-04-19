# GameSessionState phase 2 ownership migration status

## 메타데이터

- 작업명: GameSessionState phase 2 ownership migration
- 담당: Codex
- 상태: completed
- 최종수정일: 2026-04-19

## Current state

- 시작 기준 `main`은 commit `515f759` 이후 clean 상태였다.
- 023은 delegating extraction을 완료했지만 ownership migration은 deferred로 남겼다.
- 026 이후 `GameSessionState.cs`는 2,082줄이며, FastUnit editor-free boundary는 닫혀 있다.
- 027 완료 후 `GameSessionState.cs`는 1,386줄이다.
- `Assets/_Game/Scripts/Runtime/Unity/Session/*.cs`의 `_session.*Core(...)` 위임은 29개로 줄었고, guard budget을 추가했다.

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 |
| --- | --- | --- | --- |
| public surface | public constructor/facade method 이름 유지 | 완료 | focused tests |
| ownership migration | profile/expedition/reward 흐름 일부가 service object body로 이동 | 완료 | moved map |
| line budget | `GameSessionState.cs` 2,500줄 이하 유지 및 감소 | 완료 | 2,082 -> 1,386줄 |
| boundary | 새 asmdef/public abstraction/asset authoring 없음 | 완료 | git diff / status |
| guard | session envelope asset loading / delegation regrowth guard | 완료 | `BuildBoundaryGuardFastTests` |
| tests | fast/focused/lint/docs pass | 완료 | evidence |

## Moved map

- `SessionProfileSync`: `AdvanceNarrative`, `TryDequeueNarrativePresentation`, `ResetNarrativeRunScopedProgress`, `SetCurrentScene`, `CanManualProfileReload`, `SaveDebugSnapshot`, `ClearRuntimeTelemetry`, `RecordOperationalTelemetry`.
- `SessionExpeditionFlow`: `BeginNewExpedition`, `PrepareQuickBattleSmoke` overloads, `PrepareCombatSandboxDirect`, `PrepareTownQuickBattleSmoke`, `RestartQuickBattle`, `ExitCombatSandbox`, `TryCycleCampaignChapter`, `TryCycleCampaignSite`, `PrepareSelectedBattleNodeHandoff`, `ResolveSelectedNodeToRewardSettlement`, `ReloadCombatSandboxConfig`, `ReloadQuickBattleConfig`, `AdvanceExpeditionNode`, `SelectNextExpeditionNode`, `GetCurrentExpeditionNode`, `GetSelectedExpeditionNode`, `GetSelectableNextNodeIndices`, `ResolveSelectedExpeditionNode`, `AbandonExpeditionRun`, `ReturnToTownAfterReward`.
- `SessionRewardSettlementFlow`: `RecordBattleAudit`, `SetLastBattleResult`, `MarkBattleResolved`, `ApplyRewardChoice`, `PreviewPermanentUnlockFromTemporaryAugment`.

## Residual inventory

- `BindProfileCore`는 profile bootstrap과 persistence sync touch point가 커서 이번 task에 남겼다.
- deployment/equipment/passive/deploy snapshot 계열 `*Core`는 `SessionDeploymentFlow`에 남았다.
- recruitment/scout/retrain/dismiss/direct grant 계열 `*Core`는 `SessionRecruitmentFlow`에 남았다.
- `TryResolveCurrentEncounterCore`는 encounter content resolution과 debug fallback path가 섞여 있어 이번 task에 남겼다.

## Evidence

- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`: 108 total / 105 passed / 3 skipped / 0 failed.
- `pwsh -File tools/test-harness-lint.ps1 -RepoRoot .`: pass.
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.BuildBoundaryGuardFastTests`: 7 passed / 0 failed.
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.GameSessionStateTests`: 6 passed / 0 failed.
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.RunLoopContractFastTests`: 5 passed / 0 failed.
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.TownBuildHotPathTests`: 11 passed / 0 failed.
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.MetaRewardPickTests`: 1 passed / 0 failed.
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`: pass.
- targeted `pwsh -File tools/docs-check.ps1 -RepoRoot . -LinkCheckTimeoutSeconds 30 ...`: 5 targeted files, 0 markdownlint errors, link check pass.
- full `pwsh -File tools/docs-check.ps1 -RepoRoot . -LinkCheckTimeoutSeconds 10`: 364 files, 0 markdownlint errors, link check pass.
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`: pass.
- Unity batch runs repeatedly auto-touched `BattleActor_PrimitiveWrapper.prefab`; restored because asset authoring is out of scope.

## Remaining blockers

- 없음.

## Deferred / debug-only

- deployment/recruitment 전체 migration.
- `TryResolveCurrentEncounterCore` ownership migration.
- broader `030` semantic guard, if fixture/session policy changes again.
- 기존 `BatchOnly` scene integrity / LoopB backlog 수리.

## Loop budget consumed

- compile/test retry: 0/2
- behavior regression fix: 0/2
- docs-check retry: 1/1

## Handoff notes

- 이번 task는 `GameSessionState` public surface를 바꾸지 않는 phase 2 ownership migration 전용이다.
- scene/prefab/Resources asset authoring은 하지 않는다.
- 다음 code-only 후보는 deployment/recruitment ownership migration 또는 encounter resolution split이다.
