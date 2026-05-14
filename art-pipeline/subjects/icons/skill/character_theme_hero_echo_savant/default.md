---
slug: character_theme_hero_echo_savant--default
kind: skill_icon_theme_sheet
subject_id: character_theme_hero_echo_savant
variant: default
theme_character_id: hero_echo_savant
source_character_id: hero_echo_savant
presentation_only: true
refs:
  - hero_echo_savant
  - hero_echo_savant:portrait_full
aspect: "1:1"
output_size: "1568x1568"
chroma: "#FF00FF"
skills:
  - knot_arrow
  - weathering_pause
  - dormant_ward
  - external_lexicon
status: rendered
---

# 공한 — Skill Icon Theme Sheet (presentation bridge)

이 subject는 캐릭터 이미지 cycle에 속하지 않는다. `SkillId -> IconId -> Sprite` 프레젠테이션 카탈로그가 확정되기 전까지, 공한의 팔레트와 그물 결사 시각 언어를 빌린 스킬 아이콘 후보 sheet로만 관리한다. 전투 규칙상 캐릭터 전용 스킬 소유권을 의미하지 않는다.

## 산출물

원본 sheet: `art-pipeline/output/character_theme_hero_echo_savant/default.png`

`art-pipeline/output/icons/skill/character_theme_hero_echo_savant/skill_icon_<id>.png` 4장:
- skill_icon_knot_arrow.png — 매듭의 화살 (signature active 1)
- skill_icon_weathering_pause.png — 풍화의 정지 (active 2 default flex)
- skill_icon_dormant_ward.png — 휴면의 보호 (active 3 retrain pool)
- skill_icon_external_lexicon.png — 외부 어휘 (variant late-game, ch4 해금)

인게임 스킬 UI button + tooltip illustration 후보. 추상 메타포 baseline.

## 단린/이빨바람/묵향과 차이점

그물 결사 의례 archer motif로 swap:
- 색감: smoky lavender (#8C7FA0), pale lavender (#C8C1D8), deep muted violet (#5A4C6B), **cool cyan emission (#8FD4E8)**.
- 단린(holy gold), 이빨바람(ochre), 묵향(jade green)과 명확히 분리.
- 격자 매듭 / 화살 / 1800년 시간 / 격자 봉인 motif (그물 결사 의례적 어휘).
- cool cyan emission이 4 cell에 모두 jade(묵향)와 다른 BLUER 톤으로 — 4 lead palette differentiation.

## prompt 명세

```prompt
=== STYLE ANCHOR (skill icon convention) ===
Game UI skill icon style — flat-tier-painterly, centered composition, single focal symbol, magenta background for chroma cleanup.

Skill icons share Lattice Order (그물 결사) ritual archer aesthetic:
- Color palette: smoky lavender (#8C7FA0), pale lavender (#C8C1D8), deep muted violet (#5A4C6B), cool cyan crystal emission (#8FD4E8). Very dark near-black (#15120F) for outlines and shadow.
- Ritual archer / lattice knot / 1800-year silence / cool emission motif themes — disciplined, precise, far-observed. NOT holy radiance, NOT primal/wild, NOT warm sunset glow, NOT magenta/violet sorcery.
- Lattice Order ritual motif (knotted arrows, cyan prism crystals, lattice patterns, hourglass-time) — NOT angelic light, NOT explosive fire, NOT modern wizardry.

The attached character reference is for COLOR PALETTE anchor only — DO NOT include the character figure in any cell. Cells contain abstract symbol/object compositions only.

=== GRID SHEET LAYOUT (CRITICAL) ===
This image is a 2-column × 2-row sprite sheet of 4 separate skill icon illustrations.

CELL SIZE: each cell exactly 768 × 768 px (square icon).
GAP: solid #FF00FF magenta separator, exactly 32 px wide between adjacent cells.
OUTSIDE GRID: solid #FF00FF magenta filling the canvas margin.

CELL POSITIONS (col, row — 0-based, top-left to bottom-right):
- (0,0): skill = knot_arrow
- (1,0): skill = weathering_pause
- (0,1): skill = dormant_ward
- (1,1): skill = external_lexicon

CELL CONTENT — abstract skill icon (per cell):
- Centered focal symbol/object filling roughly 60-70% of the cell.
- NO character figure, NO human silhouette, NO face.
- Background of each cell: solid #FF00FF magenta.
- 1-2 px clean dark outline on the symbol silhouette for clean chroma cutout.
- Subtle painterly shading on the symbol — NOT pure flat color, NOT photorealistic.
- Each cell features SOME cool cyan accent (#8FD4E8) tying to the prism crystal — present in every icon as the conclave's ritual signature.

CELL ALIGNMENT (CRITICAL):
- All 4 cells perfectly aligned in regular 2×2 grid.
- NO overlap, same scale, focal symbol centered in each cell.

=== SKILL ICON DESCRIPTORS (per cell) ===

(0,0) KNOT_ARROW (매듭의 화살): a single arrow shown frontal, slightly diagonal, with a complex lattice-knot tied around the arrow shaft just below the arrowhead. The arrow shaft is pale lavender (#C8C1D8) with smoky lavender (#8C7FA0) feathered fletching at the back. The lattice knot is woven from thin cool cyan thread (#8FD4E8) with faint emission glow at the knot's center. The arrowhead is muted dark violet (#5A4C6B). Around the knot: a faint cool cyan ripple of 2-3 thin concentric arcs suggesting "ritual binding being completed." The visual reads as "an arrow that ties a ritual knot at the moment of impact."

(1,0) WEATHERING_PAUSE (풍화의 정지): an antique hourglass in smoky lavender and deep muted violet, viewed frontal. The hourglass has thin smoky-violet brass frame (#8C7FA0) and pale lavender wooden bases (#C8C1D8). The sand inside is FROZEN mid-fall — suspended grains in the middle of the hourglass, NOT flowing. The sand color is pale lavender. A small cool cyan crystal (#8FD4E8) embedded at the top cap emits a faint cool glow. Around the suspended sand: very faint cool cyan ripples (3-4 thin concentric arcs) suggesting time has paused. The visual reads as "1800 years of weathering held in suspension."

(0,1) DORMANT_WARD (휴면의 보호): a stylized lattice-pattern ward seen frontal — geometric concentric lattice rings (3-4 nested) in pale lavender (#C8C1D8) with cool cyan accents (#8FD4E8) at the lattice intersections. At the center of the lattice: a small abstract figure outline (very vague — just a humanoid silhouette in dormant pose, eyes closed, hands folded at chest, smoky lavender silhouette only — no face details, no clear features). The ward radiates a soft cool cyan glow outward. The visual reads as "ritual sleep wrapped in protective lattice — ally entered dormant state for safety."

(1,1) EXTERNAL_LEXICON (외부 어휘): an open scroll/parchment in pale lavender (#C8C1D8) seen frontal, slightly curled at edges. The scroll's surface is divided into TWO halves separated by a thin gap line: the left half shows old conclave script (geometric lattice symbols in muted dark violet ink), the right half shows the same symbols translated into "external lexicon" (more rounded, calligraphic markings — abstract, NOT readable). At the gap line: a faint cool cyan light spilling from one half to the other (the "translation" being made). NO clearly readable text, just the visual rhythm of two different symbol systems. Around the scroll: faint cool cyan particle wisps rising upward, signifying the act of breaking 1800-year silence. The visual reads as "ancient ritual language meeting external speech for the first time."

=== HARD CONSTRAINTS ===
- Single focal symbol/object per cell.
- NO character figure, NO human face, NO body silhouette in any cell EXCEPT the dormant_ward cell which has a deliberately VAGUE humanoid silhouette in the lattice center (no face, no clear features — just a sleeping outline).
- Background of each cell + grid gaps: solid #FF00FF magenta.
- 1-2 px clean dark outline on symbol silhouette in each cell.
- No magenta or pink tint on any symbol — palette stays in smoky lavender / pale lavender / deep muted violet with cool cyan accents.
- Cool cyan emission (#8FD4E8) in EVERY cell — strictly this hue family. NO warm amber, NO holy gold, NO violet/pink, NO jade green (that is 묵향's color, NOT this character's).
- NO holy radiance, NO warm sunset glow. Cool cyan is COOL — distinctly bluer than green.
- Output: 1568 × 1568 PNG, 2-column × 2-row grid sheet.
```
