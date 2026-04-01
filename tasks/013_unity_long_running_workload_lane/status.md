# 작업 상태

## 메타데이터

- 작업명: Unity long-running workload lane hardening
- 담당: Codex
- 상태: 진행 중
- 최종수정일: 2026-04-02

## Current state

- Unity hold의 주 원인은 CLI/MCP 자체보다 long-running workload를 `EditMode test`와 `MenuItem`
  callback에 태운 usage 문제로 진단했다.
- wrapper, Loop D runner, setup/harness 문서를 장시간 workload 분리 기준에 맞춰 갱신 중이다.
- docs policy/smoke 검사는 통과했고, docs-check는 범위 밖 embedded package README lint 1건 때문에 막힌 상태다.

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 / 다음 확인 |
| --- | --- | --- | --- |
| compile | wrapper/tool/test 변경 후 compile green | 진행 중 | Unity가 기존 Test Runner callback에서 아직 busy라 재확인 대기 |
| targeted tests | default `test-edit`에서 장시간 smoke 제외 | 완료 | `[Explicit]` manual lane 적용 |
| runtime smoke | `loopd-*` shard lane 호출 가능 | 부분 | compile 후 실제 호출 재확인 |
| docs | setup/architecture/index/task 동기화 | 부분 | docs policy/smoke pass, docs-check는 외부 package README lint로 보류 |

## Evidence

- 코드:
  - `tools/unity-bridge.ps1`
  - `Assets/_Game/Scripts/Editor/UnityCliTools/LoopDBalanceReport.cs`
  - `Assets/_Game/Scripts/Editor/Validation/FirstPlayableBalanceRunner.cs`
  - `Assets/Tests/EditMode/LoopDTelemetryAndBalanceTests.cs`
- 문서:
  - `docs/05_setup/unity-long-running-workloads.md`
  - `docs/05_setup/unity-cli.md`
  - `docs/03_architecture/unity-agent-harness-contract.md`
  - `docs/03_architecture/validation-and-acceptance-oracles.md`
  - `docs/03_architecture/combat-harness-and-debug-contract.md`
- 실행:
  - `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .` -> pass
  - `pwsh -File tools/smoke-check.ps1 -RepoRoot .` -> pass
  - `pwsh -File tools/docs-check.ps1 -RepoRoot .` -> `Packages/com.coplaydev.unity-mcp/README.md` lint 1건으로 fail
  - `pwsh -File tools/unity-bridge.ps1 status` -> 기존 Test Runner callback busy 상태로 ready 회수 실패

## Remaining blockers

- compile와 targeted filtered test evidence를 다시 회수해야 한다.
- docs-check가 범위 밖 `Packages/com.coplaydev.unity-mcp/README.md` lint 1건에 막힌다.

## Deferred / debug-only

- full Loop D suite 자체의 wall-clock 단축
- editor-safe incremental background runner

## Loop budget consumed

- compile-fix: 0
- refresh/read-console: 0
- asset authoring retry: 0
- budget 초과 시 남긴 diagnosis:
  - 없음

## Handoff notes

- 다음 세션 시작 문서:
  - `tasks/013_unity_long_running_workload_lane/status.md`
  - `docs/05_setup/unity-long-running-workloads.md`
  - `docs/05_setup/unity-cli.md`
- 금지해야 할 반복 루프:
  - long-running Loop D smoke를 default `test-edit`에 다시 넣는 것
  - `compile -> bootstrap -> report -> test-edit` 직렬 파이프라인 습관화
- 필요한 preflight:
  - `pwsh -File tools/unity-bridge.ps1 compile`
  - 필요 시 `pwsh -File tools/unity-bridge.ps1 test-edit -TestFilter <Namespace.Class>`
