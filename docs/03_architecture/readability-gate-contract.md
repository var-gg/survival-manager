# Readability Gate Contract

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-02
- 소스오브트루스: `docs/03_architecture/readability-gate-contract.md`
- 관련문서:
  - `docs/03_architecture/telemetry-contract.md`
  - `docs/03_architecture/combat-harness-and-debug-contract.md`
  - `docs/02_design/combat/battle-presentation-contract.md`

## 목적

Loop D에서 readability를 참고 리포트가 아니라 dev/harness/CI gate로 고정한다.

## salience class

| class | weight | 예시 |
| --- | --- | --- |
| `Ambient` | `0.25` | aura tick, minor refresh |
| `Minor` | `1.0` | basic attack, small DoT/HoT |
| `Major` | `2.0` | signature/flex resolve, guard break, hard CC, summon spawn |
| `Critical` | `3.0` | multi-kill, decisive burst, clutch heal/barrier |

## aggregation policy

- 같은 `source-target-status` DoT tick은 `330ms` 윈도에서 merge한다.
- 같은 source-target minor basic hit는 `330ms` 윈도에서 최대 1 visual packet만 남긴다.
- `Major/Critical` active 동안 ambient/minor visual noise를 suppress할 수 있다.
- status chip는 상위 3개만 노출한다.
- floating text burst는 대상당 초당 4개를 넘기지 않는다.
- raw telemetry는 그대로 보존하고 readability report는 aggregated visual packet 기준도 같이 본다.

## threshold

6 combatants 기준 기본 한도:

- `unexplainedDamageRatio <= 0.05`
- `unexplainedHealingRatio <= 0.05`
- `offscreenMajorEventRatio <= 0.10`
- `targetSwitchesPer10sP95 <= 6.0`
- `idleGapP95Seconds <= 3.25`
- `timeToFirstMajorActionP50 = 1.25 ~ 6.0`
- `majorEventCollisionRate <= 0.20`
- `salienceWeightPer1sP95 <= 9.0`
- `statusChipOverflowRate <= 0.05`
- `floatingTextBurstOverflowRate <= 0.05`

combatant 수가 6을 넘으면 salience budget은 아래를 사용한다.

- `9.0 + 0.5 * max(0, combatantCount - 6)`

## violation kind

- `UnexplainedDamage`
- `UnexplainedHealing`
- `SalienceOverload`
- `MajorEventCollision`
- `IdleGapTooLong`
- `TargetThrash`
- `StatusChipOverflow`
- `FloatingTextBurstOverflow`
- `OffscreenMajorEvent`
- `ProcChainOpacity`

## failure semantics

- readability fatal은 `Broken` content와 동일하게 취급한다.
- readability error는 최소 `Watch`다.
- readability debt가 높은 content에 rarity/budget 상향을 먼저 걸지 않는다.

## dev overlay minimum

- current 1초 salience weight
- 최근 5초 major/critical timeline
- unexplained event 누적치
- per-unit target switch count
- top 5 sourceDisplayName contribution
- current readability violations
