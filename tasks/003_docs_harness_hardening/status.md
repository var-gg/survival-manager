# 작업 상태

## 메타데이터
- 작업명: Docs Harness Hardening
- 담당: Codex
- 상태: handoff-ready
- 최종수정일: 2026-03-31

## 현재 상태
- docs harness, lifecycle, deprecated registry, source-of-truth matrix, eval 초안, policy check, CI gate를 추가했다.
- active index에서 deprecated pointer를 제거했고 원본 deprecated 문서는 registry로 흡수한 뒤 삭제했다.
- `docs/05_setup/index.md`를 실제 파일 집합과 다시 맞췄고, durable docs/task 메타데이터의 영어 drift를 한국어 기준으로 정규화했다.
- `tools/docs-policy-check.ps1`와 `tools/smoke-check.ps1` 검증은 통과했고, full `tools/docs-check.ps1`는 기존 repo-wide markdownlint debt 때문에 아직 통과하지 않는다.

## 완료
- `tasks/003_docs_harness_hardening/` 실행 문서 3종을 생성했다.
- 기본 시작 컨텍스트와 주요 drift 지점을 inventory로 고정했다.
- `AGENTS.md`에 mandatory docs workflow와 기본 시작 컨텍스트 규칙을 추가했다.
- `docs/00_governance/docs-harness.md`, `deprecated-docs-registry.md`, `source-of-truth-matrix.md`, `docs-evals.md`를 추가했다.
- `docs/04_decisions/adr-0017-docs-context-harness.md`를 추가하고 `docs/04_decisions/index.md`를 갱신했다.
- `docs/02_design/index.md`, `docs/03_architecture/index.md`, `docs/06_production/index.md`, `docs/05_setup/index.md`, `docs/index.md`를 실제 corpus 기준으로 갱신했다.
- deprecated pointer였던 전투/아키텍처 문서 6개를 registry에 기록한 뒤 삭제했다.
- `docs/05_setup/local-automation.md`와 `.agents/skills/docs-maintainer/SKILL.md`를 하네스 규칙에 맞게 강화했다.
- `tools/docs-policy-check.ps1`를 추가하고 `tools/docs-check.ps1`, `tools/smoke-check.ps1`, `.github/workflows/docs-lint.yml`을 갱신했다.
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`, `pwsh -File tools/docs-check.ps1 -RepoRoot . -PolicyOnly`, `pwsh -File tools/smoke-check.ps1 -RepoRoot .`를 확인했다.

## 보류
- repo-wide markdownlint / markdown-link-check backlog 정리
- full `tools/docs-check.ps1`를 CI 기본 gate로 승격하는 작업

## 이슈
- 현재 워크트리에 사용자의 gameplay/content 변경이 많이 있어 문서/도구 범위만 편집해야 한다.
- 기존 repo에는 markdownlint line-length, blank-line, heading duplication 등 style debt가 광범위하게 남아 있다.
- 기존 Windows 경로에서는 `markdown-link-check "docs/**/*.md"` glob이 직접 동작하지 않아 `tools/docs-check.ps1`를 파일 목록 순회 방식으로 바꿨다.

## 결정
- 이번 작업은 gameplay 구현으로 범위를 넓히지 않고 docs harness, governance, validation hardening에만 집중한다.
- deprecated pointer 원본은 registry 기반으로 정리하고 active index에서 즉시 제거한다.
- deterministic enforcement는 PowerShell 스크립트 + GitHub Actions를 1차 경로로 둔다.
- CI는 우선 `docs-policy-check` + `smoke-check`를 gate로 사용하고, repo-wide markdown style debt는 별도 정리 작업으로 분리한다.

## 다음 단계
- repo-wide markdownlint debt를 별도 task로 inventory하고 정리 범위를 결정한다.
- `docs-policy-check.ps1`에 필요 시 empty doc, duplicate purpose 같은 추가 규칙을 단계적으로 넣는다.
- MCP/CLI, localization처럼 문서군이 겹치는 영역은 source-of-truth matrix 행을 계속 추가한다.
