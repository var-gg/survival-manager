# 아이템과 affix 시스템

- 상태: active
- 소유자: repository
- 최종수정일: 2026-07-26
- 소스오브트루스: `docs/02_design/meta/item-and-affix-system.md`
- 관련문서:
  - `docs/02_design/meta/affix-authoring-schema.md`
  - `docs/02_design/meta/affix-pool-v1.md`
  - `docs/02_design/meta/item-passive-augment-budget.md`
  - `docs/02_design/meta/equipment-family-and-crafting-depth.md`
  - `docs/02_design/meta/economy-protection-contract.md`
  - `docs/02_design/systems/launch-content-scope-and-balance.md`
  - `pindoc://decision-equipment-presentation-v1-contract`
  - `pindoc://decision-equipment-content-v1-assetization-contract`

## 목적

이 문서는 아이템 방향성과 launch floor 범위를 정의한다.
affix field schema와 catalog는 별도 문서가 소유하고, 이 문서는 item readability와 경계를 잠근다.

## launch floor 구현 범위

- base item
- item catalog `42`: `Common 30 / Rare 9 / Epic 3`
- item identity `42`: `Baseline 34 / Named 6 / Unique 2`
- affix catalog `44`: live `44`, reserved `0`
- live affix mix: `Implicit 9 / Prefix 22 / Suffix 13`
- live family mix: `CoreScalar 20 / ConditionalTagged 6 / BuildShaping 18`
- `weapon / armor / accessory` 3슬롯
- `shield / blade / bow / focus` weapon family
- granted skill
- Town equip / swap
- floor schedule과 chapter economy로 계산되는 magnitude Reforge
- 선택한 affix magnitude를 고정하고 나머지만 다시 굴리는 Seal

launch floor에서는 아직 하지 않는다.

- 복잡한 rarity ladder
- recipe crafting
- socket/gem 시스템
- set bonus
- material crafting rail

## 역할 차별화 규칙

같은 archetype도 다음 요소 조합으로 역할이 달라져야 한다.

- trait roll
- item
- passive board
- permanent augment

아이템은 이 중 장비 축을 담당한다.

## 장비 슬롯

- weapon
- armor
- accessory

shield 전용 별도 슬롯은 열지 않는다.

## affix와 unique 규칙

- `implicit`은 base item identity에 붙는 고정 축이고, V1 `Refit` 대상이 아니다.
- `prefix / suffix`만 `15 Echo` single-affix refit 후보가 된다.
- 아이템 생성 시 Common은 `implicit + prefix`, Rare/Named는 `implicit + prefix + suffix`, Epic/Unique는 `implicit + prefix + prefix + suffix`를 기본 표시 단위로 쓴다.
- affix family는 `CoreScalar`, `ConditionalTagged`, `BuildShaping`으로 나눈다.
- unique / boss item은 rarity가 아니라 identity다. 수치 과적 대신 granted skill 또는 rule marker를 우선한다.
- item authoring은 canonical `WeaponFamilyTag`, optional `GrantedSkillId`, optional `UniqueRuleModifierTag`를 가진다.
- 아이템은 증강처럼 새 proc 동사를 추가하지 않고, `status_potency`로 적용자가 거는 숫자 상태의 magnitude를 `×(1 + potency)` 증폭한다.
- V1 `status_potency`는 `affix_hallowed` 단일 carrier로 좁게 live화한다. magnitude가 0인 제어 상태는 곱셈 결과도 0이라 무영향이다.
- `dodge`, `block`, `summon_power`는 v1 broad affix public layer로 성급히 승격하지 않는다.

## 재련/리롤 원칙

- item tuning은 `Echo` rail 위의 light correction으로만 둔다.
- Reforge는 affix identity를 유지하고 모든 instance magnitude를 다시 굴린다.
- Seal은 선택한 affix magnitude의 float bit를 보존하고 나머지 magnitude만 다시 굴린다.
- Seal 비용은 같은 Reforge bundle 비용에 `1 + (저작 multiplier × 잠금 수)`를 곱해 올림한다. multiplier는 `refit_balance.asset`이 소유한다.
- 빈 Seal 선택은 같은 command seed의 Reforge와 bit-identical이어야 한다.
- recruit / retrain / refit이 각각 외부 파워 / flex 보정 / 장비 보정 역할을 나눠 가진다.
- launch floor normal lane에서는 `EmberDust`, `EchoCrystal`, `BossSigil` 같은 material currency를 live sink로 올리지 않는다.
- crafting 시스템 전체는 later scope로 민다.

## 구현 고정점

- Pindoc 결정 `pindoc://decision-equipment-content-v1-assetization-contract`가 item/affix/drop/refit V1 자산화 계약을 소유한다.
- repo 구현은 `EquipmentContentV1Contract`, `EquipmentContentV1Assetizer`, `EquipmentContentV1CatalogValidator`가 같은 숫자와 ID manifest를 공유한다.
- `SampleSeedGenerator.Generate()`는 sample content 재생성 후 V1 assetizer를 다시 적용한다.
- drop table의 item entry는 `RewardType.Item`이어야 하며, item 획득 시 runtime이 affix id를 생성한다.
- `AffixPoolTag`는 item family의 exact selector key다. affix는 기존 `CompileTags`의 stable pool tag로 호환 pool을 선언하며, 생성과 padding은 같은 canonical candidate graph를 사용한다.
- `AllowedCraftOperations`는 `RefitService`가 Reforge/Seal 실행 전에 검사한다.
- `InventoryItemRecord`는 현재 affix identity, instance magnitude roll, Refit level만 소유한다. Seal 선택과 attempt/seed/cost 입력은 `SaveProfile.ItemCraftOperations` ledger에 별도로 저장한다.

## UI 표현 계약

장비 UI는 `pindoc://decision-equipment-presentation-v1-contract`를 따른다.

- Inventory/EquipmentRefit이 노출하는 현재 범주는 `slot`, `weapon family`, `common / rare / epic` 표현 rarity, `baseline / named / unique` identity, `implicit / prefix / suffix` affix tier, `selected / equipped / refit-target`, Reforge quote까지다.
- `Magic`과 `Legendary`는 enum 호환값일 수 있지만 V1 live visual tier가 아니다. UI는 fallback 렌더링을 제공하되 별도 frame/rarity class를 만들지 않는다.
- `Unique`는 rarity가 아니라 identity 또는 signature rule marker다.
- `InventoryItemRecord`에 source나 영구 lock 상태가 없으므로 provenance badge와 persistent locked overlay는 표시하지 않는다.
- Seal은 service와 persistence까지 live지만 현재 presenter에는 affix 선택 UI가 없다. 후속 UX 작업이 selection state, quote, confirmation, locked overlay를 추가하기 전에는 플레이어 UI에서 노출하지 않는다.
- `Temper / Imprint / Salvage`, recipe rail, material rail은 별도 service와 persistence가 생기기 전까지 UI에 열지 않는다.
- 아이템 cell과 affix row는 L3/L4 표면이다. L1 modal frame, ornate corner flourish, rarity별 gold frame을 재사용하지 않는다.

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
