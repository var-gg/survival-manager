# Balance Scenario Coverage Matrix

- 상태: active
- 소유자: repository
- 최종수정일: 2026-06-14
- 소스오브트루스: `docs/03_architecture/balance-scenario-coverage-matrix.md`
- 관련문서:
  - `docs/03_architecture/sim-sweep-and-balance-kpis.md`
  - `docs/03_architecture/combat-harness-and-debug-contract.md`
  - `docs/03_architecture/readability-gate-contract.md`
  - `pindoc://task-balance-scenario-coverage-matrix`

## 목적

first playable 전투 콘텐츠가 어떤 sweep/Loop D scenario로 관측되는지 고정한다.
이 문서는 밸런스가 맞다는 선언이 아니라, 어떤 콘텐츠 축이 어떤 oracle로 관찰되는지 보여주는 matrix다.

## code source

- matrix row source: `SM.Editor.Validation.BalanceSweepScenarioFactory.BuildCoverageMatrixRows`
- smoke source: `BalanceSweepScenarioFactory.BuildSmokeScenarios`
- threat topology source: `BalanceSweepScenarioFactory.BuildThreatTopologyScenarios`
- Loop D source: `FirstPlayableBalanceRunner`의 `PureKit`, `SystemicSlice`, `RunLite` shard

## first playable inventory 축

| 축 | 현재 inventory | 관측 lane |
| --- | --- | --- |
| character/archetype | 12 core archetype 중심, 16 safe target catalog는 content validation 별도 추적 | smoke, threat topology, Loop D |
| encounter family | observer smoke, authored site encounter, synthesized swarm, mirror/mixed draft | smoke, threat topology, Loop D |
| build lane | mixed floor, beastkin push, topology counter, purekit topology, runlite economy | smoke, threat topology, Loop D |
| synergy | 2/4 + 2/3 grammar, team augment/permanent augment | smoke, mixed draft, sustain/burst, runlite |
| status family | direct damage, guardbreak/barrier, exposure/resistance, control/tenacity, heal/barrier, summon/swarm | threat topology, Loop D |
| readability ask | determinism, cadence, fatal/watchlist, explain stamp, top damage share | Loop D |

## scenario matrix

| lane | scenario | axis | KPI / oracle | disposition |
| --- | --- | --- | --- | --- |
| `balance-sweep-smoke` | `mixed_floor_control` | mixed paid-floor squad vs observer smoke | compile/final-state determinism, average duration, first signature cast | covered |
| `balance-sweep-smoke` | `focused_beastkin_push` | beastkin push vs observer smoke | compile/final-state determinism, average duration, first signature cast | covered |
| `balance-sweep-threat-topology` | `ArmorFrontlineScenario` | armor-heavy encounter | counter coverage, validation errors/warnings | covered |
| `balance-sweep-threat-topology` | `ResistanceShellScenario` | resistance/exposure encounter | counter coverage, validation errors/warnings | covered |
| `balance-sweep-threat-topology` | `GuardBulwarkScenario` | guardbreak/barrier encounter | counter coverage, validation errors/warnings | covered |
| `balance-sweep-threat-topology` | `EvasiveSkirmishScenario` | evasive skirmish encounter | tracking/target access review | covered |
| `balance-sweep-threat-topology` | `ControlChainScenario` | control/tenacity encounter | control readability, counter coverage | covered |
| `balance-sweep-threat-topology` | `SustainBallScenario` | sustain/heal encounter | sustain source explainability | covered |
| `balance-sweep-threat-topology` | `DiveBacklineScenario` | backline pressure and peel | backline access/peel clarity | covered |
| `balance-sweep-threat-topology` | `SwarmFloodScenario` | synthesized swarm pressure | salience overload, cleave readability | debug-only enemy synthesis |
| `loopd-purekit` | 12 `BalanceScenarioId` rows | topology pair coverage | duration, first major action, top damage share, readability fatal, explain stamp | covered |
| `loopd-systemic` | standard/dive/sustain/swarm/armor/resistance/guard/mixed subset | authored/systemic topology | readability fatal, watchlist, counter topology | covered |
| `loopd-runlite` | `RunLite_EconomyChoice` | mini-run economy | dead offer ratio, no affordable option, echo spend, protected/on-plan purchase | covered |

## uncovered 분류

| 분류 | 항목 | 처리 |
| --- | --- | --- |
| `missing scenario` | 없음. pre-art p1 topology row는 현재 2 smoke + 8 threat + 12 Loop D + 1 RunLite로 잡힌다. | 현재 Task에서 닫음 |
| `insufficient seed` | long-running full seed는 `loopd-full`/manual lane 전용이다. | runtime budget 예외 |
| `debug-only` | `SwarmFloodScenario` enemy side는 synthesized fallback이다. | authored encounter 확정 후 별도 content task |
| `deferred` | symmetric mirror 4v4 timeout/draw policy | `ManualLoopD`/별도 Decision |

## 실행 계획

기본 evidence 회수 순서는 아래를 유지한다.

```powershell
pwsh -File tools/unity-bridge.ps1 balance-sweep-smoke
pwsh -File tools/unity-bridge.ps1 loopd-slice
pwsh -File tools/unity-bridge.ps1 loopd-purekit
pwsh -File tools/unity-bridge.ps1 loopd-systemic
pwsh -File tools/unity-bridge.ps1 loopd-runlite
```

`loopd-smoke`와 `loopd-full`은 full artifact가 필요한 경우에만 사용하고, default fast/edit lane으로 재흡수하지 않는다.

## 검증

- `BalanceSweepAndBootstrapTests.BalanceSweepScenarioFactory_BuildsStableSmokeInputs`
- `BalanceSweepAndBootstrapTests.BalanceSweepScenarioFactory_BuildsAllThreatTopologyScenarios`
- `BalanceSweepAndBootstrapTests.BalanceSweepScenarioFactory_ReportsCoverageMatrixAxes`
