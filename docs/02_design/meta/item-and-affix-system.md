# 아이템과 affix 시스템

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/02_design/meta/item-and-affix-system.md`
- 관련문서:
  - `docs/02_design/meta/affix-authoring-schema.md`
  - `docs/02_design/meta/affix-pool-v1.md`
  - `docs/02_design/meta/item-passive-augment-budget.md`
  - `docs/02_design/meta/equipment-family-and-crafting-depth.md`
  - `docs/02_design/meta/crafting-currencies-and-sinks.md`
  - `docs/02_design/systems/launch-content-scope-and-balance.md`

## 목적

이 문서는 아이템 방향성과 launch floor 범위를 정의한다.
affix field schema와 catalog는 별도 문서가 소유하고, 이 문서는 item readability와 경계를 잠근다.

## launch floor 구현 범위

- base item
- `implicit 1 + prefix 2 + suffix 2`
- `weapon / armor / accessory` 3슬롯
- `shield / blade / bow / focus` weapon family
- granted skill
- `gold`, `ember_dust`, `echo_crystal`, `boss_sigil` 기반 crafting contract

launch floor에서는 아직 하지 않는다.

- 복잡한 rarity ladder
- recipe crafting
- socket/gem 시스템
- set bonus

## 역할 차별화 규칙

같은 archetype도 다음 요소 조합으로 역할이 달라져야 한다.

- trait roll
- item
- augment

아이템은 이 중 장비 축을 담당한다.

## 장비 슬롯

- weapon
- armor
- accessory

shield 전용 별도 슬롯은 열지 않는다.

## affix와 unique 규칙

- affix slot은 `implicit 1`, `prefix 2`, `suffix 2`를 상한으로 둔다.
- 아이템 한 장의 핵심 affix 라인은 `2`, 최대 `3`이다.
- affix family는 `CoreScalar`, `ConditionalTagged`, `BuildShaping`으로 나눈다.
- unique / boss item은 numeric affix를 늘리지 않고 `signature rule modifier` 1개를 사용한다.
- item authoring은 canonical `WeaponFamilyTag`, optional `GrantedSkillId`, optional `UniqueRuleModifierTag`를 가진다.
- `dodge`, `block`, `status_potency`, `summon_power`는 v1 broad affix public layer로 성급히 승격하지 않는다.

## 재련/리롤 원칙

- `gold`는 broad sink로 유지한다.
- item crafting에는 `ember_dust`, `echo_crystal`, `boss_sigil`을 사용한다.
- 값싼 무한 reroll은 금지한다.
- launch floor에서는 "아이템을 다듬는 느낌"만 제공한다.
- crafting 시스템 전체를 열지는 않는다.

## 장기 규칙

- broader base item family
- rarity ladder
- material sink 확장
- advanced crafting station
- full catalog와 live subset 분리는 `affix-pool-v1.md`를 따른다.

## 밸런스 기준

- 아이템이 trait/augment보다 너무 강하면 안 된다.
- 반대로 아이템이 너무 약해서 존재감이 없어도 안 된다.
- unique는 큰 수치보다 granted skill과 rule change를 우선한다.
