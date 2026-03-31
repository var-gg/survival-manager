# 작업 명세

## 메타데이터

- 작업명: Launch Floor Catalog Closure
- 담당: Codex
- 상태: 진행 중
- 최종수정일: 2026-03-31
- 관련경로:
  - `tasks/004_launch_floor_catalog_closure/`
  - `docs/02_design/combat/encounter-catalog-and-scaling.md`
  - `docs/02_design/combat/status-effects-cc-and-cleanse-taxonomy.md`
  - `docs/02_design/meta/drop-table-rarity-bracket-and-source-matrix.md`
  - `docs/02_design/meta/pvp-ruleset-and-arena-loop.md`
  - `Assets/_Game/Scripts/Editor/SeedData/SampleSeedGenerator.cs`
  - `Assets/_Game/Scripts/Editor/Validation/ContentDefinitionValidator.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/GameSessionState.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/BattleScreenController.cs`
  - `Assets/_Game/Scripts/Runtime/Meta/Services/EncounterResolutionService.cs`
  - `Assets/_Game/Scripts/Runtime/Meta/Services/LootResolutionService.cs`
  - `Assets/_Game/Scripts/Runtime/Meta/Services/ArenaSimulationService.cs`
- 관련문서:
  - `docs/03_architecture/unity-agent-harness-contract.md`
  - `docs/03_architecture/assembly-boundaries-and-persistence-ownership.md`
  - `docs/03_architecture/validation-and-acceptance-oracles.md`
  - `docs/03_architecture/encounter-authoring-and-runtime-resolution.md`

## Goal

- launch floor에서 비어 있던 encounter/status/drop/skill tag/crafting/arena
  catalog를 durable docs와 code/content contract로 잠근다.
- normal runtime path에서 smoke-only encounter dependency와 hard-coded battle seed를 제거한다.
- hand-authored chapter/site progression, 7 synergy family, 4-slot compile contract,
  3 equipment slot을 유지한다.

## Authoritative boundary

- 이 task의 authoritative live state는 `tasks/004_launch_floor_catalog_closure/status.md`다.
- durable design truth는 `docs/02_design/**` 신규/갱신 문서가 가진다.
- durable architecture truth는 `docs/03_architecture/**` 신규/갱신 문서가 가진다.
- 기존 `phase-01_*`, `phase-02_*`, `phase-03_*` 문서는 historical split note로만 남기고,
  현재 handoff 기준은 아니다.

## In scope

- encounter / enemy squad / boss overlay catalog 추가
- chapter/site progression과 endless unlock state 추가
- status / cleanse / DR taxonomy와 runtime event 확장
- automatic loot + reward card dual channel 잠금
- skill keyword / support modifier / weapon restriction catalog 추가
- crafting depth와 currency / sink contract 추가
- async arena snapshot / replay / persistence scaffold 추가
- 관련 docs index, task status, validation 기록 동기화

## Out of scope

- procedural expedition generation
- faction을 synergy family로 승격
- live online matchmaking / leaderboard backend
- deep crafting station / orb zoo / separate shield slot
- 런타임 연출 polish, VFX, 최종 UX 마감

## asmdef impact

- `SM.Content.Definitions`
- `SM.Combat`
- `SM.Meta`
- `SM.Unity`
- `SM.Persistence.Abstractions`
- `SM.Editor`
- `SM.Tests.EditMode`

새 경계는 기존 `SquadBlueprint -> LoadoutCompiler -> BattleLoadoutSnapshot`
흐름을 유지하면서, compile output과 persistence payload를 넓히는 방향으로만 열었다.

## persistence impact

- `CampaignProgressRecord`
- `ActiveRunRecord`
- `SaveProfile`
- `ArenaDefenseSnapshotRecord`
- `ArenaBlueprintSlotRecord`
- `ArenaMatchRecordRecord`
- `ArenaSeasonStateRecord`
- `ArenaRewardLedgerEntryRecord`

run/session 저장값은 `ChapterId`, `SiteId`, `SiteNodeIndex`, `EncounterId`,
`BattleSeed`, `BattleContextHash`, `StoryCleared`, `EndlessUnlocked`까지 확장한다.

## validator / test oracle

- validator:
  - `ContentDefinitionValidator`
  - docs harness scripts
- tests:
  - `EncounterAndLootResolutionTests`
  - `StatusResolutionServiceTests`
  - `ContentValidationWorkflowTests`
  - `StatV2AndSandboxTests`
- runtime smoke:
  - `pwsh -File tools/unity-bridge.ps1 smoke-observer`

## done definition

- code compile이 green이다.
- EditMode regression이 green이다.
- required durable docs와 index/task sync가 완료된다.
- validator/doc harness 결과가 task status에 기록된다.
- smoke-observer blocker가 남아 있으면 status에 red로 남기고 완료로 주장하지 않는다.

## deferred

- repo-wide markdownlint debt 정리
- bootstrap validator와 asset typed-load 불일치의 근본 수리
- live arena backend
- deeper crafting / market / trade
