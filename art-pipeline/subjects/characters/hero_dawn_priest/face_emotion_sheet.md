---
slug: hero_dawn_priest--face_emotion_sheet
kind: face_emotion_sheet
subject_id: hero_dawn_priest
variant: face_emotion_sheet
refs:
  - hero_dawn_priest
  - hero_dawn_priest:portrait_full
aspect: "2:1"
output_size: "3168x1568"
chroma: "#FF00FF"
emotions:
  - default
  - smile
  - serious
  - shock
  - anger
  - sad
  - cry
  - quiet
status: rendered
---

# 단린 — Face Emotion Sheet (8 emotion frontal)

## 산출물

이 페이지 1번 호출로 8 emotion 얼빡 portrait를 한 sheet에 동시 생성.
이후 `sheet_split.py`로 8개 individual PNG로 분리:
- `portrait_face_default.png`
- `portrait_face_smile.png`
- `portrait_face_serious.png`
- `portrait_face_shock.png`
- `portrait_face_anger.png`
- `portrait_face_sad.png`
- `portrait_face_cry.png`
- `portrait_face_quiet.png`

## prompt 명세

```prompt
=== REF IS THE FULL CHARACTER BLUEPRINT (highest authority) ===
The attached image is the COMPLETE BLUEPRINT for this character. All 8 cells in the grid below are the SAME character with different facial expressions only — same outfit collar, same hair, same eye color, same skin, same head proportions, same identity.

Match the REF for the character's identity:
- Hair: chin-length, warm copper-auburn (#9B643F) — hand-drawn strand definition.
- Eyes: yellow/amber iris (#D8B040 ~ #E0A030 family).
- Skin: light ivory tone (#E8DDC8 family), soft idealized.
- Outfit collar (visible at neck/upper chest area): warm ivory white-platinum tunic with subtle priest cloth collar — matching ref's tunic top.
- Skin tone, eye spacing, nose shape, jaw shape, lip shape: identical across ALL 8 cells.

DO NOT:
- Change the character identity between cells.
- Reinterpret the character into a different archetype.
- Use shoulder-length hair, dark chestnut hair, navy eyes, or platinum-only palette in any cell.

=== GRID SHEET LAYOUT (CRITICAL) ===
This image is a 4-column × 2-row sprite sheet of 8 separate face close-up portraits.

CELL SIZE: each cell exactly 768 × 768 px (square close-up).
GAP: solid #FF00FF magenta separator, exactly 32 px wide between adjacent cells (horizontal and vertical).
OUTSIDE GRID: solid #FF00FF magenta filling the canvas margin.

CELL POSITIONS (column index, row index — 0-based, top-left to bottom-right):
- (col=0, row=0): emotion = default
- (col=1, row=0): emotion = smile
- (col=2, row=0): emotion = serious
- (col=3, row=0): emotion = shock
- (col=0, row=1): emotion = anger
- (col=1, row=1): emotion = sad
- (col=2, row=1): emotion = cry
- (col=3, row=1): emotion = quiet

CELL CONTENT — face close-up (per cell):
- Crop: face only — top of head visible at top edge, neck/collar at bottom edge, ears within frame, eyes positioned on upper-third line.
- Frontal view with subtle 5-10° rotation. Face fills most of the cell.
- HAIR fully framed within the cell (chin-length, no overflow).
- Background of each cell: solid #FF00FF magenta (same as grid gap).
- 1-2 px clean dark outline along the character silhouette in each cell — for clean chroma cutout.

CELL ALIGNMENT (CRITICAL):
- All 8 cells perfectly aligned in a regular 4×2 grid.
- NO overlap between cells.
- NO crooked angles.
- Same scale/zoom across all 8 cells (face roughly same size in each).
- Same head height position within each cell.

=== EMOTION DESCRIPTORS (per cell) ===

(0,0) DEFAULT: composed, calm — the resting state of someone whose duty is her identity. Eyes forward gently, lips closed softly. Warm-but-solemn priest. Faithful but quietly carrying questions. NOT smiling, NOT severe — neutral with quiet warmth.

(1,0) SMILE: quiet warmth — never a broad smile. Small lift at lip corners only, eye softening with subtle crinkle. Restrained warmth — the trust-formation moment with a companion. The smile of a priest who rarely allows herself one.

(2,0) SERIOUS: jaw set firmly, eyes carrying visible weight, brow gathered slightly. Lips closed in a firm line. The self-correction pattern moment — internal weight surfacing through restraint. Eye contact direct, intent.

(3,0) SHOCK: eyes widening (whites visible above iris), pupils dilated, lips parted with breath caught, brow lifted. Internal stop, NOT panic, NOT theatrical scream. The "A. Sternheim" first-crack moment — stillness before realization fully lands. Priest restraint preserved.

(0,1) ANGER: controlled restraint — eyes narrowed sharply, jaw tightened, lips pressed thin. Brow gathered. NOT explosive, NOT loud, NOT teeth-bared. Fury held inside priest discipline — the 선영 광신 직면 비트.

(1,1) SAD: eyes lowered, lashes hide gaze partially, lips pressed thin with slight tremor at corner. Slight downward angle of head. Quiet grief, not weeping. After witnessing cruelty or self-correction.

(2,1) CRY: a single tear traced down one cheek, lips pressed hard, jaw subtly trembling but controlled. Eyes still open but gaze distant. The ch5 인장 반납 직전 비트 — used sparingly. Restrained dignity even in tears.

(3,1) QUIET: no expression — the deepest emotional state. Gaze far away, eyes distant, lips relaxed but lifeless. Hands NOT visible (frame is face only), so no signature gesture — but the emptiness is in the eyes alone. Campaign-defining "broken faith" beat. Stillness, not blankness — alive but emptied.

=== HARD CONSTRAINTS ===
- Single subject per cell, single character across all 8 cells.
- Background of each cell + grid gaps: solid #FF00FF magenta, flat fill, no gradient.
- 1-2 px clean dark outline on character silhouette in each cell.
- No magenta tint on character anywhere — hair, skin, eyes, collar, all avoid #FF00FF and adjacent fuchsia.
- Output: 3168 × 1568 PNG, 4-column × 2-row grid sheet.
```
