# docs markdownlint cleanup 구현 기록

## 메타데이터

- 작업명: docs markdownlint cleanup
- 담당: Codex
- 상태: completed
- 최종수정일: 2026-04-19

## Phase summary

- Phase 0: task 문서 생성.
- Phase 1: markdownlint backlog 계측 및 표 spacing 기계 정리.
- Phase 2: 남은 semantic Markdown 오류 수동 정리.
- Phase 3: docs policy, full docs-check, repo smoke 검증.

## Diagnostics

- full `docs-check`는 docs policy pass 후 markdownlint에서 1716건으로 실패했다.
- 오류 대부분은 generated/compact table spacing인 MD060이었다.

## Changes

- 11개 문서의 Markdown 표 spacing을 compact style에 맞춰 정규화했다.
- `character-lore-registry.md`와 `master-script.md`의 강조형 소제목을 실제 heading으로 전환했다.
- `narrative-pacing-formula.md`와 `CLAUDE.md`의 언어 없는 fenced code block에 `text` language를 지정했다.
- blockquote 내부 빈 줄과 package README의 inline HTML placeholder를 정리했다.
- `augment-catalog-v1.md` ordered list 번호는 markdownlint auto-fix 결과를 유지했다.

## Deviation

- 없음. `.markdownlint-cli2.jsonc` rule 완화는 하지 않았다.

## Blockers

- 없음.

## Verification

- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .` pass.
- `pwsh -File tools/docs-check.ps1 -RepoRoot .` pass.
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .` pass.
