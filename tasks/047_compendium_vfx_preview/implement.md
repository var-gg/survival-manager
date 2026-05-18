# 047 Compendium VFX Preview Implement

- 상태: active
- 소유자: repository
- 최종수정일: 2026-05-18
- 소스오브트루스: `tasks/047_compendium_vfx_preview/implement.md`

## Phase summary

- Phase 1: 기존 battle VFX는 cue/semantic catalog 중심이고 skill hook 전용 catalog가 아직 없음을 확인했다.
- Phase 1: 도감에 view-only `CompendiumVfxPreviewView`를 추가해 `VfxHookId` 기반 쇼케이스 연출을 재생한다.
- Phase 1: 스킬 선택과 replay 버튼이 play token을 증가시켜 같은 스킬도 다시 재생되게 했다.
- Phase 2: `CompendiumPreview.uxml/uss`에 preview stage와 style별 연출 색을 추가했다.
- Phase 2: `UI_Town` localization seed/table에 preview/replay/caption key를 추가했다.

## Deviation

실제 particle prefab을 hook별로 resolve하지는 않았다. 현재 `BattleVfxCatalog`는 skill hook id가 아니라 `BattlePresentationCueType`과 `BattleAnimationSemantic` 중심이라, 이번 loop에서는 도감 안에서 C# scheduled UITK preview를 먼저 닫았다.

## Blockers

- 열린 Unity 인스턴스가 project lock을 잡고 있으면 batchmode FastUnit이 진입 전 실패한다.
- hook id별 prefab catalog 확장 정책은 별도 설계 결정이 필요하다.

## Diagnostics

- preview style은 `BattleSkillSpec.Kind`, `DamageType`, `SkillDelivery`, `AppliedStatuses`, `AreaEffectFamily`에서 파생한다.
- preview는 `GameSessionState`, save schema, combat resolver를 읽거나 쓰지 않는다.
- 상태/시너지/캐릭터 탭에서는 preview stage를 숨긴다.

## Why this loop happened

스킬 VFX 제작 전에는 스킬 목록과 hook을 빠르게 훑어 보며 감을 잡는 표면이 필요하다. 도감에서 클릭 즉시 연출이 보이면 다음 단계의 실제 particle authoring과 이미지/아이콘 검수도 같은 Play Mode 루프에서 이어갈 수 있다.
