# Current Known Issues

## 반드시 먼저 실행할 메뉴 1개

- `SM/Bootstrap/Prepare Observer Playable`

- 상태: active
- 최종수정일: 2026-03-29
- phase: prototype

## known issues

- Battle은 아직 observer-grade replay 전 단계라 “무엇이 일어나는지”를 충분히 보여주지 못한다.
- Town / Expedition / Reward는 아직 operator-grade placeholder UI다.
- canonical sample content가 깨졌을 때는 seed regenerate가 필요할 수 있다.
- Battle observer-grade replay와 operator UI는 다음 블록 범위다.

## workaround

- canonical root가 비어 있거나 sample asset 값이 비정상적이면 `SM/Seed/Generate Sample Content`를 다시 실행한다.
- legacy sample content 흔적이 있으면 `SM/Seed/Migrate Legacy Sample Content`를 먼저 실행한다.
- scene asset만 다시 맞추려면 `SM/Bootstrap/Repair First Playable Scenes`를 실행한다.
