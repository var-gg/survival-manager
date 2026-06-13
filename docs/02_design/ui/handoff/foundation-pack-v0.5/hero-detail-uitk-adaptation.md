# Common.HeroDetail — UITK 적응 계약 (v0.5 → Unity)

`task-hero-detail-v0-5-uitk-adoption`의 구현 계약. v0.5 reference(`surface-hero-detail.html` + `hero-detail.css`)는
브라우저 CSS 기준 1042라인 ornate 시안이지만, UITK는 그 CSS 기능의 상당수를 지원하지 않는다. 이 문서는
**무엇을 named-element 계약으로 고정하고, ornate CSS를 어떤 buildable UITK subset으로 적응하는지**를 결정해
둔 것이다. 실제 UXML/USS 저작 + compile + PlayMode 시각 검증은 깨끗한 에디터 윈도우에서 수행한다.

적응 철학은 기존 `Assets/_Game/UI/Foundation/Details/SkillDetailModal.uxml` + `USS/common_detail.uss`
(`sm-cd-*`)가 ornate skill/item 시안을 restrained token-기반 subset으로 흡수한 방식을 그대로 따른다. 즉
reference는 지향점이고, UITK 버전은 절제·토큰화·구현가능 우선.

## UITK가 지원 안 하는 CSS와 적응 결정

| reference CSS 기능 | UITK 현실 | 적응 결정 |
| --- | --- | --- |
| `::before` / `::after` (corner cap, inner border, badge 전부) | UITK 미지원 | corner cap/badge는 **실제 자식 VisualElement**로 realize하거나 생략. 1순위는 절제 — slot/패널 외곽 cap은 단일 1px border로 대체, signature/late-unlock 같은 의미 있는 badge만 자식 element로 둔다. |
| CSS `grid` (`grid-template-columns:380px 1fr 340px`) | UITK는 flexbox only | 3-rail은 `flex-direction:row` + 좌 `width:380px` / 중 `flex-grow:1` / 우 `width:340px`. 내부 stat 2-col, skill 4-slot, equip 5-slot도 `flex-direction:row` + `flex-basis` 분배. |
| `box-shadow` (slot glow, selected) | UITK 제한적/구버전 미지원 | glow는 `foundation_glow.uss` 자산 또는 border-color 강조로 대체. 선택 상태는 2px gold border + 배경 톤 변화. |
| `filter:grayscale/brightness/blur/drop-shadow` (retrain dim, late-unlock) | UITK 미지원 | retrain dim은 아이콘 `opacity` + 배경 톤 다운, late-unlock 빗금은 반복 배경 대신 단색 오버레이 element. blur halo는 생략 또는 저해상 glow sprite. |
| `-webkit-background-clip:text` (gold gradient 제목) | UITK 미지원 | 단색 `--sm-gold-200` 텍스트. gradient 텍스트 포기. |
| `radial/linear-gradient` 다중 배경 | UITK 제한적 | 패널 배경은 단색 토큰(`--sm-bg-*`) + 1px gold border. 핵심 grain/halo는 생략. |
| `position:absolute` 오버레이(tooltip/callout) | UITK 지원하나 panel 좌표계 다름 | skill tooltip/item callout은 **별도 트리거형 element**로 분리(상시 표시 아님). v1 범위에서는 정적 detail만, hover tooltip은 후속. |

## Named-element 계약 (acceptance #1)

`sm-hd-*` 클래스 + PascalCase element name. Town/Battle 양쪽에서 재사용하는 reusable component 계약.
루트는 `Assets/_Game/UI/Foundation/Details/HeroDetailPanel.uxml`(detail family에 합류), 스타일은
`USS/hero_detail.uss` + 공유 `common_detail.uss` + `ThemeTokens.uss`.

```
HeroDetailPanel            .sm-hd-panel
├─ HeroDetailTopBar        .sm-hd-top
│  ├─ HeroDetailBack       .sm-hd-back        (Button atom 재사용)
│  ├─ HeroDetailCrumb      .sm-hd-crumb
│  ├─ HeroDetailTabs       .sm-hd-tabs        (Stat/Skill/Equip 탭 — v1은 단일 스크롤도 허용)
│  └─ HeroDetailPrevNext   .sm-hd-pn
├─ HeroDetailLeft          .sm-hd-left
│  ├─ HeroDetailPortrait   .sm-hd-portrait    (HeroPortraitCard atom 재사용, P09 RenderTexture placeholder)
│  ├─ HeroDetailNameplate  .sm-hd-name        (ko-name / lvl / archetype chip / xp bar)
│  └─ HeroDetailRecruit    .sm-hd-recruit     (3-cell: origin / join / status)
├─ HeroDetailCenter        .sm-hd-center
│  ├─ HeroDetailStatSheet  .sm-hd-stat        (HeroDetailStatRow* 반복 + combat-class banner)
│  ├─ HeroDetailSkillGrid  .sm-hd-skills      (HeroDetailSlot ×4 — 아래 modifier)
│  └─ HeroDetailEquipRow   .sm-hd-equip       (HeroDetailEquipSlot ×5)
└─ HeroDetailRight         .sm-hd-right
   ├─ HeroDetailAffixList  .sm-hd-affix       (HeroDetailAffixRow* + set-bonus)
   ├─ HeroDetailTraitList  .sm-hd-traits      (boon/bane/quirk)
   └─ HeroDetailStance     .sm-hd-stance      (stance switcher)
```

## Skill-slot 위계 modifier (acceptance #2)

4-slot 위계 = signature lock 1 · flex active 1 · retrain pool 1 · late unlock 1 (동시 active는 2개).
reference의 `.sg-rib.*` / `.sg-role.*` / `.sg-icon-wrap.*`를 단일 modifier 축으로 통합:

```
HeroDetailSlot .sm-hd-slot
  + .sm-hd-slot--signature    gold 강조 border + lock badge element, 상시 active
  + .sm-hd-slot--flex-active  state-safe(green) border + check badge, 현재 장착
  + .sm-hd-slot--retrain      ink-muted dashed border + 아이콘 opacity 0.55, 교체 후보
  + .sm-hd-slot--late-unlock  epic border + chain badge + 단색 lock 오버레이 element
```
badge(lock/check/chain)는 slot 안 자식 `.sm-hd-slot-badge` element로 realize(::before 대체).

## 아이콘 fallback 상태 (acceptance #5)

```
HeroDetailSlotIcon .sm-hd-slot-icon
  + .sm-hd-slot-icon--missing    아이콘 미지정: 중립 글리프 + ink-3, "no icon" 표식
  + .sm-hd-slot-icon--fallback   카탈로그 fallback 사용 중: 점선 border로 구분
```
equip 빈 슬롯도 `.sm-hd-equip-slot--empty`(dashed)로 구분. missing ≠ fallback ≠ empty 세 상태 분리.

## View-state read model (acceptance #4)

UI 전용 read model. presenter가 공급하고 **UI에서 gameplay truth 재계산 금지**. record/struct로 충분(새
interface 불필요). 위치 후보: `Assets/_Game/Scripts/Runtime/Unity/UI/Town/HeroDetailViewState.cs`.

```
HeroDetailViewState
  Identity        : koName, enName, portraitKey(P09), archetypeId, familyId(=color group), roleLabel
  Progression     : level, tier, xpRatio (0..1, 표시용 사전계산)
  Stats           : IReadOnlyList<StatRowVm>{ key, koLabel, value, deltaKind(up/down/none), deltaText }
  Skills          : SkillSlotVm[4]{ slotKind(signature/flexActive/retrain/lateUnlock),
                                    koName, enName, iconKey, iconState(present/fallback/missing),
                                    apText, cdText, isUlt, tags[] }
  Equipment       : EquipSlotVm[5]{ slotKey, koSlotLabel, rarity, itemKoName, iconKey, statLine, isEmpty }
  AffixSummary    : IReadOnlyList<AffixRowVm>{ koLabel, subLabel, value, tone(safe/warn/dmg) } + setBonusVm?
  Traits          : IReadOnlyList<TraitVm>{ kind(boon/bane/quirk), koName, desc }
  Stance          : current stanceId + selectable stanceVm[]
```
`familyId`는 reference의 `--fam-*` color group(beastkin/vanguard/striker/ranger/mystic)에 대응하는
USS modifier(`.sm-hd--fam-vanguard` 등)로 매핑해 rail accent 색을 토큰으로 스위칭한다.

## 토큰/atom 재사용 (acceptance #3)

- 색/간격/폰트: `ThemeTokens.uss`의 `--sm-gold-*` / `--sm-ink-*` / `--sm-vellum-*` / `--sm-bg-*` / `--sm-s-*` / `--sm-fs-*`만 사용. reference의 raw hex(`#0c1126` 등) 직접 박지 않는다.
- atom: 좌측 portrait는 `HeroPortraitCard`, back/action 버튼은 `Button` atom 재사용. 시각 언어 충돌 금지.
- 신규 토큰 도입 금지(reference도 "NO new tokens" 명시). `--fam-*` accent가 토큰에 없으면 ThemeTokens에 **family accent 토큰만** 최소 추가 후 사용.

## 구현 순서 (에디터 윈도우 확보 시)

1. `HeroDetailViewState` + 순수 formatter(`HeroDetailViewStateFormatter`) 먼저 — pure 단위 테스트 가능(에디터 compile만, PlayMode 불필요).
2. `HeroDetailPanel.uxml` + `hero_detail.uss` named-element 골격(좌/중/우 rail + 4 slot + 5 equip).
3. presenter 바인딩 + Town 진입점에서 선택 영웅 열기.
4. 검증: `unity-bridge compile` → 5초 → `test-batch-fast`(formatter 테스트 포함) → Town PlayMode에서 nonblank/overlap/long-name/missing-icon 확인.

## 범위 밖 / 후속

- skill hover tooltip popover, item callout(절대좌표 오버레이) — v1 정적 detail 이후.
- mobile <768 vertical scroll variant — PC 안정화 후.
- 실제 skill icon art, P09 RenderTexture 연결 — 각 art/character Task 소관.
- 스킬 수치·12영웅 매핑 확정 — `character-skill-mapping` 계열 Task. 본 계약은 그 결과를 표시할 UI 계약만.
