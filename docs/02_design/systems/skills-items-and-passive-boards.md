# 스킬, 아이템, 패시브 보드 빌드 모델

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/02_design/systems/skills-items-and-passive-boards.md`
- 관련문서:
  - `docs/02_design/meta/item-and-affix-system.md`
  - `docs/02_design/meta/item-passive-augment-budget.md`
  - `docs/02_design/meta/permanent-augment-progression.md`
  - `docs/02_design/combat/skill-taxonomy-and-damage-model.md`
  - `docs/02_design/combat/skill-keywords-support-modifiers-and-weapon-restrictions.md`
  - `docs/02_design/meta/equipment-family-and-crafting-depth.md`
  - `docs/02_design/systems/launch-content-scope-and-balance.md`
  - `docs/03_architecture/loadout-compiler-and-battle-snapshot.md`

## 목적

이 문서는 itemized 획득과 separate skill loadout, passive board 기반 영구 성장을 하나의 빌드 문법으로 정리한다.

## 규칙

- 스킬은 item처럼 획득할 수 있지만, 장착은 장비 슬롯과 별도다.
- 장비는 stat modifier와 granted skill 둘 다 제공할 수 있다.
- passive board는 hero별 장기 방향성을 만든다.
- passive node와 영구 augment는 전투 직전에 modifier package와 compile tag로 압축된다.
- flex passive modifier는 stable tag compatibility를 통과한 경우에만 `FlexPassive` slot에 들어간다.
- 무기 패밀리 제한과 class 제한은 compile 이전 validation에서 걸러진다.

## launch floor 범위

- equipment slot: `weapon / armor / accessory`
- skill slot: `BasicAttack / SignatureActive / FlexActive / SignaturePassive / FlexPassive / MobilityReaction`
- launch floor weapon family: `shield / blade / bow / focus`
- passive board: 중형 보드 + keystone 중심
- loadout compile 결과는 전투 입력과 replay audit에 모두 같은 hash로 남아야 한다.

## 출시 기준 연결

- 출시 기준 수량은 `docs/02_design/systems/launch-content-scope-and-balance.md`가 소유한다.
- slot taxonomy와 scaling 수식은 `docs/02_design/combat/skill-taxonomy-and-damage-model.md`가 소유한다.
- keyword/flex passive/weapon restriction은 `docs/02_design/combat/skill-keywords-support-modifiers-and-weapon-restrictions.md`가 소유한다.
- item/passive/augment 예산은 `docs/02_design/meta/item-passive-augment-budget.md`가 소유한다.
