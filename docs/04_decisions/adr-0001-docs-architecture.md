# ADR-0001 문서 체계와 거버넌스 구조 채택

- 상태: superseded
- 폐기일: 2026-05-16
- 후속 결정: `pindoc://decision-doc-harness-pindoc-migration`
- 소유자: repository
- 최종수정일: 2026-05-16
- 소스오브트루스: `docs/04_decisions/adr-0001-docs-architecture.md`
- 관련문서:
  - `docs/index.md`
  - `docs/00_governance/docs-governance.md`
  - `docs/00_governance/naming-conventions.md`
  - `docs/00_governance/glossary.md`

## 배경

이 프로젝트는 AI 에이전트가 지속적으로 저장소를 수정한다. 문서 탐색 경로와 기준 문서가 불분명하면, 같은 주제를 여러 위치에서 중복 수정하거나 오래된 기준을 따를 위험이 크다.

## 폐기 사유

본 ADR의 기본 폴더 체계는 초기 repo Markdown 중심 정책이었다. 2026-05-16 현재 product/design/narrative/lore의 primary SoT는 Pindoc Wiki로 전환됐고, repo `docs/**`는 하네스, 기술 구조, setup, runtime/content contract 중심으로 축소됐다. 새 기준은 `pindoc://decision-doc-harness-pindoc-migration`과 `docs/00_governance/source-of-truth-matrix.md`를 따른다.

## 결정

다음 문서 체계를 공식 채택한다.

- `00_governance`: 문서 운영 정책과 공통 규칙
- `01_product`: 제품 목표와 범위
- `02_design`: 디자인 문서
- `03_architecture`: 기술 구조 문서
- `04_decisions`: ADR과 결정 기록
- `05_setup`: 개발 환경 설정
- `06_production`: 운영 실무 문서
- `07_release`: 릴리스 문서

추가로 다음 원칙을 채택한다.

1. 각 폴더는 `index.md`를 가진다.
2. 정책/절차/결정 문서는 메타데이터를 가진다.
3. 문서 변경 시 링크와 인덱스를 함께 갱신한다.
4. 장기 영향 결정은 ADR로 남긴다.

## 결과

### 기대 효과

- 사람과 에이전트가 같은 경로로 기준 문서를 찾을 수 있다.
- 문서 변경 영향 범위를 추적하기 쉬워진다.
- 오래된 문서를 즉시 폐기하지 않고 상태 기반으로 관리할 수 있다.

### 비용

- 문서 수정 시 인덱스와 관련 링크를 함께 관리해야 한다.
- 작은 변경에도 메타데이터 갱신이 필요하다.

## 후속 작업

1. 제품, 디자인, 아키텍처 문서의 첫 기준 문서를 순차적으로 추가한다.
2. 실제 Unity 버전과 패키지 정책이 정해지면 설정 문서를 구체화한다.
3. 릴리스와 운영 절차가 생기면 해당 폴더 인덱스를 확장한다.
