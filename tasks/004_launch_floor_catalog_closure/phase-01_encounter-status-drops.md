# Phase 01: encounter / status / drops

- 상태: in_progress
- 최종수정일: 2026-03-31
- parent task: `tasks/004_launch_floor_catalog_closure/status.md`

## authoritative axis

- encounter, status, drops authored catalog와 runtime lookup path를 닫는다.
- 이 phase는 status resolution과 loot/encounter resolution 경계만 다룬다.

## in scope

- `EncounterDefinition`, `DropTableDefinition`, `LootBundleDefinition`, `RewardSourceDefinition`, `StatusFamilyDefinition`
- `EncounterResolutionService`, `LootResolutionService`, `StatusResolutionService`, `AppliedStatusState`
- `GameSessionState`, `BattleScreenController`, `RuntimeCombatContentLookup`의 관련 lookup 진입점
- `EncounterAndLootResolutionTests`, `StatusResolutionServiceTests`

## out of scope

- support/tag taxonomy
- crafting contract
- arena persistence scaffold

## preflight

- Edit Mode 확인
- `ContentDefinitionValidator`가 새 catalog를 읽는지 먼저 확인
- 이 phase에서 persistence record를 건드리지 않는다는 점 확인

## code-only closure

- resolution service와 runtime lookup path를 code-only로 먼저 닫는다.
- `GameSessionState`와 `BattleScreenController`는 encounter/status truth를 orchestration만 한다.

## asset authoring closure

- encounter/status/drop 계열 asset과 localization table만 batch로 다룬다.
- generator나 menu execution은 Play Mode에서 금지한다.

## validator / test oracle

- validator: encounter/status/drop catalog drift 0
- targeted tests: `EncounterAndLootResolutionTests`, `StatusResolutionServiceTests`
- runtime smoke: `Town -> Expedition -> Battle -> Reward`

## done signal

- compile green
- validator pass
- 두 EditMode test pass
- runtime path smoke evidence 기록

## current blockers

- validator-first가 역사적으로 늦었다.
- asset authoring과 runtime entry 연결이 한 루프에 섞인 흔적이 있어 재검증이 필요하다.
