# Scope-correct closure docs status

## 메타데이터

- 작업명: Scope-correct closure docs
- 담당: Codex
- 상태: completed
- 최종수정일: 2026-04-19

## Current state

- active docs는 이미 FastUnit lane과 BatchOnly/editor-required lane을 분리하고 있다.
- 다만 025~030 historical status와 closure wording은 “repo-wide full separation”으로 과대해석될 위험이 있다.
- 031은 docs-only scope correction이며 runtime/asmdef 변경은 없다.

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 |
| --- | --- | --- | --- |
| active docs scope | pure/FastUnit closure와 `SM.Unity` adapter lane 분리 명시 | 완료 | docs diff |
| historical marker | `025`~`030` status에 snapshot/current-state marker 추가 | 완료 | task diff |
| runtime scope | runtime code/asmdef 변경 없음 | 완료 | diff |
| validation | docs policy/check/smoke pass | 완료 | evidence |

## Evidence

- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`: pass.
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`: pass.
- `npx --yes markdownlint-cli2 "**/*.md" "#Library/**" "#Logs/**" "#.git/**"`: pass, `384 file(s)`.
- targeted docs check for changed docs/tasks: pass.
- `pwsh -File tools/docs-check.ps1 -RepoRoot . -LinkCheckTimeoutSeconds 10`: pass.
- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`: pass, `171 total / 168 passed / 0 failed / 3 skipped`.
- `pwsh -File tools/test-harness-lint.ps1 -RepoRoot .`: pass.

## Remaining blockers

- 없음.

## Deferred / debug-only

- production bootstrap/provider injection, content adapter ownership split, UI/controller routing quarantine은 후속 runtime/editor-light 작업이다.

## Loop budget consumed

- wording correction retry: 0/2
- validation retry: 0/2
- docs-check retry: 0/1

## Handoff notes

- `031`은 closure claim의 scope를 고치는 문서 작업이다. strict repo-wide full separation을 선언하지 않는다.
