# Scene Repair Bootstrap

- 상태: active
- 최종수정일: 2026-03-29
- 단계: prototype

## 공식 메뉴

- `SM/Bootstrap/Repair First Playable Scenes`
- `SM/Bootstrap/Prepare Observer Playable`

## 현재 repair 범위

- `Boot`: `GameBootstrap`, `Main Camera`, `BootCanvas`
- `Town`: `TownCanvas`, `EventSystem`, `TownScreenController`, `RecruitCardsRoot`, `QuickBattleButton`
- `Expedition`: `ExpeditionCanvas`, `EventSystem`, `ExpeditionScreenController`, `NodeTrackRoot`
- `Battle`: `BattleCanvas`, `EventSystem`, `BattleScreenController`, `BattlePresentationRoot`, `BattleStageRoot`, `ActorOverlayRoot`, `PauseButton`, `ProgressFill`
- `Reward`: `RewardCanvas`, `EventSystem`, `RewardScreenController`, `RewardCardsRoot`

## 운영 절차

1. Unity 열기
2. `SM/Bootstrap/Prepare Observer Playable`
3. `Boot.unity` 자동 오픈 확인
4. Play

## 메모

- scene repair는 반복 실행 시 create보다 repair/update를 우선한다.
- scene scaffold와 UI object 이름은 scene installer가 source of truth다.
- `Town` / `Battle` custom controller는 현재 Unity scene serialization quirks 때문에 scene open/load 시 `FirstPlayableRuntimeSceneBinder`와 editor binder가 live rebind를 수행한다.
- Battle presentation root와 operator UI root 이름 계약은 scene asset에 저장된다.
