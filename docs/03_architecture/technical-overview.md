# 기술 개요

- 상태: draft
- 최종수정일: 2026-03-29
- phase: prototype

## 목적

이 문서는 현재 prototype 구현을 기준으로 MVP 기술 구조 방향을 정리한다.
목표는 씬 스크립트에 모든 규칙을 하드코딩하지 않으면서, 작은 playable slice를 빠르게 검증할 수 있는 구조를 유지하는 것이다.

## MVP 구조 원칙

### 아키텍처 방향

프로젝트는 다음을 분리한다.

- 콘텐츠 정의
- 런타임 인스턴스
- persistence 상태
- Unity adapter / presentation layer

### 핵심 경계 규칙

- definition과 instance는 분리된 개념이다.
- 콘텐츠 정의의 기준은 Unity asset이다.
- 런타임/저장 상태는 별도 persistence 모델에 둔다.
- 전투 시뮬레이션은 UnityEngine 결합을 최소화한 순수 C# 영역에 둔다.
- DB, UI, Scene은 core domain 바깥 adapter다.
- direct production DB access는 전제하지 않는다.
- Boot scene은 composition root 역할만 수행한다.
- save truth는 `PersistenceFacade`를 통한다.

## 현재 코드 기준 asmdef/namespace 정규화

현재 코드 기준 주요 asmdef는 다음과 같다.

- `SM.Core`
- `SM.Combat`
- `SM.Meta`
- `SM.Content`
- `SM.Persistence.Abstractions`
- `SM.Persistence.Json`
- `SM.Persistence.Postgres`
- `SM.Unity`
- `SM.Editor`
- `SM.Tests`

현재 및 제안 namespace 방향도 `SM.*` 계열로 정규화한다.

- `SM.Core.*`
- `SM.Combat.*`
- `SM.Meta.*`
- `SM.Content.*`
- `SM.Persistence.*`
- `SM.Unity.*`
- `SM.Editor.*`
- `SM.Tests.*`

## Unity adapter 계층

현재 Unity adapter는 `Assets/_Game/Scripts/Runtime/Unity/**` 아래에 둔다.

주요 책임:

- `GameBootstrap`: Boot composition root
- `GameSessionRoot`: `DontDestroyOnLoad` root
- `GameSessionState`: 현재 profile / roster / expedition 상태 보관
- `SceneFlowController`: 씬 전환 담당
- `PersistenceEntryPoint`: JSON fallback 기본 진입점

이 계층은 orchestration만 담당하고, 전투/메타/save truth 자체를 재정의하지 않는다.

## 현재 저장소 기준 구현 범위

- 게임 구현: `Assets/_Game/**`
- 테스트: `Assets/Tests/**`

## 장기 확장 지점

- richer authoring validation
- save migration versioning
- alternative persistence adapters
- telemetry and replay adapters
- scene/controller layer stabilization
