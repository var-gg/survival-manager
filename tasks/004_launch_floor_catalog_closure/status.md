# 작업 상태

## 메타데이터

- 작업명: Launch Floor Catalog Closure
- 담당: Codex
- 상태: handoff_ready
- 최종수정일: 2026-03-31

## Current state

- encounter / status / drops / skill tag / crafting / arena scaffold의
  code/content/docs는 반영됐다.
- compile은 `pwsh -File tools/unity-bridge.ps1 compile`로 green을 확인했다.
- latest confirmed full EditMode pass는 `SM.Tests.EditMode` 56/56 green이다.
- docs policy와 smoke-check는 green이다.
- `docs-check.ps1`는 repo-wide markdownlint debt로 red다.
- `smoke-observer` bootstrap lane은 green으로 복구됐다.
- latest content validation summary는 `errors 0 / warnings 0`이다.

## Acceptance matrix

| 항목 | 결과 | 비고 |
| --- | --- | --- |
| code compile | 통과 | `pwsh -File tools/unity-bridge.ps1 compile` |
| EditMode tests | 통과 | latest confirmed pass `56/56`, job `2fac41e005b64b3bb0c4808008ea9b30` |
| docs policy | 통과 | `tools/docs-policy-check.ps1` |
| smoke-check | 통과 | `tools/smoke-check.ps1` |
| docs-check | 실패 | repo-wide markdownlint debt, 이번 task 전부터 존재 |
| observer smoke | 통과 | bootstrap 후 console empty, content validation `0/0` |

## Evidence

- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .` -> pass
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .` -> pass
- `pwsh -File tools/unity-bridge.ps1 compile` -> pass
- `pwsh -File tools/unity-bridge.ps1 bootstrap` -> pass
- `pwsh -File tools/unity-bridge.ps1 console -Lines 220 -Filter error,warning,log` -> `[]`
- `Logs/content-validation/content-validation-summary.md`
  - floor counts locked, `errors 0 / warnings 0`
- EditMode regression:
  - `EncounterAndLootResolutionTests`
  - `StatusResolutionServiceTests`
  - `ContentValidationWorkflowTests`
  - `StatV2AndSandboxTests`

## File-by-file summary

- task docs:
  - `tasks/004_launch_floor_catalog_closure/spec.md`
  - `tasks/004_launch_floor_catalog_closure/plan.md`
  - `tasks/004_launch_floor_catalog_closure/implement.md`
  - `tasks/004_launch_floor_catalog_closure/status.md`
- design docs created:
  - `docs/02_design/combat/encounter-catalog-and-scaling.md`
  - `docs/02_design/combat/status-effects-cc-and-cleanse-taxonomy.md`
  - `docs/02_design/combat/skill-keywords-support-modifiers-and-weapon-restrictions.md`
  - `docs/02_design/meta/campaign-chapter-and-expedition-sites.md`
  - `docs/02_design/meta/drop-table-rarity-bracket-and-source-matrix.md`
  - `docs/02_design/meta/equipment-family-and-crafting-depth.md`
  - `docs/02_design/meta/crafting-currencies-and-sinks.md`
  - `docs/02_design/meta/pvp-ruleset-and-arena-loop.md`
- design docs updated:
  - `docs/02_design/combat/hero-traits.md`
  - `docs/02_design/meta/pvp-boundary.md`
  - `docs/02_design/meta/item-and-affix-system.md`
  - `docs/02_design/meta/crafting-and-reroll-economy.md`
  - `docs/02_design/meta/reward-economy.md`
  - `docs/02_design/meta/town-and-expedition-loop.md`
  - `docs/02_design/systems/skills-items-and-passive-boards.md`
- architecture docs created:
  - `docs/03_architecture/encounter-authoring-and-runtime-resolution.md`
  - `docs/03_architecture/status-runtime-stack-and-cleanse-rules.md`
  - `docs/03_architecture/drop-resolution-and-ledger-pipeline.md`
  - `docs/03_architecture/arena-snapshot-matchmaking-and-season-contract.md`
  - `docs/03_architecture/skill-tag-catalog-and-compatibility-resolution.md`
- architecture docs updated:
  - `docs/03_architecture/combat-state-and-event-model.md`
- index / status sync:
  - `docs/index.md`
  - `docs/02_design/index.md`
  - `docs/03_architecture/index.md`
  - `tasks/001_mvp_vertical_slice/status.md`
- runtime / editor code:
  - `Assets/_Game/Scripts/Editor/SeedData/SampleSeedGenerator.cs`
  - `Assets/_Game/Scripts/Editor/Validation/ContentDefinitionValidator.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/GameSessionState.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/BattleScreenController.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/RewardScreenController.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/RuntimeCombatContentLookup.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/RuntimeCombatContentFileParser.cs`
  - `Assets/_Game/Scripts/Runtime/Meta/Services/EncounterResolutionService.cs`
  - `Assets/_Game/Scripts/Runtime/Meta/Services/LootResolutionService.cs`
  - `Assets/_Game/Scripts/Runtime/Meta/Services/ArenaSimulationService.cs`
  - `Assets/_Game/Scripts/Runtime/Meta/Services/LoadoutCompiler.cs`
  - `Assets/_Game/Scripts/Runtime/Combat/Services/StatusResolutionService.cs`
- new definition / model files:
  - `Assets/_Game/Scripts/Runtime/Content/Definitions/CampaignChapterDefinition.cs`
  - `Assets/_Game/Scripts/Runtime/Content/Definitions/ExpeditionSiteDefinition.cs`
  - `Assets/_Game/Scripts/Runtime/Content/Definitions/EncounterDefinition.cs`
  - `Assets/_Game/Scripts/Runtime/Content/Definitions/EnemySquadTemplateDefinition.cs`
  - `Assets/_Game/Scripts/Runtime/Content/Definitions/BossOverlayDefinition.cs`
  - `Assets/_Game/Scripts/Runtime/Content/Definitions/StatusFamilyDefinition.cs`
  - `Assets/_Game/Scripts/Runtime/Content/Definitions/CleanseProfileDefinition.cs`
  - `Assets/_Game/Scripts/Runtime/Content/Definitions/ControlDiminishingRuleDefinition.cs`
  - `Assets/_Game/Scripts/Runtime/Content/Definitions/DropTableDefinition.cs`
  - `Assets/_Game/Scripts/Runtime/Content/Definitions/RewardSourceDefinition.cs`
  - `Assets/_Game/Scripts/Runtime/Content/Definitions/LootBundleDefinition.cs`
  - `Assets/_Game/Scripts/Runtime/Content/Definitions/TraitTokenDefinition.cs`
  - `Assets/_Game/Scripts/Runtime/Combat/Model/AppliedStatusState.cs`
  - `Assets/_Game/Scripts/Runtime/Meta/Model/CampaignProgressModels.cs`
  - `Assets/_Game/Scripts/Runtime/Meta/Model/LootModels.cs`
  - `Assets/_Game/Scripts/Runtime/Meta/Model/ArenaModels.cs`
- persistence / tests:
  - `Assets/_Game/Scripts/Runtime/Persistence/Abstractions/Models/CampaignProgressRecord.cs`
  - `Assets/_Game/Scripts/Runtime/Persistence/Abstractions/Models/Arena*.cs`
  - `Assets/Tests/EditMode/EncounterAndLootResolutionTests.cs`
  - `Assets/Tests/EditMode/StatusResolutionServiceTests.cs`
  - `Assets/Tests/EditMode/ContentValidationWorkflowTests.cs`
  - `Assets/Tests/EditMode/StatV2AndSandboxTests.cs`
- content roots:
  - `Assets/Resources/_Game/Content/Definitions/CampaignChapters`
  - `Assets/Resources/_Game/Content/Definitions/ExpeditionSites`
  - `Assets/Resources/_Game/Content/Definitions/Encounters`
  - `Assets/Resources/_Game/Content/Definitions/EnemySquads`
  - `Assets/Resources/_Game/Content/Definitions/BossOverlays`
  - `Assets/Resources/_Game/Content/Definitions/StatusFamilies`
  - `Assets/Resources/_Game/Content/Definitions/CleanseProfiles`
  - `Assets/Resources/_Game/Content/Definitions/ControlDiminishingRules`
  - `Assets/Resources/_Game/Content/Definitions/RewardSources`
  - `Assets/Resources/_Game/Content/Definitions/DropTables`
  - `Assets/Resources/_Game/Content/Definitions/LootBundles`
  - `Assets/Resources/_Game/Content/Definitions/TraitTokens`
- canonical asset repair:
  - `Assets/Resources/_Game/Content/Definitions/Races/*.asset`
  - `Assets/Resources/_Game/Content/Definitions/Classes/*.asset`
  - `Assets/Resources/_Game/Content/Definitions/Traits/traitpool_*.asset`
  - `Assets/Resources/_Game/Content/Definitions/DropTables/drop_table_shrine_event.asset`

## Authored catalog summary

### chapter / site

| chapter | site ids |
| --- | --- |
| `chapter_ashen_frontier` | `site_ashen_gate`, `site_cinder_watch` |
| `chapter_warren_depths` | `site_forgotten_warren`, `site_twisted_den` |
| `chapter_ruined_crypts` | `site_ruined_crypt`, `site_grave_sanctum` |

### site track

모든 site는 `skirmish -> skirmish -> elite -> boss -> extract` 5노드 선형 track을 사용한다.

### encounter count

- skirmish: `12`
- elite: `6`
- boss: `6`
- total battle encounters: `24`

### boss overlay

- `boss_overlay_ashen_gate`
- `boss_overlay_cinder_watch`
- `boss_overlay_forgotten_warren`
- `boss_overlay_twisted_den`
- `boss_overlay_ruined_crypt`
- `boss_overlay_grave_sanctum`

## Status family table

| 분류 | ids |
| --- | --- |
| control | `stun`, `root`, `silence`, `slow` |
| attrition / vulnerability | `burn`, `bleed`, `wound`, `sunder` |
| tactical mark | `marked`, `exposed` |
| defensive boon | `barrier`, `guarded`, `unstoppable` |

cleanse floor:

- `cleanse_basic`
- `cleanse_control`
- `break_and_unstoppable`

## Source matrix

| source | auto drop | reward card | rarity floor |
| --- | --- | --- | --- |
| `reward_source_skirmish` | 예 | 예 | `common`, `advanced` |
| `reward_source_elite` | 예 | 예 | `advanced`, `elite` |
| `reward_source_boss` | 예 | 예 | `elite`, `boss` |
| `reward_source_shrine_event` | 예 | 예 | `advanced`, `elite` |
| `reward_source_extract` | 예 | 예 | `common`, `advanced` + bonus bundle |
| `reward_source_salvage` | 예 | 아니오 | `common`, `advanced` |

example loot bundle:

- source: `reward_source_boss`
- seed: `12345`
- result: `boss_sigil_drop x1`, `echo_crystal_boss x2`, `item_prayer_bead x1`

## Support compatibility table

| support id | include | exclude | weapon | class |
| --- | --- | --- | --- | --- |
| `support_brutal` | `strike`, `burst` | 없음 | 없음 | 없음 |
| `support_swift` | `projectile`, `dash` | 없음 | 없음 | 없음 |
| `support_piercing` | `projectile`, `pierce` | `heal`, `shield` | `bow` | 없음 |
| `support_echo` | `burst`, `aura` | 없음 | 없음 | 없음 |
| `support_lingering` | `zone`, `aoe` | 없음 | 없음 | 없음 |
| `support_purifying` | `heal`, `shield`, `cleanse` | `bleed`, `execute` | 없음 | `mystic` |
| `support_guarded` | `guard`, `shield` | 없음 | `shield` | `vanguard` |
| `support_executioner` | `strike`, `burst`, `execute` | `heal`, `shield` | `blade` | `duelist` |
| `support_longshot` | `projectile`, `mark` | 없음 | `bow` | `ranger` |
| `support_siphon` | `burst`, `burn` | 없음 | `focus` | `mystic` |
| `support_anchored` | `shield`, `guard`, `aura` | `dash` | `shield` | `vanguard` |
| `support_hunter_mark` | `mark`, `projectile` | `heal` | `bow` | `ranger` |

## Remaining blockers

- `docs-check.ps1`는 repo-wide markdownlint debt 때문에 아직 red다.
- full EditMode suite의 최신 재실행 결과는 `unity-cli` wrapper disconnect 때문에
  새 artifact를 회수하지 못했고, latest confirmed pass는 기존 `56/56`이다.

## Deferred / debug-only

- `BattleEncounterPlans.CreateObserverSmokePlan()`
  - normal runtime source-of-truth가 아니라 debug-only fallback으로만 유지
- quick battle `debug_smoke_observer`
  - authored catalog unavailable 시 fallback context
- deferred systems:
  - procedural expedition generation
  - fear/charm/sleep/polymorph family
  - live matchmaking / leaderboard backend
  - deep crafting / market / trade

## Loop budget consumed

- compile loop: 다수 사용
- bootstrap / smoke retry: 다수 사용
- docs validation: 1회 full run
- smoke blocker 원인 좁히기를 위해 validator loader fallback, reference-definition
  serialized repair, imported catalog reload guard를 추가 조정했다

## Handoff notes

- normal runtime path, compile, docs sync, bootstrap smoke는 반영 완료 상태다.
- 다음 세션에서 우선순위가 남아 있다면 `docs-check.ps1` repo-wide debt 정리와
  full EditMode suite 재증빙을 separate pass로 보면 된다.
- `SampleSeedGenerator`는 이제 generated catalog를 save/refresh 뒤 다시 읽고,
  incomplete `skillCatalog`로 archetype `Skills`를 비우지 않도록 guard를 가진다.
