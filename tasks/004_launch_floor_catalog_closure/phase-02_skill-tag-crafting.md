# Phase 02: skill / tag / crafting

- 상태: in_progress
- 최종수정일: 2026-03-31
- parent task: `tasks/004_launch_floor_catalog_closure/status.md`

## authoritative axis

- support skill, stable tag, `RequiredClassTags` / `SupportAllowedTags` 기반 compile contract를 닫는다.
- crafting contract는 이 axis 안에 남을 수 있는 최소 compile-visible 경계만 허용한다.

## in scope

- support skill asset과 stable tag asset
- `SkillDefinitionAsset`, `SkillDefinition`
- `LoadoutCompiler`, `RuntimeCombatContentLookup`
- `ContentDefinitionValidator`, `SampleSeedGenerator`
- `LoadoutCompilerClosureTests`, `ContentValidationWorkflowTests`

## out of scope

- crafting economy 수치와 UX 전체
- encounter/status/drop authored path
- arena scaffold와 persistence ownership

## preflight

- support/tag catalog를 validator가 먼저 읽는지 확인
- crafting contract가 compile-visible 범위를 넘기면 별도 child task로 분리
- Edit Mode와 compile blocker 상태 확인

## code-only closure

- compile contract와 tag resolution을 code-only로 먼저 닫는다.
- `RequiredClassTags`나 support tag 변화가 loadout compile 결과에 어떻게 반영되는지 먼저 확정한다.

## asset authoring closure

- support skill과 stable tag asset만 batch로 다룬다.
- `SampleSeedGenerator`는 Edit Mode에서만 실행한다.

## validator / test oracle

- validator: `RequiredClassTags`, `SupportAllowedTags`, stable tag drift 0
- targeted tests: `LoadoutCompilerClosureTests`, `ContentValidationWorkflowTests`
- runtime smoke: build compile path가 support/tag 변경을 읽는지 확인

## done signal

- compile green
- support/tag validator pass
- targeted tests pass
- runtime compile path smoke evidence 기록

## current blockers

- current worktree에는 crafting contract 경계가 충분히 선명하지 않다.
- support/tag closure와 crafting contract를 다시 합치면 004의 oversized umbrella 문제가 재발한다.
