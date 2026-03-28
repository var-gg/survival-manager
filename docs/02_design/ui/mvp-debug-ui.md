# mvp debug ui

- 상태: draft
- 최종수정일: 2026-03-29
- phase: prototype

## 목적

이 문서는 Town/Expedition의 MVP debug UI 범위를 정의한다.

## 원칙

- 고퀄 UI를 만들지 않는다.
- UGUI와 placeholder만 사용한다.
- scene은 domain truth를 다시 정의하지 않고 session state를 보여준다.

## Town 화면 최소 요소

- 보유 로스터 목록
- recruit 후보 3개
- recruit / reroll 버튼
- expedition squad 현황(최대 8)
- battle deploy preview(현재 4)
- gold / permanent augment slot / trait reroll 재화
- save / load / debug start 버튼

## Expedition 화면 최소 요소

- 5노드 맵 텍스트 표현
- 현재 위치
- 남은 노드
- 예정 보상
- 다음 전투 버튼
- 귀환 버튼

## 비목표

- 최종 HUD
- 고급 인벤토리 UI
- drag-and-drop 편성 UI
- 연출형 맵 카메라
