# Pindoc design SOT cleanup status

- 상태: active
- 소유자: repository
- 최종수정일: 2026-05-16
- 소스오브트루스: `tasks/045_pindoc_design_sot_cleanup/status.md`
- 관련문서:
  - `tasks/045_pindoc_design_sot_cleanup/spec.md`
  - `tasks/045_pindoc_design_sot_cleanup/plan.md`
  - `tasks/045_pindoc_design_sot_cleanup/implement.md`
  - `pindoc://decision-doc-harness-pindoc-migration`

## Current state

완료. Pindoc 전환 결정과 repo 인벤토리를 기준으로 product/design creative longform을 active repo corpus에서 제거하고, repo 시작 문서를 Pindoc-first routing으로 갱신했다.

## Acceptance matrix

| 항목 | 상태 | 근거 |
| --- | --- | --- |
| Pindoc 전환 기준 확인 | 완료 | `decision-doc-harness-pindoc-migration`, `analysis-harness-inventory-classification` 확인 |
| task handoff 문서 작성 | 완료 | `spec.md`, `plan.md`, `implement.md`, `status.md` 작성 |
| active index 정리 | 완료 | `docs/index.md`, `docs/02_design/index.md`, `docs/02_design/narrative/index.md` 갱신 |
| 방해 product/design Markdown 제거 또는 hold 처리 | 완료 | product/deck/progression/UI handoff/creative narrative Markdown 제거, dirty narrative seed는 transition hold |
| Pindoc 시작 지도 정합 | 완료 | `wiki-start-프로젝트지도` revision 5 |
| 검증 실행 | 완료 | docs policy, targeted docs-check, smoke 통과 |

## Evidence

- Pindoc project: `survival-manager`
- 기준 결정: `pindoc://decision-doc-harness-pindoc-migration`
- Pindoc update: `wiki-start-프로젝트지도` revision 5 accepted
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .` -> passed
- `.\tools\docs-check.ps1 -RepoRoot . -LinkCheckTimeoutSeconds 60 <changed-md>` -> passed
- `.\tools\docs-check.ps1 -RepoRoot . -LinkCheckTimeoutSeconds 60 tasks/045_pindoc_design_sot_cleanup` -> passed
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .` -> passed

## Remaining blockers

없음. 단, 기존 worktree에는 사용자 Unity/content 변경이 많으므로 이 작업은 문서 변경만 다뤘고, staging/commit/push는 수행하지 않았다.

## Deferred / debug-only

- code-facing design contract 문서의 `docs/03_architecture/**` 이동.
- narrative seed 본문과 Pindoc 최신 script의 내용 동기화.

## Loop budget consumed

- 문서 검증 수정 루프: 1/2
- Pindoc update retry: 1/2

## Handoff notes

후속으로는 남은 `docs/02_design/combat/**`, `meta/**`, `systems/**`, 일부 `ui/**` code-facing contract를 `docs/03_architecture/**`와 Pindoc artifact로 세분 이동할지 결정하면 된다.
