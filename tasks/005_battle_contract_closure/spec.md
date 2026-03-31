# 작업 명세

## 메타데이터

- 작업명: Battle Contract Closure
- 담당: Codex
- 상태: 진행 중
- 최종수정일: 2026-03-31
- 관련경로:
  - `Assets/_Game/Scripts/Runtime/Combat/**`
  - `Assets/_Game/Scripts/Runtime/Content/Definitions/**`
  - `Assets/_Game/Scripts/Runtime/Unity/**`
  - `Assets/_Game/Scripts/Editor/**`
  - `Assets/Tests/EditMode/**`
  - `Assets/Localization/StringTables/**`
  - `docs/**`
- 관련문서:
  - `docs/03_architecture/unity-agent-harness-contract.md`
  - `docs/03_architecture/combat-runtime-architecture.md`
  - `docs/03_architecture/localization-runtime-and-content-pipeline.md`
  - `docs/02_design/combat/combat-readability.md`
  - `tasks/001_mvp_vertical_slice/status.md`

## Goal

- Battle observer를 placeholder 단계에서도 읽히는 전투 계약으로
  끌어올리고, UI 평면화, footprint/slotting, range discipline,
  localization 누락 0건을 같은 변경 범위에서 닫는다.

## Authoritative boundary

- 이번 task는 battle contract를 `implicit prototype behavior`에서
  `profile-driven combat/runtime/presentation contract`로
  끌어올리는 단일 axis를 닫는다.
- source-of-truth 이동 대상:
  - overhead UI 규칙 -> `SM.Unity` presentation contract
  - footprint / spacing / slotting -> `SM.Combat` domain truth + authored profile
  - range discipline / reevaluation cadence / mobility scaffold ->
    `SM.Combat` behavior truth + authored profile
  - battle localization -> committed `UI_Battle` / `Combat_Log` tables
- 동시에 닫지 않을 축:
  - prefab/catalog spawn contract 재설계
  - stamina/equipment/morale 같은 신규 대형 시스템
  - facing 기반 block arc / 고급 tactical AI / 최종 animation polish

## In scope

- battle profile authoring 타입 추가와 archetype carry-through
- slot reservation, footprint separation, range band, ranged disengage scaffold
- hit resolution 서비스 분리와 crit/dodge/block/armor/resist 순서 도입
- world-space 머리 위 UI 제거와 screen-space overhead presenter 도입
- `UI_Battle` / `Combat_Log` localization 보강과 fallback guard
- sandbox gizmo / scenario / EditMode coverage 확장
- 관련 contract/spec/harness docs 갱신

## Out of scope

- runtime prefab authoring pipeline 전환
- melee 전용 dash/lunge full rollout
- stamina, public mental stat, accuracy/hit-rate 추가
- launch-floor catalog contract reopen
- compile green만으로 task 종료 주장

## asmdef impact

- 영향 asmdef:
  - `SM.Content`
  - `SM.Combat`
  - `SM.Meta`
  - `SM.Unity`
  - `SM.Editor`
  - `SM.Tests`
- 허용 의존 검토:
  - 새 authored definition은 `SM.Content`
  - pure runtime profile / slotting / resolver는 `SM.Combat`
  - compile/template carry-through는 `SM.Meta`
  - 화면 presenter / localizer binder는 `SM.Unity`
  - gizmo / validator / bootstrap은 `SM.Editor`
- cycle 위험:
  - `SM.Combat`가 `ScriptableObject`나 localization 타입을 참조하지 않게 유지
  - `SM.Meta`가 Unity scene/presenter 구현 세부를 알지 않게 유지

## persistence impact

- 저장 모델/포트/record 직접 변경은 없다.
- `SM.Meta`, `SM.Unity`, `SM.Persistence.Abstractions` 책임 분리는 유지한다.
- compile hash/provenance는 바뀌지만 persistence ownership 축은 이번 task 범위 밖이다.

## validator / test oracle

- validator:
  - archetype profile reference / profile range / localization coverage 검증 추가
- targeted tests:
  - spacing / slotting / range discipline / mobility /
    hit resolution / localization fallback EditMode test
- runtime path smoke:
  - `tools/unity-bridge.ps1 compile`
  - `tools/unity-bridge.ps1 bootstrap`
  - `tools/unity-bridge.ps1 report-battle`
  - `tools/unity-bridge.ps1 console`

## done definition

- compile green
- content/localization validator가 새 contract drift를 잡는다
- targeted EditMode tests가 spacing, range discipline,
  mobility scaffold, hit resolution, localization fallback을
  검증한다
- battle harness에서 overhead UI가 upright screen-space로 보이고,
  blob이 완화되며, `ui.battle.*` missing key가 0건이다
- evidence와 남은 defer는
  `tasks/005_battle_contract_closure/status.md`에 남긴다

## deferred

- facing 기반 guard arc
- melee engage dash/lunge runtime 소비
- hidden behavior coefficients 일부의 public stat 승격
- prefab-based head anchor authoring
