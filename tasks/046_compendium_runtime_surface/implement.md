# 046 Compendium Runtime Surface Implement

- 상태: active
- 소유자: repository
- 최종수정일: 2026-05-18
- 소스오브트루스: `tasks/046_compendium_runtime_surface/implement.md`

## Phase summary

- Phase 1: `ContentTextResolver`에 `GetStatusName`, `GetStatusDescription`, `GetSynergyDescription` 추가.
- Phase 1: Town runtime UI에 `CompendiumPresenter`, `CompendiumView`, `CompendiumViewState` 추가.
- Phase 2: `CompendiumPreview.uxml/uss`와 Town hub 연결을 추가.
- Phase 2: `UI_Common`/`UI_Town` localization table과 `LocalizationFoundationBootstrap` seed를 갱신.

## Deviation

Pindoc write tool이 현재 세션에 노출되지 않아 wiki artifact를 직접 발행하지 못했다. 대신 Pindoc에 들어갈 결정 요약과 후속 task를 이 task 문서에 임시로 고정했다.

## Blockers

- 열린 Unity 인스턴스가 project lock을 잡고 있어 batchmode FastUnit이 진입 전 실패한다.
- 로컬 `dotnet` SDK가 없어 `.sln` 기반 C# 빌드를 대체 검증으로 사용할 수 없다.

## Diagnostics

- 도감은 read-only라 `GameSessionState`나 persistence schema를 변경하지 않았다.
- 캐릭터 unlock은 현재 profile 보유 hero 기준으로만 표시한다. 진행 기반 unlock rule은 후속 Pindoc/implementation 결정이 필요하다.

## Why this loop happened

스킬/VFX 검수는 인스펙터보다 Play Mode 도감에서 확인하는 편이 빠르다. 도감을 먼저 붙이면 다음 VFX 작업에서 `SkillDefinitionAsset.IconId`와 `VfxHookId`를 같은 화면에서 검수할 수 있다.
