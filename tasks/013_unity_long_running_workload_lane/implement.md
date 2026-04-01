# 구현 메모

## 메타데이터

- 작업명: Unity long-running workload lane hardening
- 담당: Codex
- 상태: 진행 중
- 최종수정일: 2026-04-02

## 구현 요약

- `tools/unity-bridge.ps1`에 `-TestFilter`와 `loopd-slice`, `loopd-purekit`, `loopd-systemic`,
  `loopd-runlite`, `loopd-smoke`, `loopd-full` verb를 추가했다.
- `LoopDTelemetryAndBalanceTests`의 장시간 smoke를 `[Explicit]` manual lane으로 분리했다.
- `loop_d_balance_report` custom CLI tool과 `FirstPlayableBalanceRunner` shard write path를 추가했다.
- setup/architecture 문서를 갱신해 장시간 workload를 기본 `test-edit`/menu callback에서 빼는 계약을 명시했다.

## 왜 이 loop가 필요했는가

- 최근 반복된 Unity hold는 CLI/MCP transport 자체보다, 장시간 deterministic runner를
  Unity Test Runner callback과 menu callback에 실은 usage 문제였다.
- 이 상태에서는 `status` heartbeat timeout, `busy`, `connection closed before response`가 실제 툴 고장처럼 보이기 쉽다.
- 따라서 long-running workload를 shard/manual lane으로 분리하지 않으면 같은 오판이 반복된다.

## deviation / 메모

- 이번 task는 Loop D balance 결과를 다시 튜닝하는 작업이 아니다.
- 목표는 long-running runner를 `어디서 돌려야 하는가`를 분리하는 것이다.
