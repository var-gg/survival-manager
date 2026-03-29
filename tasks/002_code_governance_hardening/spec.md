# 작업 명세

## 메타데이터
- 작업명: Code Governance Hardening
- 담당: Codex
- 상태: handoff-ready
- 최종수정일: 2026-03-29
- 관련경로:
  - `docs/00_governance/**`
  - `docs/03_architecture/**`
  - `docs/04_decisions/**`
  - `.agents/skills/**`
  - `tasks/002_code_governance_hardening/**`
- 관련문서:
  - `docs/index.md`
  - `docs/00_governance/docs-governance.md`
  - `docs/00_governance/task-execution-pattern.md`
  - `docs/03_architecture/index.md`
  - `docs/04_decisions/index.md`

## 목표
- AI와 사람이 같은 기준으로 구조·코딩 경계·문서 갱신 규칙을 적용할 수 있도록 거버넌스 문서를 정비한다.
- 기존 문서 충돌을 해소하고, 코딩 원칙/의존 방향/Unity 경계의 기준 문서를 추가한다.
- 구조 정책을 강제하는 검수 체크리스트, 스킬, ADR, 인덱스 체계를 한 작업 단위 안에서 맞춘다.

## 비목표
- 게임 기능 추가
- 코드, 씬, 패키지, `Assets/ThirdParty/**` 수정
- 기존 제품/디자인 문서의 전면 재작성

## 제약
- 지속 문서는 한국어로 유지하고 파일명, 코드, API 식별자는 영어를 유지한다.
- 한 문서에는 하나의 목적만 둔다.
- 구조/정책 변경 시 관련 `index.md`, `관련문서`, 내부 링크를 함께 갱신한다.
- 이번 작업은 문서와 스킬 파일만 수정한다.

## 산출물
- 기존 거버넌스/아키텍처/ADR 충돌 정리
- `docs/03_architecture/coding-principles.md`
- `docs/03_architecture/dependency-direction.md`
- `docs/03_architecture/unity-boundaries.md`
- `docs/00_governance/implementation-review-checklist.md`
- `.agents/skills/code-structure-guard/SKILL.md`
- 번호 충돌이 해소된 ADR 인덱스 및 신규 ADR
- 갱신된 task 문서

## 완료 기준
- `docs/00_governance/index.md`, `docs/03_architecture/index.md`, `docs/04_decisions/index.md`, `docs/index.md`가 실제 상태와 일치한다.
- ADR 번호 충돌과 관련 링크가 모두 정리된다.
- `SM.*` 기준과 Unity 경계 규칙이 문서 전반에 일관되게 반영된다.
- 새 정책 문서, 체크리스트, 스킬, ADR이 서로 링크되고 목적이 분리된다.
