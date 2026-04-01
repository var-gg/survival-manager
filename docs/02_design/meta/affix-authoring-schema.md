# affix authoring schema

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/02_design/meta/affix-authoring-schema.md`
- 관련문서:
  - `docs/02_design/meta/item-and-affix-system.md`
  - `docs/02_design/combat/authority-matrix.md`
  - `docs/02_design/combat/summon-ownership-and-deployables.md`
  - `docs/02_design/meta/affix-pool-v1.md`
  - `docs/03_architecture/content-authoring-and-balance-data.md`

## 목적

이 문서는 아이템 affix가 얼마나 복잡해질 수 있는지의 capacity를 넉넉하게 두되, launch floor item readability를 무너뜨리지 않는 schema를 정의한다.

Loop A 기준으로 affix는 `owner self` 또는 `owner-owned summons`만 수정할 수 있으며, teamwide/global/economy/offer rule을 건드릴 수 없다.

## 핵심 원칙

- POE/Torchlight의 조합성과 하이롤 감각은 차용한다.
- 여러 유닛을 동시에 관리하는 오토배틀인 만큼 텍스트 밀도는 강하게 낮춘다.
- 한 아이템의 핵심 affix 라인은 `2`, 최대 `3`이다.

## affix family

### `CoreScalar`

- 가장 읽기 쉬운 기본 수치
- affix 풀의 다수를 차지한다

### `ConditionalTagged`

- 특정 tag, status, range band, reposition, block, summon에 반응한다
- uptime을 고려해 core보다 조금 높은 budget을 허용한다

### `BuildShaping`

- play pattern을 바꾸는 드문 옵션
- 한 아이템에 2개 이상 중첩하지 않는다

## v1 public stat policy

- broad public layer는 아래 정도로 제한한다.
  - `max_health`
  - `armor`
  - `resist`
  - `phys_power`
  - `mag_power`
  - `heal_power`
  - `attack_speed`
  - `skill_haste`
  - `crit_chance`
  - `crit_multiplier`
  - `tenacity`
  - `move_speed`
- `dodge`, `block`, `status_potency`, `summon_power`는 v1 broad affix core로 바로 승격하지 않는다.

## schema field

| 필드 | 목적 |
| --- | --- |
| `Id`, `NameKey`, `DescriptionKey` | canonical identity |
| `Category`, `Tier` | 기존 slot/bucket taxonomy |
| `AffixFamily`, `EffectType` | schema axis |
| `ValueMin`, `ValueMax` | roll range |
| `AllowedSlotTypes` | slot gating |
| `RequiredTags`, `ExcludedTags` | tag gating |
| `ItemLevelMin`, `SpawnWeight` | pool control |
| `ExclusiveGroupId` | overlap lock |
| `BudgetScore` | tuning language |
| `TextTemplateKey` | concise line template |
| `AuthorityLayer` | affix authority 판별 |
| `Effects` | `Self`, `OwnerOwnedSummons` 한정 payload |
| `CompileTags`, `RuleModifierTags`, `Modifiers` | actual payload |

## authority closure

- 허용 scope: `Self`, `OwnerOwnedSummons`
- 허용 capability: `ModifyStats`, 제한적 `ApplyStatus`, `ModifyResource(Self)`, `ModifyCooldown(Self)`, `GrantPassive(Self)`
- 금지 scope: `AlliedRosterUnits`, `AlliedCombatants`, `EnemyCombatants`, `GlobalCombat`, `RewardPhase`, `ShopPhase`
- 금지 capability: `ModifyEconomyRule`, `ModifyOfferWeights`, `ModifyCompositionPayoff`, `SpawnSummon`, `SpawnDeployable`, `ModifyTargeting`

## overlap rule

- additive / multiplicative로 같은 의미가 폭주하지 않게 `ExclusiveGroupId`를 둔다.
- `ConditionalTagged`와 `BuildShaping`이 같은 payload를 다시 복제하면 안 된다.
- synergy 4-piece identity를 affix 1개가 그대로 복사하면 금지다.
- affix가 teamwide/global identity를 흉내 내면 authority violation으로 본다.

## live subset rule

- committed asset live subset은 `16~24 affix`면 충분하다.
- full catalog는 Markdown source-of-truth로 유지하고, asset runtime 진입은 subset만 허용한다.
