# 작업 명세

## 메타데이터

- 작업명: Launch Floor Catalog Closure
- 담당: Codex
- 상태: in_progress
- 최종수정일: 2026-03-31
- 관련경로:
  - `tasks/004_launch_floor_catalog_closure/`
  - `Assets/_Game/Scripts/Editor/SeedData/SampleSeedGenerator.cs`
  - `Assets/_Game/Scripts/Editor/Validation/ContentDefinitionValidator.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/GameSessionState.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/BattleScreenController.cs`
  - `Assets/_Game/Scripts/Runtime/Meta/Services/EncounterResolutionService.cs`
  - `Assets/_Game/Scripts/Runtime/Meta/Services/LootResolutionService.cs`
  - `Assets/_Game/Scripts/Runtime/Meta/Services/ArenaSimulationService.cs`
- 관련문서:
  - `docs/03_architecture/unity-agent-harness-contract.md`
  - `docs/03_architecture/unity-editor-iteration-and-asset-authoring.md`
  - `docs/03_architecture/assembly-boundaries-and-persistence-ownership.md`
  - `docs/03_architecture/validation-and-acceptance-oracles.md`
  - `docs/03_architecture/content-authoring-and-balance-data.md`

## Goal

- launch floor catalog closure를 하나의 거대한 feature ticket이 아니라
  split closure가 가능한 parent umbrella task로 다시 정의한다.
- 004의 목적은 encounter/status/drops, skill/support tags, async arena
  scaffold를 child phase로 분리해 각 축을 독립적으로 닫게 만드는 것이다.

## Authoritative boundary

- 004 parent 문서는 migration split, acceptance, handoff 규칙의
  authoritative source다.
- 실제 구현 closure의 authoritative source는 아래 child phase 문서다.
  - `phase-01_encounter-status-drops.md`
  - `phase-02_skill-tag-crafting.md`
  - `phase-03_async-arena-scaffold.md`
- parent 004는 단일 runtime source-of-truth를 직접 닫지 않는다.

## In scope

- 현재 worktree에 이미 드러난 multi-axis 변경을 3개의 child phase로
  재구성한다.
- 각 child phase에 preflight, phase gate, validator/test oracle,
  loop budget을 부여한다.
- 004 전체에 대해 `compile green is not done` 규칙을 명시한다.

## Out of scope

- 004 parent를 compile 한 번으로 닫는 것
- encounter/status/drops, skill/support tags, arena persistence를 같은
  sprint에서 동시에 마무리하는 것
- broad scene polish, extra content fill, generalized tooling 확장

## asmdef impact

- 현재 worktree 기준으로 `SM.Editor`, `SM.Meta`, `SM.Unity`,
  `SM.Persistence.Abstractions` 영향이 함께 보인다.
- parent 004는 이 영향을 기록만 하며, 실제 asmdef 검토는 child phase
  preflight에서 다시 수행한다.
- asmdef cycle 위험은 특히 phase 03 arena scaffold에서 가장 높다.

## persistence impact

- persistence ownership 영향은 phase 03 async arena scaffold에 한정한다.
- phase 01과 phase 02는 persistence record나 ownership 변경 없이 닫는
  것을 기본으로 한다.
- parent 004는 persistence 변경을 umbrella 범위로 허용하지 않고 child
  문서에서만 열도록 제한한다.

## validator / test oracle

- phase 01:
  - validator: `ContentDefinitionValidator`
  - targeted tests: `EncounterAndLootResolutionTests`,
    `StatusResolutionServiceTests`
  - runtime smoke: `Town -> Expedition -> Battle -> Reward`
- phase 02:
  - validator: `RequiredClassTags`, `SupportAllowedTags`, stable tag drift를
    읽는 `ContentDefinitionValidator`
  - targeted tests: `LoadoutCompilerClosureTests`,
    `ContentValidationWorkflowTests`
  - runtime smoke: build compile path가 support/tag 변경을 읽는지 확인
- phase 03:
  - validator: persistence/arena scaffold와 직접 연결된 oracle을 별도
    정의해야 한다.
  - targeted tests: 현재 미정, child phase에서 먼저 정의해야 한다.
  - runtime smoke: arena path 또는 equivalent session path를 명시하기
    전까지 미완료로 둔다.

## done definition

- parent 004는 세 child phase가 각각 compile, validator, targeted tests,
  runtime smoke evidence를 남겼을 때만 done이다.
- parent 004는 child phase split이 실제로 사용되기 전에는
  handoff-ready가 아니다.
- compile green 또는 sample content 생성만으로 parent를 닫지 않는다.

## deferred

- broad sample content repair
- 추가 localization fill
- arena productionization
- generalized editor tooling 자동화
