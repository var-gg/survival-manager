# 작업 구현

## 메타데이터

- 작업명: Launch Floor Catalog Closure
- 담당: Codex
- 상태: implemented
- 최종수정일: 2026-03-31

## Phase 요약

### Phase 1 encounter / status / drops

- `CampaignChapterDefinition`, `ExpeditionSiteDefinition`, `EncounterDefinition`,
  `EnemySquadTemplateDefinition`, `BossOverlayDefinition`을 추가했다.
- `StatusFamilyDefinition`, `CleanseProfileDefinition`,
  `ControlDiminishingRuleDefinition`, `AppliedStatusState`,
  `StatusResolutionService`를 추가했다.
- `DropTableDefinition`, `RewardSourceDefinition`, `LootBundleDefinition`,
  `TraitTokenDefinition`, `LootResolutionService`를 추가했다.
- `GameSessionState`, `BattleScreenController`, `RewardScreenController`를
  authored encounter + automatic loot 경로로 연결했다.

### Phase 2 skill tag / crafting contract

- stable tag catalog를 확장하고 support modifier 12종을 authoring했다.
- `SkillDefinitionAsset`와 `ItemBaseDefinition`의 compatibility / crafting field를
  runtime와 validator가 같이 읽도록 맞췄다.
- `LoadoutCompiler`는 status/compatibility tag를 compile output과 hash에 보존하도록 넓혔다.

### Phase 3 async arena scaffold

- `ArenaDefenseSnapshot`, `ArenaBlueprintSlot`, `ArenaMatchRecord`,
  `ArenaSeasonState`, `ArenaRewardLedgerEntry`와 persistence record를 추가했다.
- `ArenaSimulationService`가 defense snapshot registration, candidate selection,
  instant sim, replay bundle generation을 담당하게 했다.
- temporary augment provenance exclusion을 validation 규칙으로 추가했다.

### Phase 4 docs / task harness

- 요청된 design/architecture 문서를 새로 만들고 관련 active 문서를 갱신했다.
- `docs/index.md`, `docs/02_design/index.md`, `docs/03_architecture/index.md`,
  `tasks/001_mvp_vertical_slice/status.md`, `tasks/004.../*.md`를 동기화했다.

## deviation

- `docs-check.ps1`는 repo-wide markdownlint debt 때문에 여전히 red다.
- `unity-bridge`의 `smoke-observer`는 compile 직후 connector 재연결 타이밍에서
  간헐적으로 끊길 수 있어서, 최종 검증은 `status -> clear-console -> bootstrap ->
  console/report` 분리 루프로 회수했다.

## blockers

- `pwsh -File tools/docs-check.ps1 -RepoRoot .`
  - repo 전체의 기존 markdownlint debt 때문에 red다.

## diagnostics

- bootstrap lane blocker는 두 갈래였다.
  - `RaceDefinition` / `ClassDefinition` / `TraitPoolDefinition`이 blank serialize되어
    archetype reference가 null로 떨어지는 문제
  - generate 중간에 imported catalog를 다시 읽기 전에 archetype patch가 돌면서
    불완전한 `skillCatalog`로 `Skills: []`를 덮어쓸 수 있던 문제
- 해결은 아래 세 seam에 넣었다.
  - reference-definition serialized repair 추가
  - `StableTagDefinition` / `SkillDefinitionAsset` catalog를 save/refresh 뒤 다시 로드
  - `PatchLaunchFloorArchetypes()`가 `4/4` skill을 해석하지 못하면 destructive overwrite를
    하지 않도록 guard 추가
- reward source / drop table도 같은 serialize drift가 재발하지 않도록
  explicit serialized patch를 추가했다.

## why this loop happened

- sample content의 runtime fallback과 editor validator의 typed-load 기준이 달랐다.
- smoke-observer는 compile/test와 다른 bootstrap lane을 타기 때문에
  동일한 green 기준으로 닫히지 않았다.
- catalog closure가 content root, generator, validator, bootstrap을 동시에 건드려
  loop가 길어졌다.
- 특히 `AssetDatabase`에서 방금 생성한 catalog를 다시 조회하는 타이밍과
  serialized object patch 타이밍이 어긋나면, committed asset이 partial/blank state로
  다시 저장될 수 있다는 점이 뒤늦게 드러났다.

## 기록 규칙

- 이 문서는 micro compile 로그를 남기지 않는다.
- 최종 판정과 deliverable 요약은 `status.md`가 소유한다.
