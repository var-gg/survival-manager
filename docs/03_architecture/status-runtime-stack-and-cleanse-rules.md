# 상태 런타임 스택과 정화 규칙

- 상태: active
- 소유자: repository
- 최종수정일: 2026-06-14
- 소스오브트루스: `docs/03_architecture/status-runtime-stack-and-cleanse-rules.md`
- 관련문서:
  - `docs/02_design/combat/status-effects-cc-and-cleanse-taxonomy.md`
  - `docs/02_design/combat/status-keyword-and-proc-rulebook.md`
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
- `DefaultStackCap / DefaultStackPolicy / DefaultRefreshPolicy`
- `ProcAttributionPolicy / OwnershipPolicy`

### runtime

- `AppliedStatusState`
- `StatusApplicationSpec`
- `StatusResolutionService`

## runtime flow

1. `SkillDefinitionAsset`이 `AppliedStatuses`와 optional `CleanseProfileId`를 가진다.
2. compile 단계가 이를 `BattleSkillSpec`으로 옮긴다.
3. 전투 중 `StatusResolutionService.ApplySkillStatuses(...)`가 상태 적용과 정화를 처리한다.
4. `AdvanceStatuses(...)`가 timer 감소, periodic damage, hard CC 종료 시 resist window 부여를 담당한다.

현재 resolver는 launch floor set과 V1 stack / refresh / ownership / timing 정책을 직접 소비한다.

## V1 stack / refresh 정책

- active status slot은 `StatusId`당 하나다. 같은 `StatusId`를 여러 독립 인스턴스로 보관하지 않는다.
- 재적용 시 `Stacks = min(spec.MaxStacks, existing.Stacks + 1)`이다.
- magnitude는 `max(existing.Magnitude, spec.Magnitude)`로 유지한다. additive intensity stacking은 V1에서 열지 않는다.
- `RefreshDurationOnReapply=true`면 remaining duration은 기존 remaining과 새 duration 중 큰 값을 쓴다.
- duration cap은 기존 duration과 새 duration 중 큰 값을 보존한다. duration additive stacking은 V1에서 열지 않는다.

## V1 ownership / attribution 정책

- `AppliedStatusState`는 `SourceActorId`, `SourceSkillId`, `SourceApplicationId`를 가진다.
- skill status 적용은 actor id, skill id, status application id를 status state에 저장한다.
- triggered effect status는 owner id와 effect source id를 저장한다.
- 같은 status가 재적용되면 비어 있지 않은 최신 source가 ownership을 갱신한다.
- periodic tick과 expire/remove event는 저장된 source actor를 actor로, status 보유자를 target으로 기록한다.
- source actor를 찾을 수 없으면 status 보유자를 fallback actor로 쓴다.

## V1 proc timing 정책

- status tick은 `StatusResolutionService.AdvanceStatuses(...)` 시작부에서 timer 감소 전에 처리한다.
- timer 감소와 expire/remove event는 tick 이후 같은 pass에서 처리한다.
- cleanse는 `ApplySkillStatuses(...)` 안에서 새 status application보다 먼저 처리한다.
- hard CC가 expire/remove되면 같은 pass에서 control resist window를 부여한다.
- V1 제외: on-hit status proc chain, on-cleanse proc, on-expire proc, additive DoT intensity stacking, independent duplicate status instance.

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
- invalid stack cap / refresh / ownership policy
- hard-CC chain DR
- tenacity / cleanse / DR interaction
- replay round-trip에서 status event가 유실되지 않는지 검증
