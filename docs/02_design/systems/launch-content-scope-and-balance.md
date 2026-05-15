# 출시 기준 콘텐츠 스코프와 밸런스 허브

- 상태: active
- 소유자: repository
- 최종수정일: 2026-05-08
- 소스오브트루스: `docs/02_design/systems/launch-content-scope-and-balance.md`
- 관련문서:
  - Pindoc strategy/product artifacts
  - `docs/02_design/systems/launch-floor-content-matrix.md`
  - Pindoc character-lore / roster artifacts
  - `docs/02_design/combat/resource-cadence-loadout.md`
  - `docs/02_design/combat/skill-taxonomy-and-damage-model.md`
  - `docs/02_design/meta/item-passive-augment-budget.md`
  - `docs/02_design/meta/synergy-breakpoints-and-soft-counters.md`
  - `docs/03_architecture/content-authoring-and-balance-data.md`

## 목적

이 문서는 `survival-manager`의 출시 기준 콘텐츠 볼륨과 밸런스 문법을 한 장으로 고정한다.
제품 문서의 MVP 값은 유지하되, 유료 출시 기준 floor와 safe target은 이 문서가 소유한다.

## 범위 계층

| 구분 | current MVP minimum | paid launch floor | paid launch safe target |
| --- | --- | --- | --- |
| races | 3 | 3 | 3 |
| classes | 4 | 4 | 4 |
| recruit archetypes | 12 | 12 | 16 |
| core archetypes | n/a | 12 | 12 |
| specialist archetypes | n/a | 0 | 4 |
| skills | MVP 문서 비소유 | 40 | 40~48 |
| equippables | MVP 문서 비소유 | 36 | 42~54 |
| passive boards | MVP 문서 비소유 | 4 | 4 |
| passive nodes | MVP 문서 비소유 | 72 | 96 전후 |
| temporary augments | 24 | 18 | 24 |
| permanent augments | live slot 1 / authored candidates 12 | 9 | 12 |
| permanent augment equip slots | 1 | 1~2 | 데이터 모델 3, 운용 1~3 |
| equipment slots | 3 | 3 | 3 |
| synergy families | 7 | 7 | 7 |
| synergy breakpoints | MVP 문서 참조 | race `2 / 4`, class `2 / 3` | race `2 / 4`, class `2 / 3` |

temporary augment는 GPT Pro P0 dial pass 이후 V1 prototype에서 먼저 safe target 규모인 24개를 실험한다. paid launch floor 숫자는 출시 최소선 비교 기준으로 남기며, 현재 prototype live pool이 이 항목에서는 floor를 초과한다.

## 현재 패스에서 고정하는 핵심

- canonical class id는 `vanguard / duelist / ranger / mystic`를 유지한다.
- 문서상 역할 이름으로는 `Vanguard / Striker / Ranger / Mystic`를 쓴다.
- penetration stat id는 `phys_pen / mag_pen`을 유지한다.
- 모든 roster unit은 6-slot loadout topology를 가진다.
- 가시적 active는 `SignatureActive`, `FlexActive` 두 개만 사용한다.
- synergy는 `3 race + 4 class`의 총 7 family를 유지한다.
- counter 설계는 hard counter가 아니라 soft counter를 목표로 한다.

## current pre-art closure 기준

- 이번 패스의 핵심 gap은 raw count 부족이 아니라 authored floor closure다.
- `12 core archetype / 6 site / 24 encounter / 6 boss overlay / 6 reward source` count는 유지한다.
- 추가 생성보다 아래 순서를 우선한다.
  1. live subset truth 정리
  2. 24 encounter를 8 authored family로 재배치
  3. site별 primary answer lane을 reward routing과 validator로 잠금
- balance tuning은 truth cleanup과 matrix closure 뒤에만 진행한다.

## 출시 기준 한 줄 요약

- paid launch safe target = `16 archetypes / 40~48 skills / 42~54 equippables / 96 passive nodes / 24 temporary augments / 12 authored permanent augment candidates`
- skill loadout = `6-slot topology + 2 visible active`
- synergy family = `3 races + 4 classes`, race `2 / 4`, class `2 / 3`
- baseline counter swing = 평균 `10~15%`

## 세부 문서 소유권

- roster와 archetype taxonomy: Pindoc character-lore / roster artifacts
- explicit launch floor roster: `docs/02_design/systems/launch-floor-content-matrix.md`
- cadence와 loadout topology: `docs/02_design/combat/resource-cadence-loadout.md`
- skill taxonomy와 수식: `docs/02_design/combat/skill-taxonomy-and-damage-model.md`
- item/passive/augment 볼륨과 예산: `docs/02_design/meta/item-passive-augment-budget.md`
- passive node 카탈로그: `docs/02_design/meta/passive-board-node-catalog.md`
- synergy breakpoint와 soft counter: `docs/02_design/meta/synergy-breakpoints-and-soft-counters.md`
- authoring schema와 validation/compile 경계: `docs/03_architecture/content-authoring-and-balance-data.md`

## 구현 우선순위

1. `StatKey`와 authored schema의 canonical taxonomy를 잠근다.
2. `SkillDefinitionAsset`, `BasicAttackDefinition`, `PassiveDefinition`, `MobilityDefinition`, `UnitArchetypeDefinition`을 Loop A 기준으로 확장한다.
3. `LoadoutCompiler`가 6-slot compile 계약과 energy/ownership metadata를 carry-through 하도록 유지한다.
4. validator와 headless/editor smoke 기준으로 authoring drift를 막는다.
5. 실제 16 archetype / 96 passive node authoring은 후속 패스에서 채운다. temporary augment는 V1 prototype 24개 live pool을 먼저 닫고, offer/schedule quality pass에서 재검증한다.

## passive board count split

- paid launch floor board shape는 `12 small / 5 notable / 1 keystone`로 잠근다.
- paid launch safe target board shape는 `14 small / 8 notable / 2 keystone`로 확장한다.
- floor node 증설은 safe target 문서 갱신 전까지 허용하지 않는다.

## Loop C governance seam

- `BudgetCard`가 power-bearing content의 공통 ledger다.
- `ContentRarity`는 `Common / Rare / Epic` 3단으로 고정한다.
- counter-system은 8-lane threat/answer topology를 쓴다.
- forbidden list는 warning이 아니라 fatal validator policy다.
- recruit economy와 loot rarity는 Loop C에서 다시 열지 않는다.

## Loop D first playable seam

- V1 first playable은 전체 launch floor와 별개로 별도 slice cap을 가진다.
- 실제 recruit/flex/augment/affix pool은 first playable slice를 기준으로 필터링된다.
- slice 밖 content는 삭제하지 않아도 되지만 `MoveOutOfV1` 또는 `ParkingLot`으로 분리한다.
