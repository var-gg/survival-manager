# 기술 개요

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-21
- 소스오브트루스: `docs/03_architecture/technical-overview.md`
- 관련문서:
  - `docs/03_architecture/index.md`
  - `docs/03_architecture/coding-principles.md`
  - `docs/03_architecture/dependency-direction.md`
  - `docs/03_architecture/unity-boundaries.md`
  - `docs/03_architecture/unity-project-layout.md`

## 목적

이 문서는 현재 prototype 구현이 어떤 기술 경계를 전제로 하는지 요약한다.
상세 규칙은 개별 기준 문서로 분리하고, 이 문서는 상위 지도를 제공한다.

## 현재 구조 요약

프로젝트는 다음 네 층을 분리하는 것을 기본 선택으로 둔다.

- authored content
- pure runtime/domain rule
- persistence abstraction과 저장 어댑터
- Unity orchestration/presentation

현재 build-to-battle 흐름은 아래 다섯 단계 파이프라인을 기준으로 본다.

- content definitions
- instance growth
- battle compile
- deterministic battle simulation
- replay / ledger / audit

## 실제 asmdef 기준

현재 저장소의 주요 asmdef는 아래와 같다.

- `SM.Core`
- `SM.Content`
- `SM.Combat`
- `SM.Meta`
- `SM.Meta.Serialization`
- `SM.Persistence.Abstractions`
- `SM.Persistence.Json`
- `SM.Unity`
- `SM.Editor`
- `SM.Tests.FastUnit`
- `SM.Tests.EditMode`
- `SM.Tests.EditMode.Integration`
- `SM.Tests.PlayMode`

문서에서는 네 테스트 asmdef를 묶어 `SM.Tests` 테스트 어셈블리 그룹이라고 부른다.

## 기본 구조 원칙

- core rule은 `SM.Core`, `SM.Combat`, `SM.Meta`에 둔다.
- 콘텐츠 schema enum은 `SM.Core.Content`에 두고, 콘텐츠 정의와 authored asset 규칙은 `SM.Content`와 `Assets/_Game/Content/**`를 기준으로 둔다.
- `SM.Meta`는 `SM.Content` authored definition을 직접 참조하지 않고 pure spec/template/snapshot만 소비한다.
- persistence truth는 `SM.Persistence.Abstractions` 뒤에 두고 구현은 adapter로 분리한다.
- `SM.Unity`는 scene/input/view orchestration과 authored-to-runtime conversion을 담당한다.
- `SM.Editor`는 bootstrap, validation, authoring 지원만 담당한다.

## 순수 런타임 어셈블리

아래 asmdef는 UnityEngine 참조 없이 유지한다.

- `SM.Core`
- `SM.Combat`
- `SM.Meta`
- `SM.Meta.Serialization`
- `SM.Persistence.Abstractions`

`BuildBoundaryGuardFastTests`는 위 pure asmdef의 `noEngineReferences=true`와 핵심 참조 allowlist를 고정한다. `SM.Meta.Serialization`은 `SM.Core`, `SM.Combat`, `SM.Meta`만, `SM.Persistence.Abstractions`는 `SM.Core`, `SM.Meta`만 참조해야 한다.

`SM.Content`는 authored `ScriptableObject` 정의를 포함하므로 Unity-bound assembly로 본다.
`SM.Unity.ContentConversion`은 `SM.Content` definition을 `SM.Meta` pure spec/model로 바꾸는 composition boundary다. production narrative resource bootstrap은 `SM.Unity` 내부 `GameSessionRuntimeBootstrapProvider`가 소유하며, FastUnit은 `GameSessionTestFactory`와 empty bootstrap/fake lookup 경로를 사용한다.

## 어떤 문서를 먼저 봐야 하는가

- 클래스/파일 분리 기준이 필요하면 `coding-principles.md`
- asmdef 참조를 결정해야 하면 `dependency-direction.md`
- `MonoBehaviour`나 scene script 책임이 헷갈리면 `unity-boundaries.md`
- context 책임이 헷갈리면 `bounded-contexts.md`
- 데이터 모델 분리가 필요하면 `data-model.md`

## 현재 구현 범위

- 게임 코드와 씬: `Assets/_Game/**`
- sample content 로딩 자산: `Assets/Resources/_Game/**`
- 테스트: `Assets/Tests/**`
- 벤더 원본 보호 구역: `Assets/ThirdParty/**`
