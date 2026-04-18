# specialist encounter/tuning/localization 구현 기록

## 메타데이터

- 작업명: specialist encounter/tuning/localization
- 담당: Codex
- 상태: completed
- 최종수정일: 2026-04-19

## Phase summary

- Phase 0: task 문서 생성.
- Phase 1: `SampleSeedGenerator`에 specialist encounter composition, support lane coverage, tag repair, recruit banned pairing repair, Loop C skill rarity 보정을 추가했다.
- Phase 2: canonical Resources를 seed lane으로 재생성하고 기존 localization table/key 안에서 specialist description/toast/lore tone을 갱신했다.
- Phase 3: content validation, balance smoke, focused tests, 기본 fast/lint/docs 검증 준비를 수행했다.

## Deviation

- `ContentValidationWorkflowTests`의 report path assertion은 Windows path separator 때문에 실패해 path normalization으로 좁게 보정했다.

## Blockers

- 없음.

## Diagnostics

- `content-validate` 잔여는 gameplay authoring governance 문제로 분류했다.
- `loop_c.condition_cap`/`loop_c.keyword_cap`: standard skill의 complexity는 Rare budget card로 분류해 cap과 의미를 맞췄다.
- `tag.missing_id`: archetype support modifier bias와 augment tag list repair를 추가해 empty id reference를 제거했다.
- `loop_b.recruit.banned_pairing`: Unity YAML list item 제거 규칙과 serialized array repair를 보정해 duplicate pair를 제거했다.
- `encounter.archetype_enemy_usage_*`: 기존 24 encounter 안에서 squad composition만 교체했다.

## Why this loop happened

- 020은 canonical Resources typed load와 audit green을 복구했고, gameplay content governance는 021 범위로 남겼다.
- 021은 content truth를 asset-only로 임시 수리하지 않고 seed generator의 deterministic repair lane에 반영해 재생성 시 같은 오류가 돌아오지 않도록 했다.

## Verification

- `pwsh -File tools/unity-bridge.ps1 content-validate`: issue 0
- `pwsh -File tools/unity-bridge.ps1 balance-sweep-smoke`: exit 0, validation error/warning 0
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.ContentValidationWorkflowTests`: 5/5 pass
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.LoopCContentGovernanceTests`: 9/9 pass
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.EncounterAndLootResolutionTests`: 3/3 pass
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.CharacterAxisLocalizationTests`: 3/3 pass
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.StoryDirectorServiceTests`: 3/3 pass
- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`: failed 0, skipped 3
- `pwsh -File tools/test-harness-lint.ps1`: pass
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`: pass
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`: pass
- targeted `docs-check` for task 021 docs: 4 files, 0 errors
