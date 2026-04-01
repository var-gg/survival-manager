# 전투 메커니즘 용어집

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/02_design/combat/combat-mechanics-glossary.md`
- 관련문서:
  - `docs/02_design/combat/resource-cadence-loadout.md`
  - `docs/02_design/combat/summon-ownership-and-deployables.md`
  - `docs/02_design/combat/skill-taxonomy-and-damage-model.md`
  - `docs/03_architecture/combat-state-and-event-model.md`
  - `docs/03_architecture/status-runtime-stack-and-cleanse-rules.md`

## 목적

crit, dodge, block, energy, summon ownership 관련 핵심 용어를 v1 기준으로 명확히 고정한다.

## 정의

- `Crit`
  - 공격자 측 기대값 상승
  - v1에서는 public stat `crit_chance`, `crit_multiplier`를 사용한다.
- `Dodge / Evasion`
  - 드문 full avoid
  - 저체급/민첩 archetype에만 높은 값을 준다.
  - v1에서는 hidden behavior coefficient다.
- `Block / Guard`
  - 방어자 identity
  - v1에서는 facing arc 없이 `mitigation + internal cooldown`만 쓴다.
  - v1에서는 hidden behavior coefficient다.
- `Armor / Resistance`
  - 가장 예측 가능한 기본 방어
  - public stat이다.
- `Tenacity / Stability`
  - tenacity는 CC duration 쪽, stability는 hit reaction/guard identity 쪽 숨은 계수다.
- `Energy`
  - v1의 유일한 explicit combat resource다.
  - `SignatureActive`만 energy 100을 사용한다.
  - 기본 시작값은 10이고, 기본 공격 resolve, 피격, actual kill, assist로만 오른다.
- `AttackSpeed`
  - basic attack cycle만 바꾸는 stat이다.
- `SkillHaste`
  - `FlexActive`, `MobilityReaction`의 cooldown recovery만 바꾸는 stat이다.
  - `cooldown_recovery`는 migration alias이며 canonical stat id는 `skill_haste`다.
- `OwnedSummon`
  - owner가 소유하지만 roster count와 synergy count에는 포함되지 않는 combatant다.
- `Deployable`
  - stationary 또는 준-stationary object이며 ownership/credit rule은 summon 문서를 따른다.
- `Mirrored Kill Credit`
  - summon/deployable이 kill했을 때 owner가 score/reward용으로만 받는 반사 credit다.
  - 기본적으로 owner energy와 generic owner on-kill proc는 발생시키지 않는다.

## resolution order

1. target validity / range
2. dodge
3. crit
4. block / guard
5. armor / resistance
6. on-hit / status
7. kill / assist / mirrored credit

## cap 정책

- dodge는 hard cap 또는 낮은 authored baseline만 허용한다.
- block chance는 internal cooldown과 함께만 쓴다.
- dodge와 block을 동시에 높게 올리는 archetype은 만들지 않는다.
- accuracy / hit rate는 v1 범위에서 제외한다.
- persistent summon chain은 v1 범위에서 제외한다.

## Loop C answer vocabulary

Loop C의 counter-system source-of-truth는
`docs/02_design/combat/counter-system-topology.md`다.

| 용어 | 뜻 |
| --- | --- |
| `ArmorShred` | armor 감소 또는 armor ignore |
| `Exposure` | resistance 감소 또는 magical vulnerability |
| `GuardBroken` | guard 효율 붕괴 상태 |
| `TrackingArea` | tracking projectile 또는 persistent area delivery |
| `Stable` | CC/displacement 저항 상태 |
| `MortalWound` | heal/regen/barrier 효율을 줄이는 anti-sustain 상태 |
| `Intercepting` | dive target 대신 위협을 받아내는 peel 상태 |
| `Cleave` / `Waveclear` | status가 아니라 delivery/property category |

## Loop C 금지 용어

- `TrueDamage`
- `AccuracyRoll`
- `Revive`
- `CooldownReset`
- `MindControl`

위 용어는 glossary에 이름이 있어도 V1 authoring에서는 forbidden policy가 우선한다.
