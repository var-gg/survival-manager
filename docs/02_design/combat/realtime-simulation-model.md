# 실시간 체감 시뮬레이션 모델

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-30
- 소스오브트루스: `docs/02_design/combat/realtime-simulation-model.md`
- 관련문서:
  - `docs/02_design/combat/battlefield-and-camera.md`
  - `docs/02_design/combat/team-tactics-and-unit-rules.md`
  - `docs/03_architecture/combat-runtime-architecture.md`
  - `docs/03_architecture/combat-state-and-event-model.md`
  - `docs/04_decisions/adr-0006-combat-sim-boundary.md`

## 목적

이 문서는 플레이어가 느끼는 전투는 연속적이어야 하지만, 내부 truth는 결정론적 fixed-step으로 유지한다는 기준을 정리한다.

## 플레이어 체감 규칙

- 플레이어는 전투를 "미리 다 계산된 영상 재생"이 아니라 "지금 벌어지는 전투"처럼 느껴야 한다.
- x1 / x2 / x4 / pause는 시뮬레이션 시간 제어다.
- 전투 결과는 배치, posture, tactic, stat의 조합으로 납득 가능해야 한다.

## 내부 실행 규칙

- 도메인 전투는 fixed-step으로 돈다.
- 현재 기본 step은 `0.1s`다.
- Unity frame rate는 전투 truth를 바꾸지 않는다.
- Unity는 이전 step과 현재 step 사이를 보간해서 보여준다.

## 현재 상태 머신

전투 유닛은 아래 상태를 순환한다.

- `Spawn`
- `AdvanceToAnchor`
- `SeekTarget`
- `MoveToEngage`
- `Windup`
- `Recovery`
- `Reposition`
- `Retreat`
- `Dead`

## step 단위 처리 순서

1. 모든 유닛 timer를 step만큼 감소시킨다.
2. 살아 있는 유닛을 속도 우선 순서로 처리한다.
3. spawn 또는 anchor 복귀 중인 유닛은 home 위치까지 이동한다.
4. windup 중인 유닛은 pending action 실행 가능 여부를 확인한다.
5. 나머지 유닛은 tactic 평가 후 target, desired range, action intent를 정한다.
6. 사거리 밖이면 이동하고, 사거리 안이면 windup 또는 recovery로 전환한다.
7. 팀 내 spacing을 가볍게 보정한다.
8. step snapshot과 이번 step event를 read model로 내보낸다.

## 종료 규칙

- 한쪽 팀이 전멸하면 즉시 종료한다.
- 최대 step에 도달하면 생존 체력 합계로 승패를 정한다.
- caller는 prototype 상황에 맞춰 최대 step을 정할 수 있다.

## 결정론 규칙

- 같은 seed, 같은 초기 배치, 같은 posture, 같은 definition이면 같은 결과를 내야 한다.
- tie-break와 target score 보조 계산도 seed를 기준으로 고정한다.
- Unity presentation은 domain 결과를 다시 판정하지 않는다.
