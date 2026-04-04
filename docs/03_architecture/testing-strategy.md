# 테스트 전략

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-04-04
- 소스오브트루스: `docs/03_architecture/testing-strategy.md`
- 관련문서:
  - `docs/03_architecture/validation-and-acceptance-oracles.md`
  - `docs/00_governance/implementation-review-checklist.md`
  - `docs/03_architecture/dependency-direction.md`
  - `docs/03_architecture/unity-boundaries.md`

## 목적

이 문서는 feature closure나 done 판정을 정의하지 않는다.
이 문서는 저비용 검증 표면을 어떤 순서로 추가할지에만 답한다.

## 비목표

- acceptance matrix를 정의하지 않는다.
- `status.md` evidence 형식을 정의하지 않는다.
- compile / validator / targeted tests / runtime smoke의 closure 의미를 다시 정의하지 않는다.
- task closure 판정을 대체하지 않는다.

## 역할 경계

- feature closure, acceptance matrix, evidence 기록 규칙은 `validation-and-acceptance-oracles.md`가 담당한다.
- 이 문서는 validator, EditMode, integration, PlayMode, manual smoke를 어떤 순서로 붙일지에만 집중한다.

## 검증 표면 추가 순서

1. 문서/구조 점검
2. data/catalog validator
3. targeted EditMode test
4. focused integration test
5. PlayMode test
6. manual runtime smoke

## 우선 선택 기준

- scene만 유일한 검증 표면으로 삼지 않는다.
- data, prefab, settings asset 검증을 먼저 만든다.
- save/load 계약, asmdef 경계, content catalog 무결성 변화에는 validator 또는 targeted EditMode test를 먼저 붙인다.
- broad PlayMode보다 focused integration을 우선한다.
- long-running smoke는 마지막 phase gate로 둔다.

## 현재 targeted EditMode test 목록 (2026-04-04)

| 파일 | 커버리지 |
|---|---|
| `CombatActionResolverTests` | 기본공격/스킬/힐/쉴드/킬/어시스트/디펜드 액션 해소 |
| `MovementResolverTests` | 거리 계산, 레인지 밴드, 포스처별 홈 위치, 충돌 방지, 루트 이동불가 |
| `EngagementSlotServiceTests` | 슬롯 배치, 다수 공격자 분산, 오버플로우, 원거리 null 반환 |
| `CombatContractsTests` | hit resolution 순서 (dodge → crit → block → armor) |
| `BattleResolutionTests` | 전투 종료/이벤트 생성, 시드 결정론, 앵커별 접촉 시점 |
| `BattleSimulationSpatialTests` | 근접 접근, 원거리 유지, 힐러 경로, 슬롯 분리 |
| `StatusResolutionServiceTests` | 상태이상 지속/해제, 강인/정화, 제어 저항 윈도우 |
| `LoopAContractClosureTests` | 에너지 시그니처, 타겟 락, 소환수 킬 미러링 |
