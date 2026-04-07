# Battle Observer UI

- 상태: active
- 최종수정일: 2026-04-08
- 단계: prototype

## source-of-truth note

- overhead UI, HP bar, damage text의 현재 기준은 `docs/02_design/combat/battle-presentation-contract.md`를 우선한다.

## 목적

이 문서는 Battle scene을 사람이 5~10초 동안 관찰 가능한 수준으로 만드는 현재 observer UI 범위를 정리한다.

## 현재 표현 범위

- battle shell은 `RuntimePanelHost + UITK` 기준으로 렌더
- 좌측 아군 / 우측 적 observer 레이아웃
- 가운데 summary panel은 `result / current state / progress / action groups`를 묶어 보여 준다
- `BattleActorWrapper` 기반 actor presentation
- pre-art baseline은 primitive wrapper adapter를 사용
- actor 머리 위 screen-space overhead UI
- overhead UI / damage text / 팀 summary는 settings panel `Display` 섹션에서 ON/OFF
- debug overlay는 settings panel `Debug` 섹션에서만 노출한다
- team summary는 per-unit dump가 아니라 aggregate(`alive/total | current/max HP`)다
- 최근 로그는 내부적으로 더 유지해도 normal lane 표시는 compact 5줄 기준이다
- status headline은 `Step 042 | Rowan Skill -> Ghoul Brute | Allies pressing` 수준의 한 줄 요약을 우선한다
- 타임라인 progress track은 항상 보이지만 authored lane에서는 정보 표면만 담당한다
- `Continue`는 authored lane의 유일한 primary CTA다
- playback group / smoke group / utility group을 분리한다
- settings 버튼 + battle view settings panel
- selected tactical card

## 타임라인 플레이백

- `BattleTimelineController`가 시뮬레이션 스텝을 전수 녹화한다 (최대 300개).
- Quick Battle smoke에서만 스크러버 드래그, replay, rebattle, restart-same-seed를 노출한다.
- authored Expedition battle은 recorded timeline을 내부적으로 가져도 player-facing surface에서는 `Continue` 중심 관찰 UX로 고정한다.

### 플레이백 모드

- `QuickBattle`: `pause`, `seek`, `speed`, `replay`를 모두 상시 허용한다.
- `InGame`: 전투 종료 후에만 `pause`, `seek`, `speed`, `replay`를 허용한다.

- `BattlePlaybackPolicy`가 모드별 제어 가능 여부를 판단한다.
- current shipping shell에서는 authored lane가 `InGame` policy를 써도 playback affordance를 기본 표면에 노출하지 않는다.
- current convergence:
  - authored expedition battle -> `BattlePlaybackMode.InGame`
  - quick battle smoke -> `BattlePlaybackMode.QuickBattle`

## 행동 피드백

- basic attack: 짧은 lunge + current target line
- damaging skill: stronger source accent + target line
- heal/support: green source/target pulse
- defend/hold: guard posture + guard surface
- reposition: home tether + 이동 lean
- death/down: gray collapse
- current actor / current target / selected unit surface가 normal lane의 핵심이며, full telemetry는 F3 debug에 남긴다.

## 비목표

- animation clip 기반 연출
- 최종 HUD
- camera cut / shake
- 외부 VFX 패키지
- vendor prefab 내부 world-space HUD를 source-of-truth로 쓰는 방식

## 운영 메모

- scene installer가 `BattleRuntimeRoot`, `BattleRuntimePanelHost`, `BattlePresentationRoot`, `BattleStageRoot`, `ActorOverlayCanvas`, `ActorOverlayRoot`, `BattleCameraRoot`를 만든다.
- actor wrapper는 `BattlePresentationRoot` 하위에만 spawn되고, localized overhead는 wrapper의 `HudAnchor`에 붙는다.
- `PlaybackActionsGroup`, `SmokeActionsGroup`, `SettingsPanel`, `ProgressFill` 같은 화면 요소 이름 계약은 `BattleScreen.uxml`에 저장된다.
- selected unit은 `Tab` cycle과 좌클릭 pick 둘 다 허용한다.
- Quick Battle smoke는 Expedition 진행도를 건드리지 않고 Battle observer만 빠르게 확인한다.
- observer readability는 polished animation보다 상태 추적 가능성을 우선한다.
