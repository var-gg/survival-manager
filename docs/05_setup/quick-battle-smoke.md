# Quick Battle Smoke

- 상태: active
- 최종수정일: 2026-03-29
- 단계: prototype

## 목적

이 문서는 Town에서 2~3클릭 안에 Battle observer smoke를 보는 가장 빠른 경로를 정리한다.

## 반드시 먼저 실행할 메뉴 1개

- `SM/Bootstrap/Prepare Observer Playable`

## 가장 빠른 경로

1. Unity 열기
2. `SM/Bootstrap/Prepare Observer Playable`
3. `Boot.unity` Play
4. Town에서 `Quick Battle`
5. Battle replay 종료 후 `Continue`
6. Reward에서 카드 1장 선택
7. `Return Town`
8. language overlay로 `ko`/`en`을 전환해 Town/Reward 라벨이 즉시 바뀌는지 확인

## 기대 화면

- Town: recruit 카드 3개, roster, squad, deploy preview, Quick Battle 버튼
- Battle: primitive actor, HP label/bar, 로그, 속도 버튼, pause, progress
- Reward: 3지선다 카드, 즉시 반영되는 요약/상태 텍스트
- overlay: 우측 상단 locale selector, scene 이동 후에도 선택 유지
