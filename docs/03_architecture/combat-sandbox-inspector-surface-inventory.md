# Combat Sandbox Inspector Surface Inventory

- 상태: active
- 소유자: repository
- 최종수정일: 2026-06-14
- 소스오브트루스: `docs/03_architecture/combat-sandbox-inspector-surface-inventory.md`
- 관련문서:
  - `docs/03_architecture/editor-sandbox-tooling.md`
  - `docs/03_architecture/combat-harness-and-debug-contract.md`
  - `docs/03_architecture/readability-gate-contract.md`
  - `pindoc://task-combat-sandbox-inspector-closure`

## 목적

Combat Sandbox가 pre-art 상태에서 같은 입력과 같은 결과를 재현하고, 실패 이유를 읽을 수 있는지 확인하는 inventory다.
전투 truth는 `SM.Combat`와 compiled runtime spec이 소유하고, inspector/window/HUD는 입력 조립과 결과 확인만 담당한다.

## 현재 표면

| 표면 | 코드 좌표 | 제공 값 | 판정 |
| --- | --- | --- | --- |
| active handoff inspector | `SM.Editor.Authoring.Inspectors.CombatSandboxConfigEditor` | scenario metadata, left/right team, seed, batch count, side swap, replay/readability flags, `Compile Preview`, `Push Active`, `Run Single`, `Run Batch`, `Run Side Swap`, `Push Active + Play` | pre-art p1 |
| sandbox window | `SM.Editor.Authoring.CombatSandbox.CombatSandboxWindow` | preset library, preview, breakpoint, baseline drift, first playable membership, result cache | pre-art p1 |
| execution service summary | `SM.Editor.Authoring.CombatSandbox.CombatSandboxExecutionService` | counter coverage, governance, readability severity, explanation, structured cinematic moments, provenance | pre-art p1 |
| launch truth diff | `SM.Editor.Authoring.CombatSandbox.CombatSandboxLaunchTruthDiffService` | slot/equipment/passive-board/augment/posture/tactic/out-of-roster drift | pre-art p1 |
| runtime direct lane | `SM.Unity.Sandbox.CombatSandboxSceneController` | replay hash, metrics, batch result, layout source | pre-art p1 |
| battle HUD debug overlay | `SM.Unity.BattleScreenController` | step/time/target/action/selected unit/decisive timeline/debug lines | debug-only |

## raw vs compiled 흐름

raw authoring 값은 `CombatSandboxConfig`, scenario asset, team preset, build override, execution preset에 남는다.
compiled effective value는 `CombatSandboxScenarioCompiler`가 `BattleLoadoutSnapshot`으로 만들고, inspector/window 결과 표면은 compile hash와 provenance를 함께 표시한다.
slot, equipment, passive-board, augment, posture/tactic drift는 `CombatSandboxLaunchTruthDiffService`가 baseline과 compiled snapshot을 비교한다.

## 재현성 knobs

| knob | source | 소비 지점 | 판정 |
| --- | --- | --- | --- |
| active handoff | `combat_sandbox_active.asset` | direct sandbox runtime lane | pre-art p1 |
| seed | `CombatSandboxExecutionSettings.Seed` 또는 state override | `CombatSandboxRunRequest.Seed` | pre-art p1 |
| batch count | execution preset/state | `CombatSandboxRunRequest.BatchCount` | pre-art p1 |
| side swap | execution preset/inspector button | inspector/window run summary | pre-art p1 |
| replay flag | execution preset | replay bundle/result surface | pre-art p1 |
| readability flag | execution preset | readability summary/watchlist surface | pre-art p1 |
| scene layout source | scene controller/default layout | `LayoutSourceLabel` | pre-art p1 |

## 결과 surface matrix

| 값 | 현재 표면 | 상태 |
| --- | --- | --- |
| compile hash | inspector/window result label | covered |
| replay hash | inspector/window result label | covered |
| metrics | win rate, average duration, event count, first action | covered |
| breakpoint/drift/membership | preview/run diff summaries | covered |
| governance | rarity, role profile, budget, threats, counters, flags | covered |
| readability | salience, unexplained damage, target switch, severity counts, violations | covered |
| explanation | top damage, top decision reasons, decisive strings, cinematic moments | covered |
| provenance | subject artifact counts and source ids | covered |
| validation | preview validation message | covered |

## deferred 표면

- final UX polish, 그래픽/오디오 품질, scene/prefab authoring은 이 inventory의 완료 조건이 아니다.
- battle HUD의 상세 visual capture는 PlayMode smoke/visual QA lane에서 회수한다.
- window binding 자체는 Unity editor 상태에 민감하므로 long-running window smoke가 아니라 formatter/diff focused test로 계약을 고정한다.

## 검증

- `CombatSandboxLaunchTruthDiffTests.CombatSandboxLaunchTruthDiffService_FlagsBaselineAndOutOfRosterScopeUnits`
- `CombatSandboxLaunchTruthDiffTests.CombatSandboxLaunchTruthDiffService_DetectsSlotEquipmentPassiveAugmentAndPostureDrift`
- `CombatSandboxLaunchTruthDiffTests.CombatSandboxExecutionSummaries_SurfaceReadabilitySeverityAndCinematicMoments`
