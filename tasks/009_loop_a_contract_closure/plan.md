# 작업 계획

## 메타데이터

- 작업명: Loop A Contract Closure
- 담당: Codex
- 상태: 진행 중
- 최종수정일: 2026-04-01
- 의존:
  - `tasks/006_system_deepening_pass/status.md`
  - `docs/03_architecture/content-authoring-and-balance-data.md`
  - `docs/03_architecture/combat-runtime-architecture.md`
  - `docs/03_architecture/combat-harness-and-debug-contract.md`

## Preflight

- worktree clean과 branch baseline 일치 여부를 먼저 확인한다.
- `SM.Content` / `SM.Combat` / `SM.Meta` / `SM.Unity` / `SM.Editor` 책임 경계를 다시 점검한다.
- 기존 4-slot/mana 의존 지점을 grep으로 좁히고, migration alias가 필요한 경로를 분리한다.
- done criterion과 oracle을 `spec.md`, `status.md`에 먼저 적고 시작한다.

## Phase 1 code-only

- 새 seam enum/data contract를 concern별 파일로 추가한다.
- `UnitArchetypeDefinition`, `SkillDefinitionAsset`, compiled model, loadout compiler, runtime lookup, persistence state를 새 6-slot/energy 계약으로 옮긴다.
- authority matrix, summon ownership, targeting vocabulary validator를 먼저 추가한다.
- compile blocker를 잡는 동안 Unity asset 재생성은 섞지 않는다.

## Phase 2 asset authoring

- seed/runtime content migration path를 새 authored field를 채우는 쪽으로 확장한다.
- 필요 시 canonical sample content를 Unity 경로로 재생성/보정한다.
- asset refresh는 batch 단위로 묶고, code-only compile이 안정화된 뒤 실행한다.

## Phase 3 validation

- `compile`
- `test-edit`
- docs policy / docs check / smoke check
- validator report와 harness scenario evidence 수집
- Unity 응답 채널이 잠기면 최소 compile + direct EditMode evidence + docs/smoke를 남긴다.

## rollback / escape hatch

- 4-slot 경로를 한 번에 삭제하지 않고 migration alias를 남겨 compile을 먼저 복구한다.
- asset reserialize가 과도하면 script-only migration fallback을 유지하고 seed refresh를 늦춘다.
- Loop A seam 중 summon runtime이 compile budget을 초과하면 validator + test oracle + read model부터 우선 닫고 scene polish는 뒤로 민다.

## tool usage plan

- file-first로 `rg`, `Get-Content`, `git status`를 사용한다.
- 파일 생성/수정은 `apply_patch`만 사용한다.
- Unity compile/test/smoke는 `tools/unity-bridge.ps1` 경로를 우선한다.
- scene/prefab CRUD는 이번 loop의 기본 경로가 아니며, typed guardrail이 필요한 경우에만 Unity MCP를 사용한다.

## loop budget

- compile-fix 허용 횟수: 4
- refresh/read-console 반복 허용 횟수: 3
- blind asset generation 재시도 허용 횟수: 1
