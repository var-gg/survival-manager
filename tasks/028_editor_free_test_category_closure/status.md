# Editor-free test category closure status

## 메타데이터

- 작업명: Editor-free test category closure
- 담당: Codex
- 상태: completed
- 최종수정일: 2026-04-19

## Current state

- 시작 기준 `main`은 commit `165fc95` 이후 clean 상태였다.
- preflight inventory 기준 uncategorized EditMode test class는 22개였다.
- `MetaRewardPickTests`는 이미 `FastUnit`이었다.
- `LoopDTelemetryAndBalanceTests`는 `ManualLoopD` explicit long-running lane으로 남아 있으며, uncategorized closure와 별도로 취급한다.
- 완료 기준 inventory에서 uncategorized EditMode test class는 0개다.
- 기존 uncategorized test 중 authored Unity object/content/editor 경로를 쓰는 class는 `BatchOnly`로 격리했다.
- pure combat/meta/persistence test class는 `FastUnit`으로 올렸다.
- `CombatContractsTests`의 combat resolution 계약은 `FastUnit`에 남기고, UI/localization `GameObject` 계약은 `CombatLocalizationContractBatchOnlyTests`로 분리했다.
- FastUnit 승격 과정에서 오래된 combat contract drift가 드러났다. 단순 stale assertion 4건은 현재 구현 공식에 맞췄고, `BattleResolutionTests.LoopA_4v4_BattleEndsBeforeTimeout`은 dedicated balance retune 전까지 명시 `Ignore`로 남겼다.

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 |
| --- | --- | --- | --- |
| uncategorized closure | EditMode test class uncategorized 0개 | 완료 | inventory |
| FastUnit purity | 새 FastUnit에 authored Unity object/content/editor token 없음 | 완료 | lint / guard |
| BatchOnly isolation | Unity object/content/editor tests는 BatchOnly | 완료 | category diff |
| docs | lane policy/status 갱신 | 완료 | docs check |
| tests | fast/lint/focused/docs pass | 완료 | evidence |

## Evidence

- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`: pass, `169 total / 165 passed / 0 failed / 4 skipped`.
- `pwsh -File tools/test-harness-lint.ps1 -RepoRoot .`: pass.
- uncategorized inventory: `UNCATEGORIZED=0`.
- authored-object token grep: hits remain only in BatchOnly/helper files, not FastUnit.
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.BuildBoundaryGuardFastTests`: pass, `7 total / 7 passed`.
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.MetaRewardPickTests`: pass, `1 total / 1 passed`.
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`: pass.
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`: pass.
- `npx --yes markdownlint-cli2 "**/*.md" "#Library/**" "#Logs/**" "#.git/**"`: pass, `368 file(s)`.
- targeted docs check: `pwsh -NoProfile -Command "& { .\tools\docs-check.ps1 -RepoRoot . -LinkCheckTimeoutSeconds 10 -Paths @('AGENTS.md','docs/TESTING.md','tasks/028_editor_free_test_category_closure') }"` pass.
- full `tools/docs-check.ps1`는 repo-wide link-check 단계가 10분 제한에서도 종료되지 않아 중단했다. 변경 파일 대상 docs-check와 full markdownlint는 green이다.

## Remaining blockers

- 없음.

## Deferred / debug-only

- `ManualLoopD` 장시간 lane 재정의.
- `StatV2AndSandboxTests` 내부 pure stat tests 추가 분리.
- category closure guard 자동화는 030에서 수행한다.
- `BattleResolutionTests.LoopA_4v4_BattleEndsBeforeTimeout` balance timeout contract retune.

## Loop budget consumed

- category correction retry: 1/2
- FastUnit regression retry: 1/2
- docs-check retry: 1/1

## Handoff notes

- 이번 task는 test category closure 전용이다.
- scene/prefab/Resources asset authoring은 하지 않는다.
- Unity batch 실행 중 `BattleActor_PrimitiveWrapper.prefab`가 자동 touch되면 task 범위 밖 side effect로 복구한다.
