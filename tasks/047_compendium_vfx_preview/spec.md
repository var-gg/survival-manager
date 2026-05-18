# 047 Compendium VFX Preview Spec

- 상태: active
- 소유자: repository
- 최종수정일: 2026-05-18
- 소스오브트루스: `tasks/047_compendium_vfx_preview/spec.md`

## Goal

Town 도감의 스킬 목록에서 스킬을 선택하면 상세 영역에서 VFX hook 기반 쇼케이스 연출이 즉시 재생되게 한다.
이번 목표는 실제 파티클 prefab 제작 전에도 스킬별 연출 감성과 hook 검수 흐름을 Play Mode 안에서 닫는 것이다.

## Authoritative boundary

- 스킬 truth는 `CombatContentSnapshot.SkillCatalog`와 authored `SkillDefinitionAsset.VfxHookId`다.
- 도감 프리뷰는 view-only 쇼케이스이며 전투 판정, 데미지, cooldown, save state를 만들지 않는다.
- 실제 VFX prefab catalog 매핑은 `BattleVfxCatalog`와 후속 asset authoring task가 소유한다.

## In scope

- 스킬 상세 영역에 VFX preview stage 추가
- 스킬 선택 시 자동 재생
- 선택된 스킬 replay 버튼
- `BattleSkillSpec`의 delivery, damage type, kind, status, area family 기반 preview style 분류
- UI localization key와 task handoff 갱신

## Out of scope

- hook별 실제 particle prefab 생성 및 catalog 등록
- Battle scene actor/socket 기반 프리뷰 카메라
- VFX 타임라인 저장/편집 도구
- 상태/시너지 탭 전용 VFX 재생

## asmdef impact

새 asmdef는 만들지 않는다. 변경은 기존 `SM.Unity` runtime UI와 기존 FastUnit 텍스트 검증에 둔다.

## persistence impact

없음. 프리뷰는 선택된 도감 row와 local play token만 사용한다.

## validator / test oracle

- `TownScreenUxmlHubLayoutTests`
- `git diff --check`
- `tools/test-harness-lint.ps1`
- task markdownlint
- Unity batch `test-batch-fast`는 project lock이 풀리면 재실행한다.

## done definition

- 스킬 row를 클릭하면 상세 preview stage가 다시 재생된다.
- replay 버튼으로 선택된 스킬 연출을 다시 볼 수 있다.
- preview stage가 `VfxHookId`를 표시한다.
- 비스킬 탭에서는 preview stage가 숨겨진다.
- C# 구현은 전투/runtime truth를 복제하지 않는다.

## deferred

- `BattleVfxCatalog`를 hook id 단위로 확장할지, cue/semantic 단위 generic catalog를 유지할지 결정
- 실제 particle prefab authoring 및 에디터 preview window
- 도감 preview에서 actor silhouette, camera, target dummy를 붙이는 3D stage
