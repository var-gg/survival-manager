# Foundation pack v0.4 — Battle shell [DEPRECATED]

**상태: deprecated**. 시각 톤이 anime-painted illustrated world 방향과 안 맞아
pindoc wiki 시안 갤러리로 SoT 전환. 이 폴더의 HTML/CSS/JS는 historical reference.

- 현재 비주얼 SoT: `pindoc://battle-map-시안-갤러리-v1-10-site-일러-reference-gallery-battle`
- 전환 결정: `pindoc://claude-design-uitk-adoption-audit-2026-05-08`

**원본 status** (cycle 시점): cycle 2 (v0.4) 부분 완료 — HTML + CSS 수집, JS pending (Chrome 보안 차단으로 사용자 manual Keep 필요)

## 산출물

| 파일 | 크기 | 역할 |
| --- | --- | --- |
| `surface-battle-shell.html` | 2.8 KB | wrapper (canvas mount points + font preconnect + css/js link) |
| `battle-shell.css` | 66.7 KB | v0.4 스타일 (token 인용 + state matrix + responsive) |
| `battle-shell.js` | (~45.5 KB, **pending download**) | artboard mount points 동적 채움 |

> **JS pending**: Chrome이 .js 파일을 위험 파일로 분류해 download bar에서 Keep 클릭 필요.
> 사용자가 Keep 클릭 후 `Downloads/battle-shell.js`를 이 폴더로 이동.
> 또는 다음 cycle 의뢰 시 Open 메뉴에서 새 tab으로 열고 source 추출 시도.

## 의뢰 내용 (claude.ai/design chat input)

- 4v4 turn-based combat surface — PC + Mobile, 6 state each, no new tokens
- Reuses v0.2 token (vellum cream / gold corner cap / family-tint rib / jewel duotone) + v0.3 ChoicePromptList
- Korean hero names: 단린 (Dawn Priest, Mystic), 이빨바람 (Pack Raider, Beastkin), 묵향 (Grave Hexer, Mystic), 공한 (Echo Savant, Vanguard)
- Production scope: indie 1-dev, $10 USD, flat layering, snap motion only

## 결과 요약 (claude.ai/design 출력 인용)

> Battle Shell v0.4 — delivered.
> PC artboard (1280×800) and Mobile artboard (390×844), each with 6 state variations stacked: turn-start · mid-action · status-changed · unit-selected · wave-clear · defeat.
> Reuses v0.2 tokens (vellum/gold cap, family rib, jewel duotone) + v0.3 ChoicePromptList (wave-clear preview).
> Korean hero names: 단린 / 이빨바람 / 묵향 / 공한 with face icon swap on status change.
> P09 RenderTexture silhouette placeholders, family floor halos, snap motion only.
> ControlBar (Auto/Turbo/Pause/Retreat) on PC; bottom-sheet on Mobile.
> UnitDetailPanel = right slide-out drawer (PC) / pull-up sheet (Mobile) with 4-skill grid + buff/debuff strip.

## 시각 검증 항목 (PC 기준)

- ✅ 헤드라인 `BATTLE ❀ SHELL` (Cinzel display + flourish ornament)
- ✅ vellum cream + gold corner cap 톤
- ✅ family-tint rib 적용 (단린=Mystic 보라, 이빨바람=Beastkin 모스, 공한=Vanguard 사파이어, 묵향=Mystic 이중)
- ✅ 5-rail layout: ALLY 4/4 좌측 + center stage + FOE 4/4 우측
- ✅ TURN ORDER strip (active 단린 highlighted)
- ✅ STATUS HEADLINE 이빨바람 BEASTKIN · PACK RAIDER 156/180 + TURN 07/24 · WAVE 03/05 · OBJECTIVE
- ✅ FORM 4 · BACK 작익 control button cluster
- ✅ 적군 Korean names: 골수 유령 MYSTIC · AOE Curse, 백창병 VANGUARD · Pierce, 서리 잠복자 BEASTKIN · Freeze, 그늘 사냥꾼 STRIKER · Bite
- ✅ P09 silhouette + family floor halo (단린, 이빨바람, 묵향, 공한)

## Unity UITK 흡수 가이드 (4단계)

2026-05-08 audit 기준으로 본 pack은 **reference usable / Unity 부분 적용** 상태다. HTML+CSS는 Battle shell 구조와 USS class contract를 도출하는 데 사용 가능하지만, `battle-shell.js`가 pending이므로 dynamic artboard state preview는 아직 완전하지 않다. Unity 구현 Task는 `pindoc://task-battle-shell-v0-4-uitk-adoption`이다.

현재 Unity 쪽에는 `Assets/_Game/UI/Screens/Battle/BattleScreen.uxml/.uss`에 `LeftRoster`, `RightRoster`, `CenterSummary`, `StatusHeadline`, `CompactLog`, `ControlBar`, `UnitDetailPanel`, `TurnOrderStrip`, `SkillPresentationSlots` skeleton이 들어갔다. 기존 presenter가 사용하는 element name은 유지했지만, roster slot과 turn order의 동적 actor binding은 아직 placeholder/skeleton 단계다.

### 1. Token 추출 (이미 완료)

`battle-shell.css`는 v0.2 token만 인용 — 새 token 없음. 즉 `Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss`로 이미 transcribe 끝.

### 2. Component contract 참고

`surface-battle-shell.html` + `battle-shell.css` 읽고 다음 atom/molecule contract 도출:

- `LeftRoster` (4 ally vertical, family-tint rib left)
- `RightRoster` (1-6 enemy stack)
- `CenterSummary` (turn / wave / objective)
- `StatusHeadline` (active unit large HP bar + name)
- `CompactLog` (scrolling action log, last 4 lines)
- `ControlBar` (Auto/Turbo/Pause/Retreat 4 button)
- `UnitDetailPanel` (right slide-out drawer with portrait + 4 skill grid + buff/debuff strip)
- `TurnOrderStrip` (active unit highlight, scrollable)
- `FormationButton` (FORM 4 · BACK 작익)
- `FaceIconSwap` (idle ↔ wounded ↔ stunned ↔ feared ↔ charmed ↔ pained ↔ downed)

### 3. 시각 reference

`battle-shell.css`의 selector 우선순위 + spacing scale을 Unity UITK USS의 modifier class에 맵핑.

`atoms-button-herocard.html` (v0.2)에서 sm-btn / sm-hpc 패턴 이어가서:
- `sm-bs-roster-rail` / `sm-bs-rail-slot` (4-slot vertical)
- `sm-bs-stage` / `sm-bs-floor-halo` (family tint)
- `sm-bs-status-headline` (active unit HP)
- `sm-bs-control-bar` / `sm-bs-control-btn` (Auto/Turbo/Pause/Retreat)
- `sm-bs-unit-detail-panel` (slide-out drawer state)
- `sm-bs-turn-order` (active highlight)

### 4. 카탈로그 v3 cross-ref

`pindoc://ux-surface-catalog-v1-draft` Town/Battle 섹션의 surface spec과 cross-ref 후 atom/molecule 추가.

## 변경 이력

- 2026-05-08: cycle 2 (v0.4) Battle shell mock generation 완료. claude.ai/design v0.2 + v0.3 token 인용. JS download Chrome 보안 차단으로 pending. HTML + CSS 우선 누적.
