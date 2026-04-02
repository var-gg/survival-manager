# 작업 명세

## 메타데이터

- 작업명: Code Structure Hot-Path Routing
- 담당: Codex
- 상태: handoff-ready
- 최종수정일: 2026-04-02
- 관련경로:
  - `AGENTS.md`
  - `docs/index.md`
  - `docs/03_architecture/index.md`
  - `docs/03_architecture/testing-strategy.md`
  - `docs/07_release/index.md`
  - `docs/00_governance/source-of-truth-matrix.md`
  - `tasks/014_code_structure_hot_path_routing/`
- 관련문서:
  - `docs/00_governance/docs-governance.md`
  - `docs/00_governance/docs-harness.md`
  - `docs/03_architecture/coding-principles.md`
  - `docs/03_architecture/dependency-direction.md`
  - `docs/00_governance/implementation-review-checklist.md`

## Goal

- 코드 구조 거버넌스 규칙을 새로 만들지 않고, 기존 기준 문서를 에이전트 기본 시작면과 source-of-truth matrix에 끌어올린다.

## Authoritative boundary

- 이번 task의 단일 axis는 code-structure governance routing과 문서 start surface 정리다.
- source-of-truth는 기존 정책 문서가 유지하고, 이번 task는 그 정책을 AGENTS/index/matrix/task에 재배선한다.
- docs harness 정책 본체, ADR 본문, gameplay 구현 변경은 동시에 닫지 않는다.

## In scope

- `AGENTS.md`
- `docs/index.md`
- `docs/03_architecture/index.md`
- `docs/03_architecture/testing-strategy.md`
- `docs/07_release/index.md`
- `docs/00_governance/source-of-truth-matrix.md`
- `tasks/014_code_structure_hot_path_routing/spec.md`
- `tasks/014_code_structure_hot_path_routing/plan.md`
- `tasks/014_code_structure_hot_path_routing/implement.md`
- `tasks/014_code_structure_hot_path_routing/status.md`

## Out of scope

- `docs/00_governance/docs-governance.md`, `docs/00_governance/docs-harness.md`, `docs/00_governance/index.md` 수정
- `.agents/skills/code-structure-guard/SKILL.md`, `.agents/skills/docs-maintainer/SKILL.md` 수정
- 새 ADR 추가
- gameplay/code/asmdef/public API 변경

## asmdef impact

- 영향 없음
- asmdef 추가/삭제/의존 변경 없음
- cycle 위험 없음

## persistence impact

- 영향 없음
- 저장 모델, 포트, record, `SM.Meta`, `SM.Unity`, `SM.Persistence.Abstractions` 책임 변화 없음

## validator / test oracle

- validator 확장 없음
- 문서 검증 오라클은 `tools/docs-policy-check.ps1`, `tools/docs-check.ps1`, `tools/smoke-check.ps1`, 범위 한정 `git diff --check`
- runtime path smoke 없음

## done definition

- `AGENTS.md`가 code-structure routing을 first-class trigger로 노출한다.
- `docs/index.md`가 compact top-level index가 되고 `07_release`를 포함한다.
- `docs/03_architecture/index.md`가 oracle 문서와 testing draft 역할을 분리한다.
- `docs/00_governance/source-of-truth-matrix.md`에 code structure governance 행이 생긴다.
- `docs/03_architecture/testing-strategy.md`가 closure 문서가 아니라 검증 우선순위 문서로 좁혀진다.
- 검증 스크립트 결과와 남은 repo-wide debt가 `status.md`에 기록된다.

## deferred

- docs harness 정책 본문 추가 개정
- 관련 ADR 추가/정리
- skill routing asset 본문 강화
- repo-wide markdownlint / markdown-link-check debt 정리
