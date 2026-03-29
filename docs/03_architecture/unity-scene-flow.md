# Unity 씬 흐름

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-29
- 소스오브트루스: `docs/03_architecture/unity-scene-flow.md`
- 관련문서:
  - `docs/03_architecture/unity-boundaries.md`
  - `docs/03_architecture/unity-project-layout.md`
  - `docs/05_setup/scene-repair-bootstrap.md`

## 목적

이 문서는 Unity adapter 계층의 scene flow 책임을 정리한다.

## 핵심 원칙

- 전투 truth는 scene script가 아니라 `SM.Combat`에 있다.
- save truth는 `SM.Persistence.Abstractions` 뒤에 있다.
- `MonoBehaviour`와 scene script는 orchestration과 표시만 담당한다.
- Boot scene이 시작점이며 composition root 역할을 맡는다.
- scene asset은 수동 편집 결과보다 installer 기반 재현 가능 구성을 우선한다.

## 현재 adapter 구성

- `GameBootstrap`
- `GameSessionRoot`
- `GameSessionState`
- `SceneFlowController`
- `PersistenceEntryPoint`
- `TownScreenController`
- `ExpeditionScreenController`
- `BattleScreenController`
- `RewardScreenController`
- `SceneNames`
- `FirstPlayableSceneInstaller`
- `FirstPlayableBootstrap`

## bootstrap 책임

- `FirstPlayableSceneInstaller`는 playable scene asset 복구와 build settings 보정을 담당한다.
- `FirstPlayableBootstrap`는 sample content 보장, validation, scene repair, demo save reset, Boot open을 순서대로 orchestration 한다.
- operator가 first playable을 보려면 `SM/Bootstrap/Prepare Observer Playable`를 먼저 실행하는 흐름을 기본값으로 둔다.

## 현재 시작 흐름

1. editor에서 `SM/Bootstrap/Prepare Observer Playable` 실행
2. sample content 보장 및 validation
3. first playable scene repair + build settings 보정
4. Boot scene open
5. Play 후 `GameBootstrap`가 `GameSessionRoot` 보장
6. sample content 확인
7. profile load/create
8. Town 이동
9. Expedition 진입
10. Battle 결과 생성
11. Reward 선택
12. Town 복귀 및 저장
