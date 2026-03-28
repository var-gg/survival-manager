# Unity scene flow

- 상태: draft
- 최종수정일: 2026-03-29
- phase: prototype

## 목적

이 문서는 Unity adapter 계층의 scene flow 책임을 정리한다.

## 핵심 원칙

- 전투 truth는 scene script가 아니라 `SM.Combat` domain에 있다.
- save truth는 `SM.Persistence.Abstractions.PersistenceFacade`와 repository에 있다.
- MonoBehaviour와 scene script는 그 바깥 adapter로만 둔다.
- Boot scene이 시작점이며 여기서 Town 진입까지 이어져야 한다.

## 현재 adapter 구성

- `GameBootstrap`: Boot scene composition root
- `GameSessionRoot`: `DontDestroyOnLoad` 세션 루트
- `GameSessionState`: 현재 profile / roster / expedition / reward 상태 보관
- `SceneFlowController`: Boot/Town/Expedition/Battle/Reward 전환 담당
- `PersistenceEntryPoint`: environment -> `PersistenceConfig` -> repository 진입점
- `TownScreenController`: Town debug UI adapter
- `ExpeditionScreenController`: Expedition debug UI adapter
- `BattleScreenController`: Battle replay/view adapter
- `RewardScreenController`: Reward 선택 adapter
- `SceneNames`: 씬 이름 상수

## 시작 흐름

1. Boot scene 진입
2. `GameBootstrap`가 `GameSessionRoot` 보장
3. profile load/create
4. 샘플 콘텐츠 존재 확인
5. Town 이동
6. Town에서 원정 준비
7. Expedition에서 다음 전투 진입
8. Battle 결과 생성
9. Reward 선택
10. Town 복귀 및 저장

## scene 책임 범위

- scene script는 flow orchestration과 표시만 담당
- domain 계산은 `SM.Combat`, `SM.Meta`, `SM.Persistence.*`를 사용
- 새 placeholder scene 복제는 하지 않고 기존 `Boot/Town/Expedition/Battle/Reward`를 유지
