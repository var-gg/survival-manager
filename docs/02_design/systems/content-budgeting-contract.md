# 콘텐츠 예산 계약

## 목적

Loop C의 `BudgetCard`는 승률 예측기가 아니라 authoring ledger다. 이 문서는
power creep, soup design, off-axis exception을 조기에 막는 공통 규칙을 정의한다.

## canonical enum

- `BudgetDomain`
  - `UnitBlueprint`
  - `Skill`
  - `Passive`
  - `Mobility`
  - `Affix`
  - `SynergyBreakpoint`
  - `Augment`
  - `Status`
- `ContentRarity`
  - `Common`
  - `Rare`
  - `Epic`
- `PowerBand`
  - `Micro`
  - `Minor`
  - `Standard`
  - `Major`
  - `Signature`
  - `Keystone`
- `BudgetAxis`
  - `SustainedDamage`
  - `BurstDamage`
  - `Durability`
  - `Control`
  - `Mobility`
  - `Support`
  - `CounterCoverage`
  - `Reliability`
  - `Economy`
  - `DrawbackCredit`
- `CombatRoleBudgetProfile`
  - `Vanguard`
  - `Bruiser`
  - `Duelist`
  - `Ranger`
  - `Arcanist`
  - `Support`
  - `Summoner`

## BudgetCard source of truth

- `BudgetCard`는 아래 authoring definition의 단일 governance source다.
  - `UnitArchetypeDefinition`
  - `SkillDefinitionAsset`
  - `PassiveDefinition`
  - `MobilityDefinition`
  - `AffixDefinition`
  - `AugmentDefinition`
  - `SynergyTierDefinition`
  - `StatusFamilyDefinition`
- `CombatRoleBudgetProfile`도 `BudgetCard` 안에 둔다.
- runtime combat template/snapshot은 full authoring object 대신 compact governance
  summary만 싣는다.

## PowerBand table

| PowerBand | Target | Tolerance | 기본 용도 |
| --- | ---: | ---: | --- |
| `Micro` | 4 | ±1 | 작은 rider, 좁은 status |
| `Minor` | 8 | ±1 | 작은 passive, light utility |
| `Standard` | 12 | ±2 | 2-piece synergy, 보통 affix cluster |
| `Major` | 18 | ±2 | 4-piece synergy, 큰 passive |
| `Signature` | 26 | ±3 | signature active, strong augment |
| `Keystone` | 34 | ±3 | epic keystone augment |

## Domain budget window

| Domain | 기준값 |
| --- | --- |
| `UnitBlueprint` | `Common 100 ±4`, `Rare 120 ±4`, `Epic 140 ±4` |
| `Affix` | `Common 6 ±1`, `Rare 10 ±1`, `Epic 14 ±2` |
| `Augment` | `Common 18 ±2`, `Rare 26 ±3`, `Epic 34 ±3` |
| `SynergyBreakpoint` | `2-piece = Standard`, `4-piece = Major` |

## role profile identity

| Role | Primary | Secondary |
| --- | --- | --- |
| `Vanguard` | `Durability`, `Control` | `CounterCoverage` |
| `Bruiser` | `SustainedDamage`, `Durability` | `Mobility` |
| `Duelist` | `BurstDamage`, `Mobility` | `Reliability` |
| `Ranger` | `SustainedDamage`, `Reliability` | `Mobility` |
| `Arcanist` | `BurstDamage`, `Control` | `Reliability` |
| `Support` | `Support`, `Reliability` | `Durability` |
| `Summoner` | `SustainedDamage`, `Support` | `CounterCoverage` |

validator는 아래를 강제한다.

- unit positive budget의 최소 35%는 primary axes에 있어야 한다.
- unit positive budget의 최소 50%는 primary + secondary axes에 있어야 한다.
- top 2 axis 중 최소 1개는 primary axis여야 한다.
- `SustainedDamage + BurstDamage + Durability + Control + Mobility + Support`를 모두
  동시에 높게 가진 soup profile은 fail이다.

## axis usage rule

- `UnitBlueprint`
  - `Economy = 0`
  - `CounterCoverage`는 실제 declared counter tool이 있을 때만 사용
  - `Reliability`는 target reach, cadence 안정성, failure risk 감소를 뜻한다
- `Skill / Passive / Mobility`
  - 양수 axis는 최대 3개
  - `Economy > 0` 금지
- `Affix`
  - 양수 axis는 최대 3개
  - `Economy > 0` 금지
  - `CounterCoverage`는 `Rare+`만 허용
- `Augment`
  - 양수 axis는 최대 4개
  - `Economy`는 augment에만 허용
- `SynergyBreakpoint`
  - `2-piece`는 direction
  - `4-piece`는 payoff
  - economy / offer / recruit rule을 건드리지 않는다

## drawback credit

- `DrawbackCredit`는 명시적 gameplay drawback만 인정한다.
- 한 definition당 최대 `8`까지만 허용한다.
- tooltip/doc에 drawback이 드러나지 않으면 credit를 줄 수 없다.
- 허용 예:
  - 긴 windup
  - self-root
  - health cost
  - setup requirement
  - conditional uptime
- 금지 예:
  - "잘 써야 함"
  - AI가 무시하는 downside
  - 체감되지 않는 미세 패널티

## DerivedSanityPreview

`DerivedSanityPreview`는 완전 sim이 아니라 lightweight heuristic ledger다.

- 입력:
  - damage coefficient
  - expected uptime
  - target shape / count
  - CC duration / reliability
  - mobility distance / trigger reliability
  - sustain / barrier
  - explicit counter strength
  - augment economy effect
- delta fail threshold:
  - `UnitBlueprint <= 8`
  - `Affix / Skill / Passive / Status / Mobility / SynergyBreakpoint <= 3`
  - `Augment <= 4`
- 계산 실패 branch는 warning으로 남기고, delta 초과는 error다.

## summon / rider costing

- summon/status rider는 standalone domain을 만들지 않는다.
- short-lived summon 1기는 base `2`를 owner definition에 더한다.
- persistent summon 또는 deployable body는 base `4`를 owner definition에 더한다.
- summon payload의 `Damage / Control / Support`는 `50% * uptime bucket`으로 owner
  budget에 가산한다.
- uptime bucket은 `0.5 / 0.75 / 1.0` 중 하나만 쓴다.
- rider status는 source definition의 secondary axis로 계상한다.
  - DoT rider -> `SustainedDamage`
  - CC rider -> `Control`
  - barrier/heal rider -> `Support`
  - `ArmorShred`, `Exposure`, `GuardBreak`, `MortalWound`, `Stable`, `Intercept`
    계열 -> `CounterCoverage`

## validator fail 조건

- required `BudgetCard`가 없다
- domain/rarity/power band window를 벗어난다
- derived delta threshold를 초과한다
- role identity 규칙을 못 맞춘다
- declared counter가 있는데 `counterCoverage = 0`이다
- `counterCoverage > 0`인데 declared counter가 없다
- domain별 axis count cap과 `Economy` 금지 규칙을 어긴다

## good / bad 예시

### good 1. Common bruiser unit

- `FinalScore 102`
- primary axis는 `SustainedDamage`, `Durability`
- secondary는 `Mobility`
- counter tool 없음
- keyword 2 이하

### good 2. Rare affix with local hook

- `FinalScore 10`
- scalar + one conditional rider
- `TrackingArea:Light` 한 개만 declared
- `Economy = 0`

### good 3. Epic augment keystone

- `FinalScore 34`
- `Economy` 또는 combat identity 중 한 축만 keystone 수준
- global recruit/offer mutation 없음

### bad 1. Rare unit soup profile

- burst, sustained, durability, control, mobility, support가 모두 높다
- rare여도 top2 axis가 primary와 무관하면 fail이다

### bad 2. Affix economy leak

- affix가 reroll cost, recruit odds, pity를 건드리면 fail이다

### bad 3. Free counter coverage

- `ArmorShred:Strong`를 declared했는데 `CounterCoverage = 1`이면 fail이다
