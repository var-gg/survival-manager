# 작업 상태

## 메타데이터

- 작업명: Loop D Telemetry / Pruning / Readability Gate / First Playable Balance Closure
- 담당: Codex
- 상태: 진행 중
- 최종수정일: 2026-04-08

## Current state

- Loop D telemetry model, readability evaluator, slice generator, balance runner 코드/자산 뼈대는 들어가 있다.
- first playable authored floor closure와 validator 보강도 이미 반영돼 있다.
- release-floor tooling/docs는 `prepare-playable` canonical lane, `quick-battle-smoke` smoke lane, `tools/pre-art-rc.ps1` packet으로 정리됐다.
- `test-batch-fast` stale-result false green은 제거했다. project lock이면 fresh `TestResults-Batch.xml` 부재로 즉시 fail한다.
- 아직 final handoff는 아니다. current `HEAD` 기준 compile ready, Loop D shard artifact, observer packet을 다시 회수해야 한다.

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 / 다음 확인 |
| --- | --- | --- | --- |
| compile | Loop D 코드 추가 후 compile green | blocker | `Logs/release-floor/20260408-020521-41ebbb3/manifest.json`에서 `unity-bridge compile`이 port `8090` timeout으로 중단 |
| slice asset | first playable slice asset + markdown 생성 | 부분 | 기존 자산은 유지, same-SHA `loopd-slice` fresh rerun 필요 |
| telemetry | battle/meta telemetry event 기록 | 완료 | runtime emit point 추가 완료 |
| readability gate | report 생성 + fail semantics | blocker | same-SHA `loopd-purekit/systemic/runlite` rerun 필요 |
| prune ledger | CSV/JSON 산출 | blocker | same-SHA shard artifact 재생성 필요 |
| tests | Loop D / content validator test 갱신 | 부분 | stale-result guard 추가 후 batch lane이 project lock을 명시적 failure로 보고 |
| docs/index | task packet + docs/index sync | 완료 | release floor doc, touched-file docs gate, lane naming 반영 완료 |

## Evidence

- commit SHA baseline: `41ebbb3d8b2f65ef288cc485cbea4502aa34daae`
- dirty worktree note:
  - unrelated user changes가 `Assets/_Game/**`, `docs/02_design/ui/**`, `docs/03_architecture/localization-runtime-and-content-pipeline.md` 등에 열려 있다.
- tooling / docs refresh:
  - `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .` -> pass
  - `pwsh -File tools/smoke-check.ps1 -RepoRoot .` -> pass
  - `pwsh -File tools/test-harness-lint.ps1 -RepoRoot .` -> pass
  - `$paths = @(...); & .\tools\docs-check.ps1 -RepoRoot . -Paths $paths` -> pass
  - `pwsh -File tools/pre-art-rc.ps1 -UnityRecoveryBudget 0`
    - artifact: `Logs/release-floor/20260408-020521-41ebbb3/manifest.json`
    - result: compile phase fail, 이후 Loop D shard 미실행
- stale-result guard 확인:
  - `pwsh -File tools/unity-bridge.ps1 test-batch-fast`
    - result: `Unity batchmode test exited with code 1 and no fresh results file was produced. Another Unity instance may still hold the project lock.`
- blocked validator lane:
  - `pwsh -File tools/unity-bridge.ps1 content-validate`
    - artifact: `Logs/content-validation-ci.log`
    - result: project lock으로 batch executeMethod fail
- 핵심 코드:
  - `Assets/_Game/Scripts/Runtime/Combat/Model/LoopDTelemetryModels.cs`
  - `Assets/_Game/Scripts/Runtime/Combat/Services/BattleTelemetryRecorder.cs`
  - `Assets/_Game/Scripts/Runtime/Combat/Services/BattleTelemetryAnalysisService.cs`
  - `Assets/_Game/Scripts/Runtime/Combat/Services/TelemetryExplainValidator.cs`
  - `Assets/_Game/Scripts/Editor/Validation/FirstPlayableSliceGenerator.cs`
  - `Assets/_Game/Scripts/Editor/Validation/FirstPlayableBalanceRunner.cs`
  - `Assets/_Game/Scripts/Editor/Validation/ValidationBatchEntryPoint.cs`
- 기존 Loop D 산출물 기준 경로:
  - `Logs/loop-d-balance/first_playable_slice.md`
  - `Logs/loop-d-balance/purekit_report.json`
  - `Logs/loop-d-balance/systemic_slice_report.json`
  - `Logs/loop-d-balance/runlite_report.json`
  - `Logs/loop-d-balance/prune_ledger_v1.json`
  - `Logs/loop-d-balance/readability_watchlist.json`
  - `Logs/loop-d-balance/loop_d_closure_note.txt`

## Remaining blockers

- `unity-cli` compile이 current SHA에서 `Waiting for Unity... timed out waiting for Unity (port 8090)`로 멈춘다.
- 열린 Unity 인스턴스 때문에 batch lane은 project lock으로 abort된다. stale XML/로그 reuse는 이제 fail로 막았지만, fresh final evidence는 아직 없다.
- same-SHA `loopd-slice`, `loopd-purekit`, `loopd-systemic`, `loopd-runlite` artifact를 다시 생성해야 한다.
- `loop_d_closure_note.txt`와 task evidence는 fresh shard 수치가 다시 들어와야 final close가 가능하다.
- clean tree/commit 상태에서 rerun하지 않으면 현재 dirty-worktree evidence는 final sign-off로 쓸 수 없다.

## Deferred / debug-only

- full deterministic suite wall-clock 최적화
- authored site ladder 기반 RunLite 확장
- offscreen major event viewport heuristic 정교화

## Loop budget consumed

- compile-fix: 4
- editor hard-restart: 1
- smoke runner retry: 1
- stale-batch-guard / RC packet tooling: 1

## Handoff notes

- 다음 세션 시작 문서:
  - `tasks/012_loop_d_telemetry_pruning_readability_balance_closure/status.md`
  - `tasks/013_unity_long_running_workload_lane/status.md`
  - `tasks/012_loop_d_telemetry_pruning_readability_balance_closure/spec.md`
  - `docs/03_architecture/combat-harness-and-debug-contract.md`
  - `docs/06_production/pre-art-release-floor.md`
- default `test-edit`로 Loop D 장시간 smoke evidence를 회수하지 않는다.
- first playable shard evidence는 `pwsh -File tools/unity-bridge.ps1 loopd-slice|loopd-purekit|loopd-systemic|loopd-runlite`를 우선 사용한다.
- `pwsh -File tools/pre-art-rc.ps1`는 packet entry로 추가됐지만, compile ready가 막혀 있으면 phase 4에서 바로 실패한다.
- `Packages/com.coplaydev.unity-mcp/`, `Packages/packages-lock.json`, 현재 열린 `Assets/_Game/**` 사용자 변경은 Loop D 범위 밖 dirty state로 취급한다.
