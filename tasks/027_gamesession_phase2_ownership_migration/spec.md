# GameSessionState phase 2 ownership migration spec

## Goal

`GameSessionState` 본 파일에 남은 private `*Core` 본문을 `SM.Unity.Session` internal service object 쪽으로 옮겨, public facade는 유지하면서 session hot spot을 줄인다.

## Authoritative boundary

- `GameSessionState` public constructor와 public facade method 이름은 변경하지 않는다.
- 새 asmdef, 새 public interface, 새 public abstraction은 만들지 않는다.
- service object는 `Assets/_Game/Scripts/Runtime/Unity/Session/**` 내부 `internal sealed` 타입으로 유지한다.
- `SM.Meta` pure boundary와 `ICombatContentLookup` 위치는 변경하지 않는다.
- scene, prefab, Resources asset authoring은 하지 않는다.

## In scope

- profile sync, expedition flow, reward settlement flow 중 public surface를 유지하며 안전하게 옮길 수 있는 `*Core` body ownership migration.
- `GameSessionState.cs` 본 파일 line count와 remaining `*Core` inventory 축소.
- task status에 moved method map과 residual inventory 기록.
- focused session tests와 fast boundary guard 재검증.
- `Assets/_Game/Scripts/Runtime/Unity/Session/**`에서 asset/editor loading choke point가 퍼지지 않도록 lightweight guard 추가.

## Out of scope

- UI/controller callsite migration.
- `GameSessionState` public facade rename.
- recruitment/deployment 전체 재작성.
- content authoring, scene repair, prefab mutation.
- session ownership policy의 대형 재설계.
- `BatchOnly` scene integrity backlog 수리.

## asmdef impact

없음. 변경은 기존 `SM.Unity`와 `SM.Tests.EditMode` 안에서 닫는다.

## persistence impact

save contract 변경 없음. profile/run/reward record shape는 그대로 유지하고, 호출 위치만 service object로 이동한다.

## validator / test oracle

- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.GameSessionStateTests`
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.RunLoopContractFastTests`
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.TownBuildHotPathTests`
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.MetaRewardPickTests`
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.BuildBoundaryGuardFastTests`
- `pwsh -File tools/test-harness-lint.ps1 -RepoRoot .`
- docs policy/check/smoke

## done definition

- `GameSessionState.cs`의 remaining `*Core` method 수가 줄어든다.
- service object가 단순 `_session.*Core(...)` 위임만 하는 상태에서 벗어나 최소 profile/expedition/reward 흐름의 실제 상태전이를 소유한다.
- public surface와 focused tests가 유지된다.
- session service object의 asset/editor loading 회귀와 delegation regrowth를 fast boundary guard가 감시한다.
- task status에 moved map, residual risk, deferred 항목이 남는다.

## deferred

- deployment/recruitment 흐름의 전체 ownership migration.
- `TryResolveCurrentEncounterCore` ownership migration.
- `030`의 broader editor-free semantic guard.
- 기존 `BatchOnly` scene integrity / LoopB backlog 수리.
