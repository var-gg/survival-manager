# PVP ruleset과 arena loop

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-31
- 소스오브트루스: `docs/02_design/meta/pvp-ruleset-and-arena-loop.md`
- 관련문서:
  - `docs/02_design/meta/pvp-boundary.md`
  - `docs/02_design/systems/squad-blueprint-and-build-ownership.md`
  - `docs/03_architecture/arena-snapshot-matchmaking-and-season-contract.md`
  - `docs/03_architecture/replay-persistence-and-run-audit.md`

## 목적

이 문서는 launch floor 이후의 PvP를 live matchmaking이 아니라 async arena 문법으로 고정한다.
이번 패스의 구현 목표는 online service가 아니라 ruleset, snapshot, local/offline sim scaffold다.

## unlock과 deck 규칙

- arena unlock: 모든 story chapter clear 후
- active defense blueprint: `1`
- saved offense blueprint: 최대 `3`
- player-facing label은 `PvP 덱` 또는 `Arena 덱`을 쓸 수 있다.
- internal canonical type은 계속 `SquadBlueprint`다.

## async arena loop

1. 플레이어가 defense blueprint를 등록한다.
2. 시스템이 compiled battle snapshot 기반 `ArenaDefenseSnapshot`을 만든다.
3. offense 화면은 비슷한 rating band의 후보 `3`명을 보여 준다.
4. 플레이어가 offense blueprint를 선택하면 즉시 simulation을 수행한다.
5. 결과는 replay와 rating delta로 기록된다.

## power inclusion 규칙

### 포함

- permanent augment
- trait
- item
- skill
- passive board

### 제외

- temporary augment
- run overlay state
- 현재 run에서만 유효한 임시 전투 modifier

## season 규칙

- season cadence: `4주`
- 점수 축: launch floor에서는 `Arena Rating` 하나만 사용
- 보상: season ladder + weekly chest 수준으로 제한

## 이번 패스의 범위

- defense snapshot registration
- local/offline opponent candidate selection
- instant sim
- replay bundle generation
- local rating ledger

## 이번 패스의 비범위

- live online matchmaking
- leaderboard backend
- seasonal live ops
- PvP 전용 전투 규칙 분기 확장
