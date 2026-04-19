# Editor-free test category closure spec

## Goal

`Assets/Tests/EditMode/**`의 uncategorized 테스트를 닫고, editor-free로 실행 가능한 테스트는 `FastUnit`, authored Unity object나 editor/content 경로를 검증하는 테스트는 `BatchOnly`로 명시한다.

## Authoritative boundary

- public production API와 asmdef는 변경하지 않는다.
- FastUnit 의미는 026 기준인 editor-free/resource-free/authored-object-free lane을 유지한다.
- authored `ScriptableObject`, `GameObject` lifecycle, `SM.Content.Definitions`, `AssetDatabase`, `UnityEditor`, production content lookup이 필요한 테스트는 FastUnit에 올리지 않는다.
- `ManualLoopD`는 장시간 balance/telemetry 수동 lane으로 유지하고, 이번 task의 uncategorized closure 대상과 분리한다.

## In scope

- category가 없는 EditMode test class를 `FastUnit` 또는 `BatchOnly`로 분류한다.
- pure combat/meta/persistence 성격의 테스트는 FastUnit에 올린다.
- Unity object/content/editor 성격의 테스트는 BatchOnly로 격리한다.
- 한 파일 안에 pure combat과 Unity object 테스트가 섞인 경우 작은 테스트 class 분리로 lane 의미를 맞춘다.
- FastUnit 승격으로 기존 uncategorized contract drift가 드러나면 production behavior를 이 task에서 바꾸지 않고 status에 명시한다.
- task status에 before/after category inventory와 검증 결과를 남긴다.

## Out of scope

- production runtime behavior 변경.
- content/scene/prefab/Resources asset authoring.
- `ManualLoopD` 장시간 lane 개편.
- `BatchOnly` backlog 수리.
- category 정책 enforcement guard 확장은 030에서 수행한다.

## asmdef impact

없음. 기존 `SM.Tests.EditMode` 안에서 test class category만 정리한다.

## persistence impact

없음. persistence test는 public save contract를 그대로 사용하며 FastUnit category만 명시한다.

## validator / test oracle

- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`
- `pwsh -File tools/test-harness-lint.ps1 -RepoRoot .`
- focused `JsonPersistenceTests`
- focused `LoadoutCompilerClosureTests`
- focused `CombatContractsTests`
- targeted docs check

## done definition

- `[Test]`/`[TestCase]`/`[UnityTest]`가 있는 EditMode test class 중 uncategorized class가 0개다.
- 새 FastUnit class에는 authored Unity object/content/editor token이 없다.
- Unity object/content/editor 성격의 기존 uncategorized tests는 BatchOnly로 격리된다.
- task status에 `ManualLoopD`가 별도 explicit lane으로 남았음을 기록한다.

## deferred

- `ManualLoopD` 장시간 lane 재정의.
- `StatV2AndSandboxTests` 내부 pure stat tests의 추가 분리.
- category closure guard 자동화는 030에서 수행한다.
