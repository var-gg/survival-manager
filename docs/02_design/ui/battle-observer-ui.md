# Battle Observer UI

- 상태: active
- 최종수정일: 2026-04-05
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
- 타임라인 스크러버 (드래그 가능한 progress bar)
- settings 버튼 + battle view settings panel
- continue 버튼

## 타임라인 플레이백

- `BattleTimelineController`가 시뮬레이션 스텝을 전수 녹화한다 (최대 300개).
- 스크러버 드래그로 임의 시점 탐색이 가능하다.
- 뒤로 탐색은 녹화된 스텝 인덱싱으로 즉시 수행한다.
- 앞으로 탐색은 시뮬레이터를 추가 진행하여 스텝을 채운다.
- 전투 종료 후 리플레이: 스크러버로 시작점에서 재생. 새 전투(rebattle)와는 별개 기능이다.

### 플레이백 모드

| 모드 | pause | seek | speed | replay |
|------|-------|------|-------|--------|
| QuickBattle | 상시 | 상시 | 상시 | 상시 |
| InGame | 종료 후 | 종료 후 | 종료 후 | 종료 후 |

- `BattlePlaybackPolicy`가 모드별 제어 가능 여부를 판단한다.
- QuickBattle: 전투 중에도 모든 플레이백 조작 가능.
- InGame: 전투 완료 후에만 모든 조작 해금.

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
