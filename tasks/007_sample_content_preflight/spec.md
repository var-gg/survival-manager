# 작업 명세

## 메타데이터

- 작업명: sample content preflight
- 담당: Codex
- 상태: in_progress
- 최종수정일: 2026-04-01
- 관련경로:
  - `Assets/_Game/Scripts/Editor/SeedData/SampleSeedGenerator.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/RuntimeCombatContentLookup.cs`
  - `Assets/_Game/Scripts/Editor/Bootstrap/FirstPlayableContentBootstrap.cs`
  - `Assets/Tests/EditMode/**`
  - `tools/unity-bridge.ps1`
- 관련문서:
  - `docs/05_setup/unity-cli.md`
  - `docs/03_architecture/content-loading-contract.md`
  - `tasks/2026-03-unity-cli-local-lane/status.md`

## Goal

- sample content 재생성을 test/runtime 경로에서 분리하고, explicit preflight에서만 canonical authoring을 갱신하게 만든다.

## Authoritative boundary

- 이번 task는 `canonical sample content readiness`와 `explicit generation lane`만 닫는다.
- source-of-truth는 committed canonical asset + explicit preflight command 조합으로 고정한다.
- failing EditMode suite의 개별 로직 수리는 이 task에서 닫지 않는다.

## In scope

- sample content readiness API 추가
- runtime/editor/test의 implicit regeneration 제거
- explicit preflight wrapper verb 추가
- 관련 setup/architecture/task 문서 갱신
- canonical content 1회 explicit regenerate와 검증

## Out of scope

- failing EditMode test 18건 전체 수리
- launch floor content/scene 설계 변경
- sample content schema 재설계

## asmdef impact

- `SM.Editor`, `SM.Unity`, `SM.Tests.EditMode` 편집이 예상된다.
- 새 asmdef 추가/분리는 없다.
- runtime assembly가 editor implementation에 직접 compile-time 의존하지 않도록 `#if UNITY_EDITOR` reflection 경계는 유지한다.

## persistence impact

- 저장 모델과 persistence schema 변화는 없다.
- canonical authored asset과 preflight lane 계약만 바뀐다.

## validator / test oracle

- `pwsh -File tools/unity-bridge.ps1 compile`
- `pwsh -File tools/unity-bridge.ps1 seed-content`
- targeted EditMode test 또는 direct filtered CLI test로 implicit regeneration 제거 확인
- 필요 시 full `test-edit`는 red suite 현황만 회수하고, 이번 task acceptance는 regeneration side-effect 제거를 우선한다.

## done definition

- runtime/test 경로가 더 이상 `EnsureCanonicalSampleContent()`로 asset rewrite를 트리거하지 않는다.
- explicit preflight command가 canonical sample content regenerate를 수행한다.
- canonical content regenerate 후 test run 중 추가 asset rewrite가 발생하지 않는 근거를 남긴다.
- setup/architecture/task 문서와 index가 새 운영 기준을 반영한다.

## deferred

- red EditMode suite 개별 원인 분석과 수리
- canonical content drift의 세부 원인별 validator 정교화
- scene dirty/unrelated worktree 정리
