# ADR-0019: RuntimePanelHost 기반 UI Toolkit first 채택

- 상태: accepted
- 날짜: 2026-04-06

## 컨텍스트

first playable UI는 Town / Expedition / Battle / Reward마다 scene 안에서 uGUI object tree를 직접 만들고 binder로 다시 연결하는 구조였다.
이 구조는 초반 vertical slice 검증에는 빠르지만, 화면 자산 재사용, locale 대응, scene repair 계약, 전투 shell 확장에 불리했다.

동시에 다음 제약이 있다.

- prototype 단계이므로 asmdef와 abstraction을 한 번에 과도하게 늘리면 안 된다.
- major navigation은 이미 scene 단위 flow와 `SceneFlowController`를 기준으로 정착해 있다.
- Battle의 actor-follow overhead / floating text는 매 프레임 위치 갱신이 많아 성능 리스크가 있다.

## 결정

- Town / Expedition / Battle / Reward UI는 `UI Toolkit first`로 전환한다.
- 화면 진입 seam은 `RuntimePanelHost` 하나로 고정한다.
- 현재 Unity `6000.4.0f1` 기준 backend는 `UIDocument`를 사용한다.
- presenter / controller / view state는 backend 타입에 직접 의존하지 않아서, 추후 `Panel Renderer` 계열로 교체 가능하도록 둔다.
- major navigation은 scene 단위 유지이며 `SceneFlowController`를 canonical route로 유지한다.
- 화면 구조는 `View + Presenter + immutable ViewState`를 기본으로 한다.
- 공통 자산은 `Assets/_Game/UI/Foundation/**`, 화면 자산은 `Assets/_Game/UI/Screens/{Town,Expedition,Battle,Reward}/**`에 둔다.
- Battle은 `shell만 UITK`로 옮기고, actor-follow overhead / floating text는 기존 `BattlePresentationController` / `BattleActorView` 경로를 유지한다.
- 임시 show / hide는 GameObject enable / disable 대신 runtime panel root의 `display` 토글을 기본으로 한다.
- 이번 1차에서는 UI 전용 Addressables 확장, screen stack service, 범용 MVVM base, asmdef 재절단을 도입하지 않는다.

## 결과

### 장점

- scene repair 계약이 `runtime root + host + backend + named UXML contract`로 단순화된다.
- screen controller가 더 이상 개별 `Text/Button/Image` 필드를 대량으로 소유하지 않는다.
- locale / theme / style 자산이 scene YAML이 아니라 asset-first 구조로 옮겨간다.
- future backend swap을 `RuntimePanelHost` 내부로 국소화할 수 있다.

### 비용

- Battle은 한동안 UITK shell + uGUI/3D actor overlay 혼합 구조를 유지한다.
- `SM.Unity` 내부에 UI runtime 코드가 더 많아지므로, 후속 단계에서 asmdef 재절단 여부를 다시 판단해야 한다.
- scene direct play 경로에서는 locale 초기화 이전 fallback text가 잠깐 보일 수 있다.

### 후속 규칙

- actor-follow overlay를 나중에 UITK로 옮길 경우 `absolute + translate + DynamicTransform + pooling + off-screen hide` 규칙을 강제한다.
- EventSystem 계약은 “항상 required”가 아니라 “Input System 또는 mixed UI mode에서 required”로 유지한다.
