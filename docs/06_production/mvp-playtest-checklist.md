# MVP Playtest Checklist

- 상태: draft
- 최종수정일: 2026-03-29
- phase: prototype

## 첫 에디터 실행

1. Unity editor `6000.4.0f1`로 프로젝트를 연다.
2. 패키지 import/reload가 끝날 때까지 기다린다.
3. `SM/Seed/Generate Sample Content`를 실행한다.
4. `SM/Validation/Validate Content Definitions`를 실행한다.
5. `Assets/_Game/Scenes/Boot.unity`를 연다.
6. 프로젝트가 Play Mode에 들어갈 만큼 컴파일되는지 확인한다.
7. Play를 눌러 최소 Town까지 진입되는지 확인한다.

## 자동 검증 기준

### PlayMode
현재 PlayMode smoke는 최소 아래를 자동 검증 대상으로 둔다.

- Boot scene load
- Boot 진입 후 `GameSessionRoot` 존재
- 가능 시 Boot -> Town 자동 진입

Battle/Reward 전체 자동화 대신, 우선 `Boot -> Town` 자동 검증을 확정한다.

## 수동 검증 기준

### Town 확인 항목

- 로스터 목록이 보인다.
- recruit 후보 3개가 보인다.
- recruit 버튼이 동작한다.
- reroll 버튼이 동작한다.
- expedition squad 정보가 보인다.
- battle deploy preview 4인이 보인다.
- gold / permanent augment slot / trait reroll 재화가 보인다.
- save / load / debug start 버튼이 동작한다.

### Expedition 확인 항목

- 5노드 맵 텍스트가 보인다.
- 현재 위치 / 남은 노드 / 예정 보상이 보인다.
- 다음 전투 버튼이 동작한다.
- 귀환 버튼이 동작한다.

### Battle 확인 항목

- 4 아군 / 4 적 primitive가 보인다.
- HP / 로그 / 승패 / 속도 버튼이 보인다.

### Reward 확인 항목

- 3지선다 카드가 보인다.
- gold / item / temporary augment 중 2종 이상 보인다.
- 선택 후 Town 복귀가 가능하다.

## 이 단계 통과 기준

- Boot -> Town 자동 smoke가 돈다.
- Town debug UI가 보인다.
- 수동 경로로 첫 Battle 화면까지 갈 수 있다.
- save가 없을 때 demo profile/roster가 생성된다.
- JSON save fallback이 기본 성공 경로로 동작한다.
