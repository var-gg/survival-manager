# 테스트 전략

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-04-02
- 소스오브트루스: `docs/03_architecture/testing-strategy.md`
- 관련문서:
  - `docs/03_architecture/validation-and-acceptance-oracles.md`
  - `docs/00_governance/implementation-review-checklist.md`
  - `docs/03_architecture/dependency-direction.md`
  - `docs/03_architecture/unity-boundaries.md`

## 목적

이 문서는 feature closure나 done 판정을 정의하지 않는다.
이 문서는 저비용 검증 표면을 어떤 순서로 추가할지에만 답한다.

## 비목표

- acceptance matrix를 정의하지 않는다.
- `status.md` evidence 형식을 정의하지 않는다.
- compile / validator / targeted tests / runtime smoke의 closure 의미를 다시 정의하지 않는다.
- task closure 판정을 대체하지 않는다.

## 역할 경계

- feature closure, acceptance matrix, evidence 기록 규칙은 `validation-and-acceptance-oracles.md`가 담당한다.
- 이 문서는 validator, EditMode, integration, PlayMode, manual smoke를 어떤 순서로 붙일지에만 집중한다.

## 검증 표면 추가 순서

1. 문서/구조 점검
2. data/catalog validator
3. targeted EditMode test
4. focused integration test
5. PlayMode test
6. manual runtime smoke

## 우선 선택 기준

- scene만 유일한 검증 표면으로 삼지 않는다.
- data, prefab, settings asset 검증을 먼저 만든다.
- save/load 계약, asmdef 경계, content catalog 무결성 변화에는 validator 또는 targeted EditMode test를 먼저 붙인다.
- broad PlayMode보다 focused integration을 우선한다.
- long-running smoke는 마지막 phase gate로 둔다.
