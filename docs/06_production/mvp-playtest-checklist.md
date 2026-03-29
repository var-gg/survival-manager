# MVP Playtest Checklist

- 상태: active
- 최종수정일: 2026-03-29
- phase: prototype

## 첫 에디터 실행

1. Unity editor `6000.4.0f1`로 프로젝트를 연다.
2. 패키지 import/reload가 끝날 때까지 기다린다.
3. `SM/Bootstrap/Prepare Observer Playable`를 실행한다.
4. `Boot.unity` 자동 오픈 확인
5. Play

## 자동 검증 기준

### EditMode

- `Boot/Town/Battle/Reward` 필수 controller / canvas / event system 존재
- `BattlePresentationRoot`, `PauseButton`, `ProgressFill`, `QuickBattleButton` 저장 확인
- canonical sample content root 최소 자산 존재
- replay builder intro / event / result 계약 통과

### PlayMode

- Boot -> Town 자동 진입
- Town `QuickBattle()`로 Battle 진입
- Battle에서 replay UI root 존재
- Reward 선택 후 Town 복귀

## 수동 검증 기준

- Town 버튼을 눌렀을 때 상태 텍스트가 즉시 변한다.
- Quick Battle 2~3클릭 안에 Battle observer smoke가 열린다.
- Battle에서 HP / 로그 / actor motion / pause / progress를 읽을 수 있다.
- Reward 선택 직후 gold / inventory / temp augment 숫자가 갱신된다.
