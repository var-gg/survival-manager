# 콘텐츠 authoring과 밸런스 데이터 경계

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/03_architecture/content-authoring-and-balance-data.md`
- 관련문서:
  - `docs/03_architecture/content-authoring-model.md`
  - `docs/03_architecture/loadout-compiler-and-battle-snapshot.md`
  - `docs/02_design/systems/launch-content-scope-and-balance.md`
  - `docs/02_design/combat/authority-matrix.md`
  - `docs/02_design/combat/resource-cadence-loadout.md`
  - `docs/02_design/combat/targeting-and-ai-vocabulary.md`
  - `docs/02_design/combat/skill-authoring-schema.md`
  - `docs/02_design/meta/affix-authoring-schema.md`
  - `docs/02_design/meta/retrain-contract.md`
  - `docs/02_design/meta/synergy-and-augment-taxonomy.md`

## 목적

이 문서는 launch-scope 데이터 authoring과 compile 경계가 어느 레이어에 속하는지 고정한다.

## 경계 원칙

- `SM.Content`: authored schema와 Unity-friendly definition
- `SM.Meta`: build state, progression, compile orchestration
- `SM.Combat`: pure C# battle truth
- `SM.Unity`: authoring, presentation, scene adapter, sandbox entry
- `SM.Editor`: seed, validation, bootstrap

## 금지 원칙

- Unity Localization, `ScriptableObject`, `MonoBehaviour` 타입이 `SM.Combat`로 들어가는 것
- save/presentation 편의 때문에 battle truth와 authoring schema를 합치는 것
- compile contract를 UI와 scene script가 직접 소유하는 것

## 주요 authored field

### shared seam

- `AuthorityLayer`
- `EffectScope`
- `EffectCapability`
- `ActionSlotKind`
- `ActivationModel`
- `ActionLane`
- `ActionLockRule`
- `TargetRule`
- `BehaviorProfile`
- `CombatEntityKind`
- `SummonProfile`

### SkillDefinitionAsset

- `AuthorityLayer`
- `ActivationModel`
- `ActionLane`
- `ActionLockRule`
- `TargetRuleData`
- `Effects`
- `SummonProfile`
- `CastWindupSeconds / CooldownSeconds / RecoverySeconds`
- `AnimationHookId / VfxHookId / SfxHookId`

### BasicAttackDefinition / PassiveDefinition / MobilityDefinition

- 각 slot 전용 topology와 cadence
- `AuthorityLayer`
- `TargetRuleData`
- `Effects`
- `SummonProfile`

### UnitArchetypeDefinition

- `Loadout`
- `BehaviorProfile`
- `FormationLine`
- `BaseMaxEnergy`
- `BaseStartingEnergy`
- `BaseSkillHaste`
- base stat v2 fields

### AffixDefinition / SynergyDefinition / AugmentDefinition / StatusFamilyDefinition

- `AuthorityLayer`
- `Effects`
- layer별 allowed scope/capability boundary

## compile contract

- persistence-friendly legacy slot string은 migration 동안 읽을 수 있다.
- compile 결과의 canonical topology는 `BasicAttack / SignatureActive / FlexActive / SignaturePassive / FlexPassive / MobilityReaction`이다.
- legacy `mana_*`, `cooldown_recovery`, `core_active`, `utility_active`, `support`는 migration alias로만 취급한다.
- compile hash는 skill coeff, delivery, target rule, crit 허용 여부, energy profile, entity kind, ownership/summon profile을 포함해야 한다.
- compile provenance는 최소 `archetype_base / item / affix / passive_numeric / augment_temporary / augment_permanent / team_tactic / role_instruction / skill_slot / team_numeric`를 남긴다.

## validator 책임

`SM.Editor.Validation.ContentDefinitionValidator`와 `LoopAContractValidator`는 최소 아래를 검사한다.

- missing localization key
- invalid localization key naming
- leftover legacy prose
- invalid stat id
- invalid class id / role family / enum taxonomy
- invalid canonical id / scope kind / slot kind
- 6-slot topology drift
- signature/flex/mobility activation model drift
- authority matrix violation
- affix value band / exclusive group / gating overlap
- augment slot topology mutation
- summon-chain violation
- status stack cap / refresh policy / ownership policy
- exact synergy `2 / 4` tier 누락
- deterministic report output

## report output

- content validation report path: `Logs/content-validation/content-validation-report.json`
- content validation summary path: `Logs/content-validation/content-validation-summary.md`
- balance sweep report path: `Logs/balance-sweep/balance-sweep-report.json`
- balance sweep summary path: `Logs/balance-sweep/balance-sweep-summary.csv`

## launch authoring acceptance

### 현재 패스에서 필수

- canonical class board owner는 `vanguard / duelist / ranger / mystic`
- 모든 roster archetype은 6-slot topology를 만족한다
- `SignatureActive`는 energy 전용이고 `FlexActive`는 cooldown/trigger 전용이다
- meta acquisition contract는 fixed signature + flex rule을 깨지 않는다
- counter 설계는 hard counter가 아니라 soft counter 문법을 따른다

### paid launch floor를 주장하려면

- `12 core archetypes`
- `40 skills`
- `36 equippables`
- `72 passive nodes`
- `18 temporary augments`
- `9 permanent augments`

### paid launch safe target을 주장하려면

- `16 archetypes`
- `40~48 skills`
- `42~54 equippables`
- `96 passive nodes`
- `24 temporary augments`
- `12 permanent augments`

## Loop C registry and runtime boundary

Loop C의 governance source-of-truth는 editor-side registry와 각 definition의
`BudgetCard`다.

- `BudgetCard` required definitions
  - `UnitArchetypeDefinition`
  - `SkillDefinitionAsset`
  - `PassiveDefinition`
  - `MobilityDefinition`
  - `AffixDefinition`
  - `AugmentDefinition`
  - `SynergyTierDefinition`
  - `StatusFamilyDefinition`
- runtime combat summary fields
  - `ContentRarity`
  - `PowerBand?`
  - `CombatRoleBudgetProfile?`
  - `BudgetFinalScore`
  - `DeclaredThreatPatterns`
  - `DeclaredCounterTools`
  - `DeclaredFeatureFlags`
- runtime combat summary에는 `RecruitTier`를 싣지 않는다.

## Loop C validator ownership

- `BudgetWindowValidationPass`
- `BudgetIdentityValidationPass`
- `RarityComplexityValidationPass`
- `CounterTopologyValidationPass`
- `SynergyStructureValidationPass`
- `V1ForbiddenFeatureValidationPass`

synergy `2 / 4` 구조 검사는 `CounterTopologyValidationPass`가 아니라
`SynergyStructureValidationPass`가 소유한다.

## Loop C audit artifact

`ContentDefinitionValidator.ValidateAndWriteReport()`는 아래 artifact를 항상 남긴다.

- `Logs/content-validation/content-validation-report.json`
- `Logs/content-validation/content-validation-summary.md`
- `Logs/content-validation/content_budget_audit.json`
- `Logs/content-validation/content_budget_audit.md`
- `Logs/content-validation/counter_coverage_matrix.md`
- `Logs/content-validation/v1_forbidden_feature_report.md`

## Loop D runtime analytics boundary

- authoritative combat/economy/recruit analytics source는 runtime telemetry event다.
- `BattleReplayBundle`은 telemetry, readability, battle summary를 함께 담는다.
- first playable pool gating은 runtime `FirstPlayableSliceDefinition`을 기준으로 한다.
- prune/health decision은 Loop C governance metadata와 Loop D telemetry artifact를 함께 사용한다.
