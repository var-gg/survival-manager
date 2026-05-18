# 046 Compendium Runtime Surface Spec

- 상태: active
- 소유자: repository
- 최종수정일: 2026-05-18
- 소스오브트루스: `tasks/046_compendium_runtime_surface/spec.md`

## Goal

Town Play Mode에서 시스템 도감을 열어 스킬, 상태, 시너지, 캐릭터 언락 표면을 확인할 수 있게 한다.
첫 목표는 스킬 이름/설명/아이콘/상태/VFX hook을 에디터 인스펙터 없이 런타임에서 검수하는 것이다.

## Authoritative boundary

- 전투/콘텐츠 truth는 `ICombatContentLookup`, `CombatContentSnapshot`, authored `SkillDefinitionAsset`이다.
- 도감은 조회/표시 표면이며 전투 수치, 언락, 획득 모델을 새로 소유하지 않는다.
- 제품/기획 결정의 최종 정착지는 Pindoc Wiki다. 현재 세션에는 Pindoc write tool이 없어 repo task handoff로 임시 기록한다.

## In scope

- Town hub 유틸리티 엔트리 `Compendium` 추가
- Play Mode modal 기반 `Skills / Status / Synergy / Characters` 탭
- 스킬 전체 공개 모델: 이름, 설명, 슬롯, 클래스, 데미지, 전달 방식, 대상 규칙, 파워, 쿨다운, 상태, `IconId`, `VfxHookId`
- 상태/시너지 전체 공개 모델: 핵심 runtime rule과 VFX cue/hook 표시
- 캐릭터 도감 MVP: 슬롯은 보이되 미해금 캐릭터는 실루엣/locked 라벨로 표시

## Out of scope

- 실제 VFX 재생 프리뷰 버튼
- 캐릭터 언락 save schema 확정
- 스킬 장착식 vs draft/gacha 획득 모델 확정
- 에디터 전용 inspector 또는 authoring window
- 시크릿 슬롯/이스터에그 최종 정책

## asmdef impact

새 asmdef는 만들지 않는다. 변경은 기존 `SM.Unity` runtime UI와 기존 EditMode/FastUnit 텍스트 검증 안에 둔다.

## persistence impact

없음. 캐릭터 locked/unlocked 표시는 현재 `SaveProfile.Heroes`의 `CharacterId`/`HeroId`만 읽는다.

## validator / test oracle

- `tools/test-harness-lint.ps1`
- `git diff --check`
- `TownScreenUxmlHubLayoutTests`
- Unity batch `test-batch-fast`는 프로젝트 락이 풀리면 재실행한다.

## done definition

- Town에서 도감 버튼이 보인다.
- 도감 modal이 Play Mode에서 열리고 닫힌다.
- 스킬 88개가 localization 기반 이름/설명과 icon/VFX hook을 표시한다.
- 상태/시너지/캐릭터 탭이 전투 truth를 복제하지 않고 조회한다.
- UI key는 ko/en table과 bootstrap seed에 반영된다.

## deferred

- Pindoc Wiki Decision 발행: `시스템/시너지/스킬 전체 공개, 캐릭터 점진 언락, 시크릿 선택 숨김`
- VFX hook preview runner
- 캐릭터 도감의 unlock source와 20명/22명 최종 roster policy
