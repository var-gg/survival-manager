# 콘텐츠 authoring과 밸런스 데이터 경계

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/03_architecture/content-authoring-and-balance-data.md`
- 관련문서:
  - `docs/03_architecture/content-authoring-model.md`
  - `docs/03_architecture/loadout-compiler-and-battle-snapshot.md`
  - `docs/02_design/systems/launch-content-scope-and-balance.md`
  - `docs/02_design/systems/launch-floor-content-matrix.md`
  - `docs/02_design/combat/skill-taxonomy-and-damage-model.md`
  - `docs/02_design/combat/skill-authoring-schema.md`
  - `docs/02_design/combat/status-keyword-and-proc-rulebook.md`
  - `docs/02_design/meta/affix-authoring-schema.md`
  - `docs/02_design/meta/skill-acquisition-and-retrain.md`
  - `docs/02_design/meta/synergy-and-augment-taxonomy.md`
  - `docs/02_design/meta/item-passive-augment-budget.md`
  - `docs/02_design/meta/passive-board-node-catalog.md`
  - `docs/02_design/meta/synergy-family-catalog.md`

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

### StatKey

- canonical stat id를 우선한다.
- legacy alias는 migration 동안만 읽고, 새 authored content는 canonical stat을 사용한다.

### SkillDefinitionAsset

- `TemplateType`
- `Kind`
- `SlotKind`
- `DamageType`
- `Delivery`
- `TargetRule`
- `RangeMin / RangeMax / Radius / Width / ArcDegrees`
- `Power / PowerFlat`
- `PhysCoeff / MagCoeff / HealCoeff / HealthCoeff`
- `CanCrit`
- `CooldownSeconds / RecoverySeconds / ResourceCost`
- `AiIntents / AiScoreHints`
- `AnimationHookId / VfxHookId / SfxHookId`
- `PowerBudget`
- `LearnSource`
- `RequiredWeaponTags / RequiredClassTags / SupportAllowedTags`

### ItemBaseDefinition

- `SlotType`
- `IdentityKind`
- `BudgetBand`
- `AllowedClassTags / AllowedArchetypeTags`
- `GrantedSkills / RuleModifierTags / UniqueRuleTags`

### AffixDefinition

- `Category`
- `AffixFamily / EffectType`
- `ValueMin / ValueMax`
- `AllowedSlotTypes`
- `RequiredTags / ExcludedTags`
- `ItemLevelMin / SpawnWeight`
- `ExclusiveGroupId`
- `BudgetScore`
- `TextTemplateKey`
- `CompileTags / RuleModifierTags`

### UnitArchetypeDefinition

- `ScopeKind`
- `RoleFamilyTag`
- `PrimaryWeaponFamilyTag`
- `SupportModifierBiasTags`
- `LockedAttackProfileId / LockedAttackProfileTag`
- `LockedSignatureActiveSkill / LockedSignaturePassiveSkill`
- `FlexUtilitySkillPool / FlexSupportSkillPool`
- base stat v2 fields

### PassiveBoardDefinition / PassiveNodeDefinition

- board owner는 `ClassId`
- node는 `NodeKind`
- prerequisite와 mutual exclusion을 data로 가진다.

## compile contract

- 저장 포맷은 string slot kind를 유지할 수 있다.
- compile 결과는 `core_active / utility_active / passive / support` 4개만 허용한다.
- old save의 `active_core`는 compile 과정에서 normalize한다.
- compile hash는 skill coeff, delivery, target rule, crit 허용 여부까지 포함해야 한다.
- compile hash는 numeric modifier payload, rule modifier payload, team tactic, role instruction, normalized stat output도 포함해야 한다.
- compile provenance는 최소 `archetype_base / item / affix / passive_numeric / augment_temporary / augment_permanent / team_tactic / role_instruction / skill_slot / team_numeric`를 남긴다.

## validator 책임

`SM.Editor.Validation.ContentDefinitionValidator`는 최소 아래를 검사한다.

- missing localization key
- invalid localization key naming
- leftover legacy prose
- invalid stat id
- invalid class id / role family / enum taxonomy
- invalid canonical id / scope kind / slot kind
- affix value band / exclusive group / gating overlap
- skill template / range band / AI hint / hook id
- augment offer bucket / build bias / protection overlap
- status stack cap / refresh policy / ownership policy
- passive board owner 누락
- passive node kind 누락
- exact synergy `2 / 3 / 4` tier 누락
- unique item의 rule payload 누락
- launch-scope count report
- deterministic report output

## report output

- content validation report path: `Logs/content-validation/content-validation-report.json`
- content validation summary path: `Logs/content-validation/content-validation-summary.md`
- balance sweep report path: `Logs/balance-sweep/balance-sweep-report.json`
- balance sweep summary path: `Logs/balance-sweep/balance-sweep-summary.csv`

## launch authoring acceptance

### 현재 패스에서 필수

- canonical class board owner는 `vanguard / duelist / ranger / mystic`
- skill compile contract는 모든 hero 기준 `2 active + 1 passive/trigger + 1 support`
- meta acquisition contract는 current 4-slot compile을 깨지 않는다.
- counter 설계는 hard counter가 아니라 soft counter 문법을 따른다.

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

## 열린 질문

- `lifesteal`, `omnivamp`, `HealthCoeff`의 실전 resolver 적용은 다음 패스에서 닫는다.
- `duelist` canonical id와 `Striker` 문서 라벨을 localization/UI에서 어떻게 같이 보여줄지는 다음 UI pass에서 정리한다.
