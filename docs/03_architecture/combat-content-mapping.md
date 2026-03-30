# 전투 콘텐츠 매핑

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-30
- 소스오브트루스: `docs/03_architecture/combat-content-mapping.md`
- 관련문서:
  - `docs/03_architecture/content-authoring-model.md`
  - `docs/03_architecture/content-authoring-and-balance-data.md`
  - `docs/03_architecture/content-seed-assets.md`
  - `docs/03_architecture/combat-state-and-event-model.md`
  - `docs/02_design/combat/skill-taxonomy-and-damage-model.md`
  - `docs/02_design/combat/team-tactics-and-unit-rules.md`

## 목적

이 문서는 authored combat content가 spatial auto-skirmish runtime 모델에 어떻게 대응되는지 고정한다.

## authored enum 매핑

- `DeploymentAnchorValue` -> `DeploymentAnchorId`
- `TeamPostureTypeValue` -> `TeamPostureType`
- `TacticConditionTypeValue` -> `TacticConditionType`
- `TargetSelectorTypeValue` -> `TargetSelectorType`
- `BattleActionTypeValue` -> `BattleActionType`
- `SkillKindValue` -> `SkillKind`
- `SkillDeliveryValue` -> `SkillDelivery`
- `SkillTargetRuleValue` -> `SkillTargetRule`

## authored stat 매핑

`UnitArchetypeDefinition`은 canonical stat v2에 대응하는 base field를 직접 가진다.
현재 구현은 compatibility 목적의 legacy field도 일부 유지하지만, launch authoring은 canonical field를 우선한다.

- `BaseMaxHealth`
- `BaseArmor`
- `BaseResist`
- `BasePhysPower`
- `BaseMagPower`
- `BaseAttackSpeed`
- `BaseHealPower`
- `BaseMoveSpeed`
- `BaseAttackRange`
- `BaseManaMax`
- `BaseManaGainOnAttack`
- `BaseManaGainOnHit`
- `BaseCooldownRecovery`
- `BaseCritChance`
- `BaseCritMultiplier`
- `BasePhysPen`
- `BaseMagPen`
- `BaseAggroRadius`
- `BasePreferredDistance`
- `BaseProtectRadius`
- `BaseAttackWindup`
- `BaseCastWindup`
- `BaseProjectileSpeed`
- `BaseCollisionRadius`
- `BaseAttackCooldown`
- `BaseLeashDistance`
- `BaseTargetSwitchDelay`

이 값들은 runtime에서 `StatKey` 기반 `StatBlock` 또는 동등한 수치 사전으로 옮겨진다.

## authored 전술 매핑

- `TacticPresetEntry`는 `Priority`, `ConditionType`, `Threshold`, `ActionType`, `TargetSelector`, `Skill`을 가진다.
- runtime에서는 같은 구조를 `TacticRule`과 `SkillDefinition`으로 옮긴다.
- row 기반 selector를 다시 들여오지 않는다.

## authored launch taxonomy 연결

- `SkillDefinitionAsset`의 slot, delivery, target, coeff 세부는 `docs/02_design/combat/skill-taxonomy-and-damage-model.md`를 따른다.
- `PassiveBoardDefinition`은 `ClassId`를 owner로 가진다.
- `PassiveNodeDefinition`은 `NodeKind`, prerequisite, mutual exclusion tag를 포함할 수 있다.
- 전체 authoring field ownership은 `docs/03_architecture/content-authoring-and-balance-data.md`를 따른다.

## authored 배치/팀 성향 매핑

- `UnitArchetypeDefinition.DefaultAnchor`는 기본 배치 추천값이다.
- `UnitArchetypeDefinition.PreferredTeamPosture`는 archetype이 잘 맞는 팀 posture 힌트다.
- session layer는 실제 배치 truth를 들고, authored 값은 초기 추천이나 자동 배치 기준으로만 사용한다.

## seed 데이터 기준

- `SampleSeedGenerator`는 spatial 전투에 필요한 stat definition을 같이 만든다.
- sample archetype은 default anchor와 preferred posture를 포함해야 한다.
- sample tactic preset은 `EnemyExposed`, `MostExposedEnemy` 같은 spatial 규칙을 포함할 수 있다.

## 현재 prototype 메모

- canonical content schema는 spatial combat를 표현할 수 있게 확장됐다.
- 현재 Battle scene smoke path는 일부 unit definition을 `HeroInstanceRecord` 기반으로 코드에서 조립한다.
- 이후 content loading을 battle runtime과 직접 연결하더라도, 위 매핑 계약은 그대로 유지한다.
