# 팀 전술과 유닛 규칙

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-30
- 소스오브트루스: `docs/02_design/combat/team-tactics-and-unit-rules.md`
- 관련문서:
  - `docs/02_design/combat/deployment-and-anchors.md`
  - `docs/02_design/combat/realtime-simulation-model.md`
  - `docs/03_architecture/combat-runtime-architecture.md`
  - `docs/03_architecture/combat-content-mapping.md`

## 목적

이 문서는 전투 의사결정을 두 계층으로 나누는 기준을 정리한다.
상위는 팀 posture, 하위는 유닛별 condition -> action -> target 규칙 체인이다.

## 팀 posture 계층

현재 prototype은 아래 다섯 posture를 사용한다.

- `HoldLine`
  - front는 소폭 전진하고, back은 anchor 근처를 더 오래 지킨다.
  - leash가 가장 짧은 축에 가깝다.
- `StandardAdvance`
  - front와 back 모두 완만하게 전진한다.
  - 기본 posture다.
- `ProtectCarry`
  - front는 carry를 지킬 수 있는 거리만 전진하고, back은 더 뒤에 남는다.
  - back row leash가 더 짧다.
- `CollapseWeakSide`
  - 상대적으로 약한 lane 쪽으로 shape를 기울인다.
  - target score와 home position 둘 다 weak side를 반영한다.
- `AllInBackline`
  - 앞줄이 더 깊게 전진하고, backline 압박 bias가 강하다.
  - leash가 가장 길다.

## 유닛 규칙 계층

유닛 전술 문법은 기존처럼 `condition -> action -> target`을 유지한다.

- 현재 condition
  - `SelfHpBelow`
  - `AllyHpBelow`
  - `EnemyInRange`
  - `LowestHpEnemy`
  - `EnemyExposed`
  - `Fallback`
- 현재 action
  - `BasicAttack`
  - `ActiveSkill`
  - `WaitDefend`
- 현재 target selector
  - `Self`
  - `LowestHpAlly`
  - `FirstEnemyInRange`
  - `LowestHpEnemy`
  - `NearestEnemy`
  - `MostExposedEnemy`

## spatial 판단 기준

- 사거리는 row 판정이 아니라 실제 거리로 계산한다.
- `EnemyExposed`는 주변 아군 지원 거리와 back row penalty를 합친 노출 점수로 계산한다.
- target score는 distance, health, exposure, current target lock, team posture bias를 함께 본다.

## 현재 prototype 원칙

- posture는 팀 shape와 target bias를 바꾼다.
- unit rule은 실제 action 선택을 결정한다.
- 현재 runtime UI는 posture만 바꾼다.
- 유닛 tactic preset은 content 또는 code-defined preset으로 유지한다.
