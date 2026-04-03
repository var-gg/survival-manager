# 문서 체계 인덱스

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-02
- 소스오브트루스: `docs/index.md`
- 관련문서:
  - `AGENTS.md`
  - `docs/00_governance/docs-governance.md`
  - `docs/00_governance/docs-harness.md`
  - `docs/00_governance/source-of-truth-matrix.md`

## 목적

이 디렉터리는 `survival-manager`의 공식 문서 체계를 담는다.
현재 저장소 phase는 `prototype`이며, 문서는 실제 구현 상태와 운영 기준을 반영해야 한다.

## 문서 계층

- `00_governance/`: 문서 운영 원칙, 용어, 명명 규칙, 검수 체크리스트
- `01_product/`: 제품 목표, 범위, 사용자 가치
- `02_design/`: 게임/UX/시스템 디자인 문서
- `03_architecture/`: 기술 구조, 코딩 경계, 의존 방향, Unity 특화 제약
- `04_decisions/`: ADR과 주요 기술/운영 결정
- `05_setup/`: 개발 환경 및 초기 설정 절차
- `06_production/`: 플레이테스트/운영 기준 문서
- `07_release/`: 릴리스 기준, 체크리스트, 변경 요약 문서

## 기본 시작 컨텍스트

- `AGENTS.md` -> `docs/index.md` -> 관련 폴더 `index.md` -> 현재 task 상태 문서

## 운영 원칙

- 문서는 한 문서 한 목적을 따른다.
- 폴더 단위 인덱스로 탐색성을 유지한다.
- 구조 변경 시 관련 인덱스와 링크를 함께 갱신한다.
- 지속 문서는 한국어로 유지한다.
- 파일명, 코드, API 식별자는 영어를 유지한다.
- active index는 active/draft 문서만 노출하고 deprecated 문서는 중앙 registry로 관리한다.

## 루트 참조 문서

- `TESTING.md`: 테스트 하네스 가이드 (어셈블리, 카테고리, CLI, lint 정책)

## 시작점

- 거버넌스 인덱스: `00_governance/index.md`
- 제품 인덱스: `01_product/index.md`
- 디자인 인덱스: `02_design/index.md`
- 아키텍처 인덱스: `03_architecture/index.md`
- 의사결정 인덱스: `04_decisions/index.md`
- 설정 인덱스: `05_setup/index.md`
- 운영 인덱스: `06_production/index.md`
- 릴리스 인덱스: `07_release/index.md`
