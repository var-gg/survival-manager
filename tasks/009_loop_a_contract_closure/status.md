# 작업 상태

## 메타데이터

- 작업명: Loop A Contract Closure
- 담당: Codex
- 상태: 완료
- 최종수정일: 2026-04-01

## Current state

- Loop A seam enum/data contract 추가 완료
- 6-slot loadout, energy cadence, targeting vocabulary, summon ownership runtime 반영 완료
- authority / summon-chain validator 추가 완료
- EditMode test와 문서 source-of-truth 갱신 완료

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 |
| --- | --- | --- | --- |
| compile | 6-slot/energy/runtime migration 후 compile green | 완료 | `pwsh -File tools/unity-bridge.ps1 compile` |
| validator | authority / cadence / summon-chain 위반을 import 단계에서 차단 | 완료 | `LoopAContractValidator`, `LoopAContractClosureTests.ContentDefinitionValidator_FlagsLoopAAuthorityAndSummonChainViolations` |
| targeted tests | A1~A7 대응 EditMode/harness oracle | 완료 | `pwsh -File tools/unity-bridge.ps1 test-edit` -> `71 passed` |
| runtime smoke | debug/read-model/docs/smoke evidence 수집 | 완료 | `pwsh -File tools/smoke-check.ps1 -RepoRoot .` |

## Evidence

- 기준 문서:
  - `tasks/006_system_deepening_pass/status.md`
  - `docs/02_design/combat/authority-matrix.md`
  - `docs/02_design/combat/resource-cadence-loadout.md`
  - `docs/02_design/combat/targeting-and-ai-vocabulary.md`
  - `docs/02_design/combat/summon-ownership-and-deployables.md`
  - `docs/03_architecture/content-authoring-and-balance-data.md`
- 실행 명령:
  - `pwsh -File tools/unity-bridge.ps1 compile`
  - `pwsh -File tools/unity-bridge.ps1 test-edit`
  - `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
  - `pwsh -File tools/docs-check.ps1 -RepoRoot .`
  - `pwsh -File tools/smoke-check.ps1 -RepoRoot .`
- 결과 요약:
  - compile green
  - `test-edit`: `Passed`, `71/71`
  - docs policy green
  - smoke check green
  - docs check red: repo-wide markdownlint backlog `912`건, 이번 task 전부터 누적된 저장소 전역 이슈 포함
- 추가된 Loop A test oracle:
  - `LoopAContractClosureTests.ContentDefinitionValidator_FlagsLoopAAuthorityAndSummonChainViolations`
  - `LoopAContractClosureTests.LoopAEnergyCadence_PrimesSignatureWithinExpectedWindow`
  - `LoopAContractClosureTests.TargetScoringService_BacklineExposedSelectorFallsBackThenSwapsAfterFrontlineDies`
  - `LoopAContractClosureTests.LoopATargetLock_KeepsCurrentTargetUntilMinimumCommitExpires`
  - `LoopAContractClosureTests.SummonKill_MirrorsOwnerWithoutOwnerEnergy_AndDirectAllyTargetingExcludesSummon`

## Closure note

- 변경 전:
  - 4-slot/mana/bias-driven tactic 계약이 문서와 runtime에 함께 남아 있었다.
  - summon/deployable ownership, mirrored kill credit, target lock vocabulary가 분산돼 있었다.
- 변경 후:
  - 6-slot/energy/authority matrix/targeting/summon ownership 계약이 코드, validator, 테스트, 문서에서 같은 의미로 읽힌다.
  - recruit/retrain은 flex slot만 바꾸고, summon은 roster/synergy count에서 빠지며, mirrored kill은 기본적으로 owner energy/on-kill proc를 만들지 않는다.

## Remaining blockers

- `docs-check`의 markdownlint 실패는 이번 task 범위를 넘는 저장소 전역 debt로 남아 있다.
- Unity가 빈 `Assets/Tests/EditMode/Temp` 폴더 메타를 다시 생성할 수 있어 commit 전 마지막 worktree 확인이 필요하다.
