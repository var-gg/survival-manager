# Battle Replay Model

- 상태: active
- 최종수정일: 2026-03-30
- phase: prototype

## 목적

이 문서는 `SM.Combat` 전투 truth와 `SM.Unity` replay/presentation adapter의 책임을 분리하는 현재 계약을 정리한다.

## 현재 계약

- 전투 계산은 `BattleFactory -> BattleResolver -> BattleResult`가 담당한다.
- Battle scene은 domain state를 다시 계산하지 않는다.
- Battle scene은 `BattleReplayBuilder`가 만든 replay track만 재생한다.

## replay 구성

- `BattleReplayTrack`
  - initial roster snapshot
  - ordered frames
  - winner / tick count / event count
- `BattleReplayFrame`
  - `intro`, `event`, `result`
  - tick
  - source / target id, name
  - action type
  - value / note
  - before / after HP
  - actor state snapshot
  - action별 duration seconds

## adapter 흐름

1. `BattleScreenController`가 전투 시작 시 definition을 만든다.
2. `BattleFactory.Create`로 simulation state를 만든다.
3. 같은 시작 상태를 replay seed로 복제한다.
4. `BattleResolver.Run`이 simulation state를 끝까지 계산한다.
5. `BattleReplayBuilder.Build`가 replay seed + result에서 frame 목록을 만든다.
6. `BattlePresentationController`가 frame 단위로 actor view를 재생한다.

## 현재 playback 세부

- x1 기준 event frame은 대략 `0.40s ~ 0.60s` 범위에서 action별로 다르게 보인다.
- `BasicAttack`은 anticipation -> impact -> return timing을 가진다.
- hit / heal / defend는 actor motion과 분리된 presentation feedback으로 처리한다.
- pause는 frame advance만 멈추는 것이 아니라 actor motion coroutine도 같이 멈춘다.
- Battle scene은 simulation state를 다시 mutate하지 않고 frame snapshot만 적용한다.

## 경계 규칙

- `SM.Combat`는 replay UI용 type에 의존하지 않는다.
- `MonoBehaviour`는 battle truth를 만들지 않는다.
- observer UI는 frame을 소비할 뿐, 승패를 다시 판정하지 않는다.
- pause / speed / progress는 playback 제어이며 domain 결과를 바꾸지 않는다.
