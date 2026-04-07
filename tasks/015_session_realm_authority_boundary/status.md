# 작업 상태

## 메타데이터

- 작업명: Session Realm Authority Boundary
- 담당: Codex
- 상태: in_progress
- 최종수정일: 2026-04-08

## Current state

- realm/capability/query/command seam, offline adapter, hidden future seam 정리는 유지한 채 `OfflineLocal` persistence hardening이 추가됐다.
- `JsonSaveRepository`에 manifest/hash, temp write, backup fallback, quarantine, corrupt-vs-missing 분리 로직을 넣었다.
- `SessionCheckpointResult`, smoke lane 분리, Town manual load guard, reward settlement idempotency/resume, operational telemetry, summary-first instrumentation 정책을 반영했다.
- `tools/test-harness-lint.ps1`, `tools/docs-policy-check.ps1`, `tools/smoke-check.ps1`는 fresh green이다.
- Unity batch 기반 fresh evidence는 project lock 때문에 아직 불완전하다.
- `docs-check`는 이번 작업 범위 밖의 기존 markdownlint 이슈 때문에 전체 green을 만들지 못했다.

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 / 다음 확인 |
| --- | --- | --- | --- |
| compile | C# compile green | 미확정 | Unity batch lock 때문에 fresh compile evidence 미회수, 로컬 MSBuild는 `.NETFramework,Version=v4.7.1` reference assemblies 부재 |
| validator | test harness lint green | 통과 | `tools/test-harness-lint.ps1` |
| docs policy | 문서 정책 검사 | 통과 | `tools/docs-policy-check.ps1` |
| docs lint | 저장소 문서 lint | blocker | 기존 repo-wide markdownlint 이슈로 실패 |
| targeted tests | `FastUnit`, `EditMode` | 부분확인 | `test-batch-fast`는 lock 때문에 stale result 경고 |
| smoke | 기본 smoke check | 통과 | `tools/smoke-check.ps1` |
| runtime smoke | Boot/Town/Reward contract | 미완료 | Unity lock 해소 후 bootstrap / editmode 재확인 필요 |

## Evidence

- 핵심 코드:
  - `Assets/_Game/Scripts/Runtime/Persistence/Json/JsonSaveRepository.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/OfflineLocalSessionAdapter.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/SessionRealmCoordinator.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/GameSessionRoot.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/GameSessionState.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/UI/Town/TownScreenPresenter.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/UI/Reward/RewardScreenPresenter.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/BattleScreenController.cs`
- 새/갱신 테스트:
  - `Assets/Tests/EditMode/SaveRecoveryTests.cs`
  - `Assets/Tests/EditMode/SessionRealmCoordinatorTests.cs`
  - `Assets/Tests/EditMode/RunLoopContractFastTests.cs`
- `pwsh -File tools/test-harness-lint.ps1 -RepoRoot .`
- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
- `pwsh -File tools/docs-check.ps1 -RepoRoot .`
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`

## Remaining blockers

- Unity project lock 때문에 fresh `test-batch-fast` / `test-batch-edit` / `bootstrap` evidence가 안정적으로 회수되지 않는다.
- local MSBuild compile은 machine에 `.NETFramework,Version=v4.7.1` reference assemblies가 없어 Unity 바깥 fallback compile 근거로 쓰기 어렵다.
- `docs-check`는 `docs/**`, `CLAUDE.md`, `Packages/com.coplaydev.unity-mcp/README.md`, `.claude/worktrees/**`에 남아 있는 기존 markdownlint 이슈 때문에 실패한다.

## Deferred / debug-only

- `OnlineAuthoritative`
- official arena/PvP settlement/evidence
- `OnlineMockAdapter`
- actual server adapter
- `SaveProfile` concern 분해
- paid asset 이후 readability retune / target hardware perf certification

## Loop budget consumed

- compile-fix: 2
- refresh/read-console: 0
- asset authoring retry: 1
- budget 초과 시 남긴 diagnosis:
  - batch test project lock warning
  - local MSBuild reference assemblies missing

## Handoff notes

- Unity lock이 풀리면 `test-batch-fast`, normal playable smoke, quick battle smoke evidence를 다시 돌려 status evidence를 갱신한다.
- repo-wide markdownlint 이슈를 별도 task로 정리한 뒤 `tools/docs-check.ps1`를 다시 돌린다.
- runtime 운영 계약은 `docs/06_production/runtime-hardening-contract.md`를 기준으로 본다.
