# Postgres 설정

- 상태: draft
- 최종수정일: 2026-03-31
- 단계: prototype

## 목적

이 문서는 `survival-manager`의 로컬 개발용 Postgres 설정을 설명한다.
MVP는 Postgres를 쓸 수 없더라도 플레이 가능해야 한다.

## 정책 요약

- Postgres는 gameplay requirement가 아니라 local development adapter다.
- direct client -> production DB 경로는 가정하지 않는다.
- DB가 없을 때도 JSON fallback으로 게임이 돌아가야 한다.
- content definition은 DB record가 아니라 Unity asset으로 유지한다.
- per-frame combat state는 DB에 저장하지 않는다.

## Schema 이름

Postgres schema는 아래를 사용한다.

- `survival_manager`

## SQL 스크립트

아래 순서로 적용한다.

1. `tools/sql/001_survival_manager_schema.sql`
2. `tools/sql/002_survival_manager_bootstrap.sql`

## 연결 정보 소스

연결 정보는 아래 위치에서만 읽는다.

- environment variable
- local machine config

권장 env var:

- `SM_POSTGRES_CONNECTION`

선택적 mode env var:

- `SM_PERSISTENCE_MODE=postgres`

## MVP 런타임 동작

Postgres가 설정되어 있고 사용 가능하면 local-dev adapter를 사용할 수 있다.
Postgres가 없거나, 접근할 수 없거나, 현재 build에서 지원되지 않으면 runtime은 JSON save로 fallback해야 한다.

## 권장 로컬 흐름

1. local Postgres를 시작한다.
2. schema/bootstrap SQL을 적용한다.
3. `SM_POSTGRES_CONNECTION`을 설정한다.
4. 필요하면 `SM_PERSISTENCE_MODE=postgres`를 설정한다.
5. 게임을 실행한다.
6. DB 경로가 실패해도 JSON fallback이 여전히 동작하는지 확인한다.

## 비목표

- direct production database access
- DB가 content definition의 소유자가 되는 구조
- per-frame combat simulation state persistence
- remote infra 없이는 MVP gameplay가 동작하지 않는 구조

## 열린 질문

- local Postgres adapter는 언제 placeholder에서 full CRUD 구현으로 넘어가야 하는가?
- Unity editor workflow에서는 env var 외에 어떤 local config format이 적절한가?
- raw SQL 파일을 넘는 migration tooling은 언제 도입해야 하는가?
- 첫 실제 DB adapter에서도 run summary는 append-only로 유지해야 하는가?
