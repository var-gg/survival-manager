# Foundation pack v0.5 — Common.HeroDetail (단린 seed)

**Status**: cycle 3 (v0.5) 부분 완료 — HTML + CSS 수집, JS pending (Chrome 보안 차단)

## 산출물

| 파일 | 크기 | 역할 |
| --- | --- | --- |
| `surface-hero-detail.html` | 2.6 KB | wrapper (canvas mount points + font preconnect + css/js link) |
| `hero-detail.css` | 58.5 KB | v0.5 스타일 (token 인용 + 5 state matrix + skill hierarchy 위계) |
| `hero-detail.js` | (~32.2 KB, **pending**) | artboard mount points 동적 채움 (skill grid 4-slot + stance switcher 등) |

> **JS pending 이슈는 v0.4와 동일** — Chrome이 .js 파일 위험 분류로 download bar에서 Keep 클릭 필요.

## 의뢰 핵심 — V1 시스템 룰 시각화

본 cycle은 단린(Dawn Priest) 캐릭터의 4 skill을 V1 system 6 슬롯 룰("동시 active = 2")에 정렬한 매핑을 시각으로 lock-in하는 게 목적.

### Skill grid 4-slot hierarchy

| Slot | 단린 매핑 | UI 위계 |
| --- | --- | --- |
| 1. SIGNATURE LOCK | 봉인의 방패 (sigil_shield) | gold ring + lock icon, 라벨 "정체성" — 평생 박힘 |
| 2. FLEX ACTIVE | 백금의 호위 (platinum_aegis) | highlighted + checkmark, 라벨 "현재" — 지금 들어간 거 |
| 3. FLEX RETRAIN POOL | 재의 정화 (ash_purification) | dimmed + retrain hint icon, 라벨 "재훈련" — 마을에서 갈아 끼울 후보 |
| 4. LATE UNLOCK VARIANT | 신앙의 부재 (faith_absent) | chained icon + "5장 해금" badge — ch5 해금 변종 |

→ V1 "동시 active 2" 룰을 시각으로 표현. 4 slot 다 보여서 캐릭터 fantasy 풍성함도 유지.

본 매핑의 SoT: `pindoc://wiki-combat-character-skill-mapping-v1` (단린 seed). 12 lead 전체 매핑은 별도 task로 후속.

## 결과 요약 (claude.ai/design 출력 인용)

> v0.5 Common.HeroDetail — shipped.
> PC + Mobile artboards stacked, 5 states each (default · skill-hover · equipment-select · stance-preview · locked).
> Token continuity from v0.2/v0.3/v0.4 — vellum cream, gold corner caps, family-tint rib (Vanguard cyan), jewel duotone rarity, P09 RT placeholders.
> Skill hierarchy V1: signature lock (봉인의 방패) · flex active (백금의 호위) · retrain pool (재의 정화) · Ch-5 unlock (신앙의 부재) — only 2 active concurrently.

## Unity UITK 흡수 가이드

2026-05-08 audit 기준으로 본 pack은 **reference usable / Unity 미적용** 상태다. HTML+CSS는 HeroDetail의 정보 구조, 4-slot skill hierarchy, `sm-hd-*` modifier class 설계에 사용 가능하지만, `hero-detail.js`가 pending이므로 dynamic state preview는 아직 완전하지 않다. Unity 구현 Task는 `pindoc://task-hero-detail-v0-5-uitk-adoption`이다.

### 1. Token 추출 (v0.2 baseline 그대로)

`hero-detail.css`는 v0.2 token만 인용 — 새 token 없음. 단린 = Combat Class Vanguard라 family-tint rib는 sapphire(`--sm-fam-vanguard`).

### 2. Component contract 도출

`surface-hero-detail.html` + `hero-detail.css` 읽고 다음 atom/molecule contract 정리:

- `HeroDetailPanel` (좌 portrait + 우 stat sheet 합성 surface)
- `SkillGridSlot` (4 variant: signature-lock / flex-active / flex-retrain / late-unlock)
- `EquipmentRow` (weapon + armor + accessory 3 slot)
- `AffixList` (affix entry + set bonus tier)
- `TraitQuirkRow` (v0.2 mol 재사용)
- `StanceSwitcher` (idle/attack/guard/cast 4 stance horizontal preview)
- `StatSheet` (StatRow 다수 + delta arrow)

### 3. 시각 reference

Unity UITK USS의 modifier class 패턴 (sm-btn / sm-hpc 패턴 이어가기):
- `sm-hd-portrait-rail` (좌측 큰 portrait + 헤더 chip)
- `sm-hd-skill-grid` / `sm-hd-skill-slot` × 4 modifier
  - `sm-hd-skill-slot--signature-lock` (gold ring + lock icon)
  - `sm-hd-skill-slot--flex-active` (highlighted + checkmark)
  - `sm-hd-skill-slot--flex-retrain` (dimmed)
  - `sm-hd-skill-slot--late-unlock` (chained + chapter badge)
- `sm-hd-equipment-row` / `sm-hd-equipment-slot`
- `sm-hd-stance-switcher` / `sm-hd-stance-thumb` × 4
- `sm-hd-affix-list` / `sm-hd-affix-entry`

### 4. 카탈로그 v3 cross-ref + V1 매핑 SoT

- 카탈로그 v3 (`pindoc://ux-surface-catalog-v1-draft`) Town/Common 섹션의 surface spec과 cross-ref
- V1 skill mapping SoT (`pindoc://wiki-combat-character-skill-mapping-v1`) 단린 매핑 그대로 인용

## 변경 이력

- 2026-05-08: cycle 3 (v0.5) Common.HeroDetail mock generation 완료. 단린 4 skill V1 매핑 시각화. claude.ai/design v0.2-v0.4 token 인용 + skill hierarchy 신규 패턴. JS download Chrome 보안 차단으로 pending. HTML + CSS 우선 누적.
