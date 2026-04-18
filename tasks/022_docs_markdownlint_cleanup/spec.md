# docs markdownlint cleanup 명세

## 메타데이터

- 작업명: docs markdownlint cleanup
- 담당: Codex
- 상태: in_progress
- 최종수정일: 2026-04-19
- 관련경로:
  - `CLAUDE.md`
  - `docs/**/*.md`
  - `Packages/com.coplaydev.unity-mcp/README.md`
  - `.markdownlint-cli2.jsonc`
- 관련문서:
  - `docs/index.md`
  - `docs/00_governance/docs-governance.md`
  - `docs/00_governance/docs-harness.md`
  - `tasks/021_specialist_encounter_tuning_localization/status.md`

## Goal

- 기존 repo-wide markdownlint backlog를 별도 docs cleanup task로 닫는다.
- rule을 완화하지 않고 active docs와 root docs의 실제 Markdown 형식을 고친다.
- docs policy, full docs-check, smoke check를 green으로 만든다.

## Authoritative boundary

- 문서 본문 의미는 유지하고 Markdown 형식만 정리한다.
- persistent docs와 task/status 문서는 한국어 본문을 유지한다.
- file name, command, API 식별자는 영어를 유지한다.

## In scope

- markdownlint가 보고한 MD060, MD036, MD040, MD029, MD028, MD033을 파일별로 수정한다.
- 표 spacing, ordered list 번호, 코드펜스 언어, blockquote 공백, 강조 제목 형식을 정리한다.
- task 022 문서에 backlog 분류와 검증 결과를 남긴다.

## Out of scope

- `.markdownlint-cli2.jsonc` rule 완화
- 문서 의미 변경이나 새 설계 결정 추가
- 코드, Unity asset, scene, prefab 변경
- docs lifecycle/deprecated 정책 변경

## asmdef impact

- 없음.

## persistence impact

- 없음.

## validator / test oracle

- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
- `pwsh -File tools/docs-check.ps1 -RepoRoot .`
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`

## done definition

- full `docs-check` markdownlint error 0.
- docs policy check와 smoke check가 pass한다.
- markdownlint rule을 완화하지 않는다.
- 변경 파일은 Markdown/docs cleanup 범위에만 남는다.
