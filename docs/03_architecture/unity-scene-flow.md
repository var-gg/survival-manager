# Unity 씬 흐름

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-09
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
- `FirstPlayableBootstrap`는 fail-fast preflight와 scene open/play 진입만 담당한다.
- operator가 first playable을 보려면 `SM/Play/Full Loop`를 먼저 실행하는 흐름을 기본값으로 둔다.

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

1. editor에서 `SM/Play/Full Loop` 실행
2. canonical content와 Boot scene contract fail-fast preflight
3. Boot scene open
4. Play 후 `GameBootstrap`가 `GameSessionRoot`와 content/localization preflight를 보장
5. Boot에서 `Start Local Run` 대기
6. 내부적으로 `OfflineLocal` profile load/create
7. Town 이동
8. Expedition 진입
9. Battle 결과 생성
10. Reward 선택
11. Town 복귀 및 local save

## realm 전환 규칙

- Boot가 session realm 진입점이다.
- `Return to Start`로 Boot로 돌아갈 수 있지만 진행 중인 런에서는 막는다.
- Quick Battle과 direct-scene play는 tooling 안정성을 위해 `OfflineLocal`을 auto-start한다.
- `OnlineAuthoritative` 개념은 future seam으로 남기되, 현재 playable UI에서는 노출하지 않는다.

## Combat Sandbox direct 플로우

`SM/Play/Combat Sandbox` 메뉴는 위 흐름에서 Boot/Town을 우회하고 Battle 씬을 직접 연 뒤 Play로 진입한다.

1. `FirstPlayableBootstrap.PlayCombatSandbox()` 실행
2. active sandbox handoff / canonical content / Battle scene contract / compile 가능 여부를 fail-fast preflight
3. `EditorPrefs`에 `SM.QuickBattleRequested` 플래그 설정
4. Battle 씬 open
5. `EditorApplication.EnterPlaymode()` 자동 진입
6. `GameSessionRoot` direct-entry bootstrap이 플래그 소비
7. `PrepareCombatSandboxDirect()` + Battle sandbox 시작
8. Battle 씬에서 same-seed replay / new seed / exit sandbox 가능

## Town integration smoke 플로우

Town의 `Quick Battle (Smoke)`는 direct sandbox와 다르게 현재 Town 상태를 들고 integration smoke를 수행한다.

1. `SM/Play/Full Loop`로 Boot/Town 진입
2. Town secondary CTA `Quick Battle (Smoke)` 클릭
3. `PrepareTownQuickBattleSmoke()`로 현재 profile/deploy/posture를 유지한 채 Battle 진입
4. Battle 종료 후 `Continue -> Reward` 또는 `Return to Town (Debug)`
5. transient overlay를 정리하고 canonical Town 상태 복구

## GameSessionRoot.EnsureInstance 패턴

모든 ScreenController는 `GameSessionRoot.Instance!` 대신 `GameSessionRoot.EnsureInstance()`를 사용한다. Boot가 아닌 씬을 직접 Play하면 `OfflineLocal` session까지 자동으로 시작해 기존 tooling을 깨지 않게 유지한다.
