# Town character sheet contract

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-09
- 소스오브트루스: `docs/03_architecture/town-character-sheet-contract.md`
- 관련문서:
  - `docs/03_architecture/ui-runtime-architecture.md`
  - `docs/03_architecture/localization-runtime-and-content-pipeline.md`
  - `docs/03_architecture/editor-sandbox-tooling.md`
  - `docs/02_design/ui/town-character-sheet-ui.md`

## 목적

이 문서는 Town readonly character sheet의 source-of-truth, view-state shape, locale refresh, sandbox 연계 규칙을 고정한다.

## runtime ownership

- source data
  - `GameSessionState`
  - `SaveProfile.Heroes`
  - `SaveProfile.HeroLoadouts`
  - `SaveProfile.HeroProgressions`
  - `SaveProfile.Inventory`
  - `SaveProfile.PermanentAugmentLoadouts`
- content lookup
  - `ICombatContentLookup`
  - `ContentTextResolver`
  - `LaunchCoreRosterBaselineCatalog`
- formatter
  - `TownCharacterSheetFormatter`
- presenter
  - `TownScreenPresenter`는 선택 상태와 action 비용만 계산하고 formatter 조립만 맡긴다

## view-state shape

- `TownCharacterSheetPanelViewState`
  - `Title`
  - `Body`
- `TownCharacterSheetViewState`
  - `Overview`
  - `Loadout`
  - `Passives`
  - `Synergy`
  - `Progression`
- `TownScreenViewState`는 기존 `SelectedHeroTitle` / `SelectedHeroSummaryText`를 더 이상 갖지 않고 `CharacterSheet` 하나만 갖는다

## panel source mapping

| panel | primary source | derived source | 비고 |
| --- | --- | --- | --- |
| `Overview` | `HeroInstanceRecord`, `GameSessionState.SelectedTeamPosture`, `SelectedTeamTacticId` | `ContentTextResolver` | role은 archetype default role tag fallback 허용 |
| `Loadout` | equipped inventory + archetype default loadout + hero flex ids | `ContentTextResolver`, `LaunchCoreRosterBaselineCatalog` | per-encounter compiled mutation은 다루지 않음 |
| `Passives` | `HeroLoadoutRecord`, `PassiveBoardDefinition`, selected node | `ContentTextResolver` | node count cap은 validator constant 사용 |
| `Synergy` | `ExpeditionSquadHeroIds`, `FirstPlayableSlice.SynergyGrammar`, archetype governance | `ContentTextResolver`, `LaunchCoreRosterBaselineCatalog` | current squad 기준만 계산 |
| `Progression` | recruit/retrain/economy/permanent/passive progression state | formatter local summary | history timeline은 범위 밖 |

## locale refresh 규칙

- panel title과 content body는 `TownScreenPresenter.Refresh()` 한 번으로 다시 조립한다
- formatter는 localized string을 캐시하지 않는다
- panel body label과 state text도 `ui.town.sheet.*` key를 통해 전부 재조립한다
- `ContentTextResolver.GetTeamTacticName(...)`와 `GetSynergyName(...)`를 통해 content-localized 이름을 가져온다
- missing localization은 fallback text/id로 degrade한다

## UXML contract

- Town screen은 panel 다섯 개를 고정 name으로 가진다
  - `CharacterSheetOverviewTitleLabel`, `CharacterSheetOverviewBodyLabel`
  - `CharacterSheetLoadoutTitleLabel`, `CharacterSheetLoadoutBodyLabel`
  - `CharacterSheetPassivesTitleLabel`, `CharacterSheetPassivesBodyLabel`
  - `CharacterSheetSynergyTitleLabel`, `CharacterSheetSynergyBodyLabel`
  - `CharacterSheetProgressionTitleLabel`, `CharacterSheetProgressionBodyLabel`
- view는 name lookup만 하고 business formatting은 하지 않는다

## sandbox diff input

- Town character sheet와 sandbox diff는 둘 다 committed asset + current session/profile만 읽고, baseline 해석은 `LaunchCoreRosterBaselineCatalog` 하나로 공유한다
- sandbox diff는 `CombatSandboxLaunchTruthDiffService`가 `slot`, `equipment`, `passive-board`, `augment`, `posture/tactic`, `out_of_roster_scope`를 exact delta 문장으로 요약한다
- Town sheet는 drift category를 직접 보여 주지 않지만, 같은 source-of-truth를 읽는 운영 surface로 본다

## acceptance

- Town presenter가 giant string builder로 selected hero 전체를 조립하지 않는다
- view-state와 formatter가 분리돼 테스트에서 panel별 oracle을 세울 수 있어야 한다
- editor-only sandbox diff는 runtime assembly를 건드리지 않고 `SM.Editor`에 닫힌다
