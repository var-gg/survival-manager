# Task Status: 001 MVP Vertical Slice

## 반드시 먼저 실행할 메뉴 1개

- `SM/Bootstrap/Prepare Observer Playable`

- Status: in-progress
- Last Updated: 2026-03-29
- Phase: prototype
- Task ID: 001

## 현재 상태

이 task의 current 상태는 다음으로 요약된다.

- canonical sample content root를 `Assets/Resources/_Game/Content/Definitions/**`로 고정했다.
- stale `SampleScene.unity` smoke 계약을 제거하고 repository integrity 기준을 다시 세웠다.
- `SM/Bootstrap/Repair First Playable Scenes`와 `SM/Bootstrap/Prepare Observer Playable`를 공식 메뉴로 올렸다.
- Boot/Town/Expedition/Battle/Reward scene asset repair와 build settings 보정이 재현 가능해졌다.

## block25에서 보장하는 범위

- canonical content 생성/검증
- playable scene asset repair/save
- Boot/Town/Battle live hierarchy 기준 controller/canvas/eventsystem 확인
- observer playable 진입용 editor bootstrap

## 아직 남은 구조 작업

- observer-grade battle replay 가시화
- Town/Expedition/Reward operator UI 정리
- README / smoke / operator checklist 정리

## known issues

- Town/Expedition/Reward는 아직 debug placeholder UI
- observer-grade visual feedback은 아직 미구현
- sample asset이 깨졌을 때는 seed regenerate가 필요할 수 있음

## workaround

- canonical root가 비어 있거나 sample asset 값이 비정상적이면 `SM/Seed/Generate Sample Content` 재실행
- legacy path가 의심되면 `SM/Seed/Migrate Legacy Sample Content` 선실행
- scene asset만 다시 맞추고 싶으면 `SM/Bootstrap/Repair First Playable Scenes` 실행

## 다음 추천 단계

1. block26에서 observer-grade battle replay 계층 추가
2. block27에서 battle visual feedback 계층 추가
3. block28~29에서 operator UI와 smoke/checklist 정리
