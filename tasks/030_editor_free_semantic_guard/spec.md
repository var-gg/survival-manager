# Editor-free semantic guard spec

## Goal

026/028/029로 정리한 editor-free FastUnit lane과 test category 정책이 다시 흐려지지 않도록 lightweight guard를 추가한다.

## Authoritative boundary

- FastUnit은 editor-free/resource-free/authored-object-free lane이다.
- EditMode test class는 class-level `FastUnit`, `BatchOnly`, `ManualLoopD` 중 하나를 명시한다.
- authored Unity object, editor/content bootstrap, production session constructor는 FastUnit에 들어가지 않는다.
- `SM.Unity.Session/**`은 asset/editor loading choke point를 직접 소유하지 않는다.

## In scope

- `BuildBoundaryGuardFastTests`에 class-level category closure guard를 추가한다.
- `tools/test-harness-lint.ps1`에 같은 category closure preflight를 추가한다.
- method-level category만으로 숨은 테스트 class가 생기지 않도록 `LoopDTelemetryAndBalanceTests`를 class-level `BatchOnly`로 분류한다.
- `AGENTS.md`, `docs/TESTING.md`, task status를 현재 guard와 맞춘다.

## Out of scope

- production runtime behavior 변경.
- full BatchOnly backlog 수리.
- Loop A 4v4 timeout balance contract retune.
- scene/prefab/Resources asset authoring.

## validator / test oracle

- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`
- `pwsh -File tools/test-harness-lint.ps1 -RepoRoot .`
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.BuildBoundaryGuardFastTests`
- docs policy/check/smoke for changed docs/task files.

## done definition

- class-level category 없는 EditMode test class가 있으면 fast boundary guard와 lint가 실패한다.
- existing FastUnit authored-object/session/content guard는 유지된다.
- `LoopDTelemetryAndBalanceTests`의 non-manual tests는 explicit BatchOnly lane으로 들어간다.
