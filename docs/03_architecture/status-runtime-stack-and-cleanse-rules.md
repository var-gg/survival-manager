# 상태 런타임 스택과 정화 규칙

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-31
- 소스오브트루스: `docs/03_architecture/status-runtime-stack-and-cleanse-rules.md`
- 관련문서:
  - `docs/02_design/combat/status-effects-cc-and-cleanse-taxonomy.md`
  - `docs/03_architecture/combat-state-and-event-model.md`
  - `docs/03_architecture/replay-persistence-and-run-audit.md`

## 목적

이 문서는 status authoring과 battle runtime의 연결 지점을 정의한다.
launch floor에서는 OOP status 계층 대신 typed family + resolver chain을 사용한다.

## canonical model

### content / authoring

- `StatusFamilyDefinition`
- `StatusApplicationRule`
- `CleanseProfileDefinition`
- `ControlDiminishingRuleDefinition`

### runtime

- `AppliedStatusState`
- `StatusApplicationSpec`
- `StatusResolutionService`

## runtime flow

1. `SkillDefinitionAsset`이 `AppliedStatuses`와 optional `CleanseProfileId`를 가진다.
2. compile 단계가 이를 `BattleSkillSpec`으로 옮긴다.
3. 전투 중 `StatusResolutionService.ApplySkillStatuses(...)`가 상태 적용과 정화를 처리한다.
4. `AdvanceStatuses(...)`가 timer 감소, periodic damage, hard CC 종료 시 resist window 부여를 담당한다.

## launch floor DR 규칙

- hard control: `stun`, `root`, `silence`
- hard CC 종료 후 `1.5초` 동안 `50%` control resist window를 건다.
- `break_and_unstoppable`는 hard control 1개를 끊고 짧은 `unstoppable`과 같은 resist window를 부여한다.
- `tenacity`는 duration reduction에만 적용한다.

## cleanse 규칙

- `cleanse_basic`: `slow`, `burn`, `bleed`, `wound`, `sunder`, `marked`, `exposed`
- `cleanse_control`: `root`, `silence` + basic floor
- `break_and_unstoppable`: `stun`, `root`, `silence` 제거 후 짧은 `unstoppable`

cleanse는 non-status rule modifier를 제거하지 않는다.

## battle event 계약

status 관련 event는 typed envelope로 기록한다.

- `StatusApplied`
- `StatusRemoved`
- `CleanseTriggered`
- `ControlResistApplied`

각 event는 `BattleEventKind`, `PayloadId`, `SecondaryValue`, `Note`를 통해 replay/log에 직렬화된다.

## validator / test oracle

- duplicate / invalid status id
- missing cleanse target
- incompatible status + skill tag
- hard-CC chain DR
- tenacity / cleanse / DR interaction
- replay round-trip에서 status event가 유실되지 않는지 검증
