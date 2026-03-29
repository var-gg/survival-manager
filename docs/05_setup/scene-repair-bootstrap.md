# Scene Repair Bootstrap

- 상태: active
- 최종수정일: 2026-03-29
- phase: prototype

## 목적

이 문서는 first playable scene asset을 **코드로 재생성/복구**하는 공식 진입점을 정리한다.
block25부터 scene repair는 temporary bridge가 아니라 운영 가능한 bootstrap 계층으로 취급한다.

## 공식 메뉴

- `SM/Bootstrap/Repair First Playable Scenes`
- `SM/Bootstrap/Prepare Observer Playable`

## 메뉴 역할

### `SM/Bootstrap/Repair First Playable Scenes`

- `Boot`, `Town`, `Expedition`, `Battle`, `Reward` 씬을 순서대로 열고 저장한다.
- 각 씬에 필요한 `Camera`, `Canvas`, `EventSystem`, controller root, 텍스트, 버튼을 생성 또는 복구한다.
- `ProjectSettings/EditorBuildSettings.asset`의 scene 순서를 `Boot -> Town -> Expedition -> Battle -> Reward`로 보정한다.
- 반복 실행 시 중복 생성보다 repair/update를 우선한다.

### `SM/Bootstrap/Prepare Observer Playable`

- canonical sample content를 보장한다.
- content validation을 실행한다.
- scene repair를 수행한다.
- 로컬 demo save를 초기화한다.
- 마지막에 `Boot.unity`를 열어 바로 Play 가능한 상태로 둔다.

## 권장 절차

1. Unity 열기
2. `SM/Bootstrap/Prepare Observer Playable`
3. `Boot.unity`가 열린 상태인지 확인
4. Play

## scene repair 기준

- `Boot`: `GameBootstrap`, `Main Camera`, `BootCanvas`, 최소 안내 텍스트
- `Town`: `TownCanvas`, `EventSystem`, `TownScreenController`, recruit/save/load/debug start 버튼
- `Expedition`: `ExpeditionCanvas`, `EventSystem`, `ExpeditionScreenController`, next battle / return town 버튼
- `Battle`: `BattleCanvas`, `EventSystem`, `BattleScreenController`, speed / continue 버튼
- `Reward`: `RewardCanvas`, `EventSystem`, `RewardScreenController`, 3개 선택 버튼 + 귀환 버튼

## 운영 메모

- canonical sample content가 손상되었으면 `SM/Seed/Generate Sample Content`를 다시 실행한다.
- scene repair는 repo에 저장되는 scene asset을 직접 갱신한다.
- observer-grade replay와 battle visual feedback 강화는 다음 블록 범위다.
