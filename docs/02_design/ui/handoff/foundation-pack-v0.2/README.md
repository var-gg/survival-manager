# Foundation Pack v0.2 — claude.ai/design 핸드오프 결과 — historical reference

> ⚠ **이 HTML/JSX는 현재 비주얼 SoT가 아니다.** anime-painted 톤과 mismatch로
> 채택 보류. 비주얼 reference로 읽지 말 것. 현재 SoT는 아래 pindoc 링크.

- 상태: active
- 소유자: repository
- 최종수정일: 2026-05-13
- 소스오브트루스: 이 README는 historical 자산의 위치/형상관리 기준만 다룬다. 비주얼 SoT는 pindoc.
- 관련문서:
  - 현재 비주얼 SoT: `pindoc://town-ui-ux-시안-갤러리-v1-gallery-town-ui-mockups-v1`
  - 현재 비주얼 SoT: `pindoc://analysis-town-ui-component-system-v1`
  - 전환 결정: `pindoc://claude-design-uitk-adoption-audit-2026-05-08`
  - `docs/02_design/ui/handoff/README.md` (deprecated 마스터)

## 현재 비주얼 SoT

**이 폴더의 HTML/JSX는 더 이상 비주얼 reference로 사용하지 않는다.** 시각 톤이
프로젝트가 지향하는 anime-painted / illustrated world (명일방주 / Honkai Star
Rail 톤)과 너무 달라 채택 보류. 현재 Town/Battle/HeroDetail UI 비주얼 시안은
pindoc wiki의 시안 갤러리 (위 SoT 링크)를 본다.

활용 자산 (이미 Unity에 반영된 일부):

- `Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss` — 컬러/타이포 토큰
- `Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss`
- `Assets/_Game/UI/Foundation/Components/Button.{uxml,uss}`
- `Assets/_Game/UI/Foundation/Components/HeroPortraitCard.{uxml,uss}`

token 일부는 시안 v1 톤에 맞춰 재조정 예정 (별도 task).

## 목적 (Historical)

claude.ai/design 의뢰로 생성된 Survival Manager Foundation pack v0.2의 결과물 보존소.
JRPG/원신/유니콘 오버로드 톤 + 1인 인디 production scope를 흡수한 design system mock 6 step + 3 surface 검증.

## Handoff 운영 상태

이 폴더의 HTML/JSX는 Unity가 직접 로드하는 코드가 아니라 구현 reference다. repo 정책상 README/HTML/CSS/JSX/최종 회수 JS는 추적 가능한 reference로 두고, 브라우저 임시 다운로드와 local preview만 ignore한다. 상세 기준은 `docs/02_design/ui/handoff/README.md`를 따른다.

2026-05-08 기준 Unity 적용은 **부분 진행**이다. `ThemeTokens.uss`, `RuntimePanelTheme.uss`, Button atom, HeroPortraitCard atom, ornament SVG가 로컬에 존재하지만, atom 파일은 아직 compile/visual evidence와 commit closure 전이므로 완료로 보지 않는다.

## 파일 목록 (생성 순서)

| 파일 | 크기 | Step | 역할 |
| --- | --- | --- | --- |
| `style-guide.html` | 56 KB | 1 | Style Guide v0.2 — color/typography/ornate frame/HUD strip/motion notes |
| `atoms-button-herocard.html` | 88 KB | 2 | Button atom 5×3×5 + HeroPortraitCard small variant |
| `surface-town-recruitpack.html` | 125 KB | 3 | Town.RecruitPack 4-slot — atom 조합 검증 + 새 atom 3개(Cost chip, Tag chip, pity ProgressBar) 자연 도입 |
| `atoms-ui-behaviors.html` | 100 KB | 4 | Tooltip / Modal shell / ProgressBar / Status pill / Toast / ConfirmDialog 6 atom |
| `molecules-gameplay.html` | 100 KB | 5 | AugmentChip / SynergyBadge / ItemSlot / StatRow / TraitQuirkRow / ActionRow / CostPreview / LockState 8 molecule |
| `molecules-narrative-battle.html` | 92 KB | 6 | PortraitFrame / DialogueBox / VoicePlayer / BacklogList / AutoAdvanceToggle / BuffDebuffStrip / EquipmentMiniRow 7 molecule + Mobile composite 2장(대화 화면 / 전투 상세) |
| `surface-boot-title.html` | 57 KB | 7 (잔여 1) | Boot.Title — 게임 진입 화면 v0.1, 3 state(default / no save / loading), PC + Mobile, vellum BG + gold corner cap + family tint accent |
| `surface-town-roster-grid.html` | 101 KB | 8 (잔여 2) | Town.RosterGrid — default 진입 panel, HeroPortraitCard sm 12장 grid + filter chip + Synergy preview rail + Quick Battle FAB. PC 4 state(default/selected/mixed/filter) + Mobile 2 state |
| `design-canvas.jsx` | 31 KB | - | 전체 file 묶음 React canvas component |

총 9 file (8 HTML page + 1 JSX component) 누적.

## v0.2 디자인 토큰 요약

claude.ai/design 출력에서 흡수한 핵심 token. **이미 `Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss`에 sm- prefix variable로 transcribe 완료** (60+ token).

핵심 인용:
- **Vellum cream ink**: `--sm-vellum: rgb(245, 234, 208)` (#F5EAD0)
- **Gold ladder**: `--sm-gold-100~500` (시그니처 골드 `--sm-gold-300: rgb(230, 183, 81)`)
- **Family tint 5종**: Beastkin / Vanguard / Striker / Ranger / Mystic × (base + deep + glow)
- **Rarity dueltone**: parchment(common) / sapphire(rare) / amethyst→flame(epic)
- **State 6**: safe / warn / danger / locked / ko(ash) / injured(warm bruise)
- **Surface BG**: warm navy `--sm-bg-0~3` (검정 X, 인디고 양피지)
- **Spacing 8pt grid**: `--sm-s-1~7`
- **Motion**: `--sm-t-snap: 200ms`

기존 simple token(`--color-bg`, `--space-X` 등)이 v0.2 token에 alias로 매핑되어 **기존 4 screen + Narrative USS는 코드 변경 없이 자동으로 v0.2 톤(vellum cream + gold accent + warm navy BG)으로 swap** 됨.

## 사용 원칙 (중요)

**claude.ai/design 출력은 React/Tailwind 기반 HTML/CSS이고 Unity UITK는 USS/UXML이라 코드 직접 transpile 부적합.** 시각 reference / 컨셉 / 일관성 / component contract만 흡수한다.

## Unity UITK 흡수 진행 단계

### 1. Token 추출 → USS variable ✓ 완료

`Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss`에 v0.2 token 60+ transcribe 완료. 기존 token에 alias로 backward compat 유지.

### 2. Foundation utility class (부분 진행)

`Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss`에 v0.2 톤의 utility class 추가:
- `sm-ornate-frame` / `sm-corner-cap` / `sm-vine-flourish`
- `sm-family-tint-rib` / `sm-vellum-card`
- `sm-jewel-common` / `sm-jewel-rare` / `sm-jewel-epic`

compile과 screen-level visual evidence 전까지는 `task-foundation-pack-v0-2-unity-adoption`의 partial 상태로 본다.

### 3. Atom UXML/USS (부분 진행)

- `Assets/_Game/UI/Foundation/Components/Button.uxml + .uss` (5 variant × 3 size × 5 state)
- `Assets/_Game/UI/Foundation/Components/HeroPortraitCard.uxml + .uss` (small variant + 4 state)
- `Assets/_Game/UI/Foundation/Ornaments/*.svg` (vine flourish, corner vine, spinner arc)

UXML은 atom HTML structure를 reference로 보고 새로 정의 (직접 transpile X). 카탈로그 v3 prop matrix와 cross-check.
HeroPortraitCard placeholder는 한국어 캐릭터명 registry 방향에 맞춰 `단린 / DAWN PRIEST` 기준으로 둔다.

### 4. Visual reference로 활용

각 HTML을 브라우저에서 열어서 시각 톤을 매칭:

```bash
start docs/02_design/ui/handoff/foundation-pack-v0.2/style-guide.html
start docs/02_design/ui/handoff/foundation-pack-v0.2/surface-town-roster-grid.html
start docs/02_design/ui/handoff/foundation-pack-v0.2/surface-boot-title.html
```

## 다음 단계

5/8 reset 후 결과는 `foundation-pack-v0.3/`부터 `foundation-pack-v0.6/`까지 누적됐다. v0.4~v0.6은 HTML+CSS가 usable reference이고 JS는 pending 상태다.

Unity 적용은 `v0.2 foundation closure -> v0.4 Battle shell -> v0.5 HeroDetail -> v0.6 Common detail -> v0.3 Dialogue` 순서로 진행한다. 전투테스트의 실사용 가치가 가장 큰 Battle shell이 foundation 다음 우선순위다.

## 한계 및 주의사항

- **Korean placeholder name**: Claude design 원본은 hero 이름 placeholder로 일본 이름을 생성할 수 있다. Unity atom/template에서는 한국어 캐릭터 이름 registry 방향에 맞춰 `단린 / DAWN PRIEST` 같은 한국어 ID + 영문 별칭만 사용한다.
- **Missing brand fonts**: claude.ai/design은 substitute web font로 렌더링 (Pretendard, Noto Sans KR 미설치). Unity 구현 시 실 폰트 import.
- **모든 portrait은 P09 RenderTexture placeholder**: mock에서 2D placeholder image지만 실 구현은 P09 Modular Humanoid runtime camera capture(`PortraitCaptureService`).
- **Setup page "Any other notes"는 LLM에 전달되지 않음**: 의뢰 시 brief는 chat input("Describe what you want to create...")으로 보내야 함. 다음 generation 시 같은 함정 회피.
- **claude.ai/design 출력 코드는 직접 import 금지**: React/Tailwind이고 Unity는 UITK USS/UXML. 시각 reference / 컨셉만 흡수.

## 관련 카탈로그·brief 변경 이력

- 카탈로그 v3 settled (2026-05-05): production scope settled (voice 70-110 line / cutscene 12-15 / hero death KO-only / illust 0 baseline / mobile 병행)
- Brief v2 settled (2026-05-05): atmosphere + responsive + Foundation prop spec + PC/Mobile layout + 의뢰 prompt 템플릿
- Foundation pack v0.2 mock 1차 완성 (2026-05-06 오전): 6 step generation + Town.RecruitPack 1 surface 검증
- Foundation pack v0.2 잔여 cycle (2026-05-06 오후): + Boot.Title + Town.RosterGrid 2 surface 추가, weekly limit 도달
- Unity 적용 1차 (2026-05-06): ThemeTokens.uss v0.2 transcribe 완료. RuntimePanelTheme utility class + atom UXML/USS는 진행 중
