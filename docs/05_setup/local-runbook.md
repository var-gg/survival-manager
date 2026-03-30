# 로컬 실행 런북

- 상태: active
- 최종수정일: 2026-03-30
- phase: prototype

## 먼저 실행할 메뉴

- `SM/Bootstrap/Prepare Observer Playable`

## 빠른 실행 절차

1. Unity editor `6000.4.0f1`로 프로젝트를 연다.
2. `SM/Bootstrap/Prepare Observer Playable`를 실행한다.
3. `Boot.unity`가 자동으로 열렸는지 확인한다.
4. Play 한다.

## 가장 빠른 전투 smoke 경로

1. Town에서 `Quick Battle`
2. Battle 종료까지 대기
3. `Continue`
4. Reward 카드 1장 선택
5. `Return Town`

## 원정 재개 경로

1. Town에서 `Debug Start`
2. Expedition에서 경로를 선택한다.
3. Battle과 Reward를 한 번 완료한 뒤 Town으로 복귀한다.
4. 다시 Town에서 `Debug Start`를 눌러 진행 중 원정을 재개한다.

## 현재 계약 경로

- canonical sample content root: `Assets/Resources/_Game/Content/Definitions/**`
- scene repair source of truth: `SM/Bootstrap/Repair First Playable Scenes`
- one-shot bootstrap: `SM/Bootstrap/Prepare Observer Playable`

## 참고 문서

- 최신 playable 상태: [tasks/001_mvp_vertical_slice/status.md](../../tasks/001_mvp_vertical_slice/status.md)
- 현재 알려진 이슈: [docs/06_production/current-known-issues.md](../06_production/current-known-issues.md)
