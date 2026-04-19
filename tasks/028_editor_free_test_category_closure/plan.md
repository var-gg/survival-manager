# Editor-free test category closure plan

## Preflight

- `git status --short --branch`로 027 이후 clean main을 확인한다.
- `Assets/Tests/EditMode/**`에서 `[Test]`, `[TestCase]`, `[UnityTest]`가 있으나 `[Category]`가 없는 class를 inventory한다.
- 각 class의 `UnityEngine`, `ScriptableObject`, `SM.Content.Definitions`, `UnityEditor`, `AssetDatabase`, `RuntimeCombatContentLookup`, public `GameSessionState` 사용 여부를 확인한다.

## Phase 1 code-only

- pure combat/meta/persistence test class에 `[Category("FastUnit")]`를 추가한다.
- editor/content/Unity object lifecycle test class에 `[Category("BatchOnly")]`를 추가한다.
- `CombatContractsTests`에서 Unity `GameObject` localization contract를 별도 BatchOnly class로 분리하고, 남은 combat resolution tests는 FastUnit으로 둔다.
- production code, asmdef, content asset은 수정하지 않는다.

## Phase 2 asset authoring

없음. Unity batch run이 asset을 자동 touch하면 task 범위 밖 side effect로 복구한다.

## Phase 3 validation

- FastUnit 전체를 실행한다.
- test harness lint를 실행한다.
- representative focused tests를 실행한다.
- task/docs 변경에 대해 docs policy/check/smoke를 실행한다.

## rollback / escape hatch

- FastUnit으로 올린 test가 authored Unity object token guard에 걸리면 BatchOnly로 내린다.
- BatchOnly로 내린 test 중 coverage loss가 큰 pure subset은 별도 class로 분리한다.
- long-running `ManualLoopD`는 이번 task에서 강제로 BatchOnly로 합치지 않는다.

## tool usage plan

- 파일 수정은 `apply_patch`를 사용한다.
- Unity batch 명령은 순차 실행한다.
- category inventory는 `rg`/PowerShell read-only scan으로 확인한다.

## loop budget

- category correction retry: 2회
- FastUnit regression retry: 2회
- docs-check retry: 1회
