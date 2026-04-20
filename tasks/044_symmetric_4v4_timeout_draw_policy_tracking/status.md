# Symmetric 4v4 timeout draw policy tracking status

## 메타데이터

- 작업명: Symmetric 4v4 timeout draw policy tracking
- 담당: Codex
- 상태: completed
- 최종수정일: 2026-04-21

## Historical snapshot / current-state implications

- 이 문서는 044 완료 시점의 historical snapshot이다.
- 현재 source-of-truth는 `docs/TESTING.md`, `docs/03_architecture/combat-harness-and-debug-contract.md`, `docs/03_architecture/first-playable-balance-targets.md`, `docs/03_architecture/sim-sweep-and-balance-kpis.md`를 우선한다.
- 044는 033의 FastUnit asymmetric end oracle과 symmetric mirror 4v4 timeout/draw policy를 분리해 추적하는 작업이다.
- `LoopASymmetricMirrorPolicyTests`는 explicit `ManualLoopD` probe이며 default fast closure evidence가 아니다.

## Current state

- `BattleResolutionTests.LoopA_4v4_AsymmetricBattleEndsBeforeTimeout`는 FastUnit deterministic end oracle로 유지한다.
- `LoopASymmetricMirrorPolicyTests.SymmetricMirror4v4_ReportsCurrentOutcomeWithoutEnforcingDrawPolicy`를 추가해 현재 symmetric mirror outcome, step count, timeout 여부, 생존 수를 manual lane에서 관찰한다.
- 새 probe는 draw/balance policy를 강제하지 않는다. 정책 확정은 Loop D balance review 또는 별도 balance task에서 수행한다.

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 |
| --- | --- | --- | --- |
| FastUnit oracle 보존 | asymmetric deterministic end test 유지 | 완료 | `BattleResolutionTests` |
| policy 분리 | symmetric mirror policy는 FastUnit closure가 아님을 명시 | 완료 | docs/task diff |
| manual probe | explicit `ManualLoopD` test 추가 | 완료 | `LoopASymmetricMirrorPolicyTests` |
| validation | fast/lint/docs validation pass | 완료 | evidence |

## Evidence

- `LoopASymmetricMirrorPolicyTests`는 `[Explicit]` + `[Category("ManualLoopD")]`로 추가했다.
- `pwsh -File tools/test-harness-lint.ps1 -RepoRoot .`: pass.
- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`: pass, `152 total / 149 passed / 0 failed / 3 skipped`.
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`: pass.
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`: pass.
- `npx --yes markdownlint-cli2 "**/*.md" "#Library/**" "#Logs/**" "#.git/**"`: pass, `386 file(s)`.
- targeted docs check for changed docs/tasks: pass.

## Remaining blockers

- 없음.

## Deferred / debug-only

- symmetric mirror 4v4 timeout/draw threshold 자체를 fail gate로 확정하지 않았다.
- Full Loop D balance sweep, authored content retune, presentation smoke는 이번 작업 범위가 아니다.

## Loop budget consumed

- probe design retry: 0/2
- validation retry: 0/2
- docs-check retry: 0/1

## Handoff notes

- 044는 tracking split이다. combat formula, authored balance data, scene/prefab은 수정하지 않는다.
