# targeting과 ai vocabulary

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/02_design/combat/targeting-and-ai-vocabulary.md`
- 관련문서:
  - `docs/02_design/combat/combat-behavior-contract.md`
  - `docs/02_design/combat/skill-authoring-schema.md`
  - `docs/02_design/combat/resource-cadence-loadout.md`
  - `docs/03_architecture/combat-harness-and-debug-contract.md`

## 목적

이 문서는 구현자마다 다르게 해석되면 안 되는 targeting selector, fallback, range discipline vocabulary를 enum + data contract 수준으로 고정한다.

## formation과 range discipline

| formation | 기본 역할 | 기본 discipline |
| --- | --- | --- |
| `Frontline` | 탱커, 브루저 | `Collapse`, 짧은 `HoldBand` |
| `Midline` | 하이브리드 | `HoldBand` |
| `Backline` | 원딜, 메이지, 힐러 | `HoldBand`, `KiteBackward`, 제한적 `AnchorNearFrontline` |

허용 discipline은 `Collapse`, `HoldBand`, `KiteBackward`, `SideStepHold`, `AnchorNearFrontline` 다섯 개뿐이다.

## target selector 의미

| selector | 의미 |
| --- | --- |
| `CurrentTarget` | 현재 lock된 대상을 유지 |
| `NearestReachableEnemy` | nav/path와 attack/cast reach 기준 가장 가까운 적 |
| `NearestFrontlineEnemy` | `Frontline` 적 중 가장 가까운 대상, 없으면 `NearestReachableEnemy` |
| `LowestCurrentHpEnemy` | current HP 절댓값 최저 적 |
| `LowestHpPercentEnemy` | HP 비율 최저 적 |
| `LowestEhpEnemy` | `EstimateEhpAgainst(source)` 기준 최저 적 |
| `MarkedEnemy` | `Mark` 상태 적 우선, 없으면 fallback |
| `LargestEnemyCluster` | `clusterRadius` 내 적 수가 최대인 지점 또는 대상 |
| `BacklineExposedEnemy` | `Backline`이면서 같은 팀의 살아있는 `Frontline` guard가 `2.5m` 내에 없는 적 |
| `Self` | 자기 자신 |
| `LowestCurrentHpAlly` | current HP 절댓값 최저 아군 |
| `LowestHpPercentAlly` | HP 비율 최저 아군 |
| `LowestEhpAlly` | `EstimateEhpAgainstAverageThreat()` 기준 최저 아군 |
| `NearestInjuredAlly` | max 미만 HP를 가진 가장 가까운 아군 |
| `EmptyPointNearSelf` | self 근처 빈 배치 지점 |
| `EmptyPointNearTarget` | target 근처 빈 배치 지점 |

## default target rule

모든 basic attack과 skill은 `TargetRule`을 가진다.

| 필드 | 기본값 |
| --- | --- |
| `reevaluateIntervalSeconds` | `0.25f` |
| `minimumCommitSeconds` | `0.75f` |
| `clusterRadius` | `2.5f` |
| `lockTargetAtCastStart` | `true` |
| `retargetLockMode` | `UntilCastComplete` |

fallback 기본값은 아래와 같다.

- 적 대상 스킬: `NearestReachableEnemy`
- 아군 대상 스킬: `Self`
- ground 스킬: `Abort`

## anti-jitter rule

- target 재평가는 `0.25s` 주기와 이벤트 기반으로만 수행한다.
- 이벤트 기준은 대상 사망, untargetable, cast ready, jammed `> 0.5s`다.
- basic attack target은 최소 `0.75s` 유지한다.
- 대상이 죽거나 untargetable이 되거나 `maxAcquireRange + 1.0m` 이상 벗어나면 즉시 lock을 해제한다.

## hysteresis

`HoldBand`와 `KiteBackward`는 아래 hysteresis를 가진다.

- `distance > preferredRangeMax + approachBuffer`면 접근
- `distance < preferredRangeMin - retreatBuffer`면 후퇴
- 그 외에는 hold

AoE skill은 `preferredMinTargets > 1`이면 최대 `0.4s`까지 더 좋은 cluster를 기다릴 수 있고, 이후 fallback한다.

## debug visibility

scene gizmo 또는 equivalent debug view는 최소 아래를 보여야 한다.

- current target line
- preferred range band
- current selector / fallback state
- retarget lock remaining time
- `BacklineExposed` 판정용 frontline guard radius
- AoE cluster radius
