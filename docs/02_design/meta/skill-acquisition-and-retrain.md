# skill acquisition and retrain

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/02_design/meta/skill-acquisition-and-retrain.md`
- 관련문서:
  - `docs/02_design/combat/skill-authoring-schema.md`
  - `docs/02_design/combat/resource-cadence-loadout.md`
  - `docs/02_design/meta/recruitment-and-reroll.md`
  - `docs/02_design/meta/reward-protection-and-acquisition-loop.md`
  - `docs/03_architecture/content-authoring-and-balance-data.md`

## 목적

이 문서는 자유선택형 스킬 편집과 모집 RNG의 중간 지점을 정의한다.
핵심은 `정체성은 고정하고, 일부 유연성만 남기며, 복구 가능성을 제공하는 것`이다.

## canonical policy

### locked core

- `BasicAttack`
- `SignatureActive`
- `SignaturePassive`
- `MobilityReaction`

### flex layer

- `FlexActive`
- `FlexPassive`

locked core는 archetype identity다.
flex layer는 recruit variance와 retrain의 대상이다.

## compile mapping

- `BasicAttack` -> archetype locked slot
- `SignatureActive` -> archetype locked energy active
- `FlexActive` -> mutable cooldown/trigger active
- `SignaturePassive` -> archetype locked passive
- `FlexPassive` -> mutable passive
- `MobilityReaction` -> archetype locked reaction

## recruit rule

- recruit 시 core는 고정한다.
- `FlexActive`, `FlexPassive`는 허용 풀에서 roll한다.
- 같은 archetype이라도 flex와 trait 차이로 미세 정체성이 갈린다.

## retrain rule

- 일반 재화: flex 1개 재훈련
- 희귀 재화: flex 2개 재설정
- core 변경: launch floor에서는 금지

## account / unlock rule

- class / archetype별 shared library unlock은 `FlexActive`, `FlexPassive` 후보 풀만 확장한다.
- unlock이 compile slot 수를 늘리지는 않는다.

## 금지 규칙

- 모집 RNG와 스킬 RNG를 둘 다 hard-lock해 `p * q` 피로를 만드는 것
- core와 flex를 모두 쉽게 바꿔 모든 유닛이 평평해지는 것
- battle compile topology를 6-slot 밖으로 늘리거나 줄이는 것
