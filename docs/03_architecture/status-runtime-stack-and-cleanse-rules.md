# 상태 런타임 스택과 정화 규칙

- 상태: active
- 소유자: repository
- 최종수정일: 2026-07-23
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
- `MagnitudeUnit` (`Flat` / `Rate`)
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
- 재적용 시 `Stacks = max(existing.Stacks, min(spec.MaxStacks, existing.Stacks + 1))`이다. 이미 쌓인 상태에 더 낮은 incoming `MaxStacks`가 들어와도 기존 stack을 줄이지 않는다.
- 저장 magnitude는 `max(existing.Magnitude, spec.Magnitude)`로 유지한다. 전역 additive intensity stacking은 V1에서 열지 않는다.
- `ShredsDefense` consumer만 flat 차감량을 `stored Magnitude × Stacks`로 계산한다. 따라서 sunder 재적용은 실제로 누적되지만, 저장 merge와 다른 status channel의 해석은 바꾸지 않는다.
- `MaxStacks > 1`은 V1에서 `ShredsDefense` family에만 허용한다. 다른 channel은 stack을 소비하지 않으므로 validator가 silently inert 저작을 error로 거부한다.
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

정화가 부여하는 상태 id는 `CleanseProfileDefinition.GrantedStatusId`가 소유한다(기본 `unstoppable`,
2026-07-13 위생 정리에서 sim 리터럴을 콘텐츠로 승격). 저지불가 kind(`GrantsUnstoppable`)를 가진
파생 상태로 교체 저작할 수 있으며, 부여 지속시간의 하한 0.1초는 아래 클램프 표의 코드 소유 항목이다.

## 코드 소유 클램프 바닥 (튜닝 축 아님)

상태이상 숫자(1·2보)와 효과 종류(3보)는 콘텐츠가 소유하지만, 아래 바닥/상수는 **코드가 소유한다**
(오너 게이트④ 비준, 2026-07-12 — "클램프 코드 소유"). 어떤 magnitude·배율 조합을 저작해도 sim이
붕괴 값(공속 0, 즉사 증폭, 0 틱)으로 떨어지지 않게 하는 안전 레일이며, 여기 값을 조정하고 싶다면
콘텐츠 필드 승격이 아니라 이 문서와 코드를 함께 바꾸는 구조 결정으로 다룬다.

| 지점 | 바닥/상수 | 코드 위치 |
| --- | --- | --- |
| 받는 피해 배수 하한 (증폭+수호 가산 후) | `0.25` | `UnitSnapshot.GetIncomingDamageMultiplier` |
| 치유 수신 배수 하한 (`ReducesHealing` 가산 후) | `0.1` | `UnitSnapshot.GetHealingTakenMultiplier` |
| 공속/이속 감쇠 배수 하한 (`DampensTempo` 가산 후) | `0.1` | `UnitSnapshot.GetSlowMultiplier` |
| 공속/이속 최종값 절대 하한 | `0.1` | `UnitSnapshot.AttackSpeed` / `MoveSpeed` |
| 방어/저항 차감 바닥 (`ShredsDefense` 차감 후) | `0` | `UnitSnapshot.Armor` / `Resist` |
| 즉시 보호막 전환 최소량 (`GrantsBarrierOnApply`) | `1` | `StatusResolutionService.ApplySingleStatus` |
| 주기 틱 피해 최소량 (burn/bleed × `MagnitudeScale` 후) | `1` | `StatusResolutionService.ApplyPeriodicDamage` |
| 제어 저항 창의 지속 감쇠 하한 | `0.1` 배수 | `StatusResolutionService` (resist window 적용부) |
| 정화 부여 상태 지속 하한 | `0.1`초 | `StatusResolutionService.ApplyCleanse` |
| 치유 산출 최소량 | `1` | `HitResolutionService.ResolveSupportValue` |
| 방어 준선형 감쇠 상수 | `ArmorScalingK = 10` | `HitResolutionService` (`M/(M+K)`) |

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
- `StatusFamilyDefinition.MagnitudeUnit`은 `ShredsDefense`·주기 피해·즉시 보호막에서 `Flat`, 받는 피해 가산·치유 감소·공속/이속 감쇠에서 `Rate`여야 한다. 숫자 크기로 단위를 추론하지 않는다.
- magnitude-only family의 zero payload는 error다. `MarksTarget`, `BlocksAction`, `GrantsUnstoppable`처럼 독립적인 non-magnitude kind를 함께 가진 family의 zero는 membership-only 저작으로 허용하되 warning을 남긴다.
- flat `ShredsDefense` magnitude에 임의의 하한/상한 band를 두지 않는다. finite positive magnitude와 finite positive `MagnitudeScale`만 요구하며 방어/저항 0 바닥은 runtime이 소유한다.
- `ShredsDefense` 외 family의 `MaxStacks > 1`은 runtime이 소비하지 않으므로 error로 거부한다.
- shipped `skill_sunder_rhythm`의 1 stack과 3 stack이 각각 flat `0.5`, `1.5`를 차감하는지 검증
