# Task 017 Status

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-08
- 소스오브트루스: `tasks/017_live_truth_town_sheet_sandbox_closure/status.md`

## Current state

- Town 5-panel character sheet, launch-core roster docs, sandbox drift preview, localization key, FastUnit 대상 테스트 추가까지 패치를 적용했다
- 이번 세션 기준 자동 검증은 `docs-policy-check`, `smoke-check`, 변경 범위 대상 `markdownlint`는 통과했고, `test-batch-fast`와 repo-wide `docs-check`는 외부 blocker 때문에 아직 green을 확보하지 못했다

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 / 다음 확인 |
| --- | --- | --- | --- |
| compile | Town/Sandbox/UI/localization compile 유지 | blocker | `pwsh -File tools/unity-bridge.ps1 test-batch-fast`가 project lock으로 실패했고 `pwsh -File tools/unity-bridge.ps1 status`는 `port 8090` stale heartbeat를 보고한다 |
| validator | docs/localization/smoke check 통과 | 부분 통과 | `docs-policy-check` 통과, `smoke-check` 통과, 변경 범위 대상 `markdownlint` 통과, repo-wide `docs-check`는 기존 누적 lint 63건으로 실패 |
| targeted tests | Town sheet / resolver / sandbox drift FastUnit | 구현 완료 / 실행 blocker | `TownCharacterSheetFormatterTests`, `CharacterAxisLocalizationTests`, `CombatSandboxLaunchTruthDiffTests`를 추가/갱신했지만 batch test 재실행은 project lock 해소가 필요하다 |
| runtime smoke | Town locale flip / sandbox preview 수동 확인 | blocker | 열린 Unity editor와 stale connector 때문에 수동 smoke를 아직 수행하지 못했다 |

## Evidence

- 코드: `Assets/_Game/Scripts/Runtime/Unity/UI/Town/*`
- 코드: `Assets/_Game/Scripts/Editor/Authoring/CombatSandbox/*`
- 문서: `docs/02_design/deck/launch-core-roster-sheet.md`
- 문서: `docs/02_design/ui/town-character-sheet-ui.md`
- 문서: `docs/03_architecture/town-character-sheet-contract.md`
- 검증: `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .` 통과
- 검증: `pwsh -File tools/smoke-check.ps1 -RepoRoot .` 통과
- 검증: `npx markdownlint-cli2 docs/index.md docs/02_design/index.md docs/03_architecture/index.md docs/03_architecture/editor-sandbox-tooling.md docs/02_design/deck/launch-core-roster-sheet.md docs/02_design/ui/town-character-sheet-ui.md docs/03_architecture/town-character-sheet-contract.md tasks/017_live_truth_town_sheet_sandbox_closure/*.md` 통과
- blocker: `pwsh -File tools/unity-bridge.ps1 test-batch-fast` -> `It looks like another Unity instance is running with this project open.`
- blocker: `pwsh -File tools/unity-bridge.ps1 status` -> `Unity (port 8090): not responding (last heartbeat 117h44m9s ago)`
- blocker: `pwsh -File tools/docs-check.ps1 -RepoRoot .` -> 저장소 전체 markdownlint 63건 실패 (`docs/02_design/meta/augment-catalog-v1.md`, `docs/03_architecture/testing-strategy.md`, `.claude/worktrees/loving-morse/**`, `Packages/com.coplaydev.unity-mcp/README.md`, `CLAUDE.md`)

## Remaining blockers

- 열린 Unity editor가 프로젝트를 점유 중이라 `test-batch-fast`를 다시 돌릴 수 없다
- `unity-cli` connector가 `port 8090` stale heartbeat 상태라 `compile`, `status`, manual smoke dispatch를 신뢰할 수 없다
- repo-wide `docs-check`는 이번 패치와 무관한 기존 markdownlint 누적 실패가 남아 있다

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
- 그 다음 `SM/Play/Full Loop` Town locale flip, `SM/Authoring/Combat Sandbox` preview/result drift, `SM/Play/Combat Sandbox` direct lane smoke를 수동 확인한다
- repo-wide `docs-check`를 green으로 만들려면 이번 task 범위 밖의 기존 lint debt를 별도 정리해야 한다
