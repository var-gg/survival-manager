# Town and Expedition Loop

- 상태: draft
- 최종수정일: 2026-03-29
- phase: prototype

## 목적

이 문서는 MVP meta-loop 구조를 정의한다.
핵심은 Town에서 원정 준비를 하고, Expedition에서 노드를 선택할 수 있는 상태를 playable slice로 만드는 것이다.

## MVP 규칙

### core loop

1. Town에서 로스터 상태를 본다.
2. recruit 후보를 확인하고 영입 또는 reroll을 한다.
3. expedition squad를 준비한다.
4. deploy preview 4인을 확인한다.
5. Expedition으로 출발한다.
6. 5노드 맵에서 다음 전투/진행 또는 귀환을 선택한다.
7. 귀환 후 다시 Town에서 재정비한다.

### Town 최소 기능

- 보유 로스터 목록
- recruit 후보 3개 표시
- recruit / reroll 버튼
- expedition squad 현황(최대 8)
- battle deploy preview(현재 4)
- gold / permanent augment slot / trait reroll 재화 표시
- save / load / debug start 버튼

### Expedition 최소 기능

- 5노드 분기형 맵 표현
- 현재 위치
- 남은 노드
- 예정 보상
- 다음 전투 또는 귀환 버튼

## 구현 원칙

- scene에 새 도메인 규칙을 하드코딩하지 않는다.
- 데이터는 content definition / session state / persistence를 통해 읽는다.
- save가 없으면 데모용 초기 profile/roster를 만든다.
- UGUI와 단순 텍스트/버튼 위주로 만든다.

## 장기 확장 지점

- 편성 drag-and-drop
- 실제 node branching graph
- town facility
- richer event node
- reward preview sophistication
