# 전투 런타임 아키텍처

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-30
- 소스오브트루스: `docs/03_architecture/combat-runtime-architecture.md`
- 관련문서:
  - `docs/03_architecture/unity-boundaries.md`
  - `docs/03_architecture/combat-state-and-event-model.md`
  - `docs/03_architecture/combat-content-mapping.md`
  - `docs/02_design/combat/combat-spatial-contract.md`
  - `docs/02_design/combat/combat-behavior-contract.md`
  - `docs/04_decisions/adr-0006-combat-sim-boundary.md`
  - `docs/04_decisions/adr-0014-grid-deployment-continuous-combat.md`

## 목적

이 문서는 fixed-row replay 모델을 step-based live simulation 구조로 바꾼 뒤의 전투 런타임 책임 분리를 정리한다.

## 레이어 기준

- `SM.Combat`
  - `BattleFactory`: definition과 posture, anchor를 받아 초기 `BattleState`를 만든다.
  - `BattleSimulator`: fixed-step loop를 돌리는 domain orchestrator다.
  - `TacticEvaluator`: rule chain을 평가해 action intent를 만든다.
  - `TargetScoringService`: spatial target score를 계산한다.
  - `MovementResolver`: home position, slotting, range band, reposition, spacing을 계산한다.
  - `EngagementSlotService`: target occupancy와 slot ring을 계산한다.
  - `HitResolutionService`: dodge -> crit -> block -> armor 순서를 소유한다.
  - `CombatActionResolver`: hit, heal, defend event와 수치 변화를 적용한다.
  - `BattleReadModelBuilder`: domain state를 step read model로 변환한다.
- `SM.Unity`
  - `GameSessionState`: 배치 assignment와 team posture를 session에 유지한다.
  - `BattleScreenController`: simulator를 구동하고 scene flow와 HUD를 연결한다.
  - `BattlePresentationController`: 이전 step과 현재 step을 받아 actor view를 갱신한다.
  - `BattleActorView`: 위치 보간, head anchor, overhead UI, pulse 같은 presentation만 담당한다.

## 런타임 흐름

1. session이 배치 앵커와 team posture를 유지한다.
2. Battle scene이 `UnitDefinition` 목록을 조립한다.
3. `BattleFactory.Create`가 `BattleState`를 만든다.
4. `BattleSimulator.Step()`이 domain truth를 한 step씩 진행한다.
5. `BattleReadModelBuilder`가 `BattleSimulationStep`을 만든다.
6. Unity는 이전 step과 현재 step을 보간해서 scene에 반영한다.
7. battle 완료 시 session과 Reward scene flow에 결과만 넘긴다.

## 경계 규칙

- 전투 승패, 타깃 선택, 이동, 사거리 판정은 `SM.Combat`가 결정한다.
- Unity는 position interpolation과 feedback만 담당한다.
- `MonoBehaviour`는 전투 truth를 저장하거나 재판정하지 않는다.
- NavMesh, physics collision, scene object state는 battle truth source가 아니다.

## 현재 단순화

- movement는 custom `CombatVector2` 기반이다.
- same-team spacing은 약한 분리 보정만 사용한다.
- 적 roster와 encounter는 아직 stub 비중이 높고, `BattleSetupBuilder` 경로는 migration-only다.
- prototype의 live battle definition 일부는 content asset 대신 runtime hero record에서 조립된다.
- follow-up TODO: encounter asset authoring을 닫으면 enemy build도 authored catalog 기준으로 옮긴다.
