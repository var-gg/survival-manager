---
slug: hero_dawn_priest--skill_icon_sheet
kind: skill_icon_sheet
subject_id: hero_dawn_priest
variant: skill_icon_sheet
refs:
  - hero_dawn_priest
  - hero_dawn_priest:portrait_full
aspect: "1:1"
output_size: "1568x1568"
chroma: "#FF00FF"
skills:
  - sigil_shield
  - platinum_aegis
  - ash_purification
  - faith_absent
status: rendered
---

# 단린 — Skill Icon Sheet (4 signature skill)

## 산출물

`skill_icon_<id>.png` 4장:
- skill_icon_sigil_shield.png — 봉인의 방패 (active 1)
- skill_icon_platinum_aegis.png — 백금의 호위 (active 2)
- skill_icon_ash_purification.png — 재의 정화 (active 3)
- skill_icon_faith_absent.png — 신앙의 부재 (variant late-game)

인게임 스킬 UI button + tooltip illustration용. 추상 메타포 baseline — 캐릭터 figure 등장 X, 시각적 symbol/object만.

## prompt 명세

```prompt
=== STYLE ANCHOR (skill icon convention) ===
Game UI skill icon style — flat-tier-painterly, centered composition, single focal symbol, magenta background for chroma cleanup.

Skill icons share Eternal Order priest aesthetic:
- Color palette: warm ivory (#D8C8A8), warm gold (#C9A24E with #FFD979 emission), soft amber (#D08A2E), sigil bronze (#A87645).
- Holy/sacred light themes — warm tones, NOT cool blue, NOT violet, NOT magenta.
- Religious-ceremonial motif (sigil, glyph, light, prayer) — NOT explosive fire, NOT dark magic.

The attached character reference is for COLOR PALETTE anchor only — DO NOT include the character figure in any cell. Cells contain abstract symbol/object compositions only.

=== GRID SHEET LAYOUT (CRITICAL) ===
This image is a 2-column × 2-row sprite sheet of 4 separate skill icon illustrations.

CELL SIZE: each cell exactly 768 × 768 px (square icon).
GAP: solid #FF00FF magenta separator, exactly 32 px wide between adjacent cells.
OUTSIDE GRID: solid #FF00FF magenta filling the canvas margin.

CELL POSITIONS (col, row — 0-based, top-left to bottom-right):
- (0,0): skill = sigil_shield
- (1,0): skill = platinum_aegis
- (0,1): skill = ash_purification
- (1,1): skill = faith_absent

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

(0,0) SIGIL_SHIELD (봉인의 방패): an ivory + gold round shield seen frontal, with a glowing Eternal Order sigil emblazoned at center — radiant warm-gold light spreading outward from the sigil in soft ripples (#FFD979 emission). The shield is the focal object. NO Christian cross design — use a stylized circular sigil/glyph (concentric circles, radial glyph rays, or geometric prayer symbol). Ivory body, gold trim. The visual reads as "ritual protection summoned."

(1,0) PLATINUM_AEGIS (백금의 호위): a translucent ivory-gold dome/orb of holy light hovering above an open palm (palm subtly visible at the bottom edge of the cell, fingers curling upward). The dome is the focal object — translucent warm-ivory with gold outline, gentle radiance, particles of light escaping upward. The visual reads as "platinum protective ward summoned for an ally."

(0,1) ASH_PURIFICATION (재의 정화): falling warm-gold ash motes mixed with rising warm light streaks, swirling around a central glyph (a small Eternal Order sigil at center). Ash particles are warm gold/amber (#C9A24E to #D08A2E), NOT gray, NOT cool. Light streaks rise upward through the ash. The visual reads as "purification through sacred fire — ritual cleansing."

(1,1) FAITH_ABSENT (신앙의 부재): an Eternal Order sigil seen frontal but DIMMED — the sigil's light has faded to a cool muted ivory (#D8C8A8 only, NO gold emission, NO radiance). The sigil is partially fragmented, with cool quiet shadow at the edges. NO warm glow. The visual reads as "the priest's faith has gone silent — protection without divine light." Solemn, restrained, mournful — but still composed, not destructive.

=== HARD CONSTRAINTS ===
- Single focal symbol/object per cell.
- NO character figure, NO human face, NO body silhouette in any cell.
- Background of each cell + grid gaps: solid #FF00FF magenta.
- 1-2 px clean dark outline on symbol silhouette in each cell.
- No magenta or pink tint on any symbol — palette stays in ivory/gold/amber/bronze warm tones.
- Cells (0,0)~(0,1): warm radiance OK. Cell (1,1) faith_absent: NO warm radiance, muted ivory only.
- Output: 1568 × 1568 PNG, 2-column × 2-row grid sheet.
```
