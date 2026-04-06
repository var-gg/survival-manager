# Task 016 Plan

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-07
- 소스오브트루스: `tasks/016_character_axis_and_localized_battle_metadata/plan.md`

## Preflight

1. 기존 `RoleInstructionDefinition`과 `LoadoutCompiler` 경계를 유지한다.
2. `CharacterDefinition`은 identity layer만 담당하고 battle truth를 직접 만들지 않는다.
3. Quick Battle ally path는 `HeroId` ownership을 유지한다.

## Phase 1 code-only

- runtime carry-through
- battle metadata formatter
- battle selected panel
- content/lookup/validator/editor preview helper

## Phase 2 asset authoring

- `Characters` 루트
- 12 core character seed
- role/axis localization key 보강

## Phase 3 validation

- FastUnit
- `tools/test-harness-lint.ps1`
- 가능하면 `test-batch-edit`
- docs/smoke check

## rollback / escape hatch

- character asset이 비어도 archetype fallback으로 전투 표시는 유지한다
- role name은 glossary fallback을 유지한다

## tool usage plan

- 코드 탐색: `rg`
- 수동 편집: `apply_patch`
- 검증: `unity-bridge`, lint, docs check

## loop budget

- compile / lint / docs check 최대 3회
