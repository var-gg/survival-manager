# augment 시스템

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-02
- 소스오브트루스: `docs/02_design/meta/augment-system.md`
- 관련문서:
  - `docs/02_design/meta/synergy-and-augment-taxonomy.md`
  - `docs/02_design/meta/augment-catalog-v1.md`
  - `docs/02_design/meta/item-passive-augment-budget.md`
  - `docs/02_design/systems/launch-content-scope-and-balance.md`

## 목적

이 문서는 run 중 temporary augment와 run 밖 permanent augment의 관계를 정의한다.
bucket taxonomy와 offer protection은 별도 문서가 소유하고, 이 문서는 augment가 “run 전체 규칙을 바꾸는 레버”라는 역할을 잠근다.

## 핵심 규칙

### temporary augment

first playable은 12개의 temporary augment를 둔다.

- NeutralCombat 3 (Frontline Doctrine, Kiting Manual, Battlefield Momentum)
- EconomyRoster 3 (Efficient Training, Backup Crew, Salvager)
- SynergyLinked 3 (Banner of Vanguard, Duel Protocol, Arcane Loop)
- WildcardRisk 3 (Blood Price, Glass Arsenal, Oath of Attrition)

temporary augment는 run 중에만 적용되는 강화이며, run overlay가 소유한다.
compiled battle snapshot에는 반영되지 않고 run overlay로만 적용된다.

### permanent augment

first playable은 4개의 permanent augment를 둔다.

- Citadel Doctrine (HoldLine)
- Guardian Detail (ProtectCarry)
- Breakthrough Orders (CollapseWeakSide)
- Night Hunt Mandate (AllInBackline)

permanent augment의 목적은 long-tail progression이 아니라 **blueprint identity lock**이다.

- unlock ownership: profile-wide
- equip ownership: squad blueprint
- equip slots in play: 1
- data model capacity: 3 유지 가능
- permanent augment는 compiled battle snapshot에 반영된다.

### 최초 선택 해금 문법

**temporary augment를 특정 계열에서 최초 선택하면, 관련 permanent augment 후보가 해금된다.**

예시 문법:

- run 중 `TempAugment.Fireline`을 처음 선택
- 계열 태그 `fireline`이 영구 해금 후보 풀에 추가
- expedition 종료 또는 특정 milestone에서 `PermAugment.Fireline_Core`가 후보로 등장 가능

즉 temporary augment는 단순 즉시 보상일 뿐 아니라, permanent augment 후보를 여는 "발견/해금 문법" 역할도 한다.

### augment 방향성 규칙

augment는 아래 두 방향을 모두 허용한다.

- 시너지 강화형
- 비활성 시너지 유닛 강화형

즉 활성 조합을 밀어주는 것과, 혼합 조합/외로운 유닛을 살리는 것 둘 다 시스템적으로 허용한다.

### augment bucket

- `NeutralCombat`
- `EconomyRoster`
- `SynergyLinked`
- `WildcardRisk`

작은 stat buff만 가진 augment를 양산하지 않는다.

## first playable 구현 범위

- temporary augment 3-choice 보상
- permanent augment equip slot 1
- temporary augment 최초 선택 -> related permanent candidate unlock 문법
- unlock은 run 종료 시 또는 milestone 시점에 permanent candidate가 제시
- permanent augment equip/unequip은 Town build-management에서 수행
- 일부 augment의 synergy 강화형 / off-synergy 보정형 방향 정의

## permanent augment lifecycle

1. run 중 temporary augment를 first pick하면 관련 permanent path가 해금된다.
2. run 종료 시 또는 milestone 시점에 permanent candidate가 제시된다.
3. 해금된 permanent augment는 profile-wide로 유지된다.
4. squad blueprint에서 permanent augment 1개를 장착한다.
5. 장착된 permanent augment는 compiled battle snapshot에 반영된다.
6. temporary augment는 run overlay만 소유하고 compile에 반영되지 않는다.

## 장기 규칙 (parked)

- permanent augment 다중 슬롯
- branch형 permanent tree
- account-level progression
- archetype-specific augment family 확장

## 밸런스 기준

- temporary augment가 기본 roster/gameplay를 덮어쓰면 안 된다.
- permanent augment는 희귀하고 기대감 있어야 하지만, 금방 power inflation을 만들면 안 된다.
- off-synergy augment도 실제 선택지가 되어야 한다.

## launch 기준 연결

- first playable은 temporary 12 / permanent 4다.
- paid launch floor는 temporary 18 / permanent 9다.
- paid launch safe target은 temporary 24 / permanent 12다.
- source별 power budget은 `docs/02_design/meta/item-passive-augment-budget.md`를 따른다.
- v1 catalog와 live subset 범위는 `augment-catalog-v1.md`를 따른다.
