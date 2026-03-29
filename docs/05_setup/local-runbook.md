# Local Runbook

## 반드시 먼저 실행할 메뉴 1개

- `SM/Bootstrap/Prepare Observer Playable`

- 상태: active
- 최종수정일: 2026-03-29
- phase: prototype

## 현재 기준

- canonical sample content root를 `Assets/Resources/_Game/Content/Definitions/**`로 고정했다.
- stale `SampleScene.unity` smoke 계약을 제거했다.
- `SM/Bootstrap/Repair First Playable Scenes`가 scene asset 복구의 공식 메뉴다.
- `SM/Bootstrap/Prepare Observer Playable`가 content + validation + scene repair + Boot open을 한 번에 수행한다.

## 빠른 검증 절차

1. Unity 열기
2. `SM/Bootstrap/Prepare Observer Playable`
3. `Boot.unity`가 열렸는지 확인
4. Play
5. 필요하면 EditMode tests 실행

## known issues

- Town / Expedition / Reward UI는 아직 debug placeholder UGUI다.
- Battle observer-grade replay와 operator UI는 아직 다음 블록 범위다.
- README와 PlayMode smoke 정리는 아직 다음 블록 범위다.

## workaround

- canonical root가 비어 있거나 sample asset 값이 비정상적이면 `SM/Seed/Generate Sample Content`를 다시 실행한다.
- legacy path 흔적이 의심되면 `SM/Seed/Migrate Legacy Sample Content`를 먼저 실행한다.
- scene만 다시 맞추고 싶으면 `SM/Bootstrap/Repair First Playable Scenes`만 단독 실행한다.
