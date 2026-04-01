# 작업 상태

## 메타데이터

- 작업명: Loop D Telemetry / Pruning / Readability Gate / First Playable Balance Closure
- 담당: Codex
- 상태: 진행 중
- 최종수정일: 2026-04-02

## Current state

- Loop D telemetry model, readability evaluator, first playable slice generator, balance runner의
  코드 뼈대는 들어갔다.
- runtime battle/meta 경로에서 combat/recruit/retrain/duplicate telemetry를 기록한다.
- first playable slice는 runtime asset + `ParkingLotContentIds` 구조로 분리되도록 맞췄다.
- 아직 final handoff는 아니다. smoke/full runner artifact와 test/script evidence를 다시
  회수해야 한다.

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 / 다음 확인 |
| --- | --- | --- | --- |
| compile | Loop D 코드 추가 후 compile green | 진행 중 | Unity 재기동 후 재확인 |
| slice asset | first playable slice asset + markdown 생성 | 부분 | `FirstPlayableSliceGenerator` 재실행 필요 |
| telemetry | battle/meta telemetry event 기록 | 완료 | runtime emit point 추가 |
| readability gate | report 생성 + fail semantics | 부분 | smoke runner artifact 재실행 필요 |
| prune ledger | CSV/JSON 산출 | 부분 | runner artifact 재실행 필요 |
| tests | Loop D EditMode oracle 추가 | 완료 | `LoopDTelemetryAndBalanceTests` 추가 |
| docs/index | task packet + docs/index sync | 진행 중 | 같은 작업 단위에서 마감 필요 |

## Evidence

- 핵심 코드:
  - `Assets/_Game/Scripts/Runtime/Combat/Model/LoopDTelemetryModels.cs`
  - `Assets/_Game/Scripts/Runtime/Combat/Services/BattleTelemetryRecorder.cs`
  - `Assets/_Game/Scripts/Runtime/Combat/Services/BattleTelemetryAnalysisService.cs`
  - `Assets/_Game/Scripts/Runtime/Combat/Services/TelemetryExplainValidator.cs`
  - `Assets/_Game/Scripts/Editor/Validation/FirstPlayableSliceGenerator.cs`
  - `Assets/_Game/Scripts/Editor/Validation/FirstPlayableBalanceRunner.cs`
  - `Assets/_Game/Scripts/Editor/Validation/ValidationBatchEntryPoint.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/RuntimeCombatContentLookup.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/GameSessionState.cs`
- 관련 테스트:
  - `Assets/Tests/EditMode/LoopDTelemetryAndBalanceTests.cs`

## Remaining blockers

- Unity hard-hang 이후 재기동 상태에서 smoke runner artifact를 다시 생성해야 한다.
- `test-edit`, `test-play`, docs/smoke script evidence를 최종 회수해야 한다.
- `loop-d-closure-note.md`에는 실제 generated artifact 수치를 다시 적재해야 한다.
- long-running runner는 `tasks/013_unity_long_running_workload_lane/status.md` 기준으로 shard/manual lane에서 다시 회수한다.

## Deferred / debug-only

- full deterministic suite wall-clock 최적화
- authored site ladder 기반 RunLite 확장
- offscreen major event viewport heuristic 정교화

## Loop budget consumed

- compile-fix: 3
- editor hard-restart: 1
- smoke runner retry: 1

## Handoff notes

- 다음 세션 시작 문서:
  - `tasks/012_loop_d_telemetry_pruning_readability_balance_closure/status.md`
  - `tasks/013_unity_long_running_workload_lane/status.md`
  - `tasks/012_loop_d_telemetry_pruning_readability_balance_closure/spec.md`
  - `docs/03_architecture/telemetry-contract.md`
  - `docs/02_design/systems/first-playable-slice.md`
- default `test-edit`로 Loop D 장시간 smoke evidence를 회수하지 않는다.
- first playable shard evidence는 `pwsh -File tools/unity-bridge.ps1 loopd-slice|loopd-purekit|loopd-systemic|loopd-runlite`를 우선 사용한다.
- `Packages/com.coplaydev.unity-mcp/`와 `Packages/packages-lock.json`은 Loop D 범위 밖 dirty state로
  취급한다.
