# 스킬 taxonomy와 데미지 모델

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/02_design/combat/skill-taxonomy-and-damage-model.md`
- 관련문서:
  - `docs/02_design/combat/resource-cadence-loadout.md`
  - `docs/02_design/combat/targeting-and-ai-vocabulary.md`
  - `docs/02_design/combat/skill-authoring-schema.md`
  - `docs/02_design/combat/skill-catalog-v1.md` (deprecated, audit log)
  - `docs/02_design/combat/stat-system-and-power-budget.md`
  - `docs/03_architecture/loadout-compiler-and-battle-snapshot.md`

## 목적

이 문서는 Loop A 기준의 skill loadout contract와 수식 용어를 고정한다.
effect descriptor, targeting, presentation hook, learn source의 세부 schema는 `skill-authoring-schema.md`가 소유한다.

## loadout contract

출전 유닛의 loadout은 전투 직전에 아래 6슬롯으로 compile된다.

| slot | 의미 | 가변 여부 |
| --- | --- | --- |
| `BasicAttack` | 기본 공격 | 고정 |
| `SignatureActive` | archetype signature active | 고정 |
| `FlexActive` | flex active | 가변 |
| `SignaturePassive` | archetype signature passive | 고정 |
| `FlexPassive` | flex passive | 가변 |
| `MobilityReaction` | mobility / evade / intercept reaction | 고정 |

설계 의도는 `정체성 고정 + 제한적 유연성`이다.
가시적 active는 `SignatureActive`, `FlexActive` 두 개뿐이다.

## skill taxonomy

### skill kind

- `Strike`
- `Heal`
- `Shield`
- `Buff`
- `Debuff`
- `Utility`

### delivery

- `Melee`
- `Ranged`
- `Projectile`
- `Nova`
- `Aura`
- `Trap`
- `Zone`

### target rule

- `NearestReachableEnemy`
- `NearestFrontlineEnemy`
- `LowestCurrentHpEnemy`
- `LowestHpPercentEnemy`
- `LowestEhpEnemy`
- `MarkedEnemy`
- `LargestEnemyCluster`
- `BacklineExposedEnemy`
- `LowestCurrentHpAlly`
- `LowestHpPercentAlly`
- `LowestEhpAlly`
- `NearestInjuredAlly`
- `Self`

## template / targeting boundary

- template type는 skill의 전달 방식과 shape를 잠그는 authoring schema다.
- target selector, fallback, hysteresis는 freeform score bias가 아니라 `TargetRule`과 `BehaviorProfile` data로 잠근다.
- role packet과 seed catalog는 `skill-catalog-v1.md`를 따른다.

## class skill family

### Vanguard

- guard
- taunt
- slam CC
- intercept
- shield aura
- anti-burst

### Striker

- dash
- isolate punish
- bleed / execute
- dive
- reset

### Ranger

- volley
- pierce
- trap
- focus fire
- anti-tank

### Mystic

- burst spell
- dot / curse
- cleanse / heal
- shield
- exposed-backline punish
- zone control

## canonical 용어

- heal scaling stat은 `heal_power`
- armor penetration 개념의 canonical stat id는 `phys_pen`
- resist penetration 개념의 canonical stat id는 `mag_pen`
- cadence용 canonical stat id는 `skill_haste`
- combat resource는 `Energy` 하나만 사용한다
- bonus health 계수는 `HealthCoeff`를 사용한다
- `duelist`는 canonical class id이고, 문서상 `Striker` family와 대응된다

## 계산식 초안

### 기본 공격

```text
RawAttackDamage =
WeaponBaseDamage
x AttackPatternMultiplier
x (1 + PhysPower / 100)
```

### 스킬 피해

```text
RawSkillDamage =
BaseDamage
+ (PhysCoeff x PhysPower)
+ (MagCoeff x MagPower)
+ (HealthCoeff x BonusHealth)
```

### 치유

```text
RawHeal =
BaseHeal
+ (HealCoeff x HealPower)
+ (SupportCoeff x RelevantPower)
```

### 보호막

```text
ShieldValue =
BaseShield
+ (HealthCoeff x BonusHealth)
```

또는

```text
ShieldValue =
BaseShield
+ (HealCoeff x HealPower)
```

### 방어 계산

```text
MitigatedDamage =
RawDamage x 100 / (100 + EffectiveArmorOrResist)
```

## 제한 규칙

- mitigation cap: `70%`
- negative defense floor: `-40%`
- crit 기본 배율: `150%`
- crit 허용 대상: 기본 공격과 지정 스킬
- 기본 금지 대상: DoT, 반사, 사망 폭발, 비지정 보조 효과
- `SignatureActive`는 energy 전용이고 cooldown 기반으로 바꾸지 않는다
- `FlexActive`는 cooldown/trigger 전용이고 energy 기반으로 바꾸지 않는다

## authoring 기준

- 모든 authored action은 `AuthorityLayer`, `ActivationModel`, `ActionLane`, `ActionLockRule`, `TargetRule`, `EffectDescriptor`를 명시한다.
- `SkillDefinitionAsset`은 `SignatureActive` 또는 `FlexActive`만 맡고, `BasicAttack`, `Passive`, `MobilityReaction`은 전용 definition이 소유한다.
- template를 explicit하게 쓰는 asset은 `RangeMin`, `RangeMax`, `Radius/Width/Arc`, `LearnSource`까지 같이 채운다.
- scaling이 있으면 `PhysCoeff`, `MagCoeff`, `HealCoeff`, `HealthCoeff` 중 필요한 값을 채운다.
- flex passive modifier와 호환 제한은 tag 기반으로 표현한다.
- pure combat truth는 Unity enum을 직접 참조하지 않고 compile 시 battle-side enum으로 번역한다.
