# Loop A fast contract closure spec

## Goal

`BattleResolutionTests.LoopA_4v4_BattleEndsBeforeTimeout`에 남아 있던 ignored FastUnit 구멍을 닫고, Loop A 다인전이 deterministic fast fixture에서 timeout blind spot 없이 종료되는지 검증한다.

## Authoritative boundary

- 이 작업의 소유 경계는 `SM.Tests.EditMode` FastUnit 테스트다.
- production `SM.Combat` 수치, authored content, scene/prefab, `SM.Unity` runtime path는 변경하지 않는다.
- 대칭 4v4 balance draw 여부는 별도 balance/sweep lane의 주제이며, FastUnit 종료 oracle은 비대칭 deterministic fixture로 고정한다.

## In scope

- ignored `LoopA_4v4_BattleEndsBeforeTimeout`를 실행 가능한 FastUnit 테스트로 대체한다.
- 테스트가 4v4, front/back anchor, melee/ranged mix, deterministic seed를 유지하게 한다.
- 실패 메시지는 timeout, 남은 생존 수, winner drift를 진단할 수 있게 유지한다.
- task 문서에 변경 이유와 검증 근거를 남긴다.

## Out of scope

- combat production formula 변경.
- `BattleSimulator.DefaultMaxSteps` 변경.
- authored balance data, ScriptableObject, scene/prefab 수정.
- Loop D telemetry/balance manual lane 수리.

## asmdef impact

- 새 asmdef 또는 asmdef reference 변경 없음.
- 변경 파일은 `Assets/Tests/EditMode/BattleResolutionTests.cs`와 task 문서뿐이다.

## persistence impact

- 없음. save contract, serializer, repository adapter를 건드리지 않는다.

## validator / test oracle

- focused: `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.BattleResolutionTests.LoopA_4v4_AsymmetricBattleEndsBeforeTimeout`
- default: `pwsh -File tools/unity-bridge.ps1 test-batch-fast`
- preflight: `pwsh -File tools/test-harness-lint.ps1 -RepoRoot .`
- docs: changed-docs `docs-check`, docs policy, smoke.

## done definition

- `BattleResolutionTests`에 ignored Loop A timeout contract가 남지 않는다.
- replacement test는 FastUnit lane에서 실행되고 pass한다.
- `test-batch-fast`의 skipped 수가 해당 ignore 제거만큼 줄어든다.
- task status가 evidence와 deferred 범위를 기록한다.

## deferred

- 완전 대칭 4v4 mirror battle의 draw/timeout 허용 여부는 balance sweep 또는 ManualLoopD에서 별도로 다룬다.
- full BatchOnly backlog green은 이번 작업 범위가 아니다.
