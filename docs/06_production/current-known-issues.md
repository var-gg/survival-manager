# Current Known Issues

## 반드시 먼저 실행할 메뉴 1개

- `SM/Seed/Generate Sample Content`

- 상태: active
- 최종수정일: 2026-03-29
- phase: prototype

## known issues

- canonical content contract는 고정했지만 scene repair는 아직 temporary bridge 상태다.
- Scene integrity 검증은 block24 동안 `FirstPlayableSceneInstaller` temporary bridge를 사용한다.
- Boot/Town/Battle live component query 정합성은 아직 일관되지 않으며, block24는 scene asset text contract를 우선 보장한다.
- manual observer playable 인증은 아직 완료되지 않았다.
- Battle observer-grade replay와 operator UI는 다음 블록 범위다.

## workaround

- canonical root가 비어 있으면 `SM/Seed/Generate Sample Content`를 다시 실행한다.
- legacy sample content 흔적이 있으면 `SM/Seed/Migrate Legacy Sample Content`를 먼저 실행한다.
- local WIP scene bridge까지 같이 검사할 때만 `SM/Bootstrap/Prepare First Playable`를 임시 사용한다.
