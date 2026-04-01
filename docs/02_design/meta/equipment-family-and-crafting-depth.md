# 장비 패밀리와 제작 깊이

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-31
- 소스오브트루스: `docs/02_design/meta/equipment-family-and-crafting-depth.md`
- 관련문서:
  - `docs/02_design/meta/item-and-affix-system.md`
  - `docs/02_design/meta/economy-protection-contract.md`
  - `docs/02_design/combat/skill-keywords-support-modifiers-and-weapon-restrictions.md`
  - `docs/02_design/systems/skills-items-and-passive-boards.md`

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

| 슬롯 | 수량 |
| --- | --- |
| `implicit` | `1` |
| `prefix` | `2` |
| `suffix` | `2` |

- unique / boss item은 numeric affix를 더 늘리지 않는다.
- 대신 `signature rule modifier` 1개를 가진다.

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

## 비목표

- separate shield slot
- socket / gem / orb zoo
- recipe tree 대량 확장
- launch floor 밖 family 실전 count 증가
