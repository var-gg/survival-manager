# authority matrix

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/02_design/combat/authority-matrix.md`
- 관련문서:
  - `docs/02_design/combat/skill-authoring-schema.md`
  - `docs/02_design/meta/affix-authoring-schema.md`
  - `docs/02_design/meta/synergy-and-augment-taxonomy.md`
  - `docs/03_architecture/content-authoring-and-balance-data.md`

## 목적

이 문서는 Loop A 기준의 authority boundary를 고정한다.
핵심 규칙은 `UnitKit가 identity를 정하고, Skill이 local action을 정하고, Affix가 self/local을 수정하고, Synergy가 comp payoff를 주고, Augment만 global combat/economy rule을 바꾼다`는 것이다.

## canonical layer

| layer | 허용 scope | 허용 capability | 금지 |
| --- | --- | --- | --- |
| `UnitKit` | 자기 자신의 loadout, behavior, targeting, footprint | slot topology 정의, identity 정의 | economy, offer, reward, teamwide/global rule, composition threshold 변경 |
| `Skill` | `Self`, `CurrentTarget`, `GroundArea`, 제한적 `AlliedCombatants`, `EnemyCombatants` | `DealDamage`, `HealOrBarrier`, `ApplyStatus`, `Reposition`, `ModifyResource(Self)`, `ModifyCooldown(Self)`, `SpawnSummon`, `SpawnDeployable` | `ModifyEconomyRule`, `ModifyOfferWeights`, `ModifyCompositionPayoff`, `GlobalCombat` scope |
| `Affix` | `Self`, `OwnerOwnedSummons` | `ModifyStats`, 제한적 `ApplyStatus`, `ModifyResource(Self)`, `ModifyCooldown(Self)`, `GrantPassive(Self)` | teamwide/global/economy/offer/composition rule, summon/deployable spawn, targeting 변경 |
| `Synergy` | `AlliedRosterUnits`, `AlliedCombatants`, `EnemyCombatants` | `ModifyStats`, `ApplyStatus`, `GrantPassive`, `ModifyCompositionPayoff` | economy, offer, recruit/shop/drop, global combat rule 변경 |
| `Augment` | synergy가 다룰 수 있는 scope 전체 + `GlobalCombat`, `RewardPhase`, `ShopPhase` | synergy capability 전체 + `ModifyGlobalCombatRule`, `ModifyEconomyRule`, `ModifyOfferWeights` | roster unit loadout topology 변경, core slot 증설, persistent summon chain 허용 |
| `Status` | combat state 한정 | action restriction, scalar modifier, immunity window | economy, offer, reward 직접 수정 |

## validator contract

`ContentDefinitionValidator`와 `LoopAContractValidator`는 아래 위반을 import 단계에서 막아야 한다.

| 케이스 | 기대 결과 |
| --- | --- |
| affix가 `AlliedRosterUnits`를 buff | validation error |
| affix가 `GlobalCombat`을 변경 | validation error |
| synergy가 shop reroll cost를 변경 | validation error |
| skill이 reward/drop rule을 변경 | validation error |
| augment가 roster slot 수를 늘림 | validation error |

## effect descriptor rule

- skill, affix, synergy, augment, status authoring은 `AuthorityLayer`, `EffectScope`, `EffectCapability`를 판별 가능한 descriptor를 가져야 한다.
- descriptor가 없으면 validator는 해당 payload를 `미분류 effect`로 간주하고 error를 낸다.
- `Affix`는 `OwnerOwnedSummons`를 건드릴 수 있지만, owner가 소유하지 않은 summon/deployable이나 팀 전체를 건드릴 수 없다.

## 운영 메모

- authority는 UI/tooling 편의가 아니라 validator와 runtime truth를 묶는 계약이다.
- design 문서에서 예시를 들 때도 teamwide/global/economy 효과는 반드시 `Augment`로 분류한다.
- summon 관련 세부 규칙은 `summon-ownership-and-deployables.md`가 소유한다.
