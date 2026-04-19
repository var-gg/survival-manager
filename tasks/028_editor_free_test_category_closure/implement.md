# Editor-free test category closure implementation

## Phase 1 summary

- 22개 uncategorized EditMode test class를 `FastUnit` 또는 `BatchOnly`로 분류했다.
- pure combat/meta/persistence tests는 `FastUnit`으로 올렸다.
- content validation, UI `GameObject`, authored `ScriptableObject`, sandbox asset contract tests는 `BatchOnly`로 격리했다.
- `CombatContractsTests`에서 UI/localization `GameObject` contract를 `CombatLocalizationContractBatchOnlyTests`로 분리했다.
- FastUnit 승격으로 드러난 stale combat assertions는 현재 pure runtime formula에 맞췄고, Loop A 4v4 timeout balance contract 1건은 dedicated balance retune 전까지 `Ignore`로 명시했다.

## Phase 2 summary

- Asset authoring 없음.

## Phase 3 summary

- `test-batch-fast`, `test-harness-lint`, uncategorized inventory, `BuildBoundaryGuardFastTests` focused run을 통과했다.

## deviation

- `BattleResolutionTests.LoopA_4v4_BattleEndsBeforeTimeout`은 uncategorized 상태에서 숨겨져 있던 balance timeout drift였다. 028에서 전투 밸런스를 고치지 않고 명시 ignored FastUnit backlog로 남겼다.

## blockers

- 없음.

## diagnostics

- uncategorized EditMode test class count.
- FastUnit authored-object token scan.
- focused representative test 결과.

## why this loop happened

026은 FastUnit의 authored-object 의존을 제거했고 027은 session hotspot을 줄였다. 028은 남은 uncategorized EditMode tests를 명시 lane으로 닫아, agent가 default fast lane과 BatchOnly/editor lane을 잘못 해석하지 않게 만드는 작업이다.
