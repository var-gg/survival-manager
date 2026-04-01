# unit blueprint schema

## 목적

`UnitArchetypeDefinition`이 Loop C의 governance contract를 어떤 필드로 담아야 하는지
정리한다.

## 필수 필드

- identity
  - `Id`
  - `DisplayName`
  - role fantasy를 읽을 수 있는 race/class/formation metadata
- loadout
  - fixed slot과 flex slot
  - signature/flex/passive/mobility resolution
- governance
  - `BudgetCard`
  - `BudgetCard.rarity`
  - `BudgetCard.roleProfile`
  - `BudgetCard.declaredThreatPatterns`
  - `BudgetCard.declaredCounterTools`
  - `BudgetCard.declaredFeatureFlags`

## BudgetCard rule

- `BudgetCard`가 canonical source다.
- `CombatRoleBudgetProfile`는 archetype 본문 바깥 별도 field로 두지 않는다.
- `Economy`는 항상 `0`이다.
- `CounterCoverage`는 declared counter tool이 있을 때만 양수다.

## rarity / topology rule

- `Common`: threat 1개 또는 counter 1개
- `Rare`: threat 1개 + counter 1개, 또는 counter 2개
- `Epic`: threat 2개, counter 2개까지 허용
- global team rule은 unit blueprint에서 금지한다

## validator fail 조건

- `BudgetCard` 없음
- role identity 35% / 50% 규칙 위반
- soup profile
- declared counter와 `CounterCoverage` 불일치
- forbidden feature flag 사용

## good / bad 예시

### good 1. Vanguard

- `Durability`, `Control` 중심
- `InterceptPeel:Light` 또는 `GuardBreakMultiHit:Light` 정도의 보조 역할

### good 2. Summoner

- `SustainedDamage`, `Support` 중심
- summon payload budget을 owner card 안에서 계상

### bad 1. Rare all-rounder

- burst, sustain, durability, control, mobility, support를 전부 높게 갖는 rare는 fail

### bad 2. Hidden economy unit

- recruit odds, gold income, reroll rate를 unit가 수정하면 Loop C 범위 밖이다
