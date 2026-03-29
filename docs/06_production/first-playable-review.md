# First Playable Review

## 반드시 먼저 실행할 메뉴 1개

- `SM/Bootstrap/Prepare Observer Playable`

- 상태: active
- 최종수정일: 2026-03-29
- phase: prototype

## 실제 플레이 가능한 범위

현재 솔직하게 playable이라고 부를 수 있는 범위는 아래다.

- `Boot -> Town -> Expedition -> Battle -> Reward -> Town`
- `SM/Bootstrap/Prepare Observer Playable` 1회 실행으로 scene asset repair와 build settings 보정 가능
- Town에서 recruit / reroll / save / load / debug start 동작
- Expedition에서 next battle / return town 동작
- Battle에서 자동 전투 재생 / 속도 변경 / continue 동작
- Reward에서 3지선다 선택 / 저장 / Town 귀환 동작

## 아직 placeholder인 부분

- Town UI는 debug placeholder UGUI
- Expedition 맵은 텍스트 placeholder
- Battle은 primitive actor + 로그 표시
- Reward는 debug 3지선다 카드
- 최종 HUD / VFX / 정식 UX 아님

## 자동 검증 범위

- EditMode: scene integrity + sample content root 검사
- PlayMode: Boot -> Town 자동 진입 + first playable 1회전 smoke

## 수동 검증 범위

- UI 가독성
- 버튼 체감 동선
- Battle 재생 감각
- Reward 선택 후 Town 복귀 체감
- placeholder UI의 실제 사용성

## operator quick path

1. Unity 열기
2. `SM/Bootstrap/Prepare Observer Playable`
3. `Boot.unity` Play
4. Town에서 `Debug Start`
5. Expedition에서 `Next Battle`
