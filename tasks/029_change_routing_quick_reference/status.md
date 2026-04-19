# Change routing quick reference status

## 메타데이터

- 작업명: Change routing quick reference
- 담당: Codex
- 상태: completed
- 최종수정일: 2026-04-19

## Historical snapshot / current-state implications

- 이 문서는 029 완료 시점의 historical snapshot이다.
- 현재 source-of-truth는 `AGENTS.md`, `docs/TESTING.md`, `docs/03_architecture/index.md`의 closure scope를 우선한다.
- routing table의 목적은 첫 파일/첫 검증을 고르는 것이다. pure/FastUnit lane과 `SM.Unity`/content/UI adapter lane을 같은 closure 범위로 합치지 않는다.

## Current state

- 시작 기준 `main`은 commit `0dacffb` 이후 028이 push된 상태다.
- `AGENTS.md`와 architecture index는 경계 원칙을 설명하지만, change type별 첫 파일/첫 테스트 표는 없었다.
- `AGENTS.md`와 `docs/03_architecture/index.md`에 change type -> owner -> first validation -> escalation/editor lane 표를 추가했다.

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 |
| --- | --- | --- | --- |
| agent routing | `AGENTS.md`에 compact routing table 추가 | 완료 | diff |
| architecture routing | `docs/03_architecture/index.md`에 구조 routing table 추가 | 완료 | diff |
| docs validation | docs policy/check/smoke pass | 완료 | evidence |

## Evidence

- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`: pass.
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`: pass.
- `npx --yes markdownlint-cli2 "**/*.md" "#Library/**" "#Logs/**" "#.git/**"`: pass, `372 file(s)`.
- targeted docs check: `pwsh -NoProfile -Command "& { .\tools\docs-check.ps1 -RepoRoot . -LinkCheckTimeoutSeconds 10 -Paths @('AGENTS.md','docs/03_architecture/index.md','tasks/029_change_routing_quick_reference') }"` pass.

## Remaining blockers

- 없음.

## Deferred

- 030에서 routing policy를 semantic guard로 더 고정한다.
- 027 이후 추가 session ownership migration은 별도 task로 유지한다.

## Loop budget consumed

- docs wording retry: 0/1
- docs-check retry: 0/1

## Handoff notes

- 이번 task는 docs-only다.
- production code, tests, asmdef, scene/prefab/Resources asset은 수정하지 않는다.
