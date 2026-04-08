# 작업 상태

## 메타데이터

- 작업명: Combat Sandbox Re-centering
- 담당: Codex
- 상태: active
- 최종수정일: 2026-04-09

## Current state

- menu/runtime/authoring/display policy 재중심화 코드를 반영했다.
- `SM/Play`는 fail-fast preflight + launch 경로로 축소했고, recovery/content/validation 메뉴는 `SM/Internal/*`로 이동시켰다.
- Combat Sandbox active handoff는 Inspector-first 편집 경로로 승격했고, `Window/SM/Combat Sandbox`는 library/history/results 보조 surface로 축소했다.
- layout/preview asset과 direct sandbox editor session cache를 추가했고, runtime binder는 typed contract 검증 + refresh 책임으로 축소했다.
- 문서, help/error copy, Steam display/input policy 문서를 갱신했다.
- validation evidence는 일부 회수했지만, Unity project lock과 connector timeout 때문에 fresh compile/test/runtime smoke는 끝까지 닫지 못했다.

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 / 다음 확인 |
| --- | --- | --- | --- |
| compile | sandbox/menu/runtime refactor 후 compile 유지 | 외부 blocker | `test-batch-fast`가 열린 Unity project lock으로 실패, `unity-bridge.ps1 compile`은 connector timeout으로 증거 미회수 |
| validator | legacy alias 제거와 docs/index sync 유지 | 부분 완료 | `docs-policy-check`, `smoke-check`, `test-harness-lint`, targeted `git diff --check` 통과. `docs-check`는 기존 markdownlint debt와 `.claude/worktrees/**` 복사본 때문에 실패 |
| targeted tests | sandbox preflight/cache/layout smoke 보강 | 부분 완료 | `CombatSandboxSceneLayoutCompilerTests` 추가. batch 실행은 project lock 때문에 미완료 |
| runtime smoke | direct sandbox / Town smoke / Full Loop 분리 유지 | 외부 blocker | `unity-bridge.ps1 compile/console`가 port 8090 timeout으로 실패 |

## Evidence

- source-of-truth task 생성:
  - `tasks/018_combat_sandbox_recentering/spec.md`
  - `tasks/018_combat_sandbox_recentering/plan.md`
  - `tasks/018_combat_sandbox_recentering/status.md`
- 코드/문서 반영:
  - `Assets/_Game/Scripts/Editor/Bootstrap/FirstPlayableBootstrap.cs`
  - `Assets/_Game/Scripts/Editor/Authoring/Inspectors/CombatSandboxConfigEditor.cs`
  - `Assets/_Game/Scripts/Editor/Authoring/CombatSandbox/CombatSandboxEditorSession.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/Sandbox/CombatSandboxSceneAssetTypes.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/FirstPlayableRuntimeSceneBinder.cs`
  - `docs/03_architecture/editor-sandbox-tooling.md`
  - `docs/03_architecture/unity-scene-flow.md`
  - `docs/07_release/display-and-input-policy.md`
- 검증:
  - `pwsh -File tools/test-harness-lint.ps1 -RepoRoot .` -> pass
  - `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .` -> pass
  - `pwsh -File tools/smoke-check.ps1 -RepoRoot .` -> pass
  - `git diff --check -- docs tasks tools/unity-bridge.ps1 tools/mcp/coplay-connection-runbook.md Assets/_Game/Scripts/Editor/Authoring/CombatSandbox Assets/_Game/Scripts/Editor/Authoring/Inspectors Assets/_Game/Scripts/Editor/Bootstrap Assets/_Game/Scripts/Editor/SeedData Assets/_Game/Scripts/Editor/Validation Assets/_Game/Scripts/Runtime/Unity Assets/Localization/StringTables Assets/Tests/EditMode` -> pass
  - `pwsh -File tools/unity-bridge.ps1 test-batch-fast` -> fail (`another Unity instance is running with this project open`)
  - `pwsh -File tools/unity-bridge.ps1 compile` -> fail (`timed out waiting for Unity (port 8090)`)
  - `pwsh -File tools/docs-check.ps1 -RepoRoot .` -> fail (repo pre-existing markdownlint debt in `docs/02_design/meta/augment-catalog-v1.md`, `docs/03_architecture/testing-strategy.md`, `CLAUDE.md`, `.claude/worktrees/**`, `Packages/com.coplaydev.unity-mcp/README.md`)

## Remaining blockers

- 기존 dirty worktree가 scene/authoring asset 쪽에 열려 있으므로 관련 파일 편집 시 사용자 변경을 보존해야 한다.
- Unity project lock 때문에 batchmode `test-batch-fast`를 회수하지 못했다.
- GUI Unity connector가 `port 8090`에서 ready로 돌아오지 않아 `compile` / `console` / smoke 증거를 회수하지 못했다.
- `docs-check`는 이번 작업 범위 밖의 기존 markdownlint debt와 `.claude/worktrees/**` 복사본까지 스캔해 실패한다.

## Deferred / debug-only

- safe area 실제 runtime adapter 연결
- `GameSessionState` 대규모 2차 분해
- broader sandbox history/compare UX

## Loop budget consumed

- compile-fix: 0
- refresh/read-console: 1 (`unity-bridge.ps1 compile`, `console` timeout)
- asset authoring retry: 0
- budget 초과 시 남긴 diagnosis: Unity lock / connector timeout으로 compile/runtime evidence가 외부 blocker 상태

## Handoff notes

- 다음 세션 시작 문서:
  - `tasks/018_combat_sandbox_recentering/status.md`
  - `docs/03_architecture/editor-sandbox-tooling.md`
  - `docs/03_architecture/unity-scene-flow.md`
- legacy alias를 다시 노출하지 않는다.
- runtime binder는 self-healing 확대가 아니라 typed contract 축소 방향으로만 수정한다.
- 다음 validation 우선순위:
  - 열린 Unity 인스턴스 정리 후 `pwsh -File tools/unity-bridge.ps1 test-batch-fast`
  - `pwsh -File tools/unity-bridge.ps1 compile`
  - 가능하면 `pwsh -File tools/unity-bridge.ps1 quick-battle-smoke`와 `prepare-playable`
