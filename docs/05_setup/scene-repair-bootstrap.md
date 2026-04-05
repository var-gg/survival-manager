# Scene Repair Bootstrap

- 상태: active
- 최종수정일: 2026-04-06
- 단계: prototype

## 공식 메뉴

- `SM/Bootstrap/Repair First Playable Scenes`
- `SM/Bootstrap/Prepare Observer Playable`

## 현재 repair 범위

- `Boot`: `GameBootstrap`, `Main Camera`, `BootCanvas`
- `Town`: `TownRuntimeRoot`, `TownRuntimePanelHost`, `TownScreenController`, `EventSystem`
- `Expedition`: `ExpeditionRuntimeRoot`, `ExpeditionRuntimePanelHost`, `ExpeditionScreenController`, `EventSystem`
- `Battle`: `BattleRuntimeRoot`, `BattleRuntimePanelHost`, `BattleScreenController`, `BattlePresentationRoot`, `BattleStageRoot`, `ActorOverlayCanvas`, `ActorOverlayRoot`, `BattleCameraRoot`, `EventSystem`
- `Reward`: `RewardRuntimeRoot`, `RewardRuntimePanelHost`, `RewardScreenController`, `EventSystem`

## 운영 절차

1. Unity 열기
2. `SM/Bootstrap/Prepare Observer Playable`
3. `Boot.unity` 자동 오픈 확인
4. Play

## 메모

- scene repair는 반복 실행 시 create보다 repair/update를 우선한다.
- scene scaffold와 runtime root 이름은 scene installer가 source of truth다.
- play scene 화면 tree는 scene YAML이 아니라 `Assets/_Game/UI/Screens/**`의 UXML asset이 source of truth다.
- `FirstPlayableRuntimeSceneBinder`는 runtime에서 `RuntimePanelHost`, `EventSystem`, battle overlay binding을 self-heal한다.
- 임시 panel hide/show는 host GameObject enable/disable가 아니라 runtime panel root의 `display` 토글을 기본으로 쓴다.
