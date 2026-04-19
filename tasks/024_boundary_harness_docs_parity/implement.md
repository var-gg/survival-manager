# boundary harness docs parity 구현 기록

## 메타데이터

- 작업명: boundary harness docs parity
- 담당: Codex
- 상태: completed
- 최종수정일: 2026-04-19

## Phase summary

- Phase 0: Pro 감사 항목을 현재 repo 상태와 대조했다.
- Phase 1: `BuildCompileAuditTests`의 lightweight boundary guard를 `BuildBoundaryGuardFastTests` FastUnit 클래스로 분리했다.
- Phase 2: `test-harness-lint.ps1`의 runtime editor API/direct resource lookup 감지를 확장했다.
- Phase 3: stale test count, test asmdef map, session extraction 상태 문서를 현재 코드 기준으로 정리했다.

## Diagnostics

- `BuildCompileAuditTests`는 asset-dependent audit와 lightweight boundary guard가 같은 `BatchOnly` 클래스에 섞여 있었다.
- `SceneIntegrityTests`는 `Resources.Load`를 직접 사용하지만 class-level category가 없었다.

## Deviation

- `SceneIntegrityTests`가 `Resources.Load`를 직접 사용하므로, 확장된 lint 정책에 맞춰 `BatchOnly` category를 명시했다.

## Blockers

- 아직 없음.

## Verification

- `pwsh -File tools/test-harness-lint.ps1 -RepoRoot .`: pass.
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.BuildBoundaryGuardFastTests`: 3 total / 3 passed / 0 failed.
- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`: 138 total / 135 passed / 3 skipped / 0 failed.
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.BuildCompileAuditTests`: 7 total / 7 passed / 0 failed.
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`: pass.
- `pwsh -File tools/docs-check.ps1 -RepoRoot .`: pass, markdownlint 352 files / 0 errors.
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`: pass.

## why this loop happened

- pure boundary guard가 존재했지만 기본 fast lane 밖에 있어 agent 기본 검증 루프에서 늦게 발견되는 구조였다.
