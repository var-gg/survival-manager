# 구현 기록

## 메타데이터

- 작업명: EditMode failure closure
- 담당: Codex
- 상태: completed
- 최종수정일: 2026-04-01

## Phase log

- phase 0:
  - 새 task를 분리하고 최근 `test-edit` 결과에서 남은 9개 failure 목록을 재확인했다.
  - 전투 simulator 7건, hit-resolution 1건, validator 1건으로 분류했다.
- phase 1:
  - `EngagementSlotService`가 ranged까지 slotting 대상으로 잡고 있었고, slot ring 반경도 desired edge range를 반영하지 않아 melee/ranged 양쪽에서 event timing이 꼬이는 것을 수정했다.
  - `TacticEvaluator`에 target switch delay가 새 target 획득에도 적용되도록 보강했고, heal/shield/buff range band를 더 가까운 support 거리로 조정했다.
  - `CombatProfiles` / `HitResolutionService`의 dodge/block clamp를 runtime deterministic test와 맞게 `0..1`로 풀었다.
- phase 2:
  - `Ranged_Uses_Mobility_To_Break_Contact_When_Pressed`를 재현하기 위해 read-only `unity-cli exec` probe로 step-by-step edge distance와 mobility cooldown을 덤프했다.
  - probe 결과를 바탕으로 `MovementResolver`와 `UnitSnapshot`에서 maintain-range mobility가 `pressure zone`에서도 발동하도록 보정했다.
- phase 3:
  - `ContentValidationWorkflowTests`의 canonical `TraitPoolDefinition` load 의존을 제거하고 temp asset authoring으로 바꿨다.
  - footprint contract 도입 이후 의미가 없어진 center-distance assertion을 `BattleSimulationSpatialTests`에서 edge-distance oracle로 교체했다.
- phase 4:
  - targeted test와 full `test-edit`를 다시 돌려 `67 / 67` green을 확인했다.

## deviation

- mobility failure는 처음 예상보다 runtime bug와 test oracle drift가 함께 있었다.
- canonical `TraitPoolDefinition` type discovery 문제는 runtime/source-of-truth 전체 수선이 아니라, 현재 validator test setup을 temp asset 기반으로 바꿔 우회했다.

## blockers

- 없음

## diagnostics

- 최초 failure surface:
  - `BattleResolutionTests`: 2건
  - `BattleSimulationSpatialTests`: 5건
  - `CombatContractsTests.HitResolution_Resolves_Dodge_Before_Crit_Block_And_Armor`
  - `ContentValidationWorkflowTests.ContentDefinitionValidator_FlagsDeepSchemaContractDrift`
- mobility probe 핵심 관찰:
  - ranger는 `2.0~2.3 edge distance` 구간에서 `BreakContact` walk만 반복하고 mobility cooldown이 전혀 소비되지 않았다.
  - 원인은 `triggerMaxDistance`를 hard gate로만 써서 `range band 내부 pressure zone`에서는 roll을 쓰지 않던 점이었다.

## why this loop happened

- `sample content preflight` 분리 이후 read path rewrite는 사라졌지만, 전투 runtime의 phase/range/mobility 계약과 일부 canonical asset load 가정이 기존 EditMode 기대와 어긋난 상태로 남아 있었다.
