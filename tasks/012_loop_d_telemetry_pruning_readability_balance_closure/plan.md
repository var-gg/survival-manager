# 작업 계획

## 메타데이터

- 작업명: Loop D Telemetry / Pruning / Readability Gate / First Playable Balance Closure
- 담당: Codex
- 상태: 진행 중
- 최종수정일: 2026-04-02
- 의존:
  - `tasks/011_loop_c_content_governance_closure/status.md`

## Preflight

- Loop C runtime summary와 pool filtering 경계를 다시 확인한다.
- telemetry model과 readability gate는 runtime/source-of-truth를 먼저 고정한다.
- first playable slice cap은 authored count가 아니라 실제 runtime selection asset으로 닫는다.
- docs start context는 `AGENTS.md -> docs/index.md -> 관련 index -> task status`만 쓴다.

## Phase 1 code-only

- `TelemetryEventRecord`, `ExplainStamp`, readability/pruning/slice contract 타입을 추가한다.
- battle/meta runtime에 telemetry emitter를 심고 replay bundle에 summary/readability를 싣는다.
- first playable slice asset과 runtime filtering, Loop D runner/entrypoint를 추가한다.

## Phase 2 asset authoring

- first playable slice asset을 생성하고 cap/quota를 실제 catalog 기준으로 채운다.
- slice 밖 content는 `ParkingLotContentIds`와 prune ledger로 분리한다.
- dev sandbox와 report artifact가 Loop D summary를 읽도록 맞춘다.

## Phase 3 validation

- `pwsh -File tools/unity-bridge.ps1 compile`
- Loop D smoke runner와 artifact 확인
- `pwsh -File tools/unity-bridge.ps1 test-edit`
- `pwsh -File tools/unity-bridge.ps1 test-play`
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
- `pwsh -File tools/docs-check.ps1 -RepoRoot .`
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`

## rollback / escape hatch

- full suite가 에디터를 과도하게 점유하면 smoke path를 먼저 green으로 맞추고 full suite는
  별도 evidence로 남긴다.
- slice cap/quota가 authored catalog와 충돌하면 content를 추가하지 않고 `MoveOutOfV1`
  처리로 닫는다.
- readability fatal은 수치 retune보다 simplify/readability debt 감소를 우선한다.

## tool usage plan

- file-first로 task packet과 docs/index를 먼저 맞춘다.
- compile/test/smoke는 `tools/unity-bridge.ps1`를 우선 사용한다.
- menu dispatch와 console 회수는 typed guardrail이 필요할 때만 MCP를 쓴다.

## loop budget

- compile-fix 허용 횟수: 4
- editor hard-hang 강제 재시작 허용 횟수: 1
- smoke runner 재시도 허용 횟수: 2
