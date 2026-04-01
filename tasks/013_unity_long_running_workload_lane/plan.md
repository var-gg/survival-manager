# 작업 계획

## 메타데이터

- 작업명: Unity long-running workload lane hardening
- 담당: Codex
- 상태: 진행 중
- 최종수정일: 2026-04-02
- 의존:
  - `tasks/012_loop_d_telemetry_pruning_readability_balance_closure/status.md`
  - `docs/05_setup/unity-cli.md`

## Preflight

- Unity hold 원인이 transport 자체인지 callback workload routing인지 먼저 구분한다.
- `tools/unity-bridge.ps1`, Loop D smoke test, balance runner, 관련 setup 문서를 읽는다.
- task/status에 기본 경계와 evidence 경로를 먼저 적는다.

## Phase 1 code-only

- `unity-bridge`에 `-TestFilter`와 `loopd-*` shard verb를 추가한다.
- Loop D runner를 custom CLI tool에서 바로 호출할 수 있게 분리한다.
- long-running EditMode smoke test를 default suite에서 제외한다.

## Phase 2 asset authoring

- 없음
- 새 Unity asset 생성 대신 repo-owned tool/doc/task만 추가한다.

## Phase 3 validation

- `pwsh -File tools/unity-bridge.ps1 compile`
- 가능하면 `pwsh -File tools/unity-bridge.ps1 test-edit -TestFilter SM.Tests.EditMode.LoopDTelemetryAndBalanceTests`
- docs policy / docs check / smoke check

## rollback / escape hatch

- wrapper verb 추가가 compile blocker를 만들면 우선 `-TestFilter`만 남기고 Loop D shard verb는 다음 task로 넘긴다.
- custom CLI tool이 connector surface와 충돌하면 menu/callback lane 문서화만 남기고 tool 추가는 철회한다.

## tool usage plan

- file-first로 C#/docs/task 수정
- Unity compile/test는 `tools/unity-bridge.ps1` 우선
- MCP는 이번 task의 기본 경로가 아니다
- long-running validation은 raw `exec` 대신 repo-owned custom tool로 승격한다

## loop budget

- compile-fix 허용 횟수: 2
- refresh/read-console 반복 허용 횟수: 1
- blind asset generation 재시도: 0
