---
slug: hero_dawn_priest--bust_emotion_sheet_R
kind: bust_emotion_sheet
subject_id: hero_dawn_priest
variant: bust_emotion_sheet_R
direction: facing_right
refs:
  - hero_dawn_priest
  - hero_dawn_priest:portrait_full
  - hero_dawn_priest:portrait_face_default
aspect: "1.36:1"
output_size: "3168x2336"
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

# 단린 — Bust Emotion Sheet (facing right, 8 emotion 흉상)

## 산출물 (VN 화면 왼쪽 배치용)

`portrait_bust_<emotion>_R.png` 8장:
- portrait_bust_default_R.png
- portrait_bust_smile_R.png
- portrait_bust_serious_R.png
- portrait_bust_shock_R.png
- portrait_bust_anger_R.png
- portrait_bust_sad_R.png
- portrait_bust_cry_R.png
- portrait_bust_quiet_R.png

facing camera-right (face turned 15-20° toward right of frame, body rotated so right shoulder closer to camera) — VN dialogue에서 화면 왼쪽 위치 캐릭터로 사용. 별도 facing_left sheet도 후속 생성.

## prompt 명세

```prompt
=== REF IS THE FULL CHARACTER BLUEPRINT (highest authority) ===
The attached image is the COMPLETE BLUEPRINT for this character. All 8 cells in the grid below are the SAME character with different facial expressions only — same outfit, same hair, same eye color, same skin, same proportions, same identity, same facing direction.

Match the REF for the character's identity:
- Hair: chin-length, warm copper-auburn (#9B643F) — hand-drawn strand definition.
- Eyes: yellow/amber iris (#D8B040 ~ #E0A030 family).
- Skin: light ivory tone, soft idealized.
- Outfit (visible at chest/shoulders/arms): layered priest clothing — purple/maroon shoulder cape on top, ivory tunic underneath (#D8C8A8 warm ivory), brown leather belt at waist (just visible at frame bottom). Match REF.
- Skin tone, eye spacing, nose shape, jaw shape, lip shape, hair shape: identical across ALL 8 cells.

DO NOT:
- Change the character identity between cells.
- Use shoulder-length hair, dark chestnut hair, navy eyes.
- Add cape (other than the purple/maroon shoulder cape from ref), hood, large pectoral cross, body-spanning rosary, or paladin plate armor.
- Change the facing direction between cells.

=== GRID SHEET LAYOUT (CRITICAL) ===
This image is a 4-column × 2-row sprite sheet of 8 separate bust portraits of the SAME character.

CELL SIZE: each cell exactly 768 × 1152 px (vertical bust portrait).
GAP: solid #FF00FF magenta separator, exactly 32 px wide between adjacent cells.
OUTSIDE GRID: solid #FF00FF magenta filling the canvas margin.

CELL POSITIONS (col, row — 0-based, top-left to bottom-right):
- (0,0): emotion = default
- (1,0): emotion = smile
- (2,0): emotion = serious
- (3,0): emotion = shock
- (0,1): emotion = anger
- (1,1): emotion = sad
- (2,1): emotion = cry
- (3,1): emotion = quiet

CELL CONTENT — bust portrait (per cell):
- Crop: head + shoulders + upper chest (down to collarbone area, just above the belt). Hair fully framed top.
- FACING DIRECTION (CRITICAL — same in ALL cells): face turned 15-20° toward the RIGHT side of the frame. Eyes look toward the right. Body rotated so right shoulder is closer to camera. Mental anchor: "this character is positioned on the LEFT side of a dialogue scene, looking toward the conversation partner on the right."
- Background of each cell: solid #FF00FF magenta.
- 1-2 px clean dark outline on the character silhouette in each cell.

CELL ALIGNMENT (CRITICAL):
- All 8 cells perfectly aligned in regular 4×2 grid.
- NO overlap, NO crooked angles.
- Same scale, same head height position, same body rotation across all cells.

=== EMOTION DESCRIPTORS (per cell) ===

(0,0) DEFAULT: composed, calm — resting state of priest carrying duty as identity. Eyes forward (toward right of frame), lips closed gently. Warm-but-solemn, faithful but quietly carrying questions. NOT smiling, NOT severe.

(1,0) SMILE: quiet warmth, never broad. Small lift at lip corners only, eye softening. Restrained — the trust-formation moment. The smile of a priest who rarely allows herself one.

(2,0) SERIOUS: jaw set firmly, eyes carrying visible weight, brow gathered slightly. Lips closed in a firm line. Self-correction pattern moment — internal weight surfacing through restraint.

(3,0) SHOCK: eyes widening (whites visible above iris), pupils dilated, lips parted with breath caught, brow lifted. Internal stop, NOT panic, NOT theatrical. The "A. Sternheim" first-crack moment — stillness before realization fully lands.

(0,1) ANGER: controlled restraint — eyes narrowed sharply, jaw tightened, lips pressed thin. NOT explosive, NOT loud. Fury held inside priest discipline.

(1,1) SAD: eyes lowered, lashes hide gaze partially, lips pressed thin with slight tremor. Slight downward angle of head. Quiet grief, not weeping.

(2,1) CRY: a single tear traced down one cheek, lips pressed hard, jaw subtly trembling but controlled. Eyes still open but gaze distant. Used sparingly — restrained dignity even in tears.

(3,1) QUIET: no expression — deepest emotional state. Gaze far away, eyes distant, lips relaxed but lifeless. The campaign-defining "broken faith" beat — alive but emptied.

=== HARD CONSTRAINTS ===
- Single subject per cell, single character across all 8 cells, all FACING RIGHT.
- Background of each cell + grid gaps: solid #FF00FF magenta, flat fill, no gradient.
- 1-2 px clean dark outline on character silhouette in each cell.
- No magenta tint on character anywhere.
- Output: 3168 × 2336 PNG, 4-column × 2-row grid sheet.
```
