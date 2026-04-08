# Task 017 Spec

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-08
- 소스오브트루스: `tasks/017_live_truth_town_sheet_sandbox_closure/spec.md`

## Goal

launch-core 운영 truth, Town character sheet, Combat Sandbox drift preview를 한 패스로 닫아서
문서/readonly UI/editor authoring surface가 같은 기준으로 움직이게 만든다.

## Authoritative boundary

- 사람용 launch-core roster truth: `docs/02_design/deck/launch-core-roster-sheet.md`
- Town readonly character sheet truth: `TownCharacterSheetFormatter` + `TownCharacterSheetViewState`
- sandbox drift truth: committed asset + first playable slice + editor-only drift formatter
- 이번 task는 Markdown parsing, 새 runtime truth source, 새 interface layer를 추가하지 않는다

## In scope

- `tasks/017_live_truth_town_sheet_sandbox_closure/*`
- `docs/02_design/deck/launch-core-roster-sheet.md`
- `docs/02_design/ui/town-character-sheet-ui.md`
- `docs/03_architecture/town-character-sheet-contract.md`
- `docs/index.md`
- `docs/02_design/index.md`
- `docs/03_architecture/index.md`
- `docs/03_architecture/editor-sandbox-tooling.md`
- `Assets/_Game/UI/Screens/Town/TownScreen.uxml`
- `Assets/_Game/Scripts/Runtime/Unity/UI/Town/**`
- `Assets/_Game/Scripts/Runtime/Unity/ContentTextResolver.cs`
- `Assets/_Game/Scripts/Runtime/Unity/ICombatContentLookup.cs`
- `Assets/_Game/Scripts/Runtime/Unity/RuntimeCombatContentLookup.cs`
- `Assets/_Game/Scripts/Editor/Authoring/CombatSandbox/**`
- `Assets/Localization/StringTables/UI_Town*.asset`
- `Assets/Tests/EditMode/**`

## Out of scope

- Town character sheet editable action 추가
- permanent/history timeline UI
- Markdown 기반 roster sheet import
- 새 sandbox scene/tool mode
- launch-floor 전체 아이템/보드 starter package를 runtime compiled default로 승격하는 migration

## asmdef impact

- 없음
- 기존 `SM.Unity`, `SM.Editor`, `SM.Tests` 안에서만 확장한다
- editor-only drift service는 `SM.Editor`에 두고 runtime은 editor를 참조하지 않는다

## persistence impact

- 없음
- `SaveProfile`, `HeroInstanceRecord`, `HeroLoadoutRecord`, `HeroProgressionRecord`를 readonly로 소비만 한다

## validator / test oracle

- `TownCharacterSheetFormatter` FastUnit
- `ContentTextResolver` tactic/synergy fallback FastUnit
- `CombatSandboxLaunchTruthDiffService` FastUnit
- `UiLocalizationAuditTests` shared table coverage
- `tools/unity-bridge.ps1 test-batch-fast`
- `tools/docs-policy-check.ps1`
- `tools/docs-check.ps1`
- `tools/smoke-check.ps1`

## done definition

- Town 화면에서 selected hero가 5-panel readonly sheet로 렌더된다
- tactic/synergy localized resolver가 추가된다
- Combat Sandbox preview/results에 breakpoint/drift/membership summary가 표시된다
- starter scenario tag sync가 반영된다
- task/doc/index/status와 validation evidence가 같이 갱신된다

## deferred

- Town/Reward/Expedition cross-screen growth timeline
- starter equipment package를 runtime authored synthetic default로 bake-in 하는 단계
- boss/mirror sandbox library 확장
