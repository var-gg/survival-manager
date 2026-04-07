# 아이템, 패시브, 증강체 예산

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-07
- 소스오브트루스: `docs/02_design/meta/item-passive-augment-budget.md`
- 관련문서:
  - `docs/02_design/meta/item-and-affix-system.md`
  - `docs/02_design/meta/augment-system.md`
  - `docs/02_design/systems/launch-content-scope-and-balance.md`
  - `docs/03_architecture/content-authoring-and-balance-data.md`

## 목적

이 문서는 item, affix, passive board, augment의 런치 기준 볼륨과 source별 파워 예산을 고정한다.

## 출시 기준 수량

### paid launch floor

- equippables: `36`
- affixes: `24`
- passive boards: `4`
- passive nodes: `72`
- passive board shape: `12 small / 5 notable / 1 keystone`
- temporary augments: `18`
- permanent augments: `9`

### paid launch safe target

- equippables: `42~54`
- affixes: `30~36`
- passive boards: `4`
- passive nodes: `96`
- passive board shape: `14 small / 8 notable / 2 keystone`
- temporary augments: `24`
- permanent augments: `12`

## equippable 구조

- slot은 `weapon / armor / accessory`
- base item은 역할 뼈대를 만든다.
- affix는 같은 base item의 개체 차이를 만든다.
- unique는 큰 수치보다 규칙 변화와 granted skill 중심으로 설계한다.

## passive board 구조

- class board 4개를 유지한다.
- launch floor board당 기준 구조는 `small 12 / notable 5 / keystone 1`
- safe target board당 확장 구조는 `small 14 / notable 8 / keystone 2`
- passive node는 숫자 증폭보다 role specialization과 rule change를 우선한다.
- V1 runtime selection은 hero당 `최대 5개 active`, `keystone 최대 1`, `prerequisite 필수`, `mutual exclusion 필수`를 적용한다.
- passive board swap과 node toggle은 Town only / free로 유지한다.

## augment 구조

- temporary augment는 `silver / gold / platinum or prismatic` 3층으로 본다.
- permanent augment는 long-tail progression보다 `1-slot build identity thesis` 고정 수단으로 시작한다.
- temporary는 run 안의 변화, permanent는 run 밖의 준비를 담당한다.
- first temp pick은 same-family permanent candidate unlock trigger로 사용한다.
- permanent candidate unlock은 누적되지만 equip은 blueprint당 1개만 허용한다.

## 권장 파워 예산

- item 1개: 평균 `8~12%` personal power
- notable 1개: 평균 `3~6%`
- keystone 1개: 평균 `0~8%` + 규칙 변화
- synergy 2-piece: `5~8%`
- synergy 4-piece: `18~25%`
- silver augment: `6~8%`
- gold augment: `9~12%`
- platinum/prismatic augment: `13~18%`

## 금지 원칙

- 하나의 source가 단독으로 `20%+` power spike를 만드는 구조
- 같은 `% more` 계열이 item/passive/augment에서 3중 이상 중첩되는 구조
- unique가 granted skill도 rule tag도 없이 숫자만 큰 구조

## authoring acceptance

- `ItemBaseDefinition`은 `IdentityKind`를 명시한다.
- `IdentityKind == Unique`이면 `GrantedSkills`, `RuleModifierTags`, `UniqueRuleTags` 중 최소 하나를 가져야 한다.
- `AffixDefinition`은 `Category`와 slot applicability를 가져야 한다.
- `PassiveBoardDefinition`은 class canonical id를 owner로 가진다.
- `PassiveNodeDefinition`은 `NodeKind`, prerequisite, mutual exclusion tag를 authoring할 수 있어야 한다.
- explicit launch floor node roster는 `docs/02_design/meta/passive-board-node-catalog.md`가 소유한다.

## Loop C governance addendum

- affix budget window
  - `Common 6 ±1`
  - `Rare 10 ±1`
  - `Epic 14 ±2`
- augment budget window
  - `Common 18 ±2`
  - `Rare 26 ±3`
  - `Epic 34 ±3`
- synergy breakpoint budget
  - `2-piece = Standard 12 ±2`
  - `4-piece = Major 18 ±2`

boundary rule:

- item/drop rarity는 loot/economy/UI 체계로 남긴다.
- `ContentRarity`는 affix/augment/passive governance용이다.
- item `Unique` identity는 곧 combat `Legendary` rarity를 뜻하지 않는다.
- permanent augment power budget은 strong thesis를 허용하되 multi-slot stacking을 전제하지 않는다.
