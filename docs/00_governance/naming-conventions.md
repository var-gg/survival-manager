# 문서 명명 규칙

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-28
- 소스오브트루스: `docs/00_governance/naming-conventions.md`
- 관련문서:
  - `docs/00_governance/docs-governance.md`
  - `docs/index.md`

## 목적

문서 파일명과 문서 제목의 일관성을 유지해 탐색성과 자동화 안정성을 높인다.

## 파일명 규칙

1. 파일명은 소문자 kebab-case를 사용한다.
2. 숫자 정렬이 필요한 폴더는 두 자리 접두사로 시작한다.
3. ADR은 `adr-0001-주제.md` 형식을 사용한다.
4. 인덱스 문서는 폴더마다 `index.md`를 사용한다.
5. 임시 메모는 장기 문서 경로에 두지 않는다.

## 제목 규칙

1. 제목은 문서 목적이 드러나야 한다.
2. 파일명과 제목은 의미가 일치해야 한다.
3. 하나의 문서에 두 개 이상의 핵심 주제를 넣지 않는다.

## 경로 규칙

- 제품 문서는 `01_product/`
- 디자인 문서는 `02_design/`
- 구조와 기술 경계는 `03_architecture/`
- 결정 기록은 `04_decisions/`
- 설정 절차는 `05_setup/`
- 운영 문서는 `06_production/`
- 릴리스 문서는 `07_release/`
