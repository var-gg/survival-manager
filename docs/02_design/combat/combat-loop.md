# 전투 루프

- 상태: active
- 최종수정일: 2026-03-29
- phase: prototype

## 현재 전투 시퀀스

1. battle deploy 4인이 전투에 진입한다.
2. enemy 4인이 생성된다.
3. `BattleFactory`가 simulation state를 만든다.
4. `BattleResolver`가 simulation state를 끝까지 계산한다.
5. `BattleReplayBuilder`가 시작 상태 + result에서 replay track을 만든다.
6. `BattlePresentationController`가 frame을 시간 순서대로 재생한다.
7. 결과를 `Reward` scene으로 넘긴다.

## 현재 action 집합

- basic attack
- active skill
- wait / defend

## observer 원칙

- Battle scene은 final mutated state dump를 다시 그리지 않는다.
- intro frame / event frame / result frame을 모두 보여준다.
- x1 / x2 / x4 / pause는 playback 제어다.
- 외부 animation package 없이 coroutine / lerp로 표현한다.
