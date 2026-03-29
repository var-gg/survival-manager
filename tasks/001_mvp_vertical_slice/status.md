# Task Status: 001 MVP Vertical Slice

## 반드시 먼저 실행할 메뉴 1개

- `SM/Bootstrap/Prepare Observer Playable`

- Status: in-progress
- Last Updated: 2026-03-29
- Phase: prototype
- Task ID: 001

## 현재 상태

- canonical sample content root 고정 완료
- scene repair/bootstrap 계층 운영 가능
- Boot / Town / Battle live controller binding은 scene load 시 자동 보정됨
- Battle replay / presentation observer layer 추가 완료
- Town / Expedition / Reward operator UI 추가 완료
- Quick Battle smoke 경로 추가 완료

## 지금 바로 보이는 화면

- Town operator UI
- Expedition 5노드 박스 UI
- Battle observer replay UI
- Reward 3카드 UI

## 아직 남은 이슈

- Battle 연출은 observer-grade이며 polish 단계가 아니다
- Expedition branching 선택은 다음 단계다
- operator UI는 placeholder UGUI 품질이다
- Town / Battle controller는 아직 scene asset 영속 저장보다 live rebind 경로가 더 안정적이다

## 다음 추천 단계

1. Battle actor motion과 hit timing polish
2. Expedition branching / node choice 실구현
3. meta progression / reward variety 확장
