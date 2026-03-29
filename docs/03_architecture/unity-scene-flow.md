# Unity 씬 흐름

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-29
- 소스오브트루스: `docs/03_architecture/unity-scene-flow.md`
- 관련문서:
  - `docs/03_architecture/unity-boundaries.md`
  - `docs/03_architecture/unity-project-layout.md`
  - `docs/05_setup/first-playable-bootstrap.md`

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

## 현재 시작 흐름

1. Boot scene 진입
2. `GameBootstrap`가 `GameSessionRoot` 보장
3. sample content 확인
4. profile load/create
5. Town 이동
6. Expedition 진입
7. Battle 결과 생성
8. Reward 선택
9. Town 복귀 및 저장
