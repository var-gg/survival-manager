# 전투 표시 계약

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-06
- 소스오브트루스: `docs/02_design/combat/battle-presentation-contract.md`
- 관련문서:
  - `docs/02_design/combat/combat-readability.md`
  - `docs/02_design/ui/battle-observer-ui.md`
  - `docs/03_architecture/combat-runtime-architecture.md`
  - `docs/03_architecture/localization-runtime-and-content-pipeline.md`

## 목적

prototype 전투에서 아트 유무와 무관하게 읽히는 머리 위 정보 표시 규칙을 고정한다.

## 기본 원칙

- overhead UI는 `screen-space overlay/camera` 기준으로만 표시한다.
- `HeadAnchor`는 actor root 아래의 authored 또는 runtime-generated anchor다.
- nameplate, health bar, floating combat text는 actor 회전, pitch, roll을 상속하지 않는다.
- 발밑 ring, AOE telegraph, 범위 preview만 world-space를 사용한다.

## 표시 구성

- nameplate: actor 이름만 표시한다.
- health bar: screen-space bar로 표시한다.
- action/state line:
  `Acquire`, `Approach`, `Secure`, `Action`, `Break`, `Recover`,
  `Reposition` 같은 현재 phase를 짧게 노출한다.
- floating combat text: heal/damage만 표시하고, debug raw string은 금지한다.

## 가시성 규칙

- `LateUpdate`에서 `Camera.WorldToScreenPoint(HeadAnchor.position)`로 배치한다.
- off-screen 또는 camera 뒤에 있으면 숨긴다.
- full HP 잡몹 숨김은 후속 옵션이지만, 현재 baseline은 bar를 계속 보여도 된다.
- actor dead 상태는 유지하되, color/tint로 상태만 낮춘다.

## localization 규칙

- `ui.battle.*` missing key는 화면에 raw key로 노출하지 않는다.
- dev/editor에서는 console warning으로만 남기고, 화면에는 fallback label을 쓴다.
- `RuntimePanelHost` 기반 battle shell presenter와 actor overlay runtime은 동일한 fallback 원칙을 따른다.

## pooling 규칙

- floating combat text는 actor-local effect가 아니라
  presenter-owned overlay element로 본다.
- 현재 구현은 경량 coroutine 기반이지만, contract상 poolable widget으로 취급한다.

## acceptance

- overhead UI가 더 이상 3D 물체처럼 기울어지지 않는다.
- damage/heal text가 actor 회전과 함께 돌지 않는다.
- `ui.battle.*` 누락 시 fallback만 보이고 raw key는 보이지 않는다.
