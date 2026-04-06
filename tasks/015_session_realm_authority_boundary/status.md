# 작업 상태

## 메타데이터

- 작업명: Session Realm Authority Boundary
- 담당: Codex
- 상태: in_progress
- 최종수정일: 2026-04-06

## Current state

- realm/capability/query/command seam, offline adapter, Boot/Town/Reward UI 표면, 관련 design/architecture/ADR/task 문서가 반영됐다.
- local lint, docs policy, smoke, MSBuild compile check는 통과했다.
- Unity batch 기반 fresh evidence는 project lock 때문에 아직 불완전하다.
- `docs-check`는 이번 작업 범위 밖의 기존 markdownlint 이슈 때문에 전체 green을 만들지 못했다.

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 / 다음 확인 |
| --- | --- | --- | --- |
| compile | C# compile green | 통과 | `MSBuild.exe survival-manager.sln` |
| validator | test harness lint green | 통과 | `tools/test-harness-lint.ps1` |
| docs policy | 문서 정책 검사 | 통과 | `tools/docs-policy-check.ps1` |
| docs lint | 저장소 문서 lint | blocker | 기존 repo-wide markdownlint 이슈로 실패 |
| targeted tests | `FastUnit`, `EditMode` | 부분확인 | `test-batch-fast`는 lock 때문에 stale result 경고 |
| smoke | 기본 smoke check | 통과 | `tools/smoke-check.ps1` |
| runtime smoke | Boot/Town/Reward contract | 미완료 | Unity lock 해소 후 bootstrap / editmode 재확인 필요 |

## Evidence

- `pwsh -File tools/test-harness-lint.ps1 -RepoRoot .`
- `MSBuild.exe survival-manager.sln /m /t:Build /p:Configuration=Debug /v:m`
- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit`
- `pwsh -File tools/unity-bridge.ps1 bootstrap`
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
- `pwsh -File tools/docs-check.ps1 -RepoRoot .`
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`

## Remaining blockers

- Unity project lock 때문에 fresh `test-batch-fast` / `test-batch-edit` / `bootstrap` evidence가 안정적으로 회수되지 않는다.
- `docs-check`는 `docs/**`, `CLAUDE.md`, `Packages/com.coplaydev.unity-mcp/README.md`, `.claude/worktrees/**`에 남아 있는 기존 markdownlint 이슈 때문에 실패한다.

## Deferred / debug-only

- `OnlineMockAdapter`
- actual server adapter
- official arena settlement / reward delivery
- `SaveProfile` concern 분해

## Loop budget consumed

- compile-fix: 1
- refresh/read-console: 0
- asset authoring retry: 1
- budget 초과 시 남긴 diagnosis:
  - unity bridge `bootstrap` timeout
  - batch test project lock warning

## Handoff notes

- Unity lock이 풀리면 `test-batch-fast`, `test-batch-edit`, `bootstrap`를 다시 돌려 status evidence를 갱신한다.
- repo-wide markdownlint 이슈를 별도 task로 정리한 뒤 `tools/docs-check.ps1`를 다시 돌린다.
- Boot scene canonical save가 필요하면 `FirstPlayableSceneInstaller.RepairFirstPlayableScenes()`를 우선 실행한다.
