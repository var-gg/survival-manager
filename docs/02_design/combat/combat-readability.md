# 전투 가독성 기준

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-30
- 소스오브트루스: `docs/02_design/combat/combat-readability.md`
- 관련문서:
  - `docs/01_product/mvp-vertical-slice.md`
  - `docs/02_design/combat/battlefield-and-camera.md`
  - `docs/02_design/ui/battle-observer-ui.md`
  - `docs/03_architecture/combat-state-and-event-model.md`

## 목적

이 문서는 prototype 전투가 "읽을 수 있는 auto-battle"로 남기 위해 초기에 고정할 정보 예산과 표현 제한을 정의한다.

## 고정 스케일

- 전투 규모는 `4v4`다.
- 카메라는 양 팀 전체를 한 화면에 담는다.
- 한 번에 추적해야 하는 핵심 actor 수를 8명으로 제한한다.

## 필수 정보

전투 중 플레이어가 바로 읽을 수 있어야 하는 정보는 아래로 제한한다.

- 체력바
- 현재 타깃
- windup 진행 상태
- 방어 자세 여부
- step/event 로그
- x1 / x2 / x4 / pause

## 현재 표현 원칙

- actor는 단순 capsule과 저비용 motion으로 표현한다.
- 공격/회복은 과한 VFX보다 위치 이동, 텍스트, 간단한 pulse로 전달한다.
- 시뮬레이션 truth는 숫자와 snapshot으로 유지하고, 시각 연출은 그 위를 덧씌운다.

## 금지 방향

- 4v4 범위를 넘어서는 밀집 군집전
- 과도한 screen shake와 camera jump
- hit effect가 체력, 타깃, windup을 가리는 연출
- presentation만으로 결과를 새로 판단하는 UI

## 운영 메모

- 전투가 복잡해질수록 새로운 액션보다 가독성 예산을 먼저 확인한다.
- 새로운 UI 표시를 넣을 때는 기존 필수 정보와 충돌하지 않는지 먼저 본다.
