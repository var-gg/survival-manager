# mvp debug ui

- 상태: active
- 최종수정일: 2026-03-29
- phase: prototype

## 현재 원칙

- 고퀄 UI를 만들지 않는다.
- UGUI와 placeholder만 사용한다.
- scene installer가 UI 객체 이름과 serialized reference 계약을 만든다.
- operator가 눌러보고 상태 변화를 즉시 읽을 수 있어야 한다.

## Town 화면

- roster 목록
- recruit summary
- recruit 카드 3개
- reroll / save / load / debug start / quick battle 버튼
- expedition squad 현황
- battle deploy preview

## Expedition 화면

- 5노드 box track
- 현재 위치
- 예정 보상
- squad 정보
- next battle / return town 버튼

## Battle 화면

- primitive actor 4v4
- actor HP label / HP bar
- 팀 요약 HP 텍스트
- 로그 패널
- tick / action / speed / pause 상태 텍스트
- progress bar
- x1 / x2 / x4 / pause / continue 버튼

## Reward 화면

- 전투 결과 요약
- reward 카드 3개
- 카드 선택 버튼
- Town 귀환 버튼
