# Unity 씬 흐름

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-07
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
- play scene UI는 scene object tree보다 `RuntimePanelHost + UXML asset`을 기준으로 본다.

## 현재 adapter 구성

- `GameBootstrap`
- `GameSessionRoot`
- `GameSessionState`
- `SceneFlowController`
- `RuntimePanelHost`
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
- `FirstPlayableSceneInstaller`는 Boot에서 realm 선택용 uGUI canvas를, 그 외 play scene에서는 `*RuntimeRoot`, `*RuntimePanelHost`, `*ScreenController`, Battle overlay root를 보장한다.
- `FirstPlayableBootstrap`는 sample content 보장, validation, scene repair, demo save reset, Boot open을 순서대로 orchestration 한다.
- operator가 first playable을 보려면 `SM/Setup/Prepare Observer Playable`를 먼저 실행하는 흐름을 기본값으로 둔다.

## scene별 UI runtime 계약

- `Town` / `Expedition` / `Reward`
  - `*RuntimeRoot`
  - `*RuntimePanelHost`
  - `*ScreenController`
- `Battle`
  - `BattleRuntimeRoot`
  - `BattleRuntimePanelHost`
  - `BattleScreenController`
  - `BattlePresentationRoot`
  - `BattleStageRoot`
  - `ActorOverlayCanvas`
  - `ActorOverlayRoot`
  - `BattleCameraRoot`

major navigation은 계속 scene 단위로 유지하고, scene 내부 modal / toast / HUD shell만 runtime panel 안에서 처리한다.

## 현재 시작 흐름

1. editor에서 `SM/Setup/Prepare Observer Playable` 실행
2. sample content 보장 및 validation
3. first playable scene repair + build settings 보정
4. Boot scene open
5. Play 후 `GameBootstrap`가 `GameSessionRoot`와 content/localization preflight를 보장
6. Boot에서 `Start Local Run` 대기
7. 내부적으로 `OfflineLocal` profile load/create
8. Town 이동
9. Expedition 진입
10. Battle 결과 생성
11. Reward 선택
12. Town 복귀 및 local save

## realm 전환 규칙

- Boot가 session realm 진입점이다.
- `Return to Start`로 Boot로 돌아갈 수 있지만 진행 중인 런에서는 막는다.
- Quick Battle과 direct-scene play는 tooling 안정성을 위해 `OfflineLocal`을 auto-start한다.
- `OnlineAuthoritative` 개념은 future seam으로 남기되, 현재 playable UI에서는 노출하지 않는다.

## Quick Battle 바이패스 플로우

`SM/Quick Battle` 메뉴는 위 흐름에서 Boot/Town을 우회하고 Battle 씬을 직접 연 뒤 Play로 진입한다.

1. `FirstPlayableBootstrap.QuickBattleOneClick()` 실행
2. 기존 setup 6단계 + QuickBattleConfig 에셋 보장
3. `EditorPrefs`에 `SM.QuickBattleRequested` 플래그 설정
4. Battle 씬 open
5. `EditorApplication.EnterPlaymode()` 자동 진입
6. `BattleScreenController` direct-entry bootstrap이 플래그 소비
7. `PrepareQuickBattleSmoke()` + Battle smoke 시작
8. Battle 씬에서 Re-battle / Return Town 가능

## GameSessionRoot.EnsureInstance 패턴

모든 ScreenController는 `GameSessionRoot.Instance!` 대신 `GameSessionRoot.EnsureInstance()`를 사용한다. Boot가 아닌 씬을 직접 Play하면 `OfflineLocal` session까지 자동으로 시작해 기존 tooling을 깨지 않게 유지한다.
