# 작업 명세

## 메타데이터

- 작업명: Loop C Content Governance Closure
- 담당: Codex
- 상태: 진행 중
- 최종수정일: 2026-04-01
- 관련경로:
  - `Assets/_Game/Scripts/Runtime/Content/**`
  - `Assets/_Game/Scripts/Editor/Validation/**`
  - `Assets/_Game/Scripts/Editor/Authoring/**`
  - `Assets/_Game/Scripts/Runtime/Unity/**`
  - `Assets/Resources/_Game/Content/Definitions/**`
  - `Assets/Tests/**`
  - `docs/**`
- 관련문서:
  - `docs/02_design/systems/content-budgeting-contract.md`
  - `docs/02_design/systems/rarity-ladder-contract.md`
  - `docs/02_design/combat/counter-system-topology.md`
  - `docs/02_design/systems/v1-forbidden-list.md`
  - `docs/04_decisions/adr-0018-loop-c-content-governance.md`

## Goal

- 전투/메타 콘텐츠가 계속 늘어나도 규칙을 다시 열지 않도록 `BudgetCard`,
  `ContentRarity`, 8-lane counter topology, fatal forbidden policy를 공통 계약으로
  닫는다.

## Authoritative boundary

- 이번 task는 content governance metadata의 source-of-truth를 `BudgetCard`와 Loop C
  registry로 이동한다.
- `CombatRoleBudgetProfile`의 canonical 위치는 `BudgetCard` 안으로 통일한다.
- `ContentRarity`는 validator/compiler/runtime combat/debug용이고,
  `RecruitTier`는 offer generation/pity/save/player badge용으로 고정한다.
- recruit economy, pity 수치, retrain cost, loadout topology, summon ownership 재정의는
  동시에 닫지 않는다.

## In scope

- 공통 governance enum/contract 추가와 authoring definition 확장
- runtime template/snapshot용 compact governance summary carry-through
- Loop C validator pass와 audit artifact 생성
- dev sandbox/debug overlay의 budget/counter coverage 표시
- 8-lane threat scenario 추가
- Loop C task packet과 지속 문서 작성 및 index 동기화

## Out of scope

- recruit scoring 재설계
- pity/retrain 수치 재조정
- unique/legendary loot rarity 재설계
- post-launch rarity 확장 예약
- budget를 승률 예측기로 고도화하는 작업

## asmdef impact

- 주요 영향 asmdef:
  - `SM.Content`
  - `SM.Combat`
  - `SM.Unity`
  - `SM.Meta`
  - `SM.Editor`
  - `SM.Tests.EditMode`
- 허용 의존:
  - `SM.Editor` -> runtime asmdef read-only 참조
  - `SM.Unity` / `SM.Meta` -> `SM.Content` authored contract 참조
- 금지 의존:
  - runtime asmdef에서 editor validator 직접 참조
  - persistence asmdef로 governance-only editor type 누수
- cycle 위험 메모:
  - `BudgetCard`는 `SM.Content`에 두고 editor 전용 pass는 `SM.Editor`에 둬서
    `SM.Content <-> SM.Editor` 순환을 막는다.

## persistence impact

- save serialization의 canonical rarity는 계속 `RecruitTier`를 사용한다.
- runtime combat template/snapshot에는 `ContentRarity` 기반 governance summary만 싣고,
  `RecruitTier`는 싣지 않는다.
- `SM.Persistence.Abstractions` 책임은 바꾸지 않는다.

## validator / test oracle

- 필수 validator:
  - `BudgetWindowValidationPass`
  - `BudgetIdentityValidationPass`
  - `RarityComplexityValidationPass`
  - `CounterTopologyValidationPass`
  - `SynergyStructureValidationPass`
  - `V1ForbiddenFeatureValidationPass`
- 필수 EditMode test:
  - `AllContentHasBudgetCardTests`
  - `BudgetWindowValidationTests`
  - `BudgetIdentityValidationTests`
  - `RarityComplexityValidationTests`
  - `CounterTopologyValidationTests`
  - `SynergyStructureValidationTests`
  - `ForbiddenFeaturePolicyTests`
  - `CounterCoverageReportAggregationTests`
  - `RecruitTierContentRarityMappingTests`
- runtime path smoke:
  - compile
  - content validation artifact 생성
  - town recruit card / sandbox inspect / combat sandbox coverage 표시

## done definition

- 핵심 content asset에 `BudgetCard`가 존재한다.
- Loop C validator가 전 asset에서 green이다.
- forbidden feature 위반이 0건이다.
- team counter coverage가 dev UI에서 읽힌다.
- audit artifact 4종이 `Logs/content-validation/`에 남는다.
- Loop C docs/task packet/index가 서로 같은 결론을 가리킨다.

## deferred

- telemetry 기반 pruning
- first playable balance pass의 수치 재조정
- item loot rarity 정리와 unique identity 문서 통합
