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
- fresh execution evidence는 direct connector HTTP 우회로 다시 회수했다. `SM/전체테스트`는 now clean하게 `Boot.unity`까지 진입하고 console error 없이 통과한다.
- direct Combat Sandbox active handoff는 `Assets/Resources/_Game/Content/Definitions/QuickBattle/quick_battle_default.asset` 대신 `Assets/_Game/Authoring/CombatSandbox/combat_sandbox_active.asset`를 editor-authoring lane의 단일 handoff로 보도록 옮겼다.
- `quick_battle_default.asset`의 직접 import blocker는 한 차례 넘겼고 preflight는 scene contract -> canonical content -> active handoff instability 순으로 blocker가 이동했다.
- `CombatSandboxConfig` / layout / preview 관련 script meta와 file/class 계약을 부분 복구했지만, broader asset refresh 또는 `SM/Internal/Content/Ensure Sample Content` 이후 active handoff가 다시 `AssetDatabase` null로 되돌아가는 instability가 남아 있다.
- `RuntimeCombatContentFileParser` fallback에서 `CharacterDefinition` 식별자 추출이 빠져 canonical character YAML이 전부 버려지고 있었다. `YamlFieldExtractor`에 character id/name fallback을 추가한 뒤 fallback snapshot의 `Characters`가 다시 채워진다.
- `Battle.unity`의 `BattleScreenController.presentationController`는 실제 `BattlePresentationController`가 아니라 `<missing>` component 슬롯을 참조하고 있었다. 현재는 valid controller 슬롯으로 재연결했고 direct Combat Sandbox lane은 Battle scene 진입 후 console error 0건까지 확인했다.
- `BattleActorPresentationCatalog`의 runtime fallback은 static cache를 재사용하지 않도록 바꿨다. direct Combat Sandbox는 Battle scene 재진입 2회 연속에서도 stale wrapper cache 없이 clean 진입한다.

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 / 다음 확인 |
| --- | --- | --- | --- |
| compile | sandbox/menu/runtime refactor 후 compile 유지 | 부분 완료 | `2026-04-09 11:13:27 +09:00` `test-batch-fast`가 compile 후 실제 테스트까지 진입했다. `2026-04-09 11:24:05 +09:00` direct connector `refresh_unity`도 `ready + compileErrors=false`를 기록했다. |
| validator | legacy alias 제거와 docs/index sync 유지 | 부분 완료 | `test-harness-lint`, `docs-policy-check`, `smoke-check`, targeted `git diff --check` 통과. `docs-check`는 repo pre-existing markdownlint debt와 `.claude/worktrees/**` 복사본 때문에 계속 실패 |
| targeted tests | sandbox preflight/cache/layout smoke 보강 | 부분 완료 | `test-batch-fast`는 이제 project lock blocker를 넘겼고, 7개 failing test를 구체적으로 식별했다. 다음 라운드는 해당 7건 triage가 필요하다. |
| runtime smoke | direct sandbox / Town smoke / Full Loop 분리 유지 | 완료 | `SM/전체테스트`는 `2026-04-09 17:03:58 +09:00`에 `Boot.unity`까지 clean 진입했다. `SM/전투테스트`는 이번 라운드 latest re-entry check(`2026-04-09 22:26:35 +09:00`)에서 Battle scene 진입 + `console` error 0건을 기록했고, stop 후 재실행 반복에서도 같은 결과를 유지했다. |

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
  - `Assets/_Game/Scripts/Runtime/Unity/Sandbox/CombatSandboxConfig.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/Sandbox/CombatSandboxSlotTypes.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/Sandbox/CombatSandboxCompilationContextFactory.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/Sandbox/CombatSandboxAnchorPose.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/Sandbox/CombatSandboxSceneLayoutAsset.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/Sandbox/CombatSandboxPreviewSettingsAsset.cs`
  - `Assets/_Game/Scripts/Runtime/Unity/Sandbox/CombatSandboxSceneLayoutCompiler.cs`
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
  - `2026-04-09 11:26:31.303 +09:00` direct connector POST `menu 'SM/전투테스트'` -> fail. console:
    - `[CombatSandbox] Failed: Combat Sandbox active handoff is missing: Assets/Resources/_Game/Content/Definitions/QuickBattle/quick_battle_default.asset`
    - `InvalidOperationException: Combat Sandbox active handoff is missing: Assets/Resources/_Game/Content/Definitions/QuickBattle/quick_battle_default.asset`
  - `2026-04-09 11:32:33 +09:00` direct connector POST `menu 'SM/전체테스트'` -> fail. editor state는 `Boot.unity`까지 이동했지만 preflight가 scene contract에서 중단됐다. console:
    - `[FirstPlayableRuntimeSceneBinder] Boot scene contract is incomplete. Repair via SM/Internal/Recovery/Repair First Playable Scenes.`
    - `[FullLoop] Failed: Scene repair failed. Missing component 'BootScreenController' on 'BootScreenController' in Assets/_Game/Scenes/Boot.unity`
  - `2026-04-09 16:30:33 +09:00` direct connector POST `menu 'SM/전체테스트'` -> success. editor state: `isPlaying=False; scene=Assets/_Game/Scenes/Boot.unity`, console `Retrieved 0 entries.`
  - `2026-04-09 16:49:24 +09:00` direct connector POST `menu 'SM/전투테스트'` -> `quick_battle_default.asset` import blocker는 지나갔고 Battle scene contract blocker로 이동했다. console:
    - `[FirstPlayableRuntimeSceneBinder] Battle scene contract is incomplete. Repair via SM/Internal/Recovery/Repair First Playable Scenes.`
    - `[CombatSandbox] Failed: Scene repair failed. Missing component 'BattleCameraController' on 'BattleCameraRoot' in Assets/_Game/Scenes/Battle.unity`
  - `2026-04-09 17:03:12 +09:00` direct connector POST `menu 'SM/전투테스트'` -> active handoff를 authoring path로 옮긴 뒤 preflight compile까지 진행했다. console:
    - `[CombatSandbox] Failed: Combat Sandbox preflight compile failed.`
    - `SM canonical content가 Resources runtime path에서 누락되었습니다. 먼저 SM/Internal/Content/Ensure Sample Content를 실행...`
  - `2026-04-09 17:03:58 +09:00` direct connector POST `menu 'SM/전체테스트'` -> success. editor state: `isPlaying=False; scene=Assets/_Game/Scenes/Boot.unity`, console `Retrieved 0 entries.`
  - `2026-04-09 17:04:40 +09:00` direct connector POST `menu 'SM/Internal/Content/Ensure Sample Content'` -> console error 없음.
  - `2026-04-09 17:05:05 +09:00` direct connector POST `menu 'SM/전투테스트'` -> active handoff reload instability 재현. console:
    - `[CombatSandbox] Failed: Combat Sandbox active handoff is missing: Assets/_Game/Authoring/CombatSandbox/combat_sandbox_active.asset`
  - direct exec probe:
    - `2026-04-09 16:56~17:04 +09:00` `Assets/_Game/Authoring/CombatSandbox/combat_sandbox_active.asset`는 recreate 직후 `typed=True; main=True`까지 살아났고 `ScenarioId=opening_default_4unit`도 회수됐다.
    - 같은 asset이 broader refresh / `Ensure Sample Content` 이후 다시 `typed=False; main=False`로 되돌아가는 패턴을 재현했다.
  - 이번 라운드 direct connector `exec` 재검증 -> `RuntimeCombatContentFileParser.TryLoad` 결과 `chars=12; archetypes=12`, `RuntimeCombatContentLookup` snapshot도 `snapshotChars=12`, `warden/slayer/hunter/priest=True`로 회복됨. 원인: `YamlFieldExtractor.ExtractDefinitionId` / `ApplyFallbackIdentity`에 `CharacterDefinition` 분기가 빠져 file fallback character catalog가 비어 있었다.
  - 같은 라운드 direct connector POST `menu 'SM/전투테스트'` intermediate run -> preflight compile 통과 후 Battle runtime NRE 재현. console:
    - `[BattleScreenController] Missing BattlePresentationController reference: presentationController`
    - `NullReferenceException: BattlePresentationController.ConfigureMetadataFormatter`
  - `2026-04-09 22:24:16 +09:00` direct connector POST `menu 'SM/전투테스트'` after `Battle.unity` reference repair + runtime fallback cache fix -> success. editor state: `isPlaying=True; isPaused=False; scene=Assets/_Game/Scenes/Battle.unity`, console `Retrieved 0 entries.`
  - `2026-04-09 22:26:35 +09:00` direct connector re-entry check after stop/play cycle -> success again. editor state: `isPlaying=True; isPaused=False; scene=Assets/_Game/Scenes/Battle.unity`, console `Retrieved 0 entries.`
  - `2026-04-09 22:11:47 +09:00` `pwsh -File tools/unity-bridge.ps1 test-batch-fast` -> fail, but failure reason is compile/runtime regression이 아니라 batchmode project lock:
    - `It looks like another Unity instance is running with this project open.`
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
- `quick_battle_default.asset`는 더 이상 active handoff 경로로 쓰지 않는다. `Definitions/Resources` 아래 `CombatSandboxConfig`는 content export/import lane과 충돌하며 반복적으로 unload되므로, direct sandbox handoff를 `Assets/_Game/Authoring/CombatSandbox/combat_sandbox_active.asset`로 옮겼다.
- 하지만 `Assets/_Game/Authoring/CombatSandbox/combat_sandbox_active.asset`도 broader asset refresh / `SM/Internal/Content/Ensure Sample Content` 이후 다시 `AssetDatabase.LoadAssetAtPath` null로 되돌아간다. 즉 direct sandbox blocker는 이제 path 하나의 손상보다 `CombatSandboxConfig` asset reload stability 자체다.
- `Boot.unity` missing component blocker는 정리됐고 `SM/전체테스트`는 현재 clean 진입한다.
- `Battle.unity`의 `BattleCameraController` missing component blocker는 정리됐고, 이번 라운드에는 `BattlePresentationController` 참조가 `<missing>` slot을 물고 있던 문제도 함께 복구했다.
- direct Combat Sandbox runtime blocker였던 `BattlePresentationController` missing reference / stale runtime wrapper cache는 정리됐다.
- `Assets/_Game/Scripts/Runtime/Unity/Sandbox`의 asset-bearing script와 일부 runtime Unity script meta가 guid-only 상태라 Unity serialization contract가 불안정하다. 이번 라운드에서는 `CombatSandboxConfig.cs.meta`, `CombatSandboxAssetTypes.cs.meta`, `CombatSandboxSceneAssetTypes` 대체 파일, `BattlePresentationController.cs.meta`, `BattleCameraController.cs.meta`, `GameBootstrap.cs.meta`를 우선 복구했다.
- `test-batch-fast`는 이번 라운드에서 다시 시도했지만 열린 Unity 에디터 인스턴스 때문에 batchmode project lock으로 중단됐다. test triage 7건 자체는 여전히 후속 작업이다.
- `docs-check`는 이번 작업 범위 밖의 기존 markdownlint debt와 `.claude/worktrees/**` 복사본까지 스캔해 실패한다.

## Deferred / debug-only

- safe area 실제 runtime adapter 연결
- `GameSessionState` 대규모 2차 분해
- broader sandbox history/compare UX

## Loop budget consumed

- compile-fix: 2 (`CombatSandboxSceneController` missing using, `FirstPlayableRuntimeSceneBinder` ambiguous `Debug`)
- refresh/read-console: 9 (`compile`, direct connector menu/console 재회수, heartbeat/editor log 반복 확인)
- asset/scene recovery retry: 6 (`BootScreenController`, `BattlePresentationController`, `BattleCameraController`, authoring handoff recreate, sample content ensure, scene/layout asset type split`)
- budget 초과 시 남긴 diagnosis: `Full Loop`는 회수했지만 direct Combat Sandbox는 `CombatSandboxConfig` asset reload stability가 남아서 이번 라운드에서 완전히 닫지 못했다.

## Handoff notes

- 다음 세션 시작 문서:
  - `tasks/018_combat_sandbox_recentering/status.md`
  - `docs/03_architecture/editor-sandbox-tooling.md`
  - `docs/03_architecture/unity-scene-flow.md`
- legacy alias를 다시 노출하지 않는다.
- runtime binder는 self-healing 확대가 아니라 typed contract 축소 방향으로만 수정한다.
- 다음 validation 우선순위:
  - `CombatSandboxConfig` active handoff가 refresh / `Ensure Sample Content` 이후에도 `AssetDatabase`에서 살아남도록 root cause를 닫기
  - 필요하면 `CombatSandboxAssetTypes.cs`도 file/class 계약 기준으로 추가 분리해서 starter library create/reload 안정성을 맞추기
  - `pwsh -File tools/unity-bridge.ps1 test-batch-fast` failing 7건 triage
  - `SM/전투테스트`를 다시 실행해 active handoff import 없이 preflight compile -> Battle entry까지 닫히는지 확인
