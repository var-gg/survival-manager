# Battle Observer UI

- 상태: active
- 최종수정일: 2026-03-30
- 단계: prototype

## source-of-truth note

- overhead UI, HP bar, damage text의 현재 기준은 `docs/02_design/combat/battle-presentation-contract.md`를 우선한다.

## 목적

이 문서는 Battle scene을 사람이 5~10초 동안 관찰 가능한 수준으로 만드는 현재 observer UI 범위를 정리한다.

## 현재 표현 범위

- 좌측 아군 4슬롯, 우측 적 4슬롯 고정 배치
- capsule primitive actor
- actor 머리 위 screen-space overhead UI
- overhead UI / damage text / 팀 HP summary는 settings panel에서 ON/OFF
- 최근 로그 8줄
- tick / current action / speed / pause 상태 텍스트
- progress bar
- settings 버튼 + battle view settings panel
- continue 버튼

## 행동 피드백

- basic attack: anticipation -> 짧은 lunge -> impact hold -> 복귀
- damage: red flash + hit jolt + floating text
- heal: green pulse + floating text
- defend: blue pulse
- death: gray tint + scale down
- 현재 행동 actor는 ground shadow / overlay tint로 강조된다.

## 비목표

- animation clip 기반 연출
- 최종 HUD
- camera cut / shake
- 외부 VFX 패키지

## 운영 메모

- scene installer가 `BattlePresentationRoot`, `BattleStageRoot`, `ActorOverlayRoot`, `PauseButton`, `SettingsButton`, `SettingsPanel`, `ProgressFill`을 만든다.
- Quick Battle smoke는 Expedition 진행도를 건드리지 않고 Battle observer만 빠르게 확인한다.
- observer readability는 polished animation보다 상태 추적 가능성을 우선한다.
