# 장비 패밀리와 제작 깊이

- 상태: active
- 소유자: repository
- 최종수정일: 2026-07-16
- 소스오브트루스: `docs/02_design/meta/equipment-family-and-crafting-depth.md`
- 관련문서:
  - `docs/02_design/meta/item-and-affix-system.md`
  - `docs/02_design/meta/economy-protection-contract.md`
  - `docs/02_design/combat/skill-keywords-support-modifiers-and-weapon-restrictions.md`
  - `docs/02_design/systems/skills-items-and-passive-boards.md`
  - `pindoc://decision-equipment-presentation-v1-contract`
  - `pindoc://decision-equipment-content-v1-assetization-contract`

## 목적

이 문서는 launch floor 장비 구조와 deep crafting를 어디까지 열지 않을지 함께 고정한다.

## 장비 슬롯

- `weapon`
- `armor`
- `accessory`

shield 전용 별도 slot은 열지 않는다.

## weapon family floor

- `shield`
- `blade`
- `bow`
- `focus`

### safe target expansion

- `greatblade`
- `polearm`

`shield`는 separate slot이 아니라 `weapon family`다.

## affix 구조

V1 committed catalog는 affix asset `30`개를 유지하고, 그중 `25`개를 live roll 후보로 둔다.
reserved `5`개는 asset으로 남기되 `SpawnWeight = 0`, `ItemLevelMin = 999`로 live 생성에서 제외한다.

| tier | live 수량 | 역할 |
| --- | ---: | --- |
| `Implicit` | 6 | base item identity 축, `Refit` 대상 아님 |
| `Prefix` | 12 | 주요 수치/조건 보정, `Refit` 후보 |
| `Suffix` | 7 | 보조 보정/빌드 hook, `Refit` 후보 |

아이템 한 장의 표시 단위는 rarity/identity에 따라 아래처럼 제한한다.

| item group | 기본 affix 표시 |
| --- | --- |
| `Common` | `implicit + prefix` |
| `Rare` / `Named` | `implicit + prefix + suffix` |
| `Epic` / `Unique` | `implicit + prefix + prefix + suffix` |

- unique / boss item은 numeric affix를 더 늘리지 않는다.
- 대신 `signature rule modifier` 1개를 가진다.

## committed V1 item catalog

| 구분 | 수량 |
| --- | ---: |
| 전체 item | 42 |
| rarity `Common` | 30 |
| rarity `Rare` | 9 |
| rarity `Epic` | 3 |
| identity `Baseline` | 34 |
| identity `Named` | 6 |
| identity `Unique` | 2 |

## launch floor item identity

- base item은 무기 패밀리와 archetype identity의 뼈대를 만든다.
- affix는 수치 차이를 만든다.
- granted skill은 build 방향을 꺾는 한정된 수단으로 사용한다.
- unique / boss item은 수치 과적 대신 `granted skill` 또는 `unique rule modifier`를 우선한다.

## authoring 규칙

- `ItemBaseDefinition`은 canonical `WeaponFamilyTag`를 가진다.
- item은 optional `GrantedSkillId`를 가질 수 있다.
- unique / boss item은 `UniqueRuleModifierTag`를 사용한다.
- validator는 invalid weapon family, incompatible skill/weapon 조합, affix overfill을 막아야 한다.
- `EquipmentContentV1Contract`와 `EquipmentContentV1CatalogValidator`가 V1 수량, rarity, identity, live/reserved affix, required item drop을 막는다.

## 제작 깊이 V1

V1에서 실제로 열린 제작/보정 축은 `15 Echo` fixed-cost single-affix `Refit`뿐이다.
`Refit`은 `Prefix` 또는 `Suffix` 하나만 다시 고르며, `Implicit`은 base identity로 유지한다.
`Temper`, `Seal`, `Imprint`, `Salvage` operation은 authoring asset과 UI live surface에서 열지 않는다.

## 비목표

- separate shield slot
- socket / gem / orb zoo
- recipe tree 대량 확장
- launch floor 밖 family 실전 count 증가

## UI 표현 비목표

- `shield`는 weapon family badge로만 표시하고 equipment slot badge로 표시하지 않는다.
- `greatblade`, `polearm`은 안전한 후속 확장 후보지만 V1 UI class나 asset으로 선반영하지 않는다.
- 제작 깊이는 `15 Echo` single-affix refit CTA만 노출한다. `Temper`, `Seal`, `Imprint`, `Salvage` operation chip이나 recipe/material rail은 후속 결정 전까지 만들지 않는다.
- family별 ornate frame이나 rarity별 card frame을 만들지 않는다. item cell은 공통 L3 plate 위에 slot/family/rarity/identity state만 얹는다.
