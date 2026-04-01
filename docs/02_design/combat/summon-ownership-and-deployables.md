# summon ownership과 deployable

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/02_design/combat/summon-ownership-and-deployables.md`
- 관련문서:
  - `docs/02_design/combat/authority-matrix.md`
  - `docs/02_design/combat/resource-cadence-loadout.md`
  - `docs/03_architecture/combat-state-and-event-model.md`
  - `docs/03_architecture/combat-harness-and-debug-contract.md`

## 목적

이 문서는 summon과 deployable을 roster unit과 분리된 category로 고정하고, count, credit, inheritance, despawn 규칙을 닫는다.

## entity category

| kind | 정의 |
| --- | --- |
| `RosterUnit` | 모집/편성/시너지 카운트 대상 본 유닛 |
| `OwnedSummon` | 이동 AI와 footprint를 가지는 독립 combatant |
| `Deployable` | stationary 또는 준-stationary combatant/object |
| `Projectile` | 단발 전달체, count/ownership 계산 제외 |

## eligibility 기본값

| 항목 | `OwnedSummon` | `Deployable` |
| --- | --- | --- |
| `CountsForRosterLimit` | `false` | `false` |
| `CountsForSynergyThresholds` | `false` | `false` |
| `EligibleForDirectAllyTargeting` | `false` | `false` |
| `EligibleForTeamAuras` | `true` | `true` |
| `OccupiesFootprintAndSlots` | `true` | `false` |
| `BlocksPathing` | 기본 `false` | `false` |

즉 direct targeted ally heal/buff는 기본적으로 summon/deployable을 잡지 않고, `AlliedCombatants` scope aura는 포함한다.

## mirrored kill credit

| 항목 | 기본 규칙 |
| --- | --- |
| actual killer | summon/deployable 자신 |
| mirrored owner credit | owner는 `MirroredOwnerKill` 또는 `MirroredOwnerAssist` credit를 받음 |
| score/quest/reward/economy | mirrored credit 사용 가능 |
| owner energy | 기본 미발생 |
| generic owner on-kill combat proc | 기본 미발동 |

owner on-kill combat proc이 mirrored kill을 보려면 해당 passive/effect가 명시적으로 `AllowMirroredOwnedSummonKill = true`를 가져야 한다.

`KillEventPayload`는 최소 아래를 포함한다.

- `actualKiller`
- `actualVictim`
- `mirroredOwner`
- `isMirroredFromOwnedSummon`
- `grantsOwnerEnergy`
- `grantsOwnerOnKillTriggers`

## stat inheritance

summon은 owner total stat을 복사하지 않고, summon definition의 base stat에 owner bonus stat 일부만 더한다.

| 필드 | 기본값 |
| --- | --- |
| `offenseBonusScalar` | `0.50` |
| `defenseBonusScalar` | `0.35` |
| `utilityBonusScalar` | `0.25` |
| `inheritCritChance` | `false` |
| `inheritDodge` | `false` |
| `inheritBlock` | `false` |

`bonus stat`은 gear, affix, synergy, augment, temporary buff 증가분만 포함하고 archetype base stat과 level base growth는 제외한다.

## concurrent cap

| category | `maxConcurrentPerSource` | `maxConcurrentPerOwner` | cap 초과 처리 |
| --- | --- | --- | --- |
| `OwnedSummon` | `2` | `4` | oldest same-source despawn 후 새 summon 생성 |
| `Deployable` | `1` | `2` | source cap 먼저, owner cap 다음 |

v1에서는 이 기본 cap보다 높게 authoring하지 않는다.

## owner death와 battle end

- owner 사망 시 owned summon/deployable은 `1.0s` 뒤 despawn
- battle end 시 즉시 despawn
- despawn 중에는 추가 score/loot credit를 생성하지 않는다

## summon chain 금지

- `summonGeneration > 0` entity는 persistent `SpawnSummon`과 persistent `SpawnDeployable`을 가질 수 없다.
- projectile 또는 `<= 1.0s` one-shot effect는 ownership chain을 남기지 않는 한 허용할 수 있다.
- 이 규칙은 validator가 import 단계에서 막아야 한다.

## summon topology

- `OwnedSummon`: fixed `BasicAttack` + optional `SignatureActive` + passive `0~1`
- `Deployable`: fixed periodic action 또는 trigger action만 허용
- summon/deployable은 flex slot, equipment, recruitment variation을 갖지 않는다
