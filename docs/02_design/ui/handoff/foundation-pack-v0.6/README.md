# Foundation pack v0.6 — Common detail screens [DEPRECATED]

**상태: deprecated**. 시각 톤 mismatch로 pindoc wiki 시안 갤러리로 SoT 전환.
ItemDetail / SkillDetail / StatusEffectTooltip의 IA는 유효하지만 비주얼 reference는
pindoc 시안을 본다.

- 현재 비주얼 SoT: `pindoc://analysis-town-ui-component-system-v1`
- 전환 결정: `pindoc://claude-design-uitk-adoption-audit-2026-05-08`

**원본 status** (cycle 시점): cycle 4 (v0.6) 부분 완료 — HTML + CSS 수집, JS pending (Chrome Keep)

## 산출물 (단일 page에 3 surface 묶음)

| 파일 | 크기 | 역할 |
| --- | --- | --- |
| `surface-common-details.html` | 3.3 KB | wrapper (3 surface mount points + font preconnect) |
| `common-details.css` | 36.4 KB | modal/sheet/tooltip primitives + 3 surface 스타일 |
| `common-details.js` | (~40.9 KB, **pending**) | state matrix dynamic render |

## 의뢰 핵심 — Detail surface 3종 한 번에

V0.5 HeroDetail의 자연스러운 다음 surface — skill/equipment/status tooltip 클릭 시 호출되는 detail 화면들. cycle pack 권장 순서에서 **사용자가 0.8 우선 끌어올림** 결정.

### 3 surface 명세

| # | Surface | 핵심 컴포넌트 | State |
| --- | --- | --- | --- |
| 1 | **Common.ItemDetail** | 큰 equipment image + affix list + budget bar + set bonus tier + granted skill row + compare row (delta 화살표) | default / comparing / locked-not-eligible / loading |
| 2 | **Common.SkillDetail** | 큰 skill_icon + 효과 description (mincho 인용) + scaling formula (mono) + modifier list + cross-link chip (단린 봉인의 방패) | default / cross-link-active / locked |
| 3 | **Common.StatusEffectTooltip** | Status pill 큰 (v0.2 atom 확장) + duration ring + stack count + cleanse rule (holy/arcane/surge) | default / stacking / cleansable / persistent |

PC = Modal centered overlay (max-width 480-560px) / Mobile = bottomSheet (drag handle, swipe-down dismiss).

## 결과 요약 (claude.ai/design 출력 인용)

> v0.6 delivered. Three detail surfaces — Item / Skill / Status — with PC modals + Mobile bottom sheets and full state matrices, all on vellum cream tokens with no new additions.
> Row 3E fixed — added `.modal-po.is-static` override that resets the absolute centering when modals live inside scaler wrappers, applied it in the JS. The four expanded-modal preview cells now render at full natural height inside their scaled wrappers.

→ verifier가 modal 위치 정렬 issue 발견하고 자동 수정. claude 자체 quality control 작동.

## Korean 인용 검증

prompt에 명시한 한국어 placeholder 정상 반영 확인:
- ItemDetail: 백금 호위 인장 (granted skill: 백금의 호위)
- SkillDetail: 봉인의 방패 (단린 signature lock, "당신의 분노는 당신의 것이 아닙니다" 의례 어휘)
- StatusEffectTooltip: 정화 (단린 ash purification, holy cleanse profile)

## Unity UITK 흡수 가이드

2026-05-08 audit 기준으로 본 pack은 **reference usable / Unity 미적용** 상태다. HTML+CSS는 SkillDetail, ItemDetail, StatusEffectTooltip의 modal/sheet/tooltip contract와 `sm-cd-*` class 설계에 사용 가능하지만, `common-details.js`가 pending이므로 dynamic state preview는 아직 완전하지 않다. Unity 구현 Task는 `pindoc://task-common-detail-v0-6-uitk-adoption`이다.

### 1. Token 추출 (v0.2 baseline 그대로)
새 token 없음 — 모든 색/spacing은 ThemeTokens.uss로 이미 transcribe 끝.

### 2. Component contract 도출

`common-details.css` + `surface-common-details.html` 읽고 다음 atom/molecule contract:

- `ItemDetailModal` (확장 modal: image + affix bar + set bonus + granted skill + compare row)
- `SkillDetailModal` (popover/modal: icon frame + effect text + scaling formula + modifier list + character cross-link chip)
- `StatusEffectTooltipPanel` (small floating + expanded variant: pill + duration ring + stack badge + cleanse rule)
- `AffixBudgetBar` (atom — affix value vs cap, family-tint fill)
- `SetBonusTier` (molecule — 2 of N / 4 of N highlight)
- `ScalingFormulaRow` (atom — mono text "Atk × 1.4 + ..." 패턴)
- `CrossLinkChip` (atom — character portrait sm + skill name + arrow)
- `DurationRing` (atom — turn counter ring)
- `CleanseProfileTag` (atom — holy/arcane/surge tag chip)

### 3. 시각 reference (USS modifier class 패턴)

- `sm-cd-modal` / `sm-cd-modal--item` / `sm-cd-modal--skill` (variant)
- `sm-cd-modal-pc` / `sm-cd-modal-mobile` (responsive)
- `sm-cd-affix-bar` / `sm-cd-affix-fill`
- `sm-cd-cross-link-chip` / `sm-cd-cross-link-arrow`
- `sm-cd-status-pill` / `sm-cd-status-pill--cleansable` / `sm-cd-status-pill--persistent`
- `sm-cd-duration-ring`

### 4. 카탈로그 v3 cross-ref

- ItemDetail: `pindoc://ux-surface-catalog-v1-draft` Common.ItemDetail 섹션과 cross-ref
- SkillDetail의 cross-link 단린 봉인의 방패 → V1 skill mapping SoT(`pindoc://wiki-combat-character-skill-mapping-v1`) 인용
- StatusEffectTooltip cleanse profile → `pindoc://wiki-combat-status-v1` cleanse profile 3종과 cross-ref

## 변경 이력

- 2026-05-08: cycle 4 (v0.6) Common.ItemDetail/SkillDetail/StatusEffectTooltip 3종 한 페이지에 묶어 generation. claude.ai/design v0.2-v0.5 token 인용 + 새 token 없음. verifier가 modal scaler 위치 issue 자동 수정. JS download Chrome 보안 차단으로 pending. HTML + CSS 우선 누적.
- 사용자 결정: 원래 v0.8 plan을 cycle 4 우선으로 끌어올림 (HeroDetail 자연스러운 다음 surface로 detail callout 먼저 lock-in).
