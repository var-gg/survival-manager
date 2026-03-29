# persistence 전략

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-29
- 소스오브트루스: `docs/03_architecture/persistence-strategy.md`
- 관련문서:
  - `docs/03_architecture/persistence-schema.md`
  - `docs/03_architecture/dependency-direction.md`
  - `docs/04_decisions/adr-0009-persistence-boundary.md`
  - `docs/04_decisions/adr-0010-local-postgres-policy.md`

## 목적

이 문서는 MVP persistence 경계를 정의한다.
목표는 게임 도메인을 production DB 전제에 묶지 않으면서도, 런타임 상태를 일관되게 보존하는 것이다.

## 핵심 원칙

### persistence 경계

- 콘텐츠 정의는 Unity asset이다.
- 런타임 상태와 save 상태는 별도 persistence 모델로 다룬다.
- save 모델은 authored object 직접 참조 없이 직렬화 가능해야 한다.

### production DB 규칙

- direct client -> production DB를 전제하지 않는다.
- MVP는 local persistence-first로 동작해야 한다.
- DB 통합은 필요하더라도 외부 adapter로 다룬다.

### adapter 규칙

- DB, UI, Scene은 domain logic 바깥 adapter다.
- persistence 구현은 interface/service boundary 뒤에 둔다.
- combat/meta 규칙 안으로 DB 세부 구현을 새지 않게 한다.

## 현재 코드 기준

- `SM.Persistence.Abstractions`
- `SM.Persistence.Abstractions.Models`
- `SM.Persistence.Json`
- `SM.Persistence.Postgres`

## 현재 MVP 스타일

- 기본 성공 경로는 JSON fallback이다.
- Postgres는 local development adapter 정책으로만 다룬다.
- 연결 정보는 environment variable 또는 local config에서만 읽는다.
- Postgres가 없어도 데모 플레이를 막지 않아야 한다.
