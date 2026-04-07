# 작업 상태

## 메타데이터

- 작업명: Loop D Telemetry / Pruning / Readability Gate / First Playable Balance Closure
- 담당: Codex
- 상태: 진행 중
- 최종수정일: 2026-04-08

## Current state

- Loop D telemetry model, readability evaluator, first playable slice generator, balance runner의
  코드 뼈대는 들어갔다.
- runtime battle/meta 경로에서 combat/recruit/retrain/duplicate telemetry를 기록한다.
- session/save/recovery/reward/economy를 위한 lightweight operational telemetry join scaffold가 추가됐다.
- first playable slice는 runtime asset + `ParkingLotContentIds` 구조로 분리되도록 맞췄다.
- current authored floor closure 패스에서 아래를 추가 반영했다.
  - `first_playable_slice.asset`에 passive board 4개, signature passive cap `8`,
    flex active `12`, flex passive `20`, live synergy grammar를 잠금
  - encounter 24개에 `encounter_family_*`와 `answer_lane_*` machine-readable tag를 부여
  - site 6개의 4-beat pressure sequence와 primary answer lane을 자산/문서로 고정
  - boss overlay 6개에 `overlay_ask_*`를 부여하고 site capstone ask를 분리
  - skirmish / elite / boss drop table에 `RequiredContextTags = SiteId + answer_lane`
    routed reward entry를 추가
  - `FirstPlayableAuthoring*` validator와 build/reward/encounter coverage 규칙을 추가
- 아직 final handoff는 아니다. smoke/full runner artifact와 test/script evidence를 다시
  회수해야 한다.

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 / 다음 확인 |
| --- | --- | --- | --- |
| compile | Loop D 코드 추가 후 compile green | 진행 중 | Unity stale editor/connector 복구 후 재확인 |
| slice asset | first playable slice asset + markdown 생성 | 부분 | asset는 갱신, generated markdown은 재실행 필요 |
| telemetry | battle/meta + operational telemetry event 기록 | 완료 | runtime emit point와 session/recovery/reward/economy join scaffold 추가 |
| readability gate | report 생성 + fail semantics | 부분 | smoke runner artifact 재실행 필요 |
| prune ledger | CSV/JSON 산출 | 부분 | runner artifact 재실행 필요 |
| tests | Loop D / content validator test 갱신 | 부분 | batch verification이 project lock으로 final green 미확정 |
| docs/index | task packet + docs/index sync | 부분 | design docs는 갱신, docs script evidence 재실행 필요 |

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
  - `Assets/Tests/EditMode/LoopCContentGovernanceTests.cs`
  - `Assets/Tests/EditMode/ValidationAssetSelectionPolicyTests.cs`
- 이번 패스 주요 자산/문서:
  - `Assets/Resources/_Game/Content/Definitions/FirstPlayable/first_playable_slice.asset`
  - `Assets/Resources/_Game/Content/Definitions/Encounters/*.asset`
  - `Assets/Resources/_Game/Content/Definitions/EnemySquads/*.asset`
  - `Assets/Resources/_Game/Content/Definitions/BossOverlays/*.asset`
  - `Assets/Resources/_Game/Content/Definitions/DropTables/drop_table_skirmish.asset`
  - `Assets/Resources/_Game/Content/Definitions/DropTables/drop_table_elite.asset`
  - `Assets/Resources/_Game/Content/Definitions/DropTables/drop_table_boss.asset`
  - `docs/02_design/systems/launch-encounter-variety-and-answer-lane-matrix.md`

## Remaining blockers

- stale Unity editor/connector 때문에 `unity-bridge status`가 ready로 올라오지 않았다.
  `test-batch-fast`는 project lock 메시지와 함께 종료돼 최종 증거로 채택하지 않았다.
- Unity 재기동 후 smoke runner artifact를 다시 생성해야 한다.
- runtime hardening packet이 들어가면서 fresh operational artifact와 quick battle / normal playable evidence를 함께 다시 회수해야 한다.
- `test-edit`, `test-play`, docs/smoke script evidence를 최종 회수해야 한다.
- `loop-d-closure-note.md`에는 실제 generated artifact 수치를 다시 적재해야 한다.
- long-running runner는 `tasks/013_unity_long_running_workload_lane/status.md` 기준으로 shard/manual lane에서 다시 회수한다.

## Deferred / debug-only

- full deterministic suite wall-clock 최적화
- authored site ladder 기반 RunLite 확장
- offscreen major event viewport heuristic 정교화

## Loop budget consumed

- compile-fix: 4
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
