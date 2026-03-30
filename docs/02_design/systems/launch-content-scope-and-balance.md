# 출시 기준 콘텐츠 스코프와 밸런스 허브

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-30
- 소스오브트루스: `docs/02_design/systems/launch-content-scope-and-balance.md`
- 관련문서:
  - `docs/01_product/vision.md`
  - `docs/02_design/deck/roster-archetype-launch-scope.md`
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
| recruit archetypes | 8 | 12 | 16 |
| core archetypes | n/a | 12 | 12 |
| specialist archetypes | n/a | 0 | 4 |
| skills | MVP 문서 비소유 | 40 | 40~48 |
| equippables | MVP 문서 비소유 | 36 | 42~54 |
| passive boards | MVP 문서 비소유 | 4 | 4 |
| passive nodes | MVP 문서 비소유 | 72 | 96 전후 |
| temporary augments | 9 | 18 | 24 |
| permanent augments | MVP 문서 비소유 | 9 | 12 |
| permanent augment equip slots | 1 | 1~2 | 데이터 모델 3, 운용 1~3 |
| equipment slots | 3 | 3 | 3 |
| synergy families | 7 | 7 | 7 |
| synergy breakpoints | MVP 문서 참조 | 2 / 3 / 4 | 2 / 3 / 4 |

## 현재 패스에서 고정하는 핵심

- canonical class id는 `vanguard / duelist / ranger / mystic`를 유지한다.
- 문서상 역할 이름으로는 `Vanguard / Striker / Ranger / Mystic`를 쓴다.
- penetration stat id는 `phys_pen / mag_pen`을 유지한다.
- skill loadout compile 결과는 `2 active + 1 passive/trigger + 1 support modifier`를 기준으로 한다.
- synergy는 `3 race + 4 class`의 총 7 family를 유지한다.
- counter 설계는 hard counter가 아니라 soft counter를 목표로 한다.

## 출시 기준 한 줄 요약

- paid launch safe target = `16 archetypes / 40~48 skills / 42~54 equippables / 96 passive nodes / 24 temporary augments / 12 permanent augments`
- skill loadout = `2 active + 1 passive/trigger + 1 support modifier`
- synergy family = `3 races + 4 classes`, each `2 / 3 / 4` breakpoints
- baseline counter swing = 평균 `10~15%`

## 세부 문서 소유권

- roster와 archetype taxonomy: `docs/02_design/deck/roster-archetype-launch-scope.md`
- skill taxonomy와 수식: `docs/02_design/combat/skill-taxonomy-and-damage-model.md`
- item/passive/augment 볼륨과 예산: `docs/02_design/meta/item-passive-augment-budget.md`
- synergy breakpoint와 soft counter: `docs/02_design/meta/synergy-breakpoints-and-soft-counters.md`
- authoring schema와 validation/compile 경계: `docs/03_architecture/content-authoring-and-balance-data.md`

## 구현 우선순위

1. `StatKey`와 authored schema의 canonical taxonomy를 잠근다.
2. `SkillDefinitionAsset`, `ItemBaseDefinition`, `AffixDefinition`, `UnitArchetypeDefinition`, `PassiveBoardDefinition`, `PassiveNodeDefinition`을 launch authoring 기준으로 확장한다.
3. `LoadoutCompiler`가 4-slot compile 계약과 새 skill metadata를 carry-through 하도록 유지한다.
4. validator와 headless/editor smoke 기준으로 authoring drift를 막는다.
5. 실제 16 archetype / 24 augment / 96 passive node authoring은 후속 패스에서 채운다.
