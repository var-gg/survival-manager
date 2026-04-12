# 작업 명세

## 메타데이터

- 작업명: Combat Sandbox Re-centering
- 담당: Codex
- 상태: active
- 최종수정일: 2026-04-09
- 관련경로:
  - `Assets/_Game/Scripts/Editor/Bootstrap/**`
  - `Assets/_Game/Scripts/Editor/Authoring/CombatSandbox/**`
  - `Assets/_Game/Scripts/Runtime/Unity/Sandbox/**`
  - `Assets/_Game/Scripts/Runtime/Unity/UI/**`
  - `Assets/_Game/Scripts/Runtime/Unity/GameSession*.cs`
  - `docs/05_setup/**`
  - `docs/06_production/**`
  - `docs/07_release/**`
- 관련문서:
  - `docs/03_architecture/editor-sandbox-tooling.md`
  - `docs/03_architecture/unity-scene-flow.md`
  - `docs/03_architecture/ui-runtime-architecture.md`
  - `docs/03_architecture/unity-boundaries.md`
  - `tasks/001_mvp_vertical_slice/status.md`

## Goal

- Combat Sandbox를 inspector-first 전투 실험 도구로 재중심화하고, Full Loop와 internal recovery lane의 책임을 분리한다.

## Authoritative boundary

- 이번 task는 `SM/Play` 엔트리 계약, `CombatSandboxConfig` authoring contract, direct sandbox runtime bootstrap, scene/layout preview contract를 한 축으로 닫는다.
- source-of-truth는 `SM/전체테스트`, `SM/전투테스트`, `Window/SM/Combat Sandbox`, active `CombatSandboxConfig`, `docs/07_release/display-and-input-policy.md`로 이동한다.
- Town smoke lane의 존재는 유지하지만 canonical newcomer lane이나 sandbox authoring source로 승격하지 않는다.

## In scope

- `SM/Quick Battle`, `SM/Setup/Prepare Observer Playable`, `tools/unity-bridge.ps1 bootstrap` 제거
- `SM/Internal/Recovery`, `SM/Internal/Validation`, `SM/Internal/Content` 메뉴 재배치
- `SM/Play` preflight를 fail-fast 검사로 축소
- `CombatSandboxConfigEditor`를 primary editing surface로 승격
- cached editor compile session 추가
- `CombatSandboxConfig`에 layout / preview settings asset reference 추가
- sandbox scene layout / preview asset 도입
- runtime binder의 reflection/self-healing 축소
- Steam 1차 해상도 / input policy 문서화와 `PanelSettings` 기본값 고정

## Out of scope

- Town `Quick Battle (Smoke)` 이름 변경
- 모바일 touch UX, 모바일 전용 HUD 재배치, 모바일 성능 튜닝
- `GameSessionState` 전체 분해
- art/presentation polish
- online seam / authoritative backend work

## asmdef impact

- 영향 asmdef: `SM.Unity`, `SM.Editor`, `SM.Tests.EditMode`, `SM.Tests.PlayMode`
- `SM.Editor`가 `SM.Unity.Sandbox` authoring/runtime 타입을 계속 사용하되 runtime -> editor 참조는 추가하지 않는다.
- 새 asset/type은 기존 asmdef 내부에 배치하고 추가 interface는 만들지 않는다.
- cycle 위험은 `GameSessionState`와 editor session/cache helper가 서로 직접 참조하지 않게 막아 관리한다.

## persistence impact

- canonical Town/Full Loop save 계약은 유지한다.
- direct sandbox는 dedicated smoke namespace와 transient handoff만 사용한다.
- `CombatSandboxConfig`는 authored handoff asset이며 save truth를 대신하지 않는다.
- reward/progression persistence는 sandbox direct lane 밖에 둔다.

## validator / test oracle

- play entry preflight fail-fast validator
- sandbox compile cache invalidation FastUnit
- layout symmetry / side-swap consistency FastUnit
- localization/help/error copy lint
- targeted PlayMode smoke: direct sandbox hidden CTA, Town smoke secondary lane, Full Loop regression

## done definition

- user-facing 메뉴가 세 개로 정리되고 legacy alias가 제거된다.
- direct sandbox entry는 자동 repair 없이 preflight + Battle open + play만 수행한다.
- active `CombatSandboxConfig` inspector에서 compile/preview/run/play loop를 돌릴 수 있다.
- layout/preview settings asset이 scene controller 하드코딩을 대체한다.
- release/display policy 문서와 runtime default resolution이 `1920x1080` 기준으로 고정된다.
- evidence는 `tasks/018_combat_sandbox_recentering/status.md`에 남긴다.

## deferred

- giant class 2차 분해
- boss/mirror/batch history library 확장
- safe area 실제 runtime adapter 배선
- Town smoke UI copy polishing
