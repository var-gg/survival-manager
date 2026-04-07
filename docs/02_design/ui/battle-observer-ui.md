# Battle Observer UI

- 상태: active
- 최종수정일: 2026-04-07
- 단계: prototype

## source-of-truth note

- overhead UI, HP bar, damage text의 현재 기준은 `docs/02_design/combat/battle-presentation-contract.md`를 우선한다.

## 목적

이 문서는 Battle scene을 사람이 5~10초 동안 관찰 가능한 수준으로 만드는 현재 observer UI 범위를 정리한다.

## 현재 표현 범위

- battle shell은 `RuntimePanelHost + UITK` 기준으로 렌더
- 좌측 아군 / 우측 적 observer 레이아웃
- capsule primitive actor
- actor 머리 위 screen-space overhead UI
- overhead UI / damage text / 팀 summary는 settings panel에서 ON/OFF
- team summary는 per-unit dump가 아니라 aggregate(`alive/total | current/max HP`)다
- 최근 로그는 내부적으로 더 유지해도 normal lane 표시는 compact 5줄 기준이다
- status headline은 `Step 042 | Rowan Skill -> Ghoul Brute | Allies pressing` 수준의 한 줄 요약을 우선한다
- 타임라인 스크러버 (UITK progress track)
- explicit `Replay` 버튼
- settings 버튼 + battle view settings panel
- continue 버튼
- selected tactical card

## 타임라인 플레이백

- `BattleTimelineController`가 시뮬레이션 스텝을 전수 녹화한다 (최대 300개).
- 스크러버 드래그로 임의 시점 탐색이 가능하다.
- 뒤로 탐색은 녹화된 스텝 인덱싱으로 즉시 수행한다.
- 앞으로 탐색은 시뮬레이터를 추가 진행하여 스텝을 채운다.
- `Replay`: 같은 recorded timeline rewind.
- `Rebattle`: 새 seed로 새 timeline 생성.
- `RestartSameSeed`: debug/dev shortcut으로 유지.

### 플레이백 모드

- `QuickBattle`: `pause`, `seek`, `speed`, `replay`를 모두 상시 허용한다.
- `InGame`: 전투 종료 후에만 `pause`, `seek`, `speed`, `replay`를 허용한다.

- `BattlePlaybackPolicy`가 모드별 제어 가능 여부를 판단한다.
- QuickBattle: 전투 중에도 모든 플레이백 조작 가능.
- InGame: 전투 완료 후에만 모든 조작 해금.

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

## 운영 메모

- scene installer가 `BattleRuntimeRoot`, `BattleRuntimePanelHost`, `BattlePresentationRoot`, `BattleStageRoot`, `ActorOverlayCanvas`, `ActorOverlayRoot`, `BattleCameraRoot`를 만든다.
- `PauseButton`, `ReplayButton`, `RebattleButton`, `SettingsPanel`, `ProgressFill` 같은 화면 요소 이름 계약은 `BattleScreen.uxml`에 저장된다.
- selected unit은 `Tab` cycle과 좌클릭 pick 둘 다 허용한다.
- Quick Battle smoke는 Expedition 진행도를 건드리지 않고 Battle observer만 빠르게 확인한다.
- observer readability는 polished animation보다 상태 추적 가능성을 우선한다.
