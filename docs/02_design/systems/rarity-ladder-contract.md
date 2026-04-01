# 희귀도 사다리 계약

## 목적

Loop C의 `ContentRarity`는 단순 수치 증폭이 아니라 어떤 축을 얼마나 건드릴 수
있는지를 정하는 governance rarity다.

## canonical rarity

| Rarity | 의미 |
| --- | --- |
| `Common` | baseline efficiency, 낮은 complexity |
| `Rare` | baseline + specialization + counter/hook 1개 |
| `Epic` | baseline + specialization + local build-shaping hook 1개 |

- `Legendary`, `Unique`, `Mythic` 같은 추가 combat rarity는 V1에서 금지한다.
- item/drop rarity는 별도 체계로 남긴다.

## boundary rule

- `RecruitTier`
  - offer generation
  - pity
  - save serialization
  - player-facing badge
- `ContentRarity`
  - validator
  - compiler
  - runtime combat template
  - battle snapshot
  - debug overlay
- 금지:
  - runtime combat spec/snapshot에 `RecruitTier`를 싣는 것
  - player-facing town recruit card에 `ContentRarity`를 직접 노출하는 것

## complexity cap

| Rarity | keyword | condition | exception | unit counter tool | affix counter tool |
| --- | ---: | ---: | ---: | --- | --- |
| `Common` | 2 | 1 | 0 | 최대 1개 | 0개 |
| `Rare` | 3 | 2 | 1 | 최대 2개, 둘째는 `Light` | 최대 1개 |
| `Epic` | 4 | 2 | 1 | 최대 2개 | 최대 1개 |

## domain rule

- `UnitBlueprint`
  - `Common`: threat 1개 또는 counter 1개
  - `Rare`: threat 1개 + counter 1개, 또는 counter 2개
  - `Epic`: counter 2개까지 허용, global team rule 금지
- `Affix`
  - `Common`: scalar 또는 single-condition rider
  - `Rare`: scalar + conditional 또는 local counter hook
  - `Epic`: owner-local build-shaping rule 1개
- `Augment`
  - `Common`: comp accent
  - `Rare`: direction + strong hook
  - `Epic`: 한 축을 재편하는 keystone
  - econ keystone + combat keystone 동시 제공 금지
- `SynergyBreakpoint`
  - `2-piece`: 방향 제시
  - `4-piece`: 조합 payoff
  - `2 / 4` 외 breakpoint 금지

## UI surface rule

- `Common`: 요약 1줄 + 상세 1줄 이내
- `Rare`: 요약 1줄 + 상세 2줄 이내
- `Epic`: 요약 1줄 + 상세 2줄 + key hook 1줄 이내

tooltip이 장문의 법률 문서가 되면 rarity가 올라가도 fail이다.

## validator fail 조건

- `ContentRarity`가 `Common/Rare/Epic` 외 값이다
- domain별 complexity cap을 넘는다
- declared counter count cap을 넘는다
- local build-shaping allowance를 넘는다
- `RecruitTier`와 `ContentRarity`를 runtime combat path에서 혼용한다

## good / bad 예시

### good 1. Common skill

- keyword 2개 이하
- single target hit + light slow
- counter tool 없음

### good 2. Rare unit

- specialization이 뚜렷하다
- `ArmorShred:Standard` 1개와 threat 1개만 가진다
- common의 장점을 모두 상위호환하지 않는다

### good 3. Epic affix

- owner-local build hook 1개
- `Economy`, recruit, pity, loadout topology는 건드리지 않는다

### bad 1. Common text wall

- keyword 4개, 조건 3개, 예외 2개면 common이 아니다

### bad 2. Rare everything-plus-one

- common의 모든 장점을 더 잘하고 counter 두 개, build hook까지 가지면 fail이다

### bad 3. Epic player-facing rarity leak

- town recruit card에 `Epic`을 combat rarity로 직접 찍으면 boundary 위반이다
