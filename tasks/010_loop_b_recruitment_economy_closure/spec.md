# 작업 명세

## 메타데이터

- 작업명: Loop B Recruitment / Retrain / Economy Closure
- 담당: Codex
- 상태: 완료
- 최종수정일: 2026-04-01

## Goal

Loop B를 `4-slot recruit pack + Gold/Echo split + retrain/scout/pity/duplicate/dismiss` 계약으로 코드, validator, 테스트, UI, 문서까지 닫는다.

## Authoritative boundary

- business rule: `SM.Meta`
- runtime orchestration / Town vertical slice: `SM.Unity`
- save truth: `SM.Persistence.Abstractions`
- validator / harness: `SM.Editor`, `SM.Tests`
- source-of-truth docs: `docs/02_design/meta/*.md`, `docs/03_architecture/*.md`, `docs/05_setup/recruitment-and-retrain-harness.md`

## In scope

- recruit/retrain/duplicate/dismiss economy contract
- Gold/Echo wallet split
- recruit preview, on-plan/protected/scout/pity invariant
- unit-local retrain memory/pity/footprint persistence
- runtime guard와 deterministic test/harness

## Out of scope

- duplicate second-copy progression
- refresh slot lock
- scout exact-unit targeting
- Gold/Echo 직접 환전
- new content volume expansion

## asmdef impact

- 신규 asmdef 없음
- 기존 `SM.Meta`, `SM.Unity`, `SM.Persistence.Abstractions`, `SM.Editor`, `SM.Tests` 내부 확장

## persistence impact

- `CurrencyRecord`, `HeroInstanceRecord`, `ActiveRunRecord`에 Loop B state 반영
- `UnitRetrainState`, `UnitEconomyFootprint`, `RecruitPhaseState`, `RecruitPityState` 저장/복원 경로 반영

## validator / test oracle

- `LoopBContractValidator`
- `LoopBContractClosureTests`
- `GameSessionStateTests`
- `PlayModeSmokeTests`

## done definition

- runtime이 Gold/Echo split과 4-slot recruit pack invariants를 강제한다.
- retrain이 flex-only, current/previous exclusion, native coherence, pity를 보장한다.
- duplicate direct grant가 Echo conversion으로 닫힌다.
- dismiss refund와 gear 회수가 동작한다.
- docs/task/harness가 Loop B source-of-truth로 갱신된다.

## deferred

- crafting currency 전체 제거
- duplicate 이후 확장 progression
- 더 큰 meta UI polish
