---
slug: character_theme_hero_pack_raider--default
kind: skill_icon_theme_sheet
subject_id: character_theme_hero_pack_raider
variant: default
theme_character_id: hero_pack_raider
source_character_id: hero_pack_raider
presentation_only: true
refs:
  - hero_pack_raider
  - hero_pack_raider:portrait_full
aspect: "1:1"
output_size: "1568x1568"
chroma: "#FF00FF"
skills:
  - wind_read
  - fang_strike
  - pack_position
  - return_path
status: rendered
---

# 이빨바람 — Skill Icon Theme Sheet (presentation bridge)

이 subject는 캐릭터 이미지 cycle에 속하지 않는다. `SkillId -> IconId -> Sprite` 프레젠테이션 카탈로그가 확정되기 전까지, 이빨바람의 팔레트와 사냥꾼 시각 언어를 빌린 스킬 아이콘 후보 sheet로만 관리한다. 전투 규칙상 캐릭터 전용 스킬 소유권을 의미하지 않는다.

## 산출물

원본 sheet: `art-pipeline/output/character_theme_hero_pack_raider/default.png`

`art-pipeline/output/icons/skill/character_theme_hero_pack_raider/skill_icon_<id>.png` 4장:
- skill_icon_wind_read.png — 바람 읽기 (signature active — 정찰 + 회피 보너스)
- skill_icon_fang_strike.png — 이빨 세우기 (flex active default — 거리 줄이기 + 약점 공격)
- skill_icon_pack_position.png — 무리의 자리 (flex active retrain — 동료 보호 + 자리 교환 taunt)
- skill_icon_return_path.png — 회수의 길 (variant late-game — 자기 디버프 + 동료 큰 보호)

인게임 스킬 UI button + tooltip illustration 후보. 추상 메타포 baseline — 캐릭터 figure 등장 X, 시각적 symbol/object만.

## 단린과 차이점

단린의 신성/사제 motif(sigil, glyph, holy light, prayer)를 hunter motif로 전환:
- 사냥꾼/추적자/씨족 어휘 (wind, tooth, pack, trail)
- 색감: 따뜻한 ochre + bone/cream + dark umber. NOT holy gold radiance, NOT magenta/violet, NOT priest ivory.
- 자연 모티프 우선 (바람, 이빨, 발자국, 실/매듭)

## prompt 명세

```prompt
=== STYLE ANCHOR (skill icon convention) ===
Game UI skill icon style — flat-tier-painterly, centered composition, single focal symbol, magenta background for chroma cleanup.

Skill icons share Yri-sol tribe hunter aesthetic:
- Color palette: warm ochre leather (#A96F36), dark umber (#6B472D), bone/cream (#C9B58A), warm amber accent (#D08A2E), and very dark near-black (#15120F) for outline/shadow.
- Hunter / tracker / pack motif themes — natural, primal, sense-driven. NOT holy radiance, NOT cool blue magic, NOT magenta/violet sorcery.
- Wild/instinctual motif (wind, tooth, pack, trail, scent) — NOT religious sigil, NOT angelic light, NOT explosive fire.

The attached character reference is for COLOR PALETTE anchor only — DO NOT include the character figure in any cell. Cells contain abstract symbol/object compositions only.

=== GRID SHEET LAYOUT (CRITICAL) ===
This image is a 2-column × 2-row sprite sheet of 4 separate skill icon illustrations.

CELL SIZE: each cell exactly 768 × 768 px (square icon).
GAP: solid #FF00FF magenta separator, exactly 32 px wide between adjacent cells.
OUTSIDE GRID: solid #FF00FF magenta filling the canvas margin.

CELL POSITIONS (col, row — 0-based, top-left to bottom-right):
- (0,0): skill = wind_read
- (1,0): skill = fang_strike
- (0,1): skill = pack_position
- (1,1): skill = return_path

CELL CONTENT — abstract skill icon (per cell):
- Centered focal symbol/object filling roughly 60-70% of the cell.
- NO character figure, NO human silhouette, NO face.
- Background of each cell: solid #FF00FF magenta.
- 1-2 px clean dark outline on the symbol silhouette for clean chroma cutout.
- Subtle painterly shading on the symbol — NOT pure flat color, NOT photorealistic.

CELL ALIGNMENT (CRITICAL):
- All 4 cells perfectly aligned in regular 2×2 grid.
- NO overlap, same scale, focal symbol centered in each cell.

=== SKILL ICON DESCRIPTORS (per cell) ===

(0,0) WIND_READ (바람 읽기): an open palm seen from the side (just the hand, no arm visible), fingers slightly spread, palm tilted up as if catching wind. Above and around the palm: faint amber wind-current streaks (#D08A2E to #C9B58A family) flowing in horizontal/diagonal direction, with a single small leaf or feather caught in the current. The palm is leather-tanned ochre tone (#A96F36). The wind streaks are very subtle — NOT a magic spell, NOT bright radiance — they are the visual hint of "scent becoming visible." The visual reads as "the hunter reads the wind for prey."

(1,0) FANG_STRIKE (이빨 세우기): a single sharp tooth or short hunting blade tip seen frontal, piercing through a small armor crack or leather seam. The tooth/blade is bone-cream (#C9B58A) with subtle warm amber accent at the base. Below or around the piercing point: a subtle crimson-amber hint suggesting blood/strike impact (very minimal — not gore, just a deep warm umber smear (#6B472D family) at the puncture point). The visual reads as "one strike, exact weak point." NOT a sword in full view — focus is on the precise tip and the crack it enters.

(0,1) PACK_POSITION (무리의 자리): a stylized wolf head silhouette seen frontal, ochre and dark umber tones, with two smaller wolf silhouettes flanking the central one slightly behind/below — protective formation. The central wolf has its ears forward (alert/threat), the flanking wolves are slightly turned (looking outward, guarding sides). Color: central wolf in dark umber (#6B472D) with ochre highlights, flanking wolves in lighter ochre (#A96F36). Background within the cell (NOT the magenta canvas): subtle warm-bone (#C9B58A) circular halo behind the central wolf, suggesting "claimed territory." The visual reads as "the hunter draws the threat to himself, protecting the pack."

(1,1) RETURN_PATH (회수의 길): a winding hunting trail or rope-path seen from above, weaving back on itself in a complex curve, with small footprint marks along it. The path is dark umber (#6B472D) on a bone-cream (#C9B58A) ground. Along the path: small thorns or briar elements at irregular intervals (suggesting cost/sacrifice). At the path's center or near-end point: a small rope knot tied tightly (the "binding" symbol — the hunter binding himself to absorb cost for the pack). NO crimson, NO blood — the cost is suggested by the thorns and the binding knot, not by gore. The visual reads as "returning along the dangerous path so others may go free."

=== HARD CONSTRAINTS ===
- Single focal symbol/object per cell.
- NO character figure, NO human face, NO body silhouette in any cell.
- Background of each cell + grid gaps: solid #FF00FF magenta.
- 1-2 px clean dark outline on symbol silhouette in each cell.
- No magenta or pink tint on any symbol — palette stays in ochre/bone/dark umber/amber warm tones.
- NO holy radiance, NO cool-blue magic, NO violet sorcery in any cell. Sense glow in wind_read stays in warm amber, very subtle.
- Output: 1568 × 1568 PNG, 2-column × 2-row grid sheet.
```
