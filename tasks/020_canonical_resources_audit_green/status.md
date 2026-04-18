# canonical Resources audit green 상태

## 메타데이터

- 작업명: canonical Resources audit green
- 담당: Codex
- 상태: done
- 최종수정일: 2026-04-19

## Current state

- canonical Resources asset의 missing `m_Script` 참조를 repair lane에서 복구했다.
- authored content ScriptableObject 타입의 file-scoped namespace가 Unity typed load에서 누락되는 문제를 block namespace 전환으로 해소했다.
- `RuntimeCombatContentLookup`는 editor fallback 없이 Resources catalog snapshot을 만들 수 있고, `BuildCompileAuditTests` 전체 클래스는 green이다.
- `content-validate`의 canonical missing issue는 닫혔지만, 남은 40건은 021 specialist encounter/tuning 범위로 분류했다.

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 / 다음 확인 |
| --- | --- | --- | --- |
| compile | Unity script compile error 0 | 통과 | `BuildCompileAuditTests` batch EditMode exit 0 |
| validator | canonical Resources missing issue 0 | 부분 통과 | `content-validate` 40건 실패, `first_playable.asset_missing` 제거 확인 |
| targeted tests | `BuildCompileAuditTests` 전체 failed 0 | 통과 | total 10, passed 10, failed 0 |
| runtime smoke | `RuntimeCombatContentLookup`가 Resources path로 snapshot 성공 | 통과 | audit test와 typed Resources count 확인 |

## Evidence

- `pwsh -File tools/unity-execute-method.ps1 -Method SM.Editor.SeedData.SampleSeedGenerator.Generate -LogFile Logs/seed-content-020-block-namespace-2.log -PhaseName "canonical content regeneration with normalized augment rarity" -ProjectRoot .` → exit 0.
- `rg -n "m_Script: \{fileID: 0\}|^  Rarity: 3" Assets/Resources/_Game/Content/Definitions -g "*.asset"` → 0건.
- typed Resources count diagnostic: DefinitionsRoot 1525, Characters 16, Archetypes 16, Skills 52, CampaignChapters 3, ExpeditionSites 6, Encounters 24, EnemySquads 24, BossOverlays 6, RewardSources 6, DropTables 6, LootBundles 1.
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.BuildCompileAuditTests` → Passed, total 10, passed 10, failed 0.
- `pwsh -File tools/unity-bridge.ps1 content-validate` → error 40건, warning 12건. Error 분류: `loop_c.condition_cap` 13, `loop_c.keyword_cap` 13, `tag.missing_id` 10, `loop_b.recruit.banned_pairing` 4. Warning 분류: `encounter.archetype_enemy_usage_total` 4, `encounter.archetype_enemy_usage_elite_boss` 4, `build_lane.class_lane_missing` 2, `build_lane.archetype_baseline_missing` 2. `first_playable.asset_missing`는 제거됨.
- `pwsh -File tools/unity-bridge.ps1 test-batch-fast` → failed 0, skipped 3.
- `pwsh -File tools/test-harness-lint.ps1` → PASS.
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .` → PASS.
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .` → PASS.
- `pwsh -NoProfile -Command "& './tools/docs-check.ps1' -RepoRoot . -Paths @('tasks/020_canonical_resources_audit_green/spec.md','tasks/020_canonical_resources_audit_green/plan.md','tasks/020_canonical_resources_audit_green/status.md','tasks/020_canonical_resources_audit_green/implement.md')"` → targeted markdown files 4, 0 error.

## Remaining blockers

- 없음. canonical Resources 누락으로 `BuildCompileAuditTests`가 실패하던 blocker는 닫혔다.
- `content-validate` 잔여 실패는 021 `specialist_encounter_tuning_localization`에서 닫는다.

## Deferred / debug-only

- specialist encounter coverage, balance tuning, localization tone pass는 021에서 처리한다.
- session service object extraction은 023에서 처리한다.

## Loop budget consumed

- compile-fix: 1/1
- refresh/read-console: 2/2
- asset authoring retry: 2/2
- validation retry: 2/2
- budget 초과 시 남긴 diagnosis: Unity typed Resources load가 missing script reference 복구 뒤에도 0건을 반환했고, 원인은 authored ScriptableObject 타입의 file-scoped namespace import였다. `SM.Content` authored 타입과 `FirstPlayableSliceDefinitionAsset`를 block namespace로 전환해 canonical typed load를 복구했다.

## Handoff notes

- 다음 세션은 021 `specialist_encounter_tuning_localization`에서 `content-validate` 잔여 40건을 줄이는 것으로 시작한다.
- runtime/test 경로에 implicit sample content regeneration을 다시 넣지 않는다.
- asset repair는 `seed-content` explicit lane과 editor-only repair utility에서만 수행한다.
- Unity batch가 `Assets/_Game/Prefabs/Battle/Actors/BattleActor_PrimitiveWrapper.prefab`를 자동 reimport하므로 커밋 전 범위 밖 변경으로 제외한다.
