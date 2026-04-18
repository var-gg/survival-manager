# session service object extraction 계획

## 메타데이터

- 작업명: session service object extraction
- 담당: Codex
- 상태: in_progress
- 최종수정일: 2026-04-19

## Plan

1. 현재 `GameSessionState` public/internal flow entrypoint와 partial helper 배치를 계측한다.
2. `GameSessionState`에 five flow service fields를 추가하고 constructor에서 생성한다.
3. public/internal facade method는 기존 이름을 유지하되 service object로 위임한다.
4. 기존 method body는 `*Core` entrypoint로 보존해 behavioral migration 없이 1차 object extraction을 완료한다.
5. focused tests와 fast harness를 돌리고 source guard 결과를 기록한다.

## Test oracle

- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`
- focused:
  - `GameSessionStateTests`
  - `RunLoopContractFastTests`
  - `TownBuildHotPathTests`
  - `MetaRewardPickTests`
- source guard:
  - `GameSessionState.cs` line count `< 2500`
  - `Assets/_Game/Scripts/Runtime/Meta/**` forbidden using 없음

## Loop budget

- compile/test retry: 2
- service routing compile fix: 2
- docs/task status retry: 1
