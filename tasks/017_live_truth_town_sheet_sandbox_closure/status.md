# Task 017 Status

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-09
- 소스오브트루스: `tasks/017_live_truth_town_sheet_sandbox_closure/status.md`

## Current state

- Town 5-panel character sheet 위에 shared `LaunchCoreRosterBaselineCatalog`를 추가해 Town formatter와 sandbox drift가 같은 baseline 해석을 공유하도록 맞췄다
- Town character sheet body label/state text를 `ui.town.sheet.*` key로 전면 현지화했고 `UI_Town` shared/en/ko table을 같이 갱신했다
- sandbox drift preview는 category 나열을 넘어서 slot/tactic/provenance 기준 exact delta 문장을 보여 주도록 올렸다
- 이번 세션 기준 자동 검증은 `tools/test-harness-lint.ps1`는 통과했고, `test-batch-fast`와 Unity connector 기반 compile/smoke는 외부 blocker 때문에 아직 fresh green을 확보하지 못했다

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 / 다음 확인 |
| --- | --- | --- | --- |
| compile | Town/Sandbox/UI/localization compile 유지 | blocker | `pwsh -File tools/unity-bridge.ps1 test-batch-fast`가 project lock으로 실패했고 `pwsh -File tools/unity-bridge.ps1 status`는 `port 8090` stale heartbeat를 보고한다 |
| validator | baseline/localization/test harness 정합 유지 | 부분 통과 | `tools/test-harness-lint.ps1` 통과, `UI_Town` shared key coverage는 현재 formatter 기준으로 모두 채웠다, live compile/smoke는 Unity blocker로 미실행 |
| targeted tests | Town sheet / resolver / sandbox drift FastUnit | 구현 완료 / 실행 blocker | `TownCharacterSheetFormatterTests`, `CharacterAxisLocalizationTests`, `CombatSandboxLaunchTruthDiffTests`를 갱신했지만 batch test 재실행은 project lock 해소가 필요하다 |
| runtime smoke | Town locale flip / sandbox preview 수동 확인 | blocker | 열린 Unity editor와 stale connector 때문에 수동 smoke를 아직 수행하지 못했다 |

## Evidence

- 코드: `Assets/_Game/Scripts/Runtime/Unity/UI/Town/*`
- 코드: `Assets/_Game/Scripts/Runtime/Unity/LaunchCoreRosterBaselineCatalog.cs`
- 코드: `Assets/_Game/Scripts/Editor/Authoring/CombatSandbox/*`
- 문서: `docs/03_architecture/town-character-sheet-contract.md`
- 문서: `docs/03_architecture/editor-sandbox-tooling.md`
- 검증: `pwsh -File tools/test-harness-lint.ps1` 통과
- blocker: `pwsh -File tools/unity-bridge.ps1 test-batch-fast` -> `It looks like another Unity instance is running with this project open.`
- blocker: `pwsh -File tools/unity-bridge.ps1 status` -> `Unity (port 8090): not responding (stale heartbeat)`

## Remaining blockers

- 열린 Unity editor가 프로젝트를 점유 중이라 `test-batch-fast`를 다시 돌릴 수 없다
- `unity-cli` connector가 `port 8090` stale heartbeat 상태라 `compile`, `status`, manual smoke dispatch를 신뢰할 수 없다

## Deferred / debug-only

- starter equipment package runtime bake-in
- boss/mirror sandbox library
- progression history timeline UI

## Loop budget consumed

- compile-fix: 1
- refresh/read-console: 2
- asset authoring retry: 0

## Handoff notes

- 다음 세션 시작점은 `tasks/017_live_truth_town_sheet_sandbox_closure/status.md`
- Unity project lock을 해소한 뒤 `pwsh -File tools/unity-bridge.ps1 test-batch-fast`를 먼저 재실행한다
- 그 다음 `SM/전체테스트` Town locale flip, `SM/Authoring/Combat Sandbox` preview/result drift, `SM/전투테스트` direct lane smoke를 수동 확인한다
- repo-wide `docs-check`를 green으로 만들려면 이번 task 범위 밖의 기존 lint debt를 별도 정리해야 한다
