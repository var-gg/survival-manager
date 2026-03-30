# MVP 플레이테스트 체크리스트

- 상태: active
- 최종수정일: 2026-03-30
- phase: prototype

## 첫 에디터 실행

1. Unity editor `6000.4.0f1`로 프로젝트를 연다.
2. 패키지 import / reload가 끝날 때까지 기다린다.
3. `SM/Bootstrap/Prepare Observer Playable`를 실행한다.
4. `Boot.unity`가 열렸는지 확인한다.
5. Play 한다.

## 자동 검증 기준

### EditMode

- canonical sample content root 최소 자산 존재
- Town / Battle / Reward / Expedition 필수 controller와 runtime binding 계약 통과
- battle setup builder / economy / reward payload / scene integrity 테스트 통과

### PlayMode

- Boot -> Town 자동 진입
- Town `Quick Battle`로 Battle 진입
- Battle 종료 후 Reward 선택과 Town 복귀 가능
- Expedition route 선택 후 Reward를 거쳐 Town 복귀 가능
- Town `Debug Start`로 진행 중 원정 재개 가능

## 수동 검증 기준

- Town에서 recruit / reroll / posture / anchor 버튼을 눌렀을 때 상태 텍스트가 즉시 변한다.
- Battle에서 HP / target / windup / speed / pause / progress를 읽을 수 있다.
- Reward 선택 직후 gold / inventory / temp augment 수치가 갱신된다.
- Expedition route effect와 reward 선택 결과가 Town 복귀 후 유지된다.
