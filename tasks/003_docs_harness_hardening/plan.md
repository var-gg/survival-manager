# 작업 계획

## 메타데이터
- 작업명: Docs Harness Hardening
- 담당: Codex
- 상태: in_progress
- 최종수정일: 2026-03-31
- 의존:
  - `tasks/_templates/spec.md`
  - `tasks/_templates/plan.md`
  - `tasks/_templates/status.md`
  - `docs/00_governance/docs-governance.md`
  - `docs/00_governance/agent-operating-model.md`
  - `docs/00_governance/task-execution-pattern.md`

## 마일스톤
1. task 문서를 만들고 현재 docs/index/deprecated/language drift 상태를 inventory로 고정한다.
2. docs harness, lifecycle, registry, source-of-truth matrix, eval 초안을 추가하고 AGENTS/skill을 같은 규칙으로 묶는다.
3. active index 정리, deprecated 파일 제거, `05_setup` 인덱스 복구, 영어 drift 정규화를 수행한다.
4. deterministic check와 CI를 추가하고 결과를 `status.md`에 기록한다.

## 승인 기준
- 기본 컨텍스트 축소 규칙과 deprecated suppression 규칙이 AGENTS, 거버넌스 문서, 스킬에 모두 반영된다.
- deprecated 문서 이유는 중앙 registry 또는 replacement/ADR에만 남고 원본 파일에 영구 축적되지 않는다.
- active index는 active/draft 문서만 노출하고 실제 파일 집합을 대표한다.
- docs policy check가 active index, language/meta, index coverage, orphan, registry 유효성을 검증한다.

## 검증 명령
```powershell
pwsh -File tools/docs-policy-check.ps1 -RepoRoot .
pwsh -File tools/docs-check.ps1 -RepoRoot .
pwsh -File tools/smoke-check.ps1 -RepoRoot .
```

## 중단 조건
- deprecated 파일 삭제 전에 replacement 또는 registry 경로가 확정되지 않으면 삭제를 진행하지 않는다.
- docs policy check를 통과시키려면 범위를 과도하게 넓혀 unrelated gameplay/task를 건드려야 하는 상황이면 staged enforcement로 조정하고 이유를 남긴다.
- index와 실제 파일 집합이 계속 불일치하면 신규 문서 추가를 멈추고 탐색성 복구를 우선한다.
