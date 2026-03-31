# 전투 행동 계약

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-31
- 소스오브트루스: `docs/02_design/combat/combat-behavior-contract.md`
- 관련문서:
  - `docs/02_design/combat/realtime-simulation-model.md`
  - `docs/02_design/combat/team-tactics-and-unit-rules.md`
  - `docs/03_architecture/combat-state-and-event-model.md`
  - `docs/03_architecture/combat-runtime-architecture.md`

## 목적

prototype 오토배틀이 `seek -> stick -> basic attack blob`에 머물지
않도록 최소 행동 phase와 재평가 규칙을 정의한다.

## phase

- `Spawn`
- `AdvanceToAnchor`
- `AcquireTarget`
- `Approach`
- `SecurePosition`
- `ExecuteAction`
- `Reposition`
- `BreakContact`
- `Recover`
- `Dead`

## reevaluation 규칙

- full retarget는 매 frame이 아니라 cadence + event로만 연다.
- 기본 cadence는 `0.24s ~ 0.42s` 범위의 behavior profile이 소유한다.
- event trigger:
  - target loss
  - slot loss
  - took hit
  - skill cooldown ready
  - mobility cooldown ready

## anti-jitter 규칙

- range check는 `preferredRangeMin/Max + rangeHysteresis`를 쓴다.
- current target이 살아 있고 switch lock이 남아 있으면 그대로 유지한다.
- slotting이 끝나지 않았으면 in-range여도 바로 공격 상태로 넘어가지 않는다.

## 역할 baseline

- `vanguard`: 붙는 성향, 낮은 retreat bias, 높은 block/stability
- `duelist`: 접촉 후 짧은 거리 재조정, opportunism 높음
- `ranger`: maintain-range 우선, 압박 시 break-contact
- `mystic`: cast band 유지, 압박 시 짧은 disengage

## hidden coefficient 정책

- `DecisionQuality`, `Composure`, `RiskTolerance` 류는 지금은 public stat으로 올리지 않는다.
- 현재는 `retreatBias`, `maintainRangeBias`, `opportunism`,
  `discipline`, `dodgeChance`, `blockChance`, `stability` 같은
  hidden coefficient로 검증한다.
- 이유:
  - 플레이어 설명 비용을 늘리지 않고도 AI 성격을 조정할 수 있다.
  - v1에서 재미 확인 전 stat inflation을 막을 수 있다.
