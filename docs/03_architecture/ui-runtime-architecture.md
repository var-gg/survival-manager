# UI 런타임 아키텍처

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-06
- 소스오브트루스: `docs/03_architecture/ui-runtime-architecture.md`
- 관련문서:
  - `docs/03_architecture/unity-project-layout.md`
  - `docs/03_architecture/dependency-direction.md`
  - `docs/03_architecture/unity-scene-flow.md`
  - `docs/02_design/ui/battle-observer-ui.md`
  - `docs/04_decisions/adr-0019-runtime-panel-host-ui-toolkit-first.md`

## 목적

prototype first playable에서 Town / Expedition / Battle / Reward UI를 어떤 구조로 고정할지 정의한다.
핵심 목표는 scene마다 흩어진 uGUI object tree를 계속 늘리지 않고, asset-first UITK 구조와 최소 runtime seam으로 수렴시키는 것이다.

## 현재 결정

- 기본 UI 기술은 `UI Toolkit first`다.
- 화면 진입점은 `RuntimePanelHost` 하나로 고정한다.
- 현재 backend는 `UIDocument`지만, presenter / controller / view state는 `UIDocument` 타입에 직접 의존하지 않는다.
- major navigation은 scene 단위 유지이며 canonical route는 계속 `SceneFlowController`다.
- 화면 패턴은 `View + Presenter + immutable ViewState`다.
- 범용 MVVM base, screen stack service, Addressables 확장은 이번 1차 범위에 넣지 않는다.

## 경로와 패키징

### UI asset 경로

- `Assets/_Game/UI/Foundation/**`
  - shared theme token
  - shared runtime theme USS
  - shared `PanelSettings`
- `Assets/_Game/UI/Screens/Town/**`
- `Assets/_Game/UI/Screens/Expedition/**`
- `Assets/_Game/UI/Screens/Battle/**`
- `Assets/_Game/UI/Screens/Reward/**`

### UI runtime 코드 경로

- `Assets/_Game/Scripts/Runtime/Unity/UI/**`
  - `RuntimePanelHost`
  - `RuntimePanelAssetRegistry`
  - screen별 presenter / view / view state

이번 단계에서는 asmdef를 새로 자르지 않고 `SM.Unity` 내부의 폴더와 namespace 경계로 먼저 고정한다.

## RuntimePanelHost 규칙

- `RuntimePanelHost`가 scene의 UI backend seam이다.
- host 책임:
  - shared `PanelSettings` 적용
  - visual tree load
  - root access 제공
  - focus handoff
  - visibility toggle
  - locale refresh 시 repaint 진입점
- temporary hide / show는 host GameObject enable / disable로 처리하지 않는다.
- modal / toast / overlay / debug panel은 `root.style.display` 기반으로 숨긴다.
- 필요 시 `visible`과 `pickingMode`를 보조로 쓴다.
- GameObject disable / destroy는 scene unload 또는 명시적 teardown에서만 허용한다.

이 규칙은 `UIDocument`의 enable / disable 재구성 비용과 state loss 리스크를 피하기 위한 것이다.

## Scene 계약

- `Boot`만 기존 최소 uGUI canvas를 유지한다.
- `Town` / `Expedition` / `Reward`는 `*RuntimeRoot` + `*RuntimePanelHost` + `*ScreenController`를 기본 scene contract로 둔다.
- `Battle`는 위 계약에 더해 다음 root를 유지한다.
  - `BattlePresentationRoot`
  - `BattleStageRoot`
  - `ActorOverlayCanvas`
  - `ActorOverlayRoot`
  - `BattleCameraRoot`

`FirstPlayableSceneInstaller`와 `FirstPlayableRuntimeSceneBinder`는 이제 전체 버튼/텍스트 tree 생성이 아니라 이 최소 runtime root 계약을 보장하는 역할만 가진다.

## Battle UI 경계

### UITK로 옮긴 범위

- battle log
- speed / pause / continue / rebattle / return town control
- settings panel
- timeline scrubber
- locale control

### 현행 유지 범위

- actor-follow overhead HP / nameplate / state
- floating text
- actor-follow camera-linked overlay

즉 battle은 `shell은 UITK`, `actor presentation은 기존 GameObject overlay`라는 혼합 구조를 유지한다.

## 성능 예외 규칙

actor-follow overlay를 후속 스프린트에서 UITK로 옮길 때만 아래 규칙을 강제한다.

- `absolute` positioning 사용
- 위치 갱신은 `left/top` 대신 `style.translate`
- moving element는 `UsageHints.DynamicTransform`
- transient widget은 pooling
- off-screen hide 필수
- layout-driven animation 금지

1차 구현에서는 이 영역을 억지로 UITK로 통합하지 않는다.

## 입력 / EventSystem 규칙

- 순수 UITK + legacy Input Manager만 쓰면 scene의 `EventSystem`이 필수는 아니다.
- 하지만 현재 repo는 `com.unity.inputsystem`을 사용하고, 전환 중 mixed UI mode가 남아 있다.
- 따라서 1차 구현과 테스트 계약에서는 `EventSystem + InputSystemUIInputModule`을 계속 provision한다.
- 문서 계약 표현은 “항상 required”가 아니라 “Input System 또는 mixed UI mode일 때 required”로 본다.

## 금지 규칙

- `SM.Combat`, `SM.Meta`, `SM.Persistence.*`가 `UIElements`, `UXML`, `USS` 자산을 직접 참조하지 않는다.
- view가 `GameSessionState` 외부 시스템이나 concrete persistence / network adapter를 직접 호출하지 않는다.
- 화면 간 전환을 screen끼리 직접 호출하지 않는다.
- “나중에 재사용할지도 모른다”는 이유만으로 Foundation 승격을 하지 않는다.

## acceptance

- Town / Expedition / Battle / Reward가 `RuntimePanelHost` 기반 visual tree로 동작한다.
- Battle shell은 UITK로 전환되지만 actor overhead / floating text는 기존 경로로 유지된다.
- locale 전환 시 raw key 대신 fallback 또는 localized text만 노출된다.
- scene repair와 scene integrity 테스트가 `Canvas + button tree`가 아니라 `runtime root + host + backend + named UXML contract`를 기준으로 검사한다.
