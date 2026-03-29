# Local Runbook

## 반드시 먼저 실행할 메뉴 1개

- `SM/Bootstrap/Prepare Observer Playable`

- 상태: active
- 최종수정일: 2026-03-29
- phase: prototype

## 빠른 실행 절차

1. Unity 열기
2. `SM/Bootstrap/Prepare Observer Playable`
3. `Boot.unity` 자동 오픈 확인
4. Play

## 가장 빠른 Battle smoke 경로

1. Town에서 `Quick Battle`
2. Battle replay 종료 대기
3. `Continue`
4. Reward 카드 1장 선택
5. `Return Town`

## 현재 기준

- canonical sample content root는 `Assets/Resources/_Game/Content/Definitions/**`
- scene repair source of truth는 `SM/Bootstrap/Repair First Playable Scenes`
- observer playable one-shot bootstrap은 `SM/Bootstrap/Prepare Observer Playable`
- Battle은 `resolve once -> replay track -> observer presentation` 구조를 사용한다

## known issues

- Battle actor motion은 observer-grade이며 polished animation이 아니다.
- Expedition은 5노드 박스 시각화까지만 구현되어 있고 branching 선택은 다음 단계다.
- Town / Battle controller는 현재 scene open/load 시 live rebind 경로가 가장 안정적이다.
- scene/asset 계약이 어긋나면 메뉴 재실행이 가장 빠른 복구 경로다.
