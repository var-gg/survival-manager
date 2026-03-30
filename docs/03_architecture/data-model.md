# 데이터 모델 기준

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-29
- 소스오브트루스: `docs/03_architecture/data-model.md`
- 관련문서:
  - `docs/03_architecture/content-authoring-model.md`
  - `docs/03_architecture/content-authoring-and-balance-data.md`
  - `docs/03_architecture/persistence-strategy.md`
  - `docs/03_architecture/coding-principles.md`

## 목적

이 문서는 authored definition, runtime instance, save model을 분리하는 기준을 정의한다.
목표는 콘텐츠 자산, 런타임 truth, 저장 truth가 서로를 오염시키지 않게 하는 것이다.

## 기본 구분

### definition

- authored content blueprint
- 주로 `ScriptableObject` 또는 동등 authored asset
- 변경 주체는 authoring/editor 흐름

### runtime instance

- 전투, 원정, 보상 선택 같은 실제 플레이 상태
- save에 반영되기 전까지 메모리 안에서 동작
- presentation과 별개로 계산 가능해야 함

### save model

- 직렬화와 마이그레이션을 고려한 persistence 모델
- authored asset 직접 참조 없이 stable id 중심으로 저장

## 예시

- `HeroDefinition` ↔ `HeroRuntimeState` ↔ `HeroSaveModel`
- `SkillDefinition` ↔ `SkillRuntimeState` ↔ `SkillSaveModel`
- `RewardDefinition` ↔ `RewardOfferState` ↔ `RewardSaveModel`

## namespace/assembly 기준

- definition 관련 런타임 친화 모델: `SM.Content.*`
- 전투 상태: `SM.Combat.*`
- 메타 상태: `SM.Meta.*`
- 저장 계약과 save model: `SM.Persistence.Abstractions.*`
- 저장 구현 세부: `SM.Persistence.Json.*`, `SM.Persistence.Postgres.*`

## 분리 신호

- 하나의 타입이 authored field와 runtime mutable field를 동시에 가지면 분리 검토
- 저장 모델이 Unity object reference를 들고 있으면 경계 위반
- UI 표시 편의를 위해 battle truth와 save truth를 한 객체로 합치면 안 된다
