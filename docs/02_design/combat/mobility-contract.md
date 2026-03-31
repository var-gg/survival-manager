# 전투 이동기 계약

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-31
- 소스오브트루스: `docs/02_design/combat/mobility-contract.md`
- 관련문서:
  - `docs/02_design/combat/combat-behavior-contract.md`
  - `docs/03_architecture/combat-runtime-architecture.md`

## 목적

dash, roll, blink를 클래스별 별도 시스템이 아니라 공통 재배치 action schema로 정의한다.

## schema

- `style`: `Dash`, `Roll`, `Blink`
- `purpose`: `Engage`, `Disengage`, `Evade`, `Chase`, `MaintainRange`
- `distance`
- `cooldown`
- `castTime`
- `recovery`
- `triggerMinDistance`, `triggerMaxDistance`
- `lateralBias`

## 현재 rollout

- `ranger`: `Roll + MaintainRange`
- `mystic`: `Blink + Disengage`
- `vanguard`, `duelist`: engage dash schema만 authored, full runtime 활용은 후속

## stamina 비도입 이유

- v1 목표는 reposition cadence 검증이지 resource minigame 추가가 아니다.
- stamina를 넣으면 UI, AI 절약 규칙, 회복 규칙, 튜닝 축이 한 번에 늘어난다.
- 현재는 `cooldown + trigger condition`만으로도 원하는 공간 읽힘을 얻을 수 있다.

## non-goal

- player-only evade skill
- i-frame economy
- long-form traversal skill
