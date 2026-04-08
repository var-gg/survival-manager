# Task 017 Implement

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-08
- 소스오브트루스: `tasks/017_live_truth_town_sheet_sandbox_closure/implement.md`

## Phase log

### Phase 0 preflight

- Town screen은 `SelectedHeroTitle` + `SelectedHeroSummaryText` 문자열 패널 하나로 묶여 있었다
- `ContentTextResolver`는 tactic/synergy name lookup이 없었고, fake lookup도 해당 asset을 담지 못했다
- sandbox window는 preview/result에 metrics/provenance만 보여 주고 launch truth drift surface가 없었다

### Phase 1 code-only

- `TownCharacterSheetViewState`, `TownCharacterSheetFormatter`를 추가하고 Town UXML/view/view-state/presenter를 5-panel 구조로 바꿨다
- `BuildIdentityFormatter`는 blueprint summary 전용으로 되돌렸다
- tactic/synergy resolver와 lookup 메서드를 추가했다
- `CombatSandboxLaunchTruthDiffService`를 추가해 breakpoint/drift/membership summary를 preview/result 양쪽에 붙였다
- `CombatSandboxAuthoringAssetUtility`는 existing scenario asset도 tag를 재동기화하도록 바꿨다

### Phase 2 doc/task

- launch-core roster sheet, Town UI IA, Town contract 문서를 추가했다
- design/architecture index와 sandbox tooling 문서를 갱신했다
- task spec/plan/status를 이번 패스 기준으로 기록했다

### Phase 3 validation

- validation 명령 결과는 `status.md`에 기록한다

## deviation

- sandbox drift의 `equipment / passive-board / augment` baseline은 현재 executable default authored core package 기준으로 잡았다
- launch-core roster sheet의 starter equipment package는 operator truth 문서로 고정했지만, runtime authored synthetic default bake-in은 이번 task에서 defer했다

## blockers

- same-SHA 수동 Town locale flip / sandbox window smoke는 editor lock 상태에 따라 막힐 수 있다
- current local profile starter scenario는 사용자 저장 데이터에 따라 drift가 달라질 수 있으므로 automated oracle로 삼지 않는다

## diagnostics

- Town sheet는 hero/session/profile/loadout/progression에서만 body를 조립하고 persistence schema는 건드리지 않는다
- sandbox drift는 `slot`, `equipment`, `passive-board`, `augment`, `posture/tactic`, `out_of_roster_scope` 6 category만 쓴다
- localization audit coverage를 위해 `TownCharacterSheetFormatter.cs`를 UI audit 대상에 추가했다

## why this loop happened

- 기존 Town UI는 하나의 요약 문자열에 너무 많은 상태를 눌러 담고 있었다
- sandbox는 provenance가 있어도 launch truth drift를 보는 요약면이 없어서 authoring closure가 어려웠다
- launch-core roster/operator truth 문서가 없어 QA/UI/sandbox가 같은 표를 보지 못했다
