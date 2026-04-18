# SM.Meta/Session boundary refactor 명세

## 메타데이터

- 작업명: SM.Meta/Session boundary refactor
- 담당: Codex
- 상태: completed
- 최종수정일: 2026-04-19
- 관련경로:
  - `Assets/_Game/Scripts/Runtime/Core/Content/`
  - `Assets/_Game/Scripts/Runtime/Meta/`
  - `Assets/_Game/Scripts/Runtime/Unity/ContentConversion/`
  - `Assets/_Game/Scripts/Runtime/Unity/Session/`
  - `Assets/Tests/EditMode/BuildCompileAuditTests.cs`
- 관련문서:
  - `docs/03_architecture/dependency-direction.md`
  - `docs/03_architecture/assembly-boundaries-and-persistence-ownership.md`
  - `docs/04_decisions/adr-0023-meta-content-adapter-boundary.md`

## Goal

- `SM.Meta`를 authored `ScriptableObject`/`SM.Content` 직접 의존에서 분리하고, `GameSessionState`를 session facade로 낮춘 뒤 asmdef/test/docs guard로 재퇴행을 막는다.

## Authoritative boundary

- 이번 sprint의 단일 migration axis는 `SM.Content` authored schema와 `SM.Meta` runtime/domain rule 사이의 직접 참조 제거다.
- content schema enum의 source-of-truth는 `SM.Core.Content`로 이동한다.
- ScriptableObject-to-runtime 변환은 `SM.Unity.ContentConversion`과 bootstrap에서 수행한다.
- 이번 task는 save record 대량 rename, scene/prefab authoring, 신규 asset 생성은 닫지 않는다.

## In scope

- `SM.Core.Content` content schema enum 추가
- `SM.Meta` pure model/spec 입력으로 narrative, passive, reward, encounter, loot 경계 보정
- `SM.Meta`, `SM.Meta.Serialization`, persistence asmdef 참조 정리
- `SM.Tests.PlayMode -> SM.Editor` 참조 제거
- `GameSessionState.cs` 파일 책임 축소와 `Assets/_Game/Scripts/Runtime/Unity/Session/` 내부 흐름 파일 분리
- asmdef/source guard 테스트 추가
- architecture 문서와 ADR 갱신

## Out of scope

- scene, prefab, component, asset authoring
- `SM.Meta`의 `*Record` 타입 대량 rename
- `ICombatContentLookup` 이동
- save 파일 포맷 재작성
- PlayMode smoke의 씬 기반 재구성

## asmdef impact

- `SM.Meta`: `SM.Core`, `SM.Combat`만 참조하고 `noEngineReferences: true`로 고정한다.
- `SM.Meta.Serialization`: `SM.Core`, `SM.Combat`, `SM.Meta`만 참조하고 `noEngineReferences: true`로 고정한다.
- `SM.Persistence.Abstractions`: `SM.Content` 참조를 제거한다.
- `SM.Persistence.Json`: `SM.Content` 참조를 제거한다.
- `SM.Tests.PlayMode`: `SM.Editor` 참조를 제거한다.
- cycle 위험은 `SM.Core.Content`를 shared schema 위치로 삼아 차단한다.

## persistence impact

- persistence contract ownership은 `SM.Persistence.Abstractions.Models`에 유지한다.
- `SM.Meta`의 `*Record` 명명은 이번 task에서 보존한다. 단, 문서상 의미는 immutable domain/read model로 제한한다.
- 저장 adapter는 여전히 `SM.Persistence.Json`이 맡고, `SM.Meta`는 repository concrete를 알지 않는다.

## validator / test oracle

- `BuildCompileAuditTests`에 asmdef guard, source guard, `GameSessionState.cs` line budget guard를 추가한다.
- focused tests는 pure Meta 모델을 쓰도록 갱신한다.
- 기본 oracle은 `test-batch-fast`, `tools/test-harness-lint.ps1`, docs policy/check/smoke다.

## done definition

- `SM.Meta` 소스에서 `using SM.Content`, `UnityEngine`, `UnityEditor`가 검색되지 않는다.
- `SM.Meta.asmdef`가 `SM.Core`, `SM.Combat`만 참조한다.
- `GameSessionState.cs`가 2,500줄 이하로 유지된다.
- `test-batch-fast`와 test harness lint가 통과한다.
- architecture docs, ADR, task status에 evidence를 남긴다.

## deferred

- `SM.Meta`의 `*Record` 타입 rename은 별도 save/domain naming sprint로 넘긴다.
- `ICombatContentLookup` 위치 이동은 Unity content lookup boundary가 더 안정된 뒤 검토한다.
- `GameSessionState` partial 내부 흐름을 완전한 독립 service 객체로 더 쪼개는 작업은 후속 구조 개선으로 남긴다.
