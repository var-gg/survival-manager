# persistence 스키마

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-03-29
- 소스오브트루스: `docs/03_architecture/persistence-schema.md`
- 관련문서:
  - `docs/03_architecture/persistence-strategy.md`
  - `docs/04_decisions/adr-0009-persistence-boundary.md`
  - `docs/04_decisions/adr-0010-local-postgres-policy.md`

## 목적

이 문서는 MVP에서 저장하는 데이터와 저장하지 않는 데이터를 명시한다.

## 저장 대상

- profile
- hero instance
- inventory
- currency
- unlocked permanent augment
- run summary

## 저장 비대상

- per-frame combat simulation state
- scene-local transient presentation state
- authored content definition 본문

## 경계 규칙

- definition은 Unity asset이 소유한다.
- persistence는 stable id로 definition을 참조하되 definition 본문을 소유하지 않는다.
- runtime은 DB가 없어도 fail-closed 되지 않아야 한다.
