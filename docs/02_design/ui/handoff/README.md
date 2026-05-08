# Claude Design handoff 운영 기준

- 상태: active
- 소유자: repository
- 최종수정일: 2026-05-08
- 소스오브트루스: `docs/02_design/ui/handoff/README.md`
- 관련문서:
  - `docs/02_design/index.md`
  - `docs/03_architecture/ui-runtime-architecture.md`
  - `docs/02_design/ui/handoff/foundation-pack-v0.2/README.md`
  - pindoc Analysis: `pindoc://claude-design-uitk-adoption-audit-2026-05-08`
  - pindoc Task: `pindoc://task-claude-design-handoff-intake-policy`
  - pindoc Task: `pindoc://task-foundation-pack-v0-2-unity-adoption`
  - pindoc Task: `pindoc://task-battle-shell-v0-4-uitk-adoption`

## 목적

이 디렉터리는 Claude Design에서 받은 HTML/CSS 중심 handoff를 Unity UI Toolkit 구현으로 옮기기 전 보존하는 reference 영역이다. 여기의 파일은 게임 코드가 직접 로드하지 않으며, Unity 구현자는 시각 톤, component contract, state matrix, responsive intent만 읽어 `Assets/_Game/UI/**`의 UXML/USS와 presenter read model로 다시 작성한다.

## 형상관리 정책

handoff의 README, HTML, CSS, JSX, 최종 회수한 JS는 구현 reference로 추적 가능한 산출물로 둔다. 이미 v0.2 원본 HTML/JSX가 tracked 상태이고, 후속 v0.3~v0.6도 같은 방식으로 Unity adoption evidence가 되므로 원본 reference를 임의로 ignore하지 않는다.

브라우저 다운로드 중간 파일, local-only 비교 screenshot, 실험용 preview export는 추적하지 않는다. `.gitignore`는 `_local/`, `*.crdownload`, `*.download`만 이 디렉터리 아래에서 제외한다. JS가 Chrome 보안 차단 때문에 pending인 경우에는 README에 `JS pending`으로 기록하고, 사용자가 Keep 후 실제 source를 회수하면 해당 JS를 reference 산출물로 편입할 수 있다.

## Inventory

| Pack | 현재 파일 | Unity 적용 상태 | 메모 |
| --- | --- | --- | --- |
| v0.2 Foundation | style guide, atoms, molecules, Boot.Title, Town.RosterGrid, design canvas | 부분 적용 | `ThemeTokens.uss`, `RuntimePanelTheme.uss`, Button, HeroPortraitCard, Ornaments가 로컬에 존재하나 compile/visual evidence 전까지 closure 아님 |
| v0.3 Dialogue.PortraitScene | HTML | 미적용 | `ChoicePromptList`, backlog, auto-advance, voice state는 아직 Unity narrative surface에 없음 |
| v0.4 Battle shell | HTML + CSS, JS pending | 부분 적용 | `BattleScreen.uxml/.uss`에 LeftRoster, CenterSummary, StatusHeadline, ControlBar, UnitDetailPanel, TurnOrderStrip skeleton 적용. dynamic roster/turn-order binding은 후속 |
| v0.5 Common.HeroDetail | HTML + CSS, JS pending | 미적용 | 4-slot skill hierarchy와 hero detail read model 표시 계약 필요 |
| v0.6 Common details | HTML + CSS, JS pending | 미적용 | SkillDetail, ItemDetail, StatusEffectTooltip 공통 layer 필요 |

## Unity UITK 변환 규칙

React/Tailwind/HTML 코드는 직접 import하지 않는다. CSS token은 `Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss`로 옮기고, 반복 장식과 톤은 `RuntimePanelTheme.uss`의 utility class로 통합한다. 실제 화면은 UXML 구조와 USS modifier class로 새로 작성하며, runtime 값은 presenter가 만든 view state에서만 주입한다.

CSS `::before`/`::after`는 child `VisualElement`로 치환한다. gradient, shadow, filter는 Unity USS에서 완전 동일 구현을 목표로 하지 않고, frame layer, inset line, tint overlay, small SVG ornament 조합으로 시각 등가물을 만든다. SVG ornament는 Unity asset으로 두고 `background-image: url("project://database/...")`와 `-unity-background-image-tint-color`로 색을 맞춘다.

state matrix는 class modifier로 표현한다. 예를 들어 `sm-btn--loading`, `sm-hpc--selected`, `sm-bs-unit-detail-panel--open`, `sm-hd-skill-slot--signature-lock`, `sm-cd-status-pill--cleansable` 같은 class를 C# presenter가 토글하고, gameplay truth는 UI에서 재계산하지 않는다.

## Pack별 적용 우선순위

우선순위는 `v0.2 foundation closure -> v0.4 Battle shell -> v0.5 HeroDetail -> v0.6 Common detail -> v0.3 Dialogue` 순서다. 전투테스트에서 실제 목표 인게임 HUD를 확인하는 것이 현재 vertical slice에 가장 직접적이므로 Battle shell이 foundation 다음에 온다.

## 재조회 방법

현재 상태를 다시 확인할 때는 아래 명령을 사용한다.

```powershell
git status --short docs/02_design/ui/handoff Assets/_Game/UI/Foundation
git check-ignore -v docs/02_design/ui/handoff/foundation-pack-v0.4/battle-shell.css
rg -n "sm-bs-|sm-hd-|sm-cd-|ChoicePrompt|sm-btn|sm-hpc" Assets/_Game/UI Assets/_Game/Scripts/Runtime/Unity
```
