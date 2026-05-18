# 048 Skill VFX Animation Coverage Audit Spec

## 메타데이터

- 작업명: 스킬 VFX/애니메이션 커버리지 감사
- 담당: repository
- 상태: active
- 최종수정일: 2026-05-18
- 관련경로: `Assets/Resources/_Game/Content/Definitions/Skills`
- 관련경로: `Assets/Epic Toon FX`, `Assets/TriForge Assets`, `Assets/Kevin Iglesias`
- 관련문서: `tasks/047_compendium_vfx_preview/status.md`
- 관련문서: `tasks/048_skill_vfx_animation_coverage_audit/report.md`

## Goal

현재 정의된 88개 스킬을 실제 도감/VFX 검수 단계로 넘기기 전에, 유료 VFX와 보유 애니메이션이 스킬 표현을 감당할 수 있는지 평가하고 다음 구현 단위를 닫는다.

## Authoritative boundary

- 이번 task는 스킬 presentation의 source-of-truth를 개별 prefab one-off가 아니라 `skill -> presentation family -> skin/accent -> animation semantic -> cue sequence` 축으로 정리한다.
- 유료 에셋 원본은 source-of-truth가 아니라 재료 풀이다.
- 이번 task에서 C# runtime catalog, ScriptableObject schema, prefab authoring을 직접 변경하지 않는다.
- Pindoc wiki 발행 도구가 열려 있지 않은 상태에서는 repo task/report가 handoff source가 된다.

## In scope

- 현재 스킬 데이터의 표현 수요 수치화
- Epic Toon FX, TriForge, Kevin Iglesias 에셋의 표현 공급 범위 감사
- 현재 C# 런타임 연결 상태와 raw asset 보유량의 차이 정리
- 다음 작업에서 닫아야 할 `SkillPresentationFamily` 설계 후보와 pilot 스킬 선정

## Out of scope

- 유료 에셋 원본 prefab 수정
- 실제 skill hook별 prefab mapping authoring
- 도감 3D stage, dummy actor, target playback 구현
- 스킬 아이콘 이미지 생성
- VFX prefab composition, chroma key, texture authoring

## asmdef impact

- 이번 task는 문서 감사만 수행하므로 asmdef 변경은 없다.
- 다음 구현 task에서는 `SM.Combat` 또는 content snapshot 영역에 presentation family enum/value를 둘지 먼저 결정해야 한다.
- Unity prefab catalog는 `SM.Unity` adapter 경계에 남겨야 하며, pure combat resolver가 UnityEngine prefab을 참조하면 안 된다.

## persistence impact

- 이번 task는 저장 모델 변경이 없다.
- 다음 구현에서 family/skin이 save state에 들어가면 안 된다. 스킬 정의에서 파생되는 표시 데이터로 유지한다.
- 플레이어가 장착한 스킬 id만 저장하고 presentation은 content lookup으로 resolve한다.

## validator / test oracle

- 이번 task: docs policy, docs check, smoke check, markdownlint.
- 다음 구현 task: 모든 skill에 presentation family, skin/accent, animation semantic, vfx hook이 존재하는지 검증하는 content validator.
- 다음 구현 task: 도감에서 대표 pilot 스킬을 선택했을 때 prefab catalog fallback 없이 의미 있는 preview cue가 나오는지 smoke.

## done definition

- 보유 에셋과 스킬 수요의 coverage matrix가 문서화된다.
- raw asset 수량과 현재 runtime catalog 수량의 차이가 명확히 기록된다.
- Green/Yellow/Red coverage 판정과 다음 구현 순서가 정리된다.
- task 문서 검증 명령이 통과한다.

## deferred

- `SkillPresentationFamily` C# 타입 도입
- hook id별 real prefab resolver
- 도감 3D VFX preview stage
- 상태이상/시너지 전용 VFX playback
- 스킬 아이콘 imagegen 배치 생성
