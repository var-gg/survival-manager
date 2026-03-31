# 전투 상태와 이벤트 모델

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-31
- 소스오브트루스: `docs/03_architecture/combat-state-and-event-model.md`
- 관련문서:
  - `docs/03_architecture/combat-runtime-architecture.md`
  - `docs/02_design/combat/combat-behavior-contract.md`
  - `docs/02_design/combat/combat-mechanics-glossary.md`
  - `docs/03_architecture/status-runtime-stack-and-cleanse-rules.md`
  - `docs/02_design/combat/realtime-simulation-model.md`
  - `docs/04_decisions/adr-0006-combat-sim-boundary.md`

## 목적

이 문서는 live simulation 구조에서 어떤 타입이 setup input인지, 어떤 타입이 mutable truth인지, 어떤 타입이 Unity read model인지 구분한다.

## setup input

- `UnitDefinition`
  - archetype, class, race, skill, tactic rule, preferred anchor 같은 authored 또는 조립된 정의
- `BattleState`
  - 양 팀 roster
  - ally/enemy posture
  - fixed-step seconds
  - seed

## mutable truth

- `UnitSnapshot`
  - `CombatVector2 Position`
  - `DeploymentAnchorId Anchor`
  - `CombatActionState ActionState`
  - `CurrentTargetId`
  - pending action / pending skill
  - cooldown, execute-action timer, switch-lock timer
  - footprint / behavior / mobility profile
  - reevaluation timer, mobility cooldown, block cooldown
  - engagement slot assignment
  - current health와 alive/dead 상태
  - `AppliedStatusState`
  - barrier, control resist window
- `BattleState`
  - step index
  - elapsed seconds
  - 팀별 생존 roster

## step read model

- `BattleSimulationStep`
  - `StepIndex`
  - `TimeSeconds`
  - `Units`
  - 이번 step에 새로 발생한 `Events`
  - `IsFinished`
  - `Winner`
- `BattleUnitReadModel`
  - actor id, name, side, anchor
  - position
  - current/max HP
  - alive 여부
  - action state
  - pending action
  - target id/name
  - windup progress
  - cooldown remaining
  - defending 여부
  - head anchor, navigation/separation, preferred range, slot ring preview 값
  - 주요 status와 barrier 요약

## 이벤트 규칙

- `BattleEvent`는 전체 전투의 source of truth가 아니다.
- event는 이번 step의 해석과 HUD/log 연출을 위한 delta다.
- `BattleEventKind`는 typed envelope로 기록한다.
- launch floor status 관련 event kind:
  - `StatusApplied`
  - `StatusRemoved`
  - `CleanseTriggered`
  - `ControlResistApplied`
- event는 actor/target id와 name, action type, 값, payload id, secondary value, note, 발생 step/time을 함께 가진다.

## 결과 모델

- `BattleResult`는 완료된 전투의 winner, step count, duration, 전체 event log, final read model을 담는다.
- replay/keyframe digest는 status와 barrier를 포함한 final state hash를 사용한다.
- EditMode 테스트는 이 결과를 사용해 결정론과 전투 규칙을 검증한다.

## 유지 규칙

- position truth는 항상 `CombatVector2`다.
- `BattleDeployHeroIds` 같은 legacy view는 편의 API로만 남는다.
- replay frame 목록을 source of truth로 되돌리지 않는다.
