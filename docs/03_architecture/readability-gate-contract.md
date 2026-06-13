# Readability Gate Contract

- 상태: active
- 소유자: repository
- 최종수정일: 2026-06-14
- 소스오브트루스: `docs/03_architecture/readability-gate-contract.md`
- 관련문서:
  - `docs/03_architecture/telemetry-contract.md`
  - `docs/03_architecture/combat-harness-and-debug-contract.md`
  - `docs/02_design/combat/battle-presentation-contract.md`
  - `pindoc://decision-loopd-readability-gate-운영계약`

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

## severity mapping

readability gate는 `Info / Warning / Error / Fatal` 4단계로 운영한다.
`BattleTelemetryAnalysisService.ResolveReadabilityGateSeverity`와 `ReadabilityGateConfig`가 코드 쪽 source-of-truth다.

| violation | 기본 severity | fatal 승격 |
| --- | --- | --- |
| `UnexplainedDamage` | `Error` | ratio `> 0.10` |
| `UnexplainedHealing` | `Error` | ratio `> 0.10` |
| `ProcChainOpacity` | `Error` | 없음 |
| `SalienceOverload` | `Error` | scaled salience budget `+ 3.0` 초과 |
| `MajorEventCollision` | `Error` | rate `> 0.30` |
| `TargetThrash` | `Warning` | 없음 |
| `IdleGapTooLong` | `Warning` | 없음 |
| `OffscreenMajorEvent` | `Warning` | 없음 |
| `StatusChipOverflow` | `Info` | 없음 |
| `FloatingTextBurstOverflow` | `Info` | 없음 |

## failure semantics

- readability `Fatal`은 Loop D shard 안에서 fail이다.
- readability `Error`는 fail이 아니라 `readability_watchlist.json`에 남기는 watchlist다.
- readability `Warning`/`Info`는 trend와 UI 표현 debt로 기록하지만 단독 fail이 아니다.
- `MissingExplainStamp`는 readability severity가 아니라 telemetry 무결성 결함이므로 항상 fail이다.
- readability debt가 높은 content에 rarity/budget 상향을 먼저 걸지 않는다.

## command lane

- `loopd-slice`: slice 산출 전용. readability를 평가하지 않고 산출물을 만든다.
- `loopd-purekit`, `loopd-systemic`, `loopd-runlite`: shard 내부에서 fatal 1건 이상이면 fail로 본다. purekit도 fatal fail에 포함한다.
- `loopd-smoke`, `loopd-full`: shard 집합 명령이며 같은 fatal=fail 규칙을 상속한다.
- `test-batch-fast`와 기본 EditMode lane은 Loop D long-running 관측을 재흡수하지 않는다. severity mapping 같은 계약 테스트만 focused BatchOnly로 둔다.

## dev overlay minimum

- current 1초 salience weight
- 최근 5초 major/critical timeline
- unexplained event 누적치
- per-unit target switch count
- top 5 sourceDisplayName contribution
- current readability violations
