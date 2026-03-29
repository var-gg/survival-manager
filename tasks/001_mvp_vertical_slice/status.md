# Task Status: 001 MVP Vertical Slice

## 반드시 먼저 실행할 메뉴 1개

- `SM/Seed/Generate Sample Content`

- Status: in-progress
- Last Updated: 2026-03-29
- Phase: prototype
- Task ID: 001

## 현재 상태

이 task의 current handoff 상태는 다음으로 요약된다.

- canonical sample content root를 `Assets/Resources/_Game/Content/Definitions/**`로 고정했다.
- stale `SampleScene.unity` smoke 계약을 제거하고 repository integrity 기준을 다시 세운다.
- Boot/Town/Battle wiring 검증은 temporary scene bridge 기준으로 유지한다.
- reproducible scene repair와 observer playable 보장은 block25 이후 범위다.

## block24에서 보장하는 범위

- canonical content 생성/검증
- playable scene asset 존재 확인
- Boot/Town/Battle temporary wiring baseline 확인

## 아직 남은 구조 작업

- scene repair/menu bootstrap 공식화
- observer-grade battle replay 가시화
- Town/Expedition/Reward operator UI 정리

## known issues

- scene repair는 아직 temporary bridge 의존
- scene integrity는 block24 동안 asset text contract 기준으로 확인하며, Boot/Town/Battle live scene query 정합성은 block25에서 확정 예정
- manual first battle certification은 아직 미완료
- observer-grade visual feedback은 아직 미구현

## workaround

- canonical root가 비어 있으면 `SM/Seed/Generate Sample Content` 재실행
- legacy path가 의심되면 `SM/Seed/Migrate Legacy Sample Content` 선실행
- local WIP bridge까지 같이 볼 때만 `SM/Bootstrap/Prepare First Playable` 임시 사용

## 다음 추천 단계

1. block25에서 scene repair/bootstrap을 공식 메뉴로 정리
2. block26에서 observer-grade battle replay 계층 추가
3. block27~28에서 operator UI와 visual smoke 정리
