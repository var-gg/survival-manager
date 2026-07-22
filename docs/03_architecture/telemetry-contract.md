# Telemetry Contract

- 상태: active
- 소유자: repository
- 최종수정일: 2026-07-22
- 소스오브트루스: `docs/03_architecture/telemetry-contract.md`
- 관련문서:
  - `docs/03_architecture/readability-gate-contract.md`
  - `docs/03_architecture/replay-persistence-and-run-audit.md`
  - `docs/02_design/systems/first-playable-slice.md`

## 목적

Loop D에서 battle/meta/economy 계측의 authoritative source를 runtime telemetry event로
고정한다.

## source-of-truth

- source: simulation/runtime event
- consumer:
  - replay bundle
  - readability gate
  - battle summary
  - prune/health report
- non-source:
  - UI overlay
  - animation hook
  - VFX/floating text
  - `IBattleDiagnosticObserver`로 전달되는 measurement-only `BattleDiagnosticEvent`

## 진단 side channel 경계

- target predicate, tactic hold, intent override, displacement lifecycle처럼 canonical telemetry보다 세밀한 원인 계측은 internal `IBattleDiagnosticObserver`를 선택적으로 주입해 수집한다.
- 이 side channel은 authoritative telemetry가 아니며 replay bundle, `BattleStateCanonicalHash`, RNG, 전투 판정의 입력으로 사용하지 않는다.
- headless consumer는 observation을 별도 report로 집계한다. observer 유무의 step별 canonical hash와 event bytes 동일성을 검증하지 못하면 계측 결과를 유효한 증거로 쓰지 않는다.

## 핵심 타입

- `TelemetryDomain`
  - `Combat`, `Economy`, `Recruit`, `Retrain`, `Duplicate`, `Readability`, `BalanceHarness`
- `TelemetryEventKind`
  - battle start/end, target acquire/switch, action start/resolve
  - damage/heal/barrier/status/guard break/interrupt
  - summon spawn/despawn, kill credit
  - recruit/scout/retrain/duplicate events
  - readability violation
- `ExplainStamp`
  - `sourceKind`
  - `sourceContentId`
  - `sourceDisplayName`
  - `reasonCode`
  - `salience`

## ExplainStamp 필수 규칙

아래 event는 ExplainStamp가 반드시 있어야 한다.

- `DamageApplied`
- `HealingApplied`
- `BarrierApplied`
- `StatusApplied`
- `GuardBroken`
- `InterruptApplied`
- `SummonSpawned`
- `SummonDespawned`
- `KillCredited`

추가 제약:

- `sourceDisplayName`은 사람이 읽을 수 있는 이름이어야 한다.
- `sourceContentId`는 content catalog id여야 한다.
- `SystemRule`은 truly system-level rule에만 허용한다.

## emitter 위치

- `BattleSimulator`
  - battle start/end, target acquire/switch, mobility
- `CombatActionResolver`
  - action resolve, damage/heal/barrier, kill credit
- `StatusResolutionService`
  - status apply/remove, DoT/HoT, guard break, interrupt
- `GameSessionState`
  - recruit pack, recruit purchase, refresh, scout, retrain, duplicate

## runtime 저장 경계

- replay bundle은 raw `TelemetryEventRecord[]`를 함께 저장한다.
- `MatchRecordBlob`는 digest와 artifact path만 저장하고, full telemetry archive backend는
  이번 루프 범위가 아니다.

## generated artifact

- `Logs/loop-d-balance/purekit_report.json`
- `Logs/loop-d-balance/systemic_slice_report.json`
- `Logs/loop-d-balance/runlite_report.json`
- `Logs/loop-d-balance/readability_watchlist.json`

## fail semantics

- 필수 영향 event의 ExplainStamp 누락은 Loop D gate fail이다.
- battle summary/readability report 생성 실패도 gate fail이다.
