# GameSessionState phase 2 ownership migration implementation

## Phase 1 summary

- `SessionProfileSync`가 narrative/current scene/debug telemetry의 실제 body를 소유하도록 옮겼다.
- `SessionExpeditionFlow`가 expedition start, quick battle/sandbox, campaign/site cycling, node selection/resolution, return/abandon 흐름의 실제 body를 소유하도록 옮겼다.
- `SessionRewardSettlementFlow`가 battle audit, last battle result, reward settlement, reward choice apply/preview 흐름의 실제 body를 소유하도록 옮겼다.
- `GameSessionState.cs`는 2,082줄에서 1,386줄로 감소했다.
- 남은 `_session.*Core(...)` service delegation은 29개이며, deployment/recruitment/encounter/bootstrap residual로 제한된다.

## Phase 2 summary

- Asset authoring 없음.
- Unity batch run이 `Assets/_Game/Prefabs/Battle/Actors/BattleActor_PrimitiveWrapper.prefab`을 자동 touch했지만 task 범위 밖 side effect라서 restore했다.
- 새 asmdef, 새 public abstraction, public facade rename 없음.

## Phase 3 summary

- `test-batch-fast` pass.
- `test-harness-lint` pass.
- focused `BuildBoundaryGuardFastTests`, `GameSessionStateTests`, `RunLoopContractFastTests`, `TownBuildHotPathTests`, `MetaRewardPickTests` pass.
- 문서 검증 결과는 `status.md`에 누적한다.

## deviation

- `BuildBoundaryGuardFastTests`에 session envelope guard를 추가했다. 이는 027 residual regrowth를 막는 가벼운 guard이며 새 asmdef나 runtime API를 만들지 않는다.

## blockers

- 없음.

## diagnostics

- remaining `_session.*Core(...)` inventory: 29.
- `GameSessionState.cs` line count: 1,386.
- session asset-loading token scan: `Session/*.cs`에서 `Resources.Load`, `AssetDatabase`, `ScriptableObject`, `RuntimeCombatContentLookup`, `NarrativeRuntimeBootstrap.LoadFromResources`, `UnityEditor` 직접 소유 없음.
- focused session test results: `status.md` 참조.

## why this loop happened

023은 behavior-preserving extraction으로 service object를 만들었지만, 실제 상태전이 body 상당수가 `GameSessionState`의 private `*Core` method로 남았다. 027은 public facade를 유지하면서 service object가 실제 session flow ownership을 갖도록 이동하는 단계다.
