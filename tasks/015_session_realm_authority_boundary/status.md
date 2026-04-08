# 작업 상태

## 메타데이터

- 작업명: Session Realm Authority Boundary
- 담당: Codex
- 상태: in_progress
- 최종수정일: 2026-04-08

## Current state

- realm/capability/query/command seam, offline adapter, hidden future seam, 관련 design/architecture/ADR/task 문서는 이미 반영돼 있다.
- active runtime surface는 `GameSessionRoot.ProfileQueries` / `ProfileCommands`로 유지하고, arena/authority future seam은 `SessionRealmCoordinator` / `OfflineLocalSessionAdapter`의 explicit interface 뒤로 숨겼다.
- release-floor docs/tooling은 `prepare-playable` canonical lane, `quick-battle-smoke` smoke lane, `tools/pre-art-rc.ps1` packet으로 정리됐다.
- touched-file `docs-check` gate는 현재 변경 집합에서 green이다.
- stale batch false green은 제거했다. `test-batch-fast`와 batch executeMethod는 fresh artifact가 없으면 project lock failure로 끝난다.
- current local fresh evidence는 여전히 불완전하다. `unity-cli` compile ready 복구와 batch project lock 해소가 먼저 필요하다.

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 / 다음 확인 |
| --- | --- | --- | --- |
| compile | C# compile green | blocker | `Logs/release-floor/20260408-020521-41ebbb3/manifest.json`에서 `unity-bridge compile`이 port `8090` timeout으로 fail |
| validator | test harness lint green | 통과 | `tools/test-harness-lint.ps1` |
| docs policy | 문서 정책 검사 | 통과 | `tools/docs-policy-check.ps1` |
| docs lint | 저장소 문서 lint | 부분통과 | touched-file gate는 green, repo-wide markdownlint debt는 informational 유지 |
| targeted tests | `FastUnit`, `EditMode` | blocker | stale-result guard 추가 후 `test-batch-fast`가 project lock을 명시적 failure로 보고 |
| smoke | 기본 smoke check | 통과 | `tools/smoke-check.ps1` |
| runtime smoke | Boot/Town/Reward contract | 미완료 | compile ready 해소 후 `prepare-playable` / `test-play` / observer report 재확인 필요 |

## Evidence

- commit SHA baseline: `41ebbb3d8b2f65ef288cc485cbea4502aa34daae`
- dirty worktree note:
  - unrelated user changes가 `Assets/_Game/**`, `docs/02_design/ui/**`, `docs/03_architecture/localization-runtime-and-content-pipeline.md` 등에 열려 있다.
- pass:
  - `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
  - `$paths = @(...); & .\tools\docs-check.ps1 -RepoRoot . -Paths $paths`
  - `pwsh -File tools/smoke-check.ps1 -RepoRoot .`
  - `pwsh -File tools/test-harness-lint.ps1 -RepoRoot .`
- code cleanup:
  - `BattleActorWrapper`는 `Cast` / `ProjectileOrigin` sibling fallback 의도를 helper + test로 고정했다.
  - `TownScreenController`에서 미참조 debug-only runtime methods를 제거했다.
  - `Assets/README.md`, `Assets/_Game/README.md`를 current prototype 상태에 맞게 갱신했다.
  - 빈 placeholder `Audio`, `Settings`, `Persistence/Postgres` meta residue를 제거했다.
- RC packet attempt:
  - `pwsh -File tools/pre-art-rc.ps1 -UnityRecoveryBudget 0`
  - artifact: `Logs/release-floor/20260408-020521-41ebbb3/manifest.json`
  - result: compile phase fail (`Waiting for Unity... timed out waiting for Unity (port 8090)`)
- fresh batch blocker confirmation:
  - `pwsh -File tools/unity-bridge.ps1 status`
    - result: `unity-cli connector remained busy after 5 attempts. Unity (port 8090): not responding (last heartbeat 105h54m36s ago)`
  - `pwsh -File tools/unity-bridge.ps1 test-batch-fast`
    - result: `Unity batchmode test exited with code 1 and no fresh results file was produced. Another Unity instance may still hold the project lock.`
  - `pwsh -File tools/unity-bridge.ps1 content-validate`
    - artifact: `Logs/content-validation-ci.log`
    - result: project lock으로 batch executeMethod fail
  - current editor log diagnostic:
    - `Assets/_Game/Scripts/Runtime/Meta/Services/PassiveBoardSelectionValidator.cs:145`
    - `Assets/_Game/Scripts/Runtime/Meta/Services/PermanentAugmentProgressionService.cs:29,50`
    - note: 열린 editor session에서 Meta compile error가 보이므로 clean same-SHA rerun 전까지 release evidence를 닫지 않는다.
- lane rename / docs sync:
  - canonical setup verb: `prepare-playable`
  - smoke verb: `quick-battle-smoke`
  - deprecated alias: `bootstrap` -> `quick-battle-smoke`

## Remaining blockers

- `unity-cli` compile ready가 current SHA에서 port `8090` timeout으로 막혀 있다.
- `unity-cli status`도 stale heartbeat를 보고해 connector 자체가 현재 editor session과 정상 동기화되지 않는다.
- 열린 Unity 인스턴스 때문에 fresh `test-batch-fast` / `test-batch-edit` / `content-validate` evidence가 project lock으로 중단된다.
- `prepare-playable`, `test-play`, observer report, save/load/recovery manual note는 compile ready가 닫히기 전까지 final evidence로 채택할 수 없다.
- repo-wide `docs-check` debt는 여전히 informational이지만, 이번 패스의 blocker는 docs가 아니라 Unity ready / project lock 쪽이다.

## Deferred / debug-only

- `OnlineAuthoritative`
- official arena/PvP settlement/evidence
- `OnlineMockAdapter`
- actual server adapter
- `SaveProfile` concern 분해

## Loop budget consumed

- compile-fix: 1
- refresh/read-console: 0
- asset authoring retry: 1
- stale-batch-guard / release-floor tooling: 1
- budget 초과 시 남긴 diagnosis:
  - unity bridge `compile` timeout (`port 8090`)
  - batch test / executeMethod project lock hard fail

## Handoff notes

- Unity lock이 풀리면 `compile`, `test-batch-fast`, `test-batch-edit`, `prepare-playable`, `test-play`를 같은 SHA에서 다시 돌려 status evidence를 갱신한다.
- RC packet 기본 entry는 `pwsh -File tools/pre-art-rc.ps1`다. compile phase에서 실패하면 뒤 lane은 evidence로 채택하지 않는다.
- repo-wide markdownlint debt는 별도 task로 정리하되, current lane gate는 touched-file `docs-check`로 유지한다.
- Boot scene canonical save/recovery가 필요하면 `prepare-playable`과 `repair-scenes`를 우선 사용한다.
