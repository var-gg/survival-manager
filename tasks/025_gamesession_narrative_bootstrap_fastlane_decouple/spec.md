# GameSessionState fast-lane narrative bootstrap decouple spec

## Goal

`GameSessionState`를 생성하는 `FastUnit` 테스트가 hidden `NarrativeRuntimeBootstrap.LoadFromResources()` 경로를 밟지 않게 한다. production public constructor 동작은 유지하고, fast lane은 resource-free narrative catalog를 명시적으로 주입한다.

## Authoritative boundary

- `GameSessionState(ICombatContentLookup)` public constructor는 production path이며 `NarrativeRuntimeBootstrap.LoadFromResources()`를 유지한다.
- FastUnit session tests는 `GameSessionTestFactory`를 통해 empty `NarrativeRuntimeBootstrap`를 주입한다.
- `Resources.LoadAll`은 `NarrativeRuntimeBootstrap.LoadFromResources()`와 existing content lookup choke point 안에만 둔다.

## In scope

- `NarrativeRuntimeBootstrap.CreateEmpty()` 추가.
- `GameSessionState` internal constructor 추가.
- FastUnit direct `new GameSessionState(...)` callsite를 `GameSessionTestFactory.Create(...)`로 교체.
- non-`BatchOnly` direct session construction guard 추가.
- `AGENTS.md`, `docs/TESTING.md`, dependency direction 문서 갱신.

## Out of scope

- `GameSessionState` ownership migration phase 2.
- `ScriptableObject.CreateInstance` 기반 in-memory fixture 제거.
- scene/prefab/asset authoring.
- `ICombatContentLookup` 이동.
- `SM.Meta` pure boundary 변경.

## asmdef impact

새 asmdef는 만들지 않는다. 기존 `SM.Unity`의 `InternalsVisibleTo("SM.Tests.EditMode")`를 사용해 test factory가 internal constructor와 empty bootstrap을 호출한다.

## persistence impact

없음. save record, profile schema, serializer contract를 변경하지 않는다.

## validator / test oracle

- `test-batch-fast`가 green이어야 한다.
- `test-harness-lint`가 non-`BatchOnly` direct `new GameSessionState(...)`를 실패시켜야 한다.
- focused `BuildBoundaryGuardFastTests`가 같은 회귀를 잡아야 한다.
- focused `GameSessionStateTests`는 production public constructor 경로를 계속 검증해야 한다.

## Done definition

- FastUnit test files에서 direct public `new GameSessionState(...)`가 사라진다.
- `GameSessionTestFactory`만 FastUnit session creation entrypoint로 남는다.
- `NarrativeRuntimeBootstrap.LoadFromResources()`는 production public constructor와 BatchOnly/resource validation lane에서만 쓰인다.
- task `status.md`에 검증 결과와 deferred 항목이 기록된다.

## Deferred

- `026_content_snapshot_spec_fixtures_for_fast_tests`: `ScriptableObject.CreateInstance` 기반 fixture를 pure snapshot/spec fixture로 축소.
- `027_gamesession_phase2_ownership_migration`: remaining `*Core` body ownership을 session service object로 이동.
