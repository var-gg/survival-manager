# Local Runbook

## 반드시 먼저 실행할 메뉴 1개

- `SM/Seed/Generate Sample Content`

- 상태: active
- 최종수정일: 2026-03-29
- phase: prototype

## block24에서 먼저 고정한 것

- canonical sample content root를 `Assets/Resources/_Game/Content/Definitions/**`로 고정했다.
- stale `SampleScene.unity` smoke 계약을 제거했다.
- Boot/Town/Battle wiring 검증은 temporary scene bridge 기준으로만 확인한다.
- reproducible scene repair는 block25에서 공식화한다.

## 빠른 검증 절차

1. Unity 열기
2. `SM/Seed/Generate Sample Content`
3. 필요하면 `SM/Seed/Migrate Legacy Sample Content`
4. `SM/Validation/Validate Content Definitions`
5. EditMode tests 실행

## temporary bridge 메모

현재 dirty tree에는 `SM/Bootstrap/Prepare First Playable`와 scene installer WIP가 있다.
다만 block24의 공식 계약은 이 bridge가 아니라 **content contract와 integrity diagnosis**다.

즉:

- block24는 “플레이 가능 인증” 단계가 아니다.
- scene repair를 공식 메뉴/계층으로 승격하는 일은 block25 범위다.

## known issues

- scene repair/bootstrap은 아직 temporary bridge에 의존한다.
- `SceneIntegrityTests`는 block24 동안 scene asset text contract를 우선 확인한다. live component query 정합성은 block25 범위다.
- manual observer playable 인증은 아직 끝나지 않았다.
- Battle observer-grade replay와 operator UI는 아직 다음 블록 범위다.

## workaround

- canonical root가 비어 있으면 `SM/Seed/Generate Sample Content`를 다시 실행한다.
- legacy path 흔적이 의심되면 `SM/Seed/Migrate Legacy Sample Content`를 먼저 실행한다.
- local WIP bridge까지 같이 점검할 때만 `SM/Bootstrap/Prepare First Playable`를 임시로 사용한다.
