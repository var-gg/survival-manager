# 작업 명세

## 메타데이터
- 작업명: Docs Harness Hardening
- 담당: Codex
- 상태: in_progress
- 최종수정일: 2026-03-31
- 관련경로:
  - `AGENTS.md`
  - `docs/00_governance/**`
  - `docs/02_design/**`
  - `docs/03_architecture/**`
  - `docs/05_setup/**`
  - `.agents/skills/docs-maintainer/**`
  - `tools/docs-check.ps1`
  - `tools/docs-policy-check.ps1`
  - `.github/workflows/docs-lint.yml`
  - `tasks/003_docs_harness_hardening/**`
- 관련문서:
  - `docs/index.md`
  - `docs/00_governance/docs-governance.md`
  - `docs/00_governance/agent-operating-model.md`
  - `docs/00_governance/task-execution-pattern.md`
  - `docs/05_setup/index.md`

## 목표
- Markdown 문서를 기본 컨텍스트에 넓게 병합하지 않도록 저장소 하네스를 설치한다.
- deprecated 문서가 active index와 기본 검색 경로로 재주입되지 않게 lifecycle 규칙과 중앙 registry를 도입한다.
- durable docs, live task state, agent routing asset의 역할을 분리하고 한국어 문서 일관성을 회복한다.
- deterministic check와 eval 초안을 추가해 문서 엔트로피를 반복적으로 제어할 수 있게 만든다.

## 비목표
- gameplay 구현 추가
- Unity scene/prefab/package 구조 변경
- MCP/CLI 운영 문서를 삭제하거나 구현 범위를 재설계하는 일
- `Assets/ThirdParty/**` 수정

## 제약
- 기본 시작 컨텍스트는 `AGENTS.md` -> `docs/index.md` -> 관련 폴더 `index.md` -> 현재 task `status.md` 체인을 따른다.
- `status: deprecated` 문서는 active source로 쓰지 않고 replacement, ADR, registry를 우선한다.
- 지속 문서와 human-facing task 보고는 한국어 본문/메타데이터를 유지한다.
- 문서 수정 시 관련 `index.md`, `관련문서`, task `status.md`, 검증 스크립트를 같은 작업 단위에서 갱신한다.
- 기존 사용자의 구현 변경과 충돌하지 않도록 문서/도구 범위만 편집한다.

## 산출물
- `tasks/003_docs_harness_hardening/` 실행 문서 세트
- docs harness / lifecycle / registry / eval 관련 거버넌스 문서
- deprecated pointer 제거와 registry 기반 정리 결과
- 갱신된 `docs/05_setup/index.md` 및 언어 정규화된 문서/스킬
- `tools/docs-policy-check.ps1`와 CI 연동
- 문서 역할과 source-of-truth matrix 정리

## 완료 기준
- active index에서 deprecated pointer가 제거되고 원본 deprecated 파일이 registry로 대체된다.
- `AGENTS.md`, 거버넌스 문서, `docs-maintainer` 스킬이 같은 harness 규칙을 가리킨다.
- `docs/05_setup/index.md`가 실제 파일 집합과 일치한다.
- `tools/docs-check.ps1`와 CI가 docs policy 검사를 포함한다.
- 최소 5개 docs eval 초안이 저장소에 기록되고 task `status.md`에 검증 결과가 남는다.
