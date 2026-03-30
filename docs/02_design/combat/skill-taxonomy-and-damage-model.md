# 스킬 taxonomy와 데미지 모델

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-30
- 소스오브트루스: `docs/02_design/combat/skill-taxonomy-and-damage-model.md`
- 관련문서:
  - `docs/02_design/combat/team-tactics-and-unit-rules.md`
  - `docs/02_design/combat/stat-system-and-power-budget.md`
  - `docs/02_design/systems/launch-content-scope-and-balance.md`
  - `docs/03_architecture/loadout-compiler-and-battle-snapshot.md`

## 목적

이 문서는 skill loadout contract, class skill family, damage/heal/shield 계산식의 canonical 용어를 고정한다.

## loadout contract

출전 유닛의 skill loadout은 전투 직전에 아래 4슬롯으로 compile된다.

- `core_active`
- `utility_active`
- `passive`
- `support`

설계 의도는 `2 active + 1 passive/trigger + 1 support modifier`다.
`SM.Meta.LoadoutCompiler`는 이 4슬롯 밖의 저장 표현을 읽더라도 compile 시 위 계약으로 normalize한다.

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

- `NearestEnemy`
- `LowestHpEnemy`
- `MostExposedEnemy`
- `LowestHpAlly`
- `ProtectedAlly`
- `Self`
- `MarkedTarget`

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
- mana deny
- zone control

## canonical 용어

- heal scaling stat은 `heal_power`
- armor penetration 개념의 canonical stat id는 `phys_pen`
- resist penetration 개념의 canonical stat id는 `mag_pen`
- bonus health 계수는 `HealthCoeff`를 사용한다.
- `duelist`는 canonical class id이고, 문서상 `Striker` family와 대응된다.

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

## authoring 기준

- 모든 `SkillDefinitionAsset`은 `Kind`, `SlotKind`, `DamageType`, `Delivery`, `TargetRule`를 명시한다.
- scaling이 있으면 `PhysCoeff`, `MagCoeff`, `HealCoeff`, `HealthCoeff` 중 필요한 값을 채운다.
- support modifier와 호환 제한은 tag 기반으로 표현한다.
- pure combat truth는 Unity enum을 직접 참조하지 않고 compile 시 battle-side enum으로 번역한다.
