# Pindoc design SOT cleanup spec

- 상태: active
- 소유자: repository
- 최종수정일: 2026-05-16
- 소스오브트루스: `tasks/045_pindoc_design_sot_cleanup/spec.md`
- 관련문서:
  - `AGENTS.md`
  - `PINDOC.md`
  - `docs/index.md`
  - `docs/00_governance/source-of-truth-matrix.md`
  - `pindoc://decision-doc-harness-pindoc-migration`
  - `pindoc://analysis-harness-inventory-classification`

## Goal

repo 안의 제품/게임기획/창작 longform Markdown이 active source-of-truth처럼 읽히는 문제를 줄인다. Pindoc Wiki를 product/design/narrative planning의 기본 SoT로 두고, git repo에는 코드 경계, 하네스, setup, validation, runtime/content contract만 남긴다.

## Authoritative boundary

- Pindoc: product vision, design pillars, MVP 범위, narrative/world/campaign/character lore, visual mockup, creative planning의 primary SoT.
- Git repo: `AGENTS.md`, 기술/하네스 문서, setup/runbook, 코드 직결 architecture/contract, current task handoff.
- 전환 중인 repo design 파일은 runtime seed/reference 또는 code-facing contract로만 취급한다.

## In scope

- `docs/index.md`, governance 문서, SoT matrix에서 product/design SoT 경계를 갱신한다.
- active 시작 컨텍스트가 `docs/01_product/**`와 creative `docs/02_design/**` 문서를 기본으로 잡지 않게 정리한다.
- 이미 Pindoc 전환 결정이 있는 문서군은 active repo corpus에서 제거하거나 transitional hold로 낮춘다.
- 변경 결과와 검증을 `status.md`에 남긴다.

## Out of scope

- Pindoc artifact 전체 이관 재작성.
- runtime schema, content validator, Unity asset, C# 코드 변경.
- `docs/03_architecture/**`의 모든 관련문서 링크를 한 번에 전면 재작성.
- 사용자가 작업 중인 dirty Unity/content 변경을 staging 또는 revert.

## asmdef impact

없음. 문서 정리만 수행한다.

## persistence impact

없음.

## validator / test oracle

- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
- `pwsh -File tools/docs-check.ps1 -RepoRoot . -Paths <changed-md>`
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`

## done definition

- repo 시작 문서가 Pindoc product/design SoT 경계를 명시한다.
- active index가 제거된 product/design longform을 가리키지 않는다.
- 남은 `docs/02_design/**` 문서는 code-facing contract 또는 transitional hold로 분류된다.
- 검증 결과와 남은 리스크가 `status.md`에 기록된다.

## deferred

- `docs/02_design/combat/**`, `meta/**`, `systems/**`, 일부 `ui/**`를 `docs/03_architecture/**` 또는 Pindoc artifact로 세분 이동하는 후속 작업.
- Pindoc artifact별 supersede/update 세부 작업.
- stale repo narrative seed를 runtime seed와 Pindoc latest 기준으로 동기화하는 작업.
