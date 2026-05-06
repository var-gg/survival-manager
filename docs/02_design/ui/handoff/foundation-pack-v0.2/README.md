# Foundation Pack v0.2 — claude.ai/design 핸드오프 결과

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-05-06
- 소스오브트루스: 이 폴더의 HTML/JSX 파일
- 관련문서:
  - `docs/02_design/ui/battle-observer-ui.md`
  - `docs/02_design/ui/town-character-sheet-ui.md`
  - `docs/03_architecture/ui-runtime-architecture.md`
  - pindoc Analysis: `pindoc://ux-surface-catalog-v1-draft` (UX Surface 카탈로그 v3)
  - pindoc Analysis: `pindoc://claude-design-handoff-brief` (의뢰 brief v2)
  - pindoc Analysis: `pindoc://analysis-p09-visual-baseline` (P09 시각 baseline)

## 목적

claude.ai/design 의뢰로 생성된 Survival Manager Foundation pack v0.2의 결과물 보존소.
JRPG/원신/유니콘 오버로드 톤 + 1인 인디 production scope를 흡수한 design system mock 6 step + 1 surface 검증.

## 파일 목록 (생성 순서)

| 파일 | 크기 | Step | 역할 |
| --- | --- | --- | --- |
| `style-guide.html` | 56 KB | 1 | Style Guide v0.2 — color/typography/ornate frame/HUD strip/motion notes |
| `atoms-button-herocard.html` | 88 KB | 2 | Button atom 5×3×5 + HeroPortraitCard small variant |
| `surface-town-recruitpack.html` | 125 KB | 3 | Town.RecruitPack 4-slot — atom 조합 검증 + 새 atom 3개(Cost chip, Tag chip, pity ProgressBar) 자연 도입 |
| `atoms-ui-behaviors.html` | 100 KB | 4 | Tooltip / Modal shell / ProgressBar / Status pill / Toast / ConfirmDialog 6 atom |
| `molecules-gameplay.html` | 100 KB | 5 | AugmentChip / SynergyBadge / ItemSlot / StatRow / TraitQuirkRow / ActionRow / CostPreview / LockState 8 molecule |
| `molecules-narrative-battle.html` | 92 KB | 6 | PortraitFrame / DialogueBox / VoicePlayer / BacklogList / AutoAdvanceToggle / BuffDebuffStrip / EquipmentMiniRow 7 molecule + Mobile composite 2장(대화 화면 / 전투 상세) |
| `design-canvas.jsx` | 31 KB | - | 전체 file 묶음 React canvas component |

## v0.2 디자인 토큰 요약

claude.ai/design 출력에서 흡수할 핵심 token. Unity UITK 구현 시 `Assets/_Game/UI/Foundation/theme.uss` variable로 transcribe.

- **Vellum cream ink**: `#F5EAD0` (양피지 잉크 본문)
- **Gold corner cap**: 모든 atom + card 모서리 일관 적용
- **Vine flourish**: primary hover에서 좌우 부드럽게 reveal
- **Family-tint gradient rib**: 카드 상단 띠
- **Center gem inlay**: 카드 중앙 보석
- **Jewel duotone rarity**: parchment(common) / sapphire(rare) / amethyst→flame(epic)
- **HP bar**: 25% tick + 앰버+와인 그라디언트 + 골드 frame + ember pulse(danger state)
- **Archetype family tone**:
  - Beastkin/Undead: muted earth, moss, decay green, ash gray
  - Vanguard: cool steel, deep blue, stone gray
  - Striker: warm crimson, ember orange
  - Ranger: forest green, ochre, bone white
  - Mystic: prismatic purple, ivory, abyss black

## 사용 원칙 (중요)

**claude.ai/design 출력은 React/Tailwind 기반 HTML/CSS이고 Unity UITK는 USS/UXML이라 코드 직접 transpile 부적합.** 시각 reference / 컨셉 / 일관성 / component contract만 흡수한다.

## Unity UITK 흡수 방식 (4 단계)

### 1. Token 추출 → USS variable

각 HTML file의 inline CSS 또는 Tailwind class에서 hex / font-family / spacing px / motion ms 값을 뽑아 `Assets/_Game/UI/Foundation/theme.uss`에 `--sm-*` variable로 transcribe.

예시:
```uss
:root {
    --sm-color-vellum-cream: #F5EAD0;
    --sm-color-gold-corner: #C9A063; /* style-guide.html 추출 */
    --sm-rarity-common: #...;
    --sm-rarity-rare-sapphire: #...;
    --sm-rarity-epic-amethyst: #...;
    --sm-rarity-epic-flame: #...;
    --sm-family-beastkin-undead: #...;
    --sm-family-vanguard: #...;
    --sm-family-striker: #...;
    --sm-family-ranger: #...;
    --sm-family-mystic: #...;
    --sm-spacing-card-gap: 16px;
    --sm-motion-modal-in: 200ms;
}
```

(실제 hex 값은 각 HTML file 열어서 확인 후 transcribe)

### 2. Component contract 참고 → UXML 새 정의

HTML structure는 reference로만 보고 Unity UXML을 새로 정의한다. atom prop matrix는 `pindoc://ux-surface-catalog-v1-draft` 카탈로그 v3에 이미 정의돼 있으므로 그것과 cross-check.

예시 매핑:
- HTML `<div class="hero-portrait-card hero-portrait-card--small">` → UXML `<sm:HeroPortraitCard size="Small" />`
- HTML inner gold corner cap span → UXML `<VisualElement class="sm-corner-cap" />` (USS에서 background로 처리)

### 3. Visual reference로 활용

각 HTML을 브라우저에서 열어서 시각 톤을 매칭. 실제 구현 시 Unity Editor 옆에 두고 톤 비교.

```bash
# 브라우저에서 열기
start docs/02_design/ui/handoff/foundation-pack-v0.2/style-guide.html
start docs/02_design/ui/handoff/foundation-pack-v0.2/surface-town-recruitpack.html
```

### 4. 카탈로그 v3 cross-reference

`pindoc://ux-surface-catalog-v1-draft` 본문에 v0.2 mock 완성 명시 + 이 폴더 path를 cross-link 추가. 후속 surface mock도 같은 폴더 패턴(`foundation-pack-vX.Y/`)으로 누적.

## 다음 단계 (5월 8일 reset 이후 권장)

claude.ai/design 주간 limit 79% 도달로 추가 generation은 **5/8 reset 후**.

후속 surface mock 우선순위 (각 9분 wait 패턴 동일):
1. **Boot.Title** + **Town.RosterGrid** + **Town.SquadBuilder** — 게임 첫 인상 + build loop
2. **Battle shell 전체** — combat 가독성 핵심
3. **Reward.Summary + Reward.AugmentChoice** — decision moment
4. **Dialogue.PortraitScene + Cutscene.Player3D** — narrative + P09 portrait integration test
5. **Common.{ItemDetail / SkillDetail / StatusEffectTooltip / HeroDetail}** — modal/detail
6. **Town.{Refit/Retrain/HeroRecovery/ResumeConfirm} + Settings.Global + Battle.SettingsPanel** — 잔여

각 결과는 `foundation-pack-v0.3/`, `foundation-pack-v0.4/` 같이 새 폴더로 누적.

## 한계 및 주의사항

- **Korean placeholder name**: Claude design이 hero 이름 placeholder로 일본 이름(예: "카에데")을 사용하기도 함 — release 단계엔 한국어 캐릭터 이름 registry로 swap 필요. (메모리 `project_character_naming_korean.md` 정책)
- **Missing brand fonts**: claude.ai/design은 substitute web font로 렌더링 (Pretendard, Noto Sans KR 미설치). Unity 구현 시 실 폰트 import.
- **모든 portrait은 P09 RenderTexture placeholder**: mock에서 2D placeholder image지만 실 구현은 P09 Modular Humanoid runtime camera capture(`PortraitCaptureService`).
- **Setup page "Any other notes"는 LLM에 전달되지 않음**: 의뢰 시 brief는 chat input("Describe what you want to create...")으로 보내야 함. 다음 generation 시 같은 함정 회피.
- **claude.ai/design 출력 코드는 직접 import 금지**: React/Tailwind이고 Unity는 UITK USS/UXML. 시각 reference / 컨셉만 흡수.

## 관련 카탈로그·brief 변경 이력

- 카탈로그 v3 settled (2026-05-05): production scope settled (voice 70-110 line / cutscene 12-15 / hero death KO-only / illust 0 baseline / mobile 병행)
- Brief v2 settled (2026-05-05): atmosphere + responsive + Foundation prop spec + PC/Mobile layout + 의뢰 prompt 템플릿
- Foundation pack v0.2 mock 완성 (2026-05-06): 6 step generation + Town.RecruitPack 1 surface 검증 (이 폴더)
