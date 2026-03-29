# 테스트 전략

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-03-29
- 소스오브트루스: `docs/03_architecture/testing-strategy.md`
- 관련문서:
  - `docs/00_governance/implementation-review-checklist.md`
  - `docs/03_architecture/dependency-direction.md`
  - `docs/03_architecture/unity-boundaries.md`

## 목적

이 문서는 현재 저장소의 검증 우선순위를 정의한다.

## 우선순위

1. 문서와 구조 점검
2. data/catalog validation
3. EditMode smoke test
4. 집중된 integration test
5. PlayMode test
6. 수동 플레이 검증

## 핵심 편향

- scene만 유일한 검증 표면으로 삼지 않는다.
- data, prefab, settings asset 검증을 먼저 만든다.
- save/load 계약, asmdef 경계, content catalog 무결성 변화에는 추가 검증을 붙인다.
