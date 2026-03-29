# First Playable Review

## 반드시 먼저 실행할 메뉴 1개

- `SM/Bootstrap/Prepare Observer Playable`

- 상태: active
- 최종수정일: 2026-03-29
- phase: prototype

## 실제 플레이 가능한 범위

- `Boot -> Town -> Expedition -> Battle -> Reward -> Town`
- Town `Quick Battle`로 `Town -> Battle -> Reward -> Town` smoke 가능
- Battle은 replay track 기반 observer UI로 5~10초 동안 행동을 따라갈 수 있다

## 지금 바로 보이는 화면

- Town: roster / recruit 카드 / squad / deploy preview / operator 버튼
- Expedition: 5노드 박스 / 현재 위치 / 예정 보상 / next battle / return town
- Battle: primitive actor / HP label / 로그 / speed / pause / progress
- Reward: 3지선다 카드 / 상태 반영 요약

## 아직 placeholder인 부분

- Town / Expedition / Reward는 여전히 operator-grade debug UGUI다
- Battle camera / motion / HUD는 MVP용 observer 표현이다
- Expedition branching 선택과 장기 progression 연출은 다음 단계다
