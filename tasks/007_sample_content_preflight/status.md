# 작업 상태

## 메타데이터

- 작업명: sample content preflight
- 담당: Codex
- 상태: in_progress
- 최종수정일: 2026-04-01

## Current state

- `implicit regeneration 제거 + explicit preflight lane 추가 + canonical asset 1회 regenerate`를 구현했다.
- targeted evidence까지 확보했고 handoff 가능한 상태다.

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 / 다음 확인 |
| --- | --- | --- | --- |
| compile | sample content readiness 코드가 compile green | 충족 | `pwsh -File tools/unity-bridge.ps1 compile` |
| validator | 문서/운영 변경이 docs harness를 통과 | 부분 충족 | `docs-policy-check`, `smoke-check` 통과 / `docs-check`는 repo-wide markdownlint debt `819`건으로 red |
| targeted tests | runtime/test 경로가 더 이상 implicit rewrite를 트리거하지 않음 | 충족 | filtered `CombatSandboxWindow_BindsAndBuildsRunRequest_WithoutPlayMode` 1 pass + representative asset hash/write time unchanged |
| runtime smoke | explicit seed preflight가 별도 lane으로 동작 | 충족 | `pwsh -File tools/unity-bridge.ps1 seed-content` |

## Evidence

- 시작 컨텍스트: `AGENTS.md`, `docs/index.md`, `docs/05_setup/index.md`, `tasks/2026-03-unity-cli-local-lane/status.md`
- 암묵 regeneration 호출 지점 확인:
  - `Assets/_Game/Scripts/Runtime/Unity/RuntimeCombatContentLookup.cs`
  - `Assets/_Game/Scripts/Editor/Bootstrap/FirstPlayableContentBootstrap.cs`
  - `Assets/Tests/EditMode/*.cs`
- 실행 근거:
  - `pwsh -File tools/unity-bridge.ps1 compile`
  - `pwsh -File tools/unity-bridge.ps1 seed-content`
  - direct filtered CLI test `SM.Tests.EditMode.StatV2AndSandboxTests.CombatSandboxWindow_BindsAndBuildsRunRequest_WithoutPlayMode`
  - `pwsh -File tools/unity-bridge.ps1 test-edit` => `67 total / 58 passed / 9 failed`
  - `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
  - `pwsh -File tools/docs-check.ps1 -RepoRoot .` => repo-wide markdownlint debt `819`
  - `pwsh -File tools/smoke-check.ps1 -RepoRoot .`

## Remaining blockers

- full EditMode suite 9건은 별도 수리가 필요하다.
- explicit regenerate 결과로 canonical asset 다수가 수정됐으므로, 커밋 시 unrelated scene/localization 변경과 분리해서 stage해야 한다.
- sample asset YAML이 여전히 `m_Script: {fileID: 0}` 형태인 이유는 후속 진단이 필요하다.

## Deferred / debug-only

- 남은 9개 failing EditMode test의 개별 수리
- unrelated scene/localization worktree 정리
- sample asset serialization format 원인 분석

## Loop budget consumed

- compile-fix: 4/4
- refresh/read-console: 3/4
- asset authoring retry: 2/2
- budget 초과 시 남긴 diagnosis: explicit regenerate는 성공했지만 sample asset serialization 형식은 추가 원인 분석이 필요하다.

## Handoff notes

- 다음 세션 시작 문서: 이 `status.md`, `docs/05_setup/unity-cli.md`
- 금지 반복 루프: test/runtime 호출에서 implicit sample-content regeneration 재도입 금지
- 필요한 preflight: `seed-content`를 먼저 실행한 뒤, representative asset hash/write time을 잡고 targeted test로 rewrite 여부를 본다.
- 다음 구현 축은 남은 9개 EditMode failure 수리다. sample-content regenerate 문제와 같은 task로 다시 섞지 않는다.
