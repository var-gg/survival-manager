# 작업 계획

## 메타데이터

- 작업명: Launch Floor Catalog Closure
- 담당: Codex
- 상태: 완료 기록용
- 최종수정일: 2026-03-31

## Preflight

- `AGENTS.md -> docs/index.md -> docs/02_design/index.md -> docs/03_architecture/index.md -> tasks/001/status.md`
  순서로 시작 컨텍스트를 고정한다.
- 기존 compile/test 결과와 현재 worktree를 읽고, unrelated dirty scene은 건드리지 않는다.
- 문서 하네스 작업이 포함되므로 `$docs-maintainer` 절차를 따른다.

## Phase 1 code-only

- definition/model/service/persistence/test를 먼저 닫는다.
- encounter/status/drop/arena scaffold는 file-first로 구현한다.
- normal runtime path에서 authored encounter resolution을 우선 사용하게 바꾼다.

## Phase 2 asset authoring

- committed content root를 추가한다.
- sample generator와 validator를 확장해 launch floor catalog를 채운다.
- stable tag/support/status/reward asset을 함께 authoring한다.

## Phase 3 validation

- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
- `pwsh -File tools/docs-check.ps1 -RepoRoot .`
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`
- `pwsh -File tools/unity-bridge.ps1 compile`
- EditMode assembly test
- `pwsh -File tools/unity-bridge.ps1 smoke-observer`

## rollback / escape hatch

- typed content load가 불안정하면 `RuntimeCombatContentLookup`의 file parser fallback을 유지한다.
- quick battle smoke는 authored path failure 시 debug-only fallback으로만 남긴다.
- smoke-observer가 red면 task status에 blocker로 남기고 억지로 green 판정을 하지 않는다.

## tool usage plan

- 문서/인덱스/task 상태는 `apply_patch`로 함께 갱신한다.
- Unity 검증은 `unity-bridge`를 우선하고, 메뉴 실행/테스트 보조에만 MCP를 사용한다.
- runtime evidence는 compile, EditMode tests, smoke-observer 결과를 나눠 기록한다.

## loop budget

- compile loop: 여러 차례 사용
- bootstrap / smoke retry: 여러 차례 사용
- docs validation: 1회 full run
- oversized umbrella 리스크가 있었으므로 결과는 pass/fail 둘 다 status에 남긴다
