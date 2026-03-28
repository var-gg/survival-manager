# 전투 루프

- 상태: draft
- 최종수정일: 2026-03-29
- phase: prototype

## 목적

이 문서는 MVP 전투 루프의 언어를 정의한다.
목표는 presentation 복잡도를 올리기 전에, 전투 구현을 단순하고 읽기 쉽고 검증 가능하게 유지하는 것이다.

## MVP 전투 원칙

### 전투 형태

- 전투는 auto-battle이다.
- 전투 진형은 2열을 사용한다.
- 전투당 배치 유닛 수는 4명 기준이다.
- 현재 구현 기준 시뮬레이션 해석은 `fixed-tick` 채택 상태로 본다.
- presentation은 단순 primitive/로그 재생이어도 내부 truth는 domain resolver를 따른다.

### 현재 adapter 원칙

- battle truth는 `BattleFactory`, `BattleResolver`, `TacticEvaluator`가 담당한다.
- scene은 battle event를 재생/표시하는 adapter일 뿐이다.
- 전투 연출보다 "끝까지 1판이 돈다"를 우선한다.

### 기본 전투 시퀀스

1. battle deploy 4인이 전투에 진입한다.
2. enemy 4인이 생성된다.
3. `BattleFactory`가 `BattleState`를 만든다.
4. `BattleResolver`가 fixed-tick 전투를 끝까지 수행한다.
5. scene adapter는 battle event와 HP, 승패, 로그를 표시한다.
6. 전투 종료 후 Reward scene으로 이동한다.

### 기본 action 집합

- basic attack
- active skill
- wait / defend

## 현재 정리된 결정

- `fixed-tick vs round-based`는 열린 질문이 아니라, 현재 구현 기준으로 `fixed-tick` 채택 상태다.
- Battle scene은 primitive 4 vs 4, HP 텍스트, 전투 로그, 속도 x1/x2/x4, 승패 표시를 우선한다.
- 임시 augment는 이번 run에만 적용한다.
