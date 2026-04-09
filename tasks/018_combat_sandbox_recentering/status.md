# 작업 상태

## 메타데이터

- 작업명: Combat Sandbox Re-centering
- 담당: Codex
- 상태: active
- 최종수정일: 2026-04-09

## Current state

- menu/runtime/authoring/display policy 재중심화 코드를 반영했다.
- `SM/Play`는 fail-fast preflight + launch 경로로 축소했고, recovery/content/validation 메뉴는 `SM/Internal/*`로 이동시켰다.
- Combat Sandbox active handoff는 Inspector-first 편집 경로로 승격했고, `Window/SM/Combat Sandbox`는 library/history/results 보조 surface로 축소했다.
- `GameBootstrap` normal playable path는 `Resources.LoadAll(...)`만 허용하도록 고정했고, editor filesystem fallback 문구는 diagnostic/internal lane 전용으로 정리했다.
- `CombatSandboxConfigEditor`의 `Compile Preview` / `Run Single` / `Run Batch` / `Run Side Swap`은 이제 `ApplyModifiedProperties()` + dirty까지만 수행하고, 실제 디스크 save는 `Push Active` 또는 명시적 save에서만 일어난다.
- preview compile path는 `CombatSandboxCompilationContextFactory`를 통해 saved profile snapshot 기반으로 재구성되고, `CombatSandboxEditorSession`은 더 이상 preview/build를 위해 `GameSessionState`를 직접 만들지 않는다.
- 내부 bootstrap/request/reload naming은 `CombatSandbox` 기준으로 정리하기 시작했고, legacy `QuickBattle` key/method는 compatibility wrapper만 남겨 두었다.
- fresh execution evidence는 batchmode + direct connector HTTP 우회로 다시 회수했다. compile 증거는 확보됐지만 `SM/Play/Combat Sandbox`와 `SM/Play/Full Loop`는 각각 active handoff import 불가와 Boot scene missing component로 실패했다.

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 / 다음 확인 |
| --- | --- | --- | --- |
| compile | sandbox/menu/runtime refactor 후 compile 유지 | 부분 완료 | `2026-04-09 11:13:27 +09:00` `test-batch-fast`가 compile 후 실제 테스트까지 진입했다. `2026-04-09 11:24:05 +09:00` direct connector `refresh_unity`도 `ready + compileErrors=false`를 기록했다. |
| validator | legacy alias 제거와 docs/index sync 유지 | 부분 완료 | `test-harness-lint`, `docs-policy-check`, `smoke-check`, targeted `git diff --check` 통과. `docs-check`는 repo pre-existing markdownlint debt와 `.claude/worktrees/**` 복사본 때문에 계속 실패 |
| targeted tests | sandbox preflight/cache/layout smoke 보강 | 부분 완료 | `test-batch-fast`는 이제 project lock blocker를 넘겼고, 7개 failing test를 구체적으로 식별했다. 다음 라운드는 해당 7건 triage가 필요하다. |
| runtime smoke | direct sandbox / Town smoke / Full Loop 분리 유지 | 실패 | `SM/Play/Combat Sandbox`는 active handoff asset import 실패로 preflight 중단, `SM/Play/Full Loop`는 `Boot.unity`의 `BootScreenController` missing component로 preflight 실패 |

## Evidence

- source-of-truth task 생성:
  - `tasks/018_combat_sandbox_recentering/spec.md`
  - `tasks/018_combat_sandbox_recentering/plan.md`
  - `tasks/018_combat_sandbox_recentering/status.md`
- 코드/문서 반영:
  - `Assets/_Game/Scripts/Editor/Bootstrap/FirstPlayableBootstrap.cs`
  - `Assets/_Game/Scripts/Editor/Bootstrap/LocalizationFoundationBootstrap.cs`
  - `Assets/_Game/Scripts/Editor/Authoring/CombatSandbox/CombatSandboxAuthoringAssetUtility.cs`
  - `Assets/_Game/Scripts/Editor/Authoring/Inspectors/CombatSandboxConfigEditor.cs`
  - `Assets/_Game/Scripts/Editor/Authoring/CombatSandbox/CombatSandboxEditorSession.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/GameBootstrap.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/GameSessionRoot.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/GameSessionState.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/FirstPlayableRuntimeSceneBinder.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/Sandbox/CombatSandboxCompilationContextFactory.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/Sandbox/CombatSandboxSceneController.cs`
  - `docs/03_architecture/editor-sandbox-tooling.md`
  - `docs/03_architecture/unity-scene-flow.md`
  - `docs/05_setup/local-runbook.md`
  - `docs/06_production/current-known-issues.md`
- 실행 증거:
  - `2026-04-09 11:13:27 +09:00` `pwsh -File tools/unity-bridge.ps1 test-batch-fast` -> fail, 하지만 compile 후 실제 FastUnit 실행까지 도달했다. 결과 파일: `A:\projects\game\survival-manager\TestResults-Batch.xml`
  - `test-batch-fast` failing test 7건:
    - `SM.Tests.EditMode.BattlePresentationSnapshotTests.RenderSnapshot_DoesNotReplayDiscreteCueCount`
    - `SM.Tests.EditMode.BattlePresentationTransientSurfaceTests.AdvanceStep_FiresTransientSurfaces_ButSnapshotResetDoesNotReplayThem`
    - `SM.Tests.EditMode.BattleTimelineControllerTests.NormalizedProgress_Reflects_Current_Position`
    - `SM.Tests.EditMode.CharacterAxisLocalizationTests.BattleUnitMetadataFormatter_BuildsAxisSummaryFromCharacterHierarchy`
    - `SM.Tests.EditMode.PermanentAugmentLifecycleTests.BindProfile_FiltersLegacySlotTokens_AndClampsLegacyLoadout`
    - `SM.Tests.EditMode.RuntimeCombatContentLookupModeTests.RuntimeCombatContentLookup_CanOptIntoEditorRecoveryFallback`
    - `SM.Tests.EditMode.RuntimeCombatContentLookupModeTests.RuntimeCombatContentLookup_DefaultsToResourcesOnlyMode`
  - `2026-04-09 11:24:05.017 +09:00` direct connector POST `refresh_unity { compile = request }` -> success, heartbeat `ready`, `compileErrors=false`. 근거: `C:\Users\curioustore\.unity-cli\instances\51319016d32ea605.json`
  - `2026-04-09 11:26:31.303 +09:00` direct connector POST `menu 'SM/Play/Combat Sandbox'` -> fail. console:
    - `[CombatSandbox] Failed: Combat Sandbox active handoff is missing: Assets/Resources/_Game/Content/Definitions/QuickBattle/quick_battle_default.asset`
    - `InvalidOperationException: Combat Sandbox active handoff is missing: Assets/Resources/_Game/Content/Definitions/QuickBattle/quick_battle_default.asset`
  - `2026-04-09 11:32:33 +09:00` direct connector POST `menu 'SM/Play/Full Loop'` -> fail. editor state는 `Boot.unity`까지 이동했지만 preflight가 scene contract에서 중단됐다. console:
    - `[FirstPlayableRuntimeSceneBinder] Boot scene contract is incomplete. Repair via SM/Internal/Recovery/Repair First Playable Scenes.`
    - `[FullLoop] Failed: Scene repair failed. Missing component 'BootScreenController' on 'BootScreenController' in Assets/_Game/Scenes/Boot.unity`
  - connector 참고 로그:
    - heartbeat: `C:\Users\curioustore\.unity-cli\instances\51319016d32ea605.json`
    - batch test result: `A:\projects\game\survival-manager\TestResults-Batch.xml`
    - editor log: `C:\Users\curioustore\AppData\Local\Unity\Editor\Editor.log`
- 검증:
  - `pwsh -File tools/test-harness-lint.ps1 -RepoRoot .` -> pass
  - `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .` -> pass
  - `pwsh -File tools/smoke-check.ps1 -RepoRoot .` -> pass
  - `git diff --check -- docs tasks tools/unity-bridge.ps1 tools/mcp/coplay-connection-runbook.md Assets/_Game/Scripts/Editor/Authoring/CombatSandbox Assets/_Game/Scripts/Editor/Authoring/Inspectors Assets/_Game/Scripts/Editor/Bootstrap Assets/_Game/Scripts/Editor/SeedData Assets/_Game/Scripts/Editor/Validation Assets/_Game/Scripts/Runtime/Unity Assets/Localization/StringTables Assets/Tests/EditMode` -> pass
  - `pwsh -File tools/docs-check.ps1 -RepoRoot .` -> fail (repo pre-existing markdownlint debt in `docs/02_design/meta/augment-catalog-v1.md`, `docs/03_architecture/testing-strategy.md`, `CLAUDE.md`, `.claude/worktrees/**`, `Packages/com.coplaydev.unity-mcp/README.md`)

## Remaining blockers

- 기존 dirty worktree가 scene/authoring asset 쪽에 열려 있으므로 관련 파일 편집 시 사용자 변경을 보존해야 한다.
- `unity-cli` 0.3.5의 `status` / `--project` auto-discovery는 현재 프로젝트 heartbeat를 잘못 읽고 `133h stale`로 오판한다. 반면 connector HTTP 서버(`127.0.0.1:8090`)와 heartbeat 파일은 정상이라서, 이번 라운드는 direct POST로 우회했다.
- `Assets/Resources/_Game/Content/Definitions/QuickBattle/quick_battle_default.asset`는 파일이 디스크에 존재하고 GUID도 있으나 `AssetDatabase.LoadMainAssetAtPath`가 null을 반환한다. 동일 타입의 새 diagnostic asset은 정상 import되므로, 이 handoff YAML은 현행 스키마 기준 재serialize/복구가 필요하다.
- `Boot.unity`는 `BootScreenController` missing component 때문에 `SM/Play/Full Loop` preflight를 통과하지 못한다. 현재 dirty scene 변경과 충돌 가능성이 있어 scene repair는 사용자 변경을 보존하면서 처리해야 한다.
- `test-batch-fast`는 compile blocker를 넘겼지만 7 failing test가 남아 있다. 특히 edit-mode `Destroy` 사용과 `RuntimeCombatContentLookup` 기본 생성자 계약 변경이 눈에 띈다.
- `docs-check`는 이번 작업 범위 밖의 기존 markdownlint debt와 `.claude/worktrees/**` 복사본까지 스캔해 실패한다.

## Deferred / debug-only

- safe area 실제 runtime adapter 연결
- `GameSessionState` 대규모 2차 분해
- broader sandbox history/compare UX

## Loop budget consumed

- compile-fix: 2 (`CombatSandboxSceneController` missing using, `FirstPlayableRuntimeSceneBinder` ambiguous `Debug`)
- refresh/read-console: 3 (`test-batch-fast` 재실행, direct connector compile, direct connector menu/console 회수)
- asset authoring retry: 1 (`quick_battle_default.asset` import 진단, minimal `m_Script` repair 시도)
- budget 초과 시 남긴 diagnosis: connector stale 판정은 우회했지만, handoff asset import 불가 + Boot scene missing component + 7 failing fast tests가 새 concrete blocker로 남았다.

## Handoff notes

- 다음 세션 시작 문서:
  - `tasks/018_combat_sandbox_recentering/status.md`
  - `docs/03_architecture/editor-sandbox-tooling.md`
  - `docs/03_architecture/unity-scene-flow.md`
- legacy alias를 다시 노출하지 않는다.
- runtime binder는 self-healing 확대가 아니라 typed contract 축소 방향으로만 수정한다.
- 다음 validation 우선순위:
  - `quick_battle_default.asset`를 현행 `CombatSandboxConfig` 스키마로 복구해서 `AssetDatabase.LoadMainAssetAtPath`가 다시 살아나는지 확인
  - `Boot.unity`의 `BootScreenController` missing component 원인을 사용자 dirty scene 변경과 충돌 없이 정리
  - `pwsh -File tools/unity-bridge.ps1 test-batch-fast` failing 7건 triage
  - `SM/Play/Combat Sandbox`와 `SM/Play/Full Loop`를 다시 실행해 direct connector 우회 없이도 증거가 회수되는지 확인
