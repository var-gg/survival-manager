# 작업 상태

## 메타데이터

- 작업명: Loop C Content Governance Closure
- 담당: Codex
- 상태: 진행 중
- 최종수정일: 2026-04-01

## Current state

- Loop C 코드/asset/validator 기본 구현은 들어갔다.
- content validation은 green으로 수렴했고, town UI와 dev sandbox UI 경계도 분리됐다.
- 지금 handoff는 가능하지만 docs/index/test evidence 최종 동기화가 남아 있다.

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 / 다음 확인 |
| --- | --- | --- | --- |
| compile | Loop C schema와 UI 변경 후 compile green | 완료 | `pwsh -File tools/unity-bridge.ps1 compile` |
| validator | Budget/rarity/counter/forbidden drift 0건 | 완료 | `ValidationBatchEntryPoint.RunContentValidation()` |
| targeted tests | Loop C EditMode oracle 추가와 통과 | 부분 | test 추가 완료, runner 결과 재회수 필요 |
| runtime smoke | sandbox/debug에서 governance summary 확인 | 완료 | Recruitment/Combat sandbox dev summary 추가 |

## Evidence

- 핵심 코드:
  - `Assets/_Game/Scripts/Runtime/Unity/RuntimeCombatContentFileParser.cs`
  - `Assets/_Game/Scripts/Editor/Validation/LoopCValidationPasses.cs`
  - `Assets/_Game/Scripts/Editor/Validation/BalanceSweepScenarioFactory.cs`
  - `Assets/_Game/Scripts/Editor/Authoring/RecruitmentSandbox/RecruitmentSandboxWindow.cs`
  - `Assets/_Game/Scripts/Editor/Authoring/CombatSandbox/CombatSandboxWindow.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/TownScreenController.cs`
- 핵심 artifact:
  - `Logs/content-validation/content-validation-report.json`
  - `Logs/content-validation/content_budget_audit.md`
  - `Logs/content-validation/counter_coverage_matrix.md`
  - `Logs/content-validation/v1_forbidden_feature_report.md`
- 관련 문서:
  - `docs/02_design/systems/content-budgeting-contract.md`
  - `docs/02_design/systems/rarity-ladder-contract.md`
  - `docs/02_design/combat/counter-system-topology.md`
  - `docs/02_design/systems/v1-forbidden-list.md`

## Remaining blockers

- `test-edit`와 `test-play` 최종 결과를 다시 회수해야 한다.
- docs policy / docs check / smoke check를 새 문서 포함 상태로 다시 돌려야 한다.

## Deferred / debug-only

- telemetry 기반 pruning과 readability gate
- first playable balance pass의 수치 재조정
- item loot rarity 문서군의 통합 정리

## Loop budget consumed

- compile-fix: 2
- refresh/read-console: 1
- asset authoring retry: 1
- budget 초과 시 남긴 diagnosis:
  - validator green 이후 EditMode runner channel이 다시 닫히면 결과 회수 자체를 별도
    환경 blocker로 취급한다.

## Handoff notes

- 다음 세션 시작 문서:
  - `tasks/011_loop_c_content_governance_closure/status.md`
  - `tasks/011_loop_c_content_governance_closure/spec.md`
  - `docs/02_design/systems/content-budgeting-contract.md`
  - `docs/03_architecture/content-authoring-and-balance-data.md`
- 일반 town UI에는 `ContentRarity`를 직접 다시 노출하지 않는다.
- `RecruitTier`를 runtime combat template/snapshot에 싣는 회귀를 막아야 한다.
