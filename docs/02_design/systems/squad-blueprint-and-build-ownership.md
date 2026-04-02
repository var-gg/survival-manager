# squad blueprint와 빌드 소유권

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-02
- 소스오브트루스: `docs/02_design/systems/squad-blueprint-and-build-ownership.md`
- 관련문서:
  - `docs/01_product/vision.md`
  - `docs/02_design/combat/team-tactics-and-unit-rules.md`
  - `docs/03_architecture/loadout-compiler-and-battle-snapshot.md`

## 목적

이 문서는 deck 용어를 `squad blueprint`와 `build`로 교체하고, 영구 성장, 런 오버레이, 전투 입력의 소유권을 분리한다.

## 핵심 모델

- `SquadBlueprint`는 출전 4인, 후보 4인, team posture, team tactic, anchor 배치, hero role 지시를 가진다.
- hero build는 장비, 스킬 loadout, passive board 선택, 영구 augment 장착으로 구성된다.
- temporary augment, node 상태, pending reward는 run overlay가 소유한다.
- 전투는 항상 `compiled battle snapshot`만 소비한다.

## 소유권 규칙

- 영구 augment 해금은 프로필 공용이다.
- 영구 augment 장착은 squad blueprint 귀속이다.
- passive board는 영웅별 설정이다.
- PvP가 도입되더라도 temporary augment는 blueprint truth에 포함하지 않는다.

## compile ownership

- permanent augment는 compiled battle snapshot에 반영된다.
- temporary augment는 run overlay만 소유하고 compile에 반영되지 않는다.
- 같은 archetype이라도 장비, retrain, passive board, permanent augment에 따라 compile 결과가 달라져야 한다.
- compile 결과에는 RoleVariantTag(unit 역할)와 TeamPostureTag(팀 운영 방식)가 포함되어야 한다.

## Town build-management ownership

Town에서 수행 가능한 build-management 기능은 아래와 같다.

- **Recruit** — 새 영웅을 roster에 영입
- **Scout** — recruit pool 리롤
- **Retrain** — 영웅의 flex skill 재배치
- **Dismiss** — 영웅을 roster에서 제거 (Echo 전환)
- **Equip / Unequip** — 아이템 장착/해제
- **Refit** — Echo를 사용해 아이템의 affix 1개를 재굴림
- **Passive Board assign** — 영웅별 class board 노드 선택
- **Permanent Augment equip / unequip** — squad blueprint에 permanent augment 장착/해제

이 기능들이 same-archetype variance와 squad blueprint 정체성을 만드는 핵심 경로다.

## Refit contract

- 이름: Refit
- 위치: Town build-management
- 통화: Echo만 사용
- 기능: 아이템의 affix 1개만 재굴림
- 금지: base item 변경, slot 변경, granted skill 변경, recipe crafting, socket/gem
- 목적: 같은 archetype을 다른 역할로 재배치하는 최소한의 tuning. full crafting이 아니라 RNG recovery + build variance 도구.

## MVP 기본값

- squad blueprint 장착 영구 augment 슬롯 운용값은 `1`이다.
- 데이터 모델은 슬롯 `3`까지 확장 가능해야 한다.
- 스킬 topology는 `BasicAttack / SignatureActive / FlexActive / SignaturePassive / FlexPassive / MobilityReaction`으로 고정한다.

## same-archetype variance 목표

build ownership의 궁극적 목표는 같은 archetype이 build에 따라 다른 역할로 compile되는 것이다.
이 차이는 단순 스탯 차이만으로 끝내지 않고 semantic tag(RoleVariantTag)로도 드러나야 한다.
