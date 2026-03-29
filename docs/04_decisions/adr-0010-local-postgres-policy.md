# ADR-0010 로컬 Postgres 정책 채택

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-29
- 결정일: 2026-03-29
- 소스오브트루스: `docs/04_decisions/adr-0010-local-postgres-policy.md`
- 관련문서:
  - `docs/03_architecture/persistence-strategy.md`
  - `docs/05_setup/postgres-setup.md`

## 문맥

persistence는 필요하지만 DB 가용성이 local iteration을 막아서는 안 된다.
prototype 단계에서는 플레이 가능성이 우선이며, Postgres는 개발 편의용 adapter여야 한다.

## 결정

다음 정책을 채택한다.

- profile, hero instance, inventory, currency, unlocked permanent augment, run summary를 저장 대상으로 둔다.
- per-frame combat state는 DB 저장 대상으로 두지 않는다.
- Postgres schema 이름은 `survival_manager`를 사용한다.
- 연결 정보는 환경 변수나 local config에서만 읽는다.
- Postgres가 없어도 JSON fallback으로 플레이 가능해야 한다.
- Postgres는 launch-time production assumption이 아니라 local development adapter다.

## 결과

### 기대 효과

- DB 유무와 무관하게 플레이를 이어갈 수 있다.
- persistence adapter 경계가 명확해진다.

### 감수할 비용

- JSON과 Postgres 두 경로를 함께 이해해야 한다.
- adapter 간 정합성 관리가 필요하다.

## 후속

- JSON을 기본 안전 경로로 유지한다.
- local Postgres bootstrap 절차를 문서화한다.
