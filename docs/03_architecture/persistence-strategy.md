# persistence 전략

- 상태: draft
- 최종수정일: 2026-03-29
- phase: prototype

## 목적

이 문서는 MVP persistence 경계를 정의한다.
목표는 게임 도메인을 production DB 전제에 묶지 않으면서도, 런타임 상태를 일관되게 보존하는 것이다.

## MVP persistence 원칙

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

## 현재 코드 기준 정규화

현재 persistence 관련 asmdef/namespace는 실제 코드 기준으로 다음과 같이 본다.

- asmdef
  - `SM.Persistence.Abstractions`
  - `SM.Persistence.Json`
  - `SM.Persistence.Postgres`
- namespace
  - `SM.Persistence.Abstractions`
  - `SM.Persistence.Abstractions.Models`
  - `SM.Persistence.Json`
  - `SM.Persistence.Postgres`

## 현재 MVP save 범위

현재 문서/코드 기준 저장 대상은 다음과 같다.

- profile
- hero instances
- inventory
- currencies
- unlocked permanent augments
- run summary

다음 항목은 저장 대상이 아니다.

- per-frame combat state
- scene-local transient presentation state
- 콘텐츠 definition 본문

## 현재 MVP persistence 스타일

- 기본 성공 경로는 JSON fallback이다.
- Postgres는 local dev adapter 정책으로만 다룬다.
- 연결 정보는 env 또는 local config에서만 읽는다.
- Postgres가 없어도 데모 플레이를 막지 않아야 한다.

## 장기 확장 지점

- migration/version pipeline
- cloud save adapter
- server-backed persistence adapter
- profile management
- save inspection/debug tooling

## 열린 항목

- JSON format에 version header를 언제 추가할지
- definition id 안정성을 어떤 규칙으로 고정할지
- load 시 재계산 가능한 상태와 저장해야 하는 상태를 어디까지 나눌지
- local config format을 어떤 형태로 표준화할지
