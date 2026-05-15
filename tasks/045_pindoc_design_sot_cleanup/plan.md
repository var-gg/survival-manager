# Pindoc design SOT cleanup plan

- 상태: active
- 소유자: repository
- 최종수정일: 2026-05-16
- 소스오브트루스: `tasks/045_pindoc_design_sot_cleanup/plan.md`
- 관련문서:
  - `tasks/045_pindoc_design_sot_cleanup/spec.md`
  - `tasks/045_pindoc_design_sot_cleanup/status.md`

## Preflight

1. `AGENTS.md`, `PINDOC.md`, `docs/index.md`, `docs/00_governance/source-of-truth-matrix.md`를 확인한다.
2. Pindoc `decision-doc-harness-pindoc-migration`과 `analysis-harness-inventory-classification`을 확인한다.
3. `git status --short`로 사용자 변경을 확인하고, dirty Unity/content 변경은 건드리지 않는다.

## Phase 1 code-only

코드 변경 없음. 문서만 수정한다.

## Phase 2 asset authoring

에셋 변경 없음.

## Phase 3 validation

1. `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
2. `pwsh -File tools/docs-check.ps1 -RepoRoot . -Paths <changed-md>`
3. `pwsh -File tools/smoke-check.ps1 -RepoRoot .`

## rollback / escape hatch

삭제한 문서는 git history에서 복구 가능하다. dirty 상태였던 `docs/02_design/narrative/index.md`와 `master-script.md`는 삭제하지 않고 transitional hold로 낮춘다.

## tool usage plan

- 파일 편집은 `apply_patch`를 사용한다.
- 대량 삭제는 추적된 clean Markdown 파일 목록만 대상으로 한다.
- Pindoc 시작 지도는 `pindoc.artifact.read` 후 `pindoc.artifact.propose(update_of=...)`로 갱신한다.

## loop budget

- 문서 검증 수정 루프: 최대 2회.
- Pindoc update retry: 최대 2회.
