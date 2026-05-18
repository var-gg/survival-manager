# 046 Compendium Runtime Surface Plan

- 상태: active
- 소유자: repository
- 최종수정일: 2026-05-18
- 소스오브트루스: `tasks/046_compendium_runtime_surface/plan.md`

## Preflight

- Pindoc write tool availability 확인
- 기존 Town hub modal 구조 확인
- `ICombatContentLookup` / `CombatContentSnapshot` 조회 경계 확인

## Phase 1 code-only

- `ContentTextResolver`에 status/synergy 설명 resolve 표면 추가
- `CompendiumPresenter`, `CompendiumView`, `CompendiumViewState` 추가
- 스킬/상태/시너지/캐릭터 rows를 snapshot과 authored definition에서 읽어 view state로 변환

## Phase 2 asset authoring

- `CompendiumPreview.uxml`, `CompendiumPreview.uss` 추가
- `TownScreen.uxml`에 Compendium template, utility button, modal overlay 추가
- UI localization key를 `UI_Common`, `UI_Town` ko/en table과 bootstrap seed에 추가

## Phase 3 validation

- `git diff --check`
- `tools/test-harness-lint.ps1`
- localization key presence check
- `test-batch-fast`는 Unity project lock 해소 후 실행

## rollback / escape hatch

- Town hub에서 Compendium button/template/overlay와 `TryWireCompendium`만 제거하면 런타임 flow는 기존 상태로 복귀한다.
- 도감은 read-only surface라 save/combat truth rollback은 필요 없다.

## tool usage plan

- 문서/task 변경에는 `docs-maintainer` 기준을 적용한다.
- C# runtime UI 변경에는 `code-structure-guard` 기준을 적용한다.
- Unity 확인은 `tools/unity-bridge.ps1 test-batch-fast`를 우선 사용한다.

## loop budget

- C# MVP 1 loop
- UI asset/localization 1 loop
- 에디터/VFX preview 후속 1 loop
