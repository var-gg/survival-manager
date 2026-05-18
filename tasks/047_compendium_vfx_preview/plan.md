# 047 Compendium VFX Preview Plan

- 상태: active
- 소유자: repository
- 최종수정일: 2026-05-18
- 소스오브트루스: `tasks/047_compendium_vfx_preview/plan.md`

## Preflight

- `BattleVfxCatalog`, `BattleActorVfxSurface`, `SkillDefinitionAsset.VfxHookId` 구조 확인
- 046 도감 runtime surface의 presenter/view/viewstate 경계 확인
- project lock 때문에 Unity batch 검증이 막힐 수 있음을 사전 기록

## Phase 1 code-only

- `CompendiumVfxPreviewViewState` 추가
- `CompendiumPresenter`에 play token과 preview style resolver 추가
- `CompendiumView`에 replay action과 preview view 연결
- `CompendiumVfxPreviewView`를 view-only UITK scheduled animation으로 추가

## Phase 2 asset authoring

- `CompendiumPreview.uxml`에 preview stage, projectile, burst, pulse, replay button 추가
- `CompendiumPreview.uss`에 style별 색/움직임 표면 추가
- `UI_Town` localization bootstrap/table key 추가

## Phase 3 validation

- `git diff --check`
- `tools/test-harness-lint.ps1`
- `npx markdownlint-cli2 "tasks/047_compendium_vfx_preview/**/*.md"`
- `test-batch-fast` 재시도

## rollback / escape hatch

- `CompendiumDetailViewState.VfxPreview`, `CompendiumVfxPreviewView`, UXML preview stage, replay button만 제거하면 046 도감 목록/상세 표면으로 되돌아간다.
- 전투/세션/persistence truth는 변경하지 않으므로 rollback 영향이 UI에 한정된다.

## tool usage plan

- C# runtime UI 변경에는 `code-structure-guard` 기준을 적용한다.
- task 문서 변경에는 `docs-maintainer` 기준을 적용한다.
- Unity 확인은 가능한 경우 `tools/unity-bridge.ps1 test-batch-fast`를 우선 사용한다.

## loop budget

- C# preview runner 1 loop
- UXML/USS/localization 1 loop
- 검증 및 task handoff 1 loop
