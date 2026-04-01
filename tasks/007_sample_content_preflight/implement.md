# 작업 구현

## 메타데이터

- 작업명: sample content preflight
- 담당: Codex
- 상태: in_progress
- 최종수정일: 2026-04-01
- 실행범위:
  - sample content readiness API
  - runtime/test preflight contract
  - CLI wrapper explicit seed lane
  - setup/architecture/task docs

## Phase log

- Phase 0 preflight:
  - 기존 `EnsureCanonicalSampleContent()` 호출 지점이 test/runtime/bootstrap에 퍼져 있음을 확인했다.
  - `RuntimeCombatContentLookup`가 editor에서 reflection으로 implicit generation을 트리거하는 구조를 확인했다.
- Phase 1 code-only:
  - `SampleSeedGenerator`에 `RequireCanonicalSampleContentReady`, `TryGetCanonicalSampleContentReadinessIssue`, `GetCanonicalMinimumContentFailures`를 추가했다.
  - `RuntimeCombatContentLookup`는 editor에서 더 이상 `EnsureCanonicalSampleContent()`를 호출하지 않고 readiness failure를 caller error로 승격한다.
  - EditMode test setup들과 `FirstPlayableContentBootstrap` helper는 implicit generation 대신 read-only readiness 확인만 수행한다.
  - `SampleSeedGenerator.CreateAsset<T>`는 `m_Script: {fileID: 0}` stale YAML을 감지하면 delete/recreate 하도록 보강했다.
  - generation 끝의 `ForceReserializeCanonicalAssets()`를 제거했다.
- Phase 2 asset authoring:
  - `pwsh -File tools/unity-bridge.ps1 seed-content`로 canonical content를 explicit regenerate했다.
  - regenerate 결과 sample content asset 다수가 갱신되었고, 이후 targeted test는 asset rewrite 없이 실행됐다.
- Phase 3 validation:
  - `pwsh -File tools/unity-bridge.ps1 compile` 통과
  - direct filtered CLI test `StatV2AndSandboxTests.CombatSandboxWindow_BindsAndBuildsRunRequest_WithoutPlayMode` 통과
  - same filtered test 전후 `StableTags`/`Archetypes` 대표 asset의 hash와 write time이 변하지 않음을 확인했다.
  - full `pwsh -File tools/unity-bridge.ps1 test-edit`는 `67 total / 58 passed / 9 failed`까지 줄었고, wrapper가 artifact를 정상 회수했다.
  - `docs-policy-check`, `smoke-check` 통과
  - `docs-check`는 repo-wide markdownlint debt `819`건으로 계속 red였다.

## deviation

- 초기에는 readiness gate에 launch-floor count coverage까지 넣었지만, 이 task의 목표가 regenerate trigger 분리이지 full catalog completeness 강제가 아니므로 다시 minimum root + drift gate로 낮췄다.

## blockers

- explicit regenerate 이후에도 canonical asset YAML은 `m_Script: {fileID: 0}` / `m_EditorClassIdentifier` 형태를 유지한다.
- 이 상태에서도 targeted test는 rewrite 없이 통과했지만, 왜 Unity가 이 형식으로 serialize하는지는 아직 미해결이다.
- full EditMode suite는 전투 로직/spacing 쪽 실패 9건이 남아 있다.

## diagnostics

- `SampleSeedGenerator.Generate()` 호출 중 `AssetDatabase.CreateAsset/DeleteAsset`가 connector restart를 유발한다.
- `test-edit` 연결 끊김의 1차 원인은 wrapper가 아니라 test 중 asset rewrite/domain reload다.
- explicit `seed-content` 실행 로그에는 stale asset replace가 대량으로 찍히고, 마지막에 `SM sample content generated under Resources...`가 기록됐다.
- filtered heavy test 결과는 `1 passed / 0 failed`였고, 관측한 representative asset 4개는 hash/write time 변화가 없었다.
- full EditMode suite 최신 결과는 `67 total / 58 passed / 9 failed`이며 첫 실패는 `BattleResolutionTests.Anchor_Assignment_Changes_First_Contact_Timing`이다.
- docs harness 검증은 `docs-policy-check`, `smoke-check` 통과, `docs-check`는 기존 전역 debt 때문에 실패였다.

## why this loop happened

- test/runtime 경로에서 canonical content를 “없으면 생성”이 아니라 “불완전하면 regenerate”까지 수행하도록 열어둔 탓에, read path가 write path를 겸했다.
- canonical authoring drift가 committed asset에 남아 있는 동안 test run이 implicit repair를 수행했고, 그 과정이 connector restart와 timeout을 유발했다.
- 추가로 sample generator의 blind force-reserialize와 stale YAML replace가 섞이면서, explicit repair와 read-path verification의 경계가 흐려졌다.
