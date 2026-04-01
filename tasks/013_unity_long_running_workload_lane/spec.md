# 작업 명세

## 메타데이터

- 작업명: Unity long-running workload lane hardening
- 담당: Codex
- 상태: 진행 중
- 최종수정일: 2026-04-02
- 관련경로:
  - `tools/unity-bridge.ps1`
  - `Assets/_Game/Scripts/Editor/UnityCliTools/`
  - `Assets/_Game/Scripts/Editor/Validation/FirstPlayableBalanceRunner.cs`
  - `Assets/Tests/EditMode/LoopDTelemetryAndBalanceTests.cs`
  - `docs/05_setup/unity-cli.md`
  - `docs/05_setup/unity-long-running-workloads.md`
- 관련문서:
  - `docs/05_setup/unity-cli.md`
  - `docs/05_setup/unity-mcp.md`
  - `docs/03_architecture/unity-agent-harness-contract.md`
  - `docs/03_architecture/validation-and-acceptance-oracles.md`
  - `tasks/012_loop_d_telemetry_pruning_readability_balance_closure/status.md`

## Goal

- Unity long-running workload를 기본 `test-edit`/menu callback 경로에서 분리해 CLI/MCP busy 오판을 줄이는 운영 lane을 닫는다.

## Authoritative boundary

- 이번 task는 `Unity tooling usage contract + wrapper verbs + long-running Loop D lane 분리`만 닫는다.
- source-of-truth는 `tools/unity-bridge.ps1`와 setup/harness 문서다.
- Loop D gameplay 수치, telemetry schema, content slice 자체를 재설계하지 않는다.

## In scope

- `unity-bridge`에 targeted test filter와 Loop D shard verb 추가
- Loop D 장시간 smoke를 default EditMode suite에서 분리
- custom CLI tool 기반 long-running artifact lane 추가
- setup/architecture/task 문서와 index 동기화

## Out of scope

- Unity 전체 성능 최적화
- MCP package 자체의 근본적인 editor update-check 문제 재수정
- Loop D balance 수치 재튜닝
- CI workflow 파일 대규모 재설계

## asmdef impact

- 영향 asmdef: `SM.Editor`, `SM.Tests`
- 새 tool은 기존 `UnityCliConnector.Editor` 참조 안에 배치한다.
- 새 asmdef 추가는 하지 않는다.

## persistence impact

- persistence 영향 없음
- `SM.Meta`, `SM.Unity`, `SM.Persistence.Abstractions` 책임 변화 없음

## validator / test oracle

- validator: 없음. wrapper/custom tool/manual lane contract가 주 대상
- targeted EditMode test: `LoopDTelemetryAndBalanceTests`의 lightweight oracle
- runtime path smoke: `pwsh -File tools/unity-bridge.ps1 loopd-slice|loopd-purekit`

## done definition

- default `test-edit`에서 long-running Loop D smoke가 빠진다.
- wrapper가 `-TestFilter`와 `loopd-*` shard verb를 지원한다.
- setup/architecture 문서가 long-running workload routing을 명시한다.
- task/status에 원인과 새 기본 경로가 handoff-ready로 남는다.

## deferred

- full suite wall-clock 최적화
- background thread 기반 editor-safe incremental runner
- CI에서 manual artifact lane을 자동 스케줄링하는 작업
