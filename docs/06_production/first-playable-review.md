# 첫 플레이어블 스냅샷

- 상태: active
- 최종수정일: 2026-03-30
- 단계: prototype

## 먼저 실행할 메뉴

- `SM/Bootstrap/Prepare Observer Playable`

## 한 줄 요약

- 현재 prototype의 관찰 가능한 playable 경계는 `Boot -> Town -> Expedition -> Battle -> Reward -> Town`이다.

## 운영자 관점 핵심 포인트

- Town과 Expedition에서 3x2 anchor 배치와 team posture를 바꿀 수 있다.
- Battle은 live simulation observer UI로 진행되며, Reward 이후 Town으로 복귀한다.
- 원정은 Town `Debug Start`로 이어서 재개할 수 있다.

## live 상태 문서

- 최신 상태, 리스크, 다음 우선순위는 [tasks/001_mvp_vertical_slice/status.md](../../tasks/001_mvp_vertical_slice/status.md)를 기준으로 본다.
